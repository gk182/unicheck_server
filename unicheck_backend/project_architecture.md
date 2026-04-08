# 📐 UniCheck Backend — Tài Liệu Kiến Trúc Kỹ Thuật

> **Mục tiêu:** Hệ thống điểm danh sinh viên bằng QR Code + Xác thực khuôn mặt AI + GPS  
> **Stack:** ASP.NET Core 8 · EF Core · SQL Server · **Blazor Server** · SignalR · JWT/Cookie Auth  
> **Phiên bản tài liệu:** 1.1 — Cập nhật 19/03/2026 (Blazor Server UI + Sprint reorder)

---

## 1. 🧭 Tổng Quan Nghiệp Vụ

### 1.1 Các Actor trong hệ thống

| Actor | Mô tả | Giao tiếp qua |
|-------|--------|--------------|
| **Giảng viên** | Quản lý lớp, bắt đầu/kết thúc phiên điểm danh, xem báo cáo | **Blazor Server** (Cookie Auth) — reactive UI real-time |
| **Sinh viên** | Quét QR, xác thực khuôn mặt + GPS để check-in | Flutter Mobile App + JWT Bearer API |
| **Admin** | Quản lý user, danh mục môn học, phòng học (tương lai) | Web Admin (tương lai) |

> [!IMPORTANT]
> **Tại sao Blazor Server?**  
> Khi sinh viên check-in từ mobile, server cập nhật DB → Blazor Server tự động push thay đổi xuống trình duyệt giảng viên **mà không cần viết JavaScript SignalR client thủ công**. Blazor Server dùng SignalR làm transport layer bên dưới, nhưng lập trình hoàn toàn bằng C#.

### 1.2 Luồng nghiệp vụ chính

**A. Đăng ký khuôn mặt lần đầu (Mobile App)**
```
[Sinh viên] Đăng nhập app
      ↓
[Mobile] Gọi GET /api/students/me
      ↓
      ├── Nếu `isFaceRegistered` == true ➡ Vào trang chủ (Quét QR point)
      |
      └── Nếu `isFaceRegistered` == false ➡ Chuyển tới "Hướng dẫn quét khuôn mặt"
              ↓
         [Mobile] Camera chụp mặt, gửi POST /api/students/register-face
              ↓
         [Server] Ktra nhanh: Nếu FaceEmbedding trong DB đã tồn tại ➡ Báo lỗi 
                  (Bảo đảm chỉ được lấy data 1 lần)
              ↓
         [FaceService] Ai xử lý trích xuất vector 128-dim
              ↓
         [Server] Lưu kết quả vào trường `Student.FaceEmbedding`
              ↓
         [Mobile] Báo thành công, cho phép sử dụng app.
```

**B. Điểm danh trực tiếp trên lớp**
```
[Giảng viên] Đăng nhập
      ↓
[Dashboard] Xem lịch dạy hôm nay
      ↓
[Bắt đầu phiên] → Tạo AttendanceSession + sinh QrToken (hết hạn 20s)
      ↓
[Live Session Screen] Chiếu lên màn chiếu
      |
      ├── QR Code đổi mới mỗi 20 giây
      |
      └── Sinh viên quét QR (Flutter app)
              ↓
         [Mobile] Gửi POST /api/attendance/check-in
                  { qrToken, studentId, faceImage(base64), latitude, longitude }
              ↓
         [Server] Xác thực token còn hạn?
              ↓
         [FaceService] So sánh face embedding (cosine similarity ≥ threshold)
              ↓
         [GpsService] Tính khoảng cách Haversine vs tọa độ phòng học (≤ 100m)
              ↓
         [AttendanceService] Ghi Attendance record
              ↓
         [AttendanceStateService] Cập nhật in-memory state (Blazor DI)
              ↓
         [Blazor LiveSession.razor] NotifyStateChanged() → UI tự re-render real-time
              ↓
[Kết thúc phiên] → End session, tất cả SV chưa check-in → trạng thái "Vắng"
      ↓
[Class Detail] Giảng viên xem, sửa thủ công, xuất Excel
```

---

## 2. 🗄️ Data Model (ERD Logic)

### 2.1 Sơ đồ quan hệ

```
User (1) ──────────── (1) Student
User (1) ──────────── (1) Lecturer

Course (1) ─────────── (N) CourseClass
Lecturer (1) ──────── (N) CourseClass
CourseClass (1) ────── (N) Enrollment  ←── Student (1)
CourseClass (1) ────── (N) Schedule

Room (1) ────────────── (N) Schedule
Schedule (1) ─────────── (N) AttendanceSession
AttendanceSession (1) ── (N) Attendance ─── Student (1)

Student (1) ─────────── (N) LeaveRequest ──→ Schedule (1)
```

### 2.2 Mô tả từng Entity

