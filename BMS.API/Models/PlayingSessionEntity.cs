namespace BMS.API.Models;

public class PlayingSessionEntity
{
    public int Id { get; set; }
    public int TableId { get; set; }
    public TableEntity? Table { get; set; }
    public string CustomerName { get; set; } = string.Empty;
    public DateTime StartedAtUtc { get; set; }
    public DateTime? EndedAtUtc { get; set; }
    public decimal? TotalAmount { get; set; }

    // "Active" | "Paused" | "PendingPayment" | "Closed"
    // "Paused" = nhân viên tạm dừng tính giờ; trong lúc tạm dừng EndedAtUtc giữ THỜI ĐIỂM bắt đầu
    // tạm dừng (không thêm cột mới). Khi chơi tiếp, StartedAtUtc được dời lên đúng bằng thời gian
    // đã dừng nên thời gian bị tính tiền = thời gian chơi thật.
    // Đợt 6: "PendingPayment" = đã bấm "Trả bàn" (đồng hồ đã dừng, chốt tổng tiền) nhưng
    // nhân viên chưa chọn phương thức thanh toán để hoàn tất - bàn vẫn Occupied cho tới
    // khi complete-payment xong.
    public string Status { get; set; } = "Active";

    // Đợt 4: phụ thu (ví dụ phí hư hỏng) - chỉ được đặt lúc trả bàn (checkout), không phải
    // lúc nhận bàn, nên đứng riêng với TotalAmount (tổng cuối = tiền giờ + tiền order + phụ thu).
    public decimal SurchargeAmount { get; set; } = 0m;
    public string? SurchargeReason { get; set; }
}
