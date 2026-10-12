namespace BMS.API.Models;

// Đợt 4: Bàn giao ca làm việc - một nhân viên bắt đầu ca (ghi tiền quỹ đầu ca), làm việc,
// rồi kết thúc ca (ghi tiền quỹ cuối ca + ghi chú bàn giao cho ca sau).
public class ShiftEntity
{
    public int Id { get; set; }
    public int StaffAccountId { get; set; }
    public string StaffName { get; set; } = string.Empty;
    public DateTime StartedAtUtc { get; set; }
    public decimal OpeningCashAmount { get; set; }
    public DateTime? EndedAtUtc { get; set; }
    public decimal? ClosingCashAmount { get; set; }
    public string? HandoverNote { get; set; }

    // "Active" | "Closed"
    public string Status { get; set; } = "Active";
}
