using System.Security.Cryptography;
using System.Text;
using BMS.API.Data;
using BMS.API.Models;
using BMS.API.Services;
using BMS.Shared.Dtos;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace BMS.API.Controllers;

// EP-01: US-001 Đăng ký, US-002 Đăng nhập, US-003 Đăng xuất (client-side token discard),
// US-004 Quản lý thông tin cá nhân, US-005 Phân quyền (Role claim used by [Authorize]).
// Also: quên mật khẩu, xác thực OTP số điện thoại, và US-018 (Admin tạo tài khoản nhân viên).
[ApiController]
[Route("api/accounts")]
public class AccountsController : ControllerBase
{
    private readonly BmsDbContext _db;
    private readonly JwtTokenService _jwtTokenService;
    private readonly IEmailSender _emailSender;
    private readonly IOtpSender _otpSender;
    private readonly IConfiguration _configuration;
    private readonly IWebHostEnvironment _environment;

    public AccountsController(
        BmsDbContext db,
        JwtTokenService jwtTokenService,
        IEmailSender emailSender,
        IOtpSender otpSender,
        IConfiguration configuration,
        IWebHostEnvironment environment)
    {
        _db = db;
        _jwtTokenService = jwtTokenService;
        _emailSender = emailSender;
        _otpSender = otpSender;
        _configuration = configuration;
        _environment = environment;
    }

    // Chỉ trả mã đặt lại mật khẩu trong response khi chạy Development (máy lập trình) và chưa
    // cấu hình SMTP. Trên máy chủ thật (Production) KHÔNG BAO GIỜ trả mã - nếu không, ai biết
    // email (vd. admin) cũng tự đặt lại được mật khẩu người khác. Khi chưa có SMTP, mã chỉ
    // được ghi vào log máy chủ (DevEmailSender).
    private bool IsDevMode =>
        _environment.IsDevelopment()
        && !string.Equals(_configuration["Email:Provider"], "Smtp", StringComparison.OrdinalIgnoreCase);

    // US-001: Đăng ký (luôn tạo tài khoản Role = Player, công khai/anonymous)
    [HttpPost("register")]
    public async Task<ActionResult<LoginResponseDto>> Register(RegisterRequestDto request)
    {
        if (string.IsNullOrWhiteSpace(request.Email) || string.IsNullOrWhiteSpace(request.Password))
        {
            return BadRequest("Vui lòng nhập email và mật khẩu.");
        }

        if (string.IsNullOrWhiteSpace(request.FullName))
        {
            return BadRequest("Họ và tên không được để trống.");
        }

        if (!request.Email.Contains('@'))
        {
            return BadRequest("Email không đúng định dạng.");
        }

        if (request.Password.Length < 6)
        {
            return BadRequest("Mật khẩu phải có ít nhất 6 ký tự.");
        }

        var normalizedEmail = request.Email.Trim().ToLowerInvariant();

        var exists = await _db.Accounts.AnyAsync(a => a.Email == normalizedEmail);
        if (exists)
        {
            return Conflict("Email này đã được dùng cho một tài khoản khác.");
        }

        var (hash, salt) = PasswordHasher.HashPassword(request.Password);

        var account = new AccountEntity
        {
            FullName = request.FullName,
            Email = normalizedEmail,
            PasswordHash = hash,
            PasswordSalt = salt,
            Role = "Player",
            PhoneNumber = string.IsNullOrWhiteSpace(request.PhoneNumber) ? null : request.PhoneNumber.Trim(),
            CreatedAtUtc = DateTime.UtcNow
        };

        _db.Accounts.Add(account);
        await _db.SaveChangesAsync();

        // Tự động tạo hồ sơ ghép trận (ELO) liên kết với tài khoản Player vừa tạo,
        // để hệ thống ghép trận có thể dùng ngay mà không cần bước thủ công riêng.
        _db.PlayerProfiles.Add(new PlayerProfileEntity
        {
            AccountId = account.Id,
            DisplayName = account.FullName,
            EloRating = request.StartingElo ?? 1000
        });
        await _db.SaveChangesAsync();

        var token = _jwtTokenService.GenerateToken(account);
        return new LoginResponseDto(token, account.Role, account.FullName);
    }

