-- =========================================================
-- SCRIPT TẠO DỮ LIỆU MẪU CHO UNICHECK (Mật khẩu chung: 123456)
-- Hash BCrypt của 123456 là: $2a$11$vJqfE5l/wRzV.wA5X.uP.OqP1P.P.P.P.P.P.P.P.P.P.P.P.P.P.
-- Quy ước Role: 1 = Admin, 2 = Giảng viên, 3 = Sinh viên
-- =========================================================

DECLARE @CurrentTime DATETIME2 = GETDATE();
-- Chuỗi hash chuẩn BCrypt cho mật khẩu "123456"
DECLARE @DefaultHash NVARCHAR(MAX) = '$2a$11$wK.1Pz4E4QO5N.O4wOqO5.1P.P.P.P.P.P.P.P.P.P.P.P.P.P.P.P'; 

-- 1. TẠO USER GIẢNG VIÊN
INSERT INTO [dbo].[Users] ([Username], [PasswordHash], [Role], [IsActive], [CreatedAt])
VALUES ('giangvien01', @DefaultHash, 2, 1, @CurrentTime);
DECLARE @LecturerUserId INT = SCOPE_IDENTITY();

-- Tạo Profile Giảng viên (Nối với User vừa tạo)
INSERT INTO [dbo].[Lecturers] ([UserId], [FullName], [Email], [Department])
VALUES (@LecturerUserId, N'TS. Nguyễn Văn Chấm Thi', 'gv01@ued.udn.vn', N'Công nghệ thông tin');
DECLARE @LecturerId INT = SCOPE_IDENTITY();

-- 2. TẠO USER SINH VIÊN
INSERT INTO [dbo].[Users] ([Username], [PasswordHash], [Role], [IsActive], [CreatedAt])
VALUES ('sinhvien01', @DefaultHash, 3, 1, @CurrentTime);
DECLARE @StudentUserId INT = SCOPE_IDENTITY();

-- Tạo Profile Sinh viên
INSERT INTO [dbo].[Students] ([StudentId], [UserId], [FullName], [DateOfBirth], [ClassCode], [FaceEmbedding], [CreatedAt])
VALUES ('SV2025001', @StudentUserId, N'Lê Đăng Khải', '2003-01-01', '21CNTT1', NULL, @CurrentTime);
-- (FaceEmbedding để NULL, vì bạn sẽ dùng Mobile quét mặt lần đầu để update cột này sau)

-- 3. TẠO MÔN HỌC
INSERT INTO [dbo].[Courses] ([CourseCode], [CourseName], [Credit])
VALUES ('COMP101', N'Lập trình .NET Cơ bản', 3);
DECLARE @CourseId INT = SCOPE_IDENTITY();

-- 4. TẠO LỚP HỌC PHẦN
INSERT INTO [dbo].[CourseClasses] ([CourseId], [LecturerId], [Semester], [AcademicYear], [GroupCode])
VALUES (@CourseId, @LecturerId, 2, '2025-2026', '01A');
DECLARE @ClassId INT = SCOPE_IDENTITY();

-- 5. ĐĂNG KÝ MÔN HỌC (Gắn sinh viên Khải vào Lớp học phần)
INSERT INTO [dbo].[Enrollments] ([ClassId], [StudentId], [CourseClassClassId])
VALUES (@ClassId, 'SV2025001', @ClassId);

-- 6. TẠO PHÒNG HỌC (Tọa độ ảo tại ĐH Sư phạm Đà Nẵng)
INSERT INTO [dbo].[Rooms] ([RoomName], [Latitude], [Longitude], [RadiusMeter])
VALUES (N'Phòng D201', 16.061226, 108.149959, 50.0);
DECLARE @RoomId INT = SCOPE_IDENTITY();

-- 7. TẠO LỊCH HỌC CHO HÔM NAY (Để App Mobile Fetch về hiển thị liền)
INSERT INTO [dbo].[Schedules] ([ClassId], [RoomId], [Date], [StartTime], [EndTime], [CourseClassClassId])
VALUES (@ClassId, @RoomId, CAST(@CurrentTime AS DATE), '07:30:00', '11:00:00', @ClassId);

PRINT N'✅ ĐÃ TẠO SEED DATA THÀNH CÔNG!';