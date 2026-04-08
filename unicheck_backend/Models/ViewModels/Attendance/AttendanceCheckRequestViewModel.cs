using System.ComponentModel.DataAnnotations;

public class AttendanceCheckRequestViewModel : IValidatableObject
{
    [Required]
    public string QrToken { get; set; }

    [Required]
    public string StudentId { get; set; }

    [Range(-90, 90, ErrorMessage = "Latitude must be between -90 and 90")]
    public double? Latitude { get; set; }
    
    [Range(-180, 180, ErrorMessage = "Longitude must be between -180 and 180")]
    public double? Longitude { get; set; }

    public string? FaceImageBase64 { get; set; }

    public IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
    {
        // Logic: Nếu có Latitude thì phải có Longitude và ngược lại
        if (Latitude.HasValue != Longitude.HasValue)
        {
            yield return new ValidationResult(
                "Latitude and Longitude must both be provided or both be null.",
                new[] { nameof(Latitude), nameof(Longitude) }
            );
        }
    }
}