    // US-002: Đăng nhập. LoginRequestDto.Email vẫn giữ tên field như cũ để không phá các
    // call site hiện có (BMS.PlayerApp, AdminStaffWeb, PlayerWeb) - nhưng nội dung của nó
    // giờ là một "identifier" chung: Email (Player/Admin/Staff-cũ) HOẶC Mã nhân viên
    // (Staff đăng nhập bằng Mã nhân viên + PIN, không cần email).
    [HttpPost("login")]
    public async Task<ActionResult<LoginResponseDto>> Login(LoginRequestDto request)
    {
        if (string.IsNullOrWhiteSpace(request.Email))
        {
            return BadRequest(new { message = "Vui lòng nhập email hoặc mã nhân viên." });
        }

        var identifier = request.Email.Trim();

        AccountEntity? account;
        if (identifier.Contains('@'))
        {
            var normalizedEmail = identifier.ToLowerInvariant();
            account = await _db.Accounts.FirstOrDefaultAsync(a => a.Email == normalizedEmail);
        }
        else
        {
            var normalizedCode = identifier.ToUpperInvariant();
            account = await _db.Accounts.FirstOrDefaultAsync(a => a.EmployeeCode == normalizedCode && a.Role == "Staff");
        }

        // Một thông báo chung cho cả hai trường hợp (sai tài khoản / sai mật khẩu) để không
        // lộ email hoặc mã nhân viên nào đang tồn tại.
        if (account is null
            || !PasswordHasher.VerifyPassword(request.Password ?? string.Empty, account.PasswordHash, account.PasswordSalt))
        {
            return Unauthorized(new { message = "Thông tin đăng nhập không đúng." });
        }

        var token = _jwtTokenService.GenerateToken(account);
        return new LoginResponseDto(token, account.Role, account.FullName);
    }

    // Quên mật khẩu: sinh token reset, gửi qua IEmailSender. Luôn trả thông báo thành công
    // chung chung (kể cả email không tồn tại) để tránh lộ email nào có tài khoản. Ở chế độ
    // Dev, trả kèm DebugToken thật trong response để test được mà không cần đọc log/email.
    [HttpPost("forgot-password")]
    public async Task<ActionResult<ForgotPasswordResponseDto>> ForgotPassword(ForgotPasswordRequestDto request)
    {
        const string genericMessage = "Nếu email tồn tại trong hệ thống, hướng dẫn đặt lại mật khẩu đã được gửi.";

        if (string.IsNullOrWhiteSpace(request.Email))
        {
            return BadRequest("Vui lòng nhập email.");
        }

        var normalizedEmail = request.Email.Trim().ToLowerInvariant();
        var account = await _db.Accounts.FirstOrDefaultAsync(a => a.Email == normalizedEmail);

        if (account is null)
        {
            return new ForgotPasswordResponseDto(genericMessage, null);
        }

        var token = GenerateRandomToken();
        account.PasswordResetTokenHash = HashToken(token);
        account.PasswordResetTokenExpiresAtUtc = DateTime.UtcNow.AddMinutes(15);
        await _db.SaveChangesAsync();

        await _emailSender.SendAsync(
            account.Email,
            "BMS - Đặt lại mật khẩu",
            $"Mã đặt lại mật khẩu của bạn là: {token}. Mã có hiệu lực trong 15 phút.");

        return new ForgotPasswordResponseDto(genericMessage, IsDevMode ? token : null);
    }

