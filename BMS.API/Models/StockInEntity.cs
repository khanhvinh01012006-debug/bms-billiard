namespace BMS.API.Models;

// Việc 1: Phiếu nhập hàng - mỗi phiếu ghi nhận một lần nhập vật tư từ nhà cung cấp,
// cộng số lượng vào Kho theo đơn vị tồn kho cơ bản (quy đổi từ đơn vị nhập, ví dụ 1
// thùng = 24 chai).
public class StockInEntity
{
    public int Id { get; set; }
    public DateTime CreatedAtUtc { get; set; }
    public int StaffAccountId { get; set; }
    public string StaffName { get; set; } = string.Empty;
    public string? SupplierName { get; set; }
    public string? Note { get; set; }

    public List<StockInLineEntity> Lines { get; set; } = new();
}
