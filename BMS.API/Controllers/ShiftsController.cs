using BMS.API.Data;
using BMS.API.Models;
using BMS.Shared.Dtos;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace BMS.API.Controllers;

// Đợt 4: Bàn giao ca làm việc - nhân viên bắt đầu ca (ghi quỹ đầu ca), kết thúc ca
// (ghi quỹ cuối ca + ghi chú bàn giao cho ca sau), Admin xem lại toàn bộ lịch sử ca.
[ApiController]
[Route("api/shifts")]
[Authorize]
public class ShiftsController : ControllerBase
{
    private readonly BmsDbContext _db;

    public ShiftsController(BmsDbContext db)
    {
        _db = db;
    }

    private int? GetCurrentAccountId()
    {
        var claim = User.FindFirst("userId")?.Value;
        return int.TryParse(claim, out var id) ? id : null;
    }

    private static ShiftDto ToDto(ShiftEntity s) => new(
        s.Id, s.StaffAccountId, s.StaffName, s.StartedAtUtc, s.OpeningCashAmount,
        s.EndedAtUtc, s.ClosingCashAmount, s.HandoverNote, s.Status);

    // GET api/shifts/me/active - ca đang Active của người gọi, hoặc 204 nếu không có.
    [HttpGet("me/active")]
    public async Task<ActionResult<ShiftDto>> GetMyActiveShift()
    {
        var accountId = GetCurrentAccountId();
        if (accountId is null) return Unauthorized();

        var shift = await _db.Shifts
            .Where(s => s.StaffAccountId == accountId && s.Status == "Active")
            .OrderByDescending(s => s.StartedAtUtc)
            .FirstOrDefaultAsync();

        if (shift is null) return NoContent();
        return ToDto(shift);
    }

    // POST api/shifts/start - bắt đầu ca mới (ghi quỹ đầu ca).
    [Authorize(Roles = "Staff,Admin")]
    [HttpPost("start")]
    public async Task<ActionResult<ShiftDto>> Start(StartShiftRequestDto request)
    {
        var accountId = GetCurrentAccountId();
        if (accountId is null) return Unauthorized();

        var alreadyActive = await _db.Shifts.AnyAsync(s => s.StaffAccountId == accountId && s.Status == "Active");
        if (alreadyActive)
        {
            return BadRequest("Bạn đang có một ca làm việc chưa kết thúc.");
        }

        var fullName = User.FindFirst("fullName")?.Value ?? string.Empty;

        var shift = new ShiftEntity
        {
            StaffAccountId = accountId.Value,
            StaffName = fullName,
            StartedAtUtc = DateTime.UtcNow,
            OpeningCashAmount = request.OpeningCashAmount,
            Status = "Active"
        };

        _db.Shifts.Add(shift);

        // Gộp hành động cho nhân viên: "Bắt đầu ca" cũng tự động chấm công vào, trừ khi họ
        // đã có bản ghi chấm công đang mở (tránh tạo 2 bản ghi chấm công chồng nhau).
        var alreadyClockedIn = await _db.Attendances.AnyAsync(a => a.StaffAccountId == accountId && a.ClockOutAtUtc == null);
        if (!alreadyClockedIn)
        {
            _db.Attendances.Add(new AttendanceEntity
            {
                StaffAccountId = accountId.Value,
                StaffName = fullName,
                ClockInAtUtc = DateTime.UtcNow,
                Note = "Tự động theo Bắt đầu ca"
            });
        }

        await _db.SaveChangesAsync();

        return ToDto(shift);
    }

