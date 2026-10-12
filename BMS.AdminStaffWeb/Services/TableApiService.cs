using System.Net.Http.Headers;
using System.Net.Http.Json;
using BMS.Shared.Dtos;

namespace BMS.AdminStaffWeb.Services;

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

    public async Task<(bool Success, string? Error)> TransferAsync(int fromTableId, int toTableId)
    {
        ApplyAuthHeader();
        var response = await _http.PostAsJsonAsync("api/sessions/transfer", new { FromTableId = fromTableId, ToTableId = toTableId });
        if (response.IsSuccessStatusCode) return (true, null);
        return (false, await response.Content.ReadAsStringAsync());
    }

    public Task<(bool Success, string? Error)> PauseSessionAsync(int sessionId) => PostSessionActionAsync(sessionId, "pause");

    public Task<(bool Success, string? Error)> ResumeSessionAsync(int sessionId) => PostSessionActionAsync(sessionId, "resume");

    private async Task<(bool Success, string? Error)> PostSessionActionAsync(int sessionId, string action)
    {
        ApplyAuthHeader();
        var response = await _http.PostAsync($"api/sessions/{sessionId}/{action}", null);
        if (response.IsSuccessStatusCode) return (true, null);
        return (false, await response.Content.ReadAsStringAsync());
    }

    public async Task<(bool Success, string? Error)> CreateBookingAsync(
        int tableId, string customerName, string? phoneNumber = null, string? note = null, int durationMinutes = 120)
    {
        ApplyAuthHeader();
        var response = await _http.PostAsJsonAsync("api/bookings",
            new CreateBookingRequestDto(tableId, customerName, null, phoneNumber, note, durationMinutes));
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

    // --- Admin CRUD (requires Bearer token) ---

    public async Task<(bool Success, string? Error)> CreateTableAsync(UpsertTableRequestDto request)
    {
        ApplyAuthHeader();
        var response = await _http.PostAsJsonAsync("api/tables", request);
        if (response.IsSuccessStatusCode) return (true, null);
        return (false, await response.Content.ReadAsStringAsync());
    }

    public async Task<(bool Success, string? Error)> UpdateTableAsync(int id, UpsertTableRequestDto request)
    {
        ApplyAuthHeader();
        var response = await _http.PutAsJsonAsync($"api/tables/{id}", request);
        if (response.IsSuccessStatusCode) return (true, null);
        return (false, await response.Content.ReadAsStringAsync());
    }

    public async Task<(bool Success, string? Error)> DeleteTableAsync(int id)
    {
        ApplyAuthHeader();
        var response = await _http.DeleteAsync($"api/tables/{id}");
        if (response.IsSuccessStatusCode) return (true, null);
        return (false, await response.Content.ReadAsStringAsync());
    }

    // --- Đợt 5: Loại bàn + khung giờ tính giá (Admin CRUD) ---

    public async Task<List<TableTypeDto>> GetTableTypesAsync()
    {
        var result = await _http.GetFromJsonAsync<List<TableTypeDto>>("api/table-types");
        return result ?? new List<TableTypeDto>();
    }

    public async Task<(bool Success, string? Error)> CreateTableTypeAsync(UpsertTableTypeRequestDto request)
    {
        ApplyAuthHeader();
        var response = await _http.PostAsJsonAsync("api/table-types", request);
        if (response.IsSuccessStatusCode) return (true, null);
        return (false, await response.Content.ReadAsStringAsync());
    }

    public async Task<(bool Success, string? Error)> UpdateTableTypeAsync(int id, UpsertTableTypeRequestDto request)
    {
        ApplyAuthHeader();
        var response = await _http.PutAsJsonAsync($"api/table-types/{id}", request);
        if (response.IsSuccessStatusCode) return (true, null);
        return (false, await response.Content.ReadAsStringAsync());
    }

    public async Task<(bool Success, string? Error)> DeleteTableTypeAsync(int id)
    {
        ApplyAuthHeader();
        var response = await _http.DeleteAsync($"api/table-types/{id}");
        if (response.IsSuccessStatusCode) return (true, null);
        return (false, await response.Content.ReadAsStringAsync());
    }

    public async Task<(bool Success, string? Error)> AddPricingSlotAsync(int tableTypeId, UpsertPricingSlotRequestDto request)
    {
        ApplyAuthHeader();
        var response = await _http.PostAsJsonAsync($"api/table-types/{tableTypeId}/pricing-slots", request);
        if (response.IsSuccessStatusCode) return (true, null);
        return (false, await response.Content.ReadAsStringAsync());
    }

    public async Task<(bool Success, string? Error)> UpdatePricingSlotAsync(int slotId, UpsertPricingSlotRequestDto request)
    {
        ApplyAuthHeader();
        var response = await _http.PutAsJsonAsync($"api/table-types/pricing-slots/{slotId}", request);
        if (response.IsSuccessStatusCode) return (true, null);
        return (false, await response.Content.ReadAsStringAsync());
    }

    public async Task<(bool Success, string? Error)> DeletePricingSlotAsync(int slotId)
    {
        ApplyAuthHeader();
        var response = await _http.DeleteAsync($"api/table-types/pricing-slots/{slotId}");
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

    // Đợt 9: Dashboard - bản đồ nhiệt công suất bàn theo giờ trong ngày.
    public async Task<List<HourlyOccupancyDto>> GetOccupancyHeatmapAsync(DateTime? date = null)
    {
        try
        {
            ApplyAuthHeader();
            var url = date is DateTime d ? $"api/sessions/occupancy-heatmap?date={d:yyyy-MM-dd}" : "api/sessions/occupancy-heatmap";
            var result = await _http.GetFromJsonAsync<List<HourlyOccupancyDto>>(url);
            return result ?? new List<HourlyOccupancyDto>();
        }
        catch
        {
            return new List<HourlyOccupancyDto>();
        }
    }

    public async Task<(bool Success, string? Error)> CheckInLateBookingAsync(int bookingId)
    {
        ApplyAuthHeader();
        var response = await _http.PutAsync($"api/bookings/{bookingId}/checkin-late", null);
        if (response.IsSuccessStatusCode) return (true, null);
        return (false, await response.Content.ReadAsStringAsync());
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

    public async Task<(bool Success, string? Error)> ConfirmBookingAsync(int bookingId)
    {
        ApplyAuthHeader();
        var response = await _http.PutAsync($"api/bookings/{bookingId}/confirm", null);
        if (response.IsSuccessStatusCode) return (true, null);
        return (false, await response.Content.ReadAsStringAsync());
    }

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
}
