using BMS.API.Data;
using BMS.API.Hubs;
using BMS.API.Models;
using BMS.Shared.Dtos;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;

namespace BMS.API.Controllers;

// Đợt 2 phần D: Chat khách hàng <-> nhân viên. Real-time chỉ qua SignalR trong lúc
// app/tab đang mở ("ChatMessageReceived" broadcast tới TẤT CẢ client, client tự lọc
// theo ThreadId đang mở - giống cách TableStatusChanged đang làm). TODO tối ưu bằng
// SignalR Groups sau. Push notification khi app đóng: TODO tích hợp Firebase Cloud
// Messaging ở một đợt sau, KHÔNG làm ở đợt này.
[ApiController]
[Route("api/chat")]
[Authorize]
public class ChatController : ControllerBase
{
    private readonly BmsDbContext _db;
    private readonly IHubContext<BmsHub> _hub;

    public ChatController(BmsDbContext db, IHubContext<BmsHub> hub)
    {
        _db = db;
        _hub = hub;
    }

    private static ChatThreadDto ToThreadDto(ChatThreadEntity t) => new(
        t.Id, t.Type, t.TableId, t.Table?.Name, t.CustomerAccountId, t.CustomerAccount?.FullName,
        t.CreatedAtUtc, t.LastMessageAtUtc,
        // Đợt 16: chỉ đếm tin CHƯA ĐỌC do khách gửi (tin của nhân viên/admin không tính là
        // "chưa đọc" với phía quầy) - trước đây đếm cả tin của chính nhân viên.
        t.Messages?.Count(m => !m.IsRead && !IsStaffRole(m.SenderRole)) ?? 0);

    private static bool IsStaffRole(string? role) =>
        string.Equals(role, "Staff", StringComparison.OrdinalIgnoreCase)
        || string.Equals(role, "Admin", StringComparison.OrdinalIgnoreCase);

    private static ChatMessageDto ToMessageDto(ChatMessageEntity m) => new(
        m.Id, m.ThreadId, m.SenderAccountId, m.SenderRole, m.SenderName, m.Text, m.ImageUrl, m.SentAtUtc, m.IsRead);

    // Staff/Admin thấy tất cả thread. Player chỉ thấy thread của chính mình.
    [HttpGet("threads")]
    public async Task<ActionResult<List<ChatThreadDto>>> GetThreads()
    {
        var isStaff = User.IsInRole("Staff") || User.IsInRole("Admin");
        var accountId = GetCurrentAccountId();
        if (accountId is null) return Unauthorized();

        var query = _db.ChatThreads
            .Include(t => t.Table)
            .Include(t => t.CustomerAccount)
            .Include(t => t.Messages)
            .AsQueryable();

        if (!isStaff)
        {
            query = query.Where(t => t.CustomerAccountId == accountId);
        }

        var list = await query.OrderByDescending(t => t.LastMessageAtUtc).ToListAsync();
        return list.Select(ToThreadDto).ToList();
    }

    // Tạo mới nếu chưa có. CustomerStaff: tìm/tạo theo CustomerAccountId của người gọi.
    // TableStaff: chỉ Staff/Admin mới tạo được, gắn theo TableId.
    [HttpPost("threads")]
    public async Task<ActionResult<ChatThreadDto>> CreateOrGetThread(CreateChatThreadRequestDto request)
    {
        var accountId = GetCurrentAccountId();
        if (accountId is null) return Unauthorized();

        var isStaff = User.IsInRole("Staff") || User.IsInRole("Admin");

        if (request.Type == "TableStaff")
        {
            if (!isStaff) return Forbid();
            if (request.TableId is null) return BadRequest("Thiếu thông tin bàn cho cuộc trò chuyện.");

            var existingTable = await _db.ChatThreads
                .Include(t => t.Table)
                .FirstOrDefaultAsync(t => t.Type == "TableStaff" && t.TableId == request.TableId);

            if (existingTable is not null) return ToThreadDto(existingTable);

            var newTableThread = new ChatThreadEntity
            {
                Type = "TableStaff",
                TableId = request.TableId,
                CreatedAtUtc = DateTime.UtcNow,
                LastMessageAtUtc = DateTime.UtcNow
            };
            _db.ChatThreads.Add(newTableThread);
            await _db.SaveChangesAsync();

            newTableThread.Table = await _db.Tables.FindAsync(request.TableId);
            return ToThreadDto(newTableThread);
        }

        // CustomerStaff (mặc định): một khách hàng chỉ có một thread duy nhất với nhân viên.
        var existing = await _db.ChatThreads
            .Include(t => t.CustomerAccount)
            .FirstOrDefaultAsync(t => t.Type == "CustomerStaff" && t.CustomerAccountId == accountId);

        if (existing is not null) return ToThreadDto(existing);

        var thread = new ChatThreadEntity
        {
            Type = "CustomerStaff",
            CustomerAccountId = accountId,
            CreatedAtUtc = DateTime.UtcNow,
            LastMessageAtUtc = DateTime.UtcNow
        };
        _db.ChatThreads.Add(thread);
        await _db.SaveChangesAsync();

        thread.CustomerAccount = await _db.Accounts.FindAsync(accountId);
        return ToThreadDto(thread);
    }

