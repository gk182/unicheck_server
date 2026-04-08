using System.ComponentModel.DataAnnotations;
using unicheck_backend.Models.Enums;

namespace unicheck_backend.Models.Entities;

/// <summary>
/// Đơn xin vắng có phép của sinh viên cho 1 buổi học cụ thể.
/// </summary>
public class LeaveRequest
{
    [Key]
    public int RequestId { get; set; }

    [Required]
    public string StudentId { get; set; } = null!;

    [Required]
    public int ScheduleId { get; set; } // Buổi học muốn xin nghỉ

    [Required]
    public string Reason { get; set; } = null!;

    public string? AttachmentUrl { get; set; } // File minh chứng (ảnh, PDF)

    public LeaveRequestStatus Status { get; set; } = LeaveRequestStatus.PENDING;

    // Thông tin duyệt
    public int? ReviewedBy { get; set; }      // LecturerId người duyệt
    public DateTime? ReviewedAt { get; set; } // Thời điểm duyệt
    public string? ReviewNote { get; set; }   // Ghi chú khi từ chối

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    // Navigation
    public Student Student { get; set; } = null!;
    public Schedule Schedule { get; set; } = null!;
}
