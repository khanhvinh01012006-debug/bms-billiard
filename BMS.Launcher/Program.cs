using System.Diagnostics;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Microsoft.AspNetCore.StaticFiles;
using Microsoft.Extensions.FileProviders;

// BMS bản Windows: bấm đúp BMS.exe là chạy cả hệ thống trên máy này.
//   api\BMS.API.exe              -> http://localhost:5080   (máy chủ dữ liệu)
//   admin\BMS.AdminStaffWeb.exe  -> http://localhost:5180   (cổng Nhân viên / Quản trị)
//   player\wwwroot (file tĩnh)   -> http://localhost:5280   (trang Người chơi, do chính BMS.exe phục vụ)
// Dữ liệu (bms.db, ảnh, cấu hình) nằm ở thư mục data\ cạnh BMS.exe - sao lưu = chép thư mục data.

Console.OutputEncoding = Encoding.UTF8;
Console.Title = "BMS Billiard - đang chạy (đóng cửa sổ này để tắt)";
const int ApiPort = 5080, AdminPort = 5180, PlayerPort = 5280;
var apiUrl = $"http://localhost:{ApiPort}";
var adminUrl = $"http://localhost:{AdminPort}";
var playerUrl = $"http://localhost:{PlayerPort}";

var baseDir = AppContext.BaseDirectory;
var dataDir = Path.Combine(baseDir, "data");
Directory.CreateDirectory(Path.Combine(dataDir, "uploads"));

// Lần chạy đầu: chép database demo.
var dbPath = Path.Combine(dataDir, "bms.db");
if (!File.Exists(dbPath))
{
    File.Copy(Path.Combine(baseDir, "seed", "bms.db"), dbPath);
    Console.WriteLine("Lần chạy đầu: đã tạo dữ liệu mẫu (data\\bms.db).");
}

// Khóa ký đăng nhập: sinh ngẫu nhiên 1 lần cho mỗi máy, lưu trong data\ (không dùng chung khóa).
var keyPath = Path.Combine(dataDir, "jwt.key");
if (!File.Exists(keyPath)) File.WriteAllText(keyPath, Convert.ToBase64String(RandomNumberGenerator.GetBytes(48)));
var jwtKey = File.ReadAllText(keyPath).Trim();

// Trang Người chơi đọc địa chỉ API / cổng NV từ appsettings.json.
var playerRoot = Path.Combine(baseDir, "player", "wwwroot");
File.WriteAllText(Path.Combine(playerRoot, "appsettings.json"), JsonSerializer.Serialize(new
{
    Api = new { BaseUrl = apiUrl },
    StaffPortal = new { Url = adminUrl },
}));

foreach (var port in new[] { ApiPort, AdminPort, PlayerPort })
{
    if (IsPortBusy(port))
    {
        Console.WriteLine($"Cổng {port} đang bị chương trình khác dùng (có thể BMS đang chạy ở cửa sổ khác).");
        Console.WriteLine("Hãy đóng cửa sổ BMS cũ rồi mở lại. Nhấn Enter để thoát.");
        Console.ReadLine();
        return 1;
    }
}

// Lần chạy đầu: chủ quán tự đặt mật khẩu Admin (thay mật khẩu mặc định của dữ liệu mẫu).
// Mật khẩu chỉ truyền cho API qua biến môi trường của lần chạy này, không lưu ra file.
var adminSetMarker = Path.Combine(dataDir, "admin-password.set");
string? newAdminPassword = null;
if (!File.Exists(adminSetMarker))
{
    Console.WriteLine();
    Console.WriteLine("  LẦN CHẠY ĐẦU - đặt mật khẩu cho tài khoản Quản trị: admin@bms.local");
    while (newAdminPassword is null)
    {
        var p1 = ReadHidden("  Mật khẩu mới (ít nhất 8 ký tự): ");
        if (p1.Length < 8) { Console.WriteLine("  Mật khẩu quá ngắn."); continue; }
        var p2 = ReadHidden("  Nhập lại mật khẩu: ");
        if (p1 != p2) { Console.WriteLine("  Hai lần nhập không khớp."); continue; }
        newAdminPassword = p1;
    }
}

