using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;
using unicheck_backend.Data;
using unicheck_backend.Models.Entities;
using unicheck_backend.Models.Enums;
using unicheck_backend.Services;

namespace unicheck_backend.Controllers.Api;

[ApiController]
[Route("api/leave-requests")]
[Authorize(AuthenticationSchemes = JwtBearerDefaults.AuthenticationScheme, Roles = "STUDENT")]
public class LeaveApiController : ControllerBase
{
    private readonly AppDbContext _db;
    private readonly IConfiguration _configuration;
    private readonly ILeaveAttachmentStorage _attachmentStorage;

    public LeaveApiController(
        AppDbContext db,
        IConfiguration configuration,
        ILeaveAttachmentStorage attachmentStorage)
    {
        _db = db;
        _configuration = configuration;
        _attachmentStorage = attachmentStorage;
    }

    [HttpGet]
    public async Task<IActionResult> GetMyRequests()
    {
        var studentId = User.FindFirstValue("StudentId");
        if (string.IsNullOrWhiteSpace(studentId))
        {
            return Error(
                StatusCodes.Status401Unauthorized,
                "UNAUTHORIZED",
                "Khong xac dinh duoc sinh vien dang nhap.");
        }

        var requests = await _db.LeaveRequests
            .Include(r => r.Schedule).ThenInclude(s => s.Room)
            .Include(r => r.Schedule).ThenInclude(s => s.CourseClass).ThenInclude(cc => cc.Course)
            .Where(r => r.StudentId == studentId)
            .OrderByDescending(r => r.Schedule.Date)
            .ThenByDescending(r => r.Schedule.StartTime)
            .Select(r => new
            {
                requestId = r.RequestId,
                scheduleId = r.ScheduleId,
                courseName = r.Schedule.CourseClass.Course.CourseName,
                roomName = r.Schedule.Room.RoomName,
                date = r.Schedule.Date,
                startTime = r.Schedule.StartTime,
                endTime = r.Schedule.EndTime,
                reason = r.Reason,
                status = r.Status.ToString(),
                attachmentUrl = r.AttachmentUrl,
                reviewedAt = r.ReviewedAt,
                reviewNote = r.ReviewNote,
                createdAt = r.CreatedAt
            })
            .ToListAsync();

        return Ok(requests);
    }

    [HttpGet("eligible-schedules")]
    public async Task<IActionResult> GetEligibleSchedules()
    {
        var studentId = User.FindFirstValue("StudentId");
        if (string.IsNullOrWhiteSpace(studentId))
        {
            return Error(
                StatusCodes.Status401Unauthorized,
                "UNAUTHORIZED",
                "Khong xac dinh duoc sinh vien dang nhap.");
        }

        var now = DateTime.Now;
        var minHoursBeforeClass = GetMinHoursBeforeClassToSubmit();

        var candidates = await _db.Schedules
            .Include(s => s.Room)
            .Include(s => s.CourseClass).ThenInclude(cc => cc.Course)
            .Where(s => s.CourseClass.Enrollments.Any(e => e.StudentId == studentId))
            .Where(s => !_db.LeaveRequests.Any(r => r.ScheduleId == s.ScheduleId && r.StudentId == studentId))
            .ToListAsync();

        var eligible = candidates
            .Select(s => new
            {
                scheduleId = s.ScheduleId,
                courseName = s.CourseClass.Course.CourseName,
                roomName = s.Room.RoomName,
                date = s.Date,
                startTime = s.StartTime,
                endTime = s.EndTime,
                classStartAt = s.Date.Date + s.StartTime
            })
            .Where(s => s.classStartAt > now)
            .Where(s => minHoursBeforeClass <= 0 || s.classStartAt >= now.AddHours(minHoursBeforeClass))
            .OrderBy(s => s.date)
            .ThenBy(s => s.startTime)
            .Select(s => new
            {
                s.scheduleId,
                s.courseName,
                s.roomName,
                s.date,
                s.startTime,
                s.endTime
            })
            .ToList();

        return Ok(eligible);
    }

