using System.Net.Http.Headers;
using System.Net.Http.Json;
using BMS.Shared.Dtos;

namespace BMS.AdminStaffWeb.Services;

public class AccountApiService
{
    private readonly HttpClient _http;
    private readonly AuthState _authState;

    public AccountApiService(HttpClient http, AuthState authState)
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

    public async Task<(bool Success, LoginResponseDto? Result, string? Error)> LoginAsync(string email, string password)
    {
        try
        {
            var response = await _http.PostAsJsonAsync("api/accounts/login", new LoginRequestDto(email, password));
            if (!response.IsSuccessStatusCode)
            {
                var message = ExtractErrorMessage(await response.Content.ReadAsStringAsync());
                return (false, null, string.IsNullOrWhiteSpace(message) ? "Đăng nhập thất bại." : message);
            }

            var result = await response.Content.ReadFromJsonAsync<LoginResponseDto>();
            return (true, result, null);
        }
        catch (Exception ex)
        {
            return (false, null, $"Không thể kết nối tới máy chủ: {ex.Message}");
        }
    }

    public async Task<(bool Success, ForgotPasswordResponseDto? Result, string? Error)> ForgotPasswordAsync(string email)
    {
        try
        {
            var response = await _http.PostAsJsonAsync("api/accounts/forgot-password", new ForgotPasswordRequestDto(email));
            if (!response.IsSuccessStatusCode)
            {
                var message = await response.Content.ReadAsStringAsync();
                return (false, null, string.IsNullOrWhiteSpace(message) ? "Yêu cầu thất bại." : message);
            }

            var result = await response.Content.ReadFromJsonAsync<ForgotPasswordResponseDto>();
            return (true, result, null);
        }
        catch (Exception ex)
        {
            return (false, null, $"Không thể kết nối tới máy chủ: {ex.Message}");
        }
    }

    public async Task<(bool Success, string? Error)> ResetPasswordAsync(string email, string token, string newPassword)
    {
        try
        {
            var response = await _http.PostAsJsonAsync("api/accounts/reset-password",
                new ResetPasswordRequestDto(email, token, newPassword));
            if (response.IsSuccessStatusCode) return (true, null);
            return (false, await response.Content.ReadAsStringAsync());
        }
        catch (Exception ex)
        {
            return (false, $"Không thể kết nối tới máy chủ: {ex.Message}");
        }
    }

    public async Task<(bool Success, CreateStaffResponseDto? Result, string? Error)> CreateStaffAsync(
        string fullName, string pin, string? phoneNumber, string? employeeCode)
    {
        try
        {
            ApplyAuthHeader();
            var response = await _http.PostAsJsonAsync("api/accounts/create-staff",
                new CreateStaffRequestDto(fullName, pin, phoneNumber, employeeCode));
            if (!response.IsSuccessStatusCode)
            {
                return (false, null, ExtractErrorMessage(await response.Content.ReadAsStringAsync()));
            }
            var result = await response.Content.ReadFromJsonAsync<CreateStaffResponseDto>();
            return (true, result, null);
        }
        catch (Exception ex)
        {
            return (false, null, $"Không thể kết nối tới máy chủ: {ex.Message}");
        }
    }

    public async Task<List<StaffAccountDto>> GetStaffListAsync()
    {
        try
        {
            ApplyAuthHeader();
            var result = await _http.GetFromJsonAsync<List<StaffAccountDto>>("api/accounts?role=Staff");
            return result ?? new List<StaffAccountDto>();
        }
        catch
        {
            return new List<StaffAccountDto>();
        }
    }

    // Đợt 9: Admin cập nhật hạn mức giảm giá tự duyệt + lương/giờ cho một nhân viên.
    public async Task<(bool Success, string? Error)> UpdateStaffSettingsAsync(
        int id, decimal? discountLimitPercent, decimal? wageRatePerHour)
    {
        try
        {
            ApplyAuthHeader();
            var response = await _http.PutAsJsonAsync($"api/accounts/{id}/staff-settings",
                new UpdateStaffSettingsRequestDto(discountLimitPercent, wageRatePerHour));
            if (response.IsSuccessStatusCode) return (true, null);
            return (false, ExtractErrorMessage(await response.Content.ReadAsStringAsync()));
        }
        catch (Exception ex)
        {
            return (false, $"Không thể kết nối tới máy chủ: {ex.Message}");
        }
    }

    private static string ExtractErrorMessage(string raw)
    {
        if (string.IsNullOrWhiteSpace(raw)) return raw;

        try
        {
            using var doc = System.Text.Json.JsonDocument.Parse(raw);
            if (doc.RootElement.TryGetProperty("message", out var messageProp))
            {
                return messageProp.GetString() ?? raw;
            }
        }
        catch (System.Text.Json.JsonException)
        {
            // Not JSON - fall through and return the raw text as-is.
        }

        return raw;
    }

    public async Task<(bool Success, ProfileDto? Result, string? Error)> GetProfileAsync()
    {
        try
        {
            ApplyAuthHeader();
            var response = await _http.GetAsync("api/accounts/profile");
            if (!response.IsSuccessStatusCode)
            {
                if (response.StatusCode == System.Net.HttpStatusCode.Unauthorized)
                {
                    // Token hết hạn/không hợp lệ - đăng xuất để tránh vòng lặp lỗi và
                    // đưa người dùng quay lại trang đăng nhập với thông báo rõ ràng.
                    _authState.SignOut();
                    return (false, null, "Phiên đăng nhập đã hết hạn, vui lòng đăng nhập lại.");
                }
                return (false, null, ExtractErrorMessage(await response.Content.ReadAsStringAsync()));
            }
            var result = await response.Content.ReadFromJsonAsync<ProfileDto>();
            return (true, result, null);
        }
        catch (Exception ex)
        {
            return (false, null, $"Không thể kết nối tới máy chủ: {ex.Message}");
        }
    }

    public async Task<(bool Success, string? Error)> UpdateProfileAsync(string fullName, string? address)
    {
        try
        {
            ApplyAuthHeader();
            var response = await _http.PutAsJsonAsync("api/accounts/profile", new UpdateProfileRequestDto(fullName, address));
            if (response.IsSuccessStatusCode) return (true, null);
            if (response.StatusCode == System.Net.HttpStatusCode.Unauthorized)
            {
                _authState.SignOut();
                return (false, "Phiên đăng nhập đã hết hạn, vui lòng đăng nhập lại.");
            }
            return (false, ExtractErrorMessage(await response.Content.ReadAsStringAsync()));
        }
        catch (Exception ex)
        {
            return (false, $"Không thể kết nối tới máy chủ: {ex.Message}");
        }
    }
}
