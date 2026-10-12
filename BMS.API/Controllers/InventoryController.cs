using BMS.API.Data;
using BMS.API.Models;
using BMS.Shared.Dtos;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace BMS.API.Controllers;

// Đợt 2: Quản lý Kho (vật tư, đồ uống, phụ kiện...).
[ApiController]
[Route("api/inventory")]
// Xem kho, phiếu nhập, kiểm kho: Nhân viên + Admin. Thêm / sửa / xóa / chỉnh nhanh vật tư:
// chỉ Admin (đặt ở từng action). Trước đây cả lớp bị khóa "chỉ Admin" nên các endpoint ghi
// "Staff,Admin" thực tế vẫn chặn Nhân viên.
[Authorize(Roles = "Staff,Admin")]
public class InventoryController : ControllerBase
{
    private readonly BmsDbContext _db;

    public InventoryController(BmsDbContext db)
    {
        _db = db;
    }

    private static InventoryItemDto ToDto(InventoryItemEntity i) => new(
        i.Id, i.Name, i.Category, i.Unit, i.Quantity, i.LowStockThreshold, i.UnitPrice,
        i.ImageUrl, i.Note, i.UpdatedAtUtc, i.Quantity <= i.LowStockThreshold);

    // Việc 1: Phiếu nhập hàng + Phiếu kiểm kho định kỳ.
    private static StockInDto ToDto(StockInEntity s) => new(
        s.Id, s.CreatedAtUtc, s.StaffName, s.SupplierName, s.Note,
        s.Lines.Select(l => new StockInLineDto(
            l.Id, l.InventoryItemId, l.InventoryItem?.Name ?? string.Empty, l.PurchaseUnit,
            l.PurchaseQuantity, l.ConversionFactor, l.UnitCost, l.PurchaseQuantity * l.ConversionFactor)).ToList());

    private static StockAuditDto ToDto(StockAuditEntity a) => new(
        a.Id, a.CreatedAtUtc, a.CompletedAtUtc, a.StaffName, a.Note, a.Status,
        a.Lines.Select(l => new StockAuditLineDto(
            l.Id, l.InventoryItemId, l.InventoryItem?.Name ?? string.Empty, l.InventoryItem?.Unit ?? string.Empty,
            l.SystemQuantity, l.ActualQuantity, l.ActualQuantity is null ? null : l.ActualQuantity.Value - l.SystemQuantity)).ToList());

    [HttpGet]
    public async Task<ActionResult<List<InventoryItemDto>>> GetAll()
    {
        var items = await _db.InventoryItems.OrderBy(i => i.Name).ToListAsync();
        return items.Select(ToDto).ToList();
    }

    [HttpGet("{id:int}")]
    public async Task<ActionResult<InventoryItemDto>> GetById(int id)
    {
        var item = await _db.InventoryItems.FindAsync(id);
        if (item is null) return NotFound();
        return ToDto(item);
    }

    [Authorize(Roles = "Admin")]
    [HttpPost]
    public async Task<ActionResult<InventoryItemDto>> Create(UpsertInventoryItemRequestDto request)
    {
        if (string.IsNullOrWhiteSpace(request.Name))
        {
            return BadRequest("Tên vật tư không được để trống.");
        }

        var item = new InventoryItemEntity
        {
            Name = request.Name,
            Category = request.Category,
            Unit = request.Unit,
            Quantity = request.Quantity,
            LowStockThreshold = request.LowStockThreshold,
            UnitPrice = request.UnitPrice,
            ImageUrl = request.ImageUrl,
            Note = request.Note,
            UpdatedAtUtc = DateTime.UtcNow
        };

        _db.InventoryItems.Add(item);
        await _db.SaveChangesAsync();

        return CreatedAtAction(nameof(GetById), new { id = item.Id }, ToDto(item));
    }

