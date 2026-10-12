namespace BMS.API.Models;

public class PlayerProfileEntity
{
    public int Id { get; set; }
    public string DisplayName { get; set; } = string.Empty;
    public int EloRating { get; set; } = 1000;
    public string? AvatarUrl { get; set; }

    // Liên kết tới tài khoản đăng nhập (Role = Player) đã tạo hồ sơ này, nếu có.
    // Null cho các hồ sơ giả lập seed sẵn (không có tài khoản thật đứng sau).
    public int? AccountId { get; set; }
    public AccountEntity? Account { get; set; }

    // Đợt 3: Ví/Hội viên. Số dư dùng để thanh toán trực tiếp (thuê bàn, mua cơ...);
    // TotalTopUpAmount là tổng nạp lũy kế dùng để xác định MembershipTier.
    public decimal WalletBalance { get; set; }
    public string MembershipTier { get; set; } = "Đồng";
    public decimal TotalTopUpAmount { get; set; }

    // Đợt 9: ghi chú nội bộ của nhân viên/Admin về hội viên này, Player không thấy được.
    public string? Note { get; set; }
}
