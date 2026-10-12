# Bảo vệ backend (BMS.API) bằng JWT

> Tài liệu dùng để **trình bày và trả lời câu hỏi** về bảo mật API. Mọi đường dẫn file và số dòng
> trỏ đúng vào mã nguồn trong repo. Phần cuối có **kịch bản trình bày 5 phút** và **câu hỏi thường gặp**.

**Mục lục**
1. JWT là gì, vì sao dùng
2. Toàn cảnh: JWT đi qua những đâu trong đồ án
3. Bước 1 – Đăng nhập: máy chủ cấp token
4. Bước 2 – Client cất token và gửi kèm mỗi request
5. Bước 3 – API kiểm tra token
6. Bước 4 – Phân quyền: ai được gọi API nào
7. Bước 5 – Kiểm tra quyền sở hữu dữ liệu (dùng `userId` trong token)
8. Khóa bí mật và cấu hình
9. Bảng phân quyền đầy đủ
10. Hướng dẫn demo (curl, Postman, Swagger, jwt.io)
11. Hướng dẫn thêm một API mới có bảo vệ
12. Đổi khóa JWT khi bị lộ
13. Kiểm thử bảo mật đã chạy
14. Kịch bản trình bày 5 phút
15. Câu hỏi thường gặp

---

## 1. JWT là gì, vì sao dùng

**JWT (JSON Web Token)** là một chuỗi gồm 3 phần ngăn bởi dấu chấm:

```
eyJhbGciOiJIUzI1NiIs...  .  eyJzdWIiOiJhYmNAZXhhbXBsZS5...  .  Qm9hV2xXc1J4...
       HEADER                          PAYLOAD                        SIGNATURE
 (thuật toán: HS256)        (ai, vai trò gì, hết hạn khi nào)    (chữ ký bằng khóa bí mật)
```

- **Header** và **payload** chỉ được mã hóa Base64, ai cũng đọc được, nhưng **không sửa được**:
  sửa 1 ký tự là chữ ký không còn khớp.
- **Chữ ký** = HMAC-SHA256(header + payload, **khóa bí mật**). Chỉ máy chủ có khóa, nên chỉ máy chủ
  tạo được token hợp lệ.

**Vì sao BMS dùng JWT:**
- API phục vụ 2 loại client khác nhau: trang Người chơi (Blazor WebAssembly chạy trong trình duyệt)
  và cổng Nhân viên/Admin (Blazor Server). Cả hai chỉ cần gửi chung một header là được xác thực.
- **Không cần lưu phiên trên máy chủ (stateless):** API không phải nhớ ai đang đăng nhập; mỗi request tự mang
  "vé" của mình. Mở rộng hay khởi động lại API cũng không làm người dùng bị đăng xuất.
- Token mang sẵn `role` và `userId`, nên API phân quyền và biết "ai đang gọi" mà không cần truy vấn thêm.

---

## 2. Toàn cảnh: JWT đi qua những đâu trong đồ án

```
 ① Đăng nhập                       ② Cất token               ③ Gọi API kèm token
 Login.razor ──POST /login──▶ AccountsController.Login        AuthState.Token
   (email + mật khẩu)          │  kiểm tra mật khẩu (PBKDF2)    + localStorage/sessionStorage (trang khách)
                               └─ JwtTokenService.GenerateToken ──token──▶ ...
                                                                          │
   ┌──────────────────────────────────────────────────────────────────────┘
   ▼
 Authorization: Bearer <token>  ──▶  BMS.API
                                     ④ UseAuthentication: kiểm tra chữ ký, issuer, audience, hạn
                                     ⑤ UseAuthorization: FallbackPolicy / [Authorize(Roles=...)]
                                     ⑥ Controller: đọc User.FindFirst("userId") để lấy đúng dữ liệu của người gọi
                                        └─▶ 200 OK │ 401 (chưa/sai token) │ 403 (không đủ quyền)
```

