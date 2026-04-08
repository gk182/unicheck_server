using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using unicheck_backend.Data;
using unicheck_backend.Models.DTOs;
using unicheck_backend.Models.Enums;
using unicheck_backend.Services;
using System.Security.Claims;

namespace unicheck_backend.Controllers.Api;

/// <summary>
/// API cho Sinh viên (Mobile Flutter) — JWT Bearer auth.
/// </summary>
[ApiController]
[Route("api/students")]
[Authorize(AuthenticationSchemes = JwtBearerDefaults.AuthenticationScheme, Roles = "STUDENT")]
public class StudentApiController : ControllerBase
{
    private readonly AppDbContext _db;
    private readonly FaceService _faceService;

    public StudentApiController(AppDbContext db, FaceService faceService)
    {
        _db          = db;
        _faceService = faceService;
    }

    /// <summary>
    /// GET /api/students/me — Trả profile sinh viên đang đăng nhập.
    /// Mobile dùng để kiểm tra cờ isFaceRegistered quyết định luồng onboarding.
    /// </summary>
    [HttpGet("me")]
    public async Task<IActionResult> GetMyProfile()
    {
        // Parse StudentId từ JWT claims (được set trong AuthService.GenerateJwtToken)
        var studentId = User.FindFirstValue("StudentId");

        if (string.IsNullOrEmpty(studentId))
            return Unauthorized(new { message = "Không tìm thấy thông tin sinh viên trong token." });

        var student = await _db.Students
            .Include(s => s.User)
            .FirstOrDefaultAsync(s => s.StudentId == studentId);

        if (student == null)
            return NotFound(new { message = "Không tìm thấy hồ sơ sinh viên." });

        var profile = new StudentProfileDto
        {
            StudentId = student.StudentId,
            FullName = student.FullName,
            ClassCode = student.ClassCode,
            Faculty = student.Faculty,
            Major = student.Major,
            DateOfBirth = student.DateOfBirth,
            IsFaceRegistered = !string.IsNullOrEmpty(student.FaceEmbedding),
            Username = student.User.Username
        };

        return Ok(profile);
    }

    /// <summary>
    /// POST /api/students/register-face — Đăng ký khuôn mặt lần đầu (one-time).
    /// Gửi ảnh base64 → Python AI trích xuất embedding → lưu vào DB.
    /// </summary>
    [HttpPost("register-face")]
    public async Task<IActionResult> RegisterFace([FromBody] RegisterFaceRequestDto request)
    {
        if (!ModelState.IsValid)
            return BadRequest(ModelState);

        var studentId = User.FindFirstValue("StudentId");
        if (string.IsNullOrEmpty(studentId))
            return Unauthorized(new { message = "Không tìm thấy thông tin sinh viên." });

        var student = await _db.Students.FindAsync(studentId);
        if (student is null)
            return NotFound(new { message = "Không tìm thấy hồ sơ sinh viên." });

        if (!string.IsNullOrEmpty(student.FaceEmbedding))
            return BadRequest(new { message = "Bạn đã đăng ký khuôn mặt rồi. Không thể đăng ký lại." });

        // Gọi Python AI service để trích xuất embedding
        var result = await _faceService.ExtractEmbedding(request.FaceImageBase64);
        if (!result.Success)
            return BadRequest(new { message = result.Error });

        student.FaceEmbedding = result.EmbeddingJson;
        await _db.SaveChangesAsync();

        return Ok(new { message = "Đăng ký khuôn mặt thành công!" });
    }
}
