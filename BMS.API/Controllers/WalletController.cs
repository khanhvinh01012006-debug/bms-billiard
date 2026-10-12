using System.Security.Cryptography;
using System.Text;
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

// Đợt 3: Ví / Hội viên / Thanh toán. Nạp tiền (Cash/BankTransfer) cần nhân viên/quản
// trị phê duyệt trước khi cộng vào Ví; thanh toán bằng Ví (Method = Wallet) là trừ tiền
// ngay lập tức, tự động Approved vì chỉ là điều chỉnh số dư nội bộ.
[ApiController]
[Route("api/wallet")]
[Authorize]
public class WalletController : ControllerBase
{
    private readonly BmsDbContext _db;
    private readonly IHubContext<BmsHub> _hub;

    private readonly byte[] _qrKey;

    public WalletController(BmsDbContext db, IHubContext<BmsHub> hub, IConfiguration configuration)
    {
        _db = db;
        _hub = hub;
        _qrKey = Encoding.UTF8.GetBytes("member-qr:" + configuration["Jwt:Key"]);
    }

    private static readonly TimeSpan MemberQrLifetime = TimeSpan.FromMinutes(10);

    private static WalletInfoDto ToWalletInfoDto(PlayerProfileEntity p)
    {
        var (nextThreshold, nextTierName) = MembershipTiers.GetNextTier(p.TotalTopUpAmount);
        return new WalletInfoDto(p.WalletBalance, p.MembershipTier, p.TotalTopUpAmount, nextThreshold, nextTierName);
    }

    private static TransactionDto ToTransactionDto(TransactionEntity t) => new(
        t.Id, t.AccountId, t.Account?.FullName ?? (t.AccountId is null ? "Khách vãng lai" : string.Empty),
        t.Type, t.Method, t.Amount, t.Status,
        t.Note, t.ProofImageUrl, t.RelatedEntityType, t.RelatedEntityId, t.CreatedAtUtc,
        t.ProcessedByName, t.ProcessedAtUtc);

    private int? GetCurrentAccountId()
    {
        var claim = User.FindFirst("userId")?.Value;
        return int.TryParse(claim, out var id) ? id : null;
    }

    // GET api/wallet/me - Player only (Staff/Admin không có PlayerProfile).
    [HttpGet("me")]
    public async Task<ActionResult<WalletInfoDto>> GetMyWallet()
    {
        var accountId = GetCurrentAccountId();
        if (accountId is null) return Unauthorized();

        var profile = await _db.PlayerProfiles.FirstOrDefaultAsync(p => p.AccountId == accountId);
        if (profile is null)
        {
            return BadRequest("Tài khoản này không có hồ sơ Người chơi (Ví chỉ dành cho Người chơi).");
        }

        return ToWalletInfoDto(profile);
    }

    // GET api/wallet/transactions/me - bất kỳ role nào, chỉ xem giao dịch của chính mình.
    [HttpGet("transactions/me")]
    public async Task<ActionResult<List<TransactionDto>>> GetMyTransactions()
    {
        var accountId = GetCurrentAccountId();
        if (accountId is null) return Unauthorized();

        var list = await _db.Transactions
            .Include(t => t.Account)
            .Where(t => t.AccountId == accountId)
            .OrderByDescending(t => t.CreatedAtUtc)
            .ToListAsync();

        return list.Select(ToTransactionDto).ToList();
    }

