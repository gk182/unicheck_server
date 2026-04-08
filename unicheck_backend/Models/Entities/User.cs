using System.ComponentModel.DataAnnotations;
using unicheck_backend.Models.Enums;

namespace unicheck_backend.Models.Entities;

public class User
{
    [Key]
    public int Id { get; set; }

    [Required, MaxLength(50)]
    public string Username { get; set; } = null!;

    [Required]
    public string PasswordHash { get; set; } = null!;

    [Required]
    public UserRole Role { get; set; }

    public bool IsActive { get; set; } = true;

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    // Navigation
    public Student? Student { get; set; }
    public Lecturer? Lecturer { get; set; }
}
