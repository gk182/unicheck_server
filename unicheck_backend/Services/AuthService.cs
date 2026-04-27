using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.IdentityModel.Tokens;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using unicheck_backend.Data;
using unicheck_backend.Models.Entities;
using unicheck_backend.Models.Enums;
using Microsoft.EntityFrameworkCore;

namespace unicheck_backend.Services;

/// <summary>
/// Xử lý đăng nhập, sinh JWT token, BCrypt verify password.
/// Web MVC dùng Cookie; Mobile Flutter dùng JWT Bearer.
/// </summary>
public class AuthService
{
    private readonly AppDbContext _db;
    private readonly IConfiguration _config;

    public AuthService(AppDbContext db, IConfiguration config)
    {
        _db = db;
        _config = config;
    }

    public async Task<User?> AuthenticateAsync(string username, string password)
    {
        var user = await _db.Users
            .AsNoTracking()  // ← FIX: Không tracked DbContext, tránh threading issue
            .Include(u => u.Student)
            .Include(u => u.Lecturer)
            .FirstOrDefaultAsync(u => u.Username == username && u.IsActive);

        if (user == null) return null;

        bool isPasswordValid = BCrypt.Net.BCrypt.Verify(password, user.PasswordHash);
        return isPasswordValid ? user : null;
    }

    /// <summary>
    /// Tạo ClaimsPrincipal cho Cookie Authentication (Web Giảng viên).
    /// </summary>
    public ClaimsPrincipal GetClaimsPrincipalForCookie(User user)
    {
        var claims = new List<Claim>
        {
            new Claim(ClaimTypes.NameIdentifier, user.Id.ToString()),
            new Claim(ClaimTypes.Name, user.Username),
            new Claim(ClaimTypes.Role, user.Role.ToString())
        };

        if (user.Role == UserRole.LECTURER && user.Lecturer != null)
        {
            claims.Add(new Claim("LecturerId", user.Lecturer.LecturerId.ToString()));
            claims.Add(new Claim("FullName", user.Lecturer.FullName));
        }

        var identity = new ClaimsIdentity(claims, CookieAuthenticationDefaults.AuthenticationScheme);
        return new ClaimsPrincipal(identity);
    }

    public string GenerateJwtToken(User user)
    {
        var jwtSettings = _config.GetSection("JwtSettings");
        var secretKey = Encoding.UTF8.GetBytes(jwtSettings["SecretKey"]!);

        var claims = new List<Claim>
        {
            new Claim(ClaimTypes.NameIdentifier, user.Id.ToString()),
            new Claim(ClaimTypes.Name, user.Username),
            new Claim(ClaimTypes.Role, user.Role.ToString())
        };

        if (user.Role == UserRole.STUDENT && user.Student != null)
        {
            claims.Add(new Claim("StudentId", user.Student.StudentId));
            claims.Add(new Claim("FullName", user.Student.FullName));
        }
        else if (user.Role == UserRole.LECTURER && user.Lecturer != null)
        {
            claims.Add(new Claim("LecturerId", user.Lecturer.LecturerId.ToString()));
            claims.Add(new Claim("FullName", user.Lecturer.FullName));
        }

        var tokenDescriptor = new SecurityTokenDescriptor
        {
            Subject            = new ClaimsIdentity(claims),
            Expires            = DateTime.UtcNow.AddMinutes(double.Parse(jwtSettings["ExpiryMinutes"] ?? "1440")),
            Issuer             = jwtSettings["Issuer"],
            Audience           = jwtSettings["Audience"],
            SigningCredentials = new SigningCredentials(new SymmetricSecurityKey(secretKey), SecurityAlgorithms.HmacSha256Signature)
        };

        var tokenHandler = new JwtSecurityTokenHandler();
        var token = tokenHandler.CreateToken(tokenDescriptor);
        return tokenHandler.WriteToken(token);
    }
    public async Task<bool> ChangePasswordAsync(int userId, string currentPassword, string newPassword)
    {
        var user = await _db.Users.FirstOrDefaultAsync(u => u.Id == userId);
        if (user == null) return false;

        if (!BCrypt.Net.BCrypt.Verify(currentPassword, user.PasswordHash))
            return false;

        user.PasswordHash = BCrypt.Net.BCrypt.HashPassword(newPassword);
        await _db.SaveChangesAsync();
        return true;
    }
}