var children = new List<Process>();
void KillChildren()
{
    foreach (var p in children)
    {
        try { if (!p.HasExited) p.Kill(entireProcessTree: true); } catch { /* đã tắt */ }
    }
}
AppDomain.CurrentDomain.ProcessExit += (_, _) => KillChildren();
Console.CancelKeyPress += (_, _) => KillChildren();

var apiEnv = new Dictionary<string, string>
{
    ["ASPNETCORE_URLS"] = apiUrl,
    ["ASPNETCORE_ENVIRONMENT"] = "Production",
    ["TZ"] = "Asia/Ho_Chi_Minh",
    ["Storage__DataDirectory"] = dataDir,
    ["Jwt__Key"] = jwtKey,
    ["Cors__AllowedOrigins"] = $"{playerUrl},{adminUrl},http://127.0.0.1:{PlayerPort},http://127.0.0.1:{AdminPort}",
};
if (newAdminPassword is not null)
{
    apiEnv["Admin__InitialPassword"] = newAdminPassword;
    apiEnv["Admin__ResetPasswordOnStartup"] = "true";
}
children.Add(StartChild(Path.Combine(baseDir, "api", "BMS.API.exe"), "API", apiEnv));
children.Add(StartChild(Path.Combine(baseDir, "admin", "BMS.AdminStaffWeb.exe"), "Admin", new()
{
    ["ASPNETCORE_URLS"] = adminUrl,
    ["ASPNETCORE_ENVIRONMENT"] = "Production",
    ["Api__BaseUrl"] = apiUrl,
    ["PlayerSite__Url"] = playerUrl,
}));

// Phục vụ trang Người chơi (Blazor WebAssembly) + SPA fallback về index.html.
var builder = WebApplication.CreateSlimBuilder(new WebApplicationOptions { ContentRootPath = baseDir, WebRootPath = playerRoot });
builder.WebHost.UseUrls(playerUrl);
builder.Logging.ClearProviders();
var app = builder.Build();
var types = new FileExtensionContentTypeProvider();
types.Mappings[".webmanifest"] = "application/manifest+json";
types.Mappings[".dat"] = "application/octet-stream";
types.Mappings[".blat"] = "application/octet-stream";
var files = new PhysicalFileProvider(playerRoot);
app.Use(async (ctx, next) =>
{
    // Không cache cứng các file điều khiển cập nhật PWA.
    var path = ctx.Request.Path.Value ?? "/";
    if (path == "/" || path.EndsWith(".html") || path.EndsWith("service-worker.js") || path.EndsWith("service-worker-assets.js") || path.EndsWith("appsettings.json"))
    {
        ctx.Response.Headers.CacheControl = "no-cache";
    }
    await next();
});
app.UseDefaultFiles(new DefaultFilesOptions { FileProvider = files });
app.UseStaticFiles(new StaticFileOptions { FileProvider = files, ContentTypeProvider = types, ServeUnknownFileTypes = false });
app.MapFallback(async ctx =>
{
    ctx.Response.ContentType = "text/html; charset=utf-8";
    ctx.Response.Headers.CacheControl = "no-cache";
    await ctx.Response.SendFileAsync(Path.Combine(playerRoot, "index.html"));
});
await app.StartAsync();

Console.WriteLine();
Console.WriteLine("  ================= BMS BILLIARD ĐANG CHẠY =================");
Console.WriteLine($"  Trang Người chơi (khách):     {playerUrl}");
Console.WriteLine($"  Cổng Nhân viên / Quản trị:    {adminUrl}");
Console.WriteLine($"  Máy chủ dữ liệu (API):        {apiUrl}");
Console.WriteLine($"  Dữ liệu:                      {dataDir}");
Console.WriteLine("  Đóng cửa sổ này để tắt BMS.");
Console.WriteLine("  ===========================================================");

