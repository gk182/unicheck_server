using System.ComponentModel.DataAnnotations;

namespace unicheck_backend.Models.Entities;

/// <summary>
/// Phiên điểm danh (1 phiên / 1 buổi học).
/// QrToken đổi mới mỗi 20 giây; validate bằng QrTokenExpiry.
/// </summary>
public class AttendanceSession
{
    [Key]
    public int SessionId { get; set; }

    [Required]
    public int ScheduleId { get; set; }

    // QR Token hiện tại (GUID hex, không dấu gạch)
    [Required]
    public string QrToken { get; set; } = null!;

    // Thời hạn token hiện tại — SV phải check-in trước thời điểm này
    public DateTime QrTokenExpiry { get; set; }

    public DateTime StartTime { get; set; }
    public DateTime? EndTime { get; set; }

    public bool IsActive { get; set; }

    // Navigation
    public Schedule Schedule { get; set; } = null!;
    public ICollection<Attendance> Attendances { get; set; } = new List<Attendance>();
}
