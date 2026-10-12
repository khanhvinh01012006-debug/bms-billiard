namespace BMS.API.Models;

// Đợt 4: Chấm công - đơn giản hơn Ca làm việc (Shift), chỉ phục vụ tính công/giờ làm cho
// payroll/HR, tách biệt với việc quản lý tiền quỹ/bàn giao của Shift.
public class AttendanceEntity
{
    public int Id { get; set; }
    public int StaffAccountId { get; set; }
    public string StaffName { get; set; } = string.Empty;
    public DateTime ClockInAtUtc { get; set; }
    public DateTime? ClockOutAtUtc { get; set; }
    public string? Note { get; set; }
}
