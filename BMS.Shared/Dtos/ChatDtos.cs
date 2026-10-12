namespace BMS.Shared.Dtos;

// DTOs for the customer <-> staff chat system (đợt 2, phần D).
// Real-time delivery only, over SignalR ("ChatMessageReceived"), while the app/tab is
// open. Push notification when the app is closed is a TODO for a later sprint
// (requires Firebase Cloud Messaging integration - out of scope this round).

// Type: "TableStaff" | "CustomerStaff"
public record ChatThreadDto(
    int Id,
    string Type,
    int? TableId,
    string? TableName,
    int? CustomerAccountId,
    string? CustomerName,
    DateTime CreatedAtUtc,
    DateTime LastMessageAtUtc,
    int UnreadCount = 0);

public record ChatMessageDto(
    int Id,
    int ThreadId,
    int SenderAccountId,
    string SenderRole,
    string SenderName,
    string? Text,
    string? ImageUrl,
    DateTime SentAtUtc,
    bool IsRead);

public record SendChatMessageRequestDto(int ThreadId, string? Text, string? ImageUrl);

public record CreateChatThreadRequestDto(string Type, int? TableId);
