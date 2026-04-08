namespace unicheck_backend.Services;

/// <summary>
/// Singleton event broker dùng để bridge API Controller → Blazor Circuit.
///
/// Vấn đề: AttendanceStateService là Scoped (mỗi Blazor tab = 1 instance),
/// còn AttendanceApiController chạy trong HTTP scope riêng → không thể inject trực tiếp.
///
/// Giải pháp: Singleton này phát event khi SV check-in.
/// LiveSession.razor (Scoped) subscribe vào event này và cập nhật UI.
/// </summary>
public class AttendanceNotifier
{
    /// <summary>
    /// Fired khi có sinh viên check-in thành công.
    /// Param: (sessionId, studentInfo)
    /// </summary>
    public event Action<int, CheckedInStudentInfo>? OnStudentCheckedIn;

    /// <summary>Gọi từ AttendanceService sau khi CheckIn thành công.</summary>
    public void NotifyCheckedIn(int sessionId, CheckedInStudentInfo info)
        => OnStudentCheckedIn?.Invoke(sessionId, info);
}