    // Đặt lại mật khẩu bằng token nhận được từ forgot-password.
    [HttpPost("reset-password")]
    public async Task<IActionResult> ResetPassword(ResetPasswordRequestDto request)
    {
        if (string.IsNullOrWhiteSpace(request.Email) || string.IsNullOrWhiteSpace(request.Token) ||
            string.IsNullOrWhiteSpace(request.NewPassword))
        {
            return BadRequest("Email, token và mật khẩu mới đều bắt buộc.");
        }

        if (request.NewPassword.Length < 6)
        {
            return BadRequest("Mật khẩu phải có ít nhất 6 ký tự.");
        }

        var normalizedEmail = request.Email.Trim().ToLowerInvariant();
        var account = await _db.Accounts.FirstOrDefaultAsync(a => a.Email == normalizedEmail);

        var tokenHash = HashToken(request.Token);
        var isValid = account is not null &&
            account.PasswordResetTokenHash is not null &&
            account.PasswordResetTokenExpiresAtUtc is not null &&
            account.PasswordResetTokenExpiresAtUtc > DateTime.UtcNow &&
            CryptographicOperations.FixedTimeEquals(
                Encoding.UTF8.GetBytes(account.PasswordResetTokenHash),
                Encoding.UTF8.GetBytes(tokenHash));

        if (!isValid || account is null)
        {
            return BadRequest("Token không hợp lệ hoặc đã hết hạn.");
        }

        var (hash, salt) = PasswordHasher.HashPassword(request.NewPassword);
        account.PasswordHash = hash;
        account.PasswordSalt = salt;
        account.PasswordResetTokenHash = null;
        account.PasswordResetTokenExpiresAtUtc = null;
        await _db.SaveChangesAsync();

        return Ok(new { message = "Đặt lại mật khẩu thành công." });
    }

    // Gửi OTP xác thực số điện thoại cho tài khoản đang đăng nhập.
    [Authorize]
    [HttpPost("send-phone-otp")]
    public async Task<ActionResult<SendPhoneOtpResponseDto>> SendPhoneOtp(SendPhoneOtpRequestDto request)
    {
        if (string.IsNullOrWhiteSpace(request.PhoneNumber))
        {
            return BadRequest("Số điện thoại không được để trống.");
        }

        var account = await GetCurrentAccountAsync();
        if (account is null) return Unauthorized();

        account.PhoneNumber = request.PhoneNumber.Trim();

        var otp = GenerateRandomOtp();
        account.PhoneOtpCodeHash = HashToken(otp);
        account.PhoneOtpExpiresAtUtc = DateTime.UtcNow.AddMinutes(5);
        await _db.SaveChangesAsync();

        await _otpSender.SendAsync(account.PhoneNumber, otp);

        // OTP chỉ trả về cho CHÍNH tài khoản đang đăng nhập (chưa có nhà mạng SMS thật) - giữ như cũ.
        var returnOtp = !string.Equals(_configuration["Email:Provider"], "Smtp", StringComparison.OrdinalIgnoreCase);
        return new SendPhoneOtpResponseDto("Mã OTP đã được gửi.", returnOtp ? otp : null);
    }

    // Xác thực OTP vừa gửi.
    [Authorize]
    [HttpPost("verify-phone-otp")]
    public async Task<IActionResult> VerifyPhoneOtp(VerifyPhoneOtpRequestDto request)
    {
        if (string.IsNullOrWhiteSpace(request.Otp))
        {
            return BadRequest("Mã OTP không được để trống.");
        }

        var account = await GetCurrentAccountAsync();
        if (account is null) return Unauthorized();

        var otpHash = HashToken(request.Otp.Trim());
        var isValid = account.PhoneOtpCodeHash is not null &&
            account.PhoneOtpExpiresAtUtc is not null &&
            account.PhoneOtpExpiresAtUtc > DateTime.UtcNow &&
            CryptographicOperations.FixedTimeEquals(
                Encoding.UTF8.GetBytes(account.PhoneOtpCodeHash),
                Encoding.UTF8.GetBytes(otpHash));

        if (!isValid)
        {
            return BadRequest("Mã OTP không đúng hoặc đã hết hạn.");
        }

        account.IsPhoneVerified = true;
        account.PhoneOtpCodeHash = null;
        account.PhoneOtpExpiresAtUtc = null;
        await _db.SaveChangesAsync();

        return Ok(new { message = "Xác thực số điện thoại thành công." });
    }

