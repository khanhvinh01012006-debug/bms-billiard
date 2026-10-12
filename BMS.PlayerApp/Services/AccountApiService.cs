using System.Net.Http.Headers;
using System.Net.Http.Json;
using BMS.Shared.Dtos;

namespace BMS.PlayerApp.Services;

// EP-01: Đăng ký/Đăng nhập cho ứng dụng di động (Player + Staff dùng chung).
public class AccountApiService
{
    private readonly HttpClient _http;
    private readonly AuthState _authState;

    public AccountApiService(ApiOptions options, AuthState authState)
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

    public async Task<(bool Success, LoginResponseDto? Result, string? Error)> RegisterAsync(string fullName, string email, string password, string? phoneNumber = null)
    {
        try
        {
            var response = await _http.PostAsJsonAsync("api/accounts/register", new RegisterRequestDto(fullName, email, password, phoneNumber));
            if (!response.IsSuccessStatusCode)
            {
                var message = await response.Content.ReadAsStringAsync();
                return (false, null, string.IsNullOrWhiteSpace(message) ? "Đăng ký thất bại." : message);
            }

            var result = await response.Content.ReadFromJsonAsync<LoginResponseDto>();
            return (true, result, null);
        }
        catch (Exception ex)
        {
            return (false, null, $"Không thể kết nối tới máy chủ: {ex.Message}");
        }
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

            var message = ExtractErrorMessage(await response.Content.ReadAsStringAsync());
            return (false, string.IsNullOrWhiteSpace(message) ? "Đặt lại mật khẩu thất bại." : message);
        }
        catch (Exception ex)
        {
            return (false, $"Không thể kết nối tới máy chủ: {ex.Message}");
        }
    }

    // --- Xác thực số điện thoại (yêu cầu đã đăng nhập) ---

    public async Task<(bool Success, SendPhoneOtpResponseDto? Result, string? Error)> SendPhoneOtpAsync(string phoneNumber)
    {
        try
        {
            ApplyAuthHeader();
            var response = await _http.PostAsJsonAsync("api/accounts/send-phone-otp", new SendPhoneOtpRequestDto(phoneNumber));
            if (!response.IsSuccessStatusCode)
            {
                var message = ExtractErrorMessage(await response.Content.ReadAsStringAsync());
                return (false, null, string.IsNullOrWhiteSpace(message) ? "Gửi mã OTP thất bại." : message);
            }

            var result = await response.Content.ReadFromJsonAsync<SendPhoneOtpResponseDto>();
            return (true, result, null);
        }
        catch (Exception ex)
        {
            return (false, null, $"Không thể kết nối tới máy chủ: {ex.Message}");
        }
    }

    public async Task<(bool Success, string? Error)> VerifyPhoneOtpAsync(string otp)
    {
        try
        {
            ApplyAuthHeader();
            var response = await _http.PostAsJsonAsync("api/accounts/verify-phone-otp", new VerifyPhoneOtpRequestDto(otp));
            if (response.IsSuccessStatusCode) return (true, null);

            var message = ExtractErrorMessage(await response.Content.ReadAsStringAsync());
            return (false, string.IsNullOrWhiteSpace(message) ? "Xác thực OTP thất bại." : message);
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
}
