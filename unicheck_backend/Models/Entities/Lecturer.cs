using System.ComponentModel.DataAnnotations;

namespace unicheck_backend.Models.Entities;

public class Lecturer
{
    [Key]
    public int LecturerId { get; set; }

    [Required]
    public int UserId { get; set; }

    [Required, MaxLength(100)]
    public string FullName { get; set; } = null!;

    [Required, EmailAddress, MaxLength(200)]
    public string Email { get; set; } = null!;

    [MaxLength(100)]
    public string? Department { get; set; }

    // Navigation
    public User User { get; set; } = null!;
    public ICollection<CourseClass> CourseClasses { get; set; } = new List<CourseClass>();
}
