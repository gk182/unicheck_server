namespace unicheck_backend.Models.DTOs;

/// <summary>
/// Response cho GET /api/students/me — thông tin profile sinh viên đang đăng nhập.
/// </summary>
public class StudentProfileDto
{
    public string StudentId { get; set; } = string.Empty;
    public string FullName { get; set; } = string.Empty;
    public string? ClassCode { get; set; }
    public string? Faculty { get; set; }
    public string? Major { get; set; }
    public string Email => $"{StudentId}@ued.udn.vn".ToLower();
    public DateTime DateOfBirth { get; set; }
    public bool IsFaceRegistered { get; set; }
    public string Username { get; set; } = string.Empty;
}
