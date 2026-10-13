# Bộ câu hỏi – trả lời chuẩn bị trình bày BMS

> Câu trả lời bám đúng hệ thống đang chạy (https://bmsbilliard.com) và mã nguồn trên GitHub.
> Mỗi câu có **câu trả lời ngắn** (nói trước), phần in nghiêng là **chi tiết / chỗ mở để chứng minh**.
> Ký hiệu: ⚠️ = câu dễ bị hỏi vặn, cần trả lời thẳng về hạn chế.

**Mục lục**
- A. Nghiệp vụ tổng quan
- B. Bàn & đặt bàn
- C. Tính tiền & thanh toán
- D. Ví, hội viên, điểm, mã QR
- E. Nhân viên, ca làm, kho
- F. Cơ, giải đấu, ghép trận
- G. Câu hỏi theo 7 tiêu chí (Backlog · GitHub · SRS · Jira · BE · FE · Deploy)
- H. Câu hỏi về hạn chế và hướng phát triển
- I. Số liệu cần nhớ

---

## A. Nghiệp vụ tổng quan

**A1. Hệ thống giải quyết bài toán gì?**
Quán billiard đang quản lý thủ công: ghi giờ mở/trả bàn bằng tay, tính tiền dễ sai, không biết bàn nào trống,
khách phải gọi điện đặt bàn. BMS số hóa toàn bộ: sơ đồ bàn trực tiếp, đặt bàn online, tự tính tiền theo giờ,
thanh toán nhiều cách, ví hội viên, kho, nhân viên và báo cáo.

**A2. Có những ai dùng hệ thống, mỗi người làm gì?**
- **Khách chưa đăng nhập:** xem bàn trống, cơ, giải đấu.
- **Người chơi (hội viên):** đặt/hủy bàn, ví, nạp tiền, tích/đổi điểm, mã QR, thuê/mua/trao đổi cơ, đăng ký giải, ghép trận, chat với quán.
- **Nhân viên:** mở/trả bàn, gọi món, thanh toán, duyệt đặt bàn và nạp tiền, cho thuê cơ, nhập/kiểm kho, giao ca, chấm công.
- **Quản trị (chủ quán):** mọi việc của nhân viên + quản lý bàn/giá/cơ/kho/giải đấu/nhân viên/hội viên, báo cáo, cấu hình quán.

**A3. Vì sao chia 3 cổng?**
Mỗi đối tượng có nhu cầu khác: khách dùng điện thoại (trang Người chơi, cài như app – PWA); nhân viên cần thao tác
nhanh tại quầy; Admin cần màn hình quản lý nhiều thông tin. Tách cổng còn giúp **khách không bao giờ thấy
chức năng quản lý**. Cả 3 dùng chung 1 API, nên dữ liệu luôn thống nhất.
*Chứng minh: tài khoản khách đăng nhập cổng admin.bmsbilliard.com sẽ bị từ chối.*

**A4. Một ngày làm việc của quán diễn ra trên hệ thống thế nào?**
Nhân viên vào ca (nhập quỹ đầu ca, tự chấm công) → khách đặt bàn online hoặc đến trực tiếp → nhân viên mở bàn,
đồng hồ chạy → gọi món → trả bàn: hệ thống tính tiền giờ + món + phụ thu → thanh toán (Ví / chuyển
khoản QR / tiền mặt) → in hóa đơn → cuối ca nhập quỹ cuối ca, bàn giao → Admin xem doanh thu, chấm công.

---

## B. Bàn & đặt bàn

**B1. Bàn có những trạng thái nào?**
Trống, Đang chơi, Đã đặt, Bảo trì; phiên chơi có thể ở trạng thái Tạm dừng. Mọi thay đổi hiện **ngay lập tức** trên
tất cả màn hình (realtime qua SignalR), không cần tải lại trang.

**B2. Quy trình đặt bàn?**
Khách chọn bàn trống → nhập SĐT, thời lượng (60/90/120/180 phút) → bàn chuyển "Đã đặt", nhân viên thấy yêu cầu
ngay → nhân viên bấm **Giữ chỗ** → khách đến thì **Nhận bàn ngay** để bắt đầu tính giờ.

**B3. Khách đặt rồi không đến thì sao?** ⚠️
Yêu cầu chưa được giữ chỗ quá **15 phút** thì hệ thống **tự hủy** và trả bàn về trống (quét mỗi 45 giây). Khách đến
trễ thì nhân viên vẫn "Nhận khách đến trễ" được. Đã giữ chỗ thì nhân viên chủ động "Hủy giữ chỗ" khi cần.

**B4. Khách hủy đặt bàn thế nào? Người khác có hủy hộ được không?**
Khách hủy ở mục "Đặt bàn của tôi". Mỗi đặt bàn có **mã hủy riêng có chữ ký**, chỉ người đặt (giữ mã trên máy)
mới hủy hoặc xem được; người khác đoán số đặt bàn cũng không xem được tên, SĐT. Nhân viên hủy được mọi đặt bàn.

**B5. Hai người cùng đặt một bàn cùng lúc thì sao?**
API kiểm tra trong giao dịch database: người thứ hai nhận thông báo "Bàn vừa được người khác đặt. Vui lòng chọn bàn khác."

**B6. Tạm dừng phiên chơi dùng khi nào, tính tiền ra sao?**
Khi khách ra ngoài, bàn có sự cố… Lúc tạm dừng đồng hồ đứng yên, **thời gian dừng không bị tính tiền**; vẫn gọi
món được. Chơi tiếp thì giờ bắt đầu được dời lên đúng bằng thời gian đã dừng.
*Không thêm cột database: lúc dừng lưu tạm thời điểm dừng, lúc chơi tiếp thì cộng bù.*

**B7. Chuyển bàn thì tiền tính thế nào?** ⚠️
Giữ nguyên phiên, giờ bắt đầu và món đã gọi; khi trả bàn, **cả phiên tính theo giá của bàn mới**. Đây là quy tắc
nhóm chọn cho đơn giản, nêu rõ trong SRS; nếu quán muốn tính 2 đoạn theo 2 giá thì là hướng phát triển.

---

## C. Tính tiền & thanh toán

**C1. Tiền giờ tính thế nào?**
Mỗi loại bàn có **khung giờ giá** (ví dụ 8:00–17:00 giá A, 17:00–2:00 giá B). Hệ thống chia thời gian chơi theo
từng khung, tính theo **giờ Việt Nam (UTC+7)**, cộng lại rồi **làm tròn tới 1.000đ gần nhất**. Loại bàn không có
khung giờ thì dùng giá/giờ của bàn.
*Code dùng chung: `BMS.Shared/TablePricing.cs` – tạm tính trên thẻ bàn và tiền chốt dùng cùng công thức nên luôn khớp.*

**C2. Vì sao làm tròn 1.000đ?**
Thực tế quán không thối lẻ dưới 1.000đ. Quy tắc: làm tròn **gần nhất** (500đ trở lên làm tròn lên).

**C3. Hóa đơn gồm những gì?**
Thời gian chơi, tiền giờ, tiền món (theo từng món), phụ thu (kèm lý do, ví dụ làm hỏng nỉ), giảm giá, tổng cộng,
phương thức thanh toán, thời điểm, có nút In hóa đơn.

**C4. Có những cách thanh toán nào?**
- **Thẻ hội viên (Ví):** trừ số dư; không đủ tiền thì báo thiếu bao nhiêu. Chọn hội viên bằng tìm SĐT/email/tên hoặc **quét mã QR**.
- **Chuyển khoản:** hiện mã **VietQR có sẵn số tiền và nội dung**, khách quét bằng app ngân hàng bất kỳ; nhân viên phải tích "đã nhận tiền" mới xác nhận được.
- **Tiền mặt:** nhập tiền khách đưa, hệ thống tính tiền thối.

**C5. Chuyển khoản có tự biết tiền đã vào tài khoản không?** ⚠️
**Không.** Nhân viên xem app ngân hàng rồi xác nhận. Tự đối soát cần API ngân hàng hoặc cổng thanh toán (trả phí),
nằm ngoài phạm vi, ghi trong phần hạn chế của SRS.

**C6. Giảm giá kiểm soát thế nào để nhân viên không tự ý giảm?**
Mỗi nhân viên có **hạn mức giảm giá (%)** do Admin đặt. Trong hạn mức thì tự duyệt; vượt hạn mức thì yêu cầu
chuyển tới Admin, Admin **duyệt/từ chối** ở Tổng quan, kèm lý do. Mọi lần giảm đều được lưu.

**C7. Gọi món có trừ kho không?** ⚠️
**Chưa tự trừ.** Khi gọi món, tên và **giá lấy từ kho** (nhân viên không sửa giá được), nhưng số tồn chưa tự giảm;
tồn kho được cân bằng bằng **phiếu kiểm kho** (đếm thực tế) và chỉnh ±. Đây là hạn chế đã ghi trong SRS, hướng
phát triển là trừ tồn ngay khi gọi món và hoàn lại khi xóa món. Ngoài ra có "dịch vụ nhập tay" cho khoản không có trong kho.

---

## D. Ví, hội viên, điểm, mã QR

**D1. Ví / thẻ hội viên là gì, tạo khi nào?**
Tài khoản trả trước của người chơi, **tạo tự động khi đăng ký**. Dùng để thanh toán tại quán, mua cơ.

**D2. Nạp tiền vào ví thế nào? Khách có tự cộng tiền được không?** ⚠️
Khách **gửi yêu cầu** nạp (tiền mặt tại quầy hoặc chuyển khoản kèm ảnh chứng từ). Tiền **chỉ cộng khi nhân viên
duyệt**; nhân viên cũng có thể từ chối. Khách không có cách nào tự cộng tiền, kể cả gọi thẳng API (bảo vệ bằng JWT và phân quyền).

**D3. Hạng hội viên xét thế nào?**
Theo **tổng tiền đã nạp lũy kế**: Đồng < 2 triệu ≤ **Bạc** < 5 triệu ≤ **Vàng** < 10 triệu ≤ **Kim Cương**. Ví hiển thị
tiến độ còn bao nhiêu để lên hạng.

**D4. Tích điểm và đổi điểm?**
**10.000đ thanh toán = 1 điểm.** Đổi **100 điểm = 10.000đ** vào ví (tức hoàn khoảng 1%). Số điểm đổi phải là bội số
của 100 và không vượt quá số điểm đang có.
*Điểm tính từ lịch sử giao dịch, không thêm bảng mới → không lệch số liệu.*

**D5. Mã QR hội viên để làm gì, có bị chụp ảnh dùng lại không?**
Khi thanh toán, nhân viên quét mã QR trong Ví của khách để chọn đúng hội viên (camera, ảnh hoặc dán mã). Mã có
**chữ ký của máy chủ** (không giả được) và **hết hạn sau 10 phút** (tự làm mới), nên ảnh chụp cũ không dùng lại được.

**D6. Có in thẻ hội viên bằng nhựa được không?** ⚠️
Chưa. Mã QR hiện hết hạn 10 phút nên không in cố định được. Hướng phát triển: mã thẻ cố định kèm chức năng
"cấp lại thẻ" khi mất.

**D7. Gói giờ là gì?**
Hội viên mua trước một số giờ chơi; nhân viên/Admin thêm gói, trừ giờ mỗi lần dùng, gói có hạn dùng.

---

## E. Nhân viên, ca làm, kho

**E1. Nhân viên đăng nhập bằng gì? Vì sao không dùng email?**
**Mã nhân viên + PIN** (4–6 số), do Admin tạo. Nhân viên không cần email; đăng nhập nhanh tại quầy.

**E2. Quản lý ca và bàn giao ca?**
Đầu ca nhập **quỹ đầu ca** (tự chấm công vào); cuối ca nhập **quỹ cuối ca** + ghi chú bàn giao (tự chấm công ra).
Admin xem lịch sử ca và **lệch quỹ**.

**E3. Chấm công và lương?**
Báo cáo chấm công theo nhân viên, khoảng ngày: giờ vào/ra, số giờ làm, **ước tính lương = số giờ × lương/giờ** của
nhân viên đó.

**E4. Quản lý kho có gì?**
Danh mục vật tư/món (đơn vị, giá bán, ngưỡng cảnh báo, ảnh) → **phiếu nhập** theo đơn vị quy đổi (1 thùng = 24 lon)
→ **phiếu kiểm kho** đếm thực tế và cân bằng tồn (tồn chưa tự giảm khi gọi món) → **cảnh báo hàng sắp hết** ở Tổng quan.
Nhân viên được nhập hàng và kiểm kho; chỉ Admin sửa danh mục.

**E5. Có xóa nhân viên được không?** ⚠️
Chưa (BMS-29 còn To Do trên Jira). Hiện Admin sửa được hạn mức giảm giá và lương/giờ; sửa họ tên/SĐT nhân viên
đang làm (BMS-28 In Progress).

---

## F. Cơ, giải đấu, ghép trận

**F1. Thuê cơ, trả cơ, cơ hỏng/mất xử lý thế nào?**
Khách bấm "Thuê ngay" → số cơ sẵn có giảm → nhân viên ghi nhận khi trả: **Trả bình thường / Hư hỏng / Mất** (kèm
tiền đền).

**F2. Mua cơ thế nào?**
Hai cách: **mua bằng ví** (hệ thống kiểm tra đúng giá bán, còn hàng, trừ ví, giảm tồn), hoặc **gửi yêu cầu mua** → nhân
viên liên hệ → đánh dấu **Đã liên hệ / Đã bán (trừ tồn) / Hủy**; menu nhân viên có số đếm yêu cầu cần xử lý.
*Có chặn khách sửa giá khi mua bằng ví (gửi số tiền sai → bị từ chối).*

**F3. Trao đổi cơ (Trade)?**
Khách đăng tin trao đổi (tiêu đề, mô tả, muốn đổi lấy gì), xem tin của người khác, đóng tin của mình.

**F4. Giải đấu?**
Admin tạo giải (Nháp → Mở đăng ký → Đang diễn ra → Đã kết thúc), khách đăng ký, Admin **bốc thăm** tạo bảng đấu,
nhập người thắng từng trận; khách xem bảng đấu cập nhật trực tiếp. Giải Nháp khách không thấy.

**F5. Ghép trận theo ELO?**
Hệ thống gợi ý đối thủ có **điểm ELO gần nhất**; người chơi gửi lời mời, **chỉ người được mời** mới chấp nhận/từ chối.

---

## G. Câu hỏi theo 7 tiêu chí

### G1. Backlog
**Backlog của nhóm gồm những gì, xây dựng thế nào?**
Chia theo **10 epic** (Tài khoản & phân quyền, Bàn & đặt bàn, Thanh toán & hóa đơn, Nhân viên, Kho & thực phẩm,
Tài chính, Thẻ & hội viên, Dịch vụ người chơi, Hệ thống & triển khai). Mỗi epic có các **user story** kèm story point,
mỗi story chia **subtask** theo Database / Backend / Frontend / Testing.

**Vì sao có thêm story so với kế hoạch ban đầu?**
Trong quá trình làm và kiểm thử, nhóm phát hiện nhu cầu thực tế (tạm dừng phiên, VietQR, QR hội viên, đổi điểm,
bảo mật JWT, PWA…) nên bổ sung story BMS-226 → BMS-250 vào backlog.

### G2. GitHub
**Mã nguồn tổ chức thế nào?**
1 solution, các project: `BMS.API` (backend), `BMS.AdminStaffWeb` (cổng NV/Admin), `BMS.PlayerWeb` (trang
khách), `BMS.Shared` (dùng chung: DTO, công thức tính tiền, điểm, hạng), `BMS.Launcher` (bản Windows); thư mục
`docs/` (SRS, JWT, hỏi đáp), `deploy/` (Caddy, dữ liệu mẫu).
**Có đưa mật khẩu/khóa lên GitHub không?**
Không. `.env` (khóa JWT, mật khẩu Admin), database thật, ảnh upload đều nằm trong `.gitignore`. Dữ liệu mẫu đã
được làm sạch thông tin cá nhân trước khi công khai.

### G3. SRS
**SRS gồm những phần nào?**
Theo IEEE 830 rút gọn: giới thiệu, mô tả tổng quan (kiến trúc, người dùng, ràng buộc), **hơn 60 yêu cầu chức năng**
có tiêu chí chấp nhận và **ánh xạ mã story Jira**, use case chính, yêu cầu phi chức năng (hiệu năng, bảo mật, sao lưu,
giao diện), giao diện ngoài, mô hình dữ liệu (28 bảng), kết quả kiểm thử, hạn chế. File: `docs/SRS.md`.
**Một yêu cầu phi chức năng tiêu biểu?**
"Thay đổi trạng thái bàn hiển thị trên các màn hình khác trong ≤ 2 giây" (NFR-02); "mặc định mọi API yêu cầu
JWT" (NFR-12); "responsive 360–1920px, không tràn ngang" (NFR-31).

### G4. Jira
**Tiến độ hiện tại?**
**62 story Done, 2 In Progress** (BMS-28 sửa thông tin nhân viên, BMS-41 báo cáo theo khoảng ngày), **4 To Do**
(BMS-29 xóa nhân viên, BMS-39 khoản chi, BMS-40 lợi nhuận, BMS-42 món bán chạy). Chia 3 sprint: Sprint 1 nền tảng
tài khoản, bàn, nhân viên; Sprint 2 hóa đơn, thanh toán, ca, kho; Sprint 3 tài chính, thẻ hội viên, dịch vụ người chơi.
**Workflow trạng thái?**
To Do → In Progress → Code Review → Testing → Done.

### G5. Backend
**Công nghệ backend?**
ASP.NET Core (.NET 10) Web API, Entity Framework Core + SQLite, **SignalR** cho realtime, **JWT** cho xác thực.
**130 endpoint REST** chia nhóm: accounts, tables, bookings, sessions, cues, wallet, inventory, tournaments…
**Backend bảo vệ thế nào?**
JWT (chi tiết: `docs/BAO-MAT-JWT.md`): mặc định mọi API bắt buộc token, phân quyền theo vai trò, kiểm tra quyền sở hữu
theo `userId` trong token, mật khẩu băm PBKDF2, CORS chỉ cho 2 tên miền, HTTPS. Kiểm thử bảo mật 41/41.
**Vì sao dùng SQLite?**
Quy mô 1 quán, ít người ghi đồng thời; SQLite không cần cài máy chủ CSDL, sao lưu chỉ là chép 1 file. Nhiều chi nhánh
thì chuyển sang SQL Server/PostgreSQL (EF Core hỗ trợ, đổi provider).
**Realtime hoạt động thế nào?**
Sau khi lưu database thành công, API phát sự kiện qua SignalR (`TableStatusChanged`, `TransactionCreated`…); các
màn hình đang mở nhận và tải lại phần liên quan. Mất mạng thì tự kết nối lại và đồng bộ.

### G6. Frontend
**Công nghệ frontend?**
**Blazor**: trang Người chơi là Blazor WebAssembly (chạy hẳn trong trình duyệt, cài như app – PWA); cổng NV/Admin
là Blazor Server. Viết bằng C# chung với backend, dùng chung DTO và công thức tính tiền.
**Giao diện trên điện thoại?**
Responsive 360–1920px: điện thoại 1 cột, thanh điều hướng dưới, nút ≥ 44px, bảng cuộn ngang trong khung; đã kiểm tra
mọi trang ở 390/768/1440px, không tràn ngang.
**PWA là gì, demo được không?**
Trang khách cài được như ứng dụng (Chrome: biểu tượng Cài đặt; iPhone: Thêm vào màn hình chính), có biểu tượng riêng,
mở được khi mất mạng (trang offline), báo khi có phiên bản mới.

### G7. Deploy
**Triển khai thế nào?**
VPS Ubuntu + **Docker Compose** 3 container: `api`, `admin`, `caddy`. **Caddy** tự xin và gia hạn chứng chỉ HTTPS
Let's Encrypt, phục vụ trang khách và chuyển tiếp tới API, Admin. 3 tên miền: bmsbilliard.com, admin.bmsbilliard.com,
api.bmsbilliard.com.
**Cập nhật phiên bản có mất dữ liệu không?**
Không. Database, ảnh và cấu hình nằm ở thư mục `data/` ngoài container. Quy trình mỗi lần cập nhật: sao lưu `/opt/bms`
và `data/` → giải nén bản mới → `docker compose up -d --build` → kiểm tra → có lệnh khôi phục nếu lỗi.
**Máy chủ khởi động lại thì sao?**
Container cấu hình `restart: unless-stopped` nên tự chạy lại.
**Ngoài VPS còn cách chạy nào?**
Bản Windows `BMS.exe` bấm đúp là chạy trên 1 máy (không cần cài .NET/Docker), dùng cho trình diễn hoặc quầy thu ngân.

---

## H. Câu hỏi về hạn chế và hướng phát triển ⚠️

Trả lời thẳng, không né, rồi nêu hướng làm tiếp.

| Câu hỏi | Trả lời |
|---|---|
| Còn chức năng nào chưa làm? | Xóa nhân viên, quản lý khoản chi, lợi nhuận, thống kê món bán chạy (To Do); sửa họ tên NV và báo cáo theo khoảng ngày đang làm; tự trừ tồn kho khi gọi món |
| Quên mật khẩu có gửi email không? | Có code gửi email, nhưng máy chủ chưa cấu hình SMTP nên hiện chưa gửi thật |
| Xác thực SĐT bằng SMS? | Chưa nối nhà cung cấp SMS, mã OTP đang chạy kiểu thử nghiệm |
| Chuyển khoản tự đối soát? | Chưa, nhân viên xác nhận thủ công |
| Thu hồi một token khi bị lộ? | Chưa có thu hồi từng token và refresh token; hiện giới hạn bằng hạn 12 giờ và đổi khóa khi cần |
| Nhiều chi nhánh? | Chưa hỗ trợ; cần thêm khái niệm chi nhánh và chuyển sang CSDL máy chủ |
| Kiểm thử thế nào? | Bộ kiểm thử tự động điều khiển trình duyệt thật (khách, nhân viên, admin cùng lúc) + kiểm thử API: hơn 400 kiểm tra, đều đạt; kiểm tra lại trên tên miền thật |

---

## I. Số liệu cần nhớ

| Thông tin | Giá trị |
|---|---|
| Tự hủy đặt bàn chưa giữ chỗ | 15 phút |
| Thời lượng đặt bàn | 60 / 90 / 120 / 180 phút |
| Làm tròn tiền giờ | 1.000đ gần nhất, theo giờ Việt Nam |
| Tích điểm / đổi điểm | 10.000đ = 1 điểm · 100 điểm = 10.000đ |
| Hạng hội viên | Bạc ≥ 2 triệu · Vàng ≥ 5 triệu · Kim Cương ≥ 10 triệu (tổng nạp) |
| Mã QR hội viên | Hết hạn 10 phút, tự làm mới |
| Mã đặt lại mật khẩu | Hết hạn 15 phút |
| Token JWT | HS256, hạn 12 giờ |
| PIN nhân viên | 4–6 chữ số |
| API | 130 endpoint, 28 bảng dữ liệu |
| Jira | 10 epic · 68 story (62 Done · 2 In Progress · 4 To Do) |
| Kiểm thử | JWT 41/41 · chức năng 59/59 + 67/67 · mọi trang 88/88 · trên tên miền thật 46/46 |
