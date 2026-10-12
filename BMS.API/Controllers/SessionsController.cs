using BMS.API.Data;
using BMS.API.Hubs;
using BMS.API.Models;
using BMS.Shared;
using BMS.Shared.Dtos;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;

namespace BMS.API.Controllers;

// EP-02: US-010 (nhận bàn), US-011 (theo dõi thời gian chơi via TableDto.ActiveSessionStartedAtUtc),
// US-012 (chuyển bàn). EP-03: US-014 (tính tiền theo giờ) happens on checkout.
[ApiController]
[Route("api/sessions")]
public class SessionsController : ControllerBase
{
    private readonly BmsDbContext _db;
    private readonly IHubContext<BmsHub> _hub;

    public SessionsController(BmsDbContext db, IHubContext<BmsHub> hub)
    {
        _db = db;
        _hub = hub;
    }

    public record CheckInRequestDto(int TableId, string CustomerName, int? BookingId);
    public record CheckOutRequestDto(int TableId, decimal? SurchargeAmount = null, string? SurchargeReason = null);
    public record TransferTableRequestDto(int FromTableId, int ToTableId);

    private int? GetCurrentAccountId()
    {
        var claim = User.FindFirst("userId")?.Value;
        return int.TryParse(claim, out var id) ? id : null;
    }

    private static OrderDto ToOrderDto(OrderEntity o) => new(
        o.Id, o.PlayingSessionId, o.ItemName, o.UnitPrice, o.Quantity, o.LineTotal, o.CreatedAtUtc, o.CreatedByName);

    // US-010: Nhận bàn (check-in). Starts an active playing session on a table.
    [Authorize(Roles = "Staff,Admin")]
    [HttpPost("checkin")]
    public async Task<ActionResult<TableDto>> CheckIn(CheckInRequestDto request)
    {
        await using var transaction = await _db.Database.BeginTransactionAsync();

        var table = await _db.Tables
            .Include(t => t.Sessions.Where(s => s.Status == "Active" || s.Status == "Paused"))
            .FirstOrDefaultAsync(t => t.Id == request.TableId);

        if (table is null) return NotFound("Không tìm thấy bàn.");

        if (table.Status == TableStatus.Occupied || table.Sessions.Any())
        {
            return Conflict("Bàn này đang có khách chơi.");
        }

        if (table.Status == TableStatus.Maintenance)
        {
            return Conflict("Bàn đang bảo trì.");
        }

        var session = new PlayingSessionEntity
        {
            TableId = table.Id,
            CustomerName = request.CustomerName,
            StartedAtUtc = DateTime.UtcNow,
            Status = "Active"
        };
        _db.PlayingSessions.Add(session);

        table.Status = TableStatus.Occupied;

        if (request.BookingId is int bookingId)
        {
            var booking = await _db.Bookings.FirstOrDefaultAsync(b => b.Id == bookingId);
            if (booking is not null && booking.Status is "Pending" or "Confirmed")
            {
                booking.Status = "CheckedIn";
            }
        }

        await _db.SaveChangesAsync();
        await transaction.CommitAsync();

        await _hub.Clients.All.SendAsync("TableStatusChanged",
            new TableStatusChangedEvent(table.Id, table.Status));

        return new TableDto(table.Id, table.Name, table.Status, table.Capacity, table.PricePerHour,
            session.StartedAtUtc, table.ImageUrl, session.Id);
    }