| Thành phần | File | Vai trò |
|---|---|---|
| Tạo token | `BMS.API/Services/JwtTokenService.cs` | Ghi claim, ký HS256, hạn 12 giờ |
| Cấp token | `BMS.API/Controllers/AccountsController.cs` (Login dòng ~119, Register dòng ~53) | Kiểm tra mật khẩu rồi gọi `GenerateToken` |
| Cấu hình kiểm tra | `BMS.API/Program.cs` dòng 60–131 | `AddAuthentication().AddJwtBearer(...)`, `AddAuthorization(FallbackPolicy)` |
| Bật middleware | `BMS.API/Program.cs` dòng 259–260 | `UseAuthentication()`, `UseAuthorization()` |
| Gắn quyền | Các file `BMS.API/Controllers/*.cs` | `[Authorize]`, `[Authorize(Roles = ...)]`, `[AllowAnonymous]` |
| Lấy người gọi | Controller, ví dụ `WalletController.cs` dòng 49–52 | `User.FindFirst("userId")` |
| Cất token (khách) | `BMS.PlayerWeb/Services/AuthState.cs`, `AuthStorageService.cs` | Giữ trong bộ nhớ và localStorage/sessionStorage |
| Gửi token (khách) | `BMS.PlayerWeb/Services/JwtAuthorizationHandler.cs`, `BMS.PlayerWeb/Program.cs` dòng 15–20 | Tự gắn `Authorization: Bearer` vào mọi request |
| Cất và gửi token (NV/Admin) | `BMS.AdminStaffWeb/Services/AuthState.cs`, `ApplyAuthHeader()` trong từng `*ApiService.cs` | Token giữ trong phiên phía máy chủ Blazor |
| Khóa bí mật | `.env` (trên VPS) → `docker-compose.yml` dòng 16 `Jwt__Key` | Không nằm trong mã nguồn |

---

## 3. Bước 1 – Đăng nhập: máy chủ cấp token

**`BMS.API/Controllers/AccountsController.cs` – `Login`** (rút gọn):

```csharp
[AllowAnonymous]                 // đăng nhập thì chưa có token -> phải cho phép
[HttpPost("login")]
public async Task<ActionResult<LoginResponseDto>> Login(LoginRequestDto request)
{
    var account = /* tìm theo email hoặc mã nhân viên */;
    if (account is null || !PasswordHasher.VerifyPassword(request.Password, account.PasswordHash, account.PasswordSalt))
        return Unauthorized(new { message = "Thông tin đăng nhập không đúng." });   // 401

    var token = _jwtTokenService.GenerateToken(account);                          // cấp token
    return new LoginResponseDto(token, account.Role, account.FullName);
}
```

- Mật khẩu **không lưu dạng gốc**: lưu băm PBKDF2-SHA256, 100.000 vòng, salt riêng
  (`BMS.API/Services/PasswordHasher.cs`). Đăng nhập thì băm lại mật khẩu nhập vào rồi so sánh.
- Đăng nhập sai không nói rõ sai email hay sai mật khẩu, để tránh dò tài khoản.

**`BMS.API/Services/JwtTokenService.cs` – `GenerateToken`:**

```csharp
var claims = new List<Claim>
{
    new(JwtRegisteredClaimNames.Sub, account.Email),          // ai (email / mã NV)
    new(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString()), // mã duy nhất của token
    new("userId", account.Id.ToString()),                      // mã tài khoản - API dùng để lấy đúng dữ liệu
    new("fullName", account.FullName),
    new(ClaimTypes.Role, account.Role)                         // Player | Staff | Admin -> phân quyền
};
var credentials = new SigningCredentials(new SymmetricSecurityKey(Encoding.UTF8.GetBytes(key)),
                                         SecurityAlgorithms.HmacSha256);            // ký HS256
var token = new JwtSecurityToken(issuer, audience: issuer, claims,
                                 expires: DateTime.UtcNow.AddHours(12),            // hạn 12 giờ
                                 signingCredentials: credentials);
return new JwtSecurityTokenHandler().WriteToken(token);
```

Ví dụ payload sau khi giải mã (xem bằng jwt.io):

```json
{
  "sub": "khach@example.com",
  "jti": "6f1c…",
  "userId": "107",
  "fullName": "Nguyễn Văn A",
  "role": "Player",
  "exp": 1760400000,
  "iss": "BMS.API",
  "aud": "BMS.API"
}
```

---

## 4. Bước 2 – Client cất token và gửi kèm mỗi request

### 4.1 Trang Người chơi (Blazor WebAssembly – chạy trong trình duyệt)
1. `Pages/Login.razor` gọi API đăng nhập, nhận token rồi:
   - `AuthState.SignIn(token, role, fullName)`: giữ trong bộ nhớ (`Services/AuthState.cs`).
   - `AuthStorage.SaveAsync(...)`: lưu vào **localStorage** nếu tích "Ghi nhớ đăng nhập", ngược lại
     **sessionStorage** (`Services/AuthStorageService.cs`), để F5 hoặc mở lại trang không phải đăng nhập lại.
