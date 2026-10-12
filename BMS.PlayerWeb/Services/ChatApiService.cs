using System.Net.Http.Headers;
using System.Net.Http.Json;
using BMS.Shared.Dtos;

namespace BMS.PlayerWeb.Services;

// Đợt 2 phần D: Chat khách hàng <-> nhân viên.
public class ChatApiService
{
    private readonly HttpClient _http;
    private readonly AuthState _authState;

    public ChatApiService(HttpClient http, AuthState authState)
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

    public async Task<List<ChatThreadDto>> GetThreadsAsync()
    {
        try
        {
            ApplyAuthHeader();
            var result = await _http.GetFromJsonAsync<List<ChatThreadDto>>("api/chat/threads");
            return result ?? new List<ChatThreadDto>();
        }
        catch
        {
            return new List<ChatThreadDto>();
        }
    }

    public async Task<ChatThreadDto?> GetOrCreateCustomerThreadAsync()
    {
        try
        {
            ApplyAuthHeader();
            var response = await _http.PostAsJsonAsync("api/chat/threads", new CreateChatThreadRequestDto("CustomerStaff", null));
            if (!response.IsSuccessStatusCode) return null;
            return await response.Content.ReadFromJsonAsync<ChatThreadDto>();
        }
        catch
        {
            return null;
        }
    }

    public async Task<List<ChatMessageDto>> GetMessagesAsync(int threadId)
    {
        try
        {
            ApplyAuthHeader();
            var result = await _http.GetFromJsonAsync<List<ChatMessageDto>>($"api/chat/threads/{threadId}/messages");
            return result ?? new List<ChatMessageDto>();
        }
        catch
        {
            return new List<ChatMessageDto>();
        }
    }

    public async Task<(bool Success, string? Error)> SendMessageAsync(int threadId, string? text, string? imageUrl)
    {
        try
        {
            ApplyAuthHeader();
            var response = await _http.PostAsJsonAsync($"api/chat/threads/{threadId}/messages",
                new SendChatMessageRequestDto(threadId, text, imageUrl));
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
