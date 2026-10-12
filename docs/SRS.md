# ĐẶC TẢ YÊU CẦU PHẦN MỀM (SRS)
## Billiard Management System (BMS) – Hệ thống quản lý câu lạc bộ Billiard

| Mục | Nội dung |
|---|---|
| Phiên bản tài liệu | 1.0 |
| Ngày | 13/10/2026 |
| Dự án Jira | BMS – https://billiardsclub.atlassian.net/jira/software/projects/BMS/boards/2/backlog |
| Mã nguồn | https://github.com/khanhvinh01012006-debug/bms-billiard |
| Bản đang chạy | https://bmsbilliard.com (Người chơi) · https://admin.bmsbilliard.com (Nhân viên/Quản trị) · https://api.bmsbilliard.com (API) |
| Cấu trúc | Theo IEEE 830 (rút gọn) |

---

## 1. Giới thiệu

### 1.1 Mục đích
Tài liệu mô tả đầy đủ các yêu cầu chức năng và phi chức năng của hệ thống BMS, làm cơ sở cho việc
phát triển, kiểm thử, nghiệm thu và bảo trì. Đối tượng đọc: giảng viên/trợ giảng, Product Owner,
Scrum Master, nhóm phát triển, người kiểm thử và chủ quán sử dụng hệ thống.

### 1.2 Phạm vi sản phẩm
BMS giúp **chủ quán billiard** quản lý bàn chơi, đặt bàn, tính tiền và thanh toán, nhân viên, ca làm,
kho đồ ăn/uống, cơ cho thuê/bán, giải đấu và tài chính; đồng thời giúp **người chơi** xem bàn trực tiếp,
đặt bàn, dùng Ví/thẻ hội viên, tích điểm, thuê – mua – trao đổi cơ, đăng ký giải và ghép trận.

Hệ thống gồm 3 cổng dùng chung một máy chủ dữ liệu:

| Cổng | Người dùng | Địa chỉ |
|---|---|---|
| Trang Người chơi (web, cài được như ứng dụng – PWA) | Khách / hội viên | https://bmsbilliard.com |
| Cổng Nhân viên / Quản trị | Nhân viên, Quản trị viên | https://admin.bmsbilliard.com |
| API (máy chủ dữ liệu) | Hai cổng trên | https://api.bmsbilliard.com |

Ngoài phạm vi (phiên bản này): cổng thanh toán trực tuyến tự động đối soát, gửi SMS thật, ứng dụng
di động gốc (thay bằng PWA), đa chi nhánh.

### 1.3 Định nghĩa, từ viết tắt

| Thuật ngữ | Ý nghĩa |
|---|---|
| Phiên chơi (Session) | Khoảng thời gian một bàn được sử dụng, từ lúc mở bàn đến lúc trả bàn |
| Ví / Thẻ hội viên (Billiard Card) | Số dư trả trước của người chơi, dùng thanh toán tại quán |
| Hạng hội viên | Đồng / Bạc / Vàng / Kim Cương theo tổng tiền đã nạp |
| Khung giờ giá | Giá giờ khác nhau theo khung giờ trong ngày cho từng loại bàn |
| ELO | Điểm đánh giá trình độ người chơi dùng để ghép trận |
| JWT | JSON Web Token – mã xác thực do API cấp khi đăng nhập |
| PWA | Progressive Web App – web cài như ứng dụng, chạy khi mất mạng |
| SignalR | Kênh realtime đẩy thay đổi tới mọi màn hình |
| VietQR | Chuẩn mã QR chuyển khoản liên ngân hàng Việt Nam |
| FR / NFR | Yêu cầu chức năng / phi chức năng |

### 1.4 Tài liệu tham khảo
- Backlog Jira dự án BMS (Epic BMS-2 … BMS-9, BMS-225; Story BMS-10 … BMS-52, BMS-226 … BMS-250).
- `README.md`, `HUONG-DAN-CHAY-DO-AN.md`, `DEPLOY.md`, `docs/BAO-MAT-JWT.md` trong repo.
- IEEE Std 830-1998 – Recommended Practice for Software Requirements Specifications.

---

## 2. Mô tả tổng quan

### 2.1 Bối cảnh sản phẩm và kiến trúc

