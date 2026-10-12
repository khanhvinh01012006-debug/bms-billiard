namespace BMS.API.Models;

public class ChatThreadEntity
{
    public int Id { get; set; }

    // "TableStaff" | "CustomerStaff"
    public string Type { get; set; } = "CustomerStaff";

    public int? TableId { get; set; }
    public TableEntity? Table { get; set; }

    public int? CustomerAccountId { get; set; }
    public AccountEntity? CustomerAccount { get; set; }

    public DateTime CreatedAtUtc { get; set; }
    public DateTime LastMessageAtUtc { get; set; }

    public List<ChatMessageEntity> Messages { get; set; } = new();
}
