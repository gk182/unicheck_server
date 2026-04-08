namespace unicheck_backend.Services;

/// <summary>
/// Tính khoảng cách GPS bằng công thức Haversine.
/// Dùng để xác thực sinh viên điểm danh có đứng trong phòng học không.
/// </summary>
public class GpsService
{
    private const double EarthRadiusMeters = 6_371_000;

    /// <summary>
    /// Kiểm tra xem tọa độ (lat1, lon1) có nằm trong bán kính <paramref name="maxMeters"/> 
    /// tính từ (lat2, lon2) không.
    /// </summary>
    public bool IsWithinRange(double lat1, double lon1, double lat2, double lon2, double maxMeters)
        => HaversineDistance(lat1, lon1, lat2, lon2) <= maxMeters;

    /// <summary>
    /// Tính khoảng cách (mét) giữa 2 điểm GPS theo công thức Haversine.
    /// </summary>
    public double HaversineDistance(double lat1, double lon1, double lat2, double lon2)
    {
        var dLat = ToRad(lat2 - lat1);
        var dLon = ToRad(lon2 - lon1);

        var a = Math.Sin(dLat / 2) * Math.Sin(dLat / 2)
              + Math.Cos(ToRad(lat1)) * Math.Cos(ToRad(lat2))
              * Math.Sin(dLon / 2) * Math.Sin(dLon / 2);

        var c = 2 * Math.Atan2(Math.Sqrt(a), Math.Sqrt(1 - a));
        return EarthRadiusMeters * c;
    }

    private static double ToRad(double deg) => deg * Math.PI / 180.0;
}
