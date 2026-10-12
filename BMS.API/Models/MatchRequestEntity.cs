namespace BMS.API.Models;

public class MatchRequestEntity
{
    public int Id { get; set; }
    public int RequesterId { get; set; }
    public PlayerProfileEntity? Requester { get; set; }
    public int? TargetPlayerId { get; set; }
    public PlayerProfileEntity? TargetPlayer { get; set; }

    // "Pending" | "Accepted" | "Rejected"
    public string Status { get; set; } = "Pending";
    public DateTime CreatedAtUtc { get; set; }
}
