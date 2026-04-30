using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using BCrypt.Net;
using unicheck_backend.Models.Entities;
using unicheck_backend.Models.Enums;

namespace unicheck_backend.Data;

public static class DbSeeder
{
    public static async Task SeedAsync(IServiceProvider services)
    {
        using var scope = services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        // Nếu đã có data thì chỉ đảm bảo có tài khoản admin demo rồi thoát.
        if (db.Users.Any())
        {
            var hasAdmin = db.Users.Any(u => u.Role == UserRole.ADMIN);
            if (!hasAdmin)
            {
                db.Users.Add(new User
                {
                    Username = "admin001",
                    PasswordHash = BCrypt.Net.BCrypt.HashPassword("password123"),
                    Role = UserRole.ADMIN,
                    IsActive = true,
                    CreatedAt = DateTime.UtcNow
                });
                await db.SaveChangesAsync();
                Console.WriteLine("[DbSeeder] Added missing demo admin account: admin001/password123.");
            }

            return;
        }

        Console.WriteLine("[DbSeeder] Đang khởi tạo dữ liệu thực tế mô phỏng Khoa CNTT - UED...");

        var random = new Random();
        var defaultPasswordHash = BCrypt.Net.BCrypt.HashPassword("password123");

        // ==========================================
        // 1. TẠO PHÒNG HỌC (Tọa độ ĐH Sư Phạm Đà Nẵng)
        // ==========================================
        var rooms = new List<Room>
        {
            new Room { RoomName = "B3-101", Capacity = 100, Latitude = 16.061020, Longitude = 108.150110 },
            new Room { RoomName = "A6-202", Capacity = 60, Latitude = 16.060950, Longitude = 108.150400 },
            new Room { RoomName = "A5-302", Capacity = 120, Latitude = 16.060700, Longitude = 108.150250 },
            new Room { RoomName = "PMT-01", Capacity = 50, Latitude = 16.060800, Longitude = 108.150500 }
        };
        db.Rooms.AddRange(rooms);
        await db.SaveChangesAsync();

        // ==========================================
        // 2. TẠO MÔN HỌC
        // ==========================================
        var courseMain = new Course { CourseCode = "31231755", CourseName = "Phát triển ứng dụng Di động", Credit = 3 };
        var courseAI = new Course { CourseCode = "31231756", CourseName = "Trí tuệ Nhân tạo", Credit = 3 };
        var courseDB = new Course { CourseCode = "31231757", CourseName = "Cơ sở dữ liệu Nâng cao", Credit = 3 };
        var courses = new List<Course> { courseMain, courseAI, courseDB };
        db.Courses.AddRange(courses);
        await db.SaveChangesAsync();

        // ==========================================
        // 3. TẠO TÀI KHOẢN ADMIN DEMO
        // ==========================================
        var adminUser = new User { Username = "admin001", PasswordHash = defaultPasswordHash, Role = UserRole.ADMIN, IsActive = true, CreatedAt = DateTime.UtcNow };
        db.Users.Add(adminUser);
        await db.SaveChangesAsync();

        // ==========================================
        // 4. TẠO DUY NHẤT 1 GIẢNG VIÊN
        // ==========================================
        var gvUser = new User { Username = "gv001", PasswordHash = defaultPasswordHash, Role = UserRole.LECTURER, IsActive = true, CreatedAt = DateTime.UtcNow };
        db.Users.Add(gvUser);
        await db.SaveChangesAsync();

        var lecturer = new Lecturer { UserId = gvUser.Id, FullName = "TS. Nguyễn Văn A", Email = "nva@ued.udn.vn", Department = "Công nghệ Phần mềm" };
        db.Lecturers.Add(lecturer);
        await db.SaveChangesAsync();

        // ==========================================
        // 5. TẠO LỚP HỌC PHẦN (Tất cả do gv001 dạy)
        // ==========================================
        var classMain = new CourseClass { CourseId = courseMain.CourseId, LecturerId = lecturer.LecturerId, Semester = 2, AcademicYear = "2025-2026", GroupCode = "22-0302" };
        var classAI = new CourseClass { CourseId = courseAI.CourseId, LecturerId = lecturer.LecturerId, Semester = 2, AcademicYear = "2025-2026", GroupCode = "22-0305" };
        var classDB = new CourseClass { CourseId = courseDB.CourseId, LecturerId = lecturer.LecturerId, Semester = 2, AcademicYear = "2025-2026", GroupCode = "22-0308" };
        var courseClasses = new List<CourseClass> { classMain, classAI, classDB };
        db.CourseClasses.AddRange(courseClasses);
        await db.SaveChangesAsync();

        // ==========================================
        // 6. TẠO 60 SINH VIÊN VÀ ĐĂNG KÝ MÔN (ENROLLMENTS)
        // ==========================================
        var students = new List<Student>();
        for (int i = 1; i <= 60; i++)
        {
            string stuId = $"3120222{i:D3}";
            var user = new User { Username = $"sv{stuId}", PasswordHash = defaultPasswordHash, Role = UserRole.STUDENT, IsActive = true, CreatedAt = DateTime.UtcNow };
            db.Users.Add(user);
            await db.SaveChangesAsync();

            string classCode = i <= 30 ? "22TCNTT01" : "22TCNTT02";
            var student = new Student
            {
                StudentId = stuId,
                UserId = user.Id,
                FullName = GenerateRandomVietnameseName(random),
                DateOfBirth = new DateTime(2004, random.Next(1, 13), random.Next(1, 28)),
                ClassCode = classCode,
                Faculty = "Khoa CNTT",
                Major = "Công nghệ Thông tin",
                Email = $"{stuId}@st.ued.udn.vn"
            };
            students.Add(student);
            db.Students.Add(student);
        }
        await db.SaveChangesAsync();

        var enrollments = new List<Enrollment>();
        foreach (var sv in students)
        {
            if (sv.ClassCode == "22TCNTT01")
            {
                enrollments.Add(new Enrollment { ClassId = classMain.ClassId, StudentId = sv.StudentId });
                enrollments.Add(new Enrollment { ClassId = classAI.ClassId, StudentId = sv.StudentId });
            }
            else
            {
                enrollments.Add(new Enrollment { ClassId = classAI.ClassId, StudentId = sv.StudentId });
                enrollments.Add(new Enrollment { ClassId = classDB.ClassId, StudentId = sv.StudentId });
            }
        }
        db.Enrollments.AddRange(enrollments);
        await db.SaveChangesAsync();

        // ==========================================
        // 6. TẠO LỊCH HỌC TỪ HÔM NAY TRỞ ĐI ĐỂ DỄ TEST
        // ==========================================
        var today = DateTime.Today;
        var semesterStartDate = today;
        
        var scheduleTemplates = new[] {
            new { ClassObj = classMain, DayOffset = 0, Start = new TimeSpan(7, 15, 0), End = new TimeSpan(9, 30, 0), Room = rooms[0] }, // T2
            new { ClassObj = classAI, DayOffset = 2, Start = new TimeSpan(13, 15, 0), End = new TimeSpan(15, 30, 0), Room = rooms[1] },   // T4
            new { ClassObj = classDB, DayOffset = 4, Start = new TimeSpan(15, 45, 0), End = new TimeSpan(18, 0, 0), Room = rooms[2] }     // T6
        };

        for (int week = 0; week < 15; week++)
        {
            var weekStartDate = semesterStartDate.AddDays(week * 7);

            foreach (var tpl in scheduleTemplates)
            {
                var classDate = weekStartDate.AddDays(tpl.DayOffset);

                if (classDate >= today)
                {
                    var schedule = new Schedule
                    {
                        ClassId = tpl.ClassObj.ClassId,
                        RoomId = tpl.Room.RoomId,
                        Date = classDate,
                        StartTime = tpl.Start,
                        EndTime = tpl.End
                    };
                    db.Schedules.Add(schedule);
                    await db.SaveChangesAsync();

                    var session = new AttendanceSession
                    {
                        ScheduleId = schedule.ScheduleId,
                        QrToken = Guid.NewGuid().ToString("N"),
                        QrTokenExpiry = classDate.Add(tpl.End),
                        StartTime = classDate.Add(tpl.Start),
                        EndTime = classDate.Add(tpl.End),
                        IsActive = false // Đã kết thúc
                    };
                    db.AttendanceSessions.Add(session);
                    await db.SaveChangesAsync();

                    var enrolledStudents = enrollments.Where(e => e.ClassId == tpl.ClassObj.ClassId).Select(e => e.StudentId).ToList();
                    
                    foreach (var svId in enrolledStudents)
                    {
                        int randVal = random.Next(100);
                        AttendanceStatus status;
                        TimeSpan checkInTime = tpl.Start;

                        if (randVal < 80) { 
                            status = AttendanceStatus.PRESENT; 
                            checkInTime = tpl.Start.Subtract(TimeSpan.FromMinutes(random.Next(0, 15))); 
                        }
                        else if (randVal < 90) { 
                            status = AttendanceStatus.LATE; 
                            checkInTime = tpl.Start.Add(TimeSpan.FromMinutes(random.Next(5, 30))); 
                        }
                        else { 
                            status = AttendanceStatus.ABSENT; 
                        }

                        if (status != AttendanceStatus.ABSENT)
                        {
                            db.Attendances.Add(new Attendance
                            {
                                SessionId = session.SessionId,
                                StudentId = svId,
                                Status = status,
                                CheckInTime = classDate.Add(checkInTime),
                                FaceVerified = true,
                                LocationVerified = true
                            });
                        }
                        else
                        {
                            db.Attendances.Add(new Attendance { SessionId = session.SessionId, StudentId = svId, Status = status });
                        }
                    }
                }
            }
        }
        await db.SaveChangesAsync();

        // ==========================================
        // 7. CA TEST HIỆN TẠI VÀ CA SẮP TỚI
        // ==========================================
        var now = DateTime.Now;
        var currentStart = now.AddMinutes(-5);
        if (currentStart.Date != now.Date)
        {
            currentStart = today.AddHours(8);
        }

        var currentEnd = currentStart.AddHours(1);
        var nextStart = currentStart.AddHours(2);
        if (nextStart.Date != currentStart.Date)
        {
            nextStart = currentStart.Date.AddHours(10);
        }

        var nextEnd = nextStart.AddHours(1);

        var currentSchedule = new Schedule
        {
            ClassId = classMain.ClassId,
            RoomId = rooms[0].RoomId,
            Date = currentStart.Date,
            StartTime = currentStart.TimeOfDay,
            EndTime = currentEnd.TimeOfDay
        };
        db.Schedules.Add(currentSchedule);

        var nextSchedule = new Schedule
        {
            ClassId = classAI.ClassId,
            RoomId = rooms[1].RoomId,
            Date = nextStart.Date,
            StartTime = nextStart.TimeOfDay,
            EndTime = nextEnd.TimeOfDay
        };
        db.Schedules.Add(nextSchedule);

        await db.SaveChangesAsync();

        // Đã xóa phần tạo AttendanceSession tự động.
        // Bạn hãy đăng nhập tài khoản giảng viên (gv001) để tự mở phiên.

        Console.WriteLine("[DbSeeder] ✅ Hoàn tất! Demo account: admin001/password123 (ADMIN), gv001/password123 (LECTURER).");
    }

    private static string GenerateRandomVietnameseName(Random r)
    {
        string[] last = { "Nguyễn", "Trần", "Lê", "Phạm", "Hoàng", "Huỳnh", "Phan", "Vũ", "Võ", "Đặng", "Bùi", "Đỗ" };
        string[] middle = { "Văn", "Thị", "Hữu", "Thanh", "Minh", "Thu", "Ngọc", "Hoàng", "Xuân", "Gia", "Hải", "Khánh" };
        string[] first = { "Anh", "Tuấn", "Dũng", "Linh", "Trang", "Hương", "Hùng", "Cường", "Mai", "Lan", "Nhung", "Phương", "Phúc", "Quang", "Sơn", "Hải" };

        return $"{last[r.Next(last.Length)]} {middle[r.Next(middle.Length)]} {first[r.Next(first.Length)]}";
    }
}