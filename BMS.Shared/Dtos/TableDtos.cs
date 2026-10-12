namespace BMS.Shared.Dtos;

// DTOs for tables, booking and playing sessions (EP-02: US-006..US-012)

public enum TableStatus
{
    Available,
    Occupied,
    Reserved,
    Maintenance
}

public record TableDto(
    int Id,
    string Name,
    TableStatus Status,
    int Capacity,
    decimal PricePerHour,
    DateTime? ActiveSessionStartedAtUtc,
    string? ImageUrl = null,
    // Đợt 4: cần Id của phiên chơi đang Active để gọi các endpoint Order (gắn theo
    // session, không theo bàn trực tiếp) - null khi bàn không có phiên chơi đang diễn ra.
    int? ActiveSessionId = null,
    // Đợt 5: tên Loại bàn (Bàn Lỗ/Bàn Phăng/Bàn VIP...), null khi bàn chưa được gán loại.
    string? TableTypeName = null,
    // Đợt 6: Id của phiên chơi đang ở trạng thái "PendingPayment" (đã trả bàn/dừng giờ,
    // chờ chọn phương thức thanh toán) - null khi bàn không ở trạng thái này. Cho phép UI
    // hiển thị nút "Thanh toán" để mở lại bước chọn phương thức bất cứ lúc nào.
    int? PendingPaymentSessionId = null,
    // Phiên đang TẠM DỪNG tính giờ: thời điểm bắt đầu tạm dừng (null khi đang chạy bình thường).
    DateTime? PausedAtUtc = null);

public record CreateBookingRequestDto(
    int TableId,
    string CustomerName,
    DateTime? StartTimeUtc = null,
    string? PhoneNumber = null,
    string? Note = null,
    int DurationMinutes = 120);

public record BookingDto(
    int Id,
    int TableId,
    string CustomerName,
    DateTime CreatedAtUtc,
    string Status,
    string? PhoneNumber = null,
    string? Note = null,
    int DurationMinutes = 120,
    DateTime? StartTimeUtc = null,
    DateTime ExpiresAtUtc = default,
    string? TableName = null,
    string? CancelToken = null);

// Khách tự hủy đặt bàn: mã hủy (có chữ ký của máy chủ) chỉ trả về cho người vừa đặt.
public record CancelBookingByTokenRequestDto(string Token);

public record TableStatusChangedEvent(int TableId, TableStatus NewStatus);

public record BookingExpiredEvent(int BookingId, int TableId);

public record CheckOutResultDto(
    int TableId,
    TimeSpan Duration,
    decimal HourlyAmount,
    decimal OrdersAmount,
    decimal SurchargeAmount,
    decimal TotalAmount,
    // Đợt 6: trailing nhưng luôn được điền giá trị thật từ endpoint checkout - cần để gọi
    // complete-payment ngay sau đó (phiên giờ đã ở trạng thái "PendingPayment", chưa Closed).
    int SessionId = default);

public record UpsertTableRequestDto(string Name, int Capacity, decimal PricePerHour, string? ImageUrl, TableStatus Status = TableStatus.Available,
    // Đợt 5: gán Loại bàn (tùy chọn, trailing để không phá các call site cũ).
    int? TableTypeId = null);

// Đợt 5: Loại bàn + khung giờ tính giá (matches Figma design).
public record PricingSlotDto(int Id, TimeSpan StartTime, TimeSpan EndTime, decimal PricePerHour);

public record TableTypeDto(int Id, string Name, string? Description, List<PricingSlotDto> Slots);

public record UpsertTableTypeRequestDto(string Name, string? Description);

public record UpsertPricingSlotRequestDto(TimeSpan StartTime, TimeSpan EndTime, decimal PricePerHour);

// Đợt 6: Trả bàn 2 bước - dừng giờ trước ("PendingPayment"), rồi chọn phương thức thanh
// toán riêng (Thẻ hội viên / Chuyển khoản / Tiền mặt) để hoàn tất, có hóa đơn in được.
public record CompletePaymentRequestDto(
    // "Wallet" | "Cash" | "BankTransfer"
    string Method,
    int? MemberAccountId = null,
    decimal? CashReceivedAmount = null,
    // Đợt 9: yêu cầu giảm giá đã được Approved cho phiên chơi này (nếu có) - server tự áp
    // dụng số tiền giảm tương ứng vào tổng thanh toán.
    int? DiscountRequestId = null);

public record PaymentCompletedDto(
    int SessionId,
    int TableId,
    string TableName,
    string Method,
    TimeSpan Duration,
    decimal HourlyAmount,
    decimal OrdersAmount,
    decimal SurchargeAmount,
    decimal TotalAmount,
    decimal? CashReceivedAmount,
    decimal? ChangeAmount,
    string? MemberName,
    string ProcessedByName,
    DateTime CompletedAtUtc,
    // Đợt 9: số tiền đã giảm (0 nếu không áp dụng giảm giá).
    decimal DiscountAmount = 0m);

// Đợt 4: Lịch sử phiên chơi (moved here from being an inline record in SessionsController
// so BMS.PlayerWeb, which has no project reference to BMS.API, can deserialize it too).
public record SessionHistoryDto(int Id, int TableId, string TableName, string CustomerName,
    DateTime StartedAtUtc, DateTime? EndedAtUtc, decimal? TotalAmount, string Status,
    decimal OrdersAmount, decimal SurchargeAmount);

// Đợt 9: Dashboard - bản đồ nhiệt công suất bàn theo giờ trong ngày.
public record HourlyOccupancyDto(int Hour, int ActiveTableCount);
