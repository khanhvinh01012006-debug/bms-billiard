namespace BMS.API.Models;

// Đợt 9: Một trận đấu trong bảng đấu loại trực tiếp của giải. Round 1 = vòng đầu tiên
// (nhiều trận nhất), Round tăng dần tới vòng chung kết. SlotInRound là vị trí trận trong
// vòng đó (0-based) - dùng để biết người thắng sẽ vào ô nào của vòng kế tiếp
// (NextRound slot = SlotInRound / 2, là Player1 nếu SlotInRound chẵn, Player2 nếu lẻ).
public class TournamentMatchEntity
{
    public int Id { get; set; }
    public int TournamentId { get; set; }
    public TournamentEntity? Tournament { get; set; }
    public int Round { get; set; }
    public int SlotInRound { get; set; }
    public int? Player1RegistrationId { get; set; }
    public string? Player1Name { get; set; }
    public int? Player2RegistrationId { get; set; }
    public string? Player2Name { get; set; }
    public int? WinnerRegistrationId { get; set; }
    public string? WinnerName { get; set; }
    // "Pending" | "Completed"
    public string Status { get; set; } = "Pending";
}