    // US-011 + US-013/US-014: Trả bàn (check-out). Closes the active session and computes
    // the amount owed based on elapsed real time * PricePerHour.
    [Authorize(Roles = "Staff,Admin")]
    [HttpPost("checkout")]
    public async Task<ActionResult<CheckOutResultDto>> CheckOut(CheckOutRequestDto request)
    {
        await using var transaction = await _db.Database.BeginTransactionAsync();

        var table = await _db.Tables
            .Include(t => t.TableType)
            .ThenInclude(tt => tt!.PricingSlots)
            .FirstOrDefaultAsync(t => t.Id == request.TableId);
        if (table is null) return NotFound("Không tìm thấy bàn.");

        var session = await _db.PlayingSessions
            .Where(s => s.TableId == request.TableId && (s.Status == "Active" || s.Status == "Paused"))
            .OrderByDescending(s => s.StartedAtUtc)
            .FirstOrDefaultAsync();

        if (session is null) return Conflict("Bàn này không có phiên chơi đang hoạt động.");

        // Đang tạm dừng: thời gian chơi tính tới lúc bắt đầu tạm dừng.
        var endedAt = session.Status == "Paused" && session.EndedAtUtc is DateTime pausedAt ? pausedAt : DateTime.UtcNow;
        var duration = endedAt - session.StartedAtUtc;
        if (duration < TimeSpan.Zero) duration = TimeSpan.Zero;

        // Đợt 5: tính tiền theo khung giờ (PricingSlots) của Loại bàn nếu có; nếu Loại bàn
        // chưa cấu hình khung giờ nào (hoặc bàn chưa gán Loại bàn), fallback về PricePerHour
        // cố định của chính bàn như trước (tương thích ngược).
        // Khung giờ là giờ Việt Nam, tiền giờ làm tròn 1.000đ - xem BMS.Shared.TablePricing (dùng
        // chung với "Tạm tính" trên màn hình nhân viên).
        var slots = (table.TableType?.PricingSlots ?? new List<PricingSlotEntity>())
            .Select(s => new TablePricing.Slot(s.StartTime, s.EndTime, s.PricePerHour))
            .ToList();
        decimal hourlyAmount = TablePricing.HourlyAmount(session.StartedAtUtc, endedAt, slots, table.PricePerHour);

        // Đợt 4: tiền order (Gọi món & dịch vụ) trên chính phiên chơi này + phụ thu thủ công
        // (ví dụ phí hư hỏng) được cộng vào tổng cuối cùng ngay tại lúc trả bàn.
        decimal ordersAmount = await _db.Orders
            .Where(o => o.PlayingSessionId == session.Id)
            .SumAsync(o => (decimal?)o.LineTotal) ?? 0m;

        decimal surchargeAmount = request.SurchargeAmount is decimal s && s > 0 ? s : 0m;
        decimal total = hourlyAmount + ordersAmount + surchargeAmount;

        // Đợt 6: "Trả bàn" chỉ dừng đồng hồ và chốt tổng tiền - KHÔNG mở lại bàn và KHÔNG
        // tạo giao dịch thanh toán ở đây nữa. Bàn vẫn Occupied ("Chờ thanh toán") cho tới
        // khi nhân viên hoàn tất bước chọn phương thức thanh toán qua complete-payment.
        session.EndedAtUtc = endedAt;
        session.TotalAmount = total;
        session.SurchargeAmount = surchargeAmount;
        session.SurchargeReason = surchargeAmount > 0 ? request.SurchargeReason : null;
        session.Status = "PendingPayment";

        await _db.SaveChangesAsync();
        await transaction.CommitAsync();

        // Trạng thái table.Status không đổi (vẫn Occupied) nhưng broadcast để các máy
        // nhân viên khác refresh và thấy badge "Chờ thanh toán" (PendingPaymentSessionId).
        await _hub.Clients.All.SendAsync("TableStatusChanged",
            new TableStatusChangedEvent(table.Id, table.Status));

        return new CheckOutResultDto(table.Id, duration, hourlyAmount, ordersAmount, surchargeAmount, total, session.Id);
    }

