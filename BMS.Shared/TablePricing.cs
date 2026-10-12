namespace BMS.Shared;

/// <summary>
/// Tính tiền giờ chơi bàn - dùng chung cho API (chốt tiền khi trả bàn) và màn hình nhân viên
/// ("Tạm tính" trên thẻ bàn) để hai nơi luôn ra cùng một con số.
/// - Khung giờ của Loại bàn (VD 08:00-17:00 giá ngày, 17:00-02:00 giá tối) là giờ VIỆT NAM.
/// - Một phiên vắt qua nhiều khung giờ được chia đoạn và tính theo đơn giá từng khung; đoạn không
///   rơi vào khung nào tính theo giá cố định của bàn.
/// - Kết quả làm tròn đến 1.000đ gần nhất.
/// </summary>
public static class TablePricing
{
    public readonly record struct Slot(TimeSpan Start, TimeSpan End, decimal PricePerHour);

    /// <summary>Việt Nam dùng UTC+7 cố định quanh năm (không có giờ mùa hè).</summary>
    public static DateTime ToVietnamTime(DateTime utc) => utc + TimeSpan.FromHours(7);

    /// <summary>Làm tròn đến 1.000đ gần nhất (VD 136.403đ -> 136.000đ, 120.500đ -> 121.000đ).</summary>
    public static decimal RoundToThousand(decimal amount) =>
        Math.Round(amount / 1000m, 0, MidpointRounding.AwayFromZero) * 1000m;

    /// <summary>Tiền giờ (đã làm tròn) cho khoảng [startUtc, endUtc).</summary>
    public static decimal HourlyAmount(DateTime startUtc, DateTime endUtc, IReadOnlyList<Slot> slots, decimal fallbackPricePerHour)
    {
        if (endUtc <= startUtc) return 0m;

        var raw = slots.Count > 0
            ? SlotBasedAmount(ToVietnamTime(startUtc), ToVietnamTime(endUtc), slots, fallbackPricePerHour)
            : (decimal)(endUtc - startUtc).TotalHours * fallbackPricePerHour;
        return RoundToThousand(raw);
    }

    // start/end ở đây đã là giờ Việt Nam - chỉ đọc phần giờ trong ngày.
    private static decimal SlotBasedAmount(DateTime start, DateTime end, IReadOnlyList<Slot> slots, decimal fallbackPricePerHour)
    {
        decimal total = 0m;
        var cursor = start;

        while (cursor < end)
        {
            var timeOfDay = cursor.TimeOfDay;
            var matched = slots.Where(s => IsWithinSlot(timeOfDay, s.Start, s.End)).Cast<Slot?>().FirstOrDefault();

            DateTime nextBoundary;
            decimal rate;

            if (matched is Slot slot)
            {
                rate = slot.PricePerHour;
                nextBoundary = NextOccurrenceOfTimeOfDay(cursor, slot.End);
            }
            else
            {
                rate = fallbackPricePerHour;
                nextBoundary = slots.Min(s => NextOccurrenceOfTimeOfDay(cursor, s.Start));
            }

            var segmentEnd = nextBoundary < end ? nextBoundary : end;
            if (segmentEnd <= cursor) break; // safety net, should not happen

            total += (decimal)(segmentEnd - cursor).TotalHours * rate;
            cursor = segmentEnd;
        }

        return total;
    }

    // True when time-of-day `t` falls inside the half-open interval [start, end).
    // Handles a slot that wraps past midnight (start > end, e.g. 17:00-02:00).
    private static bool IsWithinSlot(TimeSpan t, TimeSpan start, TimeSpan end) =>
        start <= end ? (t >= start && t < end) : (t >= start || t < end);

    // Smallest DateTime strictly after `from` whose time-of-day component equals `timeOfDay`.
    private static DateTime NextOccurrenceOfTimeOfDay(DateTime from, TimeSpan timeOfDay)
    {
        var candidate = from.Date + timeOfDay;
        if (candidate <= from) candidate = candidate.AddDays(1);
        return candidate;
    }
}