```
 Trình duyệt / PWA (Người chơi)          Trình duyệt (Nhân viên, Admin)
   BMS.PlayerWeb (Blazor WebAssembly)      BMS.AdminStaffWeb (Blazor Server)
              \                                   /
               \  HTTPS + JWT  (REST JSON, SignalR) /
                v                                 v
              Caddy (HTTPS tự động, Let's Encrypt, reverse proxy)
                                   |
                         BMS.API (ASP.NET Core .NET 10)
                    REST API · SignalR hub /hubs/bms · JWT
                                   |
                     SQLite (bms.db) + ảnh upload + settings.json
```

- Triển khai bằng Docker Compose (3 container: `api`, `admin`, `caddy`) trên VPS Ubuntu.
- Ngoài ra có bản Windows chạy 1 máy (`BMS.exe`) cho trình diễn/quầy thu ngân.
- Thư viện dùng chung `BMS.Shared`: DTO, công thức tính tiền giờ, tích điểm, hạng hội viên.

### 2.2 Chức năng chính (theo Epic Jira)

| Epic | Tên | Nội dung |
|---|---|---|
| BMS-2 | Account & Permissions | Đăng ký, đăng nhập, đăng xuất, hồ sơ, phân quyền, quên mật khẩu, bảo mật JWT |
| BMS-3 | Table & Booking | Danh sách/trạng thái bàn realtime, đặt/hủy bàn, giữ chỗ, mở bàn, tạm dừng, chuyển bàn |
| BMS-4 | Payment & Invoices | Hóa đơn, tiền giờ theo khung giờ, gọi món, thanh toán Ví/VietQR/tiền mặt, giảm giá, in hóa đơn |
| BMS-5 | Staff Management | Nhân viên, ca làm, bàn giao ca, chấm công |
| BMS-6 | Inventory & Food | Vật tư/món, nhập hàng, tồn kho, kiểm kho, cảnh báo sắp hết |
| BMS-7 | Finance | Doanh thu, báo cáo |
| BMS-8 | Billiard Card & Membership | Ví, nạp tiền, hạng hội viên, tích/đổi điểm, mã QR hội viên, gói giờ |
| BMS-9 | Player Services | Ghép trận ELO, thuê/mua/trao đổi cơ, giải đấu, chat với quán |
| BMS-225 | System & Deployment | Triển khai VPS, PWA, cấu hình chung, bản Windows, GitHub, responsive |

### 2.3 Đặc điểm người dùng

| Vai trò | Mô tả | Kỹ năng | Cổng dùng |
|---|---|---|---|
| Khách (chưa đăng nhập) | Xem bàn trống, cơ, giải đấu | Dùng điện thoại cơ bản | Trang Người chơi |
| Người chơi (Player) | Hội viên của quán | Dùng điện thoại cơ bản | Trang Người chơi (PWA) |
| Nhân viên (Staff) | Thu ngân / phục vụ tại quầy | Đăng nhập bằng Mã NV + PIN | Cổng Nhân viên (máy tính/máy tính bảng/điện thoại) |
| Quản trị viên (Admin) | Chủ quán / quản lý | Dùng máy tính | Cổng Quản trị |

### 2.4 Môi trường vận hành
- Máy chủ: VPS Linux (Ubuntu 22.04+), Docker + Docker Compose, cổng 80/443, tên miền trỏ về VPS.
- Máy khách: trình duyệt hiện đại (Chrome, Edge, Safari, Firefox) trên Windows/macOS/Android/iOS.
- Màn hình hỗ trợ: điện thoại (≥ 360px), máy tính bảng (768px), máy tính (1024–1920px).

### 2.5 Ràng buộc thiết kế và cài đặt
- Ngôn ngữ/nền tảng: C# .NET 10, ASP.NET Core, Blazor (Server + WebAssembly), Entity Framework Core, SQLite.
- Toàn bộ giao diện và thông báo bằng **tiếng Việt**; tiền tệ VNĐ; múi giờ **Việt Nam (UTC+7)**.
- Mọi client chỉ truy cập dữ liệu qua API (không truy cập database trực tiếp).
- Bí mật (khóa JWT, mật khẩu Admin) cấu hình qua biến môi trường `.env`, không nằm trong mã nguồn.

### 2.6 Giả định và phụ thuộc
- Quán có kết nối Internet ổn định cho bản trên máy chủ.
- Chuyển khoản ngân hàng do nhân viên xác nhận thủ công (không có API đối soát ngân hàng).
- Gửi email quên mật khẩu cần cấu hình máy chủ SMTP (chưa cấu hình trên VPS hiện tại).
- Dịch vụ tạo ảnh VietQR (img.vietqr.io) khả dụng.

