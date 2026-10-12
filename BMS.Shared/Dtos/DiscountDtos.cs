namespace BMS.Shared.Dtos;

// Đợt 9: Giảm giá khi thanh toán - hạn mức theo nhân viên + luồng duyệt.
public record DiscountRequestDto(
    int Id, int SessionId, string TableName, string StaffName, decimal OriginalAmount,
    decimal DiscountPercent, decimal DiscountAmount, string? Reason, string Status,
    bool IsApplied, DateTime CreatedAtUtc, string? DecidedByName, DateTime? DecidedAtUtc);

public record CreateDiscountRequestDto(int SessionId, decimal DiscountPercent, string? Reason);
