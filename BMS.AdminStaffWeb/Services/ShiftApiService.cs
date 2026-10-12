using System.Net.Http.Headers;
using System.Net.Http.Json;
using BMS.Shared.Dtos;

namespace BMS.AdminStaffWeb.Services;

// Đợt 4: Bàn giao ca làm việc - Admin xem lịch sử toàn bộ ca.
public class ShiftApiService
{
    private readonly HttpClient _http;
    private readonly AuthState _authState;

    public ShiftApiService(HttpClient http, AuthState authState)
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

    public async Task<List<ShiftDto>> GetAllAsync(int? staffAccountId = null)
    {
        try
        {
            ApplyAuthHeader();
            var url = staffAccountId is int id ? $"api/shifts?staffAccountId={id}" : "api/shifts";
            var result = await _http.GetFromJsonAsync<List<ShiftDto>>(url);
            return result ?? new List<ShiftDto>();
        }
        catch
        {
            return new List<ShiftDto>();
        }
    }

    // Đợt 9: Dashboard - Lệch quỹ tiền mặt.
    public async Task<List<ShiftCashDiscrepancyDto>> GetCashDiscrepanciesAsync()
    {
        try
        {
            ApplyAuthHeader();
            var result = await _http.GetFromJsonAsync<List<ShiftCashDiscrepancyDto>>("api/shifts/cash-discrepancies");
            return result ?? new List<ShiftCashDiscrepancyDto>();
        }
        catch
        {
            return new List<ShiftCashDiscrepancyDto>();
        }
    }

    public async Task<(bool Success, string? Error)> EndShiftAsync(int shiftId, decimal closingCashAmount, string? handoverNote)
    {
        try
        {
            ApplyAuthHeader();
            var response = await _http.PostAsJsonAsync($"api/shifts/{shiftId}/end",
                new EndShiftRequestDto(closingCashAmount, handoverNote));
            if (response.IsSuccessStatusCode) return (true, null);
            return (false, await response.Content.ReadAsStringAsync());
        }
        catch (Exception ex)
        {
            return (false, $"Không thể kết nối tới máy chủ: {ex.Message}");
        }
    }

    public async Task<ShiftDto?> GetMyActiveShiftAsync()
    {
        try
        {
            ApplyAuthHeader();
            var response = await _http.GetAsync("api/shifts/me/active");
            if (response.StatusCode == System.Net.HttpStatusCode.NoContent) return null;
            if (!response.IsSuccessStatusCode) return null;
            return await response.Content.ReadFromJsonAsync<ShiftDto>();
        }
        catch
        {
            return null;
        }
    }

    public async Task<(bool Success, ShiftDto? Shift, string? Error)> StartShiftAsync(decimal openingCashAmount)
    {
        try
        {
            ApplyAuthHeader();
            var response = await _http.PostAsJsonAsync("api/shifts/start", new StartShiftRequestDto(openingCashAmount));
            if (!response.IsSuccessStatusCode)
            {
                return (false, null, await response.Content.ReadAsStringAsync());
            }
            var shift = await response.Content.ReadFromJsonAsync<ShiftDto>();
            return (true, shift, null);
        }
        catch (Exception ex)
        {
            return (false, null, $"Không thể kết nối tới máy chủ: {ex.Message}");
        }
    }
}
