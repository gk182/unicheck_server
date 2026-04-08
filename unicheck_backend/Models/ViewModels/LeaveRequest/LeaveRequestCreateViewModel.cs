using System.ComponentModel.DataAnnotations;

public class LeaveRequestCreateViewModel
{
    [Required]
    public string StudentId { get; set; }

    [Required]
    public int ScheduleId { get; set; }

    [Required]
    public string Reason { get; set; }

    public IFormFile? Attachment { get; set; }
}
