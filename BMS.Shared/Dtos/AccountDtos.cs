namespace BMS.Shared.Dtos;

// DTOs for account registration, login and profile management (EP-01: US-001..US-004)

// StartingElo: Đợt 5, "Trình độ chơi" chọn ở form đăng ký ("Mới tập" 800 / "Trung bình" 1000 /
// "Khá" 1200 / "Giỏi" 1500). Trailing optional để không phá các call site cũ (BMS.PlayerApp,
// AdminStaffWeb create-staff) - mặc định 1000 ở server khi không gửi lên.
public record RegisterRequestDto(string FullName, string Email, string Password, string? PhoneNumber = null, int? StartingElo = null);

public record LoginRequestDto(string Email, string Password);

public record LoginResponseDto(string Token, string Role, string FullName);

public record UpdateProfileRequestDto(string FullName, string? Address = null);

// Trả về từ GET api/accounts/profile. EloRating/DisplayName chỉ có khi tài khoản có
// PlayerProfile liên kết (Role = Player đã đăng ký) - đọc được nhưng không sửa qua API này.
public record ProfileDto(
    string FullName,
    string Email,
    string? PhoneNumber,
    bool IsPhoneVerified,
    string? Address,
    int? EloRating,
    string? PlayerDisplayName,
    int? PlayerProfileId = null);

// Quên mật khẩu (Dev mode trả kèm DebugToken để demo không cần dịch vụ email thật)
public record ForgotPasswordRequestDto(string Email);

public record ForgotPasswordResponseDto(string Message, string? DebugToken);

public record ResetPasswordRequestDto(string Email, string Token, string NewPassword);

// Xác thực số điện thoại bằng OTP (Dev mode trả kèm DebugOtp)
public record SendPhoneOtpRequestDto(string PhoneNumber);

public record SendPhoneOtpResponseDto(string Message, string? DebugOtp);

public record VerifyPhoneOtpRequestDto(string Otp);

// US-018: Admin tạo tài khoản nhân viên
// EmployeeCode: trailing optional để không phá call site cũ - null cho các tài khoản cũ
// hơn (Admin/Player) hoặc trước khi có tính năng đăng nhập bằng Mã nhân viên + PIN.
// Đợt 9: hạn mức % giảm giá tự duyệt + lương/giờ (trailing optional, chỉ có ý nghĩa với Role=Staff).
public record StaffAccountDto(int Id, string FullName, string Email, string? PhoneNumber, DateTime CreatedAtUtc, string? EmployeeCode = null,
    decimal? DiscountLimitPercent = null, decimal? WageRatePerHour = null);

// Đợt 9: Admin cập nhật hạn mức giảm giá tự duyệt + lương/giờ cho một nhân viên.
public record UpdateStaffSettingsRequestDto(decimal? DiscountLimitPercent, decimal? WageRatePerHour);

// Đăng nhập nhân viên bằng Mã nhân viên + PIN: Admin tạo tài khoản Staff không cần email
// thật, chỉ cần Họ tên + PIN (4-6 chữ số). EmployeeCode để trống => server tự sinh (NV0001,
// NV0002, ...).
public record CreateStaffRequestDto(string FullName, string Pin, string? PhoneNumber = null, string? EmployeeCode = null);

public record CreateStaffResponseDto(int Id, string FullName, string EmployeeCode, string? PhoneNumber, DateTime CreatedAtUtc);