    // POST api/wallet/topup - Player tạo yêu cầu nạp tiền (Cash/BankTransfer), chờ Staff/Admin duyệt.
    [HttpPost("topup")]
    public async Task<ActionResult<TransactionDto>> RequestTopUp(CreateTopUpRequestDto request)
    {
        var accountId = GetCurrentAccountId();
        if (accountId is null) return Unauthorized();

        var profile = await _db.PlayerProfiles.FirstOrDefaultAsync(p => p.AccountId == accountId);
        if (profile is null)
        {
            return BadRequest("Tài khoản này không có hồ sơ Người chơi (Ví chỉ dành cho Người chơi).");
        }

        if (request.Amount <= 0)
        {
            return BadRequest("Số tiền nạp phải lớn hơn 0.");
        }

        if (request.Method != "Cash" && request.Method != "BankTransfer")
        {
            return BadRequest("Phương thức nạp tiền chỉ có thể là Cash hoặc BankTransfer.");
        }

        var transaction = new TransactionEntity
        {
            AccountId = accountId.Value,
            Type = "TopUp",
            Method = request.Method,
            Amount = request.Amount,
            Status = "Pending",
            Note = request.Note,
            ProofImageUrl = request.ProofImageUrl,
            CreatedAtUtc = DateTime.UtcNow
        };

        _db.Transactions.Add(transaction);
        await _db.SaveChangesAsync();

        transaction.Account = await _db.Accounts.FindAsync(accountId.Value);
        var dto = ToTransactionDto(transaction);
        await _hub.Clients.All.SendAsync("TransactionCreated", dto);

        return CreatedAtAction(nameof(GetMyTransactions), null, dto);
    }

    // POST api/wallet/pay - Player thanh toán trực tiếp bằng số dư Ví, trừ tiền ngay.
    [HttpPost("pay")]
    public async Task<ActionResult<WalletInfoDto>> PayWithWallet(PayWithWalletRequestDto request)
    {
        var accountId = GetCurrentAccountId();
        if (accountId is null) return Unauthorized();

        var profile = await _db.PlayerProfiles.FirstOrDefaultAsync(p => p.AccountId == accountId);
        if (profile is null)
        {
            return BadRequest("Tài khoản này không có hồ sơ Người chơi (Ví chỉ dành cho Người chơi).");
        }

        if (request.Amount <= 0)
        {
            return BadRequest("Số tiền thanh toán phải lớn hơn 0.");
        }

        // Mua hàng đang bán (cơ, phụ kiện): máy chủ tự kiểm tra hàng còn bán, còn tồn và số tiền
        // đúng giá bán - không tin số tiền do máy khách gửi lên - rồi trừ 1 vào tồn kho.
        CueEntity? purchasedCue = null;
        if (request.RelatedEntityType == "CuePurchase")
        {
            purchasedCue = request.RelatedEntityId is int cueId ? await _db.Cues.FindAsync(cueId) : null;
            if (purchasedCue is null || !purchasedCue.IsForSale || purchasedCue.SalePrice is null)
            {
                return BadRequest("Sản phẩm này hiện không được bán.");
            }
            if (purchasedCue.AvailableCount <= 0)
            {
                return Conflict("Sản phẩm này đã hết hàng.");
            }
            if (request.Amount != purchasedCue.SalePrice.Value)
            {
                return BadRequest("Số tiền thanh toán không khớp giá bán.");
            }
        }

        if (profile.WalletBalance < request.Amount)
        {
            return BadRequest("Số dư không đủ.");
        }

        profile.WalletBalance -= request.Amount;
        if (purchasedCue is not null)
        {
            purchasedCue.AvailableCount -= 1;
        }

        var transaction = new TransactionEntity
        {
            AccountId = accountId.Value,
            Type = "Payment",
            Method = "Wallet",
            Amount = request.Amount,
            Status = "Approved",
            Note = request.Note,
            RelatedEntityType = request.RelatedEntityType,
            RelatedEntityId = request.RelatedEntityId,
            CreatedAtUtc = DateTime.UtcNow,
            ProcessedByAccountId = accountId.Value,
            ProcessedByName = "Tự động (thanh toán Ví)",
            ProcessedAtUtc = DateTime.UtcNow
        };

        _db.Transactions.Add(transaction);
        await _db.SaveChangesAsync();

        transaction.Account = await _db.Accounts.FindAsync(accountId.Value);
        await _hub.Clients.All.SendAsync("TransactionCreated", ToTransactionDto(transaction));

        return ToWalletInfoDto(profile);
    }