2. **Mọi request** đi qua `Services/JwtAuthorizationHandler.cs`:

```csharp
protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct)
{
    if (request.Headers.Authorization is null && !string.IsNullOrEmpty(_authState.Token))
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", _authState.Token);
    return base.SendAsync(request, ct);
}
```

   Đăng ký trong `BMS.PlayerWeb/Program.cs` (dòng 15–20): `new HttpClient(new JwtAuthorizationHandler(...))`.
3. Khi tải hồ sơ mà API trả **401** (token hết hạn hoặc không hợp lệ), `AccountApiService` tự gọi
   `AuthState.SignOut()` và giao diện yêu cầu đăng nhập lại.
4. Đăng xuất: xóa token khỏi bộ nhớ và localStorage/sessionStorage.

### 4.2 Cổng Nhân viên / Admin (Blazor Server – chạy trên máy chủ)
- `Components/Pages/Login.razor` → `AuthState.SignIn(...)`. Token nằm trong **phiên làm việc phía máy chủ**
  (mỗi tab trình duyệt một phiên), **không** lưu ở trình duyệt.
- Mỗi service gọi API gắn header trước khi gửi, ví dụ `Services/TableApiService.cs`:

```csharp
private void ApplyAuthHeader()
{
    _http.DefaultRequestHeaders.Authorization = _authState.Token is null
        ? null
        : new AuthenticationHeaderValue("Bearer", _authState.Token);
}
```

- Giao diện cũng ẩn hoặc hiện menu theo `AuthState.IsAdmin` / `IsStaff`. Đây chỉ là **tiện cho người dùng**:
  lớp bảo vệ thật nằm ở API (mục 5–7). Ai gọi thẳng API mà không đủ quyền vẫn bị chặn.

---

## 5. Bước 3 – API kiểm tra token

**`BMS.API/Program.cs` dòng 60–117:**

```csharp
var jwtKey = builder.Configuration["Jwt:Key"] ?? throw ...;          // lấy khóa từ cấu hình
if (Encoding.UTF8.GetByteCount(jwtKey) < 32) throw ...;               // khóa yếu -> không cho chạy

builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
.AddJwtBearer(options =>
{
    options.MapInboundClaims = false;           // giữ nguyên tên claim "sub", "userId", "role"
    options.TokenValidationParameters = new TokenValidationParameters
    {
        ValidateIssuer = true,  ValidIssuer = jwtIssuer,       // ① đúng nơi cấp
        ValidateAudience = true, ValidAudience = jwtIssuer,    // ② đúng nơi nhận
        ValidateIssuerSigningKey = true,                       // ③ chữ ký đúng khóa bí mật
        IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtKey)),
        ValidateLifetime = true,                               // ④ chưa hết hạn
        ClockSkew = TimeSpan.FromMinutes(1)                    //    cho lệch đồng hồ tối đa 1 phút
    };
    // SignalR qua WebSocket không gửi được header -> cho phép token qua ?access_token= với /hubs
    options.Events = new JwtBearerEvents { OnMessageReceived = ... };
});
```

**Pipeline** (`Program.cs` dòng 259–260): `app.UseAuthentication();` rồi đến `app.UseAuthorization();`.
Mỗi request đi qua 2 bước này **trước khi** vào controller:
- `UseAuthentication` đọc header `Authorization: Bearer ...`. Token hợp lệ thì tạo `User` (ClaimsPrincipal)
  chứa các claim; token sai hoặc hết hạn thì coi như **chưa đăng nhập**.
- `UseAuthorization` so `User` với yêu cầu quyền của endpoint. Chưa đăng nhập trả **401**,
  thiếu quyền trả **403**.

Token bị từ chối trong các trường hợp:

| Trường hợp | Kết quả |
|---|---|
| Không gửi token | 401 |
| Sửa payload (ví dụ đổi `role` thành `Admin`) | 401: chữ ký không khớp |
| Tự ký bằng khóa khác | 401 |
| Header `alg: none` (không chữ ký) | 401 |
| Token đã hết hạn (`exp` < hiện tại) | 401 |
| Sai issuer / audience | 401 |
| Token đúng nhưng vai trò không đủ | 403 |