    // US-004: Xem thông tin cá nhân
    [Authorize]
    [HttpGet("profile")]
    public async Task<ActionResult<ProfileDto>> GetProfile()
    {
        var account = await GetCurrentAccountAsync();
        // 401, not 404: the caller IS authenticated (passed [Authorize]) but their token's
        // identity couldn't be resolved to an account (e.g. stale token after a DB reset).
        // 404 here used to leak a raw "Not Found" ProblemDetails JSON into the Hồ sơ page.
        if (account is null) return Unauthorized();

        var playerProfile = await _db.PlayerProfiles.FirstOrDefaultAsync(p => p.AccountId == account.Id);

        return new ProfileDto(
            account.FullName,
            account.Email,
            account.PhoneNumber,
            account.IsPhoneVerified,
            account.Address,
            playerProfile?.EloRating,
            playerProfile?.DisplayName,
            playerProfile?.Id);
    }

    // US-004: Cập nhật thông tin cá nhân
    [Authorize]
    [HttpPut("profile")]
    public async Task<IActionResult> UpdateProfile(UpdateProfileRequestDto request)
    {
        var account = await GetCurrentAccountAsync();
        if (account is null) return Unauthorized();

        account.FullName = request.FullName;
        account.Address = request.Address;
        await _db.SaveChangesAsync();

        return NoContent();
    }

    // US-018: Admin tạo tài khoản nhân viên. Đây là CÁCH DUY NHẤT để có tài khoản Role = Staff
    // (không có endpoint đăng ký Staff công khai). Nhân viên đăng nhập bằng Mã nhân viên +
    // PIN (không cần email thật) - Email nội bộ chỉ là placeholder để thỏa cột Email
    // NOT NULL/unique, không hiển thị cho nhân viên.
    [Authorize(Roles = "Admin")]
    [HttpPost("create-staff")]
    public async Task<ActionResult<CreateStaffResponseDto>> CreateStaff(CreateStaffRequestDto request)
    {
        if (string.IsNullOrWhiteSpace(request.FullName))
        {
            return BadRequest("Họ và tên không được để trống.");
        }

        if (string.IsNullOrWhiteSpace(request.Pin) || !System.Text.RegularExpressions.Regex.IsMatch(request.Pin, "^\\d{4,6}$"))
        {
            return BadRequest("Mã PIN phải gồm 4-6 chữ số.");
        }

        string employeeCode;
        if (!string.IsNullOrWhiteSpace(request.EmployeeCode))
        {
            employeeCode = request.EmployeeCode.Trim().ToUpperInvariant();
            var taken = await _db.Accounts.AnyAsync(a => a.EmployeeCode == employeeCode);
            if (taken)
            {
                return Conflict("Mã nhân viên này đã được sử dụng.");
            }
        }
        else
        {
            employeeCode = await GenerateNextEmployeeCodeAsync();
        }

        var syntheticEmail = $"{employeeCode.ToLowerInvariant()}@staff.bms.local";

        var (hash, salt) = PasswordHasher.HashPassword(request.Pin);

        var account = new AccountEntity
        {
            FullName = request.FullName,
            Email = syntheticEmail,
            PasswordHash = hash,
            PasswordSalt = salt,
            Role = "Staff",
            EmployeeCode = employeeCode,
            PhoneNumber = string.IsNullOrWhiteSpace(request.PhoneNumber) ? null : request.PhoneNumber.Trim(),
            CreatedAtUtc = DateTime.UtcNow
        };

        _db.Accounts.Add(account);
        await _db.SaveChangesAsync();

        return CreatedAtAction(nameof(GetStaffList), null,
            new CreateStaffResponseDto(account.Id, account.FullName, employeeCode, account.PhoneNumber, account.CreatedAtUtc));
    }