    // GET api/wallet/transactions/pending - Staff/Admin: hàng đợi duyệt nạp tiền.
    [Authorize(Roles = "Staff,Admin")]
    [HttpGet("transactions/pending")]
    public async Task<ActionResult<List<TransactionDto>>> GetPendingTransactions()
    {
        var list = await _db.Transactions
            .Include(t => t.Account)
            .Where(t => t.Status == "Pending")
            .OrderBy(t => t.CreatedAtUtc)
            .ToListAsync();

        return list.Select(ToTransactionDto).ToList();
    }

    // POST api/wallet/transactions/{id}/process - Staff/Admin duyệt hoặc từ chối một giao dịch Pending.
    [Authorize(Roles = "Staff,Admin")]
    [HttpPost("transactions/{id:int}/process")]
    public async Task<ActionResult<TransactionDto>> ProcessTransaction(int id, ProcessTransactionRequestDto request)
    {
        var processorAccountId = GetCurrentAccountId();
        if (processorAccountId is null) return Unauthorized();

        var transaction = await _db.Transactions.Include(t => t.Account).FirstOrDefaultAsync(t => t.Id == id);
        if (transaction is null) return NotFound();

        if (transaction.Status != "Pending")
        {
            return BadRequest("Giao dịch này đã được xử lý.");
        }

        var processorName = User.FindFirst("fullName")?.Value ?? string.Empty;

        if (request.Approve)
        {
            if (transaction.Type == "TopUp")
            {
                var profile = await _db.PlayerProfiles.FirstOrDefaultAsync(p => p.AccountId == transaction.AccountId);
                if (profile is null)
                {
                    return BadRequest("Không tìm thấy hồ sơ Người chơi của tài khoản này.");
                }

                profile.WalletBalance += transaction.Amount;
                profile.TotalTopUpAmount += transaction.Amount;
                profile.MembershipTier = MembershipTiers.GetTierForTotal(profile.TotalTopUpAmount);
            }

            transaction.Status = "Approved";
        }
        else
        {
            transaction.Status = "Rejected";
        }

        transaction.Note = string.IsNullOrWhiteSpace(request.Note) ? transaction.Note : request.Note;
        transaction.ProcessedByAccountId = processorAccountId;
        transaction.ProcessedByName = processorName;
        transaction.ProcessedAtUtc = DateTime.UtcNow;

        await _db.SaveChangesAsync();

        var dto = ToTransactionDto(transaction);
        await _hub.Clients.All.SendAsync("TransactionCreated", dto);

        return dto;
    }

    // GET api/wallet/transactions/all - Admin: báo cáo toàn bộ giao dịch, lọc theo status/method/type.
    [Authorize(Roles = "Admin")]
    [HttpGet("transactions/all")]
    public async Task<ActionResult<List<TransactionDto>>> GetAllTransactions(
        [FromQuery] string? status, [FromQuery] string? method, [FromQuery] string? type = null)
    {
        var query = _db.Transactions.Include(t => t.Account).AsQueryable();

        if (!string.IsNullOrWhiteSpace(status))
        {
            query = query.Where(t => t.Status == status);
        }

        if (!string.IsNullOrWhiteSpace(method))
        {
            query = query.Where(t => t.Method == method);
        }

        // Đợt 6: cho phép tách báo cáo "Nạp tiền" (TopUp) và "Thanh toán" (Payment) thành
        // hai trang riêng ở Admin thay vì gộp chung như trước.
        if (!string.IsNullOrWhiteSpace(type))
        {
            query = query.Where(t => t.Type == type);
        }

        var list = await query.OrderByDescending(t => t.CreatedAtUtc).ToListAsync();
        return list.Select(ToTransactionDto).ToList();
    }

    // GET api/wallet/find-member - Staff/Admin: tìm hội viên (Player) theo SĐT/email/tên để
    // chọn thanh toán bằng Ví, hoặc gắn (tùy chọn) vào giao dịch Cash/BankTransfer.
    [Authorize(Roles = "Staff,Admin")]
    // ===== Tích điểm hội viên =====

