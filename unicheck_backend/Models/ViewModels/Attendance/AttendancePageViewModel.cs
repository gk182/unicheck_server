public class AttendancePageViewModel
{
    public int ScheduleId { get; set; }
    public string CourseName { get; set; }
    public string RoomName { get; set; }
    public DateTime Date { get; set; }

    public bool IsSessionActive { get; set; }
    public string? QrToken { get; set; }

    public List<AttendanceRowViewModel> Students { get; set; } = new List<AttendanceRowViewModel>();
}
