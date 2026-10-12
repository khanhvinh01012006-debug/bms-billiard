using System.Text.Json;
using Microsoft.JSInterop;

namespace BMS.PlayerWeb.Services;

/// <summary>
/// "Đặt bàn của tôi": nhớ các lượt đặt bàn vừa tạo trên trình duyệt này (mã đặt + mã hủy do
/// máy chủ ký) để khách xem trạng thái và tự hủy. Chỉ là tiện ích phía trình duyệt: mất dữ liệu
/// (xóa lịch sử, chế độ riêng tư) thì khách nhờ nhân viên hủy như trước.
/// </summary>
public class MyBookingsService
{
    private const string StorageKey = "bms.myBookings";
    private static readonly TimeSpan KeepFor = TimeSpan.FromDays(1);
    private readonly IJSRuntime _js;

    public MyBookingsService(IJSRuntime js)
    {
        _js = js;
    }

    public record Entry(int Id, string CancelToken, string TableName, DateTime CreatedAtUtc);

    public async Task<List<Entry>> LoadAsync()
    {
        try
        {
            var json = await _js.InvokeAsync<string?>("localStorage.getItem", StorageKey);
            var list = string.IsNullOrEmpty(json) ? new List<Entry>() : JsonSerializer.Deserialize<List<Entry>>(json) ?? new();
            // Bỏ các lượt đặt quá 1 ngày.
            return list.Where(e => DateTime.UtcNow - e.CreatedAtUtc < KeepFor).ToList();
        }
        catch
        {
            return new List<Entry>();
        }
    }

    public async Task AddAsync(Entry entry)
    {
        var list = await LoadAsync();
        list.RemoveAll(e => e.Id == entry.Id);
        list.Insert(0, entry);
        await SaveAsync(list);
    }

    public async Task RemoveAsync(int id)
    {
        var list = await LoadAsync();
        list.RemoveAll(e => e.Id == id);
        await SaveAsync(list);
    }

    // Gọi khi đăng xuất: máy dùng chung thì người sau không thấy / hủy được lượt đặt của người trước.
    public async Task ClearAsync()
    {
        try
        {
            await _js.InvokeVoidAsync("localStorage.removeItem", StorageKey);
        }
        catch
        {
            // Trình duyệt chặn lưu trữ - không có gì để xóa.
        }
    }

    private async Task SaveAsync(List<Entry> list)
    {
        try
        {
            await _js.InvokeVoidAsync("localStorage.setItem", StorageKey, JsonSerializer.Serialize(list));
        }
        catch
        {
            // Trình duyệt chặn lưu trữ (chế độ riêng tư...) - bỏ qua, chỉ mất tính năng tự hủy.
        }
    }
}
