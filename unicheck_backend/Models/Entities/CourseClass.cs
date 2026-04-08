using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace unicheck_backend.Models.Entities;

/// <summary>
/// Lớp học phần: 1 môn × 1 giảng viên × 1 học kỳ.
/// VD: "INT3120 - N01 - HK1 2025-2026 - GV: Nguyễn Văn A"
/// </summary>
public class CourseClass
{
    [Key]
    public int ClassId { get; set; }

    [Required]
    public int CourseId { get; set; }
    public Course Course { get; set; } = null!;

    [Required]
    public int LecturerId { get; set; }
    public Lecturer Lecturer { get; set; } = null!;

    [Required]
    public int Semester { get; set; } // 1 hoặc 2

    [Required, MaxLength(20)]
    public string AcademicYear { get; set; } = null!; // VD: "2025-2026"

    [Required, MaxLength(50)]
    public string GroupCode { get; set; } = null!; // VD: "N01"

    // Navigation
    public ICollection<Enrollment> Enrollments { get; set; } = new List<Enrollment>();
    public ICollection<Schedule> Schedules { get; set; } = new List<Schedule>();
}