---

## 3. Yêu cầu chức năng

Quy ước cột **Jira**: mã story trên Jira. **TT** = trạng thái hiện tại: ✅ Done, 🔶 In Progress, ⬜ To Do.

### 3.1 Tài khoản & phân quyền (BMS-2)

| Mã | Yêu cầu | Tác nhân | Tiêu chí chấp nhận | Jira | TT |
|---|---|---|---|---|---|
| FR-01 | Đăng ký tài khoản người chơi | Khách | Nhập họ tên, email, SĐT, mật khẩu; email không trùng; đăng ký xong tự đăng nhập; tạo sẵn Ví và hồ sơ ELO | BMS-10 | ✅ |
| FR-02 | Đăng nhập | Tất cả | Người chơi đăng nhập bằng email + mật khẩu; nhân viên bằng Mã NV + PIN; sai thông tin báo "Thông tin đăng nhập không đúng"; tài khoản người chơi không vào được cổng Nhân viên | BMS-11 | ✅ |
| FR-03 | Đăng xuất | Tất cả | Menu avatar góc trên phải → Đăng xuất; xóa phiên đã lưu trên máy | BMS-12 | ✅ |
| FR-04 | Quản lý thông tin cá nhân | Tất cả | Xem/sửa họ tên, địa chỉ; email chỉ xem; trạng thái xác thực SĐT | BMS-13 | ✅ |
| FR-05 | Phân quyền | Hệ thống | 3 vai trò Player/Staff/Admin; mỗi trang và API kiểm tra vai trò; người không đủ quyền nhận thông báo/HTTP 403 | BMS-14 | ✅ |
| FR-06 | Quên / đặt lại mật khẩu | Người dùng | Nhập email → nhận mã (hết hạn 15 phút); thông báo chung không tiết lộ email có tồn tại; đặt mật khẩu mới bằng mã | BMS-226 | ✅ |
| FR-07 | Bảo mật API bằng JWT | Hệ thống | Mọi API mặc định yêu cầu JWT hợp lệ; API công khai được liệt kê rõ; xem mục 5.2 và `docs/BAO-MAT-JWT.md` | BMS-227 | ✅ |

### 3.2 Bàn & đặt bàn (BMS-3)

| Mã | Yêu cầu | Tác nhân | Tiêu chí chấp nhận | Jira | TT |
|---|---|---|---|---|---|
| FR-10 | Xem danh sách bàn | Khách, mọi vai trò | Lọc theo loại bàn; hiển thị tên, loại, sức chứa, giá/giờ, ảnh | BMS-15 | ✅ |
| FR-11 | Xem trạng thái bàn realtime | Khách, mọi vai trò | Trống / Đang chơi / Đã đặt / Bảo trì / Tạm dừng; đổi trạng thái hiện trên mọi màn hình không cần tải lại (SignalR) | BMS-16, BMS-230 | ✅ |
| FR-12 | Đặt bàn | Người chơi | Chọn bàn trống, nhập SĐT, thời lượng (60–180 phút), ghi chú; bàn chuyển "Đã đặt"; nhân viên thấy yêu cầu ngay | BMS-17 | ✅ |
| FR-13 | Hủy đặt bàn | Người chơi, Nhân viên | Khách hủy trong "Đặt bàn của tôi" (bằng mã hủy riêng); nhân viên hủy yêu cầu chờ hoặc chỗ đã giữ; bàn trống lại | BMS-18 | ✅ |
| FR-14 | Giữ chỗ và tự hủy quá hạn | Nhân viên, Hệ thống | Nhân viên "Giữ chỗ"; yêu cầu chưa duyệt sau **15 phút** tự hủy (quét mỗi 45 giây); nhận khách đến trễ | BMS-229 | ✅ |
| FR-15 | Nhận bàn (mở bàn) | Nhân viên | Mở bàn nhanh với tên khách hoặc nhận khách đã đặt; bắt đầu tính giờ | BMS-19 | ✅ |
| FR-16 | Theo dõi thời gian chơi | Nhân viên, Khách | Đồng hồ chạy và tiền tạm tính trên thẻ bàn | BMS-20 | ✅ |
| FR-17 | Tạm dừng / chơi tiếp | Nhân viên | Tạm dừng thì đồng hồ đứng yên, thời gian dừng không tính tiền; vẫn gọi món được | BMS-228 | ✅ |
| FR-18 | Chuyển bàn | Nhân viên | Chọn bàn trống đích; giữ nguyên phiên, giờ bắt đầu, món đã gọi; cả phiên tính theo giá bàn mới | BMS-21 | ✅ |

