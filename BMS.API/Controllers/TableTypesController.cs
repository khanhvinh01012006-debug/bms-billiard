using BMS.API.Data;
using BMS.API.Models;
using BMS.Shared.Dtos;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace BMS.API.Controllers;

// Đợt 5: Loại bàn (Bàn Lỗ/Bàn Phăng/Bàn VIP...) + khung giờ tính giá theo loại bàn.
// Đọc (GetAll/GetById) công khai để trang Bàn chơi / trang Quản lý bàn có thể hiển thị
// tên loại bàn; thêm/sửa/xóa loại bàn và khung giờ chỉ Admin.
[ApiController]
[Route("api/table-types")]
public class TableTypesController : ControllerBase
{
    private readonly BmsDbContext _db;

    public TableTypesController(BmsDbContext db)
    {
        _db = db;
    }

    private static TableTypeDto ToDto(TableTypeEntity t) => new(
        t.Id, t.Name, t.Description,
        t.PricingSlots.Select(s => new PricingSlotDto(s.Id, s.StartTime, s.EndTime, s.PricePerHour)).ToList());

    [HttpGet]
    public async Task<ActionResult<List<TableTypeDto>>> GetAll()
    {
        var types = await _db.TableTypes
            .Include(t => t.PricingSlots)
            .OrderBy(t => t.Id)
            .ToListAsync();

        return types.Select(ToDto).ToList();
    }

    [HttpGet("{id:int}")]
    public async Task<ActionResult<TableTypeDto>> GetById(int id)
    {
        var type = await _db.TableTypes
            .Include(t => t.PricingSlots)
            .FirstOrDefaultAsync(t => t.Id == id);

        if (type is null) return NotFound();
        return ToDto(type);
    }

    [Authorize(Roles = "Admin")]
    [HttpPost]
    public async Task<ActionResult<TableTypeDto>> Create(UpsertTableTypeRequestDto request)
    {
        if (string.IsNullOrWhiteSpace(request.Name))
        {
            return BadRequest("Tên loại bàn không được để trống.");
        }

        var type = new TableTypeEntity { Name = request.Name, Description = request.Description };
        _db.TableTypes.Add(type);
        await _db.SaveChangesAsync();

        return CreatedAtAction(nameof(GetById), new { id = type.Id }, ToDto(type));
    }

    [Authorize(Roles = "Admin")]
    [HttpPut("{id:int}")]
    public async Task<IActionResult> Update(int id, UpsertTableTypeRequestDto request)
    {
        var type = await _db.TableTypes.FindAsync(id);
        if (type is null) return NotFound();

        type.Name = request.Name;
        type.Description = request.Description;
        await _db.SaveChangesAsync();

        return NoContent();
    }

    [Authorize(Roles = "Admin")]
    [HttpDelete("{id:int}")]
    public async Task<IActionResult> Delete(int id)
    {
        var type = await _db.TableTypes.FindAsync(id);
        if (type is null) return NotFound();

        // Các bàn đang gán loại này sẽ tự chuyển về "chưa gán loại" (TableTypeId = null,
        // SetNull ở OnModelCreating) chứ không bị xóa - fallback về PricePerHour cố định.
        _db.TableTypes.Remove(type);
        await _db.SaveChangesAsync();

        return NoContent();
    }

    // --- Khung giờ tính giá của một Loại bàn ---

    [Authorize(Roles = "Admin")]
    [HttpPost("{tableTypeId:int}/pricing-slots")]
    public async Task<ActionResult<PricingSlotDto>> AddSlot(int tableTypeId, UpsertPricingSlotRequestDto request)
    {
        var type = await _db.TableTypes.FindAsync(tableTypeId);
        if (type is null) return NotFound("Không tìm thấy loại bàn.");

        if (request.PricePerHour < 0)
        {
            return BadRequest("Giá theo giờ không được âm.");
        }

        var slot = new PricingSlotEntity
        {
            TableTypeId = tableTypeId,
            StartTime = request.StartTime,
            EndTime = request.EndTime,
            PricePerHour = request.PricePerHour
        };

        _db.PricingSlots.Add(slot);
        await _db.SaveChangesAsync();

        return Ok(new PricingSlotDto(slot.Id, slot.StartTime, slot.EndTime, slot.PricePerHour));
    }

    [Authorize(Roles = "Admin")]
    [HttpPut("pricing-slots/{slotId:int}")]
    public async Task<IActionResult> UpdateSlot(int slotId, UpsertPricingSlotRequestDto request)
    {
        var slot = await _db.PricingSlots.FindAsync(slotId);
        if (slot is null) return NotFound();

        if (request.PricePerHour < 0)
        {
            return BadRequest("Giá theo giờ không được âm.");
        }

        slot.StartTime = request.StartTime;
        slot.EndTime = request.EndTime;
        slot.PricePerHour = request.PricePerHour;
        await _db.SaveChangesAsync();

        return NoContent();
    }

    [Authorize(Roles = "Admin")]
    [HttpDelete("pricing-slots/{slotId:int}")]
    public async Task<IActionResult> DeleteSlot(int slotId)
    {
        var slot = await _db.PricingSlots.FindAsync(slotId);
        if (slot is null) return NotFound();

        _db.PricingSlots.Remove(slot);
        await _db.SaveChangesAsync();

        return NoContent();
    }
}
