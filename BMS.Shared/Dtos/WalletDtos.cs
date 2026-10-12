namespace BMS.Shared.Dtos;

// Đợt 3: Ví / Hội viên / Thanh toán (nạp tiền vào thẻ billiard, thanh toán bằng
// ví/tiền mặt/chuyển khoản, có báo cáo + phê duyệt).

// NextTierThreshold = 0 và NextTierName = null khi đã ở hạng cao nhất (Kim Cương).
public record WalletInfoDto(
    decimal WalletBalance,
    string MembershipTier,
    decimal TotalTopUpAmount,
    decimal NextTierThreshold,
    string? NextTierName);

public record TransactionDto(
    int Id,
    // Đợt 6: nullable - null khi giao dịch Payment thuộc về khách vãng lai không có tài
    // khoản (Cash/BankTransfer không xác định hội viên). AccountName vẫn luôn có giá trị
    // hiển thị được (server điền "Khách vãng lai" khi AccountId null).
    int? AccountId,
    string AccountName,
    string Type,
    string Method,
    decimal Amount,
    string Status,
    string? Note,
    string? ProofImageUrl,
    string? RelatedEntityType,
    int? RelatedEntityId,
    DateTime CreatedAtUtc,
    string? ProcessedByName,
    DateTime? ProcessedAtUtc);

// Method phải là "Cash" hoặc "BankTransfer" - nạp qua "Wallet" không có ý nghĩa.
public record CreateTopUpRequestDto(decimal Amount, string Method, string? Note, string? ProofImageUrl);

public record ProcessTransactionRequestDto(bool Approve, string? Note);

// Thanh toán trực tiếp bằng số dư Ví - trừ tiền ngay lập tức, không cần phê duyệt.
public record PayWithWalletRequestDto(decimal Amount, string RelatedEntityType, int? RelatedEntityId, string? Note);

// Danh sách hội viên cho Admin (Hội viên & báo cáo).
public record MemberInfoDto(
    int AccountId,
    string FullName,
    string Email,
    string? PhoneNumber,
    string MembershipTier,
    decimal WalletBalance,
    decimal TotalTopUpAmount,
    // Đợt 9: ghi chú nội bộ của nhân viên/Admin, Player không thấy được.
    string? Note = null);

// Đợt 9: Gói giờ bàn trả trước.
public record HourPackageDto(
    int Id, int AccountId, string PackageName, decimal TotalHours, decimal RemainingHours,
    decimal PricePaid, DateTime PurchasedAtUtc, DateTime? ExpiresAtUtc, string Status);

public record CreateHourPackageRequestDto(int AccountId, string PackageName, decimal TotalHours, decimal PricePaid, DateTime? ExpiresAtUtc);

public record ConsumeHourPackageRequestDto(decimal Hours);

public record UpdateMemberNoteRequestDto(string? Note);

// Tích điểm hội viên (xem BMS.Shared.LoyaltyPoints).
public record PointsInfoDto(int Balance, int Earned, int Redeemed, decimal TotalSpent);

public record RedeemPointsRequestDto(int Points);

// Mã QR hội viên: chuỗi có chữ ký của máy chủ, hết hạn sau ít phút (chống dùng ảnh chụp cũ).
public record MemberQrDto(string Code, DateTime ExpiresAtUtc);
