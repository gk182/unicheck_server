public class AttendanceRowViewModel
{
    public string StudentId { get; set; }
    public string FullName { get; set; }
    public AttendanceStatus Status { get; set; }
    public bool FaceVerified { get; set; }
    public bool LocationVerified { get; set; }
}
