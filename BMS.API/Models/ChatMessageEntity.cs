namespace BMS.API.Models;

public class ChatMessageEntity
{
    public int Id { get; set; }
    public int ThreadId { get; set; }
    public ChatThreadEntity? Thread { get; set; }

    public int SenderAccountId { get; set; }

    // Snapshot của role tại thời điểm gửi: "Player" | "Staff" | "Admin"
    public string SenderRole { get; set; } = "Player";
    public string SenderName { get; set; } = string.Empty;

    public string? Text { get; set; }
    public string? ImageUrl { get; set; }
    public DateTime SentAtUtc { get; set; }
    public bool IsRead { get; set; }
}
