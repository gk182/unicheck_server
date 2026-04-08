using Microsoft.EntityFrameworkCore;
using unicheck_backend.Models.Entities;

namespace unicheck_backend.Data;

public class AppDbContext : DbContext
{
    public AppDbContext(DbContextOptions<AppDbContext> options) : base(options) { }

    // ── DbSets ──────────────────────────────────────────────────────────────
    public DbSet<User> Users { get; set; }
    public DbSet<Student> Students { get; set; }
    public DbSet<Lecturer> Lecturers { get; set; }
    public DbSet<Course> Courses { get; set; }
    public DbSet<CourseClass> CourseClasses { get; set; }
    public DbSet<Enrollment> Enrollments { get; set; }
    public DbSet<Room> Rooms { get; set; }
    public DbSet<Schedule> Schedules { get; set; }
    public DbSet<AttendanceSession> AttendanceSessions { get; set; }
    public DbSet<Attendance> Attendances { get; set; }
    public DbSet<LeaveRequest> LeaveRequests { get; set; }

    // ── Relationships & Constraints ──────────────────────────────────────────
    protected override void OnModelCreating(ModelBuilder mb)
    {
        base.OnModelCreating(mb);

        // ── User ↔ Student (1-1) ────────────────────────────────────────────
        mb.Entity<Student>()
            .HasOne(s => s.User)
            .WithOne(u => u.Student)
            .HasForeignKey<Student>(s => s.UserId)
            .OnDelete(DeleteBehavior.Restrict);

        // ── User ↔ Lecturer (1-1) ───────────────────────────────────────────
        mb.Entity<Lecturer>()
            .HasOne(l => l.User)
            .WithOne(u => u.Lecturer)
            .HasForeignKey<Lecturer>(l => l.UserId)
            .OnDelete(DeleteBehavior.Restrict);

        // ── Course → CourseClass (1-N) ──────────────────────────────────────
        mb.Entity<CourseClass>()
            .HasOne(cc => cc.Course)
            .WithMany(c => c.CourseClasses)
            .HasForeignKey(cc => cc.CourseId)
            .OnDelete(DeleteBehavior.Restrict);

        // ── Lecturer → CourseClass (1-N) ────────────────────────────────────
        mb.Entity<CourseClass>()
            .HasOne(cc => cc.Lecturer)
            .WithMany(l => l.CourseClasses)
            .HasForeignKey(cc => cc.LecturerId)
            .OnDelete(DeleteBehavior.Restrict);

        // ── CourseClass → Enrollment (1-N) ──────────────────────────────────
        mb.Entity<Enrollment>()
            .HasOne(e => e.CourseClass)
            .WithMany(cc => cc.Enrollments)
            .HasForeignKey(e => e.ClassId)
            .OnDelete(DeleteBehavior.Cascade);

        // ── Student → Enrollment (1-N) ──────────────────────────────────────
        mb.Entity<Enrollment>()
            .HasOne(e => e.Student)
            .WithMany(s => s.Enrollments)
            .HasForeignKey(e => e.StudentId)
            .OnDelete(DeleteBehavior.Restrict);

        // UNIQUE: 1 SV không đăng ký cùng lớp 2 lần
        mb.Entity<Enrollment>()
            .HasIndex(e => new { e.ClassId, e.StudentId })
            .IsUnique();

        // ── CourseClass → Schedule (1-N) ────────────────────────────────────
        mb.Entity<Schedule>()
            .HasOne(s => s.CourseClass)
            .WithMany(cc => cc.Schedules)
            .HasForeignKey(s => s.ClassId)
            .OnDelete(DeleteBehavior.Cascade);

        // ── Room → Schedule (1-N) ───────────────────────────────────────────
        mb.Entity<Schedule>()
            .HasOne(s => s.Room)
            .WithMany(r => r.Schedules)
            .HasForeignKey(s => s.RoomId)
            .OnDelete(DeleteBehavior.Restrict);

        // ── Schedule → AttendanceSession (1-N) ─────────────────────────────
        mb.Entity<AttendanceSession>()
            .HasOne(a => a.Schedule)
            .WithMany(s => s.AttendanceSessions)
            .HasForeignKey(a => a.ScheduleId)
            .OnDelete(DeleteBehavior.Cascade);

        // ── AttendanceSession → Attendance (1-N) ───────────────────────────
        mb.Entity<Attendance>()
            .HasOne(a => a.Session)
            .WithMany(s => s.Attendances)
            .HasForeignKey(a => a.SessionId)
            .OnDelete(DeleteBehavior.Cascade);

        // ── Student → Attendance (1-N) ──────────────────────────────────────
        mb.Entity<Attendance>()
            .HasOne(a => a.Student)
            .WithMany(s => s.Attendances)
            .HasForeignKey(a => a.StudentId)
            .OnDelete(DeleteBehavior.Restrict);

        // UNIQUE: 1 SV chỉ có 1 bản ghi điểm danh trong 1 phiên
        mb.Entity<Attendance>()
            .HasIndex(a => new { a.SessionId, a.StudentId })
            .IsUnique();

        // ── Student → LeaveRequest (1-N) ────────────────────────────────────
        mb.Entity<LeaveRequest>()
            .HasOne(r => r.Student)
            .WithMany(s => s.LeaveRequests)
            .HasForeignKey(r => r.StudentId)
            .OnDelete(DeleteBehavior.Restrict);

        // ── Schedule → LeaveRequest (1-N) ───────────────────────────────────
        mb.Entity<LeaveRequest>()
            .HasOne(r => r.Schedule)
            .WithMany()
            .HasForeignKey(r => r.ScheduleId)
            .OnDelete(DeleteBehavior.Restrict);

        // UNIQUE: 1 sinh vien chi duoc tao 1 don cho 1 schedule
        mb.Entity<LeaveRequest>()
            .HasIndex(r => new { r.ScheduleId, r.StudentId })
            .IsUnique();

        // ── Index thường dùng để tăng performance ───────────────────────────
        mb.Entity<User>()
            .HasIndex(u => u.Username).IsUnique();

        mb.Entity<Course>()
            .HasIndex(c => c.CourseCode).IsUnique();

        mb.Entity<AttendanceSession>()
            .HasIndex(s => s.QrToken); // tra cứu nhanh khi SV check-in
    }
}