### 3.3 Hóa đơn & thanh toán (BMS-4)

| Mã | Yêu cầu | Tác nhân | Tiêu chí chấp nhận | Jira | TT |
|---|---|---|---|---|---|
| FR-20 | Tạo hóa đơn khi trả bàn | Nhân viên | Hóa đơn gồm thời gian chơi, tiền giờ, tiền món, phụ thu (kèm lý do), giảm giá, tổng cộng | BMS-22 | ✅ |
| FR-21 | Tính tiền giờ | Hệ thống | Theo khung giờ giá của loại bàn tính theo **giờ Việt Nam**; không có khung thì theo giá/giờ của bàn; **làm tròn tới 1.000đ gần nhất** | BMS-23, BMS-233 | ✅ |
| FR-22 | Thêm món vào hóa đơn | Nhân viên | Chọn món từ kho (ảnh, giá, tồn), số lượng; trừ tồn kho; xóa món đã gọi; thêm dịch vụ nhập tay | BMS-24 | ✅ |
| FR-23 | Thanh toán | Nhân viên | 3 phương thức: Thẻ hội viên (Ví), Chuyển khoản (VietQR), Tiền mặt (tính tiền thối); Ví không đủ thì báo thiếu | BMS-25 | ✅ |
| FR-24 | Chuyển khoản VietQR | Nhân viên | Tạo mã VietQR có sẵn số tiền + nội dung theo tài khoản trong Cấu hình chung; phải tích "đã nhận tiền" mới xác nhận | BMS-231 | ✅ |
| FR-25 | Giảm giá có duyệt | Nhân viên, Admin | Nhân viên xin giảm theo % kèm lý do; trong hạn mức của nhân viên thì tự duyệt, vượt hạn mức chờ Admin duyệt | BMS-232 | ✅ |
| FR-26 | In hóa đơn | Nhân viên | Nút "In hóa đơn" sau khi thanh toán | BMS-234 | ✅ |
| FR-27 | Lịch sử hóa đơn | Nhân viên, Admin | Danh sách phiên đã đóng: bàn, khách, giờ, tiền giờ, tiền món, phụ thu, tổng | BMS-26 | ✅ |

### 3.4 Nhân viên (BMS-5)

| Mã | Yêu cầu | Tác nhân | Tiêu chí chấp nhận | Jira | TT |
|---|---|---|---|---|---|
| FR-30 | Thêm nhân viên | Admin | Họ tên, SĐT, PIN 4–6 số, Mã NV (tự sinh nếu trống) | BMS-27 | ✅ |
| FR-31 | Cập nhật nhân viên | Admin | Sửa hạn mức giảm giá và lương/giờ (**chưa sửa được họ tên/SĐT**) | BMS-28 | 🔶 |
| FR-32 | Xóa / vô hiệu hóa nhân viên | Admin | **Chưa làm** | BMS-29 | ⬜ |
| FR-33 | Quản lý ca làm, bàn giao ca | Nhân viên, Admin | Bắt đầu ca kèm quỹ đầu ca, kết thúc ca kèm quỹ cuối ca + ghi chú bàn giao; Admin xem lịch sử ca và lệch quỹ | BMS-30, BMS-235 | ✅ |
| FR-34 | Chấm công | Nhân viên, Admin | Vào/ra ca tự chấm công; Admin xem báo cáo số giờ làm và ước tính lương theo nhân viên, khoảng ngày | BMS-31 | ✅ |

### 3.5 Kho & đồ ăn uống (BMS-6)

| Mã | Yêu cầu | Tác nhân | Tiêu chí chấp nhận | Jira | TT |
|---|---|---|---|---|---|
| FR-40 | Danh sách vật tư/món | Nhân viên, Admin | Tên, danh mục, đơn vị, tồn, ngưỡng cảnh báo, giá bán, ảnh | BMS-32 | ✅ |
| FR-41 | Thêm/sửa/xóa món | Admin | Chỉ Admin sửa danh mục kho | BMS-35 | ✅ |
| FR-42 | Nhập hàng | Nhân viên, Admin | Phiếu nhập nhiều dòng, nhập theo đơn vị quy đổi (thùng → chai) | BMS-33, BMS-236 | ✅ |
| FR-43 | Theo dõi tồn kho, cập nhật số lượng | Nhân viên, Admin | Tồn tự giảm khi gọi món; Admin chỉnh ±; kiểm kho đếm thực tế và cân bằng tồn | BMS-34, BMS-36 | ✅ |
| FR-44 | Cảnh báo hàng sắp hết | Admin | Món dưới ngưỡng hiện ở Tổng quan | BMS-37 | ✅ |

