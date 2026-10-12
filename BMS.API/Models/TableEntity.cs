using BMS.Shared.Dtos;

namespace BMS.API.Models;

public class TableEntity
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public TableStatus Status { get; set; } = TableStatus.Available;
    public int Capacity { get; set; }
    public decimal PricePerHour { get; set; }
    public string? ImageUrl { get; set; }

    // Đợt 5: Loại bàn (Bàn Lỗ/Bàn Phăng/Bàn VIP...), nullable để các bàn cũ/chưa gán
    // loại vẫn hoạt động bình thường (dùng PricePerHour cố định phía trên làm fallback).
    public int? TableTypeId { get; set; }
    public TableTypeEntity? TableType { get; set; }

    public List<BookingEntity> Bookings { get; set; } = new();
    public List<PlayingSessionEntity> Sessions { get; set; } = new();
}
