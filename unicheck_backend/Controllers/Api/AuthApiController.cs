using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using unicheck_backend.Models.DTOs;
using unicheck_backend.Models.Enums;
using unicheck_backend.Services;

namespace unicheck_backend.Controllers.Api;

[ApiController]
[Route("api/auth")]
public class AuthApiController : ControllerBase
{
    private readonly AuthService _authService;
    private readonly IConfiguration _config;

    public AuthApiController(AuthService authService, IConfiguration config)
    {
        _authService = authService;
        _config = config;
    }

    /// <summary>
    /// POST /api/auth/login — Mobile Flutter login, trả JWT token.
    /// </summary>
    [HttpPost("login")]
    [AllowAnonymous]
    public async Task<IActionResult> Login([FromBody] LoginRequestDto request)
    {
        if (!ModelState.IsValid)
            return BadRequest(ModelState);

        var user = await _authService.AuthenticateAsync(request.Username, request.Password);

        if (user == null)
            return Unauthorized(new { message = "Tên đăng nhập hoặc mật khẩu không đúng." });

        if (!user.IsActive)
            return Unauthorized(new { message = "Tài khoản của bạn đã bị khóa." });

        var token = _authService.GenerateJwtToken(user);

        string? fullName = null;
        if (user.Role == UserRole.STUDENT && user.Student != null)
            fullName = user.Student.FullName;
        else if (user.Role == UserRole.LECTURER && user.Lecturer != null)
            fullName = user.Lecturer.FullName;

        var expiryMinutes = double.Parse(
            _config.GetSection("JwtSettings")["ExpiryMinutes"] ?? "10080");

        var response = new LoginResponseDto
        {
            Token = token,
            UserId = user.Role == UserRole.STUDENT && user.Student != null
                ? user.Student.StudentId
                : (user.Role == UserRole.LECTURER && user.Lecturer != null
                    ? user.Lecturer.LecturerId.ToString()
                    : user.Id.ToString()),
            Username = user.Username,
            Role = user.Role.ToString(),
            FullName = fullName ?? user.Username,
            ExpiresAt = DateTime.UtcNow.AddMinutes(expiryMinutes)
        };

        return Ok(response);
    }
}
