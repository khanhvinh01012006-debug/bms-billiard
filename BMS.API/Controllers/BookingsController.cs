using System.Security.Cryptography;
using System.Text;
using BMS.API.Data;
using BMS.API.Hubs;
using BMS.API.Models;
using BMS.Shared.Dtos;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;

namespace BMS.API.Controllers;

// EP-02: US-008 (đặt bàn), US-009 (hủy đặt bàn).
// Đợt 2: đặt bàn giờ đi qua trạng thái "Pending" chờ nhân viên "Giữ chỗ" (Confirm) trong
// vòng 15 phút (ExpiresAtUtc), nếu không sẽ tự Expired bởi BookingExpiryHostedService.
[ApiController]
[Route("api/bookings")]
public class BookingsController : ControllerBase
{
    private readonly BmsDbContext _db;
    private readonly IHubContext<BmsHub> _hub;
    private readonly byte[] _cancelKey;

    public BookingsController(BmsDbContext db, IHubContext<BmsHub> hub, IConfiguration configuration)
    {
        _cancelKey = Encoding.UTF8.GetBytes("booking-cancel:" + configuration["Jwt:Key"]);
        _db = db;
        _hub = hub;
    }

    // Mã hủy đặt bàn = HMAC-SHA256(id) bằng khóa bí mật của máy chủ: không cần thêm cột vào DB,
    // chỉ người vừa đặt (nhận mã trong phản hồi) mới hủy được.
    private string ComputeCancelToken(int bookingId) =>
        Convert.ToBase64String(HMACSHA256.HashData(_cancelKey, Encoding.UTF8.GetBytes(bookingId.ToString())))
            .TrimEnd('=').Replace('+', '-').Replace('/', '_');

    private static BookingDto ToDto(BookingEntity b) =>
        new(b.Id, b.TableId, b.CustomerName, b.CreatedAtUtc, b.Status,
            b.PhoneNumber, b.Note, b.DurationMinutes, b.StartTimeUtc, b.ExpiresAtUtc,
            b.Table?.Name);

    // US-008: Đặt bàn.
    // Concurrency safety: the table row is locked for the duration of a single DB
    // transaction; we re-check the table's current status *inside* that transaction
    // (right before writing) so two simultaneous requests for the same table cannot
    // both succeed - the second one will see the status already changed and fail
    // with a conflict, instead of relying on a check done before the transaction opened.
    [HttpPost]
    public async Task<ActionResult<BookingDto>> Create(CreateBookingRequestDto request)
    {
        if (string.IsNullOrWhiteSpace(request.CustomerName))
        {
            return BadRequest("Vui lòng nhập tên khách.");
        }

        await using var transaction = await _db.Database.BeginTransactionAsync();

        var table = await _db.Tables.FirstOrDefaultAsync(t => t.Id == request.TableId);
        if (table is null)
        {
            return NotFound("Không tìm thấy bàn.");
        }

        if (table.Status != TableStatus.Available)
        {
            return Conflict("Bàn này hiện không trống để đặt.");
        }

        var now = DateTime.UtcNow;
        var booking = new BookingEntity
        {
            TableId = table.Id,
            CustomerName = request.CustomerName,
            PhoneNumber = request.PhoneNumber,
            Note = request.Note,
            DurationMinutes = request.DurationMinutes > 0 ? request.DurationMinutes : 120,
            CreatedAtUtc = now,
            StartTimeUtc = request.StartTimeUtc,
            ExpiresAtUtc = now.AddMinutes(15),
            Status = "Pending"
        };

        table.Status = TableStatus.Reserved;

        _db.Bookings.Add(booking);

        try
        {
            await _db.SaveChangesAsync();
            await transaction.CommitAsync();
        }
        catch (DbUpdateConcurrencyException)
        {
            await transaction.RollbackAsync();
            return Conflict("Bàn vừa được người khác đặt. Vui lòng chọn bàn khác.");
        }

        await _hub.Clients.All.SendAsync("TableStatusChanged",
            new TableStatusChangedEvent(table.Id, table.Status));

        booking.Table = table;
        return CreatedAtAction(nameof(GetById), new { id = booking.Id },
            ToDto(booking) with { CancelToken = ComputeCancelToken(booking.Id) });
    }

    // Chỉ Nhân viên/Admin, hoặc chính người đặt (có mã hủy) mới xem được - trước đây ai cũng
    // đọc được tên + số điện thoại khách chỉ bằng cách đoán mã đặt bàn.
    [HttpGet("{id:int}")]
    public async Task<ActionResult<BookingDto>> GetById(int id, [FromHeader(Name = "X-Booking-Token")] string? token)
    {
        var isStaff = User.IsInRole("Staff") || User.IsInRole("Admin");
        var tokenOk = token is not null && CryptographicOperations.FixedTimeEquals(
            Encoding.UTF8.GetBytes(ComputeCancelToken(id)), Encoding.UTF8.GetBytes(token));
        if (!isStaff && !tokenOk)
        {
            return Forbid();
        }

        var booking = await _db.Bookings.Include(b => b.Table).FirstOrDefaultAsync(b => b.Id == id);
        if (booking is null) return NotFound();
        return ToDto(booking);
    }