    // POST api/shifts/{id}/end - kết thúc ca (ghi quỹ cuối ca + ghi chú bàn giao).
    [Authorize(Roles = "Staff,Admin")]
    [HttpPost("{id:int}/end")]
    public async Task<ActionResult<ShiftDto>> End(int id, EndShiftRequestDto request)
    {
        var accountId = GetCurrentAccountId();
        if (accountId is null) return Unauthorized();

        var shift = await _db.Shifts.FirstOrDefaultAsync(s => s.Id == id);
        if (shift is null) return NotFound();

        var isAdmin = User.FindFirst(System.Security.Claims.ClaimTypes.Role)?.Value == "Admin";
        if (shift.StaffAccountId != accountId && !isAdmin)
        {
            return Forbid();
        }

        if (shift.Status != "Active")
        {
            return BadRequest("Ca làm việc này đã kết thúc.");
        }

        shift.EndedAtUtc = DateTime.UtcNow;
        shift.ClosingCashAmount = request.ClosingCashAmount;
        shift.HandoverNote = request.HandoverNote;
        shift.Status = "Closed";

        // Gộp hành động cho nhân viên: "Kết thúc ca" cũng tự động chấm công ra cho bản ghi
        // chấm công đang mở của CHÍNH nhân viên đó (nếu có) - dùng ghi chú bàn giao làm ghi
        // chú chấm công. Nếu không có bản ghi nào đang mở (ví dụ ca được bắt đầu từ trước
        // khi có tính năng này), bỏ qua phần này, không làm fail việc kết thúc ca.
        var openAttendance = await _db.Attendances
            .Where(a => a.StaffAccountId == shift.StaffAccountId && a.ClockOutAtUtc == null)
            .OrderByDescending(a => a.ClockInAtUtc)
            .FirstOrDefaultAsync();

        if (openAttendance is not null)
        {
            openAttendance.ClockOutAtUtc = DateTime.UtcNow;
            if (!string.IsNullOrWhiteSpace(request.HandoverNote))
            {
                openAttendance.Note = request.HandoverNote;
            }
        }

        await _db.SaveChangesAsync();

        return ToDto(shift);
    }

    // GET api/shifts - Admin: lịch sử toàn bộ ca làm việc, lọc theo staffAccountId (tùy chọn).
    [Authorize(Roles = "Admin")]
    [HttpGet]
    public async Task<ActionResult<List<ShiftDto>>> GetAll([FromQuery] int? staffAccountId)
    {
        var query = _db.Shifts.AsQueryable();

        if (staffAccountId is int id)
        {
            query = query.Where(s => s.StaffAccountId == id);
        }

        var list = await query.OrderByDescending(s => s.StartedAtUtc).ToListAsync();
        return list.Select(ToDto).ToList();
    }

    // Đợt 9: Dashboard - phát hiện lệch quỹ tiền mặt cuối ca so với dự kiến (quỹ đầu ca + tổng
    // các giao dịch thanh toán tiền mặt do chính nhân viên đó xử lý trong khoảng thời gian ca).
    [Authorize(Roles = "Admin")]
    [HttpGet("cash-discrepancies")]
    public async Task<ActionResult<List<ShiftCashDiscrepancyDto>>> GetCashDiscrepancies()
    {
        var since = DateTime.UtcNow.AddHours(-48);
        var closedShifts = await _db.Shifts
            .Where(s => s.Status == "Closed" && s.EndedAtUtc != null && s.EndedAtUtc >= since)
            .ToListAsync();

        var results = new List<ShiftCashDiscrepancyDto>();
        foreach (var shift in closedShifts)
        {
            var cashTotal = await _db.Transactions
                .Where(t => t.ProcessedByAccountId == shift.StaffAccountId
                    && t.Method == "Cash" && t.Type == "Payment" && t.Status == "Approved"
                    && t.CreatedAtUtc >= shift.StartedAtUtc && t.CreatedAtUtc <= shift.EndedAtUtc)
                .SumAsync(t => (decimal?)t.Amount) ?? 0m;

            var expected = shift.OpeningCashAmount + cashTotal;
            var actual = shift.ClosingCashAmount ?? 0m;
            var discrepancy = actual - expected;

            if (Math.Abs(discrepancy) > 10000m)
            {
                results.Add(new ShiftCashDiscrepancyDto(
                    shift.Id, shift.StaffName, shift.StartedAtUtc, shift.EndedAtUtc,
                    shift.OpeningCashAmount, shift.ClosingCashAmount, expected, discrepancy));
            }
        }

        return results.OrderByDescending(r => r.StartedAtUtc).ToList();
    }
}
