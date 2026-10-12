using BMS.API.Data;
using BMS.API.Hubs;
using BMS.API.Models;
using BMS.Shared.Dtos;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;

namespace BMS.API.Controllers;

// EP-08: US-041 Ghi nhận thuê cơ, US-042 Cơ hư hỏng, US-043 Cơ mất + đền tiền.
[ApiController]
[Route("api/cues")]
public class CuesController : ControllerBase
{
    private readonly BmsDbContext _db;
    private readonly IHubContext<BmsHub> _hub;

    public CuesController(BmsDbContext db, IHubContext<BmsHub> hub)
    {
        _db = db;
        _hub = hub;
    }

    private static CueDto ToDto(CueEntity c) => new(
        c.Id, c.Name, c.PricePerHour, c.AvailableCount, c.ImageUrl,
        c.IsForSale, c.SalePrice, c.Description, c.Brand, c.Category);

    private static CuePurchaseRequestDto ToPurchaseDto(CuePurchaseRequestEntity r) => new(
        r.Id, r.CueId, r.Cue?.Name ?? string.Empty, r.CustomerName, r.PhoneNumber, r.CreatedAtUtc, r.Status,
        r.Cue?.SalePrice, r.Cue?.AvailableCount ?? 0);

    private static CueRentalDto ToRentalDto(CueRentalEntity r) => new(
        r.Id, r.CueId, r.Cue?.Name ?? string.Empty, r.CustomerName,
        r.RentedAtUtc, r.ReturnedAtUtc, r.Status);

    [AllowAnonymous]
    [HttpGet]
    public async Task<ActionResult<List<CueDto>>> GetAll()
    {
        var cues = await _db.Cues.OrderBy(c => c.Id).ToListAsync();
        return cues.Select(ToDto).ToList();
    }

    [Authorize(Roles = "Admin")]
    [HttpPost]
    public async Task<ActionResult<CueDto>> Create(UpsertCueRequestDto request)
    {
        var cue = new CueEntity
        {
            Name = request.Name,
            PricePerHour = request.PricePerHour,
            AvailableCount = request.AvailableCount,
            ImageUrl = request.ImageUrl,
            IsForSale = request.IsForSale,
            SalePrice = request.SalePrice,
            Description = request.Description,
            Brand = request.Brand,
            Category = request.Category
        };

        _db.Cues.Add(cue);
        await _db.SaveChangesAsync();

        return CreatedAtAction(nameof(GetById), new { id = cue.Id }, ToDto(cue));
    }

    [AllowAnonymous]
    [HttpGet("{id:int}")]
    public async Task<ActionResult<CueDto>> GetById(int id)
    {
        var cue = await _db.Cues.FindAsync(id);
        if (cue is null) return NotFound();
        return ToDto(cue);
    }

    [Authorize(Roles = "Admin")]
    [HttpPut("{id:int}")]
    public async Task<IActionResult> Update(int id, UpsertCueRequestDto request)
    {
        var cue = await _db.Cues.FindAsync(id);
        if (cue is null) return NotFound();

        cue.Name = request.Name;
        cue.PricePerHour = request.PricePerHour;
        cue.AvailableCount = request.AvailableCount;
        cue.ImageUrl = request.ImageUrl;
        cue.IsForSale = request.IsForSale;
        cue.SalePrice = request.SalePrice;
        cue.Description = request.Description;
        cue.Brand = request.Brand;
        cue.Category = request.Category;

        await _db.SaveChangesAsync();
        return NoContent();
    }

