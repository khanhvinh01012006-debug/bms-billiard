namespace BMS.Shared.Dtos;

// DTOs for cue rental (EP-08: US-041, US-042, US-043) + Mua/Trade (đợt 2)

public record CueDto(
    int Id,
    string Name,
    decimal PricePerHour,
    int AvailableCount,
    string? ImageUrl = null,
    bool IsForSale = false,
    decimal? SalePrice = null,
    string? Description = null,
    string? Brand = null,
    string? Category = null);

public record CreateCueRentalRequestDto(int CueId, string CustomerName);

public record CueRentalDto(
    int Id,
    int CueId,
    string CueName,
    string CustomerName,
    DateTime RentedAtUtc,
    DateTime? ReturnedAtUtc,
    string Status);

// Outcome: "Ok" | "Damaged" | "Lost"
public record ReturnCueRentalRequestDto(string Outcome, decimal? PenaltyAmount, string? Note);

public record UpsertCueRequestDto(
    string Name,
    decimal PricePerHour,
    int AvailableCount,
    string? ImageUrl,
    bool IsForSale = false,
    decimal? SalePrice = null,
    string? Description = null,
    string? Brand = null,
    string? Category = null);

// Mua cơ: chỉ tạo YÊU CẦU mua, chưa có thanh toán thật (đợt sau).
public record CueSalePurchaseRequestDto(int CueId, string CustomerName, string PhoneNumber);

public record CuePurchaseRequestDto(
    int Id,
    int CueId,
    string CueName,
    string CustomerName,
    string PhoneNumber,
    DateTime CreatedAtUtc,
    string Status,
    decimal? SalePrice = null,
    int AvailableCount = 0);

// Nhân viên/Admin xử lý yêu cầu mua: "Contacted" (đã liên hệ khách), "Completed" (đã bán -
// trừ tồn kho), "Cancelled" (hủy).
public record UpdatePurchaseRequestStatusDto(string Status);

// Chợ trao đổi cơ (Trade) giữa người chơi.
public record CueListingDto(
    int Id,
    int SellerAccountId,
    string SellerName,
    string? SellerPhoneNumber,
    string Title,
    string? Description,
    string? ImageUrl,
    string? DesiredTrade,
    decimal? Price,
    string Status,
    DateTime CreatedAtUtc);

public record CreateCueListingRequestDto(
    string Title,
    string? Description,
    string? ImageUrl,
    string? DesiredTrade,
    decimal? Price);
