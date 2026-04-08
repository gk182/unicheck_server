public class AttendanceResultViewModel
{
    public bool Success { get; set; }
    public string Message { get; set; }
    public AttendanceStatus Status { get; set; }
    public DateTime? CheckInTime { get; set; }
}
