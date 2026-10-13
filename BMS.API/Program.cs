using System.Text;
using BMS.API.Data;
using BMS.API.Hubs;
using BMS.API.Models;
using BMS.API.Services;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.FileProviders;
using Microsoft.IdentityModel.Tokens;
using Microsoft.OpenApi.Models;

var builder = WebApplication.CreateBuilder(args);

// --- Thư mục dữ liệu (bms.db + uploads) ---
// Storage:DataDirectory (biến môi trường Storage__DataDirectory, ví dụ /data trong Docker)
// gom bms.db và ảnh upload vào một thư mục cố định để không mất khi build lại container.
// Để trống thì giữ nguyên cách chạy local cũ: bms.db theo ConnectionStrings, ảnh ở wwwroot/uploads.
var connectionString = builder.Configuration.GetConnectionString("DefaultConnection");
string? uploadsDirectory = null;
var dataDirectory = builder.Configuration["Storage:DataDirectory"];
if (!string.IsNullOrWhiteSpace(dataDirectory))
{
    dataDirectory = Path.GetFullPath(dataDirectory);
    uploadsDirectory = Path.Combine(dataDirectory, "uploads");
    Directory.CreateDirectory(uploadsDirectory);
    connectionString = $"Data Source={Path.Combine(dataDirectory, "bms.db")}";
}
builder.Services.AddSingleton(new StorageOptions(uploadsDirectory));
// Cấu hình chung (settings.json): cạnh bms.db trong thư mục dữ liệu, chạy local thì ở App_Data.
builder.Services.AddSingleton(new ClubSettingsStore(
    !string.IsNullOrWhiteSpace(dataDirectory) ? dataDirectory : Path.Combine(builder.Environment.ContentRootPath, "App_Data")));

// --- EF Core (Sqlite) ---
builder.Services.AddDbContext<BmsDbContext>(options =>
    options.UseSqlite(connectionString));

// --- SignalR ---
builder.Services.AddSignalR();

// --- Auth services ---
builder.Services.AddSingleton<JwtTokenService>();

// --- Email/OTP sender (chọn qua config Email:Provider = "Dev" (mặc định) | "Smtp") ---
var emailProvider = builder.Configuration["Email:Provider"] ?? "Dev";
if (string.Equals(emailProvider, "Smtp", StringComparison.OrdinalIgnoreCase))
{
    builder.Services.AddScoped<IEmailSender, SmtpEmailSender>();
}
else
{
    builder.Services.AddScoped<IEmailSender, DevEmailSender>();
}

// OTP SMS: real provider integration TODO - always Dev mode for now (see IOtpSender.cs).
builder.Services.AddScoped<IOtpSender, DevOtpSender>();

// Quét/tự hủy các booking "Pending" quá hạn giữ chỗ (15 phút) chưa được nhân viên xác nhận.
builder.Services.AddHostedService<BookingExpiryHostedService>();

// --- JWT Bearer authentication ---
var jwtKey = builder.Configuration["Jwt:Key"]
    ?? throw new InvalidOperationException("Jwt:Key is not configured (đặt biến môi trường Jwt__Key).");
if (Encoding.UTF8.GetByteCount(jwtKey) < 32)
{
    throw new InvalidOperationException("Jwt:Key phải dài ít nhất 32 ký tự.");
}
var jwtIssuer = builder.Configuration["Jwt:Issuer"]
    ?? throw new InvalidOperationException("Jwt:Issuer is not configured.");

