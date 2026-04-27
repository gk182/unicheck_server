using Microsoft.EntityFrameworkCore;
using unicheck_backend.Data;

namespace unicheck_backend.Services;

public class ScheduleService
{
    private readonly AppDbContext _db;

    public ScheduleService(AppDbContext db)
    {
        _db = db;
    }

    /// <summary>
    /// Lấy lịch dạy hôm nay của một giảng viên, dùng cho Dashboard.
    /// </summary>
    public async Task<List<DashboardScheduleDto>> GetDashboardSchedulesAsync(int lecturerId)
    {
        var today = DateTime.Today;

        var schedules = await _db.Schedules
            .Include(s => s.CourseClass).ThenInclude(cc => cc.Course)
            .Include(s => s.CourseClass).ThenInclude(cc => cc.Enrollments)
            .Include(s => s.Room)
            .Include(s => s.AttendanceSessions)
            .Where(s => s.CourseClass.LecturerId == lecturerId && s.Date == today)
            .OrderBy(s => s.StartTime)
            .AsSplitQuery()
            .ToListAsync();

        return schedules.Select(s => 
        {
            var session = s.AttendanceSessions.OrderByDescending(x => x.StartTime).FirstOrDefault();
            return new DashboardScheduleDto
            {
                ScheduleId = s.ScheduleId,
                SessionId = session?.SessionId ?? 0,
                CourseName = s.CourseClass.Course.CourseName,
                GroupCode = s.CourseClass.GroupCode ?? "",
                RoomName = s.Room.RoomName,
                StartTime = s.StartTime.ToString(@"hh\:mm"),
                EndTime = s.EndTime.ToString(@"hh\:mm"),
                StudentCount = s.CourseClass.Enrollments.Count,
                IsSessionActive = session?.IsActive == true,
                IsDone = session != null && !session.IsActive
            };
        }).ToList();
    }
    public async Task<List<ScheduleViewDto>> GetWeekSchedulesAsync(int lecturerId, DateTime startDate, DateTime endDate)
    {
        var schedules = await _db.Schedules
            .Include(s => s.CourseClass).ThenInclude(cc => cc.Course)
            .Include(s => s.Room)
            .Where(s => s.CourseClass.LecturerId == lecturerId && s.Date >= startDate && s.Date <= endDate)
            .OrderBy(s => s.Date).ThenBy(s => s.StartTime)
            .ToListAsync();

        int colorIndex = 0;
        var courseColors = new Dictionary<string, string>();

        return schedules.Select(s => 
        {
            var courseName = s.CourseClass.Course.CourseName;
            if (!courseColors.ContainsKey(courseName))
            {
                courseColors[courseName] = $"color-{colorIndex % 10}";
                colorIndex++;
            }

            return new ScheduleViewDto
            {
                ScheduleId = s.ScheduleId,
                CourseCode = s.CourseClass.Course.CourseCode,
                CourseName = courseName,
                GroupCode = s.CourseClass.GroupCode ?? "",
                RoomName = s.Room.RoomName,
                ColorClass = courseColors[courseName],
                DayOfWeek = s.Date.DayOfWeek,
                TimeSlot = GetClosestSlot(s.StartTime)
            };
        }).ToList();
    }

    public async Task<List<ClassViewDto>> GetClassesByLecturerAsync(int lecturerId)
    {
        var classes = await _db.CourseClasses
            .Include(c => c.Course)
            .Include(c => c.Enrollments)
            .Include(c => c.Schedules)
                .ThenInclude(s => s.AttendanceSessions)
            .Where(c => c.LecturerId == lecturerId)
            .AsSplitQuery()
            .ToListAsync();

        return classes.Select(c => 
        {
            int totalSchedules = c.Schedules.Count;
            int completedSessions = c.Schedules.Count(s => s.AttendanceSessions.Any(a => !a.IsActive));
            int pct = totalSchedules == 0 ? 0 : (int)Math.Round((double)completedSessions / totalSchedules * 100);

            return new ClassViewDto
            {
                ClassId = c.ClassId,
                CourseCode = c.Course.CourseCode,
                CourseName = c.Course.CourseName,
                GroupCode = c.GroupCode ?? "",
                Semester = c.Semester,
                AcademicYear = c.AcademicYear ?? "",
                StudentCount = c.Enrollments.Count,
                TotalSessions = totalSchedules,
                DoneSessions = completedSessions,
                AttendancePct = pct
            };
        }).ToList();
    }

    /// <summary>
    /// Lấy Schedule kèm chi tiết CourseClass + Course + Room.
    /// Dùng cho LiveSession.razor load metadata.
    /// </summary>
    public async Task<Schedule?> GetScheduleWithDetails(int scheduleId)
    {
        return await _db.Schedules
            .Include(s => s.CourseClass).ThenInclude(cc => cc.Course)
            .Include(s => s.CourseClass).ThenInclude(cc => cc.Lecturer)
            .Include(s => s.Room)
            .FirstOrDefaultAsync(s => s.ScheduleId == scheduleId);
    }

    private string GetClosestSlot(TimeSpan startTime)
    {
        int hour = startTime.Hours;
        if (hour < 9) return "07:00";
        if (hour < 11) return "09:00";
        if (hour < 13) return "11:00";
        if (hour < 15) return "13:00";
        if (hour < 17) return "15:00";
        return "17:00";
    }
}

public class ClassViewDto
{
    public int ClassId { get; set; }
    public string CourseCode { get; set; } = string.Empty;
    public string CourseName { get; set; } = string.Empty;
    public string GroupCode { get; set; } = string.Empty;
    public int Semester { get; set; }
    public string AcademicYear { get; set; } = string.Empty;
    public int StudentCount { get; set; }
    public int TotalSessions { get; set; }
    public int DoneSessions { get; set; }
    public int AttendancePct { get; set; }
}

public class DashboardScheduleDto
{
    public int ScheduleId { get; set; }
    public int SessionId { get; set; }
    public string CourseName { get; set; } = string.Empty;
    public string GroupCode { get; set; } = string.Empty;
    public string RoomName { get; set; } = string.Empty;
    public string StartTime { get; set; } = string.Empty;
    public string EndTime { get; set; } = string.Empty;
    public int StudentCount { get; set; }
    public bool IsSessionActive { get; set; }
    public bool IsDone { get; set; }
}

public class ScheduleViewDto
{
    public int ScheduleId { get; set; }
    public string CourseCode { get; set; } = string.Empty;
    public string CourseName { get; set; } = string.Empty;
    public string GroupCode { get; set; } = string.Empty;
    public string RoomName { get; set; } = string.Empty;
    public string ColorClass { get; set; } = string.Empty;
    public DayOfWeek DayOfWeek { get; set; }
    public string TimeSlot { get; set; } = string.Empty;
}
