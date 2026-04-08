namespace unicheck_backend.Models.ViewModels.Attendance;

/// <summary>
/// Data model dùng chung giữa ClassDetail.razor và AttendanceTable.razor
/// </summary>
public class AttendanceRowModel
{
    public string  StudentId        { get; set; } = "";
    public string  FullName         { get; set; } = "";
    public string  Status           { get; set; } = "ABSENT";
    public string? CheckInTime      { get; set; }
    public double? FaceConfidence   { get; set; }
    public bool    LocationVerified { get; set; }
    public string? Note             { get; set; }
}
