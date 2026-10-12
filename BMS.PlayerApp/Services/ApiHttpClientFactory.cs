namespace BMS.PlayerApp.Services;

/// <summary>
/// Creates the shared HttpClient used to talk to BMS.API. In DEBUG builds we trust the
/// ASP.NET Core development HTTPS certificate so the Android emulator can reach
/// https://10.0.2.2:5001 without a real, trusted certificate installed.
/// </summary>
public static class ApiHttpClientFactory
{
    public static HttpClient Create(ApiOptions options)
    {
#if DEBUG
        var handler = new HttpClientHandler
        {
            ServerCertificateCustomValidationCallback = (_, _, _, _) => true
        };
        var client = new HttpClient(handler);
#else
        var client = new HttpClient();
#endif
        client.BaseAddress = new Uri(options.BaseUrl);
        client.Timeout = TimeSpan.FromSeconds(10);
        return client;
    }
}
