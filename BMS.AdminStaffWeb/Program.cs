using BMS.AdminStaffWeb.Components;
using BMS.AdminStaffWeb.Services;

var builder = WebApplication.CreateBuilder(args);

// Đợt 16: định dạng số/tiền kiểu Việt Nam (60.000) như thiết kế Stitch - chỉ ảnh hưởng hiển
// thị; ô nhập type="number" của Blazor luôn bind theo InvariantCulture.
var viCulture = new System.Globalization.CultureInfo("vi-VN");
System.Globalization.CultureInfo.DefaultThreadCurrentCulture = viCulture;
System.Globalization.CultureInfo.DefaultThreadCurrentUICulture = viCulture;

builder.Services.AddRazorComponents()
    .AddInteractiveServerComponents();

var apiBaseUrl = builder.Configuration["Api:BaseUrl"] ?? "https://localhost:5001";

builder.Services.AddScoped<AuthState>();

// Dev-only: the first HTTPS call to the local dev certificate can take several
// seconds because .NET/Windows tries to check certificate revocation (OCSP/CRL)
// over the network, which stalls on a machine with no/limited internet access
// (common on lab VMs). CheckCertificateRevocationList = false removes that
// network round trip for calls between our own localhost projects. This is safe
// only because BMS.API is our own process on localhost - never do this for a
// real external HTTPS endpoint.
var isDevelopment = builder.Environment.IsDevelopment();

void ConfigureLocalDevHandler(IHttpClientBuilder httpClientBuilder)
{
    if (!isDevelopment)
    {
        return;
    }

    httpClientBuilder.ConfigurePrimaryHttpMessageHandler(() => new HttpClientHandler
    {
        CheckCertificateRevocationList = false
    });
}

ConfigureLocalDevHandler(builder.Services.AddHttpClient<AccountApiService>(client =>
{
    client.BaseAddress = new Uri(apiBaseUrl);
    client.Timeout = TimeSpan.FromSeconds(8);
}));

ConfigureLocalDevHandler(builder.Services.AddHttpClient<SettingsApiService>(client =>
{
    client.BaseAddress = new Uri(apiBaseUrl);
    client.Timeout = TimeSpan.FromSeconds(8);
}));

ConfigureLocalDevHandler(builder.Services.AddHttpClient<TableApiService>(client =>
{
    client.BaseAddress = new Uri(apiBaseUrl);
    client.Timeout = TimeSpan.FromSeconds(8);
}));

ConfigureLocalDevHandler(builder.Services.AddHttpClient<CueApiService>(client =>
{
    client.BaseAddress = new Uri(apiBaseUrl);
    client.Timeout = TimeSpan.FromSeconds(8);
}));

ConfigureLocalDevHandler(builder.Services.AddHttpClient<InventoryApiService>(client =>
{
    client.BaseAddress = new Uri(apiBaseUrl);
    client.Timeout = TimeSpan.FromSeconds(8);
}));

ConfigureLocalDevHandler(builder.Services.AddHttpClient<TournamentApiService>(client =>
{
    client.BaseAddress = new Uri(apiBaseUrl);
    client.Timeout = TimeSpan.FromSeconds(8);
}));

ConfigureLocalDevHandler(builder.Services.AddHttpClient<WalletApiService>(client =>
{
    client.BaseAddress = new Uri(apiBaseUrl);
    client.Timeout = TimeSpan.FromSeconds(8);
}));

ConfigureLocalDevHandler(builder.Services.AddHttpClient<ShiftApiService>(client =>
{
    client.BaseAddress = new Uri(apiBaseUrl);
    client.Timeout = TimeSpan.FromSeconds(8);
}));

ConfigureLocalDevHandler(builder.Services.AddHttpClient<AttendanceApiService>(client =>
{
    client.BaseAddress = new Uri(apiBaseUrl);
    client.Timeout = TimeSpan.FromSeconds(8);
}));

// Nhân viên đăng nhập ở cổng này: gọi món theo phiên chơi và hộp thư chat với khách.
ConfigureLocalDevHandler(builder.Services.AddHttpClient<OrderApiService>(client =>
{
    client.BaseAddress = new Uri(apiBaseUrl);
    client.Timeout = TimeSpan.FromSeconds(8);
}));

ConfigureLocalDevHandler(builder.Services.AddHttpClient<ChatApiService>(client =>
{
    client.BaseAddress = new Uri(apiBaseUrl);
    client.Timeout = TimeSpan.FromSeconds(8);
}));

builder.Services.AddScoped<StaffNotificationService>();

var app = builder.Build();

if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Error");
    app.UseHsts();
}

app.UseHttpsRedirection();
app.UseStaticFiles();
app.UseAntiforgery();

app.MapRazorComponents<App>()
    .AddInteractiveServerRenderMode();

// Redirect the home page to the login form (Staff and Admin share it; each role is sent
// to its own start page after signing in). We deliberately do this with a
// plain MapGet redirect instead of a Home.razor page that calls
// NavigationManager.NavigateTo() from OnInitialized() without a render mode set -
// that pattern threw a NavigationException during debugging in an earlier attempt.
app.MapGet("/", () => Results.Redirect("/login"));

app.Run();
