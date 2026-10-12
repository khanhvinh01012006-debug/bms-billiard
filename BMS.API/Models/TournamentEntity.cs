namespace BMS.API.Models;

// Đợt 2: Quản lý Giải đấu - Admin tạo/quản lý, Player đăng ký tham gia.
public class TournamentEntity
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
    public DateTime StartDateUtc { get; set; }
    public DateTime EndDateUtc { get; set; }
    public decimal EntryFee { get; set; }
    public int? MaxParticipants { get; set; }

    // "Draft" | "Open" | "InProgress" | "Completed" | "Cancelled"
    public string Status { get; set; } = "Draft";
    public string? ImageUrl { get; set; }
    public DateTime CreatedAtUtc { get; set; }

    public List<TournamentRegistrationEntity> Registrations { get; set; } = new();
}
