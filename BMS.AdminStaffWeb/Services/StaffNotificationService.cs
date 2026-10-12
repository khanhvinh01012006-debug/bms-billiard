using Microsoft.AspNetCore.SignalR.Client;

namespace BMS.AdminStaffWeb.Services;

/// <summary>
/// Tracks unread-chat, pending-topup and pending cue-purchase counts for the Staff nav badges
/// ("Hộp thư" / "Duyệt nạp tiền" / "Yêu cầu mua cơ") so staff notice new work without having to
/// open each page and look. Connects to the same BmsHub the chat/tournament pages already use and
/// listens for "ChatMessageReceived" / "TransactionCreated" / "CuePurchaseRequestChanged".
/// </summary>
public class StaffNotificationService : IAsyncDisposable
{
    private readonly ChatApiService _chatApiService;
    private readonly WalletApiService _walletApiService;
    private readonly CueApiService _cueApiService;
    private readonly string _apiBaseUrl;

    private HubConnection? _hubConnection;
    private bool _started;

    public int UnreadChatCount { get; private set; }
    public int PendingTopUpCount { get; private set; }
    public int PendingPurchaseCount { get; private set; }

    public event Action? Changed;

    public StaffNotificationService(ChatApiService chatApiService, WalletApiService walletApiService, CueApiService cueApiService, IConfiguration configuration)
    {
        _chatApiService = chatApiService;
        _walletApiService = walletApiService;
        _cueApiService = cueApiService;
        _apiBaseUrl = (configuration["Api:BaseUrl"] ?? "https://localhost:5001").TrimEnd('/');
    }

    public async Task StartAsync()
    {
        if (_started) return;
        _started = true;

        await RefreshCountsAsync();

        _hubConnection = new HubConnectionBuilder()
            .WithUrl($"{_apiBaseUrl}/hubs/bms")
            .WithAutomaticReconnect(new ForeverRetryPolicy())
            .Build();

        _hubConnection.On<object>("ChatMessageReceived", async _ => await RefreshCountsAsync());
        _hubConnection.On<object>("TransactionCreated", async _ => await RefreshCountsAsync());
        _hubConnection.On<object>("CuePurchaseRequestChanged", async _ => await RefreshCountsAsync());
        // Nối lại sau khi mất mạng: đếm lại vì có thể đã lỡ sự kiện.
        _hubConnection.Reconnected += async _ => await RefreshCountsAsync();

        try
        {
            await _hubConnection.StartAsync();
        }
        catch
        {
            // Falls back to counts only updating on next navigation/refresh.
        }
    }

    public async ValueTask StopAsync()
    {
        _started = false;
        UnreadChatCount = 0;
        PendingTopUpCount = 0;
        PendingPurchaseCount = 0;

        if (_hubConnection is not null)
        {
            await _hubConnection.DisposeAsync();
            _hubConnection = null;
        }
    }

    // Đợt 16: cho trang Hộp thư gọi lại sau khi mở 1 cuộc trò chuyện (tin đã được đánh dấu đã đọc).
    public Task RefreshAsync() => RefreshCountsAsync();

    private async Task RefreshCountsAsync()
    {
        var threads = await _chatApiService.GetThreadsAsync();
        UnreadChatCount = threads.Sum(t => t.UnreadCount);

        var pending = await _walletApiService.GetPendingTransactionsAsync();
        PendingTopUpCount = pending.Count;

        var purchases = await _cueApiService.GetPurchaseRequestsAsync();
        PendingPurchaseCount = purchases.Count(r => r.Status == "Pending");

        Changed?.Invoke();
    }

    public async ValueTask DisposeAsync()
    {
        if (_hubConnection is not null)
        {
            await _hubConnection.DisposeAsync();
        }
    }
}
