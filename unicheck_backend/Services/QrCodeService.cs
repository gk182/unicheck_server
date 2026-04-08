using unicheck_backend.Data;
using unicheck_backend.Models.Entities;
using Microsoft.EntityFrameworkCore;
using QRCoder;

namespace unicheck_backend.Services;

/// <summary>
/// Sinh và validate QR token cho phiên điểm danh.
/// Token là GUID hex 32 ký tự, hết hạn sau 20 giây (cấu hình trong appsettings).
/// </summary>
public class QrCodeService
{
    private readonly AppDbContext _db;
    private readonly IConfiguration _config;
    private readonly int _tokenLifetimeSec;
    private readonly int _imageSizePx;

    public QrCodeService(AppDbContext db, IConfiguration config)
    {
        _db     = db;
        _config = config;
        _tokenLifetimeSec = config.GetValue("QrSettings:TokenLifetimeSeconds", 20);
        _imageSizePx      = config.GetValue("QrSettings:ImageSizePx", 300);
    }

    /// <summary>Sinh GUID token mới (32 ký tự hex, không dấu gạch).</summary>
    public string GenerateToken() => Guid.NewGuid().ToString("N");

    /// <summary>
    /// Tạo QR code từ token, trả về base64 PNG data URI sẵn sàng cho img src.
    /// </summary>
    public string GenerateQrImage(string token)
    {
        using var qrGenerator = new QRCodeGenerator();
        using var qrCodeData  = qrGenerator.CreateQrCode(token, QRCodeGenerator.ECCLevel.Q);
        using var qrCode      = new PngByteQRCode(qrCodeData);

        // pixelsPerModule = tổng size / số module (thường ~33 module cho QR)
        int pixelsPerModule = Math.Max(1, _imageSizePx / 33);
        byte[] pngBytes = qrCode.GetGraphic(pixelsPerModule);

        string base64 = Convert.ToBase64String(pngBytes);
        return $"data:image/png;base64,{base64}";
    }

    /// <summary>
    /// Đổi QR token cho phiên điểm danh đang active.
    /// Cập nhật DB và trả về (newToken, newExpiry).
    /// </summary>
    public async Task<(string Token, DateTime Expiry)> RefreshQrForSession(int sessionId)
    {
        var session = await _db.AttendanceSessions
            .FirstOrDefaultAsync(s => s.SessionId == sessionId && s.IsActive);

        if (session is null)
            throw new InvalidOperationException($"Session {sessionId} không tồn tại hoặc đã kết thúc.");

        session.QrToken       = GenerateToken();
        session.QrTokenExpiry = DateTime.UtcNow.AddSeconds(_tokenLifetimeSec);

        await _db.SaveChangesAsync();

        return (session.QrToken, session.QrTokenExpiry);
    }

    /// <summary>
    /// Kiểm tra token mà SV quét có hợp lệ không:
    /// - Session đang active
    /// - Token khớp
    /// - Chưa hết hạn
    /// </summary>
    public async Task<bool> ValidateToken(string inputToken, int sessionId)
    {
        var session = await _db.AttendanceSessions
            .AsNoTracking()
            .FirstOrDefaultAsync(s => s.SessionId == sessionId);

        if (session is null) return false;

        return session.IsActive
            && session.QrToken == inputToken
            && DateTime.UtcNow < session.QrTokenExpiry;
    }
}