    [HttpPost]
    public async Task<IActionResult> SubmitRequest([FromBody] LeaveRequestInputDto request)
    {
        var studentId = User.FindFirstValue("StudentId");
        if (string.IsNullOrWhiteSpace(studentId))
        {
            return Error(
                StatusCodes.Status401Unauthorized,
                "UNAUTHORIZED",
                "Khong xac dinh duoc sinh vien dang nhap.");
        }

        var reason = request.Reason?.Trim();
        if (string.IsNullOrWhiteSpace(reason))
        {
            return Error(
                StatusCodes.Status400BadRequest,
                "LEAVE_REASON_REQUIRED",
                "Ly do xin nghi la bat buoc.");
        }

        var minReasonLength = GetMinReasonLength();
        if (reason.Length < minReasonLength)
        {
            return Error(
                StatusCodes.Status400BadRequest,
                "LEAVE_REASON_TOO_SHORT",
                $"Ly do xin nghi phai co it nhat {minReasonLength} ky tu.");
        }

        var schedule = await _db.Schedules
            .Include(s => s.CourseClass).ThenInclude(cc => cc.Enrollments)
            .FirstOrDefaultAsync(s => s.ScheduleId == request.ScheduleId);

        if (schedule is null)
        {
            return Error(
                StatusCodes.Status404NotFound,
                "SCHEDULE_NOT_FOUND",
                "Lich hoc khong ton tai.");
        }

        if (!schedule.CourseClass.Enrollments.Any(e => e.StudentId == studentId))
        {
            return Error(
                StatusCodes.Status403Forbidden,
                "STUDENT_NOT_ENROLLED_FOR_SCHEDULE",
                "Sinh vien khong thuoc lop hoc cua buoi nay.");
        }

        var now = DateTime.Now;
        var classStartAt = schedule.Date.Date + schedule.StartTime;
        if (classStartAt <= now)
        {
            return Error(
                StatusCodes.Status400BadRequest,
                "LEAVE_DEADLINE_PASSED",
                "Buoi hoc da dien ra, khong the nop don xin nghi.");
        }

        var minHoursBeforeClass = GetMinHoursBeforeClassToSubmit();
        if (minHoursBeforeClass > 0 && classStartAt < now.AddHours(minHoursBeforeClass))
        {
            return Error(
                StatusCodes.Status400BadRequest,
                "LEAVE_SUBMISSION_WINDOW_CLOSED",
                $"Don xin nghi phai nop truoc {minHoursBeforeClass} gio so voi gio bat dau buoi hoc.");
        }

        var existingRequest = await _db.LeaveRequests
            .FirstOrDefaultAsync(r => r.ScheduleId == request.ScheduleId && r.StudentId == studentId);

        if (existingRequest is not null)
        {
            return Error(
                StatusCodes.Status400BadRequest,
                "LEAVE_ALREADY_SUBMITTED",
                "Ban da nop don xin nghi cho buoi hoc nay. Don bi tu choi cung khong duoc nop lai.");
        }

        var leaveReq = new LeaveRequest
        {
            StudentId = studentId,
            ScheduleId = request.ScheduleId,
            Reason = reason,
            AttachmentUrl = string.IsNullOrWhiteSpace(request.AttachmentUrl) ? null : request.AttachmentUrl.Trim(),
            Status = LeaveRequestStatus.PENDING
        };

        _db.LeaveRequests.Add(leaveReq);
        await _db.SaveChangesAsync();

        return Ok(new
        {
            requestId = leaveReq.RequestId,
            message = "Nop don thanh cong"
        });
    }

