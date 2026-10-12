using BMS.API.Data;
using BMS.API.Hubs;
using BMS.API.Models;
using BMS.Shared.Dtos;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;

namespace BMS.API.Controllers;

// Đợt 2: Quản lý Giải đấu. Admin tạo/sửa/xóa, mọi người đã đăng nhập xem được danh
// sách, Player tự đăng ký/hủy đăng ký tham gia. TODO đợt sau: xếp lịch thi đấu, bốc
// thăm, kết quả trận đấu trong giải - hiện tại chỉ quản lý thông tin giải + đăng ký.
[ApiController]
[Route("api/tournaments")]
[Authorize]
public class TournamentsController : ControllerBase
{
    private readonly BmsDbContext _db;
    private readonly IHubContext<BmsHub> _hub;

    public TournamentsController(BmsDbContext db, IHubContext<BmsHub> hub)
    {
        _db = db;
        _hub = hub;
    }

    private static TournamentDto ToDto(TournamentEntity t)
    {
        var registeredCount = t.Registrations?.Count(r => r.Status == "Registered") ?? 0;
        var isFull = t.MaxParticipants is not null && registeredCount >= t.MaxParticipants.Value;
        return new TournamentDto(
            t.Id, t.Name, t.Description, t.StartDateUtc, t.EndDateUtc, t.EntryFee,
            t.MaxParticipants, t.Status, t.ImageUrl, t.CreatedAtUtc, registeredCount, isFull);
    }

    private static TournamentRegistrationDto ToRegistrationDto(TournamentRegistrationEntity r) => new(
        r.Id, r.TournamentId, r.AccountId, r.PlayerName, r.PhoneNumber, r.RegisteredAtUtc, r.Status);

    // Khách chưa đăng nhập cũng xem được danh sách giải; giải "Nháp" (Admin đang soạn) chỉ
    // Nhân viên / Admin thấy.
    [AllowAnonymous]
    [HttpGet]
    public async Task<ActionResult<List<TournamentDto>>> GetAll()
    {
        var canSeeDrafts = User.IsInRole("Staff") || User.IsInRole("Admin");
        var list = await _db.Tournaments
            .Include(t => t.Registrations)
            .Where(t => canSeeDrafts || t.Status != "Draft")
            .OrderByDescending(t => t.StartDateUtc)
            .ToListAsync();

        return list.Select(ToDto).ToList();
    }

    [HttpGet("{id:int}")]
    public async Task<ActionResult<TournamentDto>> GetById(int id)
    {
        var tournament = await _db.Tournaments
            .Include(t => t.Registrations)
            .FirstOrDefaultAsync(t => t.Id == id);

        if (tournament is null) return NotFound();
        return ToDto(tournament);
    }

    [Authorize(Roles = "Admin")]
    [HttpPost]
    public async Task<ActionResult<TournamentDto>> Create(UpsertTournamentRequestDto request)
    {
        if (string.IsNullOrWhiteSpace(request.Name))
        {
            return BadRequest("Tên giải đấu không được để trống.");
        }

        var tournament = new TournamentEntity
        {
            Name = request.Name,
            Description = request.Description,
            StartDateUtc = request.StartDateUtc,
            EndDateUtc = request.EndDateUtc,
            EntryFee = request.EntryFee,
            MaxParticipants = request.MaxParticipants,
            Status = string.IsNullOrWhiteSpace(request.Status) ? "Draft" : request.Status,
            ImageUrl = request.ImageUrl,
            CreatedAtUtc = DateTime.UtcNow
        };

        _db.Tournaments.Add(tournament);
        await _db.SaveChangesAsync();

        var dto = ToDto(tournament);
        await _hub.Clients.All.SendAsync("TournamentUpdated", dto);

        return CreatedAtAction(nameof(GetById), new { id = tournament.Id }, dto);
    }

    [Authorize(Roles = "Admin")]
    [HttpPut("{id:int}")]
    public async Task<IActionResult> Update(int id, UpsertTournamentRequestDto request)
    {
        var tournament = await _db.Tournaments
            .Include(t => t.Registrations)
            .FirstOrDefaultAsync(t => t.Id == id);
        if (tournament is null) return NotFound();

        tournament.Name = request.Name;
        tournament.Description = request.Description;
        tournament.StartDateUtc = request.StartDateUtc;
        tournament.EndDateUtc = request.EndDateUtc;
        tournament.EntryFee = request.EntryFee;
        tournament.MaxParticipants = request.MaxParticipants;
        tournament.Status = request.Status;
        tournament.ImageUrl = request.ImageUrl;

        await _db.SaveChangesAsync();

        var dto = ToDto(tournament);
        await _hub.Clients.All.SendAsync("TournamentUpdated", dto);

        return NoContent();
    }

