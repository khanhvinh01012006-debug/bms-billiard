using BMS.API.Data;
using BMS.API.Hubs;
using BMS.API.Models;
using BMS.Shared.Dtos;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;

namespace BMS.API.Controllers;

// EP-08: US-040 Gửi lời mời ghép trận
[ApiController]
[Route("api/match-requests")]
public class MatchRequestsController : ControllerBase
{
    private readonly BmsDbContext _db;
    private readonly IHubContext<BmsHub> _hub;

    public MatchRequestsController(BmsDbContext db, IHubContext<BmsHub> hub)
    {
        _db = db;
        _hub = hub;
    }

    private static MatchRequestDto ToDto(MatchRequestEntity m) => new(
        m.Id,
        m.RequesterId,
        m.Requester?.DisplayName ?? string.Empty,
        m.TargetPlayerId,
        m.TargetPlayer?.DisplayName,
        m.Status,
        m.CreatedAtUtc);

    [HttpGet]
    public async Task<ActionResult<List<MatchRequestDto>>> GetAll()
    {
        var requests = await _db.MatchRequests
            .Include(m => m.Requester)
            .Include(m => m.TargetPlayer)
            .OrderByDescending(m => m.CreatedAtUtc)
            .ToListAsync();

        return requests.Select(ToDto).ToList();
    }

    // US-040: Gửi lời mời ghép trận
    [HttpPost]
    public async Task<ActionResult<MatchRequestDto>> Create(CreateMatchRequestDto request)
    {
        var requester = await _db.PlayerProfiles.FindAsync(request.RequesterId);
        if (requester is null) return NotFound("Không tìm thấy người gửi lời mời.");

        if (request.TargetPlayerId is int targetId)
        {
            var target = await _db.PlayerProfiles.FindAsync(targetId);
            if (target is null) return NotFound("Không tìm thấy người chơi được mời.");
            if (targetId == request.RequesterId) return BadRequest("Không thể gửi lời mời cho chính mình.");
        }

        var matchRequest = new MatchRequestEntity
        {
            RequesterId = request.RequesterId,
            TargetPlayerId = request.TargetPlayerId,
            Status = "Pending",
            CreatedAtUtc = DateTime.UtcNow
        };

        _db.MatchRequests.Add(matchRequest);
        await _db.SaveChangesAsync();

        await _db.Entry(matchRequest).Reference(m => m.Requester).LoadAsync();
        if (matchRequest.TargetPlayerId is not null)
        {
            await _db.Entry(matchRequest).Reference(m => m.TargetPlayer).LoadAsync();
        }

        var dto = ToDto(matchRequest);
        await _hub.Clients.All.SendAsync("MatchRequestChanged", dto);

        return CreatedAtAction(nameof(GetById), new { id = matchRequest.Id }, dto);
    }

    [HttpGet("{id:int}")]
    public async Task<ActionResult<MatchRequestDto>> GetById(int id)
    {
        var matchRequest = await _db.MatchRequests
            .Include(m => m.Requester)
            .Include(m => m.TargetPlayer)
            .FirstOrDefaultAsync(m => m.Id == id);

        if (matchRequest is null) return NotFound();
        return ToDto(matchRequest);
    }

    public record RespondMatchRequestDto(bool Accept);

    // Chấp nhận/từ chối lời mời ghép trận
    [HttpPut("{id:int}")]
    public async Task<ActionResult<MatchRequestDto>> Respond(int id, RespondMatchRequestDto request)
    {
        var matchRequest = await _db.MatchRequests
            .Include(m => m.Requester)
            .Include(m => m.TargetPlayer)
            .FirstOrDefaultAsync(m => m.Id == id);

        if (matchRequest is null) return NotFound();
        if (matchRequest.Status != "Pending") return Conflict("Lời mời này đã được xử lý.");

        matchRequest.Status = request.Accept ? "Accepted" : "Rejected";
        await _db.SaveChangesAsync();

        var dto = ToDto(matchRequest);
        await _hub.Clients.All.SendAsync("MatchRequestChanged", dto);

        return dto;
    }
}