    [HttpGet("threads/{id:int}/messages")]
    public async Task<ActionResult<List<ChatMessageDto>>> GetMessages(int id)
    {
        var accountId = GetCurrentAccountId();
        if (accountId is null) return Unauthorized();

        var thread = await _db.ChatThreads.FirstOrDefaultAsync(t => t.Id == id);
        if (thread is null) return NotFound();

        var isStaff = User.IsInRole("Staff") || User.IsInRole("Admin");
        if (!isStaff && thread.CustomerAccountId != accountId)
        {
            return Forbid();
        }

        var messages = await _db.ChatMessages
            .Where(m => m.ThreadId == id)
            .OrderBy(m => m.SentAtUtc)
            .ToListAsync();

        // Đợt 16: nhân viên/admin mở cuộc trò chuyện => đánh dấu các tin của khách là đã đọc.
        // Trước đây IsRead không bao giờ được đặt true nên badge "Hộp thư" chỉ tăng, không giảm.
        if (isStaff)
        {
            var unread = messages.Where(m => !m.IsRead && !IsStaffRole(m.SenderRole)).ToList();
            if (unread.Count > 0)
            {
                foreach (var m in unread)
                {
                    m.IsRead = true;
                }
                await _db.SaveChangesAsync();
            }
        }

        return messages.Select(ToMessageDto).ToList();
    }

    [HttpPost("threads/{id:int}/messages")]
    public async Task<ActionResult<ChatMessageDto>> SendMessage(int id, SendChatMessageRequestDto request)
    {
        if (string.IsNullOrWhiteSpace(request.Text) && string.IsNullOrWhiteSpace(request.ImageUrl))
        {
            return BadRequest("Tin nhắn phải có nội dung hoặc ảnh.");
        }

        var accountId = GetCurrentAccountId();
        if (accountId is null) return Unauthorized();

        var thread = await _db.ChatThreads.FirstOrDefaultAsync(t => t.Id == id);
        if (thread is null) return NotFound();

        var isStaff = User.IsInRole("Staff") || User.IsInRole("Admin");
        if (!isStaff && thread.CustomerAccountId != accountId)
        {
            return Forbid();
        }

        var role = User.FindFirst(System.Security.Claims.ClaimTypes.Role)?.Value ?? "Player";
        var name = User.FindFirst("fullName")?.Value ?? string.Empty;

        var message = new ChatMessageEntity
        {
            ThreadId = id,
            SenderAccountId = accountId.Value,
            SenderRole = role,
            SenderName = name,
            Text = request.Text,
            ImageUrl = request.ImageUrl,
            SentAtUtc = DateTime.UtcNow,
            IsRead = false
        };

        _db.ChatMessages.Add(message);
        thread.LastMessageAtUtc = message.SentAtUtc;

        await _db.SaveChangesAsync();

        var dto = ToMessageDto(message);
        await _hub.Clients.All.SendAsync("ChatMessageReceived", dto);

        return CreatedAtAction(nameof(GetMessages), new { id }, dto);
    }

    private int? GetCurrentAccountId()
    {
        var claim = User.FindFirst("userId")?.Value;
        return int.TryParse(claim, out var id) ? id : null;
    }
}
