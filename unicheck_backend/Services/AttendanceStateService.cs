using unicheck_backend.Models.Entities;

namespace unicheck_backend.Services;

/// <summary>
/// Scoped per Blazor circuit (1 browser tab = 1 instance).
/// AttendanceService.CheckIn() gọi AddCheckedInStudent() →
/// LiveSession.razor tự động re-render qua OnChange event.
/// </summary>
public class AttendanceStateService
{
    public int SessionId { get; private set; }
    public int TotalStudents { get; private set; }
    public List<CheckedInStudentInfo> CheckedInStudents { get; } = new();
    public bool IsSessionActive { get; private set; }
    public string? CurrentQrToken { get; private set; }
    public DateTime QrTokenExpiry { get; private set; }

    // Blazor components subscribe vào event này để auto re-render
    public event Action? OnChange;

    public void InitSession(int sessionId, int totalStudents, string qrToken, DateTime expiry)
    {
        SessionId       = sessionId;
        TotalStudents   = totalStudents;
        IsSessionActive = true;
        CurrentQrToken  = qrToken;
        QrTokenExpiry   = expiry;
        CheckedInStudents.Clear();
        NotifyStateChanged();
    }

    public void AddCheckedInStudent(CheckedInStudentInfo student)
    {
        // Tránh trùng lặp
        if (CheckedInStudents.Any(s => s.StudentId == student.StudentId)) return;
        CheckedInStudents.Add(student);
        NotifyStateChanged();
    }

    public void RefreshQrToken(string newToken, DateTime newExpiry)
    {
        CurrentQrToken = newToken;
        QrTokenExpiry  = newExpiry;
        NotifyStateChanged();
    }

    public void EndSession()
    {
        IsSessionActive = false;
        NotifyStateChanged();
    }

    private void NotifyStateChanged() => OnChange?.Invoke();

    // Computed properties
    public int PresentCount  => CheckedInStudents.Count(s => s.Status == AttendanceStatus.PRESENT);
    public int LateCount     => CheckedInStudents.Count(s => s.Status == AttendanceStatus.LATE);
    public int AbsentCount   => TotalStudents - CheckedInStudents.Count;
}

/// <summary>Thông tin SV check-in để hiển thị trên màn hình GV.</summary>
public class CheckedInStudentInfo
{
    public string  StudentId      { get; set; } = "";
    public string  FullName       { get; set; } = "";
    public string? AvatarUrl      { get; set; }
    public DateTime CheckInTime   { get; set; }
    public double? FaceConfidence { get; set; }
    public bool   FaceVerified    { get; set; }
    public bool   LocationVerified { get; set; }
    public AttendanceStatus Status { get; set; }
}
