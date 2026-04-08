using System.ComponentModel.DataAnnotations;

namespace unicheck_backend.Models.Entities;

/// <summary>
/// Buổi học cụ thể (1 slot trong lịch của 1 lớp học phần).
/// </summary>
public class Schedule
{
    [Key]
    public int ScheduleId { get; set; }

    [Required]
    public int ClassId { get; set; } // → CourseClass

    [Required]
    public int RoomId { get; set; } // → Room

    public DateTime Date { get; set; } // Ngày học

    public TimeSpan StartTime { get; set; } // Giờ bắt đầu
    public TimeSpan EndTime { get; set; }   // Giờ kết thúc

    // Navigation
    public CourseClass CourseClass { get; set; } = null!;
    public Room Room { get; set; } = null!;
    public ICollection<AttendanceSession> AttendanceSessions { get; set; } = new List<AttendanceSession>();
}
