# BMS Billiard – Hướng dẫn chạy đồ án

Toàn bộ mã nguồn, cấu hình triển khai, dữ liệu mẫu và tài liệu của BMS nằm trong **một thư mục: `D:\CNPMNC`**.

| Thư mục / file | Nội dung |
|---|---|
| `BMS.API` | Máy chủ dữ liệu (ASP.NET Core, SQLite, SignalR realtime) |
| `BMS.AdminStaffWeb` | Cổng Nhân viên + Quản trị (Blazor Server) |
| `BMS.PlayerWeb` | Trang Người chơi (Blazor WebAssembly, cài được như app – PWA) |
| `BMS.Shared` | Kiểu dữ liệu, công thức tính tiền giờ, tích điểm dùng chung |
| `BMS.Launcher` | `BMS.exe` – trình khởi động bản Windows bấm đúp là chạy |
| `BMS.PlayerApp` | Ứng dụng MAUI (bản cũ, không dùng khi triển khai) |
| `deploy/` | Caddyfile, dữ liệu mẫu `seed/bms-demo.db`, script đóng gói Windows, hướng dẫn sử dụng |
| `docker-compose.yml`, `DEPLOY.md`, `.env.example` | Triển khai lên máy chủ (VPS) |
| `dist/` | Sản phẩm đóng gói (`BMS-Windows`, `BMS-Windows.zip`) – tạo bằng script, không sửa tay |

## Cách 1 – Dùng như app thật (đang chạy trên Internet)

Người dùng cuối chỉ cần trình duyệt, không cài gì thêm:

- **Khách:** <https://bmsbilliard.com>
- **Nhân viên / Quản trị:** <https://admin.bmsbilliard.com>

Cài thành ứng dụng có biểu tượng riêng trên màn hình:
- **Android (Chrome):** mở trang → menu ⋮ → *Cài đặt ứng dụng* / *Thêm vào màn hình chính*.
- **iPhone (Safari):** nút Chia sẻ → *Thêm vào MH chính*.
- **Windows / macOS (Chrome, Edge):** biểu tượng *Cài đặt* ở cuối thanh địa chỉ.

## Cách 2 – Bản Windows bấm đúp (chạy trên 1 máy, không cần Internet)

Dùng để nộp/trình diễn đồ án hoặc giao cho quán chạy trên máy quầy:

1. Lấy `dist\BMS-Windows.zip`, giải nén vào ví dụ `C:\BMS`.
2. Bấm đúp **`BMS.exe`**. Lần đầu: đặt mật khẩu Admin (tài khoản `admin@bms.local`).
3. Trình duyệt tự mở trang Người chơi (`http://localhost:5280`) và cổng Quản trị (`http://localhost:5180`).
4. Đóng cửa sổ đen để tắt. Dữ liệu nằm trong `data\` cạnh `BMS.exe` – sao lưu bằng cách chép thư mục này.

Chi tiết cho người dùng: `deploy\HUONG-DAN-SU-DUNG.txt` (được chép kèm vào bản Windows).

Không cần cài .NET, Visual Studio hay Docker – bản Windows đã chứa sẵn runtime.

## Cách 3 – Bán / cài cho quán khác trên máy chủ riêng

Mỗi quán thuê 1 VPS + tên miền, chạy theo `DEPLOY.md` (3 lệnh). Người dùng của quán vẫn chỉ
mở trang web / cài PWA như Cách 1. Thông tin quán và tài khoản ngân hàng sửa trong
*Cổng Quản trị → Cấu hình chung*, không phải sửa code.

## Dành cho người phát triển

- Tạo lại bản Windows (cần .NET 10 SDK):
  `powershell -ExecutionPolicy Bypass -File D:\CNPMNC\deploy\build-windows.ps1`
- Mở `BMS.sln` bằng Visual Studio 2022+ (.NET 10) và chạy nhiều dự án (API, AdminStaffWeb, PlayerWeb) để sửa code.
- Cập nhật bản trên máy chủ: xem mục "Cập nhật phiên bản mới" trong `DEPLOY.md`.

## Ba vai trò

| Vai trò | Đăng nhập ở | Chức năng chính |
|---|---|---|
| Người chơi | Trang Người chơi | Xem bàn trực tiếp, đặt/hủy đặt bàn, thuê – mua – trao đổi cơ, giải đấu, ghép trận ELO, ví (nạp tiền, tích/đổi điểm, mã QR hội viên), chat với quán |
| Nhân viên | Cổng NV (Mã NV + PIN) | Mở bàn, tạm dừng/chơi tiếp, chuyển bàn, gọi món, trả bàn – thanh toán (Ví/QR/tiền mặt, quét QR hội viên), duyệt đặt bàn & nạp tiền, cho thuê cơ, xử lý yêu cầu mua cơ, giao ca, chấm công |
| Quản trị | Cổng Quản trị | Tổng quan doanh thu, quản lý bàn – loại bàn – khung giá, cơ, kho, giải đấu, nhân viên, hội viên, báo cáo, cấu hình chung |
