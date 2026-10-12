namespace BMS.Shared.Dtos;

// Cấu hình chung của quán (Admin sửa trong trang "Cấu hình chung"). Lưu ở file settings.json
// trong thư mục dữ liệu của API - không đổi cấu trúc database. Giá trị mặc định = thông tin
// đang dùng trước khi có trang cấu hình.
public record ClubSettingsDto(
    string ClubName = "BMS Billiard Club",
    string? Hotline = "0703 301 266",
    string? Email = "khanhvinh.260800@gmail.com",
    string? FacebookUrl = "https://www.facebook.com/khanh.vinh.589100",
    string? Address = null,
    string? Website = null,
    // VietQR: mã BIN ngân hàng (6 số, 970423 = TPBank), số tài khoản và tên chủ tài khoản.
    string? BankBin = "970423",
    string? BankAccountNumber = "08638025879",
    string? BankAccountName = "CHU KHANH VINH");
