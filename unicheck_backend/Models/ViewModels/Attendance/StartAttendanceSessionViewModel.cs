using System.ComponentModel.DataAnnotations;

public class StartAttendanceSessionViewModel
{
    [Required(ErrorMessage = "Vui lòng chọn lịch học")]
    public int ScheduleId { get; set; }

    [Range(1, 120, ErrorMessage = "Thời gian phiên điểm danh phải từ 1 đến 120 phút")]
    public int DurationMinutes { get; set; } = 5;
}
