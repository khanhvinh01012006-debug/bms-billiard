namespace BMS.API.Models;

// Yêu cầu MUA cơ. Chưa có hệ thống thanh toán (để đợt sau) nên đây chỉ là một
// yêu cầu chờ nhân viên/Admin xử lý thủ công ngoài hệ thống, KHÔNG trừ tiền thật.
public class CuePurchaseRequestEntity
{
    public int Id { get; set; }
    public int CueId { get; set; }
    public CueEntity? Cue { get; set; }
    public string CustomerName { get; set; } = string.Empty;
    public string PhoneNumber { get; set; } = string.Empty;
    public int? RequesterAccountId { get; set; }
    public DateTime CreatedAtUtc { get; set; }

    // "Pending" | "Handled" | "Cancelled"
    public string Status { get; set; } = "Pending";
}
