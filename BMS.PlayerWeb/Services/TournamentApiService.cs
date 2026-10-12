using System.Net.Http.Headers;
using System.Net.Http.Json;
using BMS.Shared.Dtos;

namespace BMS.PlayerWeb.Services;

// Đợt 2: Quản lý Giải đấu - phía Player xem danh sách + đăng ký/hủy đăng ký.
public class TournamentApiService
{
    private readonly HttpClient _http;
    private readonly AuthState _authState;

    public TournamentApiService(HttpClient http, AuthState authState)
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

    public async Task<List<TournamentDto>> GetTournamentsAsync()
    {
        try
        {
            ApplyAuthHeader();
            var result = await _http.GetFromJsonAsync<List<TournamentDto>>("api/tournaments");
            return result ?? new List<TournamentDto>();
        }
        catch
        {
            return new List<TournamentDto>();
        }
    }

    public async Task<TournamentDto?> GetTournamentAsync(int id)
    {
        try
        {
            ApplyAuthHeader();
            return await _http.GetFromJsonAsync<TournamentDto>($"api/tournaments/{id}");
        }
        catch
        {
            return null;
        }
    }

    public async Task<bool> IsRegisteredAsync(int tournamentId)
    {
        try
        {
            ApplyAuthHeader();
            return await _http.GetFromJsonAsync<bool>($"api/tournaments/{tournamentId}/my-registration");
        }
        catch
        {
            return false;
        }
    }

    public async Task<(bool Success, string? Error)> RegisterAsync(int tournamentId, string phoneNumber)
    {
        try
        {
            ApplyAuthHeader();
            var response = await _http.PostAsJsonAsync($"api/tournaments/{tournamentId}/register",
                new RegisterForTournamentRequestDto(phoneNumber));
            if (response.IsSuccessStatusCode) return (true, null);
            return (false, await response.Content.ReadAsStringAsync());
        }
        catch (Exception ex)
        {
            return (false, $"Không thể kết nối tới máy chủ: {ex.Message}");
        }
    }

    // Đợt 9: Bảng đấu loại trực tiếp - Player chỉ xem, không có quyền ghi nhận kết quả.
    public async Task<List<TournamentMatchDto>> GetBracketAsync(int tournamentId)
    {
        try
        {
            ApplyAuthHeader();
            var result = await _http.GetFromJsonAsync<List<TournamentMatchDto>>($"api/tournaments/{tournamentId}/bracket");
            return result ?? new List<TournamentMatchDto>();
        }
        catch
        {
            return new List<TournamentMatchDto>();
        }
    }

    public async Task<(bool Success, string? Error)> UnregisterAsync(int tournamentId)
    {
        try
        {
            ApplyAuthHeader();
            var response = await _http.PostAsync($"api/tournaments/{tournamentId}/unregister", null);
            if (response.IsSuccessStatusCode) return (true, null);
            return (false, await response.Content.ReadAsStringAsync());
        }
        catch (Exception ex)
        {
            return (false, $"Không thể kết nối tới máy chủ: {ex.Message}");
        }
    }
}
