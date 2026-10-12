namespace BMS.Shared.Dtos;

// Quản lý Giải đấu - Admin quản lý, Player đăng ký tham gia.

public record TournamentDto(
    int Id,
    string Name,
    string? Description,
    DateTime StartDateUtc,
    DateTime EndDateUtc,
    decimal EntryFee,
    int? MaxParticipants,
    string Status,
    string? ImageUrl,
    DateTime CreatedAtUtc,
    int RegisteredCount,
    bool IsFull);

public record UpsertTournamentRequestDto(
    string Name,
    string? Description,
    DateTime StartDateUtc,
    DateTime EndDateUtc,
    decimal EntryFee,
    int? MaxParticipants,
    string Status,
    string? ImageUrl);

public record TournamentRegistrationDto(
    int Id,
    int TournamentId,
    int AccountId,
    string PlayerName,
    string? PhoneNumber,
    DateTime RegisteredAtUtc,
    string Status);

public record RegisterForTournamentRequestDto(string PhoneNumber);

// Đợt 9: Bảng đấu loại trực tiếp (bracket).
public record TournamentMatchDto(
    int Id, int TournamentId, int Round, int SlotInRound,
    int? Player1RegistrationId, string? Player1Name,
    int? Player2RegistrationId, string? Player2Name,
    int? WinnerRegistrationId, string? WinnerName, string Status);

public record SetMatchResultRequestDto(int WinnerRegistrationId);
