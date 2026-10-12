namespace BMS.API.Models;

public class CueEntity
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public decimal PricePerHour { get; set; }
    public int AvailableCount { get; set; }
    public string? ImageUrl { get; set; }

    // Đợt 2: Mua cơ (không thanh toán thật - chỉ ghi nhận yêu cầu).
    public bool IsForSale { get; set; }
    public decimal? SalePrice { get; set; }
    public string? Description { get; set; }

    // Đợt 8: Cửa hàng cơ/phụ kiện (theo mẫu billiardsomega.com) - hãng + phân loại để
    // khách lọc/duyệt trong tab "Mua".
    public string? Brand { get; set; }
    // "Cơ" | "Bàn" | "Phụ kiện" (bao case, tips, găng tay, giá đỡ, v.v.)
    public string? Category { get; set; }

    public List<CueRentalEntity> Rentals { get; set; } = new();
}
