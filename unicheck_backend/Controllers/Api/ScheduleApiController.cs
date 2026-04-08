using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;
using unicheck_backend.Data;

namespace unicheck_backend.Controllers.Api;

[ApiController]
[Route("api/schedules")]
[Authorize(AuthenticationSchemes = JwtBearerDefaults.AuthenticationScheme, Roles = "STUDENT")]
public class ScheduleApiController : ControllerBase
{
    private readonly AppDbContext _db;

    public ScheduleApiController(AppDbContext db)
    {
        _db = db;
    }

    [HttpGet]
    public async Task<IActionResult> GetMySchedules()
    {
        var studentId = User.FindFirstValue("StudentId");
        if (string.IsNullOrEmpty(studentId))
            return Unauthorized(new { message = "Không tìm thấy thông tin sinh viên" });

        var schedules = await _db.Schedules
            .Include(s => s.Room)
            .Include(s => s.CourseClass).ThenInclude(cc => cc.Course)
            .Include(s => s.CourseClass).ThenInclude(cc => cc.Lecturer)
            .Where(s => s.CourseClass.Enrollments.Any(e => e.StudentId == studentId))
            .OrderBy(s => s.Date).ThenBy(s => s.StartTime)
            .Select(s => new global::ScheduleViewModel
            {
                ScheduleId = s.ScheduleId,
                CourseName = s.CourseClass.Course.CourseName,
                RoomName = s.Room.RoomName,
                Date = s.Date,
                StartTime = s.StartTime,
                EndTime = s.EndTime
            })
            .ToListAsync();

        return Ok(schedules);
    }
}
