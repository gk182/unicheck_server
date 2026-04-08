namespace unicheck_backend.Models.Enums;

public enum AttendanceStatus
{
    ABSENT = 0,   // Vắng (mặc định khi phiên bắt đầu)
    PRESENT = 1,  // Có mặt (check-in đúng giờ)
    LATE = 2,     // Đi muộn (check-in trễ nhưng trong giới hạn)
    EXCUSED = 3   // Vắng có phép (đơn được duyệt)
}