    // Gửi yêu cầu MUA một cây cơ (chỉ ghi nhận, chưa có thanh toán thật - đợt sau).
    [Authorize]
    [HttpPost("purchase-request")]
    public async Task<ActionResult<CuePurchaseRequestDto>> CreatePurchaseRequest(CueSalePurchaseRequestDto request)
    {
        if (string.IsNullOrWhiteSpace(request.CustomerName) || string.IsNullOrWhiteSpace(request.PhoneNumber))
        {
            return BadRequest("Họ tên và số điện thoại là bắt buộc.");
        }

        var cue = await _db.Cues.FindAsync(request.CueId);
        if (cue is null) return NotFound("Không tìm thấy cơ.");
        if (!cue.IsForSale) return Conflict("Cây cơ này hiện không được rao bán.");

        var accountIdClaim = User.FindFirst("userId")?.Value;
        int? requesterAccountId = int.TryParse(accountIdClaim, out var accId) ? accId : null;

        var purchaseRequest = new CuePurchaseRequestEntity
        {
            CueId = cue.Id,
            CustomerName = request.CustomerName,
            PhoneNumber = request.PhoneNumber,
            RequesterAccountId = requesterAccountId,
            CreatedAtUtc = DateTime.UtcNow,
            Status = "Pending"
        };

        _db.CuePurchaseRequests.Add(purchaseRequest);
        await _db.SaveChangesAsync();

        purchaseRequest.Cue = cue;
        var dto = ToPurchaseDto(purchaseRequest);
        await _hub.Clients.All.SendAsync("CuePurchaseRequestChanged", dto);
        return CreatedAtAction(nameof(GetPurchaseRequests), null, dto);
    }

    // Danh sách yêu cầu mua cơ - để nhân viên/Admin xử lý thủ công.
    [Authorize(Roles = "Staff,Admin")]
    [HttpGet("purchase-requests")]
    public async Task<ActionResult<List<CuePurchaseRequestDto>>> GetPurchaseRequests()
    {
        var list = await _db.CuePurchaseRequests
            .Include(r => r.Cue)
            .OrderByDescending(r => r.CreatedAtUtc)
            .ToListAsync();
        return list.Select(ToPurchaseDto).ToList();
    }

    // Xử lý yêu cầu mua: Chờ xử lý -> Đã liên hệ -> Đã bán (trừ 1 vào tồn kho) / Đã hủy.
    [Authorize(Roles = "Staff,Admin")]
    [HttpPut("purchase-requests/{id:int}/status")]
    public async Task<ActionResult<CuePurchaseRequestDto>> UpdatePurchaseRequestStatus(int id, UpdatePurchaseRequestStatusDto request)
    {
        var allowed = new[] { "Contacted", "Completed", "Cancelled" };
        if (!allowed.Contains(request.Status))
        {
            return BadRequest("Trạng thái không hợp lệ.");
        }

        await using var transaction = await _db.Database.BeginTransactionAsync();

        var purchaseRequest = await _db.CuePurchaseRequests.Include(r => r.Cue).FirstOrDefaultAsync(r => r.Id == id);
        if (purchaseRequest is null) return NotFound();
        if (purchaseRequest.Status is "Completed" or "Cancelled")
        {
            return Conflict("Yêu cầu này đã được xử lý xong.");
        }

        if (request.Status == "Completed")
        {
            if (purchaseRequest.Cue is null || purchaseRequest.Cue.AvailableCount <= 0)
            {
                return Conflict("Hàng này đã hết, không thể ghi nhận đã bán.");
            }
            purchaseRequest.Cue.AvailableCount -= 1;
        }

        purchaseRequest.Status = request.Status;
        await _db.SaveChangesAsync();
        await transaction.CommitAsync();

        var dto = ToPurchaseDto(purchaseRequest);
        await _hub.Clients.All.SendAsync("CuePurchaseRequestChanged", dto);
        return dto;
    }

    [Authorize(Roles = "Admin")]
    [HttpDelete("{id:int}")]
    public async Task<IActionResult> Delete(int id)
    {
        var cue = await _db.Cues.FindAsync(id);
        if (cue is null) return NotFound();

        _db.Cues.Remove(cue);
        await _db.SaveChangesAsync();
        return NoContent();
    }

