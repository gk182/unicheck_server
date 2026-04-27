using ClosedXML.Excel;
using unicheck_backend.Data;
using Microsoft.EntityFrameworkCore;

namespace unicheck_backend.Services;

/// <summary>
/// Xuất báo cáo điểm danh ra file Excel (.xlsx) bằng ClosedXML.
/// Định dạng chuẩn văn bản hành chính: header trường, tiêu đề, bảng SV, footer ký tên.
/// </summary>
public class ExportService
{
    private readonly AppDbContext _db;

    public ExportService(AppDbContext db) => _db = db;

    // ─────────────────────────────────────────────────────────────────────────
    // EXPORT 1 BUỔI HỌC (ClassDetail.razor)
    // Cột điểm danh = 1 buổi cụ thể với ký hiệu: Có mặt / Vắng / Muộn / Có phép
    // ─────────────────────────────────────────────────────────────────────────
    public async Task<byte[]> ExportSessionAsync(int sessionId)
    {
        // 1. Load session + CourseClass + Course + Enrollments
        var session = await _db.AttendanceSessions
            .Include(s => s.Schedule)
                .ThenInclude(sc => sc.CourseClass)
                    .ThenInclude(cc => cc.Course)
            .Include(s => s.Schedule)
                .ThenInclude(sc => sc.Room)
            .FirstOrDefaultAsync(s => s.SessionId == sessionId)
            ?? throw new InvalidOperationException("Không tìm thấy phiên điểm danh.");

        var courseClass = session.Schedule.CourseClass;
        var course      = courseClass.Course;

        var attendances = await _db.Attendances
            .Include(a => a.Student)
            .Where(a => a.SessionId == sessionId)
            .OrderBy(a => a.StudentId)
            .ToListAsync();

        var sessionDate = session.Schedule.Date.ToString("dd/MM/yyyy");

        // 2. Build workbook
        using var wb = new XLWorkbook();
        var ws = wb.Worksheets.Add("DiemDanh");

        // ── Font mặc định ────────────────────────────────────────────────────
        ws.Style.Font.FontName = "Times New Roman";
        ws.Style.Font.FontSize = 10;

        // ── Header trường (rows 1-2) ─────────────────────────────────────────
        ws.Range("A1:B1").Merge().Value = "ĐẠI HỌC ĐÀ NẴNG";
        ws.Range("A2:B2").Merge().Value = "TRƯỜNG ĐẠI HỌC SƯ PHẠM";
        ws.Range("A2:B2").Style.Font.SetBold();

        ws.Range("C1:I1").Merge().Value = "CỘNG HÒA XÃ HỘI CHỦ NGHĨA VIỆT NAM";
        ws.Range("C1:I1").Style.Font.SetBold();
        ws.Range("C2:I2").Merge().Value = "Độc lập - Tự do - Hạnh phúc";
        ws.Range("C2:I2").Style.Font.SetBold();

        // ── Tiêu đề (rows 3-4) ──────────────────────────────────────────────
        ws.Range("A3:I3").Merge().Value = "DANH SÁCH ĐIỂM DANH LỚP HỌC PHẦN";
        ws.Range("A3:I3").Style.Font.SetBold().Font.SetFontSize(11);

        ws.Range("A4:I4").Merge().Value =
            $"Học kỳ: {courseClass.Semester}   Năm học: {courseClass.AcademicYear}   " +
            $"Buổi ngày: {sessionDate}";
        ws.Range("A4:I4").Style.Font.SetBold();

        // ── Thông tin môn học (rows 5-6) ────────────────────────────────────
        ws.Range("A5:D5").Merge().Value = $"Mã học phần: {course.CourseCode}";
        ws.Range("A5:D5").Style.Font.SetBold();
        ws.Range("E5:I5").Merge().Value = $"Tên học phần: {course.CourseName}";
        ws.Range("E5:I5").Style.Font.SetBold();

        ws.Range("A6:D6").Merge().Value = $"Số tín chỉ: {course.Credit}";
        ws.Range("A6:D6").Style.Font.SetBold();
        ws.Range("E6:I6").Merge().Value = $"Nhóm học phần: {courseClass.GroupCode}";
        ws.Range("E6:I6").Style.Font.SetBold();

        // Căn giữa toàn bộ vùng header
        ws.Range("A1:I6").Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;
        ws.Range("A1:I6").Style.Alignment.Vertical   = XLAlignmentVerticalValues.Center;
        ws.Rows("1:6").Height = 18;

        // ── Header bảng (row 8) ──────────────────────────────────────────────
        int headerRow = 8;
        string[] headers = { "STT", "Mã SV", "Họ và tên", "Ngày sinh", "Lớp SH", "Có mặt", "Vắng" };
        // Điều chỉnh header cột 6-7 theo status
        headers[5] = "Trạng thái";
        headers[6] = "Ghi chú";

        for (int c = 0; c < headers.Length; c++)
            ws.Cell(headerRow, c + 1).Value = headers[c];

        ws.Range(headerRow, 1, headerRow, headers.Length).Style
            .Font.SetBold()
            .Font.SetFontName("Times New Roman")
            .Alignment.SetHorizontal(XLAlignmentHorizontalValues.Center)
            .Alignment.SetVertical(XLAlignmentVerticalValues.Center)
            .Fill.SetBackgroundColor(XLColor.FromHtml("#d9e1f2"))
            .Border.SetOutsideBorder(XLBorderStyleValues.Thin)
            .Border.SetInsideBorder(XLBorderStyleValues.Thin);
        ws.Row(headerRow).Height = 20;

        // ── Data rows ────────────────────────────────────────────────────────
        int row = headerRow + 1;
        int stt = 1;
        foreach (var att in attendances)
        {
            var statusText = att.Status.ToString() switch
            {
                "PRESENT" => "Có mặt",
                "LATE"    => "Đi muộn",
                "ABSENT"  => "Vắng",
                "EXCUSED" => "Có phép",
                _         => att.Status.ToString()
            };

            ws.Cell(row, 1).Value = stt++;
            ws.Cell(row, 2).Value = att.StudentId;
            ws.Cell(row, 3).Value = att.Student.FullName;
            ws.Cell(row, 4).Value = att.Student.DateOfBirth.ToString("dd-MM-yyyy");
            ws.Cell(row, 5).Value = att.Student.ClassCode ?? "";
            ws.Cell(row, 6).Value = statusText;
            ws.Cell(row, 7).Value = att.Note ?? "";

            // Căn giữa tất cả, trừ họ tên (căn trái)
            ws.Range(row, 1, row, headers.Length).Style
                .Font.SetFontName("Times New Roman")
                .Alignment.SetHorizontal(XLAlignmentHorizontalValues.Center)
                .Alignment.SetVertical(XLAlignmentVerticalValues.Center)
                .Border.SetOutsideBorder(XLBorderStyleValues.Thin)
                .Border.SetInsideBorder(XLBorderStyleValues.Thin);

            ws.Cell(row, 3).Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Left; // Họ tên căn trái

            // Màu ô trạng thái
            ws.Cell(row, 6).Style.Fill.BackgroundColor = att.Status.ToString() switch
            {
                "PRESENT" => XLColor.FromHtml("#e2efda"),
                "LATE"    => XLColor.FromHtml("#fff2cc"),
                "ABSENT"  => XLColor.FromHtml("#fce4d6"),
                "EXCUSED" => XLColor.FromHtml("#dae8fc"),
                _         => XLColor.NoColor
            };

            row++;
        }

        // ── Footer ───────────────────────────────────────────────────────────
        int footerStart = row + 1;

        // "Danh sách này có X sinh viên"
        ws.Range(footerStart, 1, footerStart, headers.Length).Merge().Value =
            $"Danh sách này có: {attendances.Count} sinh viên";
        ws.Cell(footerStart, 1).Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;

        // Thống kê nhanh
        int footerStats = footerStart + 1;
        ws.Range(footerStats, 1, footerStats, headers.Length).Merge().Value =
            $"Có mặt: {attendances.Count(a => a.Status.ToString() == "PRESENT")}  |  " +
            $"Đi muộn: {attendances.Count(a => a.Status.ToString() == "LATE")}  |  " +
            $"Vắng: {attendances.Count(a => a.Status.ToString() == "ABSENT")}  |  " +
            $"Có phép: {attendances.Count(a => a.Status.ToString() == "EXCUSED")}";
        ws.Cell(footerStats, 1).Style
            .Font.SetItalic()
            .Alignment.SetHorizontal(XLAlignmentHorizontalValues.Center);

        // Chữ ký GV (bên phải)
        int signRow = footerStats + 2;
        ws.Range(signRow, 5, signRow, headers.Length).Merge().Value =
            "Đà Nẵng, ngày      tháng      năm 20...";
        ws.Range(signRow + 1, 5, signRow + 1, headers.Length).Merge().Value = "Cán bộ giảng dạy";
        ws.Range(signRow + 2, 5, signRow + 2, headers.Length).Merge().Value = "(Ký và ghi rõ họ tên)";
        ws.Range(signRow, 5, signRow + 2, headers.Length).Style
            .Alignment.SetHorizontal(XLAlignmentHorizontalValues.Center)
            .Font.SetFontName("Times New Roman");
        ws.Row(signRow + 1).Style.Font.SetBold();

        // ── In ấn ────────────────────────────────────────────────────────────
        ws.PageSetup.PaperSize   = (XLPaperSize)9; // A4
        ws.PageSetup.PageOrientation = XLPageOrientation.Portrait;
        ws.PageSetup.Margins.Left  = 2.0;
        ws.PageSetup.Margins.Right = 1.5;

        // ── Auto-fit ─────────────────────────────────────────────────────────
        ws.Columns().AdjustToContents();
        ws.Column(3).Width = Math.Max(ws.Column(3).Width, 28); // Họ tên
        ws.Column(1).Width = 5;  // STT
        ws.Column(4).Width = 12; // Ngày sinh

        using var stream = new MemoryStream();
        wb.SaveAs(stream);
        return stream.ToArray();
    }

