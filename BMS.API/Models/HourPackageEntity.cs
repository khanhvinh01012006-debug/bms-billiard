namespace BMS.API.Models;

// Đợt 9: Gói giờ bàn trả trước - hội viên mua trước một số giờ chơi với giá ưu đãi, trừ
// dần RemainingHours mỗi khi nhân viên ghi nhận sử dụng (thao tác thủ công tại quầy, tách
// biệt với luồng thanh toán complete-payment để không làm phức tạp thêm luồng đó).
public class HourPackageEntity
{
    public int Id { get; set; }
    public int AccountId { get; set; }
    public string PackageName { get; set; } = string.Empty;
    public decimal TotalHours { get; set; }
    public decimal RemainingHours { get; set; }
    public decimal PricePaid { get; set; }
    public DateTime PurchasedAtUtc { get; set; }
    public DateTime? ExpiresAtUtc { get; set; }
    // "Active" | "UsedUp" | "Expired"
    public string Status { get; set; } = "Active";
}
