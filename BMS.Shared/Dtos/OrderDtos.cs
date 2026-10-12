namespace BMS.Shared.Dtos;

// Đợt 4: Gọi món & dịch vụ - order lines gắn với một PlayingSession đang Active.

public record OrderDto(
    int Id,
    int PlayingSessionId,
    string ItemName,
    decimal UnitPrice,
    int Quantity,
    decimal LineTotal,
    DateTime CreatedAtUtc,
    string CreatedByName);

public record AddOrderRequestDto(
    string ItemName,
    decimal UnitPrice,
    int Quantity,
    int? InventoryItemId);
