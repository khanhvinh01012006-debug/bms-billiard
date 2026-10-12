using BMS.API.Data;
using BMS.API.Hubs;
using BMS.API.Models;
using BMS.Shared.Dtos;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;

namespace BMS.API.Services;

/// <summary>
/// Quét định kỳ các Booking Status="Pending" quá hạn (ExpiresAtUtc &lt; UtcNow) và
/// chuyển sang "Expired", nhả bàn về Available nếu bàn chưa được ai giữ khác.
/// DbContext không thread-safe và không sống lâu được, nên mỗi lần chạy phải tạo một
/// scope mới (IServiceScopeFactory) để lấy một BmsDbContext riêng.
/// </summary>
public class BookingExpiryHostedService : BackgroundService
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<BookingExpiryHostedService> _logger;
    private static readonly TimeSpan Interval = TimeSpan.FromSeconds(45);

    public BookingExpiryHostedService(IServiceScopeFactory scopeFactory, ILogger<BookingExpiryHostedService> logger)
    {
        _scopeFactory = scopeFactory;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await ExpirePendingBookingsAsync(stoppingToken);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "BookingExpiryHostedService: lỗi khi quét booking quá hạn.");
            }

            try
            {
                await Task.Delay(Interval, stoppingToken);
            }
            catch (TaskCanceledException)
            {
                // Shutdown bình thường.
            }
        }
    }

    private async Task ExpirePendingBookingsAsync(CancellationToken stoppingToken)
    {
        using var scope = _scopeFactory.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<BmsDbContext>();
        var hub = scope.ServiceProvider.GetRequiredService<IHubContext<BmsHub>>();

        var now = DateTime.UtcNow;
        var expired = await db.Bookings
            .Include(b => b.Table)
            .Where(b => b.Status == "Pending" && b.ExpiresAtUtc < now)
            .ToListAsync(stoppingToken);

        if (expired.Count == 0) return;

        foreach (var booking in expired)
        {
            booking.Status = "Expired";

            if (booking.Table is not null && booking.Table.Status == TableStatus.Reserved)
            {
                booking.Table.Status = TableStatus.Available;
            }
        }

        await db.SaveChangesAsync(stoppingToken);

        foreach (var booking in expired)
        {
            await hub.Clients.All.SendAsync("BookingExpired",
                new BookingExpiredEvent(booking.Id, booking.TableId), stoppingToken);

            if (booking.Table is not null)
            {
                await hub.Clients.All.SendAsync("TableStatusChanged",
                    new TableStatusChangedEvent(booking.Table.Id, booking.Table.Status), stoppingToken);
            }
        }
    }
}