if (await WaitReady($"{apiUrl}/api/settings") && await WaitReady($"{adminUrl}/login"))
{
    if (newAdminPassword is not null)
    {
        File.WriteAllText(adminSetMarker, DateTime.Now.ToString("yyyy-MM-dd HH:mm"));
        Console.WriteLine("  Đã đặt mật khẩu Admin. Đăng nhập cổng Quản trị bằng admin@bms.local + mật khẩu vừa đặt.");
    }
    Console.WriteLine("  Sẵn sàng. Đang mở trình duyệt...");
    if (!args.Contains("--no-browser"))
    {
        OpenBrowser(playerUrl);
        OpenBrowser($"{adminUrl}/login");
    }
}
else
{
    Console.WriteLine("  Không khởi động được API hoặc cổng Admin - xem thông báo phía trên.");
}

await app.WaitForShutdownAsync();
KillChildren();
return 0;

static Process StartChild(string exe, string tag, Dictionary<string, string> env)
{
    var psi = new ProcessStartInfo(exe)
    {
        WorkingDirectory = Path.GetDirectoryName(exe)!,
        UseShellExecute = false,
        RedirectStandardOutput = true,
        RedirectStandardError = true,
        StandardOutputEncoding = Encoding.UTF8,
        StandardErrorEncoding = Encoding.UTF8,
        CreateNoWindow = true,
    };
    foreach (var (k, v) in env) psi.Environment[k] = v;
    var p = Process.Start(psi) ?? throw new InvalidOperationException($"Không chạy được {exe}");
    // Chỉ in cảnh báo/lỗi của tiến trình con cho gọn cửa sổ.
    void Pipe(string? line)
    {
        if (line is null) return;
        if (line.Contains("fail:") || line.Contains("crit:") || line.Contains("Exception") || line.Contains("Unhandled"))
            Console.WriteLine($"  [{tag}] {line}");
    }
    p.OutputDataReceived += (_, e) => Pipe(e.Data);
    p.ErrorDataReceived += (_, e) => Pipe(e.Data);
    p.BeginOutputReadLine();
    p.BeginErrorReadLine();
    return p;
}

static async Task<bool> WaitReady(string url)
{
    using var http = new HttpClient { Timeout = TimeSpan.FromSeconds(3) };
    for (var i = 0; i < 60; i++)
    {
        try
        {
            if ((await http.GetAsync(url)).IsSuccessStatusCode) return true;
        }
        catch
        {
            // chưa lên
        }
        await Task.Delay(1000);
    }
    return false;
}

static string ReadHidden(string prompt)
{
    Console.Write(prompt);
    if (Console.IsInputRedirected)
    {
        // chạy tự động (script cài đặt / kiểm thử): đọc từ stdin
        Console.WriteLine();
        return Console.ReadLine() ?? "";
    }
    var sb = new StringBuilder();
    while (true)
    {
        var k = Console.ReadKey(intercept: true);
        if (k.Key == ConsoleKey.Enter) break;
        if (k.Key == ConsoleKey.Backspace) { if (sb.Length > 0) { sb.Length--; Console.Write("\b \b"); } continue; }
        if (!char.IsControl(k.KeyChar)) { sb.Append(k.KeyChar); Console.Write('*'); }
    }
    Console.WriteLine();
    return sb.ToString();
}

static bool IsPortBusy(int port)
{
    try
    {
        var l = new System.Net.Sockets.TcpListener(System.Net.IPAddress.Loopback, port);
        l.Start();
        l.Stop();
        return false;
    }
    catch
    {
        return true;
    }
}

static void OpenBrowser(string url)
{
    try { Process.Start(new ProcessStartInfo(url) { UseShellExecute = true }); } catch { /* không mở được thì người dùng tự mở */ }
}
