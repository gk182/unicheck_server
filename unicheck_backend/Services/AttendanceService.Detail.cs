using unicheck_backend.Data;
using unicheck_backend.Models.Enums;
using Microsoft.EntityFrameworkCore;

namespace unicheck_backend.Services;

/// <summary>
/// Cập nhật trạng thái điểm danh thủ công bởi giảng viên (GV sửa tay trên ClassDetail.razor).
/// Cũng serve cho ExportService để xuất dữ liệu Excel.
/// </summary>
public partial class AttendanceService
{
    // ── UPDATE STATUS (GV SỬA TAY) ───────────────────────────────────────────

    /// <summary>
    /// GV sửa trạng thái điểm danh 1 SV trong 1 session cụ thể.
    /// </summary>
    public async Task UpdateStatus(int sessionId, string studentId, AttendanceStatus newStatus, string? note = null)
    {
        var att = await _db.Attendances
            .FirstOrDefaultAsync(a => a.SessionId == sessionId && a.StudentId == studentId);

        if (att is null)
            throw new InvalidOperationException($"Không tìm thấy bản ghi điểm danh của SV {studentId}.");

        att.Status = newStatus;
        if (note is not null) att.Note = note;

        // Đồng bộ AbsenceType theo status
        att.AbsenceType = newStatus switch
        {
            AttendanceStatus.EXCUSED => AbsenceType.EXCUSED,
            AttendanceStatus.ABSENT  => AbsenceType.UNEXCUSED,
            _                        => AbsenceType.NONE,
        };

        await _db.SaveChangesAsync();
    }

    // ── GET SESSION DETAIL (cho ClassDetail.razor) ───────────────────────────

    /// <summary>
    /// Lấy toàn bộ danh sách điểm danh của 1 session để hiển thị trên ClassDetail.
    /// </summary>
    public async Task<AttendanceSessionDetailDto> GetSessionDetail(int sessionId)
    {
        var session = await _db.AttendanceSessions
            .Include(s => s.Schedule)
                .ThenInclude(sc => sc.CourseClass)
                    .ThenInclude(cc => cc.Course)
            .Include(s => s.Schedule)
                .ThenInclude(sc => sc.Room)
            .FirstOrDefaultAsync(s => s.SessionId == sessionId)
            ?? throw new InvalidOperationException("Session không tồn tại.");

        var attendances = await _db.Attendances
            .Include(a => a.Student)
            .Where(a => a.SessionId == sessionId)
            .OrderBy(a => a.StudentId)
            .ToListAsync();

        return new AttendanceSessionDetailDto
        {
            SessionId  = sessionId,
            CourseName = session.Schedule.CourseClass.Course.CourseName,
            Date       = session.Schedule.Date.ToString("dd/MM/yyyy"),
            RoomName   = session.Schedule.Room.RoomName,
            Rows       = attendances.Select(a => new AttendanceRowDetailDto
            {
                AttendanceId     = a.Id,
                StudentId        = a.StudentId,
                FullName         = a.Student.FullName,
                Status           = a.Status.ToString(),
                CheckInTime      = a.CheckInTime.HasValue
                                    ? a.CheckInTime.Value.ToLocalTime().ToString("HH:mm")
                                    : null,
                FaceConfidence   = a.FaceConfidence,
                LocationVerified = a.LocationVerified,
                DistanceMeter    = a.DistanceMeter,
                Note             = a.Note,
            }).ToList()
        };
    }
}

// ── DTOs ──────────────────────────────────────────────────────────────────────

public class AttendanceSessionDetailDto
{
    public int    SessionId  { get; set; }
    public string CourseName { get; set; } = "";
    public string Date       { get; set; } = "";
    public string RoomName   { get; set; } = "";
    public List<AttendanceRowDetailDto> Rows { get; set; } = new();
}

public class AttendanceRowDetailDto
{
    public int     AttendanceId     { get; set; }
    public string  StudentId        { get; set; } = "";
    public string  FullName         { get; set; } = "";
    public string  Status           { get; set; } = "ABSENT";
    public string? CheckInTime      { get; set; }
    public double? FaceConfidence   { get; set; }
    public bool    LocationVerified { get; set; }
    public double? DistanceMeter    { get; set; }
    public string? Note             { get; set; }
}
