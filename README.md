# Billiard Management System (BMS)

> **Bản đang chạy:** Người chơi https://bmsbilliard.com · Nhân viên/Quản trị https://admin.bmsbilliard.com · API https://api.bmsbilliard.com
>
> **Tài liệu:** [Hướng dẫn chạy đồ án](HUONG-DAN-CHAY-DO-AN.md) · [Triển khai VPS](DEPLOY.md) · [Đặc tả yêu cầu (SRS)](docs/SRS.md) · [Bảo mật API bằng JWT](docs/BAO-MAT-JWT.md) · [Hỏi đáp bảo vệ](docs/HOI-DAP-BAO-VE.md)
>
> **Jira:** https://billiardsclub.atlassian.net/jira/software/projects/BMS/boards/2/backlog

Hệ thống quản lý câu lạc bộ bida, gồm 5 project trong `BMS.sln`:

| Project | Loại | Port cố định |
|---|---|---|
| `BMS.Shared` | Class library (DTO dùng chung) | - |
| `BMS.API` | ASP.NET Core Web API (backend duy nhất) | https://localhost:5001 |
| `BMS.AdminStaffWeb` | Blazor Web App (Interactive Server) | https://localhost:5101 |
| `BMS.PlayerWeb` | Blazor WebAssembly | https://localhost:5201 |
| `BMS.PlayerApp` | .NET MAUI (chỉ Android) | - |

Tất cả client (Admin Web, Player Web, MAUI App) đều gọi qua HTTP tới `BMS.API` -
không client nào truy cập database trực tiếp. SignalR (`BmsHub`, endpoint
`/hubs/bms`) phát các sự kiện `TableStatusChanged`, `MatchRequestChanged`,
`CueRentalChanged` NGAY SAU KHI đã lưu database thành công.

## Cách chạy (Visual Studio 2026)

1. Mở `BMS.sln`.
2. Chạy **BMS.API** trước tiên (F5 hoặc Ctrl+F5). Trang `/swagger` sẽ tự mở
   (`launchUrl` đã cấu hình sẵn trong `Properties/launchSettings.json`).
   Lần chạy đầu, database Sqlite `bms.db` sẽ tự tạo (`EnsureCreated()`) và
   seed sẵn dữ liệu mẫu (4 bàn, 6 người chơi, 3 loại cơ, 1 tài khoản Admin:
   `admin@bms.local` / `Admin@123`).
3. Chạy tiếp **BMS.AdminStaffWeb** (giao diện quản trị/nhân viên) và/hoặc
   **BMS.PlayerWeb** (giao diện khách hàng trên web). Có thể set nhiều
   Startup Project trong Visual Studio để chạy đồng thời.
4. Để chạy **BMS.PlayerApp** trên Android Emulator: chọn BMS.PlayerApp làm
   Startup Project, chọn thiết bị ảo Android, F5. App gọi API qua
   `https://10.0.2.2:5001` (địa chỉ đặc biệt của Android Emulator trỏ về
   máy host, vì emulator không nhìn thấy `localhost` của máy host).

## Ánh xạ tính năng ↔ Jira User Story

