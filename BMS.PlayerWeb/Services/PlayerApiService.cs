using System.Net.Http.Json;
using BMS.Shared.Dtos;

namespace BMS.PlayerWeb.Services;

public class PlayerApiService
{
    private readonly HttpClient _http;

    public PlayerApiService(HttpClient http)
    {
        _http = http;
    }

    public async Task<List<PlayerProfileDto>> GetPlayersAsync()
    {
        var result = await _http.GetFromJsonAsync<List<PlayerProfileDto>>("api/players");
        return result ?? new List<PlayerProfileDto>();
    }

    public async Task<PlayerProfileDto?> GetPlayerAsync(int id)
    {
        try
        {
            return await _http.GetFromJsonAsync<PlayerProfileDto>($"api/players/{id}");
        }
        catch
        {
            return null;
        }
    }

    public async Task<List<PlayerProfileDto>> GetSuggestionsAsync(int playerId, int take = 5)
    {
        var result = await _http.GetFromJsonAsync<List<PlayerProfileDto>>($"api/players/{playerId}/suggestions?take={take}");
        return result ?? new List<PlayerProfileDto>();
    }

    public async Task<List<MatchRequestDto>> GetMatchRequestsAsync()
    {
        var result = await _http.GetFromJsonAsync<List<MatchRequestDto>>("api/match-requests");
        return result ?? new List<MatchRequestDto>();
    }

    public async Task<(bool Success, string? Error)> SendMatchRequestAsync(int requesterId, int? targetPlayerId)
    {
        var response = await _http.PostAsJsonAsync("api/match-requests", new CreateMatchRequestDto(requesterId, targetPlayerId));
        if (response.IsSuccessStatusCode) return (true, null);
        return (false, await response.Content.ReadAsStringAsync());
    }
}
