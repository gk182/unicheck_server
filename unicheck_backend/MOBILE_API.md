# 📱 UniCheck Mobile API — Tài Liệu Kết Nối

> **Base URL:** `http://<SERVER_IP>:5094` (Development)  
> **Auth:** JWT Bearer Token — gửi trong header `Authorization: Bearer <token>`  
> **Content-Type:** `application/json`

---

## 1. 🔑 Đăng Nhập

### `POST /api/auth/login`

> **Auth:** Không cần (Public)

**Request:**
```json
{
  "username": "20210001",
  "password": "123456"
}
```

**Response 200:**
```json
{
  "token": "eyJhbGciOiJIUzI1NiIs...",
  "userId": "20210001",
  "username": "20210001",
  "role": "STUDENT",
  "fullName": "Nguyễn Văn A",
  "expiresAt": "2026-03-31T04:00:00Z"
}
```

**Response 401:**
```json
{ "message": "Tên đăng nhập hoặc mật khẩu không đúng." }
```

> [!IMPORTANT]
> Sau khi login thành công, lưu `token` vào `flutter_secure_storage` hoặc `SharedPreferences`.  
> Mọi API bên dưới đều cần gửi header: `Authorization: Bearer <token>`

---

## 2. 👤 Thông Tin Sinh Viên

### `GET /api/students/me`

> **Auth:** JWT Bearer (Role: STUDENT)

**Response 200:**
```json
{
  "studentId": "20210001",
  "fullName": "Nguyễn Văn A",
  "classCode": "CNTT2021A",
  "faculty": "Công nghệ Thông tin",
  "major": "Khoa học Máy tính",
  "email": "20210001@ued.udn.vn",
  "dateOfBirth": "2003-05-15T00:00:00",
  "isFaceRegistered": false,
  "username": "20210001"
}
```

> [!TIP]
> Kiểm tra `isFaceRegistered`:
> - `false` → Chuyển bắt buộc tới màn hình **Đăng ký khuôn mặt** trước
> - `true` → Vào trang chủ bình thường

---

## 3. 📸 Đăng Ký Khuôn Mặt (One-Time)

### `POST /api/students/register-face`

> **Auth:** JWT Bearer (Role: STUDENT)  
> Chỉ được gọi 1 lần. Nếu đã đăng ký rồi sẽ nhận lỗi 400.

**Request:**
```json
{
  "faceImageBase64": "/9j/4AAQSkZJRg..."
}
```

**Response 200:**
```json
{ "message": "Đăng ký khuôn mặt thành công!" }
```

**Response 400:**
```json
{ "message": "Bạn đã đăng ký khuôn mặt rồi. Không thể đăng ký lại." }
```

> [!IMPORTANT]
> **Yêu cầu ảnh:**
> - Encode sang **base64** trước khi gửi (`base64Encode(imageBytes)`)
> - Ảnh chụp thẳng mặt, đủ sáng, không đeo kính/khẩu trang
> - Định dạng JPEG, không cần xóa prefix `data:image/jpeg;base64,` (gửi raw base64)
> - Python AI service phải đang chạy tại `http://localhost:8000`

---

## 4. 📅 Lịch Học

### `GET /api/schedules`

> **Auth:** JWT Bearer (Role: STUDENT)

Trả về toàn bộ lịch học của SV (sắp xếp theo ngày + giờ).

**Response 200:**
```json
[
  {
    "scheduleId": 1,
    "courseName": "Phát triển ứng dụng Web",
    "roomName": "B1-201",
    "date": "2026-03-24T00:00:00",
    "startTime": "07:30:00",
    "endTime": "09:30:00"
  }
]
```

> [!TIP]
> `startTime` / `endTime` là `TimeSpan` dạng `"HH:mm:ss"`.  
> Parse Dart: `TimeOfDay(hour: int.parse(s.split(':')[0]), minute: int.parse(s.split(':')[1]))`

---

