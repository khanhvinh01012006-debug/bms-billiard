using System.Net.Http.Headers;
using System.Net.Http.Json;
using BMS.Shared.Dtos;

namespace BMS.AdminStaffWeb.Services;

// Đợt 4: Gọi món & dịch vụ - order lines gắn với một phiên chơi (PlayingSession) đang Active.
public class OrderApiService
{
    private readonly HttpClient _http;
    private readonly AuthState _authState;

    public OrderApiService(HttpClient http, AuthState authState)
    {
        _http = http;
        _authState = authState;
    }

    private void ApplyAuthHeader()
    {
        _http.DefaultRequestHeaders.Authorization = _authState.Token is null
            ? null
            : new AuthenticationHeaderValue("Bearer", _authState.Token);
    }

    public async Task<List<InventoryItemDto>> GetQuickAddInventoryAsync()
    {
        try
        {
            ApplyAuthHeader();
            var result = await _http.GetFromJsonAsync<List<InventoryItemDto>>("api/sessions/inventory-quickadd");
            return result ?? new List<InventoryItemDto>();
        }
        catch
        {
            return new List<InventoryItemDto>();
        }
    }

    public async Task<List<OrderDto>> GetSessionOrdersAsync(int sessionId)
    {
        try
        {
            ApplyAuthHeader();
            var result = await _http.GetFromJsonAsync<List<OrderDto>>($"api/sessions/{sessionId}/orders");
            return result ?? new List<OrderDto>();
        }
        catch
        {
            return new List<OrderDto>();
        }
    }

    public async Task<(bool Success, string? Error)> AddOrderAsync(int sessionId, AddOrderRequestDto request)
    {
        try
        {
            ApplyAuthHeader();
            var response = await _http.PostAsJsonAsync($"api/sessions/{sessionId}/orders", request);
            if (response.IsSuccessStatusCode) return (true, null);
            return (false, await response.Content.ReadAsStringAsync());
        }
        catch (Exception ex)
        {
            return (false, $"Không thể kết nối tới máy chủ: {ex.Message}");
        }
    }

    public async Task<(bool Success, string? Error)> DeleteOrderAsync(int orderId)
    {
        try
        {
            ApplyAuthHeader();
            var response = await _http.DeleteAsync($"api/sessions/orders/{orderId}");
            if (response.IsSuccessStatusCode) return (true, null);
            return (false, await response.Content.ReadAsStringAsync());
        }
        catch (Exception ex)
        {
            return (false, $"Không thể kết nối tới máy chủ: {ex.Message}");
        }
    }
}
