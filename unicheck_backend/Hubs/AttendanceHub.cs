// using Microsoft.AspNetCore.SignalR;
// using UniCheck.Services.Interfaces;

// public class AttendanceHub : Hub
// {
//     private readonly IAttendanceService _attendanceService;

//     public AttendanceHub(IAttendanceService attendanceService)
//     {
//         _attendanceService = attendanceService;
//     }

//     // SV + GV join vào session
//     public async Task JoinSession(int sessionId)
//     {
//         await Groups.AddToGroupAsync(
//             Context.ConnectionId,
//             $"attendance_session_{sessionId}"
//         );
//     }

//     // SV gửi kết quả điểm danh
//     public async Task SubmitAttendance(AttendanceCheckRequestViewModel model)
//     {
//         var result = await _attendanceService.CheckInAsync(model);

//         // gửi cho GV realtime
//         await Clients.Group($"attendance_session_{model.SessionId}")
//             .SendAsync("StudentCheckedIn", result);

//         // gửi lại cho SV
//         await Clients.Caller.SendAsync("CheckInResult", result);
//     }
// }