    // ─────────────────────────────────────────────────────────────────────────
    // EXPORT TOÀN HỌC KỲ — MA TRẬN ĐIỂM DANH (ClassHistory.razor)
    // Mỗi cột = 1 buổi học, ký hiệu P/L/A/E
    // ─────────────────────────────────────────────────────────────────────────
    public async Task<byte[]> ExportClassAsync(int classId)
    {
        var courseClass = await _db.CourseClasses
            .Include(c => c.Course)
            .Include(c => c.Enrollments)
                .ThenInclude(e => e.Student)
            .Include(c => c.Schedules)
                .ThenInclude(s => s.AttendanceSessions)
                    .ThenInclude(sess => sess.Attendances)
            .AsSplitQuery()
            .FirstOrDefaultAsync(c => c.ClassId == classId)
            ?? throw new InvalidOperationException("Không tìm thấy lớp học.");

        var course   = courseClass.Course;
        var sessions = courseClass.Schedules
            .SelectMany(s => s.AttendanceSessions)
            .OrderBy(sess => sess.StartTime)
            .ToList();

        int totalCols = 5 + sessions.Count + 1; // STT + Mã SV + Họ tên + Ngày sinh + Lớp SH + buổi1..N + Tổng vắng

        using var wb = new XLWorkbook();
        var ws = wb.Worksheets.Add("TongHop");

        // ── Font mặc định ────────────────────────────────────────────────────
        ws.Style.Font.FontName = "Times New Roman";
        ws.Style.Font.FontSize = 10;

        // ── Header trường ────────────────────────────────────────────────────
        ws.Range(1, 1, 1, 2).Merge().Value = "ĐẠI HỌC ĐÀ NẴNG";
        ws.Range(2, 1, 2, 2).Merge().Value = "TRƯỜNG ĐẠI HỌC SƯ PHẠM";
        ws.Range(2, 1, 2, 2).Style.Font.SetBold();

        ws.Range(1, 3, 1, totalCols).Merge().Value = "CỘNG HÒA XÃ HỘI CHỦ NGHĨA VIỆT NAM";
        ws.Range(1, 3, 1, totalCols).Style.Font.SetBold();
        ws.Range(2, 3, 2, totalCols).Merge().Value = "Độc lập - Tự do - Hạnh phúc";
        ws.Range(2, 3, 2, totalCols).Style.Font.SetBold();

        // ── Tiêu đề ──────────────────────────────────────────────────────────
        ws.Range(3, 1, 3, totalCols).Merge().Value = "BẢNG TỔNG HỢP ĐIỂM DANH LỚP HỌC PHẦN";
        ws.Range(3, 1, 3, totalCols).Style.Font.SetBold().Font.SetFontSize(11);

        ws.Range(4, 1, 4, totalCols).Merge().Value =
            $"Học kỳ: {courseClass.Semester}   Năm học: {courseClass.AcademicYear}";
        ws.Range(4, 1, 4, totalCols).Style.Font.SetBold();

        ws.Range(5, 1, 5, 2).Merge().Value  = $"Mã học phần: {course.CourseCode}";
        ws.Range(5, 1, 5, 2).Style.Font.SetBold();
        ws.Range(5, 3, 5, totalCols).Merge().Value = $"Tên học phần: {course.CourseName}";
        ws.Range(5, 3, 5, totalCols).Style.Font.SetBold();

        ws.Range(6, 1, 6, 2).Merge().Value  = $"Số tín chỉ: {course.Credit}";
        ws.Range(6, 1, 6, 2).Style.Font.SetBold();
        ws.Range(6, 3, 6, totalCols).Merge().Value = $"Nhóm học phần: {courseClass.GroupCode}";
        ws.Range(6, 3, 6, totalCols).Style.Font.SetBold();

        ws.Range(1, 1, 6, totalCols).Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;
        ws.Range(1, 1, 6, totalCols).Style.Alignment.Vertical   = XLAlignmentVerticalValues.Center;
        ws.Rows("1:6").Height = 18;

        // ── Header bảng (row 8) ──────────────────────────────────────────────
        int headerRow = 8;
        ws.Cell(headerRow, 1).Value = "STT";
        ws.Cell(headerRow, 2).Value = "Mã SV";
        ws.Cell(headerRow, 3).Value = "Họ và tên";
        ws.Cell(headerRow, 4).Value = "Ngày sinh";
        ws.Cell(headerRow, 5).Value = "Lớp SH";

        var sessionCols = new Dictionary<int, int>(); // sessionId → column index
        int colIdx = 6;
        foreach (var sess in sessions)
        {
            ws.Cell(headerRow, colIdx).Value = sess.StartTime.ToLocalTime().ToString("dd/MM/yyyy");
            sessionCols[sess.SessionId] = colIdx;
            colIdx++;
        }
        int lastCol = colIdx;
        ws.Cell(headerRow, lastCol).Value = "Tổng vắng";

        ws.Range(headerRow, 1, headerRow, lastCol).Style
            .Font.SetBold()
            .Font.SetFontName("Times New Roman")
            .Alignment.SetHorizontal(XLAlignmentHorizontalValues.Center)
            .Alignment.SetVertical(XLAlignmentVerticalValues.Center)
            .Fill.SetBackgroundColor(XLColor.FromHtml("#d9e1f2"))
            .Border.SetOutsideBorder(XLBorderStyleValues.Thin)
            .Border.SetInsideBorder(XLBorderStyleValues.Thin);
        ws.Row(headerRow).Height = 20;

        // ── Data rows ────────────────────────────────────────────────────────
        int dataRow = headerRow + 1;
        int stt = 1;
        var students = courseClass.Enrollments
            .Select(e => e.Student)
            .OrderBy(s => s.StudentId)
            .ToList();

        foreach (var student in students)
        {
            ws.Cell(dataRow, 1).Value = stt++;
            ws.Cell(dataRow, 2).Value = student.StudentId;
            ws.Cell(dataRow, 3).Value = student.FullName;
            ws.Cell(dataRow, 4).Value = student.DateOfBirth.ToString("dd-MM-yyyy");
            ws.Cell(dataRow, 5).Value = student.ClassCode ?? "";

            int absentCount = 0;
            foreach (var sess in sessions)
            {
                var att        = sess.Attendances.FirstOrDefault(a => a.StudentId == student.StudentId);
                var statusCode = att?.Status.ToString() switch
                {
                    "PRESENT" => "P",
                    "LATE"    => "L",
                    "ABSENT"  => "A",
                    "EXCUSED" => "E",
                    _         => "-"
                };
                int c = sessionCols[sess.SessionId];
                ws.Cell(dataRow, c).Value = statusCode;
                ws.Cell(dataRow, c).Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;

                // Màu ô
                ws.Cell(dataRow, c).Style.Fill.BackgroundColor = statusCode switch
                {
                    "P" => XLColor.FromHtml("#e2efda"),
                    "L" => XLColor.FromHtml("#fff2cc"),
                    "A" => XLColor.FromHtml("#fce4d6"),
                    "E" => XLColor.FromHtml("#dae8fc"),
                    _   => XLColor.NoColor
                };

                if (statusCode == "A") absentCount++;
            }

            ws.Cell(dataRow, lastCol).Value = absentCount;
            ws.Cell(dataRow, lastCol).Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;
            if (absentCount >= 3)
                ws.Cell(dataRow, lastCol).Style.Font.FontColor = XLColor.Red;

            ws.Range(dataRow, 1, dataRow, lastCol).Style
                .Font.SetFontName("Times New Roman")
                .Alignment.SetVertical(XLAlignmentVerticalValues.Center)
                .Border.SetOutsideBorder(XLBorderStyleValues.Thin)
                .Border.SetInsideBorder(XLBorderStyleValues.Thin);

            ws.Cell(dataRow, 3).Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Left;

            dataRow++;
        }

        // ── Chú thích ký hiệu ────────────────────────────────────────────────
        int noteRow = dataRow + 1;
        ws.Range(noteRow, 1, noteRow, lastCol).Merge().Value =
            "Ghi chú: P = Có mặt  |  L = Đi muộn  |  A = Vắng  |  E = Có phép";
        ws.Cell(noteRow, 1).Style.Font.SetItalic().Alignment.SetHorizontal(XLAlignmentHorizontalValues.Left);

        // ── Footer ───────────────────────────────────────────────────────────
        int footerRow = noteRow + 1;
        ws.Range(footerRow, 1, footerRow, lastCol).Merge().Value =
            $"Danh sách này có: {students.Count} sinh viên đăng ký học";
        ws.Cell(footerRow, 1).Style.Alignment.SetHorizontal(XLAlignmentHorizontalValues.Center);

        int signRow = footerRow + 2;
        ws.Range(signRow, lastCol - 2, signRow, lastCol).Merge().Value =
            "Đà Nẵng, ngày      tháng      năm 20...";
        ws.Range(signRow + 1, lastCol - 2, signRow + 1, lastCol).Merge().Value = "Cán bộ giảng dạy";
        ws.Range(signRow + 2, lastCol - 2, signRow + 2, lastCol).Merge().Value = "(Ký và ghi rõ họ tên)";
        ws.Range(signRow, lastCol - 2, signRow + 2, lastCol).Style
            .Alignment.SetHorizontal(XLAlignmentHorizontalValues.Center)
            .Font.SetFontName("Times New Roman");
        ws.Row(signRow + 1).Style.Font.SetBold();

        // ── In ấn ────────────────────────────────────────────────────────────
        ws.PageSetup.PaperSize       = (XLPaperSize)9;
        ws.PageSetup.PageOrientation = sessions.Count > 8 ? XLPageOrientation.Landscape : XLPageOrientation.Portrait;
        ws.PageSetup.FitToPages(1, 0);

        // ── Auto-fit ─────────────────────────────────────────────────────────
        ws.Columns().AdjustToContents();
        ws.Column(3).Width = Math.Max(ws.Column(3).Width, 28);
        ws.Column(1).Width = 5;
        ws.Column(4).Width = 12;

        using var stream = new MemoryStream();
        wb.SaveAs(stream);
        return stream.ToArray();
    }
}
