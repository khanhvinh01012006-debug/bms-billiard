using System.Net.Http.Headers;
using System.Net.Http.Json;
using BMS.Shared.Dtos;

namespace BMS.PlayerWeb.Services;

public class TableApiService
{
    private readonly HttpClient _http;
    private readonly AuthState _authState;

    public TableApiService(HttpClient http, AuthState authState)
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

    public async Task<List<TableDto>> GetTablesAsync()
    {
        var result = await _http.GetFromJsonAsync<List<TableDto>>("api/tables");
        return result ?? new List<TableDto>();
    }

    // Trả về đặt bàn vừa tạo (kèm CancelToken để khách tự hủy sau này).
    public async Task<(bool Success, BookingDto? Booking, string? Error)> CreateBookingAsync(
        int tableId, string customerName, string? phoneNumber = null, string? note = null, int durationMinutes = 120)
    {
        ApplyAuthHeader();
        var response = await _http.PostAsJsonAsync("api/bookings",
            new CreateBookingRequestDto(tableId, customerName, null, phoneNumber, note, durationMinutes));
        if (response.IsSuccessStatusCode) return (true, await response.Content.ReadFromJsonAsync<BookingDto>(), null);
        return (false, null, await response.Content.ReadAsStringAsync());
    }

    public async Task<BookingDto?> GetMyBookingAsync(int bookingId, string cancelToken)
    {
        try
        {
            using var request = new HttpRequestMessage(HttpMethod.Get, $"api/bookings/{bookingId}");
            request.Headers.Add("X-Booking-Token", cancelToken);
            using var response = await _http.SendAsync(request);
            return response.IsSuccessStatusCode ? await response.Content.ReadFromJsonAsync<BookingDto>() : null;
        }
        catch
        {
            return null;
        }
    }

    // Khách tự hủy yêu cầu đặt bàn của mình bằng mã hủy nhận được lúc đặt.
    public async Task<(bool Success, string? Error)> CancelMyBookingAsync(int bookingId, string cancelToken)
    {
        var response = await _http.PostAsJsonAsync($"api/bookings/{bookingId}/cancel-by-token",
            new CancelBookingByTokenRequestDto(cancelToken));
        if (response.IsSuccessStatusCode) return (true, null);
        return (false, await response.Content.ReadAsStringAsync());
    }

    public async Task<(bool Success, string? Error)> CancelBookingAsync(int bookingId)
    {
        ApplyAuthHeader();
        var response = await _http.DeleteAsync($"api/bookings/{bookingId}");
        if (response.IsSuccessStatusCode) return (true, null);
        return (false, await response.Content.ReadAsStringAsync());
    }

    // --- Staff: duyệt yêu cầu đặt bàn ---

    public async Task<List<BookingDto>> GetPendingBookingsAsync()
    {
        ApplyAuthHeader();
        try
        {
            var result = await _http.GetFromJsonAsync<List<BookingDto>>("api/bookings?status=Pending");
            return result ?? new List<BookingDto>();
        }
        catch
        {
            return new List<BookingDto>();
        }
    }

    public async Task<List<BookingDto>> GetRecentExpiredBookingsAsync()
    {
        ApplyAuthHeader();
        try
        {
            var result = await _http.GetFromJsonAsync<List<BookingDto>>("api/bookings?status=Expired");
            return result ?? new List<BookingDto>();
        }
        catch
        {
            return new List<BookingDto>();
        }
    }

    // Yêu cầu đã "Giữ chỗ" (Confirmed) nhưng khách chưa đến - dùng để hiển thị nút
    // "Khách đã đến - Bắt đầu tính giờ" trên các bàn đang ở trạng thái Reserved.
    public async Task<List<BookingDto>> GetConfirmedBookingsAsync()
    {
        ApplyAuthHeader();
        try
        {
            var result = await _http.GetFromJsonAsync<List<BookingDto>>("api/bookings?status=Confirmed");
            return result ?? new List<BookingDto>();
        }
        catch
        {
            return new List<BookingDto>();
        }
    }

    public async Task<(bool Success, string? Error)> ConfirmBookingAsync(int bookingId)
    {
        ApplyAuthHeader();
        var response = await _http.PutAsync($"api/bookings/{bookingId}/confirm", null);
        if (response.IsSuccessStatusCode) return (true, null);
        return (false, await response.Content.ReadAsStringAsync());
    }

    public async Task<(bool Success, string? Error)> CheckInLateBookingAsync(int bookingId)
    {
        ApplyAuthHeader();
        var response = await _http.PutAsync($"api/bookings/{bookingId}/checkin-late", null);
        if (response.IsSuccessStatusCode) return (true, null);
        return (false, await response.Content.ReadAsStringAsync());
    }