### 3.6 Tài chính (BMS-7)

| Mã | Yêu cầu | Tác nhân | Tiêu chí chấp nhận | Jira | TT |
|---|---|---|---|---|---|
| FR-50 | Xem doanh thu | Admin | Doanh thu hôm nay, báo cáo thanh toán lọc theo phương thức, báo cáo nạp tiền | BMS-38 | ✅ |
| FR-51 | Báo cáo doanh thu theo thời gian | Admin | Có bản đồ công suất theo giờ trong ngày; **chưa lọc theo khoảng ngày tùy chọn** | BMS-41 | 🔶 |
| FR-52 | Quản lý khoản chi | Admin | **Chưa làm** | BMS-39 | ⬜ |
| FR-53 | Xem lợi nhuận | Admin | **Chưa làm** (phụ thuộc FR-52) | BMS-40 | ⬜ |
| FR-54 | Thống kê món bán chạy | Admin | **Chưa làm** | BMS-42 | ⬜ |

### 3.7 Thẻ hội viên & tích điểm (BMS-8)

| Mã | Yêu cầu | Tác nhân | Tiêu chí chấp nhận | Jira | TT |
|---|---|---|---|---|---|
| FR-60 | Thẻ hội viên (Ví) | Hệ thống | Tạo tự động khi đăng ký; hiển thị số dư, tổng đã nạp, hạng | BMS-43 | ✅ |
| FR-61 | Nạp tiền | Người chơi, Nhân viên | Khách gửi yêu cầu (tiền mặt tại quầy / chuyển khoản kèm ảnh chứng từ); nhân viên duyệt hoặc từ chối; chỉ cộng tiền khi duyệt | BMS-44 | ✅ |
| FR-62 | Thanh toán bằng thẻ | Nhân viên | Chọn hội viên (tìm theo SĐT/email/tên hoặc **quét QR**) và trừ Ví | BMS-45 | ✅ |
| FR-63 | Tích điểm | Hệ thống | **10.000đ thanh toán = 1 điểm** | BMS-46 | ✅ |
| FR-64 | Đổi điểm | Người chơi | **100 điểm = 10.000đ** vào Ví; số điểm đổi là bội số của 100, không vượt số điểm đang có | BMS-237 | ✅ |
| FR-65 | Lịch sử giao dịch thẻ | Người chơi, Admin | Nạp tiền, thanh toán, đổi điểm, trạng thái, thời gian | BMS-47 | ✅ |
| FR-66 | Mã QR hội viên | Người chơi, Nhân viên | Ví hiện mã QR có chữ ký máy chủ, **hết hạn 10 phút**, tự làm mới; nhân viên quét bằng camera, chọn ảnh hoặc dán mã; mã sửa/hết hạn bị từ chối | BMS-238 | ✅ |
| FR-67 | Hạng hội viên | Hệ thống | Đồng < 2 triệu ≤ **Bạc** < 5 triệu ≤ **Vàng** < 10 triệu ≤ **Kim Cương** (theo tổng nạp) | BMS-239 | ✅ |
| FR-68 | Gói giờ | Nhân viên, Admin | Thêm gói giờ cho hội viên, trừ giờ khi dùng, hạn dùng | BMS-240 | ✅ |

### 3.8 Dịch vụ người chơi (BMS-9)