---

## 6. Bước 4 – Phân quyền: ai được gọi API nào

### 6.1 Mặc định MỌI endpoint đều bắt buộc JWT (`Program.cs` dòng 119–131)

```csharp
builder.Services.AddAuthorization(options =>
{
    options.FallbackPolicy = new AuthorizationPolicyBuilder()
        .AddAuthenticationSchemes(JwtBearerDefaults.AuthenticationScheme)
        .RequireAuthenticatedUser()      // không có token hợp lệ -> 401
        .Build();
});
```

Nhờ FallbackPolicy, **lập trình viên quên gắn `[Authorize]` thì API vẫn được bảo vệ**: "đóng mặc định,
mở khi cần" (secure by default).

### 6.2 Ba thuộc tính dùng trong controller

| Thuộc tính | Ý nghĩa | Ví dụ trong đồ án |
|---|---|---|
| `[AllowAnonymous]` | Công khai, không cần token | `AccountsController.Login/Register`, `TablesController.GetAll`, `CuesController.GetAll`, `SettingsController.Get`, `TournamentsController` danh sách giải |
| `[Authorize]` | Chỉ cần đăng nhập (vai trò nào cũng được) | `WalletController` (cả lớp – ví của chính mình), `MatchRequestsController`, `ChatController` |
| `[Authorize(Roles = "Staff,Admin")]` | Nhân viên hoặc Admin | `SessionsController.CheckIn/CheckOut/Pause/Transfer`, duyệt nạp tiền, quét QR hội viên |
| `[Authorize(Roles = "Admin")]` | Chỉ Admin | Thêm/sửa/xóa bàn, cơ, giải đấu; tạo nhân viên; `PUT api/settings`; báo cáo giao dịch |

Ví dụ `BMS.API/Controllers/SessionsController.cs`:

```csharp
[Authorize(Roles = "Staff,Admin")]          // người chơi gọi -> 403
[HttpPost("checkin")]
public async Task<ActionResult<TableDto>> CheckIn(CheckInRequestDto request) { ... }
```

### 6.3 Kênh realtime SignalR
`app.MapHub<BmsHub>("/hubs/bms").AllowAnonymous()` (`Program.cs` dòng 263): hub chỉ **phát** sự kiện công khai
(bàn đổi trạng thái…) để khách chưa đăng nhập cũng thấy sơ đồ bàn trực tiếp. Hub không có hàm nào
cho client gọi để thay đổi dữ liệu; mọi thay đổi đều đi qua API có JWT.

---

## 7. Bước 5 – Kiểm tra quyền sở hữu dữ liệu (dùng `userId` trong token)

Có token hợp lệ **chưa đủ**: người chơi A không được xem ví của B. API **không tin** mã tài khoản do client
gửi lên, mà lấy từ token đã được ký:

```csharp
// BMS.API/Controllers/WalletController.cs dòng 49–60
private int? GetCurrentAccountId()
{
    var claim = User.FindFirst("userId")?.Value;           // lấy từ token, không lấy từ request
    return int.TryParse(claim, out var id) ? id : null;
}

[HttpGet("me")]
public async Task<ActionResult<WalletInfoDto>> GetMyWallet()
{
    var accountId = GetCurrentAccountId();
    if (accountId is null) return Unauthorized();
    var profile = await _db.PlayerProfiles.FirstOrDefaultAsync(p => p.AccountId == accountId);   // chỉ ví của mình
    ...
}
```

Ví dụ khác: **ghép trận** (`MatchRequestsController.cs` dòng 29–33, 65, 125–133)
- Gửi lời mời: `requesterId` phải là hồ sơ của chính người đang đăng nhập, nếu không thì **403**.
- Chấp nhận lời mời: chỉ **người được mời** mới chấp nhận được; người gửi chỉ được rút lại;
  Nhân viên/Admin xử lý được mọi lời mời.

Các chỗ dùng cùng kỹ thuật: ví, điểm, mã QR hội viên, lịch sử giao dịch, hồ sơ cá nhân, chat, tin trao đổi cơ,
đăng ký giải đấu, chấm công / ca làm của nhân viên.

---

## 8. Khóa bí mật và cấu hình