    private async Task<PointsInfoDto> GetPointsAsync(int accountId)
    {
        var totalSpent = await _db.Transactions
            .Where(t => t.AccountId == accountId && t.Type == "Payment" && t.Status == "Approved")
            .SumAsync(t => (decimal?)t.Amount) ?? 0m;
        var redeemedAmount = await _db.Transactions
            .Where(t => t.AccountId == accountId && t.Type == LoyaltyPoints.RedeemTransactionType && t.Status == "Approved")
            .SumAsync(t => (decimal?)t.Amount) ?? 0m;
        var earned = LoyaltyPoints.EarnedFor(totalSpent);
        var redeemed = (int)(redeemedAmount / LoyaltyPoints.PointValue);
        return new PointsInfoDto(Math.Max(0, earned - redeemed), earned, redeemed, totalSpent);
    }

    [HttpGet("points/me")]
    public async Task<ActionResult<PointsInfoDto>> GetMyPoints()
    {
        var accountId = GetCurrentAccountId();
        if (accountId is null) return Unauthorized();
        return await GetPointsAsync(accountId.Value);
    }

    // Đổi điểm vào Ví (bội số 100 điểm, 100 điểm = 10.000đ).
    [HttpPost("points/redeem")]
    public async Task<ActionResult<PointsInfoDto>> RedeemPoints(RedeemPointsRequestDto request)
    {
        var accountId = GetCurrentAccountId();
        if (accountId is null) return Unauthorized();
        if (request.Points <= 0 || request.Points % LoyaltyPoints.RedeemStep != 0)
        {
            return BadRequest($"Số điểm đổi phải là bội số của {LoyaltyPoints.RedeemStep}.");
        }

        await using var dbTransaction = await _db.Database.BeginTransactionAsync();
        var profile = await _db.PlayerProfiles.FirstOrDefaultAsync(p => p.AccountId == accountId);
        if (profile is null) return BadRequest("Chỉ hội viên (Người chơi) mới tích và đổi được điểm.");

        var points = await GetPointsAsync(accountId.Value);
        if (request.Points > points.Balance)
        {
            return BadRequest("Không đủ điểm để đổi.");
        }

        var amount = request.Points * LoyaltyPoints.PointValue;
        profile.WalletBalance += amount;
        var transaction = new TransactionEntity
        {
            AccountId = accountId.Value,
            Type = LoyaltyPoints.RedeemTransactionType,
            Method = "Points",
            Amount = amount,
            Status = "Approved",
            Note = $"Đổi {request.Points} điểm vào Ví",
            CreatedAtUtc = DateTime.UtcNow,
            ProcessedByAccountId = accountId.Value,
            ProcessedByName = "Tự động (đổi điểm)",
            ProcessedAtUtc = DateTime.UtcNow
        };
        _db.Transactions.Add(transaction);
        await _db.SaveChangesAsync();
        await dbTransaction.CommitAsync();

        transaction.Account = await _db.Accounts.FindAsync(accountId.Value);
        await _hub.Clients.All.SendAsync("TransactionCreated", ToTransactionDto(transaction));
        return await GetPointsAsync(accountId.Value);
    }

    // ===== Mã QR hội viên =====
    // Mã dạng "BMS1.{accountId}.{hết hạn unix}.{chữ ký}" - chữ ký HMAC bằng khóa bí mật của máy
    // chủ nên không giả được; hết hạn sau 10 phút nên ảnh chụp màn hình cũ không dùng lại được.

    private string SignMemberQr(int accountId, long expiresUnix) =>
        Convert.ToBase64String(HMACSHA256.HashData(_qrKey, Encoding.UTF8.GetBytes($"{accountId}.{expiresUnix}")))
            .TrimEnd('=').Replace('+', '-').Replace('/', '_');

