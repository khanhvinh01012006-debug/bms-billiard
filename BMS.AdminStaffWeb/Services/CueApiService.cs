using System.Net.Http.Headers;
using System.Net.Http.Json;
using BMS.Shared.Dtos;

namespace BMS.AdminStaffWeb.Services;

public class CueApiService
{
    private readonly HttpClient _http;
    private readonly AuthState _authState;

    public CueApiService(HttpClient http, AuthState authState)
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

    public async Task<List<CueDto>> GetCuesAsync()
    {
        var result = await _http.GetFromJsonAsync<List<CueDto>>("api/cues");
        return result ?? new List<CueDto>();
    }

    public async Task<List<CueRentalDto>> GetActiveRentalsAsync()
    {
        ApplyAuthHeader();
        var result = await _http.GetFromJsonAsync<List<CueRentalDto>>("api/cues/rentals");
        return result ?? new List<CueRentalDto>();
    }

    public async Task<(bool Success, string? Error)> RentAsync(int cueId, string customerName)
    {
        ApplyAuthHeader();
        var response = await _http.PostAsJsonAsync("api/cues/rent", new CreateCueRentalRequestDto(cueId, customerName));
        if (response.IsSuccessStatusCode) return (true, null);
        return (false, await response.Content.ReadAsStringAsync());
    }

    public async Task<(bool Success, string? Error)> ReturnAsync(int rentalId, string outcome, decimal? penaltyAmount, string? note)
    {
        ApplyAuthHeader();
        var response = await _http.PostAsJsonAsync($"api/cues/rentals/{rentalId}/return",
            new ReturnCueRentalRequestDto(outcome, penaltyAmount, note));
        if (response.IsSuccessStatusCode) return (true, null);
        return (false, await response.Content.ReadAsStringAsync());
    }

    // --- Admin CRUD ---

    public async Task<(bool Success, string? Error)> CreateCueAsync(UpsertCueRequestDto request)
    {
        ApplyAuthHeader();
        var response = await _http.PostAsJsonAsync("api/cues", request);
        if (response.IsSuccessStatusCode) return (true, null);
        return (false, await response.Content.ReadAsStringAsync());
    }

    public async Task<(bool Success, string? Error)> UpdateCueAsync(int id, UpsertCueRequestDto request)
    {
        ApplyAuthHeader();
        var response = await _http.PutAsJsonAsync($"api/cues/{id}", request);
        if (response.IsSuccessStatusCode) return (true, null);
        return (false, await response.Content.ReadAsStringAsync());
    }

    public async Task<(bool Success, string? Error)> DeleteCueAsync(int id)
    {
        ApplyAuthHeader();
        var response = await _http.DeleteAsync($"api/cues/{id}");
        if (response.IsSuccessStatusCode) return (true, null);
        return (false, await response.Content.ReadAsStringAsync());
    }

    public async Task<(bool Success, string? Url, string? Error)> UploadImageAsync(byte[] content, string fileName, string contentType)
    {
        ApplyAuthHeader();
        using var form = new MultipartFormDataContent();
        using var fileContent = new ByteArrayContent(content);
        fileContent.Headers.ContentType = new MediaTypeHeaderValue(contentType);
        form.Add(fileContent, "file", fileName);

        var response = await _http.PostAsync("api/uploads", form);
        if (!response.IsSuccessStatusCode)
        {
            return (false, null, await response.Content.ReadAsStringAsync());
        }

        var result = await response.Content.ReadFromJsonAsync<UploadImageResultDto>();
        return (true, result?.Url, null);
    }

    // ===== Yêu cầu mua cơ / phụ kiện (khách bấm "Đặt mua") =====
    public async Task<List<CuePurchaseRequestDto>> GetPurchaseRequestsAsync()
    {
        try
        {
            ApplyAuthHeader();
            return await _http.GetFromJsonAsync<List<CuePurchaseRequestDto>>("api/cues/purchase-requests") ?? new();
        }
        catch
        {
            return new List<CuePurchaseRequestDto>();
        }
    }

    public async Task<(bool Success, string? Error)> UpdatePurchaseRequestStatusAsync(int id, string status)
    {
        ApplyAuthHeader();
        var response = await _http.PutAsJsonAsync($"api/cues/purchase-requests/{id}/status", new UpdatePurchaseRequestStatusDto(status));
        if (response.IsSuccessStatusCode) return (true, null);
        return (false, await response.Content.ReadAsStringAsync());
    }
}