| Môi trường | Khóa nằm ở đâu |
|---|---|
| Chạy local (Visual Studio) | `BMS.API/appsettings.Development.json` → `Jwt:Key` (khóa thử, chỉ dùng trên máy lập trình) |
| VPS (Docker) | File `.env` trên VPS: `JWT_KEY=...`, truyền vào container qua `docker-compose.yml`: `Jwt__Key: ${JWT_KEY}` |
| Bản Windows | Tự sinh ngẫu nhiên lần đầu, lưu `data\jwt.key` riêng từng máy |

- `.env` **không** đưa lên GitHub (có trong `.gitignore`), nên người xem mã nguồn không biết khóa.
- `Jwt:Issuer` = `BMS.API` (`appsettings.json`). Token phải có `iss` và `aud` đúng giá trị này.

---

## 9. Bảng phân quyền đầy đủ

| Nhóm | Không cần token | Người chơi (Player) | Nhân viên (Staff) | Quản trị (Admin) |
|---|---|---|---|---|
| Tài khoản | Đăng ký, đăng nhập, quên/đặt lại mật khẩu | Hồ sơ của mình | Hồ sơ của mình | + tạo nhân viên, danh sách tài khoản |
| Bàn, loại bàn, giá | Xem danh sách | Đặt bàn, hủy đặt bàn của mình | Mở/trả bàn, tạm dừng, chuyển bàn, gọi món, thanh toán, duyệt đặt bàn | + thêm/sửa/xóa bàn, loại bàn, khung giá |
| Cơ | Xem danh sách, tin trao đổi | Thuê, yêu cầu mua, đăng tin | Ghi nhận trả cơ, xử lý yêu cầu mua | + thêm/sửa/xóa cơ |
| Ví & điểm | – | Ví, điểm, mã QR **của mình**, nạp tiền | Duyệt nạp tiền, tra hội viên, quét QR | + báo cáo giao dịch, danh sách hội viên |
| Kho | – | – | Xem kho, nhập hàng, kiểm kho | + sửa danh mục kho |
| Giải đấu | Xem danh sách, bảng đấu | Đăng ký giải | – | Tạo giải, bốc thăm, nhập kết quả |
| Ghép trận | – | Gửi/nhận lời mời **của mình** | Xử lý mọi lời mời | Xử lý mọi lời mời |
| Ca làm, chấm công | – | – | Ca của mình | + báo cáo toàn bộ |
| Cấu hình chung | Xem (chân trang) | – | – | Sửa |

---

## 10. Hướng dẫn demo

### 10.1 Bằng curl (trên VPS, Git Bash hoặc PowerShell có `curl.exe`)

```bash
# (1) Không token -> 401
curl -i https://api.bmsbilliard.com/api/wallet/me

# (2) API công khai -> 200
curl -i https://api.bmsbilliard.com/api/tables

# (3) Đăng nhập lấy token (dùng tài khoản thử của nhóm)
curl -s -X POST https://api.bmsbilliard.com/api/accounts/login \
     -H "Content-Type: application/json" \
     -d '{"email":"<email thử>","password":"<mật khẩu thử>"}'
#   -> {"token":"eyJ...","role":"Player","fullName":"..."}

# (4) Gọi lại kèm token -> 200
curl -i https://api.bmsbilliard.com/api/wallet/me -H "Authorization: Bearer <token>"

# (5) Người chơi gọi API của Admin -> 403
curl -i https://api.bmsbilliard.com/api/accounts -H "Authorization: Bearer <token người chơi>"

# (6) Sửa 1 ký tự trong token rồi gọi lại -> 401
```

### 10.2 Bằng Postman
1. Tạo request **POST** `https://api.bmsbilliard.com/api/accounts/login`, tab *Body* chọn *raw → JSON*:
   `{"email":"...","password":"..."}` rồi bấm Send. Copy giá trị `token`.
2. Tạo request **GET** `https://api.bmsbilliard.com/api/wallet/me`, bấm Send **chưa có token**: kết quả **401 Unauthorized**.
3. Tab *Authorization* chọn *Type = Bearer Token*, dán token rồi Send: kết quả **200** kèm số dư ví.
4. Đổi URL thành `https://api.bmsbilliard.com/api/accounts` (API chỉ Admin), dùng token người chơi: kết quả **403 Forbidden**.
5. Sửa 1 ký tự cuối token rồi Send: kết quả **401**.

