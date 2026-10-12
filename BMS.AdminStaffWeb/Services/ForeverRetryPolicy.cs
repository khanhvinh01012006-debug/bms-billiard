using Microsoft.AspNetCore.SignalR.Client;

namespace BMS.AdminStaffWeb.Services;

/// <summary>
/// Chính sách tự nối lại SignalR không bao giờ bỏ cuộc (mặc định của WithAutomaticReconnect()
/// dừng sau ~42 giây). Thử ngay, rồi 2s, 5s, 10s, sau đó cứ 30 giây một lần - để điện thoại
/// mất mạng / để app ở nền lâu vẫn tự cập nhật lại khi có mạng.
/// </summary>
public sealed class ForeverRetryPolicy : IRetryPolicy
{
    private static readonly TimeSpan[] Delays =
    {
        TimeSpan.Zero, TimeSpan.FromSeconds(2), TimeSpan.FromSeconds(5), TimeSpan.FromSeconds(10)
    };

    public TimeSpan? NextRetryDelay(RetryContext retryContext) =>
        retryContext.PreviousRetryCount < Delays.Length
            ? Delays[retryContext.PreviousRetryCount]
            : TimeSpan.FromSeconds(30);
}