    // Danh sách yêu cầu đặt bàn - dùng cho khu vực "Yêu cầu đặt bàn chờ duyệt" của nhân viên.
    // status=Pending trả các yêu cầu đang chờ; status=Expired trả các yêu cầu quá hạn gần
    // đây (trong vòng 2 giờ) để nhân viên còn có thể "Nhận khách đến trễ".
    [Authorize(Roles = "Staff,Admin")]
    [HttpGet]
    public async Task<ActionResult<List<BookingDto>>> GetAll([FromQuery] string? status)
    {
        var query = _db.Bookings.Include(b => b.Table).AsQueryable();

        if (string.Equals(status, "Expired", StringComparison.OrdinalIgnoreCase))
        {
            var cutoff = DateTime.UtcNow.AddHours(-2);
            query = query.Where(b => b.Status == "Expired" && b.ExpiresAtUtc > cutoff);
        }
        else if (!string.IsNullOrWhiteSpace(status))
        {
            query = query.Where(b => b.Status == status);
        }

        var list = await query.OrderByDescending(b => b.CreatedAtUtc).ToListAsync();
        return list.Select(ToDto).ToList();
    }

    // Nhân viên "Giữ chỗ" cho khách - xác nhận yêu cầu đặt bàn đang Pending.
    [Authorize(Roles = "Staff,Admin")]
    [HttpPut("{id:int}/confirm")]
    public async Task<ActionResult<BookingDto>> Confirm(int id)
    {
        var booking = await _db.Bookings.Include(b => b.Table).FirstOrDefaultAsync(b => b.Id == id);
        if (booking is null) return NotFound();

        if (booking.Status != "Pending")
        {
            return Conflict("Chỉ có thể giữ chỗ cho yêu cầu đang ở trạng thái chờ.");
        }

        booking.Status = "Confirmed";
        await _db.SaveChangesAsync();

        // Báo các màn hình khác (máy NV khác, khách) tải lại: bàn này giờ có khách giữ chỗ.
        if (booking.Table is not null)
        {
            await _hub.Clients.All.SendAsync("TableStatusChanged",
                new TableStatusChangedEvent(booking.Table.Id, booking.Table.Status));
        }

        return ToDto(booking);
    }

    // Nhân viên "Nhận khách đến trễ" - xác nhận dù booking đã quá hạn tự hủy.
    [Authorize(Roles = "Staff,Admin")]
    [HttpPut("{id:int}/checkin-late")]
    public async Task<ActionResult<BookingDto>> CheckInLate(int id)
    {
        var booking = await _db.Bookings.Include(b => b.Table).FirstOrDefaultAsync(b => b.Id == id);
        if (booking is null) return NotFound();

        if (booking.Status != "Expired")
        {
            return Conflict("Chỉ áp dụng cho yêu cầu đặt bàn đã quá hạn.");
        }

        booking.Status = "Confirmed";

        // Nếu bàn đang trống (đã được nhả ra khi hết hạn), giữ lại cho khách đến trễ.
        if (booking.Table is not null && booking.Table.Status == TableStatus.Available)
        {
            booking.Table.Status = TableStatus.Reserved;
            await _hub.Clients.All.SendAsync("TableStatusChanged",
                new TableStatusChangedEvent(booking.Table.Id, booking.Table.Status));
        }

        await _db.SaveChangesAsync();

        return ToDto(booking);
    }

    // US-009: Hủy đặt bàn
    [Authorize(Roles = "Staff,Admin")]
    [HttpDelete("{id:int}")]
    public Task<IActionResult> Cancel(int id) => CancelCoreAsync(id);

    // Khách tự hủy yêu cầu đặt bàn của chính mình bằng mã hủy nhận được lúc đặt.
    [AllowAnonymous]
    [HttpPost("{id:int}/cancel-by-token")]
    public Task<IActionResult> CancelByToken(int id, CancelBookingByTokenRequestDto request)
    {
        var expected = Encoding.UTF8.GetBytes(ComputeCancelToken(id));
        var given = Encoding.UTF8.GetBytes(request.Token ?? string.Empty);
        if (!CryptographicOperations.FixedTimeEquals(expected, given))
        {
            return Task.FromResult<IActionResult>(Forbid());
        }
        return CancelCoreAsync(id);
    }

    private async Task<IActionResult> CancelCoreAsync(int id)
    {
        await using var transaction = await _db.Database.BeginTransactionAsync();

        var booking = await _db.Bookings.FirstOrDefaultAsync(b => b.Id == id);
        if (booking is null) return NotFound();

        if (booking.Status != "Pending" && booking.Status != "Confirmed")
        {
            return Conflict("Chỉ có thể hủy yêu cầu đang chờ hoặc đã giữ chỗ.");
        }

        booking.Status = "Cancelled";

        var table = await _db.Tables.FirstOrDefaultAsync(t => t.Id == booking.TableId);
        if (table is not null && table.Status == TableStatus.Reserved)
        {
            table.Status = TableStatus.Available;
        }

        await _db.SaveChangesAsync();
        await transaction.CommitAsync();

        if (table is not null)
        {
            await _hub.Clients.All.SendAsync("TableStatusChanged",
                new TableStatusChangedEvent(table.Id, table.Status));
        }

        return NoContent();
    }
}