### 10.3 Bằng Swagger (chạy API local trong Visual Studio)
1. Chạy `BMS.API` và mở `https://localhost:5001/swagger`.
2. Gọi `POST /api/accounts/login` rồi copy token.
3. Bấm nút **Authorize** (ổ khóa), dán `Bearer <token>`. Swagger đã cấu hình sẵn kiểu Bearer JWT
   (`Program.cs` mục "Swagger (with Bearer auth support)").
4. Gọi các API khác để thấy 200 / 401 / 403.

Lưu ý: Swagger chỉ bật khi chạy Development, **không** mở trên VPS để không lộ danh sách API.

### 10.4 Xem nội dung token
Dán token vào <https://jwt.io>: thấy rõ `userId`, `role`, `exp`. Sửa payload trên jwt.io rồi dùng token đó
gọi API sẽ bị **401**, vì không có khóa bí mật thì không ký lại được.

---

## 11. Hướng dẫn thêm một API mới có bảo vệ

1. Viết action trong controller như bình thường. **Không gắn gì thì API mặc định đã bắt buộc đăng nhập**
   (FallbackPolicy).
2. Chọn đúng mức quyền:

```csharp
[AllowAnonymous]                         // công khai (chỉ khi thật sự cần)
[Authorize(Roles = "Staff,Admin")]       // nhân viên trở lên
[Authorize(Roles = "Admin")]             // chỉ admin
```

3. Nếu API trả dữ liệu "của tôi", **lấy mã người gọi từ token**, không nhận từ body/query:

```csharp
var accountId = int.Parse(User.FindFirst("userId")!.Value);
```

4. Phía trang Người chơi không cần làm gì thêm, vì `JwtAuthorizationHandler` tự gắn token.
   Phía cổng NV/Admin gọi `ApplyAuthHeader()` trước khi gửi request (theo mẫu các `*ApiService.cs`).

---

## 12. Đổi khóa JWT khi bị lộ

```bash
cd /opt/bms
openssl rand -base64 48          # tạo khóa mới
nano .env                        # thay JWT_KEY=<khóa mới>
docker compose up -d             # API khởi động lại với khóa mới
```

Mọi token cũ lập tức **hết hiệu lực** và người dùng phải đăng nhập lại. Mã QR hội viên và mã hủy đặt bàn
(cũng ký bằng khóa suy ra từ `JWT_KEY`) cũng hết hiệu lực theo.

---

## 13. Kiểm thử bảo mật đã chạy (41/41 đạt)

| Nhóm | Các trường hợp |
|---|---|
| Cấu trúc token | Có đủ `sub`, `userId`, `role`, `iss`, `aud`, `exp`, `jti`; hạn đúng 12 giờ |
| Công khai | `tables`, `cues`, `table-types`, `tournaments`, `settings`, `cue-listings`, SignalR → 200 không cần token |
| Không token → 401 | ví, hồ sơ, ghép trận (xem/tạo/trả lời), người chơi, đặt bàn, lịch sử phiên, mở bàn, kho, tra hội viên, sửa cấu hình, thêm bàn, danh sách tài khoản |
| Token giả → 401 | sửa payload thành Admin, ký bằng khóa giả, `alg: none`, hết hạn, sai issuer/audience; phản hồi có `WWW-Authenticate: Bearer` |
| Sai vai trò → 403 | người chơi gọi API nhân viên; người chơi tra hội viên; nhân viên gọi API Admin |
| Sở hữu | A gửi lời mời dưới tên B → 403; người ngoài chấp nhận lời mời → 403; A tự chấp nhận → 403; người được mời chấp nhận → 200 |

Ngoài ra đã kiểm tra trên tên miền thật: gọi `api/players`, `api/match-requests`, `api/bookings` không có token
đều trả **401**; danh sách bàn trả **200**.

---

## 14. Kịch bản trình bày 5 phút