## 5. 🔲 Check-In Điểm Danh

### `POST /api/attendance/check-in`

> **Auth:** JWT Bearer (Role: STUDENT)

**Request:**
```json
{
  "qrToken": "a1b2c3d4-e5f6-...",
  "faceImageBase64": "/9j/4AAQSkZJRg...",
  "latitude": 16.054407,
  "longitude": 108.202164
}
```

**Response 200:**
```json
{
  "success": true,
  "status": "PRESENT",
  "message": "Điểm danh thành công!",
  "faceVerified": true,
  "faceConfidence": 0.94,
  "locationVerified": true,
  "distanceMeter": 23.5
}
```

**Response 400 (các trường hợp lỗi):**
```json
{ "message": "QR code không hợp lệ hoặc đã hết hạn." }
{ "message": "QR code đã hết hạn. Vui lòng quét lại." }
{ "message": "Bạn không có trong danh sách lớp học này." }
{ "message": "Bạn đã điểm danh rồi." }
{ "message": "Đã quá 30 phút kể từ đầu giờ học. Không thể điểm danh." }
{ "message": "Sinh viên chưa đăng ký khuôn mặt." }
```

> [!IMPORTANT]
> **Luồng check-in mobile (thứ tự bắt buộc):**
> 1. Mở camera → quét QR code → lấy `qrToken` từ QR
> 2. Chụp ảnh mặt sinh viên → encode base64
> 3. Lấy GPS hiện tại (`Geolocator.getCurrentPosition`)
> 4. Gọi API `POST /api/attendance/check-in` với 4 field trên
> 5. Hiển thị kết quả: `status`, `faceConfidence`, `distanceMeter`

> [!WARNING]
> **QR token chỉ sống 20 giây** — phải gọi API ngay sau khi quét, không delay.
> Nếu nhận `"QR code đã hết hạn"` → thông báo SV quét lại mã QR mới trên màn chiếu.

---

## 6. 📊 Lịch Sử Điểm Danh

### `GET /api/attendances/history`

> **Auth:** JWT Bearer (Role: STUDENT)

**Response 200:**
```json
[
  {
    "attendanceId": 1,
    "courseName": "Phát triển ứng dụng Web",
    "roomName": "B1-201",
    "date": "2026-03-20T00:00:00",
    "checkInTime": "2026-03-20T07:35:00Z",
    "status": "PRESENT",
    "note": null
  },
  {
    "attendanceId": 2,
    "courseName": "Cơ sở dữ liệu",
    "roomName": "A2-305",
    "date": "2026-03-21T00:00:00",
    "checkInTime": null,
    "status": "ABSENT",
    "note": null
  }
]
```

**Status values:** `PRESENT` | `LATE` | `ABSENT` | `EXCUSED`

> [!NOTE]
> `checkInTime` là UTC — cộng 7 tiếng để hiển thị giờ Việt Nam:
> ```dart
> final localTime = DateTime.parse(checkInTime).toLocal();
> ```

---

## 7. 📈 Thống Kê Chuyên Cần

### `GET /api/attendances/statistics`

> **Auth:** JWT Bearer (Role: STUDENT)

**Response 200:**
```json
{
  "Present": 12,
  "Absent": 2,
  "Late": 1,
  "Excused": 1
}
```

---

## 8. 📝 Đơn Xin Nghỉ

### `GET /api/leave-requests`

> **Auth:** JWT Bearer (Role: STUDENT)

**Response 200:**
```json
[
  {
    "requestId": 1,
    "scheduleId": 5,
    "courseName": "Phát triển ứng dụng Web",
    "date": "2026-03-28T00:00:00",
    "reason": "Đi khám bệnh",
    "status": "PENDING",
    "attachmentUrl": null,
    "reviewedAt": null
  }
]
```

### `POST /api/leave-requests`

> **Auth:** JWT Bearer (Role: STUDENT)