    // Đợt 6: cho phép UI mở lại modal thanh toán từ bất kỳ máy nào (vd. nhân viên khác
    // bấm "Thanh toán" trên badge "Chờ thanh toán" của bàn) mà không cần lưu lại state cũ -
    // trả về đúng dữ liệu như lúc checkout trả về (Duration/HourlyAmount/OrdersAmount/...).
    [Authorize(Roles = "Staff,Admin")]
    [HttpGet("{sessionId:int}/checkout-summary")]
    public async Task<ActionResult<CheckOutResultDto>> GetCheckoutSummary(int sessionId)
    {
        var session = await _db.PlayingSessions.FirstOrDefaultAsync(s => s.Id == sessionId);
        if (session is null) return NotFound("Không tìm thấy phiên chơi.");
        if (session.Status != "PendingPayment")
        {
            return Conflict("Phiên chơi không ở trạng thái chờ thanh toán.");
        }

        var duration = (session.EndedAtUtc ?? DateTime.UtcNow) - session.StartedAtUtc;
        if (duration < TimeSpan.Zero) duration = TimeSpan.Zero;

        var totalAmount = session.TotalAmount ?? 0m;
        var ordersAmount = await _db.Orders
            .Where(o => o.PlayingSessionId == session.Id)
            .SumAsync(o => (decimal?)o.LineTotal) ?? 0m;
        var hourlyAmount = totalAmount - ordersAmount - session.SurchargeAmount;

        return new CheckOutResultDto(session.TableId, duration, hourlyAmount, ordersAmount,
            session.SurchargeAmount, totalAmount, session.Id);
    }

