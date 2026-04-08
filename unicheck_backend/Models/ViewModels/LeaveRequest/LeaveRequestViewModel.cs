public class LeaveRequestViewModel
{
    public int RequestId { get; set; }
    public string StudentId { get; set; }
    public string StudentName { get; set; }
    public string Reason { get; set; }
    public LeaveRequestStatus Status { get; set; }
    public DateTime CreatedAt { get; set; }
}