    [Authorize(Roles = "Admin")]
    [HttpDelete("{id:int}")]
    public async Task<IActionResult> Delete(int id)
    {
        var tournament = await _db.Tournaments.FindAsync(id);
        if (tournament is null) return NotFound();

        _db.Tournaments.Remove(tournament);
        await _db.SaveChangesAsync();

        await _hub.Clients.All.SendAsync("TournamentUpdated", new { DeletedId = id });

        return NoContent();
    }

    // Admin xem danh sách người đã đăng ký một giải đấu.
    [Authorize(Roles = "Admin")]
    [HttpGet("{id:int}/registrations")]
    public async Task<ActionResult<List<TournamentRegistrationDto>>> GetRegistrations(int id)
    {
        var tournament = await _db.Tournaments.FindAsync(id);
        if (tournament is null) return NotFound();

        var list = await _db.TournamentRegistrations
            .Where(r => r.TournamentId == id)
            .OrderBy(r => r.RegisteredAtUtc)
            .ToListAsync();

        return list.Select(ToRegistrationDto).ToList();
    }

    // Player kiểm tra xem chính mình đã đăng ký giải đấu này chưa (để hiện đúng nút
    // "Đăng ký"/"Hủy đăng ký" trên PlayerWeb).
    [HttpGet("{id:int}/my-registration")]
    public async Task<ActionResult<bool>> GetMyRegistrationStatus(int id)
    {
        var accountId = GetCurrentAccountId();
        if (accountId is null) return Unauthorized();

        var isRegistered = await _db.TournamentRegistrations
            .AnyAsync(r => r.TournamentId == id && r.AccountId == accountId && r.Status == "Registered");

        return isRegistered;
    }

    // Player tự đăng ký tham gia giải đấu.
    [HttpPost("{id:int}/register")]
    public async Task<ActionResult<TournamentRegistrationDto>> Register(int id, RegisterForTournamentRequestDto request)
    {
        var accountId = GetCurrentAccountId();
        if (accountId is null) return Unauthorized();

        var tournament = await _db.Tournaments
            .Include(t => t.Registrations)
            .FirstOrDefaultAsync(t => t.Id == id);
        if (tournament is null) return NotFound();

        if (tournament.Status != "Open")
        {
            return BadRequest("Giải đấu này hiện không mở đăng ký.");
        }

        var registeredCount = tournament.Registrations.Count(r => r.Status == "Registered");
        if (tournament.MaxParticipants is not null && registeredCount >= tournament.MaxParticipants.Value)
        {
            return BadRequest("Giải đấu đã đủ số lượng người tham gia.");
        }

        var alreadyRegistered = tournament.Registrations.Any(r => r.AccountId == accountId && r.Status == "Registered");
        if (alreadyRegistered)
        {
            return BadRequest("Bạn đã đăng ký giải đấu này rồi.");
        }

        var fullName = User.FindFirst("fullName")?.Value ?? string.Empty;

        var registration = new TournamentRegistrationEntity
        {
            TournamentId = id,
            AccountId = accountId.Value,
            PlayerName = fullName,
            PhoneNumber = request.PhoneNumber,
            RegisteredAtUtc = DateTime.UtcNow,
            Status = "Registered"
        };

        _db.TournamentRegistrations.Add(registration);
        await _db.SaveChangesAsync();

        tournament.Registrations.Add(registration);
        var dto = ToDto(tournament);
        await _hub.Clients.All.SendAsync("TournamentUpdated", dto);

        return CreatedAtAction(nameof(GetById), new { id }, ToRegistrationDto(registration));
    }

