using Microsoft.EntityFrameworkCore;
using unicheck_backend.Data;
using unicheck_backend.Models.Entities;
using unicheck_backend.Models.Enums;

namespace unicheck_backend.Services;

public class LeaveService
{
    private readonly AppDbContext _db;

    public LeaveService(AppDbContext db) => _db = db;

    public async Task<List<LeaveRequestViewModel>> GetByLecturerAsync(int lecturerId)
    {
        return await _db.LeaveRequests
            .Include(r => r.Student)
            .Include(r => r.Schedule)
                .ThenInclude(s => s.CourseClass)
                    .ThenInclude(cc => cc.Course)
            .Where(r => r.Schedule.CourseClass.LecturerId == lecturerId)
            .OrderByDescending(r => r.Schedule.Date)
            .Select(r => new LeaveRequestViewModel
            {
                RequestId = r.RequestId,
                StudentId = r.StudentId,
                StudentName = r.Student.FullName,
                CourseName = r.Schedule.CourseClass.Course.CourseName,
                SessionDate = r.Schedule.Date.ToString("dd/MM/yyyy"),
                Reason = r.Reason,
                Status = r.Status.ToString(),
                AttachmentUrl = r.AttachmentUrl,
                CreatedAt = r.CreatedAt.ToString("dd/MM/yyyy HH:mm")
            })
            .ToListAsync();
    }

    public async Task ApproveAsync(int requestId, int lecturerId)
    {
        var request = await GetAndValidate(requestId, lecturerId);
        if (request.Status != LeaveRequestStatus.PENDING)
            throw new InvalidOperationException("Don nay khong o trang thai cho duyet.");

        request.Status = LeaveRequestStatus.APPROVED;
        request.ReviewedAt = DateTime.UtcNow;
        await _db.SaveChangesAsync();
    }

    public async Task RejectAsync(int requestId, int lecturerId, string? reviewNote = null)
    {
        var request = await GetAndValidate(requestId, lecturerId);
        if (request.Status != LeaveRequestStatus.PENDING)
            throw new InvalidOperationException("Don nay khong o trang thai cho duyet.");

        request.Status = LeaveRequestStatus.REJECTED;
        request.ReviewedAt = DateTime.UtcNow;
        request.ReviewNote = string.IsNullOrWhiteSpace(reviewNote) ? null : reviewNote.Trim();
        await _db.SaveChangesAsync();
    }

    private async Task<LeaveRequest> GetAndValidate(int requestId, int lecturerId)
    {
        var request = await _db.LeaveRequests
            .Include(r => r.Schedule).ThenInclude(s => s.CourseClass)
            .FirstOrDefaultAsync(r => r.RequestId == requestId);

        if (request is null)
            throw new InvalidOperationException("Khong tim thay don xin nghi.");

        if (request.Schedule.CourseClass.LecturerId != lecturerId)
            throw new UnauthorizedAccessException("Ban khong co quyen xu ly don nay.");

        return request;
    }
}

public class LeaveRequestViewModel
{
    public int RequestId { get; set; }
    public string StudentId { get; set; } = "";
    public string StudentName { get; set; } = "";
    public string CourseName { get; set; } = "";
    public string SessionDate { get; set; } = "";
    public string Reason { get; set; } = "";
    public string Status { get; set; } = "PENDING";
    public string? AttachmentUrl { get; set; }
    public string CreatedAt { get; set; } = "";
}
