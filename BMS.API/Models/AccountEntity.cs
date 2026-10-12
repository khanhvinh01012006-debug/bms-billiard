namespace BMS.API.Models;

public class AccountEntity
{
    public int Id { get; set; }
    public string FullName { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public byte[] PasswordHash { get; set; } = Array.Empty<byte>();
    public byte[] PasswordSalt { get; set; } = Array.Empty<byte>();

    // "Admin" | "Staff" | "Player"
    public string Role { get; set; } = "Player";
    public DateTime CreatedAtUtc { get; set; }

    // Chỉ tài khoản Staff mới có mã nhân viên (dùng để đăng nhập bằng Mã nhân viên + PIN
    // thay vì Email + mật khẩu). Admin/Player luôn null và vẫn đăng nhập bằng Email như cũ.
    public string? EmployeeCode { get; set; }

    public string? PhoneNumber { get; set; }
    public bool IsPhoneVerified { get; set; }
    public string? Address { get; set; }

    // Forgot-password flow: only a HASH of the reset token is stored, never the plaintext.
    public string? PasswordResetTokenHash { get; set; }
    public DateTime? PasswordResetTokenExpiresAtUtc { get; set; }

    // Phone verification (OTP) flow: only a HASH of the OTP is stored, never the plaintext.
    public string? PhoneOtpCodeHash { get; set; }
    public DateTime? PhoneOtpExpiresAtUtc { get; set; }

    // Đợt 9: hạn mức % giảm giá nhân viên được tự duyệt khi thanh toán (chỉ áp dụng Role=Staff).
    // Null/0 nghĩa là không được tự giảm giá, mọi yêu cầu đều phải chờ Admin duyệt.
    public decimal? DiscountLimitPercent { get; set; }
    // Đợt 9: lương theo giờ, dùng để ước tính lương từ bảng chấm công (chỉ áp dụng Role=Staff).
    public decimal? WageRatePerHour { get; set; }
}