    [Authorize(Roles = "Staff,Admin")]
    [HttpGet("rentals")]
    public async Task<ActionResult<List<CueRentalDto>>> GetActiveRentals()
    {
        var rentals = await _db.CueRentals
            .Include(r => r.Cue)
            .Where(r => r.Status == "Active")
            .OrderByDescending(r => r.RentedAtUtc)
            .ToListAsync();

        return rentals.Select(ToRentalDto).ToList();
    }

    // US-041: Ghi nhận thuê cơ. Decrements AvailableCount inside a transaction so two
    // simultaneous rentals cannot both take the last cue.
    [Authorize]
    [HttpPost("rent")]
    public async Task<ActionResult<CueRentalDto>> Rent(CreateCueRentalRequestDto request)
    {
        if (string.IsNullOrWhiteSpace(request.CustomerName))
        {
            return BadRequest("Vui lòng nhập tên khách.");
        }

        await using var transaction = await _db.Database.BeginTransactionAsync();

        var cue = await _db.Cues.FirstOrDefaultAsync(c => c.Id == request.CueId);
        if (cue is null) return NotFound("Không tìm thấy cơ.");

        if (cue.AvailableCount <= 0)
        {
            return Conflict("Cơ này đã cho thuê hết.");
        }

        cue.AvailableCount -= 1;

        var rental = new CueRentalEntity
        {
            CueId = cue.Id,
            CustomerName = request.CustomerName,
            RentedAtUtc = DateTime.UtcNow,
            Status = "Active"
        };
        _db.CueRentals.Add(rental);

        await _db.SaveChangesAsync();
        await transaction.CommitAsync();

        rental.Cue = cue;
        var dto = ToRentalDto(rental);
        await _hub.Clients.All.SendAsync("CueRentalChanged", dto);

        return CreatedAtAction(nameof(GetRentalById), new { id = rental.Id }, dto);
    }

    [Authorize(Roles = "Staff,Admin")]
    [HttpGet("rentals/{id:int}")]
    public async Task<ActionResult<CueRentalDto>> GetRentalById(int id)
    {
        var rental = await _db.CueRentals.Include(r => r.Cue).FirstOrDefaultAsync(r => r.Id == id);
        if (rental is null) return NotFound();
        return ToRentalDto(rental);
    }

    // US-041/US-042/US-043: Trả cơ. Outcome "Ok" returns the cue to stock, "Damaged"
    // records damage (cue stays out of stock), "Lost" records loss + penalty amount.
    [Authorize(Roles = "Staff,Admin")]
    [HttpPost("rentals/{id:int}/return")]
    public async Task<ActionResult<CueRentalDto>> Return(int id, ReturnCueRentalRequestDto request)
    {
        await using var transaction = await _db.Database.BeginTransactionAsync();

        var rental = await _db.CueRentals.Include(r => r.Cue).FirstOrDefaultAsync(r => r.Id == id);
        if (rental is null) return NotFound();
        if (rental.Status != "Active") return Conflict("Lượt thuê này đã được xử lý.");

        rental.ReturnedAtUtc = DateTime.UtcNow;
        rental.Note = request.Note;

        switch (request.Outcome)
        {
            case "Ok":
                rental.Status = "Returned";
                if (rental.Cue is not null) rental.Cue.AvailableCount += 1;
                break;
            case "Damaged":
                rental.Status = "Damaged";
                rental.PenaltyAmount = request.PenaltyAmount;
                // Damaged cue is not returned to available stock until repaired manually.
                break;
            case "Lost":
                rental.Status = "Lost";
                rental.PenaltyAmount = request.PenaltyAmount;
                // Lost cue is permanently removed from the fleet, not returned to stock.
                break;
            default:
                return BadRequest("Kết quả trả cơ không hợp lệ.");
        }

        await _db.SaveChangesAsync();
        await transaction.CommitAsync();

        var dto = ToRentalDto(rental);
        await _hub.Clients.All.SendAsync("CueRentalChanged", dto);

        return dto;
    }
}
