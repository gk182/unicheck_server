using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;
using unicheck_backend.Data;
using unicheck_backend.Models.DTOs;
using unicheck_backend.Services;

namespace unicheck_backend.Controllers.Api;

[ApiController]
[Route("api/attendances")]
[Authorize(AuthenticationSchemes = JwtBearerDefaults.AuthenticationScheme, Roles = "STUDENT")]
public class AttendanceApiController : ControllerBase
{
    private readonly AppDbContext _db;
    private readonly AttendanceService _attendanceService;

    public AttendanceApiController(AppDbContext db, AttendanceService attendanceService)
    {
        _db                = db;
        _attendanceService = attendanceService;
    }

    [HttpGet("history")]
    public async Task<IActionResult> GetHistory()
    {
        var studentId = User.FindFirstValue("StudentId");
        if (string.IsNullOrEmpty(studentId))
            return Unauthorized();

        var history = await _db.Attendances
            .Include(a => a.Session).ThenInclude(s => s.Schedule).ThenInclude(sch => sch.CourseClass).ThenInclude(cc => cc.Course)
            .Include(a => a.Session).ThenInclude(s => s.Schedule).ThenInclude(sch => sch.Room)
            .Where(a => a.StudentId == studentId)
            .OrderByDescending(a => a.Session.Schedule.Date)
            .Select(a => new
            {
                AttendanceId = a.Id,
                CourseName = a.Session.Schedule.CourseClass.Course.CourseName,
                RoomName = a.Session.Schedule.Room.RoomName,
                Date = a.Session.Schedule.Date,
                CheckInTime = a.CheckInTime,
                Status = a.Status.ToString(),
                Note = a.Note
            })
            .ToListAsync();

        return Ok(history);
    }

    [HttpGet("statistics")]
    public async Task<IActionResult> GetStatistics()
    {
        var studentId = User.FindFirstValue("StudentId");
        if (string.IsNullOrEmpty(studentId))
            return Unauthorized();

        var stats = await _db.Attendances
            .Where(a => a.StudentId == studentId)
            .GroupBy(a => a.Status)
            .Select(g => new
            {
                Status = g.Key.ToString(),
                Count = g.Count()
            })
            .ToListAsync();

        var result = new Dictionary<string, int>
        {
            { "Present", stats.FirstOrDefault(s => s.Status == "PRESENT")?.Count ?? 0 },
            { "Absent", stats.FirstOrDefault(s => s.Status == "ABSENT")?.Count ?? 0 },
            { "Late", stats.FirstOrDefault(s => s.Status == "LATE")?.Count ?? 0 },
            { "Excused", stats.FirstOrDefault(s => s.Status == "EXCUSED")?.Count ?? 0 }
        };

        return Ok(result);
    }

    /// <summary>
    /// POST /api/attendance/check-in — SV quét QR + gửi ảnh mặt + toạ độ GPS.
    /// </summary>
    [HttpPost("~/api/attendance/check-in")] // Dùng route riêng theo architecture doc
    public async Task<IActionResult> CheckIn([FromBody] CheckInRequestDto dto)
    {
        if (!ModelState.IsValid)
            return BadRequest(ModelState);

        var studentId = User.FindFirstValue("StudentId");
        if (string.IsNullOrEmpty(studentId))
            return Unauthorized(new { message = "Không tìm thấy thông tin sinh viên trong token." });

        var result = await _attendanceService.CheckIn(dto, studentId);

        if (!result.Success)
            return BadRequest(new { message = result.Message });

        return Ok(result);
    }
}

