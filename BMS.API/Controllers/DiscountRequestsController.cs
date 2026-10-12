using BMS.API.Data;
using BMS.API.Hubs;
using BMS.API.Models;
using BMS.Shared.Dtos;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;

namespace BMS.API.Controllers;

// Đợt 9: Yêu cầu giảm giá khi thanh toán - tự duyệt trong hạn mức, vượt hạn mức thì chờ Admin.
[ApiController]
[Route("api/discount-requests")]
[Authorize]
public class DiscountRequestsController : ControllerBase
{
    private readonly BmsDbContext _db;
    private readonly IHubContext<BmsHub> _hub;

    public DiscountRequestsController(BmsDbContext db, IHubContext<BmsHub> hub)
    {
        _db = db;
        _hub = hub;
    }

    private static DiscountRequestDto ToDto(DiscountRequestEntity d) => new(
        d.Id, d.SessionId, d.TableName, d.StaffName, d.OriginalAmount, d.DiscountPercent,
        d.DiscountAmount, d.Reason, d.Status, d.IsApplied, d.CreatedAtUtc, d.DecidedByName, d.DecidedAtUtc);

    [Authorize(Roles = "Staff,Admin")]
    [HttpPost]
    public async Task<ActionResult<DiscountRequestDto>> Create(CreateDiscountRequestDto request)
    {
        if (request.DiscountPercent <= 0 || request.DiscountPercent > 100)
        {
            return BadRequest("Phần trăm giảm giá phải trong khoảng 0-100.");
        }

        var session = await _db.PlayingSessions.Include(s => s.Table).FirstOrDefaultAsync(s => s.Id == request.SessionId);
        if (session is null) return NotFound("Không tìm thấy phiên chơi.");
        if (session.Status != "PendingPayment")
        {
            return Conflict("Phiên chơi phải ở trạng thái chờ thanh toán mới tạo được yêu cầu giảm giá.");
        }

        var accountIdClaim = User.FindFirst("userId")?.Value;
        int.TryParse(accountIdClaim, out var accountId);
        var fullName = User.FindFirst("fullName")?.Value ?? "Nhân viên";

        var staffAccount = await _db.Accounts.FindAsync(accountId);
        var limit = staffAccount?.DiscountLimitPercent ?? 0m;

        var originalAmount = session.TotalAmount ?? 0m;
        var discountAmount = Math.Round(originalAmount * request.DiscountPercent / 100m, 0, MidpointRounding.AwayFromZero);
        var autoApproved = request.DiscountPercent <= limit;

        var discountRequest = new DiscountRequestEntity
        {
            SessionId = session.Id,
            TableName = session.Table?.Name ?? string.Empty,
            StaffAccountId = accountId,
            StaffName = fullName,
            OriginalAmount = originalAmount,
            DiscountPercent = request.DiscountPercent,
            DiscountAmount = discountAmount,
            Reason = request.Reason,
            Status = autoApproved ? "Approved" : "PendingApproval",
            CreatedAtUtc = DateTime.UtcNow,
            DecidedByName = autoApproved ? "Tự động (trong hạn mức)" : null,
            DecidedAtUtc = autoApproved ? DateTime.UtcNow : null
        };

        _db.DiscountRequests.Add(discountRequest);
        await _db.SaveChangesAsync();

        var dto = ToDto(discountRequest);
        if (!autoApproved)
        {
            await _hub.Clients.All.SendAsync("DiscountRequestCreated", dto);
        }

        return dto;
    }

    [Authorize(Roles = "Admin")]
    [HttpGet]
    public async Task<ActionResult<List<DiscountRequestDto>>> GetAll([FromQuery] string? status)
    {
        var query = _db.DiscountRequests.AsQueryable();
        if (!string.IsNullOrWhiteSpace(status)) query = query.Where(d => d.Status == status);
        var list = await query.OrderByDescending(d => d.CreatedAtUtc).ToListAsync();
        return list.Select(ToDto).ToList();
    }

    // Để nhân viên đứng tại bàn chờ Admin duyệt có thể tự kiểm tra kết quả (polling đơn giản).
    [Authorize(Roles = "Staff,Admin")]
    [HttpGet("by-session/{sessionId:int}")]
    public async Task<ActionResult<DiscountRequestDto>> GetLatestForSession(int sessionId)
    {
        var discountRequest = await _db.DiscountRequests
            .Where(d => d.SessionId == sessionId && !d.IsApplied)
            .OrderByDescending(d => d.CreatedAtUtc)
            .FirstOrDefaultAsync();

        if (discountRequest is null) return NoContent();
        return ToDto(discountRequest);
    }

    [Authorize(Roles = "Admin")]
    [HttpPost("{id:int}/approve")]
    public async Task<ActionResult<DiscountRequestDto>> Approve(int id)
    {
        var discountRequest = await _db.DiscountRequests.FindAsync(id);
        if (discountRequest is null) return NotFound();
        if (discountRequest.Status != "PendingApproval") return Conflict("Yêu cầu này đã được xử lý.");

        discountRequest.Status = "Approved";
        discountRequest.DecidedByName = User.FindFirst("fullName")?.Value ?? "Admin";
        discountRequest.DecidedAtUtc = DateTime.UtcNow;
        await _db.SaveChangesAsync();

        var dto = ToDto(discountRequest);
        await _hub.Clients.All.SendAsync("DiscountRequestDecided", dto);
        return dto;
    }

    [Authorize(Roles = "Admin")]
    [HttpPost("{id:int}/reject")]
    public async Task<ActionResult<DiscountRequestDto>> Reject(int id)
    {
        var discountRequest = await _db.DiscountRequests.FindAsync(id);
        if (discountRequest is null) return NotFound();
        if (discountRequest.Status != "PendingApproval") return Conflict("Yêu cầu này đã được xử lý.");

        discountRequest.Status = "Rejected";
        discountRequest.DecidedByName = User.FindFirst("fullName")?.Value ?? "Admin";
        discountRequest.DecidedAtUtc = DateTime.UtcNow;
        await _db.SaveChangesAsync();

        var dto = ToDto(discountRequest);
        await _hub.Clients.All.SendAsync("DiscountRequestDecided", dto);
        return dto;
    }
}
