using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;
using unicheck_backend.Models.Enums;
using unicheck_backend.Services;
using System.Net;

namespace unicheck_backend.Controllers.Web;

/// <summary>
/// Xử lý Cookie auth cho Giảng viên Web (Blazor).
/// POST /Auth/Login  → set Cookie → redirect /dashboard
/// GET  /Auth/Logout → clear Cookie → redirect /login
/// </summary>
[ApiController]
[Route("[controller]")]
[ApiExplorerSettings(IgnoreApi = true)]
public class AuthController : Controller
{
    private readonly AuthService _authService;
    private readonly ILogger<AuthController> _logger;

    public AuthController(AuthService authService, ILogger<AuthController> logger)
    {
        _authService = authService;
        _logger = logger;
    }

    /// <summary>
    /// POST /Auth/Login — Xử lý form từ Blazor Login.razor
    /// Dùng AuthService.AuthenticateAsync() thật với BCrypt
    /// </summary>
    [HttpPost("Login")]
    [AllowAnonymous]
    public async Task<IActionResult> Login(
        [FromForm] string username,
        [FromForm] string password)
    {
        _logger.LogInformation(">>> [WEB LOGIN] Nhận request: {Username}", username);

        if (string.IsNullOrEmpty(username) || string.IsNullOrEmpty(password))
        {
            _logger.LogWarning("Empty username or password submitted.");
            return Redirect($"/login?error={WebUtility.UrlEncode("Vui lòng nhập đầy đủ thông tin.")}");
        }

        var user = await _authService.AuthenticateAsync(username, password);

        if (user == null)
        {
            _logger.LogWarning("Authentication failed for user: {Username}", username);
            return Redirect($"/login?error={WebUtility.UrlEncode("Tên đăng nhập hoặc mật khẩu không đúng")}");
        }

        _logger.LogInformation("User {Username} authenticated. Role: {Role}", username, user.Role);

        // Cho phép Lecturer và Admin đăng nhập web
        if (user.Role != UserRole.LECTURER && user.Role != UserRole.ADMIN)
        {
            _logger.LogWarning("Non-lecturer/admin login attempt on web: {Username} (Role: {Role})", username, user.Role);
            return Redirect($"/login?error={WebUtility.UrlEncode("Chỉ giảng viên hoặc admin mới có thể đăng nhập trang web")}");
        }

        var principal = _authService.GetClaimsPrincipalForCookie(user);

        await HttpContext.SignInAsync(
            CookieAuthenticationDefaults.AuthenticationScheme,
            principal,
            new AuthenticationProperties { IsPersistent = true }
        );

        _logger.LogInformation("Login success: {Username} (Role: {Role})", username, user.Role);

        if (user.Role == UserRole.ADMIN)
        {
            return LocalRedirect("/admin");
        }

        return LocalRedirect("/dashboard");
    }

    /// <summary>GET /Auth/Logout</summary>
    [HttpGet("Logout")]
    [Authorize]
    public async Task<IActionResult> Logout()
    {
        await HttpContext.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
        return Redirect("/login");
    }
}
