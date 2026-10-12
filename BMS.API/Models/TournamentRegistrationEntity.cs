namespace BMS.API.Models;

// Đợt 2: Đăng ký tham gia giải đấu của người chơi.
public class TournamentRegistrationEntity
{
    public int Id { get; set; }
    public int TournamentId { get; set; }
    public TournamentEntity? Tournament { get; set; }
    public int AccountId { get; set; }
    public AccountEntity? Account { get; set; }

    // Snapshot tại thời điểm đăng ký, giống cách CueListing lưu SellerAccount.
    public string PlayerName { get; set; } = string.Empty;
    public string? PhoneNumber { get; set; }
    public DateTime RegisteredAtUtc { get; set; }

    // "Registered" | "Cancelled"
    public string Status { get; set; } = "Registered";
}
