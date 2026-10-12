namespace BMS.API.Models;

// Đợt 2: Quản lý Kho (vật tư, đồ uống, phụ kiện...) - Admin only.
public class InventoryItemEntity
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Category { get; set; } = string.Empty;
    public string Unit { get; set; } = string.Empty;
    public int Quantity { get; set; }
    public int LowStockThreshold { get; set; } = 5;
    public decimal? UnitPrice { get; set; }
    public string? ImageUrl { get; set; }
    public string? Note { get; set; }
    public DateTime UpdatedAtUtc { get; set; }

    // Đợt 5: mặc định true cho hầu hết vật tư. Vài món (phấn cơ, đầu cơ...) được câu lạc
    // bộ tính là miễn phí kèm bàn nên KHÔNG hiển thị trong danh sách "Gọi món & dịch vụ"
    // nhanh của nhân viên (vẫn còn trong Kho để theo dõi tồn kho như bình thường).
    public bool IsQuickAddOrderable { get; set; } = true;
}