**Request:**
```json
{
  "scheduleId": 5,
  "reason": "Đi khám bệnh",
  "attachmentUrl": null
}
```

**Response 200:**
```json
{ "message": "Nộp đơn thành công", "requestId": 1 }
```

**Response 400:**
```json
{ "message": "Bạn đã nộp đơn xin nghỉ cho buổi học này rồi." }
```

---

## 🧪 Dart/Flutter — Full ApiService

```dart
// lib/core/api/api_service.dart

import 'dart:convert';
import 'dart:io';
import 'package:dio/dio.dart';
import 'package:geolocator/geolocator.dart';

class ApiService {
  // Android Emulator: 10.0.2.2  |  iOS Simulator: localhost  |  Real device: IP máy tính
  static const String baseUrl = 'http://10.0.2.2:5094';

  late final Dio _dio;

  ApiService({String? token}) {
    _dio = Dio(BaseOptions(
      baseUrl: baseUrl,
      connectTimeout: const Duration(seconds: 15),
      receiveTimeout: const Duration(seconds: 30), // check-in cần lâu hơn (AI)
      headers: {
        'Content-Type': 'application/json',
        if (token != null) 'Authorization': 'Bearer $token',
      },
    ));
  }

  // ── Auth ────────────────────────────────────────────────────────────────────
  Future<Map<String, dynamic>> login(String username, String password) async {
    final res = await _dio.post('/api/auth/login', data: {
      'username': username,
      'password': password,
    });
    return res.data;
  }

  // ── Student ─────────────────────────────────────────────────────────────────
  Future<Map<String, dynamic>> getMyProfile() async {
    final res = await _dio.get('/api/students/me');
    return res.data;
  }

  /// Đăng ký khuôn mặt lần đầu (one-time)
  Future<void> registerFace(File imageFile) async {
    final bytes = await imageFile.readAsBytes();
    final base64Image = base64Encode(bytes);
    await _dio.post('/api/students/register-face', data: {
      'faceImageBase64': base64Image,
    });
  }

  // ── Schedule ─────────────────────────────────────────────────────────────────
  Future<List<dynamic>> getSchedules() async {
    final res = await _dio.get('/api/schedules');
    return res.data;
  }

  // ── Check-In ─────────────────────────────────────────────────────────────────
  /// [qrToken]: chuỗi text decode từ QR code
  /// [faceImage]: ảnh chụp từ camera
  /// [position]: GPS từ Geolocator
  Future<Map<String, dynamic>> checkIn({
    required String qrToken,
    required File faceImage,
    required Position position,
  }) async {
    final bytes = await faceImage.readAsBytes();
    final base64Image = base64Encode(bytes);

    final res = await _dio.post('/api/attendance/check-in', data: {
      'qrToken': qrToken,
      'faceImageBase64': base64Image,
      'latitude': position.latitude,
      'longitude': position.longitude,
    });
    return res.data;
  }

  // ── Attendance ───────────────────────────────────────────────────────────────
  Future<List<dynamic>> getHistory() async {
    final res = await _dio.get('/api/attendances/history');
    return res.data;
  }

  Future<Map<String, dynamic>> getStatistics() async {
    final res = await _dio.get('/api/attendances/statistics');
    return res.data;
  }

  // ── Leave Requests ───────────────────────────────────────────────────────────
  Future<List<dynamic>> getLeaveRequests() async {
    final res = await _dio.get('/api/leave-requests');
    return res.data;
  }

  Future<Map<String, dynamic>> submitLeaveRequest({
    required int scheduleId,
    required String reason,
    String? attachmentUrl,
  }) async {
    final res = await _dio.post('/api/leave-requests', data: {
      'scheduleId': scheduleId,
      'reason': reason,
      'attachmentUrl': attachmentUrl,
    });
    return res.data;
  }
}
```

---

## 📋 Các package Flutter cần thiết

