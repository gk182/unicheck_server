using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using unicheck_backend.Data;
using unicheck_backend.Models.DTOs;
using unicheck_backend.Models.Entities;

namespace unicheck_backend.Controllers.Api;

[ApiController]
[Route("api/admin")]
[Authorize(AuthenticationSchemes = CookieAuthenticationDefaults.AuthenticationScheme + "," + JwtBearerDefaults.AuthenticationScheme, Roles = "ADMIN")]
public class AdminApiController : ControllerBase
{
    private readonly AppDbContext _db;

    public AdminApiController(AppDbContext db)
    {
        _db = db;
    }

    [HttpGet("metadata")]
    public async Task<IActionResult> GetMetadata()
    {
        var courses = await _db.Courses
            .OrderBy(c => c.CourseCode)
            .Select(c => new
            {
                c.CourseId,
                c.CourseCode,
                c.CourseName
            })
            .ToListAsync();

        var lecturers = await _db.Lecturers
            .OrderBy(l => l.FullName)
            .Select(l => new
            {
                l.LecturerId,
                l.FullName,
                l.Email
            })
            .ToListAsync();

        var rooms = await _db.Rooms
            .OrderBy(r => r.RoomName)
            .Select(r => new
            {
                r.RoomId,
                r.RoomName
            })
            .ToListAsync();

        var studentCount = await _db.Students.CountAsync();

        return Ok(new
        {
            courses,
            lecturers,
            rooms,
            studentCount
        });
    }

    [HttpGet("classes")]
    public async Task<IActionResult> GetClasses(
        [FromQuery] string? academicYear,
        [FromQuery] int? semester,
        [FromQuery] int? lecturerId,
        [FromQuery] string? courseCode)
    {
        var query = _db.CourseClasses
            .Include(c => c.Course)
            .Include(c => c.Lecturer)
            .AsQueryable();

        if (!string.IsNullOrWhiteSpace(academicYear))
        {
            query = query.Where(c => c.AcademicYear == academicYear.Trim());
        }

        if (semester.HasValue)
        {
            query = query.Where(c => c.Semester == semester.Value);
        }

        if (lecturerId.HasValue)
        {
            query = query.Where(c => c.LecturerId == lecturerId.Value);
        }

        if (!string.IsNullOrWhiteSpace(courseCode))
        {
            var normalized = courseCode.Trim().ToLower();
            query = query.Where(c => c.Course.CourseCode.ToLower() == normalized);
        }

        var classes = await query
            .OrderByDescending(c => c.AcademicYear)
            .ThenBy(c => c.Semester)
            .ThenBy(c => c.Course.CourseCode)
            .Select(c => new
            {
                c.ClassId,
                c.CourseId,
                c.Course.CourseCode,
                c.Course.CourseName,
                c.GroupCode,
                c.Semester,
                c.AcademicYear,
                c.LecturerId,
                LecturerName = c.Lecturer.FullName,
                EnrollmentCount = c.Enrollments.Count,
                ScheduleCount = c.Schedules.Count
            })
            .ToListAsync();

        return Ok(classes);
    }

    [HttpGet("schedules")]
    public async Task<IActionResult> GetSchedules([FromQuery] int? classId = null)
    {
        var query = _db.Schedules
            .Include(s => s.Room)
            .Include(s => s.CourseClass)
            .ThenInclude(cc => cc.Course)
            .AsQueryable();

        if (classId.HasValue)
        {
            query = query.Where(s => s.ClassId == classId.Value);
        }

        var schedules = await query
            .OrderByDescending(s => s.Date)
            .ThenByDescending(s => s.StartTime)
            .Take(300)
            .Select(s => new
            {
                s.ScheduleId,
                s.ClassId,
                s.RoomId,
                RoomName = s.Room.RoomName,
                CourseCode = s.CourseClass.Course.CourseCode,
                GroupCode = s.CourseClass.GroupCode,
                s.Date,
                s.StartTime,
                s.EndTime
            })
            .ToListAsync();

        return Ok(schedules);
    }