    // Đợt 6: Bước 2 của Trả bàn - nhân viên chọn phương thức thanh toán (Thẻ hội viên /
    // Chuyển khoản / Tiền mặt) để hoàn tất phiên đang "PendingPayment", tạo giao dịch
    // Payment tương ứng và mở lại bàn (Available). Trả về đủ dữ liệu để in hóa đơn ngay.
    [Authorize(Roles = "Staff,Admin")]
    [HttpPost("{sessionId:int}/complete-payment")]
    public async Task<ActionResult<PaymentCompletedDto>> CompletePayment(int sessionId, CompletePaymentRequestDto request)
    {
        var staffAccountId = GetCurrentAccountId();
        if (staffAccountId is null) return Unauthorized();
        var staffFullName = User.FindFirst("fullName")?.Value ?? string.Empty;

        if (request.Method != "Wallet" && request.Method != "Cash" && request.Method != "BankTransfer")
        {
            return BadRequest("Phương thức thanh toán không hợp lệ.");
        }

        await using var transaction = await _db.Database.BeginTransactionAsync();

        var session = await _db.PlayingSessions
            .Include(s => s.Table)
            .FirstOrDefaultAsync(s => s.Id == sessionId);

        if (session is null) return NotFound("Không tìm thấy phiên chơi.");
        if (session.Status != "PendingPayment")
        {
            return Conflict("Phiên chơi chưa ở trạng thái chờ thanh toán.");
        }
        if (session.Table is null)
        {
            return Conflict("Không tìm thấy bàn của phiên chơi này.");
        }

        var totalAmount = session.TotalAmount ?? 0m;

        decimal discountAmount = 0m;
        DiscountRequestEntity? discountRequest = null;
        if (request.DiscountRequestId is int discountRequestId)
        {
            discountRequest = await _db.DiscountRequests.FindAsync(discountRequestId);
            if (discountRequest is null) return BadRequest("Không tìm thấy yêu cầu giảm giá.");
            if (discountRequest.SessionId != sessionId) return BadRequest("Yêu cầu giảm giá không khớp phiên chơi này.");
            if (discountRequest.IsApplied) return Conflict("Yêu cầu giảm giá này đã được áp dụng.");
            if (discountRequest.Status != "Approved") return Conflict("Yêu cầu giảm giá chưa được duyệt.");

            discountAmount = discountRequest.DiscountAmount;
            totalAmount = Math.Max(0, totalAmount - discountAmount);
        }

        decimal? cashReceived = null;
        decimal? change = null;
        string? memberName = null;
        int? memberAccountId = null;

        if (request.Method == "Wallet")
        {
            if (request.MemberAccountId is null)
            {
                return BadRequest("Vui lòng chọn hội viên để thanh toán bằng Ví.");
            }

            var profile = await _db.PlayerProfiles
                .Include(p => p.Account)
                .FirstOrDefaultAsync(p => p.AccountId == request.MemberAccountId.Value);

            if (profile is null)
            {
                return BadRequest("Không tìm thấy hồ sơ hội viên.");
            }

            if (profile.WalletBalance < totalAmount)
            {
                return BadRequest("Số dư ví không đủ.");
            }

            profile.WalletBalance -= totalAmount;
            memberAccountId = profile.AccountId;
            memberName = profile.Account?.FullName;
        }
        else if (request.Method == "Cash")
        {
            if (request.CashReceivedAmount is null || request.CashReceivedAmount.Value < totalAmount)
            {
                return BadRequest("Số tiền khách đưa không đủ.");
            }

            cashReceived = request.CashReceivedAmount.Value;
            change = cashReceived.Value - totalAmount;

            if (request.MemberAccountId is int optionalMemberId)
            {
                var profile = await _db.PlayerProfiles
                    .Include(p => p.Account)
                    .FirstOrDefaultAsync(p => p.AccountId == optionalMemberId);
                if (profile is not null)
                {
                    memberAccountId = profile.AccountId;
                    memberName = profile.Account?.FullName;
                }
            }
        }
        else // BankTransfer
        {
            if (request.MemberAccountId is int optionalMemberId)
            {
                var profile = await _db.PlayerProfiles
                    .Include(p => p.Account)
                    .FirstOrDefaultAsync(p => p.AccountId == optionalMemberId);
                if (profile is not null)
                {
                    memberAccountId = profile.AccountId;
                    memberName = profile.Account?.FullName;
                }
            }
        }

        var table = session.Table;

        var paymentTransaction = new TransactionEntity
        {
            AccountId = memberAccountId,
            Type = "Payment",
            Method = request.Method,
            Amount = totalAmount,
            Status = "Approved",
            Note = discountAmount > 0
                ? $"Thanh toán bàn {table.Name} - phiên #{session.Id} (đã giảm {discountRequest!.DiscountPercent}%, -{discountAmount:N0}đ)"
                : $"Thanh toán bàn {table.Name} - phiên #{session.Id}",
            RelatedEntityType = "PlayingSession",
            RelatedEntityId = session.Id,
            CreatedAtUtc = DateTime.UtcNow,
            ProcessedByAccountId = staffAccountId,
            ProcessedByName = staffFullName,
            ProcessedAtUtc = DateTime.UtcNow
        };
        _db.Transactions.Add(paymentTransaction);

        if (discountRequest is not null)
        {
            discountRequest.IsApplied = true;
        }

        session.Status = "Closed";
        table.Status = TableStatus.Available;

        await _db.SaveChangesAsync();
        await transaction.CommitAsync();

        await _hub.Clients.All.SendAsync("TableStatusChanged",
            new TableStatusChangedEvent(table.Id, table.Status));

        paymentTransaction.Account = memberAccountId is int accId ? await _db.Accounts.FindAsync(accId) : null;
        await _hub.Clients.All.SendAsync("TransactionCreated", new TransactionDto(
            paymentTransaction.Id, paymentTransaction.AccountId,
            paymentTransaction.Account?.FullName ?? (paymentTransaction.AccountId is null ? "Khách vãng lai" : string.Empty),
            paymentTransaction.Type, paymentTransaction.Method, paymentTransaction.Amount, paymentTransaction.Status,
            paymentTransaction.Note, paymentTransaction.ProofImageUrl, paymentTransaction.RelatedEntityType,
            paymentTransaction.RelatedEntityId, paymentTransaction.CreatedAtUtc,
            paymentTransaction.ProcessedByName, paymentTransaction.ProcessedAtUtc));

        var duration = (session.EndedAtUtc ?? DateTime.UtcNow) - session.StartedAtUtc;
        if (duration < TimeSpan.Zero) duration = TimeSpan.Zero;
        var ordersAmount = await _db.Orders
            .Where(o => o.PlayingSessionId == session.Id)
            .SumAsync(o => (decimal?)o.LineTotal) ?? 0m;
        var hourlyAmount = totalAmount - ordersAmount - session.SurchargeAmount;

        return new PaymentCompletedDto(
            session.Id, table.Id, table.Name, request.Method, duration, hourlyAmount, ordersAmount,
            session.SurchargeAmount, totalAmount, cashReceived, change, memberName, staffFullName,
            paymentTransaction.CreatedAtUtc, discountAmount);
    }