```yaml
# pubspec.yaml
dependencies:
  dio: ^5.4.0              # HTTP client
  geolocator: ^12.0.0      # GPS
  camera: ^0.11.0          # Camera chụp ảnh mặt
  mobile_scanner: ^5.0.0   # Quét QR code
  flutter_secure_storage: ^9.0.0  # Lưu JWT token an toàn
  permission_handler: ^11.0.0     # Xin quyền camera/GPS
```

---

## ⚠️ Lưu Ý Quan Trọng Khi Implement

### 1. Base URL
| Môi trường | Base URL |
|------------|----------|
| Android Emulator | `http://10.0.2.2:5094` |
| iOS Simulator | `http://localhost:5094` |
| Real device (cùng WiFi) | `http://<IP_máy_tính>:5094` |
| Production | `https://api.unicheck.edu.vn` |

> [!CAUTION]
> Android không dùng được `localhost` — phải dùng `10.0.2.2`.

### 2. Quyền cần xin (AndroidManifest + Info.plist)
```
- CAMERA (chụp ảnh mặt, quét QR)
- ACCESS_FINE_LOCATION (GPS check-in)
- INTERNET
```

### 3. Luồng Check-In hoàn chỉnh
```
[Màn hình QR Scanner]
    → Quét QR → decode ra qrToken (string)
    → Chuyển sang [Màn hình Chụp Mặt]
    → Camera chụp ảnh → encode base64
    → Geolocator.getCurrentPosition()
    → Gọi POST /api/attendance/check-in
    → Success: hiển thị kết quả (PRESENT/LATE + confidence)
    → Error: hiển thị message lỗi (QR hết hạn, đã điểm danh, v.v.)
```

### 4. Xử lý lỗi Dio
```dart
try {
  final result = await apiService.checkIn(...);
  // xử lý thành công
} on DioException catch (e) {
  if (e.response?.statusCode == 400) {
    final msg = e.response?.data['message'] ?? 'Lỗi không xác định';
    // Hiển thị snackbar lỗi
  } else if (e.type == DioExceptionType.connectionTimeout) {
    // AI service chưa bật hoặc mạng yếu
  }
}
```

### 5. JWT Token hết hạn
- Token mặc định sống **7 ngày** (`ExpiryMinutes: 10080` trong `appsettings.json`)
- Nếu nhận `401 Unauthorized` → tự động logout và chuyển về Login

### 6. Python AI Service (cho check-in)
> [!WARNING]
> Backend gọi Python AI tại `http://localhost:8000`.  
> Khi test, đảm bảo **Python `main.py` đang chạy** trước khi test check-in.  
> Lệnh: `python main.py` (trong thư mục project)

### 7. QR Token timeout (20 giây)
> [!WARNING]
> QR code refresh mỗi **20 giây**. Mobile cần:
> - Quét QR xong → gọi API ngay, không chờ
> - Nếu nhận lỗi `"QR code đã hết hạn"` → thông báo SV quét lại mã mới

---

## 📋 Seed Data (Tài khoản test)

### 👨‍🏫 Giảng Viên (Web)
| Username | Password | Role | Tên |
|----------|----------|------|-----|
| `gv001` | `password123` | LECTURER | TS. Nguyễn Văn Minh |

### 👨‍🎓 Sinh Viên (Mobile) — 50 tài khoản
| Username | MSSV | Password |
|----------|------|----------|
| `3120222001` | `3120222001` | `password123` |
| `3120222002` | `3120222002` | `password123` |
| `3120222003` | `3120222003` | `password123` |
| ... (đến 3120222050) | ... | `password123` |

> [!CAUTION]
> Password là `password123` — **KHÔNG PHẢI** `123456`.  
> Username sinh viên có prefix `sv` trước MSSV: `3120222001`

> [!NOTE]
> Seed data chỉ có ở mode **Development**. Xem file `Data/DbSeeder.cs`.  
> Tên SV được random mỗi lần seed — MSSV và username thì cố định.