    [HttpPost("classes")]
    public async Task<IActionResult> CreateClass([FromBody] CreateClassRequestDto request)
    {
        if (!ModelState.IsValid)
        {
            return BadRequest(ModelState);
        }

        if (request.StartsWithInvalidAcademicYear())
        {
            return BadRequest(new { message = "AcademicYear phải theo định dạng YYYY-YYYY." });
        }

        var courseExists = await _db.Courses.AnyAsync(c => c.CourseId == request.CourseId);
        if (!courseExists)
        {
            return NotFound(new { message = "Course không tồn tại." });
        }

        var lecturerExists = await _db.Lecturers.AnyAsync(l => l.LecturerId == request.LecturerId);
        if (!lecturerExists)
        {
            return NotFound(new { message = "Lecturer không tồn tại." });
        }

        var normalizedGroupCode = request.GroupCode.Trim();
        var normalizedYear = request.AcademicYear.Trim();

        var classExists = await _db.CourseClasses.AnyAsync(c =>
            c.CourseId == request.CourseId &&
            c.Semester == request.Semester &&
            c.AcademicYear == normalizedYear &&
            c.GroupCode == normalizedGroupCode);

        if (classExists)
        {
            return Conflict(new { message = "Lớp học phần đã tồn tại." });
        }

        var entity = new CourseClass
        {
            CourseId = request.CourseId,
            LecturerId = request.LecturerId,
            Semester = request.Semester,
            AcademicYear = normalizedYear,
            GroupCode = normalizedGroupCode
        };

        _db.CourseClasses.Add(entity);
        await _db.SaveChangesAsync();

        return CreatedAtAction(nameof(GetClasses), new { id = entity.ClassId }, new
        {
            entity.ClassId,
            entity.CourseId,
            entity.LecturerId,
            entity.Semester,
            entity.AcademicYear,
            entity.GroupCode
        });
    }

    [HttpPut("classes/{classId:int}/lecturer")]
    public async Task<IActionResult> AssignLecturer(int classId, [FromBody] AssignLecturerRequestDto request)
    {
        if (!ModelState.IsValid)
        {
            return BadRequest(ModelState);
        }

        var courseClass = await _db.CourseClasses.FirstOrDefaultAsync(c => c.ClassId == classId);
        if (courseClass is null)
        {
            return NotFound(new { message = "Class không tồn tại." });
        }

        var lecturer = await _db.Lecturers.FirstOrDefaultAsync(l => l.LecturerId == request.LecturerId);
        if (lecturer is null)
        {
            return NotFound(new { message = "Lecturer không tồn tại." });
        }

        var hasActiveSession = await _db.AttendanceSessions
            .Include(s => s.Schedule)
            .AnyAsync(s => s.Schedule.ClassId == classId && s.IsActive);

        if (hasActiveSession)
        {
            return BadRequest(new { message = "Không thể đổi giảng viên khi lớp đang có phiên điểm danh hoạt động." });
        }

        courseClass.LecturerId = lecturer.LecturerId;
        await _db.SaveChangesAsync();

        return Ok(new
        {
            courseClass.ClassId,
            courseClass.LecturerId,
            LecturerName = lecturer.FullName
        });
    }

    [HttpPut("schedules/{scheduleId:int}")]
    public async Task<IActionResult> UpdateSchedule(int scheduleId, [FromBody] UpdateScheduleRequestDto request)
    {
        if (!ModelState.IsValid)
        {
            return BadRequest(ModelState);
        }

        if (request.StartTime >= request.EndTime)
        {
            return BadRequest(new { message = "StartTime phải nhỏ hơn EndTime." });
        }

        var schedule = await _db.Schedules.FirstOrDefaultAsync(s => s.ScheduleId == scheduleId);
        if (schedule is null)
        {
            return NotFound(new { message = "Schedule không tồn tại." });
        }

        var room = await _db.Rooms.FirstOrDefaultAsync(r => r.RoomId == request.RoomId);
        if (room is null)
        {
            return NotFound(new { message = "Room không tồn tại." });
        }

        var hasActiveSession = await _db.AttendanceSessions
            .AnyAsync(s => s.ScheduleId == scheduleId && s.IsActive);

        if (hasActiveSession)
        {
            return BadRequest(new { message = "Không thể chỉnh lịch khi phiên điểm danh đang hoạt động." });
        }

        var hasRoomConflict = await _db.Schedules.AnyAsync(s =>
            s.ScheduleId != scheduleId &&
            s.RoomId == request.RoomId &&
            s.Date.Date == request.Date.Date &&
            s.StartTime < request.EndTime &&
            s.EndTime > request.StartTime);

        if (hasRoomConflict)
        {
            return Conflict(new { message = "Phòng học đã bị trùng lịch trong khoảng thời gian này." });
        }

        schedule.Date = request.Date.Date;
        schedule.StartTime = request.StartTime;
        schedule.EndTime = request.EndTime;
        schedule.RoomId = request.RoomId;
        await _db.SaveChangesAsync();

        return Ok(new
        {
            schedule.ScheduleId,
            schedule.ClassId,
            schedule.Date,
            schedule.StartTime,
            schedule.EndTime,
            schedule.RoomId,
            RoomName = room.RoomName
        });
    }