    [HttpGet("member-qr")]
    public async Task<ActionResult<MemberQrDto>> GetMemberQr()
    {
        var accountId = GetCurrentAccountId();
        if (accountId is null) return Unauthorized();
        if (!await _db.PlayerProfiles.AnyAsync(p => p.AccountId == accountId))
        {
            return BadRequest("Chỉ hội viên (Người chơi) mới có mã QR hội viên.");
        }

        var expires = DateTime.UtcNow.Add(MemberQrLifetime);
        var unix = new DateTimeOffset(expires).ToUnixTimeSeconds();
        return new MemberQrDto($"BMS1.{accountId}.{unix}.{SignMemberQr(accountId.Value, unix)}", expires);
    }

    // Nhân viên quét QR -> xác thực chữ ký + hạn dùng -> trả thông tin hội viên.
    [Authorize(Roles = "Staff,Admin")]
    [HttpGet("member-by-qr")]
    public async Task<ActionResult<MemberInfoDto>> FindMemberByQr([FromQuery] string code)
    {
        var parts = (code ?? string.Empty).Trim().Split('.');
        if (parts.Length != 4 || parts[0] != "BMS1" || !int.TryParse(parts[1], out var accountId) || !long.TryParse(parts[2], out var unix))
        {
            return BadRequest("Mã QR không phải mã hội viên BMS.");
        }

        var expected = Encoding.UTF8.GetBytes(SignMemberQr(accountId, unix));
        if (!CryptographicOperations.FixedTimeEquals(expected, Encoding.UTF8.GetBytes(parts[3])))
        {
            return BadRequest("Mã QR không hợp lệ.");
        }
        if (DateTimeOffset.FromUnixTimeSeconds(unix) < DateTimeOffset.UtcNow)
        {
            return BadRequest("Mã QR đã hết hạn - nhờ khách mở lại mã mới trong Ví.");
        }

        var member = await _db.PlayerProfiles
            .Include(p => p.Account)
            .Where(p => p.AccountId == accountId && p.Account != null)
            .Select(p => new MemberInfoDto(
                p.Account!.Id, p.Account.FullName, p.Account.Email, p.Account.PhoneNumber,
                p.MembershipTier, p.WalletBalance, p.TotalTopUpAmount))
            .FirstOrDefaultAsync();
        return member is null ? NotFound("Không tìm thấy hội viên.") : member;
    }

    [Authorize(Roles = "Staff,Admin")]
    [HttpGet("find-member")]
    public async Task<ActionResult<List<MemberInfoDto>>> FindMember([FromQuery] string? query)
    {
        var q = _db.PlayerProfiles
            .Include(p => p.Account)
            .Where(p => p.Account != null && p.Account.Role == "Player");

        if (!string.IsNullOrWhiteSpace(query))
        {
            q = q.Where(p =>
                p.Account!.FullName.Contains(query) ||
                (p.Account.PhoneNumber ?? "").Contains(query) ||
                p.Account.Email.Contains(query));
        }

        var list = await q
            .OrderByDescending(p => p.TotalTopUpAmount)
            .Take(10)
            .Select(p => new MemberInfoDto(
                p.Account!.Id, p.Account.FullName, p.Account.Email, p.Account.PhoneNumber,
                p.MembershipTier, p.WalletBalance, p.TotalTopUpAmount))
            .ToListAsync();

        return list;
    }

    // GET api/wallet/members - Admin: danh sách hội viên (Player) kèm Ví/hạng.
    [Authorize(Roles = "Admin")]
    [HttpGet("members")]
    public async Task<ActionResult<List<MemberInfoDto>>> GetMembers()
    {
        var list = await _db.PlayerProfiles
            .Include(p => p.Account)
            .Where(p => p.Account != null && p.Account.Role == "Player")
            .OrderByDescending(p => p.TotalTopUpAmount)
            .Select(p => new MemberInfoDto(
                p.Account!.Id, p.Account.FullName, p.Account.Email, p.Account.PhoneNumber,
                p.MembershipTier, p.WalletBalance, p.TotalTopUpAmount, p.Note))
            .ToListAsync();

        return list;
    }

