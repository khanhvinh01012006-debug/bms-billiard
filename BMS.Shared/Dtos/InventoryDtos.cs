namespace BMS.Shared.Dtos;

// Quản lý Kho (Inventory) - Admin only.

public record InventoryItemDto(
    int Id,
    string Name,
    string Category,
    string Unit,
    int Quantity,
    int LowStockThreshold,
    decimal? UnitPrice,
    string? ImageUrl,
    string? Note,
    DateTime UpdatedAtUtc,
    bool IsLowStock);

public record UpsertInventoryItemRequestDto(
    string Name,
    string Category,
    string Unit,
    int Quantity,
    int LowStockThreshold,
    decimal? UnitPrice,
    string? ImageUrl,
    string? Note);

// Điều chỉnh nhanh số lượng tồn kho (+/-) mà không cần mở form sửa đầy đủ.
public record AdjustInventoryQuantityRequestDto(int Delta, string? Reason);

// Việc 1: Phiếu nhập hàng + Phiếu kiểm kho định kỳ.
public record StockInLineDto(int Id, int InventoryItemId, string InventoryItemName, string PurchaseUnit, int PurchaseQuantity, int ConversionFactor, decimal? UnitCost, int BaseUnitsAdded);
public record StockInDto(int Id, DateTime CreatedAtUtc, string StaffName, string? SupplierName, string? Note, List<StockInLineDto> Lines);
public record CreateStockInLineRequestDto(int InventoryItemId, string PurchaseUnit, int PurchaseQuantity, int ConversionFactor, decimal? UnitCost);
public record CreateStockInRequestDto(string? SupplierName, string? Note, List<CreateStockInLineRequestDto> Lines);
public record StockAuditLineDto(int Id, int InventoryItemId, string InventoryItemName, string Unit, int SystemQuantity, int? ActualQuantity, int? Discrepancy);
public record StockAuditDto(int Id, DateTime CreatedAtUtc, DateTime? CompletedAtUtc, string StaffName, string? Note, string Status, List<StockAuditLineDto> Lines);
public record CreateStockAuditRequestDto(string? Note);
public record UpdateStockAuditLineRequestDto(int ActualQuantity);