    // Player tự hủy đăng ký của chính mình.
    [HttpPost("{id:int}/unregister")]
    public async Task<IActionResult> Unregister(int id)
    {
        var accountId = GetCurrentAccountId();
        if (accountId is null) return Unauthorized();

        var registration = await _db.TournamentRegistrations
            .FirstOrDefaultAsync(r => r.TournamentId == id && r.AccountId == accountId && r.Status == "Registered");
        if (registration is null) return NotFound("Bạn chưa đăng ký giải đấu này.");

        registration.Status = "Cancelled";
        await _db.SaveChangesAsync();

        var tournament = await _db.Tournaments
            .Include(t => t.Registrations)
            .FirstOrDefaultAsync(t => t.Id == id);

        if (tournament is not null)
        {
            var dto = ToDto(tournament);
            await _hub.Clients.All.SendAsync("TournamentUpdated", dto);
        }

        return NoContent();
    }

    private int? GetCurrentAccountId()
    {
        var claim = User.FindFirst("userId")?.Value;
        return int.TryParse(claim, out var id) ? id : null;
    }

    private static TournamentMatchDto ToMatchDto(TournamentMatchEntity m) => new(
        m.Id, m.TournamentId, m.Round, m.SlotInRound,
        m.Player1RegistrationId, m.Player1Name, m.Player2RegistrationId, m.Player2Name,
        m.WinnerRegistrationId, m.WinnerName, m.Status);

    // Đợt 9: Admin bốc thăm & tạo bảng đấu loại trực tiếp từ danh sách người đã đăng ký
    // (Status "Registered"), theo thứ tự đăng ký trước. Nếu số người không phải lũy thừa
    // của 2, các ô trống ở vòng 1 là "bye" - người có cặp đấu trống tự động thắng ngay.
    // Tạo lại từ đầu nếu đã có bảng đấu cũ (xóa hết match cũ của giải trước khi tạo mới).
    [Authorize(Roles = "Admin")]
    [HttpPost("{id:int}/generate-bracket")]
    public async Task<ActionResult<List<TournamentMatchDto>>> GenerateBracket(int id)
    {
        var tournament = await _db.Tournaments
            .Include(t => t.Registrations)
            .FirstOrDefaultAsync(t => t.Id == id);
        if (tournament is null) return NotFound();

        var players = tournament.Registrations
            .Where(r => r.Status == "Registered")
            .OrderBy(r => r.RegisteredAtUtc)
            .Select(r => new { r.Id, r.PlayerName })
            .ToList();

        if (players.Count < 2)
        {
            return BadRequest("Cần ít nhất 2 người đăng ký để tạo bảng đấu.");
        }

        var oldMatches = await _db.TournamentMatches.Where(m => m.TournamentId == id).ToListAsync();
        _db.TournamentMatches.RemoveRange(oldMatches);

        // Kích thước bảng đấu = lũy thừa của 2 nhỏ nhất >= số người chơi.
        var bracketSize = 1;
        while (bracketSize < players.Count) bracketSize *= 2;

        var totalRounds = (int)Math.Log2(bracketSize);
        var allMatches = new List<TournamentMatchEntity>();

        // Vòng 1: ghép cặp lần lượt, các ô dư (so với số người thật) để trống (bye).
        var round1Count = bracketSize / 2;
        var round1 = new List<TournamentMatchEntity>();
        for (var slot = 0; slot < round1Count; slot++)
        {
            var p1Index = slot * 2;
            var p2Index = slot * 2 + 1;
            var p1 = p1Index < players.Count ? players[p1Index] : null;
            var p2 = p2Index < players.Count ? players[p2Index] : null;

            var match = new TournamentMatchEntity
            {
                TournamentId = id,
                Round = 1,
                SlotInRound = slot,
                Player1RegistrationId = p1?.Id,
                Player1Name = p1?.PlayerName,
                Player2RegistrationId = p2?.Id,
                Player2Name = p2?.PlayerName
            };

            // Bye: chỉ có 1 trong 2 người (p2 null vì số người lẻ ở ranh giới cuối) -> người
            // đó tự động thắng ngay, không cần chờ nhập kết quả thủ công.
            if (p1 is not null && p2 is null)
            {
                match.WinnerRegistrationId = p1.Id;
                match.WinnerName = p1.PlayerName;
                match.Status = "Completed";
            }

            round1.Add(match);
        }
        allMatches.AddRange(round1);

        // Các vòng sau: tạo khung trống trước (sẽ được điền dần khi vòng trước hoàn tất),
        // trừ trường hợp bye ở vòng 1 thì điền luôn người thắng vào vòng 2 ngay tại đây.
        var previousRound = round1;
        for (var round = 2; round <= totalRounds; round++)
        {
            var matchCount = bracketSize / (int)Math.Pow(2, round);
            var currentRound = new List<TournamentMatchEntity>();
            for (var slot = 0; slot < matchCount; slot++)
            {
                currentRound.Add(new TournamentMatchEntity
                {
                    TournamentId = id,
                    Round = round,
                    SlotInRound = slot
                });
            }

            // Lan truyền các bye từ vòng trước vào vòng này ngay lúc tạo bảng.
            foreach (var prevMatch in previousRound.Where(m => m.Status == "Completed"))
            {
                var nextSlot = prevMatch.SlotInRound / 2;
                var next = currentRound[nextSlot];
                if (prevMatch.SlotInRound % 2 == 0)
                {
                    next.Player1RegistrationId = prevMatch.WinnerRegistrationId;
                    next.Player1Name = prevMatch.WinnerName;
                }
                else
                {
                    next.Player2RegistrationId = prevMatch.WinnerRegistrationId;
                    next.Player2Name = prevMatch.WinnerName;
                }
            }

            allMatches.AddRange(currentRound);
            previousRound = currentRound;
        }

        _db.TournamentMatches.AddRange(allMatches);
        tournament.Status = "InProgress";
        await _db.SaveChangesAsync();

        var dto = ToDto(tournament);
        await _hub.Clients.All.SendAsync("TournamentUpdated", dto);
        await _hub.Clients.All.SendAsync("BracketUpdated", id);

        return allMatches.Select(ToMatchDto).ToList();
    }

