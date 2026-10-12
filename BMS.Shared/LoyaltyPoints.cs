namespace BMS.Shared;

/// <summary>
/// Tích điểm hội viên (dùng chung API + giao diện). Điểm không lưu cột riêng mà tính từ lịch
/// sử giao dịch: điểm tích = tổng tiền các lần thanh toán đã hoàn tất của hội viên / 10.000đ;
/// điểm đã dùng = tổng tiền các lần "Đổi điểm" (giao dịch PointsRedeem) / giá trị 1 điểm.
/// </summary>
public static class LoyaltyPoints
{
    /// <summary>Mỗi 10.000đ thanh toán được 1 điểm.</summary>
    public const decimal SpendPerPoint = 10_000m;

    /// <summary>1 điểm = 100đ khi đổi vào Ví (100 điểm = 10.000đ).</summary>
    public const decimal PointValue = 100m;

    /// <summary>Đổi theo bội số 100 điểm.</summary>
    public const int RedeemStep = 100;

    public const string RedeemTransactionType = "PointsRedeem";

    public static int EarnedFor(decimal totalSpent) => (int)Math.Floor(totalSpent / SpendPerPoint);
}
