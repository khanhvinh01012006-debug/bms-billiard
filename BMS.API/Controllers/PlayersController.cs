using BMS.API.Data;
using BMS.Shared.Dtos;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace BMS.API.Controllers;

// EP-08: US-039 Ghép trận theo ELO
[ApiController]
[Route("api/players")]
public class PlayersController : ControllerBase
{
    private readonly BmsDbContext _db;

    public PlayersController(BmsDbContext db)
    {
        _db = db;
    }

    private static PlayerProfileDto ToDto(Models.PlayerProfileEntity p) =>
        new(p.Id, p.DisplayName, p.EloRating, p.AvatarUrl);

    [HttpGet]
    public async Task<ActionResult<List<PlayerProfileDto>>> GetAll()
    {
        var players = await _db.PlayerProfiles.OrderByDescending(p => p.EloRating).ToListAsync();
        return players.Select(ToDto).ToList();
    }

    [HttpGet("{id:int}")]
    public async Task<ActionResult<PlayerProfileDto>> GetById(int id)
    {
        var player = await _db.PlayerProfiles.FindAsync(id);
        if (player is null) return NotFound();
        return ToDto(player);
    }

    // US-039: Gợi ý ghép trận theo ELO gần nhất với người chơi hiện tại.
    [HttpGet("{id:int}/suggestions")]
    public async Task<ActionResult<List<PlayerProfileDto>>> GetSuggestions(int id, [FromQuery] int take = 5)
    {
        var me = await _db.PlayerProfiles.FindAsync(id);
        if (me is null) return NotFound();

        var suggestions = await _db.PlayerProfiles
            .Where(p => p.Id != id)
            .OrderBy(p => Math.Abs(p.EloRating - me.EloRating))
            .Take(take)
            .ToListAsync();

        return suggestions.Select(ToDto).ToList();
    }
}
