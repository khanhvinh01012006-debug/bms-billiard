namespace BMS.API.Models;

// Một dòng vật tư trong Phiếu kiểm kho.
public class StockAuditLineEntity
{
    public int Id { get; set; }
    public int StockAuditId { get; set; }
    public StockAuditEntity? StockAudit { get; set; }

    public int InventoryItemId { get; set; }
    public InventoryItemEntity? InventoryItem { get; set; }

    // Chụp lại Quantity của vật tư tại thời điểm tạo phiếu.
    public int SystemQuantity { get; set; }

    // Null cho tới khi nhân viên nhập số lượng thực tế đếm được.
    public int? ActualQuantity { get; set; }
}
