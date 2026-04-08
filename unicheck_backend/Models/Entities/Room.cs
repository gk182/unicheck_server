using System.ComponentModel.DataAnnotations;

namespace unicheck_backend.Models.Entities;

public class Room
{
    [Key]
    public int RoomId { get; set; }

    [Required, MaxLength(50)]
    public string RoomName { get; set; } = null!; // VD: "B1-201"

    public int Capacity { get; set; }

    // Tọa độ GPS phòng học — dùng để verify khoảng cách check-in
    public double Latitude { get; set; }
    public double Longitude { get; set; }

    // Bán kính hợp lệ tính bằng mét (mặc định 100m)
    public double RadiusMeter { get; set; } = 100;

    // Navigation
    public ICollection<Schedule> Schedules { get; set; } = new List<Schedule>();
}