    [HttpPost("enrollments")]
    public async Task<IActionResult> EnrollStudents([FromBody] EnrollStudentsRequestDto request)
    {
        if (!ModelState.IsValid)
        {
            return BadRequest(ModelState);
        }

        var courseClass = await _db.CourseClasses.FirstOrDefaultAsync(c => c.ClassId == request.ClassId);
        if (courseClass is null)
        {
            return NotFound(new { message = "Class không tồn tại." });
        }

        var normalizedStudentIds = request.StudentIds
            .Where(s => !string.IsNullOrWhiteSpace(s))
            .Select(s => s.Trim())
            .Distinct()
            .ToList();

        if (normalizedStudentIds.Count == 0)
        {
            return BadRequest(new { message = "Danh sách StudentIds rỗng." });
        }

        var result = await ProcessEnrollmentAsync(request.ClassId, normalizedStudentIds);
        return Ok(result);
    }

    [HttpPost("enrollments/import-csv")]
    public async Task<IActionResult> ImportCsvEnrollment([FromBody] ImportEnrollmentCsvRequestDto request)
    {
        if (!ModelState.IsValid)
        {
            return BadRequest(ModelState);
        }

        var courseClass = await _db.CourseClasses.FirstOrDefaultAsync(c => c.ClassId == request.ClassId);
        if (courseClass is null)
        {
            return NotFound(new { message = "Class không tồn tại." });
        }

        if (string.IsNullOrWhiteSpace(request.CsvContent))
        {
            return BadRequest(new { message = "Nội dung CSV rỗng." });
        }

        var rows = request.CsvContent
            .Split(new[] { '\n', '\r' }, StringSplitOptions.RemoveEmptyEntries)
            .Select(r => r.Trim())
            .Where(r => !string.IsNullOrWhiteSpace(r))
            .ToList();

        var studentIds = new List<string>();
        foreach (var row in rows)
        {
            var firstCell = row.Split(new[] { ',', ';', '\t' }, StringSplitOptions.None)
                .FirstOrDefault()?.Trim() ?? string.Empty;

            if (string.IsNullOrWhiteSpace(firstCell))
            {
                continue;
            }

            if (firstCell.Equals("studentid", StringComparison.OrdinalIgnoreCase)
                || firstCell.Equals("mssv", StringComparison.OrdinalIgnoreCase)
                || firstCell.Equals("student_id", StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            studentIds.Add(firstCell);
        }

        var normalizedStudentIds = studentIds
            .Distinct()
            .ToList();

        if (normalizedStudentIds.Count == 0)
        {
            return BadRequest(new { message = "Không đọc được MSSV hợp lệ từ CSV." });
        }

        var result = await ProcessEnrollmentAsync(request.ClassId, normalizedStudentIds);
        return Ok(result);
    }

    private async Task<EnrollmentResultDto> ProcessEnrollmentAsync(int classId, List<string> normalizedStudentIds)
    {
        var existingStudents = await _db.Students
            .Where(s => normalizedStudentIds.Contains(s.StudentId))
            .Select(s => s.StudentId)
            .ToListAsync();

        var failedStudentIds = normalizedStudentIds.Except(existingStudents).ToList();

        var existingEnrollments = await _db.Enrollments
            .Where(e => e.ClassId == classId && normalizedStudentIds.Contains(e.StudentId))
            .Select(e => e.StudentId)
            .ToListAsync();

        var toEnroll = existingStudents.Except(existingEnrollments).ToList();

        foreach (var studentId in toEnroll)
        {
            _db.Enrollments.Add(new Enrollment
            {
                ClassId = classId,
                StudentId = studentId
            });
        }

        if (toEnroll.Count > 0)
        {
            await _db.SaveChangesAsync();
        }

        return new EnrollmentResultDto
        {
            ClassId = classId,
            TotalAttempted = normalizedStudentIds.Count,
            SuccessCount = toEnroll.Count,
            SkippedCount = existingEnrollments.Count,
            FailedCount = failedStudentIds.Count,
            FailedStudentIds = failedStudentIds,
            SkippedStudentIds = existingEnrollments
        };
    }
}

internal static class AdminValidationExtensions
{
    public static bool StartsWithInvalidAcademicYear(this CreateClassRequestDto request)
    {
        if (string.IsNullOrWhiteSpace(request.AcademicYear))
        {
            return true;
        }

        var parts = request.AcademicYear.Trim().Split('-', StringSplitOptions.RemoveEmptyEntries);
        if (parts.Length != 2)
        {
            return true;
        }

        if (!int.TryParse(parts[0], out var startYear) || !int.TryParse(parts[1], out var endYear))
        {
            return true;
        }

        return startYear >= endYear;
    }
}