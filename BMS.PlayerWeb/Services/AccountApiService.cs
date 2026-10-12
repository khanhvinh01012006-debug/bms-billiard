using System.Net.Http.Headers;
using System.Net.Http.Json;
using BMS.Shared.Dtos;

namespace BMS.PlayerWeb.Services;

// EP-01: US-001 Đăng ký, US-002 Đăng nhập cho khách hàng (Player Web).
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

    public async Task<(bool Success, SendPhoneOtpResponseDto? Result, string? Error)> SendPhoneOtpAsync(string phoneNumber)
    {
        try
        {
            ApplyAuthHeader();
            var response = await _http.PostAsJsonAsync("api/accounts/send-phone-otp", new SendPhoneOtpRequestDto(phoneNumber));
            if (!response.IsSuccessStatusCode)
            {
                return (false, null, ExtractErrorMessage(await response.Content.ReadAsStringAsync()));
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
            return (false, ExtractErrorMessage(await response.Content.ReadAsStringAsync()));
        }
        catch (Exception ex)
        {
            return (false, $"Không thể kết nối tới máy chủ: {ex.Message}");
        }
    }

    public async Task<(bool Success, LoginResponseDto? Result, string? Error)> RegisterAsync(string fullName, string email, string password, string? phoneNumber = null, int? startingElo = null)
    {
        try
        {
            var response = await _http.PostAsJsonAsync("api/accounts/register", new RegisterRequestDto(fullName, email, password, phoneNumber, startingElo));
            if (!response.IsSuccessStatusCode)
            {
                var message = ExtractErrorMessage(await response.Content.ReadAsStringAsync());
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
                var message = ExtractErrorMessage(await response.Content.ReadAsStringAsync());
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
            return (false, ExtractErrorMessage(await response.Content.ReadAsStringAsync()));
        }
        catch (Exception ex)
        {
            return (false, $"Không thể kết nối tới máy chủ: {ex.Message}");
        }
    }

    // Some endpoints return errors as JSON ({"message": "..."}) so the two "email not
    // found" vs "wrong password" cases can be told apart; ASP.NET's built-in error
    // handling (e.g. a plain `return NotFound()`/`return BadRequest()`) instead returns
    // an RFC 9110 ProblemDetails JSON body ({"type":..., "title":"...", "status":404, ...})
    // - without unwrapping "title" too, that raw JSON blob was leaking straight into the UI
    // (e.g. under "Xác thực SĐT" on the Hồ sơ page). Handle both shapes plus plain text so
    // the UI never shows a raw JSON string to the user.
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
            if (doc.RootElement.TryGetProperty("title", out var titleProp))
            {
                var title = titleProp.GetString();
                if (!string.IsNullOrWhiteSpace(title))
                {
                    // ProblemDetails titles are generic English strings ("Not Found",
                    // "Bad Request") - translate the common ones to Vietnamese instead
                    // of showing raw English to the user.
                    return title switch
                    {
                        "Not Found" => "Không tìm thấy dữ liệu yêu cầu.",
                        "Unauthorized" => "Bạn cần đăng nhập lại.",
                        "Bad Request" => "Yêu cầu không hợp lệ.",
                        "Forbidden" => "Bạn không có quyền thực hiện thao tác này.",
                        _ => title
                    };
                }
            }
        }
        catch (System.Text.Json.JsonException)
        {
            // Not JSON - fall through and return the raw text as-is.
        }

        return raw;
    }
}
