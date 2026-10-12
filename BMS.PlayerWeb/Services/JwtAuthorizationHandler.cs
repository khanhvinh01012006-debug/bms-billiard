using System.Net.Http.Headers;

namespace BMS.PlayerWeb.Services;

/// <summary>
/// Gắn JWT của người đang đăng nhập vào MỌI request gửi tới BMS.API (header
/// "Authorization: Bearer ..."). API mặc định từ chối request không có token hợp lệ (401),
/// nên mọi service dùng chung HttpClient đều tự được xác thực - không phụ thuộc từng service
/// tự gắn header.
/// </summary>
public class JwtAuthorizationHandler : DelegatingHandler
{
    private readonly AuthState _authState;

    public JwtAuthorizationHandler(AuthState authState) => _authState = authState;

    protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        if (request.Headers.Authorization is null && !string.IsNullOrEmpty(_authState.Token))
        {
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", _authState.Token);
        }

        return base.SendAsync(request, cancellationToken);
    }
}