    [HttpPost("attachments")]
    [Consumes("multipart/form-data")]
    public async Task<IActionResult> UploadAttachment([FromForm] LeaveAttachmentUploadInputDto input)
    {
        var studentId = User.FindFirstValue("StudentId");
        if (string.IsNullOrWhiteSpace(studentId))
        {
            return Error(
                StatusCodes.Status401Unauthorized,
                "UNAUTHORIZED",
                "Khong xac dinh duoc sinh vien dang nhap.");
        }

        if (input.File is null || input.File.Length == 0)
        {
            return Error(
                StatusCodes.Status400BadRequest,
                "LEAVE_ATTACHMENT_REQUIRED",
                "Ban chua chon tep dinh kem.");
        }

        var maxBytes = GetMaxAttachmentSizeBytes();
        if (input.File.Length > maxBytes)
        {
            return Error(
                StatusCodes.Status400BadRequest,
                "LEAVE_ATTACHMENT_TOO_LARGE",
                $"Tep dinh kem vuot qua gioi han {maxBytes / (1024 * 1024)}MB.");
        }

        var fileExt = Path.GetExtension(input.File.FileName).ToLowerInvariant();
        var allowedExtensions = GetAllowedAttachmentExtensions();
        if (!allowedExtensions.Contains(fileExt))
        {
            return Error(
                StatusCodes.Status400BadRequest,
                "LEAVE_ATTACHMENT_INVALID_TYPE",
                $"Chi chap nhan cac dinh dang: {string.Join(", ", allowedExtensions)}.");
        }

        LeaveAttachmentStoredFile stored;
        try
        {
            stored = await _attachmentStorage.SaveLeaveAttachmentAsync(
                input.File,
                studentId,
                HttpContext.RequestAborted);
        }
        catch (Exception)
        {
            return Error(
                StatusCodes.Status502BadGateway,
                "LEAVE_ATTACHMENT_UPLOAD_FAILED",
                "Khong the upload minh chung. Vui long thu lai sau.");
        }

        var absoluteUrl = Uri.TryCreate(stored.RelativeUrl, UriKind.Absolute, out _)
            ? stored.RelativeUrl
            : $"{Request.Scheme}://{Request.Host}{stored.RelativeUrl}";
        return Ok(new LeaveAttachmentUploadResultDto(
            absoluteUrl,
            stored.OriginalFileName,
            stored.ContentType,
            stored.FileSizeBytes));
    }

    private int GetMinHoursBeforeClassToSubmit()
    {
        var configured = _configuration.GetValue<int?>("LeaveRequestPolicy:MinHoursBeforeClassToSubmit");
        return configured.GetValueOrDefault(24);
    }

    private int GetMinReasonLength()
    {
        var configured = _configuration.GetValue<int?>("LeaveRequestPolicy:MinReasonLength");
        return configured.GetValueOrDefault(10);
    }

    private long GetMaxAttachmentSizeBytes()
    {
        var maxMb = _configuration.GetValue<int?>("LeaveRequestPolicy:AttachmentMaxFileSizeMb")
            .GetValueOrDefault(5);
        return Math.Max(maxMb, 1) * 1024L * 1024L;
    }

    private HashSet<string> GetAllowedAttachmentExtensions()
    {
        var configured = _configuration.GetSection("LeaveRequestPolicy:AttachmentAllowedExtensions").Get<string[]>();
        var defaults = new[] { ".jpg", ".jpeg", ".png", ".pdf" };
        var extensions = configured is { Length: > 0 } ? configured : defaults;

        return extensions
            .Select(x => x.Trim().ToLowerInvariant())
            .Where(x => !string.IsNullOrWhiteSpace(x))
            .Select(x => x.StartsWith('.') ? x : $".{x}")
            .ToHashSet();
    }

    private ObjectResult Error(int statusCode, string code, string message)
    {
        return StatusCode(statusCode, new ErrorResponseDto(code, message));
    }
}

public class LeaveRequestInputDto
{
    public int ScheduleId { get; set; }
    public string? Reason { get; set; }
    public string? AttachmentUrl { get; set; }
}

public class LeaveAttachmentUploadInputDto
{
    public IFormFile? File { get; set; }
}

public record ErrorResponseDto(string Code, string Message);
public record LeaveAttachmentUploadResultDto(
    string AttachmentUrl,
    string OriginalFileName,
    string ContentType,
    long FileSizeBytes);
