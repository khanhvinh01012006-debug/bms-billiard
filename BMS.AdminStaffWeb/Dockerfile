# Build context: thư mục gốc dự án (chứa BMS.sln)
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src
# QUAN TRỌNG: chép đủ mã nguồn (gồm các file .razor) TRƯỚC khi restore. SDK chỉ kéo gói
# Microsoft.AspNetCore.App.Internal.Assets (chứa _framework/blazor.web.js) khi lúc restore
# thấy có file .razor. Restore chỉ với .csproj rồi publish --no-restore sẽ ra bản thiếu
# blazor.web.js -> trang không tương tác, bấm "Đăng nhập" bị POST thường -> HTTP 400.
COPY BMS.Shared/ BMS.Shared/
COPY BMS.AdminStaffWeb/ BMS.AdminStaffWeb/
RUN dotnet publish BMS.AdminStaffWeb/BMS.AdminStaffWeb.csproj -c Release -o /app/publish

FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS final
WORKDIR /app
COPY --from=build /app/publish .
EXPOSE 8080
ENTRYPOINT ["dotnet", "BMS.AdminStaffWeb.dll"]
