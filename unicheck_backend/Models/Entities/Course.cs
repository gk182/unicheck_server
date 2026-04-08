using System.ComponentModel.DataAnnotations;

namespace unicheck_backend.Models.Entities;

public class Course
{
    [Key]
    public int CourseId { get; set; }

    [Required, MaxLength(20)]
    public string CourseCode { get; set; } = null!; // VD: "INT3120"

    [Required, MaxLength(200)]
    public string CourseName { get; set; } = null!; // VD: "Phát triển ứng dụng Web"

    public int Credit { get; set; }

    // Navigation
    public ICollection<CourseClass> CourseClasses { get; set; } = new List<CourseClass>();
}
