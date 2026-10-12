namespace BMS.Shared;

// Đợt 3: Ngưỡng hạng hội viên dựa trên tổng số tiền đã nạp lũy kế (TotalTopUpAmount).
// Dùng chung ở cả BMS.API (tính lại hạng khi duyệt nạp tiền) và các client (hiển thị
// tiến độ lên hạng tiếp theo).
public static class MembershipTiers
{
    public const string Dong = "Đồng";
    public const string Bac = "Bạc";
    public const string Vang = "Vàng";
    public const string KimCuong = "Kim Cương";

    public const decimal BacThreshold = 2_000_000m;
    public const decimal VangThreshold = 5_000_000m;
    public const decimal KimCuongThreshold = 10_000_000m;

    public static string GetTierForTotal(decimal total)
    {
        if (total >= KimCuongThreshold) return KimCuong;
        if (total >= VangThreshold) return Vang;
        if (total >= BacThreshold) return Bac;
        return Dong;
    }

    // Ngưỡng và tên của hạng TIẾP THEO so với tổng hiện tại. Trả về (0, null) nếu đã ở
    // hạng cao nhất (Kim Cương).
    public static (decimal NextThreshold, string? NextTierName) GetNextTier(decimal total)
    {
        if (total < BacThreshold) return (BacThreshold, Bac);
        if (total < VangThreshold) return (VangThreshold, Vang);
        if (total < KimCuongThreshold) return (KimCuongThreshold, KimCuong);
        return (0m, null);
    }
}
