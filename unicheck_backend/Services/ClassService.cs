using Microsoft.EntityFrameworkCore;
using unicheck_backend.Data;
using unicheck_backend.Models.Enums;
using System.Linq;

namespace unicheck_backend.Services;

public class ClassService
{
    private readonly AppDbContext _db;

    public ClassService(AppDbContext db)
    {
        _db = db;
    }

    public async Task<ClassDetailsDto?> GetClassDetailsAsync(int classId)
    {
        var cls = await _db.CourseClasses
            .Include(c => c.Course)
            .Include(c => c.Enrollments).ThenInclude(e => e.Student)
            .Include(c => c.Schedules).ThenInclude(s => s.AttendanceSessions).ThenInclude(asess => asess.Attendances)
            .FirstOrDefaultAsync(c => c.ClassId == classId);

        if (cls == null) return null;

        var sessions = cls.Schedules
            .SelectMany(s => s.AttendanceSessions)
            .OrderBy(asess => asess.StartTime)
            .Select((asess, index) => new SessionDetailDto
            {
                SessionId = asess.SessionId,
                Number = index + 1,
                Date = asess.StartTime.ToString("dd/MM/yyyy"),
                DateShort = asess.StartTime.ToString("dd/MM"),
                StartTime = asess.StartTime.ToString("HH:mm")
            }).ToList();

        var students = cls.Enrollments.Select(e => new StudentPivotDto
        {
            StudentId = e.StudentId,
            FullName = e.Student.FullName,
            Records = sessions.Select(s => 
            {
                var att = _db.Attendances.FirstOrDefault(a => a.SessionId == s.SessionId && a.StudentId == e.StudentId);
                return att?.Status switch
                {
                    AttendanceStatus.PRESENT => "P",
                    AttendanceStatus.LATE => "L",
                    AttendanceStatus.ABSENT => "A",
                    AttendanceStatus.EXCUSED => "E",
                    _ => "-"
                };
            }).ToList()
        }).ToList();

        var pendingLeaves = await _db.LeaveRequests
            .Include(lr => lr.Student)
            .Include(lr => lr.Schedule)
            .Where(lr => lr.Schedule.ClassId == classId && lr.Status == LeaveRequestStatus.PENDING)
            .OrderBy(lr => lr.CreatedAt)
            .Select(lr => new LeaveRequestDto
            {
                RequestId = lr.RequestId,
                StudentId = lr.StudentId,
                FullName = lr.Student.FullName,
                Date = lr.Schedule.Date.ToString("dd/MM/yyyy"),
                Reason = lr.Reason,
                Status = lr.Status.ToString()
            }).ToListAsync();

        return new ClassDetailsDto
        {
            ClassId = cls.ClassId,
            CourseCode = cls.Course.CourseCode,
            CourseName = cls.Course.CourseName,
            GroupCode = cls.GroupCode ?? "",
            Semester = cls.Semester,
            AcademicYear = cls.AcademicYear ?? "",
            StudentCount = cls.Enrollments.Count,
            TotalSessions = cls.Schedules.Count,
            DoneSessions = sessions.Count(),
            Sessions = sessions,
            Students = students,
            LeaveRequests = pendingLeaves
        };
    }

    public async Task<List<StudentReportDto>> GetAttendanceReportAsync(int classId)
    {
        var enrollments = await _db.Enrollments
            .Include(e => e.Student)
            .Where(e => e.ClassId == classId)
            .ToListAsync();

        var sessions = await _db.AttendanceSessions
            .Include(s => s.Schedule)
            .Where(s => s.Schedule.ClassId == classId)
            .ToListAsync();

        var sessionIds = sessions.Select(s => s.SessionId).ToList();

        var reports = new List<StudentReportDto>();

        foreach (var en in enrollments)
        {
            var atts = await _db.Attendances
                .Where(a => sessionIds.Contains(a.SessionId) && a.StudentId == en.StudentId)
                .ToListAsync();

            reports.Add(new StudentReportDto
            {
                ClassId = classId,
                StudentId = en.StudentId,
                FullName = en.Student.FullName,
                Present = atts.Count(a => a.Status == AttendanceStatus.PRESENT),
                Late = atts.Count(a => a.Status == AttendanceStatus.LATE),
                Absent = atts.Count(a => a.Status == AttendanceStatus.ABSENT),
                Excused = atts.Count(a => a.Status == AttendanceStatus.EXCUSED)
            });
        }

        return reports;
    }
}

public class ClassDetailsDto
{
    public int ClassId { get; set; }
    public string CourseCode { get; set; } = "";
    public string CourseName { get; set; } = "";
    public string GroupCode { get; set; } = "";
    public int Semester { get; set; }
    public string AcademicYear { get; set; } = "";
    public int StudentCount { get; set; }
    public int TotalSessions { get; set; }
    public int DoneSessions { get; set; }
    public List<SessionDetailDto> Sessions { get; set; } = new();
    public List<StudentPivotDto> Students { get; set; } = new();
    public List<LeaveRequestDto> LeaveRequests { get; set; } = new();
}

public class LeaveRequestDto
{
    public int RequestId { get; set; }
    public string StudentId { get; set; } = "";
    public string FullName { get; set; } = "";
    public string Date { get; set; } = "";
    public string Reason { get; set; } = "";
    public string Status { get; set; } = "";
}

public class SessionDetailDto
{
    public int SessionId { get; set; }
    public int Number { get; set; }
    public string Date { get; set; } = "";
    public string DateShort { get; set; } = "";
    public string StartTime { get; set; } = "";
}

public class StudentPivotDto
{
    public string StudentId { get; set; } = "";
    public string FullName { get; set; } = "";
    public List<string> Records { get; set; } = new();
}

public class StudentReportDto
{
    public int ClassId { get; set; }
    public string StudentId { get; set; } = "";
    public string FullName { get; set; } = "";
    public int Present { get; set; }
    public int Late { get; set; }
    public int Absent { get; set; }
    public int Excused { get; set; }
}