    // --- Staff operational actions (nhận/trả/chuyển bàn), yêu cầu Bearer token Role = Staff/Admin ---

    public async Task<(bool Success, string? Error)> CheckInAsync(int tableId, string customerName, int? bookingId = null)
    {
        ApplyAuthHeader();
        var response = await _http.PostAsJsonAsync("api/sessions/checkin", new { TableId = tableId, CustomerName = customerName, BookingId = bookingId });
        if (response.IsSuccessStatusCode) return (true, null);
        return (false, await response.Content.ReadAsStringAsync());
    }

    public async Task<(bool Success, CheckOutResultDto? Result, string? Error)> CheckOutAsync(
        int tableId, decimal? surchargeAmount = null, string? surchargeReason = null)
    {
        ApplyAuthHeader();
        var response = await _http.PostAsJsonAsync("api/sessions/checkout",
            new { TableId = tableId, SurchargeAmount = surchargeAmount, SurchargeReason = surchargeReason });
        if (!response.IsSuccessStatusCode)
        {
            return (false, null, await response.Content.ReadAsStringAsync());
        }
        var result = await response.Content.ReadFromJsonAsync<CheckOutResultDto>();
        return (true, result, null);
    }

    // --- Đợt 6: Bước 2 của Trả bàn - hoàn tất thanh toán ---

    public async Task<(bool Success, CheckOutResultDto? Result, string? Error)> GetCheckoutSummaryAsync(int sessionId)
    {
        ApplyAuthHeader();
        var response = await _http.GetAsync($"api/sessions/{sessionId}/checkout-summary");
        if (!response.IsSuccessStatusCode)
        {
            return (false, null, await response.Content.ReadAsStringAsync());
        }
        var result = await response.Content.ReadFromJsonAsync<CheckOutResultDto>();
        return (true, result, null);
    }

    public async Task<(bool Success, PaymentCompletedDto? Result, string? Error)> CompletePaymentAsync(
        int sessionId, string method, int? memberAccountId = null, decimal? cashReceivedAmount = null, int? discountRequestId = null)
    {
        ApplyAuthHeader();
        var response = await _http.PostAsJsonAsync($"api/sessions/{sessionId}/complete-payment",
            new CompletePaymentRequestDto(method, memberAccountId, cashReceivedAmount, discountRequestId));
        if (!response.IsSuccessStatusCode)
        {
            return (false, null, await response.Content.ReadAsStringAsync());
        }
        var result = await response.Content.ReadFromJsonAsync<PaymentCompletedDto>();
        return (true, result, null);
    }

    // --- Đợt 9: Giảm giá khi thanh toán ---

    public async Task<(bool Success, DiscountRequestDto? Result, string? Error)> CreateDiscountRequestAsync(
        int sessionId, decimal percent, string? reason)
    {
        ApplyAuthHeader();
        var response = await _http.PostAsJsonAsync("api/discount-requests",
            new CreateDiscountRequestDto(sessionId, percent, reason));
        if (!response.IsSuccessStatusCode)
        {
            return (false, null, await response.Content.ReadAsStringAsync());
        }
        var result = await response.Content.ReadFromJsonAsync<DiscountRequestDto>();
        return (true, result, null);
    }

    public async Task<DiscountRequestDto?> GetDiscountStatusAsync(int sessionId)
    {
        ApplyAuthHeader();
        try
        {
            var response = await _http.GetAsync($"api/discount-requests/by-session/{sessionId}");
            if (!response.IsSuccessStatusCode || response.StatusCode == System.Net.HttpStatusCode.NoContent)
            {
                return null;
            }
            return await response.Content.ReadFromJsonAsync<DiscountRequestDto>();
        }
        catch
        {
            return null;
        }
    }

    // --- Đợt 4: Lịch sử phiên chơi ---

    public async Task<List<SessionHistoryDto>> GetSessionHistoryAsync()
    {
        ApplyAuthHeader();
        try
        {
            var result = await _http.GetFromJsonAsync<List<SessionHistoryDto>>("api/sessions/history");
            return result ?? new List<SessionHistoryDto>();
        }
        catch
        {
            return new List<SessionHistoryDto>();
        }
    }

    public async Task<(bool Success, string? Error)> TransferAsync(int fromTableId, int toTableId)
    {
        ApplyAuthHeader();
        var response = await _http.PostAsJsonAsync("api/sessions/transfer", new { FromTableId = fromTableId, ToTableId = toTableId });
        if (response.IsSuccessStatusCode) return (true, null);
        return (false, await response.Content.ReadAsStringAsync());
    }
}
