using BMS.API.Data;
using BMS.API.Models;
using BMS.Shared.Dtos;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace BMS.API.Controllers;

// Đợt 2: Chợ trao đổi cơ (Trade) giữa người chơi. TODO đợt sau: kiểm duyệt tin đăng
// (Admin duyệt trước khi hiển thị công khai) - hiện tại tin lên thẳng không cần duyệt.
[ApiController]
[Route("api/cue-listings")]
public class CueListingsController : ControllerBase
{
    private readonly BmsDbContext _db;

    public CueListingsController(BmsDbContext db)
    {
        _db = db;
    }

    private static CueListingDto ToDto(CueListingEntity l) => new(
        l.Id, l.SellerAccountId, l.SellerAccount?.FullName ?? string.Empty, l.SellerAccount?.PhoneNumber,
        l.Title, l.Description, l.ImageUrl, l.DesiredTrade, l.Price, l.Status, l.CreatedAtUtc);

    // Danh sách tin đang mở (Status = Open), công khai không cần đăng nhập.
    [HttpGet]
    public async Task<ActionResult<List<CueListingDto>>> GetOpenListings()
    {
        var list = await _db.CueListings
            .Include(l => l.SellerAccount)
            .Where(l => l.Status == "Open")
            .OrderByDescending(l => l.CreatedAtUtc)
            .ToListAsync();

        return list.Select(ToDto).ToList();
    }

    [Authorize]
    [HttpGet("mine")]
    public async Task<ActionResult<List<CueListingDto>>> GetMine()
    {
        var accountId = GetCurrentAccountId();
        if (accountId is null) return Unauthorized();

        var list = await _db.CueListings
            .Include(l => l.SellerAccount)
            .Where(l => l.SellerAccountId == accountId)
            .OrderByDescending(l => l.CreatedAtUtc)
            .ToListAsync();

        return list.Select(ToDto).ToList();
    }

    // Bất kỳ ai đã đăng nhập đều đăng được tin.
    [Authorize]
    [HttpPost]
    public async Task<ActionResult<CueListingDto>> Create(CreateCueListingRequestDto request)
    {
        if (string.IsNullOrWhiteSpace(request.Title))
        {
            return BadRequest("Tiêu đề tin đăng không được để trống.");
        }

        var accountId = GetCurrentAccountId();
        if (accountId is null) return Unauthorized();

        var listing = new CueListingEntity
        {
            SellerAccountId = accountId.Value,
            Title = request.Title,
            Description = request.Description,
            ImageUrl = request.ImageUrl,
            DesiredTrade = request.DesiredTrade,
            Price = request.Price,
            CreatedAtUtc = DateTime.UtcNow,
            Status = "Open"
        };

        _db.CueListings.Add(listing);
        await _db.SaveChangesAsync();

        var seller = await _db.Accounts.FindAsync(accountId.Value);
        listing.SellerAccount = seller;

        return CreatedAtAction(nameof(GetOpenListings), null, ToDto(listing));
    }

    // Đóng tin của chính mình (so khớp AccountId từ JWT).
    [Authorize]
    [HttpPut("{id:int}/close")]
    public async Task<IActionResult> Close(int id)
    {
        var accountId = GetCurrentAccountId();
        if (accountId is null) return Unauthorized();

        var listing = await _db.CueListings.FirstOrDefaultAsync(l => l.Id == id);
        if (listing is null) return NotFound();

        if (listing.SellerAccountId != accountId)
        {
            return Forbid();
        }

        listing.Status = "Closed";
        await _db.SaveChangesAsync();

        return NoContent();
    }

    private int? GetCurrentAccountId()
    {
        var claim = User.FindFirst("userId")?.Value;
        return int.TryParse(claim, out var id) ? id : null;
    }
}
