using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace unicheck_backend.Models.Entities;

public class Student
{
    [Key]
    [DatabaseGenerated(DatabaseGeneratedOption.None)]
    public string StudentId { get; set; } = null!; // MSV, VD: "20210001"

    [Required]
    public int UserId { get; set; }

    [Required, MaxLength(100)]
    public string FullName { get; set; } = null!;

    public DateTime DateOfBirth { get; set; }

    [MaxLength(20)]
    public string? ClassCode { get; set; } // Lớp hành chính, VD: "CNTT2021A"

    // Thông tin thêm
    [MaxLength(100)]
    public string? Faculty { get; set; } // Khoa, VD: "Toán - Tin"

    [MaxLength(100)]
    public string? Major { get; set; } // Ngành, VD: "Sư phạm Tin học"

    [MaxLength(100)]
    public string? Email { get; set; }


    // Lưu vector AI dạng JSON (float[] 128-dim FaceNet)
    public string? FaceEmbedding { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    // Navigation
    public User User { get; set; } = null!;
    public ICollection<Enrollment> Enrollments { get; set; } = new List<Enrollment>();
    public ICollection<Attendance> Attendances { get; set; } = new List<Attendance>();
    public ICollection<LeaveRequest> LeaveRequests { get; set; } = new List<LeaveRequest>();
}