    [Authorize(Roles = "Admin")]
    [HttpPut("{id:int}")]
    public async Task<IActionResult> Update(int id, UpsertInventoryItemRequestDto request)
    {
        var item = await _db.InventoryItems.FindAsync(id);
        if (item is null) return NotFound();

        item.Name = request.Name;
        item.Category = request.Category;
        item.Unit = request.Unit;
        item.Quantity = request.Quantity;
        item.LowStockThreshold = request.LowStockThreshold;
        item.UnitPrice = request.UnitPrice;
        item.ImageUrl = request.ImageUrl;
        item.Note = request.Note;
        item.UpdatedAtUtc = DateTime.UtcNow;

        await _db.SaveChangesAsync();
        return NoContent();
    }

    [Authorize(Roles = "Admin")]
    [HttpDelete("{id:int}")]
    public async Task<IActionResult> Delete(int id)
    {
        var item = await _db.InventoryItems.FindAsync(id);
        if (item is null) return NotFound();

        _db.InventoryItems.Remove(item);
        await _db.SaveChangesAsync();
        return NoContent();
    }

    // Điều chỉnh nhanh số lượng tồn kho (+/-) mà không cần mở form sửa đầy đủ.
    [Authorize(Roles = "Admin")]
    [HttpPost("{id:int}/adjust")]
    public async Task<ActionResult<InventoryItemDto>> Adjust(int id, AdjustInventoryQuantityRequestDto request)
    {
        var item = await _db.InventoryItems.FindAsync(id);
        if (item is null) return NotFound();

        item.Quantity = Math.Max(0, item.Quantity + request.Delta);
        item.UpdatedAtUtc = DateTime.UtcNow;

        await _db.SaveChangesAsync();
        return ToDto(item);
    }

    // ===== Việc 1: Phiếu nhập hàng =====

    [Authorize(Roles = "Staff,Admin")]
    [HttpPost("stock-in")]
    public async Task<ActionResult<StockInDto>> CreateStockIn(CreateStockInRequestDto request)
    {
        if (request.Lines is null || request.Lines.Count == 0)
        {
            return BadRequest("Phiếu nhập hàng phải có ít nhất một dòng vật tư.");
        }

        var accountIdClaim = User.FindFirst("userId")?.Value;
        int.TryParse(accountIdClaim, out var accountId);
        var fullName = User.FindFirst("fullName")?.Value ?? "Nhân viên";

        await using var transaction = await _db.Database.BeginTransactionAsync();

        var stockIn = new StockInEntity
        {
            CreatedAtUtc = DateTime.UtcNow,
            StaffAccountId = accountId,
            StaffName = fullName,
            SupplierName = request.SupplierName,
            Note = request.Note
        };

        foreach (var line in request.Lines)
        {
            var item = await _db.InventoryItems.FindAsync(line.InventoryItemId);
            if (item is null) return NotFound($"Không tìm thấy vật tư #{line.InventoryItemId}.");

            stockIn.Lines.Add(new StockInLineEntity
            {
                InventoryItemId = item.Id,
                PurchaseUnit = line.PurchaseUnit,
                PurchaseQuantity = line.PurchaseQuantity,
                ConversionFactor = line.ConversionFactor,
                UnitCost = line.UnitCost
            });

            item.Quantity += line.PurchaseQuantity * line.ConversionFactor;
            item.UpdatedAtUtc = DateTime.UtcNow;
        }

        _db.StockIns.Add(stockIn);
        await _db.SaveChangesAsync();
        await transaction.CommitAsync();

        stockIn = await _db.StockIns
            .Include(s => s.Lines).ThenInclude(l => l.InventoryItem)
            .FirstAsync(s => s.Id == stockIn.Id);

        return Ok(ToDto(stockIn));
    }

    [Authorize(Roles = "Staff,Admin")]
    [HttpGet("stock-in")]
    public async Task<ActionResult<List<StockInDto>>> GetStockInHistory()
    {
        var list = await _db.StockIns
            .Include(s => s.Lines).ThenInclude(l => l.InventoryItem)
            .OrderByDescending(s => s.CreatedAtUtc)
            .ToListAsync();

        return list.Select(ToDto).ToList();
    }

    // ===== Việc 1: Phiếu kiểm kho định kỳ =====

