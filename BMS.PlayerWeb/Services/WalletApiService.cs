using System.Net.Http.Headers;
using System.Net.Http.Json;
using BMS.Shared.Dtos;

namespace BMS.PlayerWeb.Services;

// Đợt 3: Ví / Hội viên / Thanh toán - phía Player (và Staff dùng phần duyệt nạp tiền).
public class WalletApiService
{
    private readonly HttpClient _http;
    private readonly AuthState _authState;

    public WalletApiService(HttpClient http, AuthState authState)
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

    public async Task<WalletInfoDto?> GetWalletInfoAsync()
    {
        try
        {
            ApplyAuthHeader();
            return await _http.GetFromJsonAsync<WalletInfoDto>("api/wallet/me");
        }
        catch
        {
            return null;
        }
    }

    public async Task<List<TransactionDto>> GetMyTransactionsAsync()
    {
        try
        {
            ApplyAuthHeader();
            var result = await _http.GetFromJsonAsync<List<TransactionDto>>("api/wallet/transactions/me");
            return result ?? new List<TransactionDto>();
        }
        catch
        {
            return new List<TransactionDto>();
        }
    }

    public async Task<(bool Success, string? Error)> RequestTopUpAsync(decimal amount, string method, string? note, string? proofImageUrl)
    {
        try
        {
            ApplyAuthHeader();
            var response = await _http.PostAsJsonAsync("api/wallet/topup",
                new CreateTopUpRequestDto(amount, method, note, proofImageUrl));
            if (response.IsSuccessStatusCode) return (true, null);
            return (false, await response.Content.ReadAsStringAsync());
        }
        catch (Exception ex)
        {
            return (false, $"Không thể kết nối tới máy chủ: {ex.Message}");
        }
    }

    public async Task<(bool Success, WalletInfoDto? Wallet, string? Error)> PayWithWalletAsync(
        decimal amount, string relatedEntityType, int? relatedEntityId, string? note)
    {
        try
        {
            ApplyAuthHeader();
            var response = await _http.PostAsJsonAsync("api/wallet/pay",
                new PayWithWalletRequestDto(amount, relatedEntityType, relatedEntityId, note));
            if (!response.IsSuccessStatusCode)
            {
                return (false, null, await response.Content.ReadAsStringAsync());
            }

            var wallet = await response.Content.ReadFromJsonAsync<WalletInfoDto>();
            return (true, wallet, null);
        }
        catch (Exception ex)
        {
            return (false, null, $"Không thể kết nối tới máy chủ: {ex.Message}");
        }
    }

    // --- Staff: duyệt nạp tiền ---

    public async Task<List<TransactionDto>> GetPendingTransactionsAsync()
    {
        try
        {
            ApplyAuthHeader();
            var result = await _http.GetFromJsonAsync<List<TransactionDto>>("api/wallet/transactions/pending");
            return result ?? new List<TransactionDto>();
        }
        catch
        {
            return new List<TransactionDto>();
        }
    }

    public async Task<(bool Success, string? Error)> ProcessTransactionAsync(int id, bool approve, string? note)
    {
        try
        {
            ApplyAuthHeader();
            var response = await _http.PostAsJsonAsync($"api/wallet/transactions/{id}/process",
                new ProcessTransactionRequestDto(approve, note));
            if (response.IsSuccessStatusCode) return (true, null);
            return (false, await response.Content.ReadAsStringAsync());
        }
        catch (Exception ex)
        {
            return (false, $"Không thể kết nối tới máy chủ: {ex.Message}");
        }
    }

    // Đợt 6: Staff tìm hội viên theo SĐT/email/tên để thanh toán bằng Ví (bắt buộc) hoặc
    // gắn tùy chọn vào giao dịch Cash/BankTransfer.
    public async Task<List<MemberInfoDto>> FindMembersAsync(string query)
    {
        try
        {
            ApplyAuthHeader();
            var url = "api/wallet/find-member" + (string.IsNullOrWhiteSpace(query) ? "" : $"?query={Uri.EscapeDataString(query)}");
            var result = await _http.GetFromJsonAsync<List<MemberInfoDto>>(url);
            return result ?? new List<MemberInfoDto>();
        }
        catch
        {
            return new List<MemberInfoDto>();
        }
    }

    public async Task<(bool Success, string? Url, string? Error)> UploadProofImageAsync(byte[] data, string fileName, string contentType)
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

    // ===== Tích điểm hội viên =====
    public async Task<PointsInfoDto?> GetPointsAsync()
    {
        try
        {
            ApplyAuthHeader();
            return await _http.GetFromJsonAsync<PointsInfoDto>("api/wallet/points/me");
        }
        catch
        {
            return null;
        }
    }

    public async Task<(bool Success, PointsInfoDto? Points, string? Error)> RedeemPointsAsync(int points)
    {
        ApplyAuthHeader();
        var response = await _http.PostAsJsonAsync("api/wallet/points/redeem", new RedeemPointsRequestDto(points));
        if (response.IsSuccessStatusCode) return (true, await response.Content.ReadFromJsonAsync<PointsInfoDto>(), null);
        return (false, null, await response.Content.ReadAsStringAsync());
    }

    // ===== Mã QR hội viên (có hạn dùng ngắn) =====
    public async Task<MemberQrDto?> GetMemberQrAsync()
    {
        try
        {
            ApplyAuthHeader();
            return await _http.GetFromJsonAsync<MemberQrDto>("api/wallet/member-qr");
        }
        catch
        {
            return null;
        }
    }
}