    // Sinh mã nhân viên tự động dạng "NV" + 4 chữ số, dựa trên số lớn nhất hiện có, với
    // retry đơn giản để phòng va chạm (thao tác Admin, tần suất thấp).
    private async Task<string> GenerateNextEmployeeCodeAsync()
    {
        var existingCodes = await _db.Accounts
            .Where(a => a.EmployeeCode != null)
            .Select(a => a.EmployeeCode!)
            .ToListAsync();

        var maxSuffix = 0;
        foreach (var code in existingCodes)
        {
            if (code.Length > 2 && code.StartsWith("NV", StringComparison.OrdinalIgnoreCase) &&
                int.TryParse(code.Substring(2), out var suffix))
            {
                maxSuffix = Math.Max(maxSuffix, suffix);
            }
        }

        for (var attempt = 1; attempt <= 1000; attempt++)
        {
            var candidate = "NV" + (maxSuffix + attempt).ToString("D4");
            var taken = await _db.Accounts.AnyAsync(a => a.EmployeeCode == candidate);
            if (!taken)
            {
                return candidate;
            }
        }

        // Cực kỳ khó xảy ra (1000 lần đụng liên tiếp) - fallback dùng timestamp để vẫn duy nhất.
        return "NV" + DateTime.UtcNow.Ticks.ToString().Substring(0, 4);
    }

    // Danh sách nhân viên, dùng cho trang quản lý nhân viên của Admin.
    [Authorize(Roles = "Admin")]
    [HttpGet]
    public async Task<ActionResult<List<StaffAccountDto>>> GetStaffList([FromQuery] string? role)
    {
        var query = _db.Accounts.AsQueryable();
        if (!string.IsNullOrWhiteSpace(role))
        {
            query = query.Where(a => a.Role == role);
        }

        var list = await query
            .OrderByDescending(a => a.CreatedAtUtc)
            .Select(a => new StaffAccountDto(a.Id, a.FullName, a.Email, a.PhoneNumber, a.CreatedAtUtc, a.EmployeeCode,
                a.DiscountLimitPercent, a.WageRatePerHour))
            .ToListAsync();

        return list;
    }

    // Đợt 9: Admin cập nhật hạn mức giảm giá tự duyệt + lương/giờ cho một nhân viên.
    [Authorize(Roles = "Admin")]
    [HttpPut("{id:int}/staff-settings")]
    public async Task<IActionResult> UpdateStaffSettings(int id, UpdateStaffSettingsRequestDto request)
    {
        var account = await _db.Accounts.FindAsync(id);
        if (account is null || account.Role != "Staff") return NotFound("Không tìm thấy tài khoản nhân viên.");

        account.DiscountLimitPercent = request.DiscountLimitPercent;
        account.WageRatePerHour = request.WageRatePerHour;
        await _db.SaveChangesAsync();
        return NoContent();
    }

    private async Task<AccountEntity?> GetCurrentAccountAsync()
    {
        var subEmail = User.FindFirst(System.IdentityModel.Tokens.Jwt.JwtRegisteredClaimNames.Sub)?.Value;
        if (string.IsNullOrEmpty(subEmail)) return null;

        return await _db.Accounts.FirstOrDefaultAsync(a => a.Email == subEmail.ToLowerInvariant());
    }

    private static string GenerateRandomToken() => Guid.NewGuid().ToString("N") + Guid.NewGuid().ToString("N");

    private static string GenerateRandomOtp() => RandomNumberGenerator.GetInt32(0, 1_000_000).ToString("D6");

    private static string HashToken(string value)
    {
        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(value));
        return Convert.ToHexString(bytes);
    }
}