    [Authorize(Roles = "Staff,Admin")]
    [HttpPost("stock-audits")]
    public async Task<ActionResult<StockAuditDto>> CreateStockAudit(CreateStockAuditRequestDto request)
    {
        var accountIdClaim = User.FindFirst("userId")?.Value;
        int.TryParse(accountIdClaim, out var accountId);
        var fullName = User.FindFirst("fullName")?.Value ?? "Nhân viên";

        var items = await _db.InventoryItems.ToListAsync();

        var audit = new StockAuditEntity
        {
            CreatedAtUtc = DateTime.UtcNow,
            StaffAccountId = accountId,
            StaffName = fullName,
            Note = request.Note,
            Status = "Draft"
        };

        foreach (var item in items)
        {
            audit.Lines.Add(new StockAuditLineEntity
            {
                InventoryItemId = item.Id,
                SystemQuantity = item.Quantity,
                ActualQuantity = null
            });
        }

        _db.StockAudits.Add(audit);
        await _db.SaveChangesAsync();

        audit = await _db.StockAudits
            .Include(a => a.Lines).ThenInclude(l => l.InventoryItem)
            .FirstAsync(a => a.Id == audit.Id);

        return Ok(ToDto(audit));
    }

    [Authorize(Roles = "Staff,Admin")]
    [HttpGet("stock-audits")]
    public async Task<ActionResult<List<StockAuditDto>>> GetStockAudits()
    {
        var list = await _db.StockAudits
            .Include(a => a.Lines).ThenInclude(l => l.InventoryItem)
            .OrderByDescending(a => a.CreatedAtUtc)
            .ToListAsync();

        return list.Select(ToDto).ToList();
    }

    [Authorize(Roles = "Staff,Admin")]
    [HttpGet("stock-audits/{id:int}")]
    public async Task<ActionResult<StockAuditDto>> GetStockAuditById(int id)
    {
        var audit = await _db.StockAudits
            .Include(a => a.Lines).ThenInclude(l => l.InventoryItem)
            .FirstOrDefaultAsync(a => a.Id == id);

        if (audit is null) return NotFound();
        return ToDto(audit);
    }

    [Authorize(Roles = "Staff,Admin")]
    [HttpPut("stock-audits/{auditId:int}/lines/{lineId:int}")]
    public async Task<IActionResult> UpdateStockAuditLine(int auditId, int lineId, UpdateStockAuditLineRequestDto request)
    {
        var line = await _db.StockAuditLines
            .Include(l => l.StockAudit)
            .FirstOrDefaultAsync(l => l.Id == lineId && l.StockAuditId == auditId);

        if (line is null) return NotFound();
        if (line.StockAudit!.Status != "Draft") return Conflict("Phiếu kiểm kho này đã hoàn tất.");

        line.ActualQuantity = request.ActualQuantity;
        await _db.SaveChangesAsync();
        return NoContent();
    }

    [Authorize(Roles = "Staff,Admin")]
    [HttpPost("stock-audits/{id:int}/complete")]
    public async Task<ActionResult<StockAuditDto>> CompleteStockAudit(int id)
    {
        await using var transaction = await _db.Database.BeginTransactionAsync();

        var audit = await _db.StockAudits
            .Include(a => a.Lines).ThenInclude(l => l.InventoryItem)
            .FirstOrDefaultAsync(a => a.Id == id);

        if (audit is null) return NotFound();
        if (audit.Status != "Draft") return Conflict("Phiếu kiểm kho này đã hoàn tất.");

        if (audit.Lines.Any(l => l.ActualQuantity == null))
        {
            return BadRequest("Vui lòng nhập đủ số lượng thực tế cho tất cả vật tư trước khi hoàn tất.");
        }

        foreach (var line in audit.Lines)
        {
            line.InventoryItem!.Quantity = line.ActualQuantity!.Value;
            line.InventoryItem.UpdatedAtUtc = DateTime.UtcNow;
        }

        audit.Status = "Completed";
        audit.CompletedAtUtc = DateTime.UtcNow;

        await _db.SaveChangesAsync();
        await transaction.CommitAsync();

        return ToDto(audit);
    }
}
