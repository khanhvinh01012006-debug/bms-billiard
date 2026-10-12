namespace BMS.API.Models;

// Đợt 5: Loại bàn (Bàn Lỗ / Bàn Phăng / Bàn VIP...) - mỗi loại có thể có một danh sách
// khung giờ tính giá riêng (PricingSlots). Nếu một loại bàn không có khung giờ nào, các
// bàn thuộc loại đó vẫn dùng PricePerHour cố định của chính TableEntity (fallback, để
// tương thích ngược với dữ liệu cũ trước khi có tính năng này).
public class TableTypeEntity
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }

    public List<TableEntity> Tables { get; set; } = new();
    public List<PricingSlotEntity> PricingSlots { get; set; } = new();
}
