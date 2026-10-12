namespace BMS.API.Models;

// Chợ trao đổi cơ (Trade) giữa người chơi. TODO đợt sau: kiểm duyệt tin đăng trước khi
// hiển thị công khai (hiện tại tin lên thẳng mà không cần Admin duyệt).
public class CueListingEntity
{
    public int Id { get; set; }
    public int SellerAccountId { get; set; }
    public AccountEntity? SellerAccount { get; set; }
    public string Title { get; set; } = string.Empty;
    public string? Description { get; set; }
    public string? ImageUrl { get; set; }
    public string? DesiredTrade { get; set; }
    public decimal? Price { get; set; }
    public DateTime CreatedAtUtc { get; set; }

    // "Open" | "Closed"
    public string Status { get; set; } = "Open";
}
