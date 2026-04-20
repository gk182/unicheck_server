using System.ComponentModel.DataAnnotations;

namespace unicheck_backend.Models.DTOs;

public class CreateClassRequestDto
{
    [Required]
    public int CourseId { get; set; }

    [Required]
    public int LecturerId { get; set; }

    [Required]
    [Range(1, 2)]
    public int Semester { get; set; }

    [Required]
    [MaxLength(20)]
    public string AcademicYear { get; set; } = string.Empty;

    [Required]
    [MaxLength(50)]
    public string GroupCode { get; set; } = string.Empty;
}

public class AssignLecturerRequestDto
{
    [Required]
    public int LecturerId { get; set; }
}

public class UpdateScheduleRequestDto
{
    [Required]
    public DateTime Date { get; set; }

    [Required]
    public TimeSpan StartTime { get; set; }

    [Required]
    public TimeSpan EndTime { get; set; }

    [Required]
    public int RoomId { get; set; }
}

public class EnrollStudentsRequestDto
{
    [Required]
    public int ClassId { get; set; }

    [Required]
    [MinLength(1)]
    public List<string> StudentIds { get; set; } = new();
}

public class EnrollmentResultDto
{
    public int ClassId { get; set; }
    public int TotalAttempted { get; set; }
    public int SuccessCount { get; set; }
    public int SkippedCount { get; set; }
    public int FailedCount { get; set; }
    public List<string> FailedStudentIds { get; set; } = new();
    public List<string> SkippedStudentIds { get; set; } = new();
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}

public class ImportEnrollmentCsvRequestDto
{
    [Required]
    public int ClassId { get; set; }

    [Required]
    public string CsvContent { get; set; } = string.Empty;
}