    [AllowAnonymous]
    [HttpGet("{id:int}/bracket")]
    public async Task<ActionResult<List<TournamentMatchDto>>> GetBracket(int id)
    {
        var matches = await _db.TournamentMatches
            .Where(m => m.TournamentId == id)
            .OrderBy(m => m.Round).ThenBy(m => m.SlotInRound)
            .ToListAsync();

        return matches.Select(ToMatchDto).ToList();
    }

    // Đợt 9: Admin ghi nhận người thắng một trận - tự động điền người thắng vào trận
    // tương ứng ở vòng kế tiếp (nếu có); nếu đây là trận chung kết (không còn vòng sau) thì
    // tự động đánh dấu giải đấu Status = "Completed".
    [Authorize(Roles = "Admin")]
    [HttpPost("matches/{matchId:int}/result")]
    public async Task<ActionResult<TournamentMatchDto>> SetMatchResult(int matchId, SetMatchResultRequestDto request)
    {
        var match = await _db.TournamentMatches.FirstOrDefaultAsync(m => m.Id == matchId);
        if (match is null) return NotFound();

        string? winnerName = null;
        if (request.WinnerRegistrationId == match.Player1RegistrationId) winnerName = match.Player1Name;
        else if (request.WinnerRegistrationId == match.Player2RegistrationId) winnerName = match.Player2Name;
        else return BadRequest("Người thắng phải là một trong hai người chơi của trận này.");

        match.WinnerRegistrationId = request.WinnerRegistrationId;
        match.WinnerName = winnerName;
        match.Status = "Completed";

        var nextMatch = await _db.TournamentMatches.FirstOrDefaultAsync(m =>
            m.TournamentId == match.TournamentId && m.Round == match.Round + 1 && m.SlotInRound == match.SlotInRound / 2);

        if (nextMatch is not null)
        {
            if (match.SlotInRound % 2 == 0)
            {
                nextMatch.Player1RegistrationId = match.WinnerRegistrationId;
                nextMatch.Player1Name = match.WinnerName;
            }
            else
            {
                nextMatch.Player2RegistrationId = match.WinnerRegistrationId;
                nextMatch.Player2Name = match.WinnerName;
            }
        }
        else
        {
            // Không còn vòng sau -> đây là chung kết, giải đấu kết thúc.
            var tournament = await _db.Tournaments.FindAsync(match.TournamentId);
            if (tournament is not null) tournament.Status = "Completed";
        }

        await _db.SaveChangesAsync();

        await _hub.Clients.All.SendAsync("BracketUpdated", match.TournamentId);

        return ToMatchDto(match);
    }
}
