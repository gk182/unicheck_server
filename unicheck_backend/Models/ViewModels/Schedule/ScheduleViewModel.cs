public class ScheduleViewModel
{
    public int ScheduleId { get; set; }
    public string CourseName { get; set; }
    public string RoomName { get; set; }
    public DateTime Date { get; set; }
    public TimeSpan StartTime { get; set; }
    public TimeSpan EndTime { get; set; }
}
