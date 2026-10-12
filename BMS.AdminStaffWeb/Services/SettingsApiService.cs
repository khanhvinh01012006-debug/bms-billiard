using System.Net.Http.Headers;
using System.Net.Http.Json;
using BMS.Shared.Dtos;

namespace BMS.AdminStaffWeb.Services;

// Cấu hình chung của quán (thông tin liên hệ + tài khoản nhận chuyển khoản VietQR).
public class SettingsApiService
{
    private readonly HttpClient _http;
    private readonly AuthState _authState;

    public SettingsApiService(HttpClient http, AuthState authState)
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

    public async Task<ClubSettingsDto> GetAsync()
    {
        try
        {
            return await _http.GetFromJsonAsync<ClubSettingsDto>("api/settings") ?? new ClubSettingsDto();
        }
        catch
        {
            return new ClubSettingsDto();
        }
    }

    public async Task<(bool Success, string? Error)> UpdateAsync(ClubSettingsDto settings)
    {
        try
        {
            ApplyAuthHeader();
            var response = await _http.PutAsJsonAsync("api/settings", settings);
            if (response.IsSuccessStatusCode) return (true, null);
            return (false, await response.Content.ReadAsStringAsync());
        }
        catch
        {
            return (false, "Không kết nối được máy chủ.");
        }
    }
}
