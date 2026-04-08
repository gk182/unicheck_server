using System.ComponentModel.DataAnnotations;

namespace unicheck_backend.Models.DTOs;

/// <summary>
/// Request body cho POST /api/students/register-face.
/// </summary>
public class RegisterFaceRequestDto
{
    /// <summary>Ảnh khuôn mặt sinh viên, encode base64.</summary>
    [Required]
    public string FaceImageBase64 { get; set; } = string.Empty;
}
