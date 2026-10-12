using System.Net.Http.Headers;
using System.Net.Http.Json;
using BMS.Shared.Dtos;

namespace BMS.PlayerWeb.Services;

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

    public async Task<(bool Success, string? Error)> RentAsync(int cueId, string customerName)
    {
        ApplyAuthHeader();
        var response = await _http.PostAsJsonAsync("api/cues/rent", new CreateCueRentalRequestDto(cueId, customerName));
        if (response.IsSuccessStatusCode) return (true, null);
        return (false, await response.Content.ReadAsStringAsync());
    }

    // --- Staff operational actions (danh sách đang thuê, trả/hư hỏng/mất cơ) ---

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

    // --- Mua cơ (chỉ gửi yêu cầu, chưa thanh toán thật) ---

    public async Task<(bool Success, string? Error)> RequestPurchaseAsync(int cueId, string customerName, string phoneNumber)
    {
        try
        {
            ApplyAuthHeader();
            var response = await _http.PostAsJsonAsync("api/cues/purchase-request",
                new CueSalePurchaseRequestDto(cueId, customerName, phoneNumber));
            if (response.IsSuccessStatusCode) return (true, null);
            return (false, await response.Content.ReadAsStringAsync());
        }
        catch (Exception ex)
        {
            return (false, $"Không thể kết nối tới máy chủ: {ex.Message}");
        }
    }

    // --- Chợ trao đổi cơ (Trade) ---

    public async Task<List<CueListingDto>> GetListingsAsync()
    {
        try
        {
            var result = await _http.GetFromJsonAsync<List<CueListingDto>>("api/cue-listings");
            return result ?? new List<CueListingDto>();
        }
        catch
        {
            return new List<CueListingDto>();
        }
    }

    public async Task<(bool Success, string? Error)> CreateListingAsync(string title, string? description, string? imageUrl, string? desiredTrade, decimal? price)
    {
        try
        {
            ApplyAuthHeader();
            var response = await _http.PostAsJsonAsync("api/cue-listings",
                new CreateCueListingRequestDto(title, description, imageUrl, desiredTrade, price));
            if (response.IsSuccessStatusCode) return (true, null);
            return (false, await response.Content.ReadAsStringAsync());
        }
        catch (Exception ex)
        {
            return (false, $"Không thể kết nối tới máy chủ: {ex.Message}");
        }
    }

    public async Task<(bool Success, string? Url, string? Error)> UploadImageAsync(byte[] data, string fileName, string contentType)
    {
        try
        {
            ApplyAuthHeader();
            using var content = new MultipartFormDataContent();
            using var fileContent = new ByteArrayContent(data);
            fileContent.Headers.ContentType = new MediaTypeHeaderValue(string.IsNullOrEmpty(contentType) ? "application/octet-stream" : contentType);
            content.Add(fileContent, "file", fileName);

            var response = await _http.PostAsync("api/uploads", content);
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
