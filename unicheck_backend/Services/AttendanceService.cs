using unicheck_backend.Data;
using unicheck_backend.Models.DTOs;
using unicheck_backend.Models.Entities;
using unicheck_backend.Models.Enums;
using Microsoft.EntityFrameworkCore;

namespace unicheck_backend.Services;

/// <summary>
/// Xử lý toàn bộ nghiệp vụ điểm danh:
/// StartSession → CheckIn (QR + Face + GPS) → EndSession
/// </summary>
public partial class AttendanceService
{
    private readonly AppDbContext _db;
    private readonly QrCodeService _qrService;
    private readonly FaceService _faceService;
    private readonly GpsService _gpsService;
    private readonly AttendanceNotifier _notifier;

    public AttendanceService(
        AppDbContext db,
        QrCodeService qrService,
        FaceService faceService,
        GpsService gpsService,
        AttendanceNotifier notifier)
    {
        _db          = db;
        _qrService   = qrService;
        _faceService = faceService;
        _gpsService  = gpsService;
        _notifier    = notifier;
    }

    // ── CHECK IN ─────────────────────────────────────────────────────────────

    /// <summary>
    /// Luồng check-in đầy đủ:
    /// 1. Validate QR token
    /// 2. Kiểm tra SV đã check-in chưa
    /// 3. Xác thực khuôn mặt (FaceService → Python AI)
    /// 4. Xác thực GPS (GpsService Haversine)
    /// 5. Cập nhật Attendance record
    /// 6. Broadcast real-time tới LiveSession.razor (AttendanceNotifier)
    /// </summary>
    public async Task<CheckInResponseDto> CheckIn(CheckInRequestDto dto, string studentId)
    {
        // 1. Tìm session đang active có QR token khớp
        var session = await _db.AttendanceSessions
            .Include(s => s.Schedule)
                .ThenInclude(sc => sc.Room)
            .Include(s => s.Schedule)
                .ThenInclude(sc => sc.CourseClass)
            .FirstOrDefaultAsync(s => s.QrToken == dto.QrToken && s.IsActive);

        if (session is null)
            return Fail("QR code không hợp lệ hoặc đã hết hạn.");

        // Validate token expiry
        if (DateTime.UtcNow >= session.QrTokenExpiry)
            return Fail("QR code đã hết hạn. Vui lòng quét lại.");

        // 2. Tìm Attendance record của SV trong session này
        var attendance = await _db.Attendances
            .FirstOrDefaultAsync(a => a.SessionId == session.SessionId && a.StudentId == studentId);

        if (attendance is null)
            return Fail("Bạn không có trong danh sách lớp học này.");

        if (attendance.CheckInTime.HasValue)
            return Fail("Bạn đã điểm danh rồi.");

        // Kiểm tra giới hạn thời gian check-in (> 30 phút → không cho)
        var scheduleStartUtc = session.Schedule.Date.Date + session.Schedule.StartTime
                               - TimeSpan.FromHours(7); // UTC+7 → UTC
        var minutesLate = (DateTime.UtcNow - scheduleStartUtc).TotalMinutes;
        if (minutesLate > 30)
            return Fail("Đã quá 30 phút kể từ đầu giờ học. Không thể điểm danh.");

        // 3. Xác thực khuôn mặt
        var student = await _db.Students.FindAsync(studentId);
        if (student is null)
            return Fail("Không tìm thấy thông tin sinh viên.");

        if (string.IsNullOrEmpty(student.FaceEmbedding))
            return Fail("Sinh viên chưa đăng ký khuôn mặt.");

        var faceResult = await _faceService.VerifyFace(dto.FaceImageBase64, student.FaceEmbedding);

        // 4. Xác thực GPS
        var room = session.Schedule.Room;
        var distance = _gpsService.HaversineDistance(
            dto.Latitude, dto.Longitude,
            room.Latitude, room.Longitude);
        var locationVerified = distance <= room.RadiusMeter;

        // 5. Xác định trạng thái
        var status = minutesLate <= 15 ? AttendanceStatus.PRESENT : AttendanceStatus.LATE;

        // Cập nhật Attendance
        attendance.CheckInTime      = DateTime.UtcNow;
        attendance.Latitude         = dto.Latitude;
        attendance.Longitude        = dto.Longitude;
        attendance.FaceVerified     = faceResult.IsMatch;
        attendance.FaceConfidence   = faceResult.Confidence;
        attendance.LocationVerified = locationVerified;
        attendance.DistanceMeter    = distance;
        attendance.Status           = status;
        attendance.AbsenceType      = AbsenceType.NONE;

        await _db.SaveChangesAsync();

        // 6. Broadcast real-time tới Blazor LiveSession
        _notifier.NotifyCheckedIn(session.SessionId, new CheckedInStudentInfo
        {
            StudentId        = studentId,
            FullName         = student.FullName,
            CheckInTime      = DateTime.UtcNow,
            FaceVerified     = faceResult.IsMatch,
            FaceConfidence   = faceResult.Confidence,
            LocationVerified = locationVerified,
            Status           = status,
        });

        return new CheckInResponseDto
        {
            Success          = true,
            Status           = status.ToString(),
            Message          = status == AttendanceStatus.PRESENT ? "Điểm danh thành công!" : "Điểm danh muộn.",
            FaceVerified     = faceResult.IsMatch,
            FaceConfidence   = faceResult.Confidence,
            LocationVerified = locationVerified,
            DistanceMeter    = distance,
        };
    }

