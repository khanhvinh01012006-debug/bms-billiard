using System.Net.Http.Headers;
using System.Net.Http.Json;
using BMS.Shared.Dtos;

namespace BMS.AdminStaffWeb.Services;

// Đợt 3: Ví / Hội viên / Thanh toán - phía Admin (báo cáo, duyệt giao dịch, danh sách hội viên).
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

    public async Task<List<TransactionDto>> GetAllTransactionsAsync(string? status, string? method, string? type = null)
    {
        try
        {
            ApplyAuthHeader();
            var query = new List<string>();
            if (!string.IsNullOrWhiteSpace(status)) query.Add($"status={Uri.EscapeDataString(status)}");
            if (!string.IsNullOrWhiteSpace(method)) query.Add($"method={Uri.EscapeDataString(method)}");
            if (!string.IsNullOrWhiteSpace(type)) query.Add($"type={Uri.EscapeDataString(type)}");
            var url = "api/wallet/transactions/all" + (query.Count > 0 ? "?" + string.Join("&", query) : "");

            var result = await _http.GetFromJsonAsync<List<TransactionDto>>(url);
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

    public async Task<List<MemberInfoDto>> GetMembersAsync()
    {
        try
        {
            ApplyAuthHeader();
            var result = await _http.GetFromJsonAsync<List<MemberInfoDto>>("api/wallet/members");
            return result ?? new List<MemberInfoDto>();
        }
        catch
        {
            return new List<MemberInfoDto>();
        }
    }

    // Đợt 9: Ghi chú nội bộ hội viên + Gói giờ bàn trả trước.
    public async Task<(bool Success, string? Error)> UpdateMemberNoteAsync(int accountId, string? note)
    {
        try
        {
            ApplyAuthHeader();
            var response = await _http.PutAsJsonAsync($"api/wallet/members/{accountId}/note",
                new UpdateMemberNoteRequestDto(note));
            if (response.IsSuccessStatusCode) return (true, null);
            return (false, await response.Content.ReadAsStringAsync());
        }
        catch (Exception ex)
        {
            return (false, $"Không thể kết nối tới máy chủ: {ex.Message}");
        }
    }

    public async Task<List<HourPackageDto>> GetHourPackagesAsync(int accountId)
    {
        try
        {
            ApplyAuthHeader();
            var result = await _http.GetFromJsonAsync<List<HourPackageDto>>($"api/wallet/hour-packages/by-account/{accountId}");
            return result ?? new List<HourPackageDto>();
        }
        catch
        {
            return new List<HourPackageDto>();
        }
    }

    public async Task<(bool Success, string? Error)> CreateHourPackageAsync(CreateHourPackageRequestDto request)
    {
        try
        {
            ApplyAuthHeader();
            var response = await _http.PostAsJsonAsync("api/wallet/hour-packages", request);
            if (response.IsSuccessStatusCode) return (true, null);
            return (false, await response.Content.ReadAsStringAsync());
        }
        catch (Exception ex)
        {
            return (false, $"Không thể kết nối tới máy chủ: {ex.Message}");
        }
    }

    public async Task<(bool Success, string? Error)> ConsumeHourPackageAsync(int id, decimal hours)
    {
        try
        {
            ApplyAuthHeader();
            var response = await _http.PostAsJsonAsync($"api/wallet/hour-packages/{id}/consume",
                new ConsumeHourPackageRequestDto(hours));
            if (response.IsSuccessStatusCode) return (true, null);
            return (false, await response.Content.ReadAsStringAsync());
        }
        catch (Exception ex)
        {
            return (false, $"Không thể kết nối tới máy chủ: {ex.Message}");
        }
    }

    // Đợt 9: Dashboard - Chờ duyệt giảm giá (tái dùng WalletApiService cho đơn giản).
    public async Task<List<DiscountRequestDto>> GetPendingDiscountRequestsAsync()
    {
        try
        {
            ApplyAuthHeader();
            var result = await _http.GetFromJsonAsync<List<DiscountRequestDto>>("api/discount-requests?status=PendingApproval");
            return result ?? new List<DiscountRequestDto>();
        }
        catch
        {
            return new List<DiscountRequestDto>();
        }
    }

    public async Task<(bool Success, string? Error)> ApproveDiscountRequestAsync(int id)
    {
        try
        {
            ApplyAuthHeader();
            var response = await _http.PostAsync($"api/discount-requests/{id}/approve", null);
            if (response.IsSuccessStatusCode) return (true, null);
            return (false, await response.Content.ReadAsStringAsync());
        }
        catch (Exception ex)
        {
            return (false, $"Không thể kết nối tới máy chủ: {ex.Message}");
        }
    }

    public async Task<(bool Success, string? Error)> RejectDiscountRequestAsync(int id)
    {
        try
        {
            ApplyAuthHeader();
            var response = await _http.PostAsync($"api/discount-requests/{id}/reject", null);
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

    // Quét mã QR hội viên -> máy chủ xác thực chữ ký + hạn dùng -> thông tin hội viên.
    public async Task<(MemberInfoDto? Member, string? Error)> FindMemberByQrAsync(string code)
    {
        ApplyAuthHeader();
        var response = await _http.GetAsync($"api/wallet/member-by-qr?code={Uri.EscapeDataString(code)}");
        if (response.IsSuccessStatusCode) return (await response.Content.ReadFromJsonAsync<MemberInfoDto>(), null);
        var error = await response.Content.ReadAsStringAsync();
        return (null, string.IsNullOrWhiteSpace(error) ? "Không đọc được mã QR." : error);
    }
}
