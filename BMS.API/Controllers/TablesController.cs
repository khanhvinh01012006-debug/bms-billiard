using BMS.API.Data;
using BMS.API.Hubs;
using BMS.API.Models;
using BMS.Shared.Dtos;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;

namespace BMS.API.Controllers;

// EP-02: US-006 (list), US-007 (status). Admin CRUD supports EP-02 table setup.
[ApiController]
[Route("api/tables")]
public class TablesController : ControllerBase
{
    private readonly BmsDbContext _db;
    private readonly IHubContext<BmsHub> _hub;

    public TablesController(BmsDbContext db, IHubContext<BmsHub> hub)
    {
        _db = db;
        _hub = hub;
    }

    private static TableDto ToDto(TableEntity t)
    {
        var activeSession = t.Sessions.Where(s => s.Status is "Active" or "Paused").FirstOrDefault();
        // Đợt 6: phiên đã "Trả bàn" (dừng giờ) nhưng chưa hoàn tất chọn phương thức thanh toán.
        var pendingPaymentSession = t.Sessions.Where(s => s.Status == "PendingPayment").FirstOrDefault();

        return new TableDto(t.Id, t.Name, t.Status, t.Capacity, t.PricePerHour,
            activeSession?.StartedAtUtc, t.ImageUrl, activeSession?.Id, t.TableType?.Name,
            pendingPaymentSession?.Id,
            activeSession?.Status == "Paused" ? activeSession.EndedAtUtc : null);
    }

    // US-006: Xem danh sách bàn
    [AllowAnonymous]
    [HttpGet]
    public async Task<ActionResult<List<TableDto>>> GetAll()
    {
        var tables = await _db.Tables
            .Include(t => t.Sessions.Where(s => s.Status == "Active" || s.Status == "Paused" || s.Status == "PendingPayment"))
            .Include(t => t.TableType)
            .OrderBy(t => t.Id)
            .ToListAsync();

        return tables.Select(ToDto).ToList();
    }

    // US-007: Xem trạng thái bàn (một bàn cụ thể)
    [AllowAnonymous]
    [HttpGet("{id:int}")]
    public async Task<ActionResult<TableDto>> GetById(int id)
    {
        var table = await _db.Tables
            .Include(t => t.Sessions.Where(s => s.Status == "Active" || s.Status == "Paused" || s.Status == "PendingPayment"))
            .Include(t => t.TableType)
            .FirstOrDefaultAsync(t => t.Id == id);

        if (table is null) return NotFound();
        return ToDto(table);
    }

    [Authorize(Roles = "Admin")]
    [HttpPost]
    public async Task<ActionResult<TableDto>> Create(UpsertTableRequestDto request)
    {
        var table = new TableEntity
        {
            Name = request.Name,
            Capacity = request.Capacity,
            PricePerHour = request.PricePerHour,
            ImageUrl = request.ImageUrl,
            Status = request.Status,
            TableTypeId = request.TableTypeId
        };

        _db.Tables.Add(table);
        await _db.SaveChangesAsync();

        await _hub.Clients.All.SendAsync("TableStatusChanged",
            new TableStatusChangedEvent(table.Id, table.Status));

        return CreatedAtAction(nameof(GetById), new { id = table.Id }, ToDto(table));
    }

    [Authorize(Roles = "Admin")]
    [HttpPut("{id:int}")]
    public async Task<IActionResult> Update(int id, UpsertTableRequestDto request)
    {
        var table = await _db.Tables.FindAsync(id);
        if (table is null) return NotFound();

        // Chặn admin đổi trạng thái đè lên một phiên chơi đang diễn ra (Occupied thật) -
        // tránh mất theo dõi thời gian/tiền của khách đang chơi. Các trạng thái khác đổi tự do.
        var hasActiveSession = await _db.PlayingSessions
            .AnyAsync(s => s.TableId == id && (s.Status == "Active" || s.Status == "Paused"));
        if (hasActiveSession && table.Status == TableStatus.Occupied && request.Status != TableStatus.Occupied)
        {
            return BadRequest("Bàn đang có phiên chơi hoạt động - hãy Trả bàn (checkout) trước khi đổi trạng thái.");
        }

        table.Name = request.Name;
        table.Capacity = request.Capacity;
        table.PricePerHour = request.PricePerHour;
        table.ImageUrl = request.ImageUrl;
        table.Status = request.Status;
        table.TableTypeId = request.TableTypeId;

        await _db.SaveChangesAsync();

        await _hub.Clients.All.SendAsync("TableStatusChanged",
            new TableStatusChangedEvent(table.Id, table.Status));

        return NoContent();
    }

    [Authorize(Roles = "Admin")]
    [HttpDelete("{id:int}")]
    public async Task<IActionResult> Delete(int id)
    {
        var table = await _db.Tables.FindAsync(id);
        if (table is null) return NotFound();

        _db.Tables.Remove(table);
        await _db.SaveChangesAsync();

        return NoContent();
    }
}
