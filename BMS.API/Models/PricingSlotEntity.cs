namespace BMS.API.Models;

// Đợt 5: Một khung giờ tính giá của một Loại bàn, ví dụ 08:00-17:00 = 60.000đ/giờ.
// EndTime có thể nhỏ hơn StartTime để biểu diễn khung giờ vắt qua nửa đêm
// (ví dụ 17:00-02:00) - xử lý "wrap" này ở logic tính tiền lúc trả bàn.
public class PricingSlotEntity
{
    public int Id { get; set; }
    public int TableTypeId { get; set; }
    public TableTypeEntity? TableType { get; set; }
    public TimeSpan StartTime { get; set; }
    public TimeSpan EndTime { get; set; }
    public decimal PricePerHour { get; set; }
}
