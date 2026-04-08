using System.ComponentModel.DataAnnotations;

namespace unicheck_backend.Models.DTOs;

/// <summary>
/// Request body cho POST /api/attendance/check-in từ Mobile Flutter.
/// </summary>
public class CheckInRequestDto
{
    /// <summary>QR token hiện tại từ màn hình LiveSession của GV.</summary>
    [Required]
    public string QrToken { get; set; } = string.Empty;

    /// <summary>Ảnh khuôn mặt sinh viên, encode base64.</summary>
    [Required]
    public string FaceImageBase64 { get; set; } = string.Empty;

    /// <summary>Vĩ độ GPS của sinh viên tại thời điểm check-in.</summary>
    [Required]
    public double Latitude { get; set; }

    /// <summary>Kinh độ GPS của sinh viên tại thời điểm check-in.</summary>
    [Required]
    public double Longitude { get; set; }
}
