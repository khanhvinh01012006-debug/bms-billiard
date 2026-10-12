namespace BMS.API.Models;

// Đợt 9: Yêu cầu giảm giá khi thanh toán bàn. Nếu % giảm nằm trong hạn mức
// (AccountEntity.DiscountLimitPercent) của nhân viên thì tự động Approved; nếu vượt hạn
// mức thì PendingApproval, chờ Admin duyệt trước khi áp dụng vào complete-payment.
public class DiscountRequestEntity
{
    public int Id { get; set; }
    public int SessionId { get; set; }
    public string TableName { get; set; } = string.Empty;
    public int StaffAccountId { get; set; }
    public string StaffName { get; set; } = string.Empty;
    public decimal OriginalAmount { get; set; }
    public decimal DiscountPercent { get; set; }
    public decimal DiscountAmount { get; set; }
    public string? Reason { get; set; }
    // "Approved" | "PendingApproval" | "Rejected"
    public string Status { get; set; } = "PendingApproval";
    public bool IsApplied { get; set; }
    public DateTime CreatedAtUtc { get; set; }
    public string? DecidedByName { get; set; }
    public DateTime? DecidedAtUtc { get; set; }
}
