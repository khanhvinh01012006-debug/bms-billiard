using System.Net.Http.Headers;
using System.Net.Http.Json;
using BMS.Shared.Dtos;

namespace BMS.AdminStaffWeb.Services;

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

    public async Task<(bool Success, string? Error)> CreateTournamentAsync(UpsertTournamentRequestDto request)
    {
        try
        {
            ApplyAuthHeader();
            var response = await _http.PostAsJsonAsync("api/tournaments", request);
            if (response.IsSuccessStatusCode) return (true, null);
            return (false, await response.Content.ReadAsStringAsync());
        }
        catch (Exception ex)
        {
            return (false, $"Không thể kết nối tới máy chủ: {ex.Message}");
        }
    }

    public async Task<(bool Success, string? Error)> UpdateTournamentAsync(int id, UpsertTournamentRequestDto request)
    {
        try
        {
            ApplyAuthHeader();
            var response = await _http.PutAsJsonAsync($"api/tournaments/{id}", request);
            if (response.IsSuccessStatusCode) return (true, null);
            return (false, await response.Content.ReadAsStringAsync());
        }
        catch (Exception ex)
        {
            return (false, $"Không thể kết nối tới máy chủ: {ex.Message}");
        }
    }

    public async Task<(bool Success, string? Error)> DeleteTournamentAsync(int id)
    {
        try
        {
            ApplyAuthHeader();
            var response = await _http.DeleteAsync($"api/tournaments/{id}");
            if (response.IsSuccessStatusCode) return (true, null);
            return (false, await response.Content.ReadAsStringAsync());
        }
        catch (Exception ex)
        {
            return (false, $"Không thể kết nối tới máy chủ: {ex.Message}");
        }
    }

    public async Task<List<TournamentRegistrationDto>> GetRegistrationsAsync(int tournamentId)
    {
        try
        {
            ApplyAuthHeader();
            var result = await _http.GetFromJsonAsync<List<TournamentRegistrationDto>>($"api/tournaments/{tournamentId}/registrations");
            return result ?? new List<TournamentRegistrationDto>();
        }
        catch
        {
            return new List<TournamentRegistrationDto>();
        }
    }

    // Đợt 9: Bảng đấu loại trực tiếp - Admin bốc thăm & ghi nhận kết quả từng trận.
    public async Task<(bool Success, string? Error)> GenerateBracketAsync(int tournamentId)
    {
        try
        {
            ApplyAuthHeader();
            var response = await _http.PostAsync($"api/tournaments/{tournamentId}/generate-bracket", null);
            if (response.IsSuccessStatusCode) return (true, null);
            return (false, await response.Content.ReadAsStringAsync());
        }
        catch (Exception ex)
        {
            return (false, $"Không thể kết nối tới máy chủ: {ex.Message}");
        }
    }

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

    public async Task<(bool Success, string? Error)> SetMatchResultAsync(int matchId, int winnerRegistrationId)
    {
        try
        {
            ApplyAuthHeader();
            var response = await _http.PostAsJsonAsync($"api/tournaments/matches/{matchId}/result",
                new SetMatchResultRequestDto(winnerRegistrationId));
            if (response.IsSuccessStatusCode) return (true, null);
            return (false, await response.Content.ReadAsStringAsync());
        }
        catch (Exception ex)
        {
            return (false, $"Không thể kết nối tới máy chủ: {ex.Message}");
        }
    }

    public async Task<(bool Success, string? Url, string? Error)> UploadImageAsync(byte[] content, string fileName, string contentType)
    {
        try
        {
            ApplyAuthHeader();
            using var form = new MultipartFormDataContent();
            using var fileContent = new ByteArrayContent(content);
            fileContent.Headers.ContentType = new MediaTypeHeaderValue(string.IsNullOrEmpty(contentType) ? "application/octet-stream" : contentType);
            form.Add(fileContent, "file", fileName);

            var response = await _http.PostAsync("api/uploads", form);
            if (!response.IsSuccessStatusCode)
            {
                return (false, null, await response.Content.ReadAsStringAsync());
            }

            var result = await response.Content.ReadFromJsonAsync<UploadImageResultDto>();
            return (true, result?.Url, null);
        }
        catch (Exception ex)
        {
            return (false, null, $"Không thể kết nối tới máy chủ: {ex.Message}");
        }
    }
}
