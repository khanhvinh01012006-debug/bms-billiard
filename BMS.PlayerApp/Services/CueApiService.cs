using System.Net.Http.Headers;
using System.Net.Http.Json;
using BMS.Shared.Dtos;

namespace BMS.PlayerApp.Services;

public class CueApiService
{
    private readonly HttpClient _http;
    private readonly AuthState _authState;

    public CueApiService(ApiOptions options, AuthState authState)
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

    public async Task<List<CueDto>> GetCuesAsync()
    {
        var result = await _http.GetFromJsonAsync<List<CueDto>>("api/cues");
        return result ?? new List<CueDto>();
    }

    public async Task<(bool Success, string? Error)> RentAsync(int cueId, string customerName)
    {
        ApplyAuthHeader();
        var response = await _http.PostAsJsonAsync("api/cues/rent", new CreateCueRentalRequestDto(cueId, customerName));
        if (response.IsSuccessStatusCode) return (true, null);
        return (false, await response.Content.ReadAsStringAsync());
    }

    // --- Staff operational actions (danh sách đang thuê + trả cơ) ---

    public async Task<List<CueRentalDto>> GetActiveRentalsAsync()
    {
        ApplyAuthHeader();
        var result = await _http.GetFromJsonAsync<List<CueRentalDto>>("api/cues/rentals");
        return result ?? new List<CueRentalDto>();
    }

    public async Task<(bool Success, string? Error)> ReturnAsync(int rentalId, string outcome, decimal? penaltyAmount, string? note)
    {
        ApplyAuthHeader();
        var response = await _http.PostAsJsonAsync($"api/cues/rentals/{rentalId}/return",
            new ReturnCueRentalRequestDto(outcome, penaltyAmount, note));
        if (response.IsSuccessStatusCode) return (true, null);
        return (false, await response.Content.ReadAsStringAsync());
    }
}
