namespace unicheck_backend.Models.DTOs;

/// <summary>
/// Response trả về mobile sau khi check-in.
/// </summary>
public class CheckInResponseDto
{
    public bool   Success    { get; set; }
    public string Status     { get; set; } = string.Empty; // "PRESENT" | "LATE"
    public string Message    { get; set; } = string.Empty;

    // Chi tiết xác thực
    public bool   FaceVerified      { get; set; }
    public double FaceConfidence    { get; set; }
    public bool   LocationVerified  { get; set; }
    public double DistanceMeter     { get; set; }
}