| Mã | Yêu cầu | Tác nhân | Tiêu chí chấp nhận | Jira | TT |
|---|---|---|---|---|---|
| FR-70 | Tìm người chơi để ghép trận | Người chơi | Gợi ý đối thủ có ELO gần nhất | BMS-48 | ✅ |
| FR-71 | Lời mời ghép trận | Người chơi | Gửi lời mời dưới tên mình; chỉ người được mời chấp nhận/từ chối; người gửi được rút lại | BMS-49 | ✅ |
| FR-72 | Thuê cơ | Người chơi, Nhân viên | Khách bấm "Thuê ngay"; giảm số cơ sẵn có; nhân viên thấy lượt thuê | BMS-50 | ✅ |
| FR-73 | Trả cơ / hư hỏng | Nhân viên | Ghi nhận trả bình thường hoặc hư hỏng | BMS-51 | ✅ |
| FR-74 | Mất cơ và tiền đền | Nhân viên | Ghi nhận mất cơ kèm tiền đền | BMS-52 | ✅ |
| FR-75 | Mua cơ | Người chơi, Nhân viên | Mua bằng Ví đúng giá bán (chặn sửa giá, hết hàng); hoặc gửi yêu cầu mua → nhân viên xử lý Đã liên hệ / Đã bán (trừ tồn) / Hủy | BMS-242 | ✅ |
| FR-76 | Trao đổi cơ | Người chơi | Đăng tin trao đổi, xem tin, đóng tin của mình | BMS-243 | ✅ |
| FR-77 | Giải đấu | Admin, Người chơi | Admin tạo giải (Nháp/Mở đăng ký/...), khách đăng ký, Admin bốc thăm tạo bảng đấu, nhập người thắng; khách xem bảng đấu | BMS-241 | ✅ |
| FR-78 | Chat với quán | Người chơi, Nhân viên | Khách nhắn tin, nhân viên trả lời trong Hộp thư, có đếm tin chưa đọc | BMS-244 | ✅ |

### 3.9 Hệ thống & triển khai (BMS-225)

| Mã | Yêu cầu | Tác nhân | Tiêu chí chấp nhận | Jira | TT |
|---|---|---|---|---|---|
| FR-80 | Cấu hình chung | Admin | Sửa tên quán, hotline, email, Facebook, địa chỉ, website, tài khoản VietQR; hiển thị ở chân trang khách | BMS-247 | ✅ |
| FR-81 | Cài như ứng dụng (PWA) | Người chơi, Nhân viên | Cài từ trình duyệt; mở được khi mất mạng (trang offline); báo có phiên bản mới | BMS-246 | ✅ |
| FR-82 | Triển khai máy chủ | Quản trị hệ thống | Docker Compose + Caddy HTTPS; dữ liệu nằm ngoài container; sao lưu/khôi phục theo `DEPLOY.md` | BMS-245 | ✅ |
| FR-83 | Bản Windows 1 máy | Chủ quán | `BMS.exe` bấm đúp chạy, không cần cài .NET/Docker; lần đầu đặt mật khẩu Admin | BMS-248 | ✅ |
| FR-84 | Tìm kiếm toàn hệ thống | Người chơi, Admin | Tìm bàn, cơ, giải đấu (khách); bàn, hội viên, cơ, nhân viên (Admin) | – | ✅ |

---

## 4. Use case chính

### UC-01 Đặt bàn và nhận bàn
**Tác nhân:** Người chơi, Nhân viên. **Tiền điều kiện:** người chơi đã đăng nhập; có bàn trống.
1. Người chơi chọn bàn trống → "Đặt bàn" → nhập SĐT, thời lượng → xác nhận.
2. Hệ thống tạo yêu cầu (Chờ xác nhận), bàn chuyển "Đã đặt", báo realtime tới nhân viên.
3. Nhân viên bấm "Giữ chỗ" (Đã giữ chỗ).
4. Khách đến → nhân viên "Nhận bàn ngay" → bắt đầu tính giờ.

**Luồng thay thế:** 2a. Quá 15 phút chưa giữ chỗ → hệ thống tự hủy. 3a. Khách/nhân viên hủy → bàn trống lại.

### UC-02 Phục vụ và thanh toán một phiên chơi
**Tác nhân:** Nhân viên. **Tiền điều kiện:** bàn đang chơi.
1. Nhân viên gọi món (chọn từ kho, số lượng) – tồn kho tự giảm.
2. (Tùy chọn) Tạm dừng / chơi tiếp, chuyển bàn.
3. "Thanh toán" → nhập phụ thu (nếu có) → "Xác nhận trả bàn": hệ thống chốt tiền giờ theo khung giờ VN, làm tròn 1.000đ.
4. (Tùy chọn) Xin giảm giá → tự duyệt hoặc chờ Admin duyệt.
5. Chọn phương thức: Thẻ hội viên (tìm hoặc quét QR) / Chuyển khoản (VietQR, tích đã nhận tiền) / Tiền mặt (tính tiền thối).
6. Hệ thống ghi giao dịch, cộng điểm cho hội viên, đóng phiên, in hóa đơn.

### UC-03 Nạp tiền vào Ví
1. Người chơi nhập số tiền, phương thức (kèm ảnh chứng từ nếu chuyển khoản) → gửi.
2. Nhân viên thấy yêu cầu realtime → Duyệt (cộng số dư, cập nhật hạng) hoặc Từ chối.

