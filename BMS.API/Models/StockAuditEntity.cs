namespace BMS.API.Models;

// Việc 1: Phiếu kiểm kho định kỳ - chụp lại số lượng hệ thống của toàn bộ vật tư tại
// thời điểm tạo phiếu, nhân viên nhập số lượng thực tế đếm được, khi hoàn tất thì Kho
// được cập nhật lại theo số thực tế.
public class StockAuditEntity
{
    public int Id { get; set; }
    public DateTime CreatedAtUtc { get; set; }
    public DateTime? CompletedAtUtc { get; set; }
    public int StaffAccountId { get; set; }
    public string StaffName { get; set; } = string.Empty;
    public string? Note { get; set; }

    // "Draft" | "Completed"
    public string Status { get; set; } = "Draft";

    public List<StockAuditLineEntity> Lines { get; set; } = new();
}