    // Đợt 4: Gọi món & dịch vụ - danh sách nhanh vật tư trong Kho để nhân viên chọn thêm
    // vào tab của bàn (chỉ đọc, không dùng chung quyền hạn Admin-only của InventoryController).
    [Authorize(Roles = "Staff,Admin")]
    [HttpGet("inventory-quickadd")]
    public async Task<ActionResult<List<InventoryItemDto>>> GetInventoryQuickAdd()
    {
        var items = await _db.InventoryItems
            .Where(i => i.IsQuickAddOrderable)
            .OrderBy(i => i.Name)
            .ToListAsync();
        return items.Select(i => new InventoryItemDto(
            i.Id, i.Name, i.Category, i.Unit, i.Quantity, i.LowStockThreshold, i.UnitPrice,
            i.ImageUrl, i.Note, i.UpdatedAtUtc, i.Quantity <= i.LowStockThreshold)).ToList();
    }

    // GET api/sessions/{sessionId}/orders - danh sách order trên một phiên chơi.
    [Authorize(Roles = "Staff,Admin")]
    [HttpGet("{sessionId:int}/orders")]
    public async Task<ActionResult<List<OrderDto>>> GetSessionOrders(int sessionId)
    {
        var exists = await _db.PlayingSessions.AnyAsync(s => s.Id == sessionId);
        if (!exists) return NotFound("Không tìm thấy phiên chơi.");

        var list = await _db.Orders
            .Where(o => o.PlayingSessionId == sessionId)
            .OrderBy(o => o.CreatedAtUtc)
            .ToListAsync();

        return list.Select(ToOrderDto).ToList();
    }

    // POST api/sessions/{sessionId}/orders - thêm một dòng order vào tab của phiên chơi.
    [Authorize(Roles = "Staff,Admin")]
    [HttpPost("{sessionId:int}/orders")]
    public async Task<ActionResult<OrderDto>> AddOrder(int sessionId, AddOrderRequestDto request)
    {
        var accountId = GetCurrentAccountId();
        if (accountId is null) return Unauthorized();

        var session = await _db.PlayingSessions.FirstOrDefaultAsync(s => s.Id == sessionId);
        if (session is null) return NotFound("Không tìm thấy phiên chơi.");
        if (session.Status is not ("Active" or "Paused")) return Conflict("Phiên chơi đã kết thúc, không thể thêm order.");

        if (request.Quantity <= 0) return BadRequest("Số lượng phải lớn hơn 0.");

        var itemName = request.ItemName;
        var unitPrice = request.UnitPrice;

        if (request.InventoryItemId is int invId)
        {
            var invItem = await _db.InventoryItems.FindAsync(invId);
            if (invItem is null) return BadRequest("Không tìm thấy vật tư trong Kho.");
            // Snapshot tên/giá từ Kho tại thời điểm thêm, không tin dữ liệu client gửi lên
            // cho hai trường này (để tránh client giả mạo giá) - vẫn dùng ItemName/UnitPrice
            // của client khi đây là dòng dịch vụ tự do (InventoryItemId = null).
            itemName = invItem.Name;
            unitPrice = invItem.UnitPrice ?? unitPrice;
        }

        if (string.IsNullOrWhiteSpace(itemName)) return BadRequest("Tên món/dịch vụ không được để trống.");

        var fullName = User.FindFirst("fullName")?.Value ?? string.Empty;

        var order = new OrderEntity
        {
            PlayingSessionId = sessionId,
            ItemName = itemName,
            UnitPrice = unitPrice,
            Quantity = request.Quantity,
            LineTotal = unitPrice * request.Quantity,
            InventoryItemId = request.InventoryItemId,
            CreatedAtUtc = DateTime.UtcNow,
            CreatedByAccountId = accountId.Value,
            CreatedByName = fullName
        };

        _db.Orders.Add(order);
        await _db.SaveChangesAsync();

        var table = await _db.Tables.FirstOrDefaultAsync(t => t.Id == session.TableId);
        if (table is not null)
        {
            await _hub.Clients.All.SendAsync("TableStatusChanged",
                new TableStatusChangedEvent(table.Id, table.Status));
        }

        return Ok(ToOrderDto(order));
    }