### UC-04 Quản trị danh mục
Admin thêm/sửa/xóa bàn, loại bàn và khung giá, cơ, vật tư/món, giải đấu, nhân viên; xem báo cáo; sửa Cấu hình chung.

---

## 5. Yêu cầu phi chức năng

### 5.1 Hiệu năng
| Mã | Yêu cầu |
|---|---|
| NFR-01 | Thao tác thường (mở bàn, gọi món, thanh toán) phản hồi < 2 giây với ≤ 50 bàn và ≤ 20 người dùng đồng thời |
| NFR-02 | Thay đổi trạng thái bàn hiển thị trên các màn hình khác trong ≤ 2 giây (SignalR) |
| NFR-03 | Ảnh sản phẩm có nhiều kích thước, tải lười (lazy-load) để trang nhanh trên điện thoại |

### 5.2 Bảo mật
| Mã | Yêu cầu |
|---|---|
| NFR-10 | Toàn bộ truy cập qua **HTTPS** (chứng chỉ Let's Encrypt tự gia hạn), http tự chuyển https |
| NFR-11 | Xác thực bằng **JWT HS256**: kiểm tra chữ ký, issuer, audience, hạn dùng **12 giờ**; khóa ký ≥ 32 ký tự đặt ngoài mã nguồn |
| NFR-12 | **Mặc định mọi API yêu cầu JWT**; API công khai liệt kê rõ; phân quyền theo vai trò và kiểm tra quyền sở hữu dữ liệu (chỉ xem ví/điểm/mã QR của chính mình) |
| NFR-13 | Mật khẩu/PIN băm **PBKDF2-SHA256, 100.000 vòng, salt riêng**; không lưu mật khẩu gốc |
| NFR-14 | CORS chỉ cho phép 2 tên miền của hệ thống |
| NFR-15 | Mã hủy đặt bàn, mã QR hội viên ký HMAC-SHA256, có hạn dùng; không lộ thông tin cá nhân khi không có mã |
| NFR-16 | Quên mật khẩu không tiết lộ email có tồn tại; mã đặt lại hết hạn 15 phút |

### 5.3 Độ tin cậy, sao lưu
| Mã | Yêu cầu |
|---|---|
| NFR-20 | Dữ liệu (database, ảnh, cấu hình) nằm ngoài container; cập nhật phiên bản không mất dữ liệu |
| NFR-21 | Có quy trình sao lưu và khôi phục (DEPLOY.md); container tự khởi động lại khi máy chủ khởi động lại |
| NFR-22 | Mất kết nối → tự kết nối lại và tải lại dữ liệu, có thông báo tiếng Việt; mất mạng ở trang khách → trang offline |

### 5.4 Khả dụng, giao diện
| Mã | Yêu cầu |
|---|---|
| NFR-30 | Giao diện tiếng Việt, thống nhất (khung nội dung ≤ 1200px, cùng kiểu tiêu đề/thẻ/nút) ở cả 3 cổng |
| NFR-31 | Responsive 360–1920px: không tràn ngang; điện thoại 1 cột, nút ≥ 44px, menu dưới; bảng cuộn ngang trong khung |
| NFR-32 | Trạng thái rỗng có biểu tượng + 1 dòng chữ (+ nút hành động khi phù hợp) |
| NFR-33 | Thời gian hiển thị theo giờ Việt Nam; tiền định dạng 90.000đ |

### 5.5 Khả năng bảo trì, triển khai
| Mã | Yêu cầu |
|---|---|
| NFR-40 | Mã nguồn quản lý trên GitHub; tách 5 project (API, AdminStaffWeb, PlayerWeb, Shared, Launcher) |
| NFR-41 | Triển khai bằng 1 lệnh `docker compose up -d --build`; cấu hình qua `.env` |
| NFR-42 | Công thức nghiệp vụ dùng chung đặt trong `BMS.Shared` (tính tiền giờ, điểm, hạng) |

---

## 6. Yêu cầu giao diện ngoài

### 6.1 Giao diện người dùng
- Trang Người chơi: Bàn chơi, Thuê & Mua Cơ, Giải đấu, Ghép trận, Ví, Hồ sơ, Tìm kiếm, Đăng nhập/Đăng ký/Quên mật khẩu.
- Cổng Nhân viên: Bàn chơi (sơ đồ bàn), Thuê & Mua Cơ, Yêu cầu mua cơ, Duyệt nạp tiền, Hộp thư, Ca làm việc, Lịch sử phiên chơi, Giải đấu.
- Cổng Quản trị: Tổng quan, Bàn Live, Quản lý bàn, Loại bàn & giá, Cơ, Kho, Giải đấu, Nhân viên, Hội viên, Nạp tiền, Báo cáo & Doanh thu, Lịch sử ca, Chấm công, Cấu hình chung.

### 6.2 Giao diện phần mềm (API)
REST JSON tại `https://api.bmsbilliard.com/api/...` (130 endpoint), nhóm: `accounts`, `tables`, `table-types`,
`bookings`, `sessions`, `cues`, `cue-listings`, `inventory`, `wallet`, `discount-requests`, `shifts`,
`attendance`, `tournaments`, `match-requests`, `players`, `chat`, `uploads`, `settings`.
Realtime: SignalR `/hubs/bms` (sự kiện `TableStatusChanged`, `BookingExpired`, `MatchRequestChanged`,
`CueRentalChanged`, `CuePurchaseRequestChanged`, `TransactionCreated`...).

### 6.3 Giao diện truyền thông
HTTPS (TLS) qua Caddy; WebSocket cho SignalR; email SMTP (tùy chọn) cho quên mật khẩu; ảnh VietQR từ img.vietqr.io.

---

## 7. Mô hình dữ liệu (thực thể chính)

| Nhóm | Thực thể |
|---|---|
| Tài khoản | Account (vai trò, mã NV, hạn mức giảm giá, lương/giờ), PlayerProfile (ELO, Ví, hạng, tổng nạp) |
| Bàn | Table, TableType, PricingSlot (khung giờ giá), Booking, PlayingSession, Order (món đã gọi) |
| Thanh toán | Transaction (nạp tiền, thanh toán, đổi điểm), DiscountRequest, HourPackage |
| Cơ | Cue, CueRental, CuePurchaseRequest, CueListing |
| Kho | InventoryItem, StockIn + StockInLine, StockAudit + StockAuditLine |
| Nhân sự | Shift, Attendance |
| Giải đấu, ghép trận | Tournament, TournamentRegistration, TournamentMatch, MatchRequest |
| Liên lạc | ChatThread, ChatMessage |

Cơ sở dữ liệu SQLite, 28 bảng, tạo bằng Entity Framework Core.

---

## 8. Kiểm thử và nghiệm thu

| Bộ kiểm thử tự động | Kết quả |
|---|---|
| Vai trò, đăng nhập, phân quyền giao diện | 62/62 |
| Đầy đủ chức năng (đặt bàn, thanh toán 3 phương thức, nạp tiền, cơ, kho, giải đấu, báo cáo...) | 59/59 |
| Chức năng bổ sung (tạm dừng, chuyển bàn, hủy đặt bàn, mua cơ, tích/đổi điểm, QR hội viên bằng camera, cấu hình chung) | 67/67 |
| Đặt bàn realtime giữa 2 trình duyệt | 3/3 |
| Quét mọi trang 3 cổng (ảnh, logo, tràn ngang, lỗi console) | 88/88 |
| Responsive 390/768/1440px | 21/21 + giao diện 78/78 |
| PWA / mất kết nối | 10/10, 5/5 |
| Bảo mật JWT (không token, token giả/sửa quyền/hết hạn, sai vai trò, mạo danh) | 41/41 |
| Trên tên miền thật (không đăng nhập / có đăng nhập) | 46/46 / 97 mục đạt |

---

## 9. Hạn chế và hướng phát triển

| Hạng mục | Tình trạng | Jira |
|---|---|---|
| Xóa / vô hiệu hóa nhân viên; sửa họ tên, SĐT nhân viên | Chưa làm / một phần | BMS-29, BMS-28 |
| Quản lý khoản chi, lợi nhuận, thống kê món bán chạy, báo cáo theo khoảng ngày | Chưa làm / một phần | BMS-39, 40, 42, 41 |
| Gửi email quên mật khẩu trên máy chủ | Cần cấu hình SMTP | – |
| Xác thực SĐT bằng SMS thật | Chưa tích hợp nhà cung cấp SMS | – |
| Thẻ hội viên in giấy/nhựa (mã QR cố định) | Chưa làm (mã QR hiện hết hạn 10 phút) | – |
| Đối soát chuyển khoản tự động | Chưa làm (nhân viên xác nhận thủ công) | – |