builder.Services.AddAuthentication(options =>
{
    options.DefaultAuthenticateScheme = JwtBearerDefaults.AuthenticationScheme;
    options.DefaultChallengeScheme = JwtBearerDefaults.AuthenticationScheme;
})
.AddJwtBearer(options =>
{
    // Without this, ASP.NET Core silently remaps short JWT claim names to long legacy
    // ClaimTypes.* URIs when validating an incoming token (e.g. "sub" -> ClaimTypes.
    // NameIdentifier). JwtTokenService mints the "sub" claim using the short name
    // (JwtRegisteredClaimNames.Sub), so AccountsController.GetCurrentAccountAsync()'s
    // User.FindFirst(JwtRegisteredClaimNames.Sub) was silently returning null for every
    // request (root cause of GET/PUT api/accounts/profile always failing with 401/404,
    // which the client then treated as an expired session and auto-signed the user out
    // of - happened for Player and Staff accounts alike, on any page that loaded the
    // profile: Hồ sơ cá nhân, Ghép trận). Every other controller already reads the
    // custom "userId"/"fullName" claims, which are NOT touched by this remapping, which
    // is why those endpoints worked fine the whole time.
    options.MapInboundClaims = false;

    options.TokenValidationParameters = new TokenValidationParameters
    {
        ValidateIssuer = true,
        ValidIssuer = jwtIssuer,
        ValidateAudience = true,
        ValidAudience = jwtIssuer,
        ValidateIssuerSigningKey = true,
        IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtKey)),
        ValidateLifetime = true,
        ClockSkew = TimeSpan.FromMinutes(1)
    };

    // Allow SignalR clients to pass the JWT via query string, since the browser
    // WebSocket API cannot set an Authorization header.
    options.Events = new JwtBearerEvents
    {
        OnMessageReceived = context =>
        {
            var accessToken = context.Request.Query["access_token"];
            var path = context.HttpContext.Request.Path;
            if (!string.IsNullOrEmpty(accessToken) && path.StartsWithSegments("/hubs"))
            {
                context.Token = accessToken;
            }
            return Task.CompletedTask;
        }
    };
});

// Mặc định MỌI endpoint đều yêu cầu JWT hợp lệ (FallbackPolicy). Endpoint công khai (đăng nhập,
// đăng ký, xem danh sách bàn/cơ/giải đấu...) phải ghi rõ [AllowAnonymous] - thêm endpoint mới mà
// quên gắn quyền thì vẫn được bảo vệ, không vô tình mở ra ngoài.
builder.Services.AddAuthorization(options =>
{
    options.FallbackPolicy = new Microsoft.AspNetCore.Authorization.AuthorizationPolicyBuilder()
        .AddAuthenticationSchemes(JwtBearerDefaults.AuthenticationScheme)
        .RequireAuthenticatedUser()
        .Build();
});

// --- Controllers ---
builder.Services.AddControllers();

// --- Swagger (with Bearer auth support) ---
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(options =>
{
    options.SwaggerDoc("v1", new OpenApiInfo { Title = "BMS API", Version = "v1" });

    var securityScheme = new OpenApiSecurityScheme
    {
        Name = "Authorization",
        Type = SecuritySchemeType.Http,
        Scheme = "bearer",
        BearerFormat = "JWT",
        In = ParameterLocation.Header,
        Description = "Nhập token dạng: Bearer {token}",
        Reference = new OpenApiReference
        {
            Type = ReferenceType.SecurityScheme,
            Id = "Bearer"
        }
    };

    options.AddSecurityDefinition("Bearer", securityScheme);
    options.AddSecurityRequirement(new OpenApiSecurityRequirement
    {
        { securityScheme, Array.Empty<string>() }
    });
});