    // PUT api/wallet/members/{accountId}/note - Staff/Admin: cập nhật ghi chú nội bộ về hội viên.
    [Authorize(Roles = "Staff,Admin")]
    [HttpPut("members/{accountId:int}/note")]
    public async Task<IActionResult> UpdateMemberNote(int accountId, UpdateMemberNoteRequestDto request)
    {
        var profile = await _db.PlayerProfiles.FirstOrDefaultAsync(p => p.AccountId == accountId);
        if (profile is null) return NotFound("Không tìm thấy hồ sơ hội viên.");

        profile.Note = request.Note;
        await _db.SaveChangesAsync();
        return NoContent();
    }

    private static HourPackageDto ToHourPackageDto(HourPackageEntity h) => new(
        h.Id, h.AccountId, h.PackageName, h.TotalHours, h.RemainingHours, h.PricePaid,
        h.PurchasedAtUtc, h.ExpiresAtUtc, h.Status);

    // GET api/wallet/hour-packages/by-account/{accountId} - Staff/Admin: danh sách gói giờ
    // trả trước của một hội viên.
    [Authorize(Roles = "Staff,Admin")]
    [HttpGet("hour-packages/by-account/{accountId:int}")]
    public async Task<ActionResult<List<HourPackageDto>>> GetHourPackages(int accountId)
    {
        var list = await _db.HourPackages
            .Where(h => h.AccountId == accountId)
            .OrderByDescending(h => h.PurchasedAtUtc)
            .ToListAsync();
        return list.Select(ToHourPackageDto).ToList();
    }

    // POST api/wallet/hour-packages - Staff/Admin: bán một gói giờ trả trước cho hội viên.
    [Authorize(Roles = "Staff,Admin")]
    [HttpPost("hour-packages")]
    public async Task<ActionResult<HourPackageDto>> CreateHourPackage(CreateHourPackageRequestDto request)
    {
        if (request.TotalHours <= 0) return BadRequest("Số giờ phải lớn hơn 0.");

        var accountExists = await _db.Accounts.AnyAsync(a => a.Id == request.AccountId);
        if (!accountExists) return BadRequest("Không tìm thấy tài khoản hội viên.");

        var package = new HourPackageEntity
        {
            AccountId = request.AccountId,
            PackageName = request.PackageName,
            TotalHours = request.TotalHours,
            RemainingHours = request.TotalHours,
            PricePaid = request.PricePaid,
            PurchasedAtUtc = DateTime.UtcNow,
            ExpiresAtUtc = request.ExpiresAtUtc,
            Status = "Active"
        };

        _db.HourPackages.Add(package);
        await _db.SaveChangesAsync();

        return ToHourPackageDto(package);
    }

    // POST api/wallet/hour-packages/{id}/consume - Staff/Admin: trừ giờ khi hội viên sử dụng
    // gói giờ trả trước tại quầy.
    [Authorize(Roles = "Staff,Admin")]
    [HttpPost("hour-packages/{id:int}/consume")]
    public async Task<ActionResult<HourPackageDto>> ConsumeHourPackage(int id, ConsumeHourPackageRequestDto request)
    {
        var package = await _db.HourPackages.FindAsync(id);
        if (package is null) return NotFound();
        if (package.Status != "Active") return Conflict("Gói giờ này không còn hiệu lực.");
        if (request.Hours <= 0) return BadRequest("Số giờ trừ phải lớn hơn 0.");
        if (request.Hours > package.RemainingHours) return BadRequest("Gói giờ không đủ số giờ còn lại.");

        package.RemainingHours -= request.Hours;
        if (package.RemainingHours <= 0) package.Status = "UsedUp";

        await _db.SaveChangesAsync();
        return ToHourPackageDto(package);
    }
}
