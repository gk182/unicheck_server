namespace unicheck_backend.Models.Enums;

public enum AbsenceType
{
    NONE = 0,       // Không vắng (có mặt / đi muộn)
    EXCUSED = 1,    // Vắng có phép (đơn được duyệt)
    UNEXCUSED = 2   // Vắng không phép
}