// --- CORS ---
// Chỉ cho phép các origin liệt kê trong Cors:AllowedOrigins (biến môi trường
// Cors__AllowedOrigins, phân tách bằng dấu phẩy hoặc chấm phẩy). Khi chạy Development
// thì cho thêm Admin Web (5101/5100) và Player Web (5201/5200) trên localhost.
var allowedOrigins = (builder.Configuration["Cors:AllowedOrigins"] ?? string.Empty)
    .Split([',', ';'], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
    .Select(origin => origin.TrimEnd('/'))
    .ToList();
if (builder.Environment.IsDevelopment())
{
    allowedOrigins.AddRange([
        "https://localhost:5101", "http://localhost:5100",
        "https://localhost:5201", "http://localhost:5200"
    ]);
}

builder.Services.AddCors(options =>
{
    options.AddPolicy("BmsClients", policy =>
    {
        policy.WithOrigins(allowedOrigins.Distinct().ToArray())
              .AllowAnyHeader()
              .AllowAnyMethod()
              .AllowCredentials();
    });
});

var app = builder.Build();

// --- DB creation + seed ---
// NOTE: We intentionally use EnsureCreated() instead of real EF Core migrations for this
// first delivery, since the schema is still being iterated on quickly. TODO: switch to
// `dotnet ef migrations add InitialCreate` + Database.Migrate() once the schema stabilizes.
using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<BmsDbContext>();
    db.Database.EnsureCreated();

    // Mật khẩu admin ban đầu lấy từ Admin:InitialPassword (biến môi trường
    // Admin__InitialPassword); appsettings.Development.json giữ Admin@123 cho chạy local.
    var adminEmail = app.Configuration["Admin:Email"] ?? "admin@bms.local";
    var adminPassword = app.Configuration["Admin:InitialPassword"];

    if (!db.Accounts.Any(a => a.Role == "Admin"))
    {
        if (string.IsNullOrWhiteSpace(adminPassword))
        {
            throw new InvalidOperationException(
                "Chưa có tài khoản Admin: cần đặt Admin:InitialPassword (biến môi trường Admin__InitialPassword).");
        }

        var (hash, salt) = PasswordHasher.HashPassword(adminPassword);
        db.Accounts.Add(new AccountEntity
        {
            FullName = "Quản trị viên",
            Email = adminEmail,
            PasswordHash = hash,
            PasswordSalt = salt,
            Role = "Admin",
            CreatedAtUtc = DateTime.UtcNow
        });
        db.SaveChanges();
    }
    else if (app.Configuration.GetValue<bool>("Admin:ResetPasswordOnStartup")
             && !string.IsNullOrWhiteSpace(adminPassword))
    {
        // Dùng khi đưa database demo có sẵn lên máy chủ: đặt lại mật khẩu của tài khoản
        // Admin:Email theo Admin:InitialPassword thay cho mật khẩu mặc định Admin@123.
        var admin = db.Accounts.FirstOrDefault(a => a.Role == "Admin" && a.Email == adminEmail);
        if (admin is not null)
        {
            var (hash, salt) = PasswordHasher.HashPassword(adminPassword);
            admin.PasswordHash = hash;
            admin.PasswordSalt = salt;
            db.SaveChanges();
        }
    }
}

// Swagger: luôn bật khi chạy local (Development). Trên máy chủ (Production) mặc định TẮT để không
// lộ danh sách API; bật tạm khi cần demo bằng biến môi trường Swagger__Enabled=true (SWAGGER_ENABLED
// trong .env). Swagger chỉ là trang mô tả - mọi API vẫn được JWT bảo vệ như bình thường.
if (app.Environment.IsDevelopment() || app.Configuration.GetValue<bool>("Swagger:Enabled"))
{
    app.UseSwagger();
    app.UseSwaggerUI(options => options.DocumentTitle = "BMS API - Swagger");
}

app.UseStaticFiles(); // serves wwwroot/uploads
if (uploadsDirectory is not null)
{
    // Ảnh upload nằm trong thư mục dữ liệu, vẫn phục vụ ở cùng đường dẫn /uploads/...
    app.UseStaticFiles(new StaticFileOptions
    {
        FileProvider = new PhysicalFileProvider(uploadsDirectory),
        RequestPath = "/uploads"
    });
}

app.UseCors("BmsClients");

app.UseAuthentication();
app.UseAuthorization();

app.MapControllers();
app.MapHub<BmsHub>("/hubs/bms").AllowAnonymous(); // chỉ phát sự kiện công khai (trạng thái bàn...)

app.Run();