    private static CheckInResponseDto Fail(string msg) => new() { Success = false, Message = msg };

    // ── START SESSION ─────────────────────────────────────────────────────────

    /// <summary>
    /// Bắt đầu phiên điểm danh cho 1 buổi học.
    /// 1. Xác minh giảng viên có quyền (lecturer sở hữu schedule này)
    /// 2. Kiểm tra chưa có phiên active cho schedule này
    /// 3. Tạo AttendanceSession + QR Token
    /// 4. Tạo Attendance records (ABSENT) cho tất cả SV enrolled
    /// </summary>
    /// <returns>SessionId vừa tạo</returns>
    public async Task<AttendanceSession> StartSession(int scheduleId, int lecturerId)
    {
        // 1. Load schedule + verify lecturer
        var schedule = await _db.Schedules
            .Include(s => s.CourseClass)
                .ThenInclude(cc => cc.Course)
            .Include(s => s.Room)
            .FirstOrDefaultAsync(s => s.ScheduleId == scheduleId);

        if (schedule is null)
            throw new InvalidOperationException("Schedule không tồn tại.");

        if (schedule.CourseClass.LecturerId != lecturerId)
            throw new UnauthorizedAccessException("Bạn không có quyền bắt đầu phiên cho buổi học này.");

        // 2. Kiểm tra phiên đã tồn tại
        bool hasActiveSession = await _db.AttendanceSessions
            .AnyAsync(s => s.ScheduleId == scheduleId && s.IsActive);

        if (hasActiveSession)
            throw new InvalidOperationException("Đã có phiên điểm danh đang hoạt động cho buổi học này.");

        // 3. Tạo AttendanceSession
        var qrToken = _qrService.GenerateToken();
        var session = new AttendanceSession
        {
            ScheduleId    = scheduleId,
            QrToken       = qrToken,
            QrTokenExpiry = DateTime.UtcNow.AddSeconds(20),
            StartTime     = DateTime.UtcNow,
            IsActive      = true,
        };

        _db.AttendanceSessions.Add(session);
        await _db.SaveChangesAsync(); // Cần save để có SessionId

        // 4. Lấy danh sách SV enrolled → tạo Attendance records ABSENT
        var enrolledStudentIds = await _db.Enrollments
            .Where(e => e.ClassId == schedule.ClassId)
            .Select(e => e.StudentId)
            .ToListAsync();

        var attendances = enrolledStudentIds.Select(studentId => new Attendance
        {
            SessionId  = session.SessionId,
            StudentId  = studentId,
            Status     = AttendanceStatus.ABSENT,
            AbsenceType = AbsenceType.NONE,
        }).ToList();

        _db.Attendances.AddRange(attendances);
        await _db.SaveChangesAsync();

        return session;
    }

    // ── END SESSION ──────────────────────────────────────────────────────────

    /// <summary>
    /// Kết thúc phiên điểm danh.
    /// 1. Đánh dấu session inactive, ghi EndTime
    /// 2. Cập nhật SV có LeaveRequest approved → Status = EXCUSED
    /// </summary>
    public async Task EndSession(int sessionId)
    {
        var session = await _db.AttendanceSessions
            .Include(s => s.Schedule)
            .FirstOrDefaultAsync(s => s.SessionId == sessionId);

        if (session is null)
            throw new InvalidOperationException("Session không tồn tại.");

        // 1. Đóng session
        session.IsActive = false;
        session.EndTime  = DateTime.UtcNow;

        // 2. Tìm LeaveRequest đã Approved cho buổi học này
        var approvedLeaveStudentIds = await _db.LeaveRequests
            .Where(r => r.ScheduleId == session.ScheduleId
                     && r.Status == LeaveRequestStatus.APPROVED)
            .Select(r => r.StudentId)
            .ToListAsync();

        if (approvedLeaveStudentIds.Any())
        {
            // Cập nhật Attendance → EXCUSED cho các SV có đơn nghỉ được duyệt
            var excusedAttendances = await _db.Attendances
                .Where(a => a.SessionId == sessionId
                         && approvedLeaveStudentIds.Contains(a.StudentId)
                         && a.Status == AttendanceStatus.ABSENT)
                .ToListAsync();

            foreach (var att in excusedAttendances)
            {
                att.Status      = AttendanceStatus.EXCUSED;
                att.AbsenceType = AbsenceType.EXCUSED;
            }
        }

        await _db.SaveChangesAsync();
    }

    // ── HELPERS ──────────────────────────────────────────────────────────────

    /// <summary>Lấy thông tin session kèm schedule + room + course.</summary>
    public async Task<AttendanceSession?> GetSessionWithDetails(int sessionId)
    {
        return await _db.AttendanceSessions
            .Include(s => s.Schedule)
                .ThenInclude(sc => sc.CourseClass)
                    .ThenInclude(cc => cc.Course)
            .Include(s => s.Schedule)
                .ThenInclude(sc => sc.Room)
            .FirstOrDefaultAsync(s => s.SessionId == sessionId);
    }

    /// <summary>Đếm tổng số SV đã enrolled trong lớp học phần qua schedule.</summary>
    public async Task<int> CountEnrolledStudents(int scheduleId)
    {
        var schedule = await _db.Schedules
            .AsNoTracking()
            .FirstOrDefaultAsync(s => s.ScheduleId == scheduleId);

        if (schedule is null) return 0;

        return await _db.Enrollments
            .CountAsync(e => e.ClassId == schedule.ClassId);
    }
}
