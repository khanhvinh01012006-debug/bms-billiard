using System.Net.Http.Headers;
using System.Net.Http.Json;
using BMS.Shared.Dtos;

namespace BMS.AdminStaffWeb.Services;

// Đợt 4: Chấm công - Admin xem báo cáo toàn bộ.
public class AttendanceApiService
{
    private readonly HttpClient _http;
    private readonly AuthState _authState;

    public AttendanceApiService(HttpClient http, AuthState authState)
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

    public async Task<List<AttendanceDto>> GetAllAsync(int? staffAccountId = null, DateTime? from = null, DateTime? to = null)
    {
        try
        {
            ApplyAuthHeader();
            var query = new List<string>();
            if (staffAccountId is int id) query.Add($"staffAccountId={id}");
            if (from is DateTime f) query.Add($"from={f:o}");
            if (to is DateTime t) query.Add($"to={t:o}");

            var url = "api/attendance" + (query.Count > 0 ? "?" + string.Join("&", query) : "");
            var result = await _http.GetFromJsonAsync<List<AttendanceDto>>(url);
            return result ?? new List<AttendanceDto>();
        }
        catch
        {
            return new List<AttendanceDto>();
        }
    }

    public async Task<(bool Success, string? Error)> ClockInAsync(string? note)
    {
        try
        {
            ApplyAuthHeader();
            var response = await _http.PostAsJsonAsync("api/attendance/clock-in", new ClockInRequestDto(note));
            if (response.IsSuccessStatusCode) return (true, null);
            return (false, await response.Content.ReadAsStringAsync());
        }
        catch (Exception ex)
        {
            return (false, $"Không thể kết nối tới máy chủ: {ex.Message}");
        }
    }

    public async Task<(bool Success, string? Error)> ClockOutAsync(string? note)
    {
        try
        {
            ApplyAuthHeader();
            var response = await _http.PostAsJsonAsync("api/attendance/clock-out", new ClockOutRequestDto(note));
            if (response.IsSuccessStatusCode) return (true, null);
            return (false, await response.Content.ReadAsStringAsync());
        }
        catch (Exception ex)
        {
            return (false, $"Không thể kết nối tới máy chủ: {ex.Message}");
        }
    }

    public async Task<List<AttendanceDto>> GetMyTodayAsync()
    {
        try
        {
            ApplyAuthHeader();
            var result = await _http.GetFromJsonAsync<List<AttendanceDto>>("api/attendance/me/today");
            return result ?? new List<AttendanceDto>();
        }
        catch
        {
            return new List<AttendanceDto>();
        }
    }
}
