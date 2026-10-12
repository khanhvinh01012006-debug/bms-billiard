namespace BMS.API.Models;

public class BookingEntity
{
    public int Id { get; set; }
    public int TableId { get; set; }
    public TableEntity? Table { get; set; }
    public string CustomerName { get; set; } = string.Empty;
    public string? PhoneNumber { get; set; }
    public string? Note { get; set; }
    public int DurationMinutes { get; set; } = 120;
    public DateTime CreatedAtUtc { get; set; }
    public DateTime? StartTimeUtc { get; set; }
    public DateTime ExpiresAtUtc { get; set; }

    // "Pending" | "Confirmed" | "Cancelled" | "Expired" | "CheckedIn"
    public string Status { get; set; } = "Pending";
}
