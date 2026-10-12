# Build context: thư mục gốc dự án (chứa BMS.sln)
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src
COPY BMS.Shared/BMS.Shared.csproj BMS.Shared/
COPY BMS.API/BMS.API.csproj BMS.API/
RUN dotnet restore BMS.API/BMS.API.csproj
COPY BMS.Shared/ BMS.Shared/
COPY BMS.API/ BMS.API/
RUN dotnet publish BMS.API/BMS.API.csproj -c Release -o /app/publish --no-restore

FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS final
WORKDIR /app
COPY --from=build /app/publish .
# Database demo: chỉ chép vào /data/bms.db ở lần chạy đầu (khi volume chưa có file).
COPY deploy/seed/bms-demo.db /app/seed/bms.db
COPY deploy/api-entrypoint.sh /app/api-entrypoint.sh
# sed bỏ ký tự CRLF phòng khi file được copy/sửa trên Windows.
RUN sed -i 's/\r$//' /app/api-entrypoint.sh && chmod +x /app/api-entrypoint.sh && mkdir -p /data/uploads
ENV Storage__DataDirectory=/data
VOLUME /data
EXPOSE 8080
ENTRYPOINT ["/app/api-entrypoint.sh"]
