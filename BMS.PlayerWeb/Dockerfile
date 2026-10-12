# Build context: thư mục gốc dự án (chứa BMS.sln)
# Build Blazor WebAssembly ra file tĩnh rồi phục vụ bằng Caddy (Caddy cũng làm
# reverse proxy + HTTPS cho API và Admin Web, xem deploy/Caddyfile).
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src
COPY BMS.Shared/BMS.Shared.csproj BMS.Shared/
COPY BMS.PlayerWeb/BMS.PlayerWeb.csproj BMS.PlayerWeb/
RUN dotnet restore BMS.PlayerWeb/BMS.PlayerWeb.csproj
COPY BMS.Shared/ BMS.Shared/
COPY BMS.PlayerWeb/ BMS.PlayerWeb/
RUN dotnet publish BMS.PlayerWeb/BMS.PlayerWeb.csproj -c Release -o /app/publish --no-restore

# Ghi địa chỉ API và cổng nhân viên thật vào wwwroot/appsettings.json, xóa bản nén cũ
# (.br/.gz) của file này để Caddy không trả nhầm bản localhost.
ARG API_BASE_URL=https://localhost:5001
ARG STAFF_PORTAL_URL=https://localhost:5101
RUN printf '{\n  "Api": {\n    "BaseUrl": "%s"\n  },\n  "StaffPortal": {\n    "Url": "%s"\n  }\n}\n' "$API_BASE_URL" "$STAFF_PORTAL_URL" > /app/publish/wwwroot/appsettings.json \
 && rm -f /app/publish/wwwroot/appsettings.json.br /app/publish/wwwroot/appsettings.json.gz \
 && cat /app/publish/wwwroot/appsettings.json

FROM caddy:2.10-alpine AS final
COPY --from=build /app/publish/wwwroot /srv/player
COPY deploy/Caddyfile /etc/caddy/Caddyfile
