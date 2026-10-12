# Bảo vệ backend (BMS.API) bằng JWT

## 1. JWT là gì, dùng để làm gì trong BMS

**JWT (JSON Web Token)** là một chuỗi gồm 3 phần `header.payload.chữ-ký`, do máy chủ cấp khi
người dùng đăng nhập thành công. Từ đó mỗi lần gọi API, client gửi kèm header:

```
Authorization: Bearer <token>
```

API **không lưu phiên đăng nhập** (stateless): nó chỉ cần kiểm tra chữ ký của token để biết token
do chính máy chủ cấp và chưa bị sửa, rồi đọc thông tin người dùng (mã tài khoản, vai trò) nằm
trong token để quyết định cho phép hay từ chối.

```
 Người dùng            Trang web (Player / Admin)              BMS.API
     | email + mật khẩu         |                                  |
     |------------------------->| POST /api/accounts/login         |
     |                          |--------------------------------->| kiểm tra mật khẩu (PBKDF2)
     |                          |        { token: "eyJ..." }       | ký token HMAC-SHA256
     |                          |<---------------------------------|
     |                          | GET /api/wallet/me               |
     |                          | Authorization: Bearer eyJ...     |
     |                          |--------------------------------->| 1. chữ ký đúng khóa bí mật?
     |                          |                                  | 2. issuer/audience đúng?
     |                          |                                  | 3. còn hạn (exp)?
     |                          |                                  | 4. vai trò (role) đủ quyền?
     |                          |      200 / 401 / 403             |
     |                          |<---------------------------------|
```

## 2. Nội dung token BMS cấp

Tạo ở `BMS.API/Services/JwtTokenService.cs`, thuật toán **HS256** (HMAC-SHA256), hạn **12 giờ**.

| Claim | Ý nghĩa |
|---|---|
| `sub` | Email / mã nhân viên của tài khoản |
| `userId` | Mã tài khoản - API dùng để biết "ai đang gọi" (ví dụ: chỉ xem ví của chính mình) |
| `fullName` | Họ tên hiển thị |
| `role` | `Player` / `Staff` / `Admin` - dùng cho phân quyền |
| `jti` | Mã duy nhất của token |
| `iss`, `aud` | Nơi cấp / nơi nhận (`BMS.API`) |
| `exp` | Thời điểm hết hạn |

Khóa ký (`Jwt:Key`, tối thiểu 32 ký tự) **không nằm trong code**: trên máy chủ đặt qua biến môi
trường `JWT_KEY` trong file `.env` (không đưa lên GitHub). Không có khóa thì không ai giả được token.

## 3. API kiểm tra token như thế nào (`BMS.API/Program.cs`)

```csharp
options.TokenValidationParameters = new TokenValidationParameters
{
    ValidateIssuer = true,  ValidIssuer = jwtIssuer,     // đúng nơi cấp
    ValidateAudience = true, ValidAudience = jwtIssuer,  // đúng nơi nhận
    ValidateIssuerSigningKey = true,                     // chữ ký đúng khóa bí mật
    IssuerSigningKey = new SymmetricSecurityKey(...),
    ValidateLifetime = true,                             // chưa hết hạn
    ClockSkew = TimeSpan.FromMinutes(1)
};
```

**Mặc định mọi endpoint đều bắt buộc JWT** (FallbackPolicy):

```csharp
builder.Services.AddAuthorization(options =>
{
    options.FallbackPolicy = new AuthorizationPolicyBuilder()
        .AddAuthenticationSchemes(JwtBearerDefaults.AuthenticationScheme)
        .RequireAuthenticatedUser()
        .Build();
});
```

Endpoint nào muốn công khai phải ghi rõ `[AllowAnonymous]`; endpoint nào cần vai trò cụ thể thì
ghi `[Authorize(Roles = "Staff,Admin")]` hoặc `[Authorize(Roles = "Admin")]`. Thêm endpoint mới
mà quên gắn quyền thì vẫn tự được bảo vệ.

Ngoài kiểm tra vai trò, các API còn kiểm tra **quyền sở hữu** bằng `userId` trong token, ví dụ:
người chơi chỉ xem ví/điểm/mã QR của chính mình; chỉ gửi lời mời ghép trận dưới tên mình; chỉ người
được mời mới chấp nhận lời mời.

