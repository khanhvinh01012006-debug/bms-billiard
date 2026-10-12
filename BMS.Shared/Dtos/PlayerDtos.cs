namespace BMS.Shared.Dtos;

// DTOs for player profiles and ELO-based match making (EP-08: US-039, US-040)

public record PlayerProfileDto(int Id, string DisplayName, int EloRating, string? AvatarUrl = null);

public record CreateMatchRequestDto(int RequesterId, int? TargetPlayerId);

public record MatchRequestDto(
    int Id,
    int RequesterId,
    string RequesterName,
    int? TargetPlayerId,
    string? TargetName,
    string Status,
    DateTime CreatedAtUtc);
