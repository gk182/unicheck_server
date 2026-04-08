using System.ComponentModel.DataAnnotations;

namespace unicheck_backend.Models.Entities;

/// <summary>
/// Sinh viên đăng ký lớp học phần.
/// Unique constraint: (ClassId, StudentId) — 1 SV không đăng ký cùng lớp 2 lần.
/// </summary>
public class Enrollment
{
    [Key]
    public int Id { get; set; }

    [Required]
    public int ClassId { get; set; }

    [Required]
    public string StudentId { get; set; } = null!;

    // Navigation
    public CourseClass CourseClass { get; set; } = null!;
    public Student Student { get; set; } = null!;
}
