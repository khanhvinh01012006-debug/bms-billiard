using BMS.PlayerWeb;
using BMS.PlayerWeb.Services;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.AspNetCore.Components.WebAssembly.Hosting;

var builder = WebAssemblyHostBuilder.CreateDefault(args);
builder.RootComponents.Add<App>("#app");
builder.RootComponents.Add<HeadOutlet>("head::after");

// Base URL of BMS.API - the single backend every client talks to over HTTP.
// Đọc từ wwwroot/appsettings.json (Api:BaseUrl) để đổi địa chỉ API khi triển khai mà
// không phải sửa code; mặc định vẫn là API chạy local.
var apiBaseUrl = (builder.Configuration["Api:BaseUrl"] ?? "https://localhost:5001").TrimEnd('/');

// Mọi request tới API tự mang JWT (xem JwtAuthorizationHandler).
builder.Services.AddScoped(sp => new HttpClient(new JwtAuthorizationHandler(sp.GetRequiredService<AuthState>()) { InnerHandler = new HttpClientHandler() })
{
    BaseAddress = new Uri(apiBaseUrl),
    Timeout = TimeSpan.FromSeconds(8)
});
builder.Services.AddScoped<TableApiService>();
builder.Services.AddScoped<CueApiService>();
builder.Services.AddScoped<PlayerApiService>();
builder.Services.AddScoped<AccountApiService>();
builder.Services.AddScoped<ChatApiService>();
builder.Services.AddScoped<TournamentApiService>();
builder.Services.AddScoped<WalletApiService>();
builder.Services.AddScoped<AuthState>();
builder.Services.AddScoped<AuthStorageService>();
builder.Services.AddScoped<MyBookingsService>();
builder.Services.AddSingleton(new ApiOptions(apiBaseUrl));

// Đợt 16: định dạng số/tiền kiểu Việt Nam (90.000đ) như thiết kế Stitch, thay cho kiểu
// mặc định của trình duyệt (90,000). Chỉ ảnh hưởng hiển thị - ô nhập type="number" của
// Blazor luôn bind theo InvariantCulture nên không đổi cách nhập liệu.
var viCulture = new System.Globalization.CultureInfo("vi-VN");
System.Globalization.CultureInfo.DefaultThreadCurrentCulture = viCulture;
System.Globalization.CultureInfo.DefaultThreadCurrentUICulture = viCulture;

await builder.Build().RunAsync();