| Thời gian | Nói gì | Làm gì trên màn hình |
|---|---|---|
| 0:00–0:45 | "Backend của nhóm bảo vệ bằng JWT. Khi đăng nhập đúng, API cấp một token có chữ ký HMAC-SHA256, chứa mã tài khoản, vai trò và hạn 12 giờ. Mỗi request gửi kèm token trong header Authorization." | Mở mục 2 (sơ đồ) của tài liệu này |
| 0:45–1:45 | "Token được tạo ở JwtTokenService: đây là các claim, đây là thuật toán ký, đây là hạn dùng. Khóa bí mật nằm trong .env trên máy chủ, không có trên GitHub." | Mở `JwtTokenService.cs` và `docker-compose.yml` dòng 16 |
| 1:45–2:45 | "API kiểm tra 4 điều: chữ ký, issuer, audience, hạn dùng. Nhóm bật FallbackPolicy để mọi API mặc định bắt buộc token; chỉ chỗ công khai mới ghi AllowAnonymous." | Mở `Program.cs` dòng 60–131 |
| 2:45–3:30 | "Phân quyền theo vai trò bằng Authorize(Roles). Ngoài ra API lấy userId từ token để mỗi người chỉ xem dữ liệu của mình." | Mở `SessionsController.cs` (Roles) và `WalletController.cs` dòng 49–60 |
| 3:30–4:30 | Demo: không token 401 → đăng nhập lấy token → có token 200 → người chơi gọi API Admin 403 → sửa token 401 | Postman (mục 10.2) hoặc curl (mục 10.1); dán token lên jwt.io |
| 4:30–5:00 | "Nhóm có bộ kiểm thử tự động 41 trường hợp bảo mật, đều đạt; kiểm tra lại trên tên miền thật." | Mở mục 13 |

**Chuẩn bị trước buổi trình bày:**
- 1 tài khoản người chơi thử và 1 tài khoản Admin (tự đặt mật khẩu, không ghi vào tài liệu).
- Postman đã tạo sẵn 4 request ở mục 10.2.
- Mở sẵn các file: `JwtTokenService.cs`, `Program.cs`, `SessionsController.cs`, `WalletController.cs`, `docker-compose.yml`.

---

## 15. Câu hỏi thường gặp

**JWT khác session (phiên) truyền thống thế nào?**
Session lưu trạng thái đăng nhập trên máy chủ, client chỉ giữ mã phiên. JWT tự chứa thông tin và chữ ký,
máy chủ không cần lưu gì, chỉ cần kiểm tra chữ ký. Vì vậy hợp với API phục vụ nhiều loại client.

**Payload ai cũng đọc được, vậy có an toàn không?**
Payload chỉ chứa thông tin không bí mật (mã tài khoản, tên, vai trò, hạn), **không** chứa mật khẩu.
Đọc được nhưng không sửa được, vì sửa thì chữ ký sai.

**Nếu token bị lộ thì sao?**
Người có token dùng được đến khi hết hạn (tối đa 12 giờ). Cách giảm rủi ro: luôn dùng HTTPS (đã bật),
thời hạn ngắn, và khi cần thì đổi `JWT_KEY` để vô hiệu toàn bộ token (mục 12).
Hạn chế hiện tại: chưa có cơ chế thu hồi từng token riêng lẻ và chưa có refresh token.

**Vì sao trang khách lưu token ở localStorage mà không dùng cookie?**
Trang Người chơi là Blazor WebAssembly gọi API ở tên miền khác (`api.bmsbilliard.com`), nên gửi token qua
header Bearer là cách đơn giản và không bị tấn công giả mạo request (CSRF) như cookie. Đổi lại phải chống
XSS: Blazor tự mã hóa nội dung hiển thị và trang không chèn HTML từ người dùng. Người dùng không tích
"Ghi nhớ đăng nhập" thì token chỉ nằm trong sessionStorage và mất khi đóng tab.
Cổng NV/Admin giữ token phía máy chủ nên trình duyệt không chạm tới token.

**HS256 khác RS256 thế nào, sao chọn HS256?**
HS256 dùng 1 khóa bí mật chung để ký và kiểm tra; RS256 dùng cặp khóa riêng/công khai. BMS chỉ có một API
vừa cấp vừa kiểm tra token nên HS256 là đủ và đơn giản. Nếu sau này có nhiều dịch vụ cần kiểm tra token
thì nên chuyển RS256.

**401 khác 403 thế nào?**
401 = chưa xác thực (không có token, token sai hoặc hết hạn). 403 = đã xác thực nhưng không đủ quyền
(ví dụ người chơi gọi API của Admin).

**Giao diện đã ẩn nút rồi, sao API còn phải kiểm tra?**
Ẩn nút chỉ tiện cho người dùng; ai cũng có thể gọi thẳng API bằng Postman/curl. Bảo mật thật phải ở API.

**Mật khẩu lưu thế nào?**
Băm PBKDF2-SHA256, 100.000 vòng, mỗi tài khoản một salt riêng (`PasswordHasher.cs`). Không ai, kể cả Admin,
xem được mật khẩu gốc.
