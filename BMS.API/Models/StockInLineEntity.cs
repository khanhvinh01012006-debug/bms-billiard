namespace BMS.API.Models;

// Một dòng vật tư trong Phiếu nhập hàng.
public class StockInLineEntity
{
    public int Id { get; set; }
    public int StockInId { get; set; }
    public StockInEntity? StockIn { get; set; }

    public int InventoryItemId { get; set; }
    public InventoryItemEntity? InventoryItem { get; set; }

    // Đơn vị nhập (vd "thùng") có thể khác đơn vị tồn kho cơ bản (vd "chai").
    public string PurchaseUnit { get; set; } = string.Empty;
    public int PurchaseQuantity { get; set; }

    // 1 đơn vị nhập = bao nhiêu đơn vị tồn kho cơ bản (vd 1 thùng = 24 chai).
    public int ConversionFactor { get; set; } = 1;
    public decimal? UnitCost { get; set; }
}
