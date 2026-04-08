using System.ComponentModel.DataAnnotations;
using unicheck_backend.Models.Enums;

namespace unicheck_backend.Models.Entities;

/// <summary>
/// Bản ghi điểm danh của 1 sinh viên trong 1 phiên.
/// Được tạo trước với Status=ABSENT khi phiên bắt đầu; cập nhật khi SV check-in.
/// </summary>
public class Attendance
{
    [Key]
    public int Id { get; set; }

    [Required]
    public int SessionId { get; set; }

    [Required]
    public string StudentId { get; set; } = null!;

    public DateTime? CheckInTime { get; set; } // null = chưa check-in

    // GPS tại thời điểm check-in
    public double? Latitude { get; set; }
    public double? Longitude { get; set; }

    // Kết quả xác thực AI
    public bool FaceVerified { get; set; }
    public double? FaceConfidence { get; set; } // Cosine similarity score [0..1]

    // Kết quả xác thực GPS
    public bool LocationVerified { get; set; }
    public double? DistanceMeter { get; set; } // Khoảng cách thực tế tính được

    public AttendanceStatus Status { get; set; } = AttendanceStatus.ABSENT;

    public AbsenceType AbsenceType { get; set; } = AbsenceType.NONE;

    public string? Note { get; set; } // GV ghi chú thủ công

    // Navigation
    public AttendanceSession Session { get; set; } = null!;
    public Student Student { get; set; } = null!;
}