#### [User](file:///d:/Project/KLTN/UniCheck/unicheck_backend/Models/Entities/User.cs#6-28) — Tài khoản hệ thống
| Field | Type | Mô tả |
|-------|------|--------|
| `Id` | int PK | Auto |
| `Username` | string(50) | Duy nhất |
| `PasswordHash` | string | BCrypt hash |
| `Role` | enum UserRole | [Student](file:///d:/Project/KLTN/UniCheck/unicheck_backend/Models/Entities/Student.cs#6-34), [Lecturer](file:///d:/Project/KLTN/UniCheck/unicheck_backend/Models/Entities/Lecturer.cs#5-26), `Admin` |
| `IsActive` | bool | Cho phép vô hiệu hóa |
| `CreatedAt` | DateTime | UTC |

#### [Student](file:///d:/Project/KLTN/UniCheck/unicheck_backend/Models/Entities/Student.cs#6-34) — Hồ sơ sinh viên
| Field | Type | Mô tả |
|-------|------|--------|
| `StudentId` | string PK | Mã sinh viên (VD: "20210001") — không auto |
| `UserId` | int FK | → User |
| `FullName` | string(100) | |
| `DateOfBirth` | DateTime | |
| `ClassCode` | string(20) | Lớp hành chính (VD: "CNTT2021A") |
| `FaceEmbedding` | string? | JSON array float[] 128-dim (FaceNet) |

> ⚠️ **Quan trọng:** `FaceEmbedding` lưu dưới dạng JSON string để tránh schema phức tạp. Khi dùng: `JsonSerializer.Deserialize<float[]>(student.FaceEmbedding)`

#### [Lecturer](file:///d:/Project/KLTN/UniCheck/unicheck_backend/Models/Entities/Lecturer.cs#5-26) — Hồ sơ giảng viên
| Field | Type | Mô tả |
|-------|------|--------|
| `LecturerId` | int PK | Auto |
| `UserId` | int FK | → User |
| `FullName` | string(100) | |
| `Email` | string | Email nhận thông báo |
| `Department` | string(100) | Khoa/Bộ môn |

#### [Course](file:///d:/Project/KLTN/UniCheck/unicheck_backend/Models/Entities/Course.cs#5-21) — Môn học (danh mục)
| Field | Type | Mô tả |
|-------|------|--------|
| `CourseId` | int PK | |
| `CourseCode` | string(20) | VD: "INT3120" |
| `CourseName` | string(200) | VD: "Phát triển ứng dụng Web" |
| `Credit` | int | Số tín chỉ |

#### [CourseClass](file:///d:/Project/KLTN/UniCheck/unicheck_backend/Models/Entities/CourseClass.cs#10-36) — Lớp học phần (1 môn x 1 giảng viên x 1 học kỳ)
| Field | Type | Mô tả |
|-------|------|--------|
| `ClassId` | int PK | |
| `CourseId` | int FK | → Course |
| `LecturerId` | int FK | → Lecturer |
| `Semester` | int | 1 hoặc 2 |
| `AcademicYear` | string(20) | VD: "2025-2026" |
| `GroupCode` | string(50) | VD: "N01" — nhóm học |

> **Ví dụ:** "INT3120 - N01 - HK1 2025-2026 - GV: Nguyễn Văn A"

#### [Enrollment](file:///d:/Project/KLTN/UniCheck/unicheck_backend/Models/Entities/Enrollment.cs#9-24) — Sinh viên đăng ký lớp học phần
| Field | Type | Mô tả |
|-------|------|--------|
| `Id` | int PK | |
| `ClassId` | int FK | → CourseClass |
| `StudentId` | string FK | → Student |

> **Constraint quan trọng:** [(ClassId, StudentId)](file:///d:/Project/KLTN/UniCheck/unicheck_backend/Models/Entities/User.cs#6-28) UNIQUE — 1 SV không đăng ký lớp 2 lần

#### [Room](file:///d:/Project/KLTN/UniCheck/unicheck_backend/Models/Entities/Room.cs#5-25) — Phòng học
| Field | Type | Mô tả |
|-------|------|--------|
| `RoomId` | int PK | |
| `RoomName` | string(50) | VD: "B1-201" |
| `Latitude` | double | Tọa độ GPS phòng học |
| `Longitude` | double | Tọa độ GPS phòng học |
| `Capacity` | int | Sức chứa |

> ⚠️ **Thiếu:** Entity [Room.cs](file:///d:/Project/KLTN/UniCheck/unicheck_backend/Models/Entities/Room.cs) hiện tại **chưa có `Latitude`, `Longitude`** — cần thêm để verify GPS

#### [Schedule](file:///d:/Project/KLTN/UniCheck/unicheck_backend/Models/Entities/Schedule.cs#8-29) — Buổi học (lịch cụ thể từng buổi)
| Field | Type | Mô tả |
|-------|------|--------|
| `ScheduleId` | int PK | |
| `ClassId` | int FK | → CourseClass |
| `RoomId` | int FK | → Room |
| `Date` | DateTime | Ngày học |
| `StartTime` | TimeSpan | Giờ bắt đầu |
| `EndTime` | TimeSpan | Giờ kết thúc |

#### [AttendanceSession](file:///d:/Project/KLTN/UniCheck/unicheck_backend/Models/Entities/AttendanceSession.cs#9-33) — Phiên điểm danh (1 phiên / 1 buổi học)
| Field | Type | Mô tả |
|-------|------|--------|
| `SessionId` | int PK | |
| `ScheduleId` | int FK | → Schedule |
| `QrToken` | string | GUID ngẫu nhiên, đổi mỗi 20 giây |
| `QrTokenExpiry` | DateTime | Thời hạn token hiện tại |
| `StartTime` | DateTime | Khi GV bắt đầu phiên |
| `EndTime` | DateTime | Khi GV kết thúc phiên |
| `IsActive` | bool | Phiên đang hoạt động |

> ⚠️ **Thiếu:** `QrTokenExpiry` — Entity chưa có field này! Cần thêm để validate token

#### [Attendance](file:///d:/Project/KLTN/UniCheck/unicheck_backend/Models/Entities/Attendance.cs#10-45) — Bản ghi điểm danh từng sinh viên
| Field | Type | Mô tả |
|-------|------|--------|
| `Id` | int PK | |
| `SessionId` | int FK | → AttendanceSession |
| `StudentId` | string FK | → Student |
| `CheckInTime` | DateTime? | Null nếu vắng |
| `Latitude` | double? | GPS khi check-in |
| `Longitude` | double? | GPS khi check-in |
| `FaceVerified` | bool | AI xác nhận khuôn mặt |
| `LocationVerified` | bool | GPS đủ gần phòng học |
| `FaceConfidence` | double? | ⚠️ **Thiếu** — Điểm confidence AI |
| `Status` | enum | `Present`, `Late`, `Absent`, `Excused` |
| `AbsenceType` | enum | `Unexcused`, `Excused`, `_` |
| `Note` | string? | GV ghi chú thủ công |

#### [LeaveRequest](file:///d:/Project/KLTN/UniCheck/unicheck_backend/Models/Entities/LeaveRequest.cs#9-38) — Đơn xin vắng có phép
| Field | Type | Mô tả |
|-------|------|--------|
| `RequestId` | int PK | |
| `StudentId` | string FK | → Student |
| `ScheduleId` | int FK | → Schedule (buổi xin nghỉ) |
| `Reason` | string | Lý do |
| `AttachmentUrl` | string? | File minh chứng |
| `Status` | enum | `Pending`, `Approved`, `Rejected` |
| `ReviewedBy` | int? | ⚠️ **Thiếu** — LecturerId người duyệt |
| `ReviewedAt` | DateTime? | ⚠️ **Thiếu** — Thời gian duyệt |

---

## 3. 🐛 Danh Sách Lỗi Cần Sửa Ngay

> [!CAUTION]
> Các lỗi dưới đây sẽ khiến project **không build được** hoặc **migration sai**.

### 3.1 Lỗi cú pháp namespace (toàn bộ Entities)

Tất cả các file trong `Models/Entities/` đang viết sai — thiếu dấu `{}` bao quanh class:

```csharp
// ❌ SAI (file-scoped namespace — cần C# 10+ và phải kết thúc bằng ;)
namespace unicheck_backend.Models
public class Student { ... }

// ✅ ĐÚNG — cách 1: block namespace
namespace unicheck_backend.Models
{
    public class Student { ... }
}

// ✅ ĐÚNG — cách 2: file-scoped (C# 10+, cần có dấu ;)
namespace unicheck_backend.Models;
public class Student { ... }
```

**➡ Quyết định:** Dùng file-scoped namespace (cách 2) — thêm dấu `;` vào sau tất cả `namespace` declarations.

### 3.2 `CourseClass.cs` có self-referencing property sai

```csharp
// ❌ SAI — vô nghĩa, gây vòng lặp vô hạn
public CourseClass CourseClass { get; set; }

// ✅ XÓA đi, không cần thiết
```

### 3.3 `AppDbContext` thiếu `OnModelCreating`

Hiện tại không có cấu hình FK, các quan hệ EF Core phải suy luận tự động → dễ sai.

### 3.4 `Room.cs` thiếu tọa độ GPS

```csharp
// Cần thêm:
public double Latitude { get; set; }
public double Longitude { get; set; }
```

### 3.5 `AttendanceSession.cs` thiếu `QrTokenExpiry`

```csharp
// Cần thêm:
public DateTime QrTokenExpiry { get; set; }
```

### 3.6 Namespace `unicheck_backend.Enums` không nhất quán

`User.cs` import `using unicheck_backend.Enums;` nhưng các enum files có thể không có namespace đó. Cần kiểm tra.

---

## 4. 🏗️ Cấu Trúc Thư Mục Chuẩn

```
unicheck_backend/
│
├── Controllers/
│   ├── web/                        ← Blazor auth entry point (Cookie)
│   │   └── AuthController.cs       ← GET/POST /Auth/Login, /Auth/Logout
│   │
│   └── Api/                        ← REST API (JWT, cho mobile Flutter)
│       ├── AuthApiController.cs       ← POST /api/auth/login
│       ├── AttendanceApiController.cs ← POST /api/attendance/check-in
│       ├── ScheduleApiController.cs   ← GET /api/schedules/today
│       ├── StudentApiController.cs    ← GET /api/students/me
│       └── LeaveApiController.cs      ← POST /api/leave-requests
│
├── Components/                     ← ✨ Blazor Server Components (GV Web)
│   ├── _Imports.razor              ← Global using cho Blazor
│   ├── App.razor                   ← Root component + Router
│   ├── Routes.razor                ← Route definitions
│   ├── Layout/
│   │   ├── MainLayout.razor        ← Layout chính: Sidebar + Header + Body
│   │   ├── Sidebar.razor           ← Navigation menu (lớp, lịch, báo cáo)
│   │   └── Header.razor            ← Thông tin GV + nút logout
│   ├── Pages/
│   │   ├── Auth/
│   │   │   └── Login.razor         ← /login — Form đăng nhập GV
│   │   ├── Dashboard/
│   │   │   └── Dashboard.razor     ← /dashboard — Lịch dạy hôm nay
│   │   ├── Sessions/
│   │   │   ├── LiveSession.razor   ← /session/{scheduleId} — Màn chiếu QR + SV list
│   │   │   └── ClassDetail.razor   ← /session/{id}/detail — Xem/sửa sau phiên
│   │   ├── Lecturer/
│   │   │   └── MyClasses.razor     ← /classes — Danh sách lớp học phần
│   │   └── Leave/
│   │       └── LeaveRequests.razor ← /leaves — GV duyệt đơn nghỉ
│   └── Shared/
│       ├── StudentCard.razor       ← Card 1 SV check-in (dùng trong LiveSession)
│       ├── QrDisplay.razor         ← QR code + countdown tự đổi
│       ├── StatCard.razor          ← Card thống kê (Sĩ số / Có mặt / Vắng)
│       ├── AttendanceTable.razor   ← Bảng điểm danh có thể edit inline
│       └── Toast.razor             ← Thông báo toast
│
├── Services/                       ← Business Logic Layer
│   ├── AuthService.cs              ← Login, JWT gen, BCrypt
│   ├── AttendanceService.cs        ← StartSession, CheckIn, EndSession
│   ├── AttendanceStateService.cs   ← [NEW] Scoped state cho Blazor real-time
│   ├── QrCodeService.cs            ← GenerateToken, GenerateQrImage, ValidateToken
│   ├── FaceService.cs              ← VerifyFace (cosine similarity + HTTP AI)
│   ├── GpsService.cs               ← Haversine distance
│   ├── LeaveService.cs             ← Submit, Approve, Reject leave
│   ├── ScheduleService.cs          ← GetTodaySchedules
│   └── ExportService.cs            ← Export Excel (ClosedXML)
│
├── Models/
│   ├── Entities/                   ← EF Core DB models (✅ đã hoàn thiện Sprint 1)
│   ├── DTOs/                       ← API request/response (mobile ↔ server)
│   │   ├── Auth/
│   │   │   ├── LoginRequestDto.cs
│   │   │   └── LoginResponseDto.cs
│   │   ├── Attendance/
│   │   │   ├── CheckInRequestDto.cs   ← { qrToken, faceImageBase64, lat, lng }
│   │   │   └── CheckInResponseDto.cs  ← { success, status, message }
│   │   ├── Schedule/
│   │   │   └── ScheduleSummaryDto.cs
│   │   └── Leave/
│   │       └── LeaveRequestDto.cs
│   ├── ViewModels/                 ← Data models cho Blazor components
│   │   ├── Attendance/             ← (✅ đã có một số files)
│   │   ├── Dashboard/
│   │   │   └── DashboardViewModel.cs
│   │   └── Sessions/
│   │       ├── LiveSessionViewModel.cs
│   │       └── ClassDetailViewModel.cs
│   └── Enums/                      ← (✅ đã hoàn thiện Sprint 1)
│
├── Data/
│   └── AppDbContext.cs             ← (✅ đã hoàn thiện Sprint 1)
│
├── Migrations/                     ← EF Core generated
│
├── Views/                          ← Chỉ còn 1 view cho Auth entry + Error
│   ├── Shared/
│   │   └── _Layout.cshtml          ← Layout tối giản cho login page
│   └── Auth/
│       └── Login.cshtml            ← Hoặc dùng Blazor Login.razor thay thế
│
├── wwwroot/
│   ├── css/
│   │   └── app.css                 ← Custom styles cho Blazor
│   └── js/
│       └── app.js                  ← JS interop helpers (QR, camera)
│
├── GlobalUsings.cs                 ← (✅ đã tạo Sprint 1)
├── Program.cs                      ← DI + Blazor Server middleware
├── appsettings.json
└── appsettings.Development.json
```

### 4.1 Cơ chế Real-time với Blazor Server

```
Sinh viên check-in (mobile)
        ↓
POST /api/attendance/check-in
        ↓
AttendanceService.CheckIn()
        ↓
AttendanceStateService.AddCheckedInStudent(studentData)
  → NotifyStateChanged() ← event
        ↓                      ↓
   DB.SaveChanges()    LiveSession.razor
                       (đang chạy trên server)
                       OnStateChanged() được gọi
                              ↓
                       StateHasChanged()  ← Blazor re-render
                              ↓
                    SignalR (tự động của Blazor Server)
                              ↓
                    Trình duyệt GV cập nhật NGAY
```

> [!NOTE]
> `AttendanceStateService` được đăng ký là **Scoped** (per SignalR circuit).  
> Mỗi tab trình duyệt GV = 1 circuit riêng. Service sẽ giữ state của phiên điểm danh đang mở.

---

## 5. ⚙️ Logic Nghiệp Vụ Chi Tiết Từng Service

### 5.1 `AuthService`

```
Login(username, password):
  1. Tìm User theo username
  2. BCrypt.Verify(password, user.PasswordHash)
  3. Nếu Role = Lecturer → set Cookie (web)
  4. Nếu Role = Student → trả JWT token (mobile)
  5. Nếu sai → throw UnauthorizedException

GenerateJwtToken(user):
  - Claims: userId, username, role, lecturerId/studentId
  - Expiry: 7 ngày
  - Signed bằng SecretKey trong appsettings
```

### 5.2 `QrCodeService`

```
GenerateToken():
  → Guid.NewGuid().ToString("N")  // 32 ký tự hex, không dấu gạch

RefreshQrForSession(sessionId):
  1. Tìm AttendanceSession
  2. session.QrToken = GenerateToken()
  3. session.QrTokenExpiry = DateTime.UtcNow.AddSeconds(20)
  4. SaveChanges()
  5. Broadcast token mới qua SignalR

ValidateToken(inputToken, sessionId):
  1. Tìm AttendanceSession
  2. session.IsActive == true?
  3. session.QrToken == inputToken?
  4. DateTime.UtcNow < session.QrTokenExpiry?
  → return bool

GenerateQrImage(token):
  → Dùng thư viện QRCoder → trả base64 PNG
```

### 5.3 `AttendanceService`

```
StartSession(scheduleId, lecturerId):
  1. Verify giảng viên có quyền với schedule này
  2. Kiểm tra session chưa được tạo cho buổi này
  3. Tạo AttendanceSession { ScheduleId, QrToken, QrTokenExpiry, StartTime, IsActive=true }
  4. Lấy danh sách Enrollment → tạo Attendance records với Status=Absent cho mọi SV
  5. Return sessionId

CheckIn(CheckInRequestDto dto):
  1. ValidateToken(dto.QrToken, sessionId)
  2. Tìm Attendance record của studentId trong session
  3. Nếu đã check-in → return "Already checked in"
  4. FaceService.Verify(dto.FaceImageBase64, student.FaceEmbedding)
  5. GpsService.IsWithinRange(dto.Lat, dto.Lng, room.Lat, room.Lng, maxMeters=100)
  6. Cập nhật Attendance:
     - CheckInTime = UtcNow
     - FaceVerified = faceResult.IsMatch
     - LocationVerified = gpsResult.IsValid
     - FaceConfidence = faceResult.Score
     - Status = DetermineStatus(session.Schedule.StartTime, UtcNow)
  7. SignalR broadcast StudentCheckedIn
  8. Return CheckInResponseDto

DetermineStatus(scheduleStart, checkInTime):
  - Nếu checkInTime ≤ scheduleStart + 15 phút → Present
  - Nếu checkInTime ≤ scheduleStart + 30 phút → Late  
  - Nếu > 30 phút → Absent (không cho check-in)

EndSession(sessionId):
  1. session.IsActive = false
  2. session.EndTime = UtcNow
  3. Tất cả Attendance có Status=Absent → giữ nguyên (đã được tạo từ đầu)
  4. LeaveRequest đã Approved → cập nhật Status = Excused
  5. SaveChanges()
```

### 5.4 `FaceService`

```
VerifyFace(imageBase64, storedEmbeddingJson):
  1. Parse storedEmbeddingJson → float[]
  2. Gửi imageBase64 lên Python AI service (HTTP call)
     hoặc dùng ML.NET để extract embedding tại chỗ
  3. AI service trả về embedding mới
  4. CosineSimilarity(stored, new) → score ∈ [0, 1]
  5. return { IsMatch = score >= 0.8, Score = score }

ExtractFaceEmbedding(imageBase64):
  1. Gửi imageBase64 lên Python AI service (HTTP call - Endpoint /extract)
  2. Nhận kết quả là array float[] 128 chiều
  3. Format json string array để chuẩn bị lưu vào CSDL

CosineSimilarity(float[] a, float[] b):
  = dot(a,b) / (|a| * |b|)
```

> **Quyết định kiến trúc:** Python AI service chạy riêng (FastAPI) tại port 8000.  
> `FaceService` gọi qua `HttpClient` → `POST http://localhost:8000/verify` với JSON `{ image_base64, embedding }`.

> **Lưu ý Database (Tính năng đăng ký):** Trường `FaceEmbedding` trong Entity `Student` đã được đánh dấu là `string?` (nullable) từ đầu, nên việc thêm tính năng này **không yêu cầu sửa Migration**, Database sẽ không bị xung đột. Logic ở endpoint `register-face` chỉ cần check `If (student.FaceEmbedding != null) { return UnprocessableEntity("Mặt đã đăng ký, không thể đăng ký lại"); }` để đảm bảo hệ thống chỉ lấy data 1 lần duy nhất trong suốt vòng đời tài khoản.

### 5.5 `GpsService`

```
IsWithinRange(double lat1, lon1, lat2, lon2, double maxMeters):
  distance = Haversine(lat1, lon1, lat2, lon2)
  return distance <= maxMeters

Haversine(lat1, lon1, lat2, lon2):
  R = 6371000 // bán kính Trái Đất (mét)
  φ1 = lat1 * π/180
  φ2 = lat2 * π/180
  Δφ = (lat2-lat1) * π/180
  Δλ = (lon2-lon1) * π/180
  a = sin²(Δφ/2) + cos(φ1)·cos(φ2)·sin²(Δλ/2)
  c = 2·atan2(√a, √(1−a))
  return R·c
```

### 5.6 `AttendanceStateService` (Blazor Real-time State)

```csharp
// Singleton scoped theo Blazor circuit (tab trình duyệt)
public class AttendanceStateService
{
    // Danh sách SV đã check-in, Blazor component subscribe vào event này
    public List<CheckedInStudentDto> CheckedInStudents { get; } = new();
    public int TotalStudents { get; set; }
    public int SessionId { get; set; }
    
    // Event: khi có SV check-in mới → Blazor component tự re-render
    public event Action? OnChange;
    
    public void AddCheckedInStudent(CheckedInStudentDto student)
    {
        CheckedInStudents.Add(student);
        NotifyStateChanged();
    }
    
    private void NotifyStateChanged() => OnChange?.Invoke();
}

// Trong LiveSession.razor:
protected override void OnInitialized()
{
    AttendanceState.OnChange += StateHasChanged; // tự re-render khi có SV mới
}

public void Dispose()
{
    AttendanceState.OnChange -= StateHasChanged; // unsubscribe khi rời trang
}
```

---

## 6. 📡 API Endpoints Đầy Đủ

### Auth
| Method | URL | Auth | Mô tả |
|--------|-----|------|--------|
| POST | `/api/auth/login` | Public | Mobile login → trả JWT |
| POST | `/Auth/Login` | Public | Web login → set Cookie |
| POST | `/Auth/Logout` | Cookie | Web logout |

### Schedule (GV)
| Method | URL | Auth | Mô tả |
|--------|-----|------|--------|
| GET | `/api/schedules/today` | JWT | Mobile: lịch hôm nay của SV |
| GET | `/Dashboard` | Cookie | Web: dashboard GV |

### Attendance Session (GV)
| Method | URL | Auth | Mô tả |
|--------|-----|------|--------|
| POST | `/api/sessions/start` | Cookie | Bắt đầu phiên |
| POST | `/api/sessions/{id}/end` | Cookie | Kết thúc phiên |
| GET | `/api/sessions/{id}/qr` | Cookie | Lấy QR hiện tại |
| GET | `/Session/Live/{scheduleId}` | Cookie | Màn hình live |
| GET | `/Session/Detail/{sessionId}` | Cookie | Chi tiết sau phiên |

### Học Sinh/Sinh Viên (Mobile)
| Method | URL | Auth | Mô tả |
|--------|-----|------|--------|
| GET | `/api/students/me` | JWT | Trả về info user hiện tại (Profile, Class, FaceRegistered cờ) |
| POST | `/api/students/register-face` | JWT | Đăng ký khuôn mặt lần đầu (chỉ chụp 1 lần duy nhất) |
| POST | `/api/attendance/check-in` | JWT | SV check-in với tọa độ GPS và ảnh khuôn mặt thực |
| GET | `/api/schedules` | JWT | Trả về lịch học (Hôm nay / Tuần này / Học kỳ) |
| GET | `/api/attendances/history` | JWT | Lịch sử điểm danh chi tiết từng môn (Có mặt/Vắng/Trễ) |
| GET | `/api/attendances/statistics` | JWT | Thống kê số buổi chuyên cần tổng quát cả học kỳ |
| GET | `/api/leave-requests` | JWT | Lấy danh sách + xem trạng thái các đơn xin nghỉ đã nộp |
| POST | `/api/leave-requests` | JWT | Nộp đơn xin vắng có phép kèm minh chứng |

### Điểm danh (Giảng Viên - Web/API)
| Method | URL | Auth | Mô tả |
|--------|-----|------|--------|
| PUT | `/api/attendance/{id}/status` | Cookie | GV sửa thủ công trạng thái vắng/trễ |
| GET | `/api/attendance/export/{sessionId}` | Cookie | Xuất Excel danh sách đã đến lớp |

### Leave Requests (Giảng Viên - Web/API)
| Method | URL | Auth | Mô tả |
|--------|-----|------|--------|
| GET | `/api/instructor/leave-requests` | Cookie | GV xem danh sách (Dashboard/Leave) |
| PUT | `/api/instructor/leave-requests/{id}/approve` | Cookie | GV duyệt |
| PUT | `/api/instructor/leave-requests/{id}/reject` | Cookie | GV từ chối |

---

## 7. 🔧 `AppDbContext.OnModelCreating` — Cấu hình quan hệ

```csharp
protected override void OnModelCreating(ModelBuilder mb)
{
    // User - Student (1-1)
    mb.Entity<Student>()
      .HasOne(s => s.User).WithOne(u => u.Student)
      .HasForeignKey<Student>(s => s.UserId);

    // User - Lecturer (1-1)
    mb.Entity<Lecturer>()
      .HasOne(l => l.User).WithOne(u => u.Lecturer)
      .HasForeignKey<Lecturer>(l => l.UserId);

    // CourseClass - Course (N-1)
    mb.Entity<CourseClass>()
      .HasOne(cc => cc.Course).WithMany(c => c.CourseClasses)
      .HasForeignKey(cc => cc.CourseId);

    // CourseClass - Lecturer (N-1)
    mb.Entity<CourseClass>()
      .HasOne(cc => cc.Lecturer).WithMany()
      .HasForeignKey(cc => cc.LecturerId);

    // Enrollment - CourseClass & Student
    mb.Entity<Enrollment>()
      .HasOne(e => e.Student).WithMany(s => s.Enrollments)
      .HasForeignKey(e => e.StudentId);
    mb.Entity<Enrollment>()
      .HasOne<CourseClass>().WithMany(cc => cc.Enrollments)
      .HasForeignKey(e => e.ClassId);
    // UNIQUE constraint
    mb.Entity<Enrollment>()
      .HasIndex(e => new { e.ClassId, e.StudentId }).IsUnique();

    // Schedule - CourseClass & Room
    mb.Entity<Schedule>()
      .HasOne<CourseClass>().WithMany(cc => cc.Schedules)
      .HasForeignKey(s => s.ClassId);
    mb.Entity<Schedule>()
      .HasOne(s => s.Room).WithMany()
      .HasForeignKey(s => s.RoomId);

    // AttendanceSession - Schedule
    mb.Entity<AttendanceSession>()
      .HasOne(a => a.Schedule).WithMany(s => s.AttendanceSessions)
      .HasForeignKey(a => a.ScheduleId);

    // Attendance - Session & Student
    mb.Entity<Attendance>()
      .HasOne(a => a.Session).WithMany(s => s.Attendances)
      .HasForeignKey(a => a.SessionId);
    mb.Entity<Attendance>()
      .HasOne(a => a.Student).WithMany(s => s.Attendances)
      .HasForeignKey(a => a.StudentId);
    // UNIQUE: 1 SV chỉ có 1 record trong 1 session
    mb.Entity<Attendance>()
      .HasIndex(a => new { a.SessionId, a.StudentId }).IsUnique();

    // LeaveRequest - Student & Schedule
    mb.Entity<LeaveRequest>()
      .HasOne(r => r.Student).WithMany(s => s.LeaveRequests)
      .HasForeignKey(r => r.StudentId);
    mb.Entity<LeaveRequest>()
      .HasOne(r => r.Schedule).WithMany()
      .HasForeignKey(r => r.ScheduleId);
}
```

---

## 8. 🏁 Thứ Tự Triển Khai (Dev Roadmap)

### ✅ Sprint 1 — Fix nền móng — HOÀN TẤT
- [x] Fix namespace toàn bộ Entities/Enums (file-scoped `*.Entities;` / `*.Enums;`)
- [x] Xóa self-referencing property sai trong `CourseClass.cs`
- [x] Thêm `QrTokenExpiry`, `FaceConfidence`, `DistanceMeter`, `ReviewedBy/At/Note`, `Capacity`
- [x] Hoàn thiện `AppDbContext.OnModelCreating` (FKs, unique constraints, indexes)
- [x] Tạo `GpsService.cs` (Haversine đầy đủ)
- [x] Tạo `GlobalUsings.cs`
- [x] `dotnet build` → 0 errors · `dotnet ef database update` → thành công
- [x] Seed data: 1 Lecturer, 5 Students, 3 Courses, 3 CourseClasses, 3 Rooms, 3 Schedules (`Data/DbSeeder.cs` — idempotent, gọi khi app khởi động dành cho Development)

---

### ✅ Sprint 2 — Blazor Server UI (Giảng viên Web) — HOÀN TẤT

**9/9 màn hình** hoàn thiện với mock data. Build thành công **0 Error(s)**.

**Layout & Shell:**
- [x] `MainLayout.razor`, `Sidebar.razor` (nav groups + pending badge), `Header.razor` (notification bell + user dropdown)
- [x] `App.razor`, `Routes.razor` (404 redirect), `_Imports.razor`
- [x] `wwwroot/css/app.css` — dark navy + emerald green theme (~750 dòng)
- [x] `wwwroot/js/app.js` — sidebar toggle, user dropdown, QR placeholder, toast

**Authentication:**
- [x] `Pages/Auth/Login.razor` — form đăng nhập
- [x] `AuthController.cs` — Cookie auth handler

**Màn hình chính (mock data):**
| URL | File | Chức năng |
|---|---|---|
| `/dashboard` | `Dashboard.razor` | Lịch dạy hôm nay + stat cards |
| `/schedule` | `ScheduleView.razor` | Calendar tuần, chuyển tuần prev/next |
| `/classes` | `MyClasses.razor` | Grid danh sách lớp học phần |
| `/classes/{id}` | `ClassHistory.razor` | Bảng pivot SV × Buổi học |
| `/session/{id}` | `LiveSession.razor` | QR countdown + student feed real-time |
| `/session/{id}/detail` | `ClassDetail.razor` | Bảng điểm danh + edit inline |
| `/reports` | `AttendanceReport.razor` | Tab lớp + dist bars + bảng SV cảnh báo đỏ |
| `/leaves` | `LeaveRequests.razor` | Duyệt/từ chối đơn xin nghỉ |
| `/profile` | `Profile.razor` | Hồ sơ GV + đổi mật khẩu |
| `/...` (404) | `NotFound.razor` | Trang 404 custom |

**Shared Components:** `StatCard`, `StudentCard`, `QrDisplay`, `AttendanceTable`
**State Management:** `AttendanceStateService` — real-time event-driven

---


### ✅ Sprint 3 — Auth & Core User Data (Cơ sở) — HOÀN TẤT
- [x] `AuthService`: Login, BCrypt.Verify, GenerateJwt, GetClaimsPrincipalForCookie
- [x] `AuthApiController`: POST `/api/auth/login` → Trả JWT cho mobile
- [x] `StudentApiController`: GET `/api/students/me` (Profile của SV + cờ `isFaceRegistered`)
- [x] Kết nối `Login.razor` (Web GV) → `AuthController` Cookie real (chỉ cho Lecturer)

### ✅ Sprint 4 — Mobile App Data APIs (Non-Attendance Module) — HOÀN TẤT
- [x] `ScheduleApiController`: GET `/api/schedules` (TKB sinh viên)
- [x] `AttendanceApiController`: GET `/api/attendances/history` & `/statistics` (Lịch sử & thống kê chuyên cần cho SV)
- [x] `LeaveApiController`: POST `/api/leave-requests`, GET `/api/leave-requests` (Nộp & xem kết quả đơn)
- [x] Test Mobile API qua Postman / Swagger (Luồng thông tin SV).

### ✅ Sprint 5 — Dashboard & Lớp Học Phần Backend (Giảng Viên Web) — HOÀN TẤT
- [x] `ScheduleService.GetByLecturerId()`
- [x] Cung cấp API nội bộ / Data Loaders cho Dashboard, ScheduleView, MyClasses
- [x] View logic cho `ClassHistory.razor` và `AttendanceReport.razor`
- [x] Thay Mock Data trong Web UI bằng dữ liệu DB thật.

### ✅ Sprint 6 — Attendance Core (Live Session & QR Code) — HOÀN TẤT
- [x] `QrCodeService`: GenerateToken, ValidateToken, GenerateQrImage (QRCoder → base64 PNG), RefreshQrForSession
- [x] `AttendanceService`: StartSession (tạo session + Attendance ABSENT cho tất cả SV enrolled), EndSession (đóng session + cập nhật EXCUSED)
- [x] Kết nối `AttendanceStateService` → Blazor `LiveSession.razor` (Real-time Broadcast) — inject services, auth claims, QR auto-refresh 20s
- [x] `QrDisplay.razor` → Hiện QR thật (server-rendered `<img>` base64), tự động refresh token mỗi 20s.

### ✅ Sprint 7 — AI, GPS Integration & Check-In (Luồng Chính Mobile) — HOÀN TẤT
- [x] `FaceService`: Connect HTTP sang Python FastAPI (`/register-face` & `/verify-face`) với multipart/form-data
- [x] `GpsService`: Haversine <= RadiusMeter (từ Room entity, default 100m)
- [x] `StudentApiController`: POST `/api/students/register-face` (Đăng ký mặt 1 lần → lưu embedding JSON vào DB)
- [x] `AttendanceApiController`: POST `/api/attendance/check-in` (QR + GPS + Face → Điểm danh vào Live Session)
- [x] `AttendanceNotifier` (Singleton): Event broker API → Blazor circuit realtime push
- [x] `LiveSession.razor`: Subscribe `AttendanceNotifier` → auto update check-in list

### ✅ Sprint 8 — Duyệt đơn, Quản lý Lớp & Export Data — HOÀN TẤT
- [x] `LeaveService`: Implement `GetByLecturerAsync`, `ApproveAsync`, `RejectAsync` — có kiểm tra quyền lecturerId
- [x] `LeaveRequests.razor`: Kết nối DB thực, nút Duyệt/Từ chối gọi `LeaveService`, loading state & error handling
- [x] `AttendanceService` (partial): `UpdateStatus` (GV sửa tay) + `GetSessionDetail` (load danh sách điểm danh từ DB)
- [x] `ClassDetail.razor`: Load data thực từ `AttendanceService.GetSessionDetail`, inline status edit lưu DB
- [x] `ExportService`: Xuất Excel 2 loại — `ExportSessionAsync` (1 buổi, màu status) + `ExportClassAsync` (ma trận toàn học kỳ)
- [x] `downloadFromBase64` JS: Trigger browser download file .xlsx từ Blazor
- [x] ClosedXML 0.105.0: Đã cài đặt thành công

---

## 9. ⚡ Quyết Định Kỹ Thuật Quan Trọng

| Vấn đề | Quyết định | Lý do |
|--------|-----------|-------|
| **GV Web UI** | **Blazor Server** | Real-time reactive UI không cần viết JS SignalR client thủ công |
| **Real-time update** | `AttendanceStateService` + Blazor event | C# thuần, không cần `Hub.cs` phức tạp cho phía web |
| Auth Web vs Mobile | Cookie cho Web (Blazor), JWT cho Mobile | Phân biệt rõ 2 client |
| Face AI | Python FastAPI (port 8000) | .NET không có thư viện face recognition tốt |
| QR Token storage | DB field `QrToken` + `QrTokenExpiry` | Đơn giản, không cần Redis |
| Face Embedding | JSON string trong DB | Tránh schema phức tạp, dễ debug |
| GPS validation | `GpsService` Haversine thuần C# | Không cần external API |
| Export Excel | ClosedXML | Miễn phí, dễ dùng |
| DI Pattern | Concrete classes (không Repository Pattern) | Đơn giản hơn cho KLTN |
| State Management | `AttendanceStateService` Scoped | Mỗi browser tab = 1 Blazor circuit = 1 state riêng |

---

*Tài liệu này cần được cập nhật sau mỗi sprint.*