| Epic | User Story | Tính năng | Vị trí code |
|---|---|---|---|
| EP-01 | US-001 | Đăng ký | `AccountsController.Register`, `Register` DTOs |
| EP-01 | US-002 | Đăng nhập | `AccountsController.Login`, JWT (`JwtTokenService`) |
| EP-01 | US-003 | Đăng xuất | `AuthState.SignOut` (xóa token phía client) |
| EP-01 | US-004 | Quản lý thông tin cá nhân | `AccountsController.GetProfile/UpdateProfile` |
| EP-01 | US-005 | Phân quyền | Role claim JWT + `[Authorize(Roles = "Admin")]` |
| EP-02 | US-006 | Xem danh sách bàn | `TablesController.GetAll`, `Tables.razor` |
| EP-02 | US-007 | Xem trạng thái bàn | `TablesController.GetById`, `TableDto.Status` |
| EP-02 | US-008 | Đặt bàn | `BookingsController.Create` |
| EP-02 | US-009 | Hủy đặt bàn | `BookingsController.Cancel` |
| EP-02 | US-010 | Nhận bàn | `SessionsController.CheckIn` |
| EP-02 | US-011 | Theo dõi thời gian chơi | `TableDto.ActiveSessionStartedAtUtc` + đồng hồ real-time trên UI |
| EP-02 | US-012 | Chuyển bàn | `SessionsController.Transfer` |
| EP-03 | US-013 | Tạo hóa đơn | `SessionsController.CheckOut` (tạo + chốt cùng lúc) |
| EP-03 | US-014 | Tính tiền theo giờ | `SessionsController.CheckOut` (tính theo `PricePerHour` * thời gian thực) |
| EP-03 | US-015 | Thêm món vào hóa đơn | *(chưa có entity "món" trong phạm vi lần này, xem ghi chú bên dưới)* |
| EP-03 | US-016 | Thanh toán | `SessionsController.CheckOut` (thanh toán ngay khi trả bàn) |
| EP-03 | US-017 | Lịch sử hóa đơn | `SessionsController.GetHistory` |
| EP-04 | US-018..022 | Quản lý nhân viên/ca làm/chấm công | *(chỉ đại diện bằng Account/Role, chưa code chi tiết theo yêu cầu)* |
| EP-08 | US-039 | Ghép trận theo ELO | `PlayersController.GetSuggestions` |
| EP-08 | US-040 | Gửi lời mời ghép trận | `MatchRequestsController.Create` |
| EP-08 | US-041 | Ghi nhận thuê cơ | `CuesController.Rent` |
| EP-08 | US-042 | Cơ hư hỏng | `CuesController.Return` (Outcome = "Damaged") |
| EP-08 | US-043 | Cơ mất + đền tiền | `CuesController.Return` (Outcome = "Lost" + `PenaltyAmount`) |

Ghi chú US-015: đặc tả kỹ thuật cho lần dựng lại này không định nghĩa entity
"món ăn/thức uống" nên "thêm món vào hóa đơn" chưa có API riêng - hóa đơn
hiện chỉ gồm tiền giờ chơi bàn. Sẽ bổ sung khi nhóm định nghĩa rõ danh mục
món trong Jira.

## Lưu ý bắt buộc khi tải lại/giải nén bản zip mới

**Nếu tải lại hoặc giải nén một bản zip mới của source code, PHẢI XÓA thư
mục dự án cũ trước, rồi giải nén vào một thư mục HOÀN TOÀN MỚI**, sau đó mở
lại `BMS.sln` và **Clean + Rebuild** trong Visual Studio. Đây là bài học từ
lỗi `AmbiguousMatchException` gặp phải khi giải nén đè lên thư mục cũ, do
một file `Home.razor` cũ còn sót lại tạo ra route trùng với route mới.

## Cảnh báo bảo mật hiện tại

CORS trong `BMS.API/Program.cs` chỉ cho phép các origin trong `Cors:AllowedOrigins`
(biến môi trường `Cors__AllowedOrigins`), cộng thêm localhost:5101/5201 khi chạy
Development. Khóa JWT và mật khẩu admin ban đầu đọc từ cấu hình/biến môi trường
(`Jwt__Key`, `Admin__InitialPassword`); bản dev nằm trong `appsettings.Development.json`.
Triển khai lên VPS bằng Docker: xem `DEPLOY.md`.

Ngoài ra, hiện tại dùng `EnsureCreated()` thay vì EF Core Migrations thật -
khi cần thay đổi schema, hãy chuyển sang `dotnet ef migrations add ...` +
`Database.Migrate()` (đã có TODO comment trong `Program.cs`).

## Phạm vi (Jira scope)

Phạm vi hiện tại bám sát đúng 43 User Story trong Jira của nhóm (8 Epic, 172
Sub-task, 3 Sprint). Các tính năng mở rộng (quản lý ca làm & đối soát tiền
mặt, ví thành viên, giải đấu...) đã được ghi nhận nhưng **CHƯA đưa vào
code**, để dành cho các Sprint sau khi nhóm quyết định mở rộng Jira. Các
Epic Inventory, Finance, Billiard Card cũng chưa được code trong lần này.

## Theme màu

Toàn bộ giao diện (Admin Web, Player Web, MAUI App) chỉ dùng tông **xanh lá
(felt green)** làm màu thương hiệu (`--felt`, `--felt-light`, `--felt-dark`)
kết hợp nền trung tính be/kem (`--cream`). Không dùng màu nâu gỗ. Màu vàng
(`--reserved`) chỉ dùng để phân biệt trạng thái "Đã đặt" của bàn, không phải
màu thương hiệu; tương tự đỏ (`--occupied`) cho "Đang chơi" và xám
(`--maintenance`) cho "Bảo trì".