## 4. Bảng phân quyền

| Nhóm | Không cần token (công khai) | Người chơi (Player) | Nhân viên (Staff) | Quản trị (Admin) |
|---|---|---|---|---|
| Tài khoản | Đăng ký, đăng nhập, quên/đặt lại mật khẩu | Hồ sơ của mình | Hồ sơ của mình | + tạo nhân viên, danh sách tài khoản |
| Bàn, loại bàn, giá | Xem danh sách | Đặt bàn, hủy đặt bàn của mình | Mở/trả bàn, tạm dừng, chuyển bàn, gọi món, thanh toán, duyệt đặt bàn | + thêm/sửa/xóa bàn, loại bàn, khung giá |
| Cơ | Xem danh sách, tin trao đổi | Thuê, yêu cầu mua, đăng tin | Ghi nhận trả cơ, xử lý yêu cầu mua | + thêm/sửa/xóa cơ |
| Ví & điểm | - | Ví, điểm, mã QR **của mình**, nạp tiền | Duyệt nạp tiền, tra hội viên, quét QR | + báo cáo giao dịch, danh sách hội viên |
| Kho | - | - | Xem kho, nhập hàng, kiểm kho | + sửa danh mục kho |
| Giải đấu | Xem danh sách, bảng đấu | Đăng ký giải | - | Tạo giải, bốc thăm, nhập kết quả |
| Ghép trận | - | Gửi/nhận lời mời **của mình** | Xử lý mọi lời mời | Xử lý mọi lời mời |
| Ca làm, chấm công | - | - | Ca của mình | + báo cáo toàn bộ |
| Cấu hình chung | Xem (chân trang) | - | - | Sửa |

Kết nối realtime SignalR (`/hubs/bms`) cho phép khách chưa đăng nhập, vì chỉ phát các sự kiện
công khai (trạng thái bàn đổi). Kết quả kiểm tra quyền: **401** = chưa có token / token sai / hết
hạn; **403** = token hợp lệ nhưng không đủ quyền.

## 5. Phía giao diện gửi token thế nào

- **Trang Người chơi** (Blazor WebAssembly): sau khi đăng nhập, token lưu ở trình duyệt; mọi
  request tới API tự gắn `Authorization: Bearer` qua `BMS.PlayerWeb/Services/JwtAuthorizationHandler.cs`.
  Khi tải hồ sơ mà API trả 401 (token hết hạn / không hợp lệ), giao diện tự đăng xuất và yêu cầu
  đăng nhập lại.
- **Cổng Nhân viên/Admin** (Blazor Server): token giữ trong phiên làm việc phía máy chủ
  (`AuthState`), mỗi service gắn header trước khi gọi API.
- Mật khẩu lưu trong database dạng băm **PBKDF2-SHA256, 100.000 vòng, có salt** - không lưu mật khẩu gốc.

## 6. Cách kiểm chứng (demo cho giảng viên)

```bash
# 1. Không token -> 401
curl -i https://api.bmsbilliard.com/api/wallet/me

# 2. Đăng nhập lấy token (thay email/mật khẩu bằng tài khoản thử)
curl -s -X POST https://api.bmsbilliard.com/api/accounts/login \
     -H "Content-Type: application/json" -d '{"email":"...","password":"..."}'

# 3. Gọi lại kèm token -> 200
curl -i https://api.bmsbilliard.com/api/wallet/me -H "Authorization: Bearer <token>"

# 4. Người chơi gọi API của Admin -> 403
curl -i https://api.bmsbilliard.com/api/accounts -H "Authorization: Bearer <token người chơi>"
```

Có thể dán token vào <https://jwt.io> để xem các claim (chỉ xem được, không sửa được vì không có khóa).

Bộ kiểm thử tự động đã chạy 41 trường hợp: không token, token bị sửa quyền thành Admin, token ký
bằng khóa giả, token `alg: none`, token hết hạn, sai issuer/audience, sai vai trò, mạo danh người
khác khi gửi/chấp nhận lời mời ghép trận - **đạt 41/41**.
