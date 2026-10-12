using BMS.API.Data;
using BMS.API.Models;
using BMS.Shared;
using BMS.Shared.Dtos;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace BMS.API.Controllers;

// Đợt 4: Chấm công - đơn giản hơn Ca làm việc (Shift): chỉ chấm giờ vào/ra cho mục đích
// payroll/HR, tách biệt với việc quản lý tiền quỹ/bàn giao của Shift.
[ApiController]
[Route("api/attendance")]
[Authorize]
public class AttendanceController : ControllerBase
{
    private readonly BmsDbContext _db;

    public AttendanceController(BmsDbContext db)
    {
        _db = db;
    }

    private int? GetCurrentAccountId()
    {
        var claim = User.FindFirst("userId")?.Value;
        return int.TryParse(claim, out var id) ? id : null;
    }

    private static AttendanceDto ToDto(AttendanceEntity a) => new(
        a.Id, a.StaffAccountId, a.StaffName, a.ClockInAtUtc, a.ClockOutAtUtc, a.Note,
        (a.ClockOutAtUtc - a.ClockInAtUtc)?.TotalHours);

    // GET api/attendance/me/today - bản ghi chấm công hôm nay của người gọi (kể cả bản
    // ghi đang mở/chưa clock-out).
    [HttpGet("me/today")]
    public async Task<ActionResult<List<AttendanceDto>>> GetMyToday()
    {
        var accountId = GetCurrentAccountId();
        if (accountId is null) return Unauthorized();

        // "Hôm nay" theo giờ Việt Nam (UTC+7), quy đổi lại về UTC để so với dữ liệu lưu UTC.
        var today = TablePricing.ToVietnamTime(DateTime.UtcNow).Date.AddHours(-7);
        var tomorrow = today.AddDays(1);

        var list = await _db.Attendances
            .Where(a => a.StaffAccountId == accountId &&
                        (a.ClockInAtUtc >= today && a.ClockInAtUtc < tomorrow || a.ClockOutAtUtc == null))
            .OrderByDescending(a => a.ClockInAtUtc)
            .ToListAsync();

        return list.Select(ToDto).ToList();
    }

    // POST api/attendance/clock-in - chấm công vào ca.
    [Authorize(Roles = "Staff,Admin")]
    [HttpPost("clock-in")]
    public async Task<ActionResult<AttendanceDto>> ClockIn(ClockInRequestDto request)
    {
        var accountId = GetCurrentAccountId();
        if (accountId is null) return Unauthorized();

        var alreadyOpen = await _db.Attendances.AnyAsync(a => a.StaffAccountId == accountId && a.ClockOutAtUtc == null);
        if (alreadyOpen)
        {
            return BadRequest("Bạn đã chấm công vào và chưa chấm công ra.");
        }

        var fullName = User.FindFirst("fullName")?.Value ?? string.Empty;

        var attendance = new AttendanceEntity
        {
            StaffAccountId = accountId.Value,
            StaffName = fullName,
            ClockInAtUtc = DateTime.UtcNow,
            Note = request.Note
        };

        _db.Attendances.Add(attendance);
        await _db.SaveChangesAsync();

        return ToDto(attendance);
    }

    // POST api/attendance/clock-out - chấm công ra ca, đóng bản ghi đang mở.
    [Authorize(Roles = "Staff,Admin")]
    [HttpPost("clock-out")]
    public async Task<ActionResult<AttendanceDto>> ClockOut(ClockOutRequestDto request)
    {
        var accountId = GetCurrentAccountId();
        if (accountId is null) return Unauthorized();

        var attendance = await _db.Attendances
            .Where(a => a.StaffAccountId == accountId && a.ClockOutAtUtc == null)
            .OrderByDescending(a => a.ClockInAtUtc)
            .FirstOrDefaultAsync();

        if (attendance is null)
        {
            return BadRequest("Bạn chưa chấm công vào ca.");
        }

        attendance.ClockOutAtUtc = DateTime.UtcNow;
        if (!string.IsNullOrWhiteSpace(request.Note))
        {
            attendance.Note = request.Note;
        }

        await _db.SaveChangesAsync();

        return ToDto(attendance);
    }

    // GET api/attendance - Admin: báo cáo chấm công, lọc theo staffAccountId/from/to (tùy chọn).
    [Authorize(Roles = "Admin")]
    [HttpGet]
    public async Task<ActionResult<List<AttendanceDto>>> GetAll(
        [FromQuery] int? staffAccountId, [FromQuery] DateTime? from, [FromQuery] DateTime? to)
    {
        var query = _db.Attendances.AsQueryable();

        if (staffAccountId is int id)
        {
            query = query.Where(a => a.StaffAccountId == id);
        }

        if (from is DateTime fromDate)
        {
            query = query.Where(a => a.ClockInAtUtc >= fromDate);
        }

        if (to is DateTime toDate)
        {
            query = query.Where(a => a.ClockInAtUtc <= toDate);
        }

        var list = await query.OrderByDescending(a => a.ClockInAtUtc).ToListAsync();
        return list.Select(ToDto).ToList();
    }
}
