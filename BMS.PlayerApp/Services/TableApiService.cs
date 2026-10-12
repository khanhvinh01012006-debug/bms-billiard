using System.Net.Http.Headers;
using System.Net.Http.Json;
using BMS.Shared.Dtos;

namespace BMS.PlayerApp.Services;

public class TableApiService
{
    private readonly HttpClient _http;
    private readonly AuthState _authState;

    public TableApiService(ApiOptions options, AuthState authState)
    {
        _http = ApiHttpClientFactory.Create(options);
        _authState = authState;
    }

    private void ApplyAuthHeader()
    {
        _http.DefaultRequestHeaders.Authorization = _authState.Token is null
            ? null
            : new AuthenticationHeaderValue("Bearer", _authState.Token);
    }

    public async Task<List<TableDto>> GetTablesAsync()
    {
        var result = await _http.GetFromJsonAsync<List<TableDto>>("api/tables");
        return result ?? new List<TableDto>();
    }

    public async Task<(bool Success, string? Error)> CreateBookingAsync(int tableId, string customerName)
    {
        var response = await _http.PostAsJsonAsync("api/bookings", new CreateBookingRequestDto(tableId, customerName));
        if (response.IsSuccessStatusCode) return (true, null);
        return (false, await response.Content.ReadAsStringAsync());
    }

    // --- Staff operational actions (nhận/trả bàn) ---

    public async Task<(bool Success, string? Error)> CheckInAsync(int tableId, string customerName)
    {
        ApplyAuthHeader();
        var response = await _http.PostAsJsonAsync("api/sessions/checkin", new { TableId = tableId, CustomerName = customerName, BookingId = (int?)null });
        if (response.IsSuccessStatusCode) return (true, null);
        return (false, await response.Content.ReadAsStringAsync());
    }

    public async Task<(bool Success, CheckOutResultDto? Result, string? Error)> CheckOutAsync(int tableId)
    {
        ApplyAuthHeader();
        var response = await _http.PostAsJsonAsync("api/sessions/checkout", new { TableId = tableId });
        if (!response.IsSuccessStatusCode)
        {
            return (false, null, await response.Content.ReadAsStringAsync());
        }
        var result = await response.Content.ReadFromJsonAsync<CheckOutResultDto>();
        return (true, result, null);
    }
}
