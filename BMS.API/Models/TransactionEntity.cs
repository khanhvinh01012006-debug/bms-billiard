namespace BMS.API.Models;

// Đợt 3: Ví/Hội viên/Thanh toán. Ghi nhận mọi lượt nạp tiền (TopUp) và thanh toán
// (Payment) qua các phương thức khác nhau, có trạng thái phê duyệt cho những giao
// dịch cần nhân viên/quản trị xác nhận thủ công (Cash, BankTransfer).
public class TransactionEntity
{
    public int Id { get; set; }

    // Đợt 6: nullable - một khách vãng lai (walk-in) trả tiền mặt/chuyển khoản không cần
    // có tài khoản trong hệ thống. Chỉ Method = Wallet mới bắt buộc phải có AccountId.
    public int? AccountId { get; set; }
    public AccountEntity? Account { get; set; }

    // "TopUp" | "Payment"
    public string Type { get; set; } = string.Empty;

    // "Wallet" | "Cash" | "BankTransfer"
    public string Method { get; set; } = string.Empty;

    public decimal Amount { get; set; }

    // "Pending" | "Approved" | "Rejected"
    public string Status { get; set; } = "Pending";

    public string? Note { get; set; }

    // Ảnh chứng từ chuyển khoản ngân hàng (nếu Method = BankTransfer).
    public string? ProofImageUrl { get; set; }

    // Ngữ cảnh tùy chọn: giao dịch Payment này thanh toán cho cái gì, vd "CuePurchase", "Booking".
    public string? RelatedEntityType { get; set; }
    public int? RelatedEntityId { get; set; }

    public DateTime CreatedAtUtc { get; set; }

    public int? ProcessedByAccountId { get; set; }
    public string? ProcessedByName { get; set; }
    public DateTime? ProcessedAtUtc { get; set; }
}
