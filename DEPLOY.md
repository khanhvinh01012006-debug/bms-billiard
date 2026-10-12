# Triển khai BMS lên VPS (Docker + Caddy + HTTPS)

| Tên miền | Dịch vụ | Container |
|---|---|---|
| https://bmsbilliard.com | BMS.PlayerWeb (Blazor WASM, file tĩnh) | `caddy` |
| https://admin.bmsbilliard.com | BMS.AdminStaffWeb (Blazor Server) | `admin` |
| https://api.bmsbilliard.com | BMS.API + SignalR `/hubs/bms` | `api` |

Caddy tự xin/gia hạn chứng chỉ Let's Encrypt và chuyển http -> https.
Dữ liệu (`bms.db`, ảnh `uploads/`, cấu hình chung `settings.json`) nằm ở thư mục `./data` trên VPS, build lại không mất.
Các container chạy giờ Việt Nam (`TZ=Asia/Ho_Chi_Minh`); tiền giờ tính theo khung giờ VN, làm tròn tới 1.000đ.
Lần chạy đầu, nếu `./data/bms.db` chưa có thì API tự chép database demo (`deploy/seed/bms-demo.db`).

Yêu cầu: VPS Ubuntu 22.04 có Docker + Docker Compose, mở cổng 80 và 443,
DNS của 3 tên miền đã trỏ về IP VPS (163.61.182.77).

## 1. Đóng gói và chép dự án lên VPS (chạy trên máy Windows, PowerShell)

```powershell
cd D:\
tar -czf bms-deploy.tar.gz --exclude=bin --exclude=obj --exclude=.vs --exclude=BMS.PlayerApp --exclude="bms.db*" --exclude=data --exclude=App_Data --exclude=.env --exclude="*.docx" --exclude="*.tar.gz" --exclude=dist --exclude=BMS.Launcher -C D:\CNPMNC .
scp bms-deploy.tar.gz root@163.61.182.77:/root/
```

(Thay `root` bằng user SSH của bạn nếu khác.)

## 2. Giải nén và cấu hình trên VPS

```bash
ssh root@163.61.182.77
mkdir -p /opt/bms && tar -xzf /root/bms-deploy.tar.gz -C /opt/bms
cd /opt/bms
cp .env.example .env
openssl rand -base64 48        # copy kết quả dán vào JWT_KEY
nano .env                      # sửa JWT_KEY, ADMIN_PASSWORD (xem mục 6)
```

## 3. Chạy

```bash
cd /opt/bms
docker compose up -d --build
docker compose ps              # 3 container api, admin, caddy phải "Up"
```

Lần đầu build mất vài phút (tải image .NET 10). Sau khi lên, Caddy cần thêm khoảng
10-60 giây để lấy chứng chỉ HTTPS. Kiểm tra:

```bash
curl -I https://bmsbilliard.com
curl https://api.bmsbilliard.com/api/tables
```

## 4. Xem log

```bash
docker compose logs -f api       # API (Ctrl+C để thoát)
docker compose logs -f admin     # Admin Web
docker compose logs -f caddy     # HTTPS / chứng chỉ / proxy
docker compose logs --tail 100   # 100 dòng cuối của tất cả
docker compose restart api       # khởi động lại 1 dịch vụ
```

Lỗi chứng chỉ thường do DNS chưa trỏ đúng hoặc cổng 80/443 bị chặn: xem `docker compose logs caddy`.

## 5. Sao lưu / khôi phục dữ liệu (bms.db, uploads, settings.json)

Sao lưu (dừng API vài giây để file SQLite nhất quán):

```bash
cd /opt/bms
mkdir -p backups
docker compose stop api
tar -czf backups/bms-$(date +%F-%H%M).tar.gz data
docker compose start api
ls -lh backups
```

Tải bản sao lưu về máy Windows (PowerShell):

```powershell
scp root@163.61.182.77:/opt/bms/backups/bms-*.tar.gz D:\
```

Khôi phục:

```bash
cd /opt/bms
docker compose stop api
rm -rf data && tar -xzf backups/bms-YYYY-MM-DD-HHMM.tar.gz
docker compose start api
```

Muốn quay về database demo ban đầu: `docker compose stop api && rm -rf data && docker compose start api`.

## 6. Cập nhật phiên bản mới

Chép lại code như bước 1-2 (giải nén đè vào `/opt/bms`, **giữ nguyên `.env` và `data/`**), rồi:

```bash
cd /opt/bms
docker compose up -d --build
```

## Biến cấu hình (.env)

| Biến | Ý nghĩa |
|---|---|
| `PLAYER_DOMAIN`, `ADMIN_DOMAIN`, `API_DOMAIN` | 3 tên miền. Đổi `API_DOMAIN` thì phải `--build` lại (địa chỉ API được ghi vào PlayerWeb lúc build) |
| `JWT_KEY` | Khóa ký đăng nhập, >= 32 ký tự ngẫu nhiên. Đổi khóa = mọi người phải đăng nhập lại; mã QR hội viên (10 phút) và mã hủy đặt bàn đang lưu trên máy khách cũng hết hiệu lực |
| `ADMIN_EMAIL` | Tài khoản admin (mặc định `admin@bms.local`) |
| `ADMIN_PASSWORD` | Mật khẩu admin: dùng khi tạo admin lần đầu, hoặc đặt lại nếu `ADMIN_RESET_PASSWORD=true` |
| `ADMIN_RESET_PASSWORD` | `true`: mỗi lần API khởi động đặt lại mật khẩu admin = `ADMIN_PASSWORD` (cần vì DB demo có sẵn admin `Admin@123`). Nếu sau này đổi mật khẩu admin trên giao diện thì đặt `false` |

Thông tin quán (hotline, email, Facebook, địa chỉ, website) và tài khoản nhận chuyển khoản VietQR
sửa trong cổng Admin > **Cấu hình chung** (không cần sửa .env hay build lại).

CORS của API chỉ cho phép `https://PLAYER_DOMAIN` và `https://ADMIN_DOMAIN` (tự lấy từ .env).

## Chạy local (Visual Studio) vẫn như cũ

`appsettings.Development.json` của API giữ khóa JWT dev và mật khẩu `Admin@123`,
CORS cho phép localhost:5101/5201; PlayerWeb đọc `wwwroot/appsettings.json`
(mặc định `https://localhost:5001`).
