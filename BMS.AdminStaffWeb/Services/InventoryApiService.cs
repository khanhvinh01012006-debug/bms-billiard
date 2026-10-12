using System.Net.Http.Headers;
using System.Net.Http.Json;
using BMS.Shared.Dtos;

namespace BMS.AdminStaffWeb.Services;

public class InventoryApiService
{
    private readonly HttpClient _http;
    private readonly AuthState _authState;

    public InventoryApiService(HttpClient http, AuthState authState)
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

    public async Task<List<InventoryItemDto>> GetItemsAsync()
    {
        try
        {
            ApplyAuthHeader();
            var result = await _http.GetFromJsonAsync<List<InventoryItemDto>>("api/inventory");
            return result ?? new List<InventoryItemDto>();
        }
        catch
        {
            return new List<InventoryItemDto>();
        }
    }

    public async Task<(bool Success, string? Error)> CreateItemAsync(UpsertInventoryItemRequestDto request)
    {
        try
        {
            ApplyAuthHeader();
            var response = await _http.PostAsJsonAsync("api/inventory", request);
            if (response.IsSuccessStatusCode) return (true, null);
            return (false, await response.Content.ReadAsStringAsync());
        }
        catch (Exception ex)
        {
            return (false, $"Không thể kết nối tới máy chủ: {ex.Message}");
        }
    }

    public async Task<(bool Success, string? Error)> UpdateItemAsync(int id, UpsertInventoryItemRequestDto request)
    {
        try
        {
            ApplyAuthHeader();
            var response = await _http.PutAsJsonAsync($"api/inventory/{id}", request);
            if (response.IsSuccessStatusCode) return (true, null);
            return (false, await response.Content.ReadAsStringAsync());
        }
        catch (Exception ex)
        {
            return (false, $"Không thể kết nối tới máy chủ: {ex.Message}");
        }
    }

    public async Task<(bool Success, string? Error)> DeleteItemAsync(int id)
    {
        try
        {
            ApplyAuthHeader();
            var response = await _http.DeleteAsync($"api/inventory/{id}");
            if (response.IsSuccessStatusCode) return (true, null);
            return (false, await response.Content.ReadAsStringAsync());
        }
        catch (Exception ex)
        {
            return (false, $"Không thể kết nối tới máy chủ: {ex.Message}");
        }
    }

    public async Task<(bool Success, string? Error)> AdjustQuantityAsync(int id, int delta, string? reason)
    {
        try
        {
            ApplyAuthHeader();
            var response = await _http.PostAsJsonAsync($"api/inventory/{id}/adjust", new AdjustInventoryQuantityRequestDto(delta, reason));
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

    // Việc 1: Phiếu nhập hàng + Phiếu kiểm kho định kỳ.

    public async Task<(bool Success, StockInDto? Result, string? Error)> CreateStockInAsync(CreateStockInRequestDto request)
    {
        try
        {
            ApplyAuthHeader();
            var response = await _http.PostAsJsonAsync("api/inventory/stock-in", request);
            if (response.IsSuccessStatusCode)
            {
                var result = await response.Content.ReadFromJsonAsync<StockInDto>();
                return (true, result, null);
            }
            return (false, null, await response.Content.ReadAsStringAsync());
        }
        catch (Exception ex)
        {
            return (false, null, $"Không thể kết nối tới máy chủ: {ex.Message}");
        }
    }

    public async Task<List<StockInDto>> GetStockInHistoryAsync()
    {
        try
        {
            ApplyAuthHeader();
            var result = await _http.GetFromJsonAsync<List<StockInDto>>("api/inventory/stock-in");
            return result ?? new List<StockInDto>();
        }
        catch
        {
            return new List<StockInDto>();
        }
    }

    public async Task<(bool Success, StockAuditDto? Result, string? Error)> CreateStockAuditAsync(string? note)
    {
        try
        {
            ApplyAuthHeader();
            var response = await _http.PostAsJsonAsync("api/inventory/stock-audits", new CreateStockAuditRequestDto(note));
            if (response.IsSuccessStatusCode)
            {
                var result = await response.Content.ReadFromJsonAsync<StockAuditDto>();
                return (true, result, null);
            }
            return (false, null, await response.Content.ReadAsStringAsync());
        }
        catch (Exception ex)
        {
            return (false, null, $"Không thể kết nối tới máy chủ: {ex.Message}");
        }
    }

    public async Task<List<StockAuditDto>> GetStockAuditsAsync()
    {
        try
        {
            ApplyAuthHeader();
            var result = await _http.GetFromJsonAsync<List<StockAuditDto>>("api/inventory/stock-audits");
            return result ?? new List<StockAuditDto>();
        }
        catch
        {
            return new List<StockAuditDto>();
        }
    }

    public async Task<StockAuditDto?> GetStockAuditByIdAsync(int id)
    {
        try
        {
            ApplyAuthHeader();
            return await _http.GetFromJsonAsync<StockAuditDto>($"api/inventory/stock-audits/{id}");
        }
        catch
        {
            return null;
        }
    }

    public async Task<(bool Success, string? Error)> UpdateStockAuditLineAsync(int auditId, int lineId, int actualQuantity)
    {
        try
        {
            ApplyAuthHeader();
            var response = await _http.PutAsJsonAsync(
                $"api/inventory/stock-audits/{auditId}/lines/{lineId}",
                new UpdateStockAuditLineRequestDto(actualQuantity));
            if (response.IsSuccessStatusCode) return (true, null);
            return (false, await response.Content.ReadAsStringAsync());
        }
        catch (Exception ex)
        {
            return (false, $"Không thể kết nối tới máy chủ: {ex.Message}");
        }
    }

    public async Task<(bool Success, StockAuditDto? Result, string? Error)> CompleteStockAuditAsync(int id)
    {
        try
        {
            ApplyAuthHeader();
            var response = await _http.PostAsync($"api/inventory/stock-audits/{id}/complete", null);
            if (response.IsSuccessStatusCode)
            {
                var result = await response.Content.ReadFromJsonAsync<StockAuditDto>();
                return (true, result, null);
            }
            return (false, null, await response.Content.ReadAsStringAsync());
        }
        catch (Exception ex)
        {
            return (false, null, $"Không thể kết nối tới máy chủ: {ex.Message}");
        }
    }
}