    // DELETE api/sessions/orders/{orderId} - xóa một dòng order thêm nhầm (chỉ khi phiên còn Active).
    [Authorize(Roles = "Staff,Admin")]
    [HttpDelete("orders/{orderId:int}")]
    public async Task<IActionResult> DeleteOrder(int orderId)
    {
        var order = await _db.Orders.Include(o => o.Session).FirstOrDefaultAsync(o => o.Id == orderId);
        if (order is null) return NotFound();

        if (order.Session is null || order.Session.Status is not ("Active" or "Paused"))
        {
            return Conflict("Không thể xóa order khi phiên chơi đã kết thúc.");
        }

        var tableId = order.Session.TableId;
        _db.Orders.Remove(order);
        await _db.SaveChangesAsync();

        var table = await _db.Tables.FirstOrDefaultAsync(t => t.Id == tableId);
        if (table is not null)
        {
            await _hub.Clients.All.SendAsync("TableStatusChanged",
                new TableStatusChangedEvent(table.Id, table.Status));
        }

        return NoContent();
    }

    // US-012: Chuyển bàn. Moves the active session from one table to another.
    [Authorize(Roles = "Staff,Admin")]
    [HttpPost("transfer")]
    public async Task<ActionResult<TableDto>> Transfer(TransferTableRequestDto request)
    {
        if (request.FromTableId == request.ToTableId)
        {
            return BadRequest("Bàn đích phải khác bàn đang chơi.");
        }

        await using var transaction = await _db.Database.BeginTransactionAsync();

        var fromTable = await _db.Tables.FirstOrDefaultAsync(t => t.Id == request.FromTableId);
        var toTable = await _db.Tables.FirstOrDefaultAsync(t => t.Id == request.ToTableId);

        if (fromTable is null || toTable is null) return NotFound("Không tìm thấy bàn.");

        if (toTable.Status != TableStatus.Available)
        {
            return Conflict("Bàn đích không còn trống.");
        }

        var session = await _db.PlayingSessions
            .Where(s => s.TableId == request.FromTableId && (s.Status == "Active" || s.Status == "Paused"))
            .OrderByDescending(s => s.StartedAtUtc)
            .FirstOrDefaultAsync();

        if (session is null) return Conflict("Bàn nguồn không có phiên chơi đang hoạt động.");

        session.TableId = toTable.Id;

        fromTable.Status = TableStatus.Available;
        toTable.Status = TableStatus.Occupied;

        await _db.SaveChangesAsync();
        await transaction.CommitAsync();

        await _hub.Clients.All.SendAsync("TableStatusChanged",
            new TableStatusChangedEvent(fromTable.Id, fromTable.Status));
        await _hub.Clients.All.SendAsync("TableStatusChanged",
            new TableStatusChangedEvent(toTable.Id, toTable.Status));

        return new TableDto(toTable.Id, toTable.Name, toTable.Status, toTable.Capacity, toTable.PricePerHour,
            session.StartedAtUtc, toTable.ImageUrl, session.Id);
    }

    // Tạm dừng tính giờ (khách ra ngoài, sự cố bàn...). Không thêm cột: lúc tạm dừng, EndedAtUtc
    // lưu thời điểm bắt đầu tạm dừng; Status = "Paused".
    [Authorize(Roles = "Staff,Admin")]
    [HttpPost("{sessionId:int}/pause")]
    public async Task<IActionResult> Pause(int sessionId)
    {
        var session = await _db.PlayingSessions.FirstOrDefaultAsync(s => s.Id == sessionId);
        if (session is null) return NotFound("Không tìm thấy phiên chơi.");
        if (session.Status == "Paused") return Conflict("Phiên chơi đang tạm dừng.");
        if (session.Status != "Active") return Conflict("Phiên chơi đã kết thúc.");

        session.Status = "Paused";
        session.EndedAtUtc = DateTime.UtcNow;
        await _db.SaveChangesAsync();

        await _hub.Clients.All.SendAsync("TableStatusChanged",
            new TableStatusChangedEvent(session.TableId, TableStatus.Occupied));
        return NoContent();
    }

    // Chơi tiếp: dời giờ bắt đầu lên đúng bằng thời gian đã tạm dừng để tiền giờ không tính phần dừng.
    [Authorize(Roles = "Staff,Admin")]
    [HttpPost("{sessionId:int}/resume")]
    public async Task<IActionResult> Resume(int sessionId)
    {
        var session = await _db.PlayingSessions.FirstOrDefaultAsync(s => s.Id == sessionId);
        if (session is null) return NotFound("Không tìm thấy phiên chơi.");
        if (session.Status != "Paused") return Conflict("Phiên chơi không ở trạng thái tạm dừng.");

        var pausedAt = session.EndedAtUtc ?? DateTime.UtcNow;
        var pausedFor = DateTime.UtcNow - pausedAt;
        if (pausedFor > TimeSpan.Zero) session.StartedAtUtc = session.StartedAtUtc.Add(pausedFor);
        session.Status = "Active";
        session.EndedAtUtc = null;
        await _db.SaveChangesAsync();

        await _hub.Clients.All.SendAsync("TableStatusChanged",
            new TableStatusChangedEvent(session.TableId, TableStatus.Occupied));
        return NoContent();
    }

    // US-017: Lịch sử hóa đơn - closed sessions represent settled invoices
    // (US-013 tạo hóa đơn + US-014 tính tiền theo giờ + US-016 thanh toán all
    // happen together at checkout time above). Đợt 4: bổ sung OrdersAmount/SurchargeAmount
    // để trang Lịch sử phiên chơi hiển thị được phần chi tiết hóa đơn (tiền order/phụ thu).
    [Authorize(Roles = "Staff,Admin")]
    [HttpGet("history")]
    public async Task<ActionResult<List<SessionHistoryDto>>> GetHistory()
    {
        var sessions = await _db.PlayingSessions
            .Include(s => s.Table)
            .Where(s => s.Status == "Closed")
            .OrderByDescending(s => s.EndedAtUtc)
            .Select(s => new SessionHistoryDto(
                s.Id, s.TableId, s.Table!.Name, s.CustomerName,
                s.StartedAtUtc, s.EndedAtUtc, s.TotalAmount, s.Status,
                _db.Orders.Where(o => o.PlayingSessionId == s.Id).Sum(o => (decimal?)o.LineTotal) ?? 0m,
                s.SurchargeAmount))
            .ToListAsync();

        return sessions;
    }

    // Đợt 9: Dashboard - bản đồ nhiệt công suất bàn theo giờ trong ngày hôm nay (giờ địa phương
    // máy chủ không đáng tin do khác múi giờ client, nên nhận tham số ngày local từ client).
    [Authorize(Roles = "Staff,Admin")]
    [HttpGet("occupancy-heatmap")]
    public async Task<ActionResult<List<HourlyOccupancyDto>>> GetOccupancyHeatmap([FromQuery] DateTime? date)
    {
        // Ngày và các cột giờ tính theo giờ Việt Nam (UTC+7); dữ liệu lưu UTC.
        var targetDate = (date ?? TablePricing.ToVietnamTime(DateTime.UtcNow)).Date;
        var dayStart = targetDate.AddHours(-7);
        var dayEnd = dayStart.AddDays(1);

        // Lấy trước tất cả phiên chơi có khả năng giao với khung ngày này, rồi tính overlap theo
        // giờ ở phía C# cho an toàn (tránh EF Core dịch sai các phép so sánh DateTime phức tạp).
        var candidateSessions = await _db.PlayingSessions
            .Where(s => s.StartedAtUtc < dayEnd && (s.EndedAtUtc == null || s.EndedAtUtc > dayStart))
            .Select(s => new { s.StartedAtUtc, s.EndedAtUtc })
            .ToListAsync();

        var counts = new int[24];
        foreach (var session in candidateSessions)
        {
            var start = session.StartedAtUtc;
            var end = session.EndedAtUtc ?? DateTime.UtcNow;
            for (var hour = 0; hour < 24; hour++)
            {
                var hourStart = dayStart.AddHours(hour);
                var hourEnd = hourStart.AddHours(1);
                if (start < hourEnd && end > hourStart)
                {
                    counts[hour]++;
                }
            }
        }

        return Enumerable.Range(0, 24).Select(h => new HourlyOccupancyDto(h, counts[h])).ToList();
    }
}
