# Đóng gói BMS bản Windows "bấm đúp là chạy" (không cần cài .NET, Visual Studio hay Docker).
# Chạy trên máy lập trình (cần .NET 10 SDK):
#   powershell -ExecutionPolicy Bypass -File D:\CNPMNC\deploy\build-windows.ps1
# Kết quả: D:\CNPMNC\dist\BMS-Windows (thư mục chạy được) + D:\CNPMNC\dist\BMS-Windows.zip (để gửi/bán).
$ErrorActionPreference = 'Stop'
$root = Split-Path -Parent $PSScriptRoot
$out = Join-Path $root 'dist\BMS-Windows'
$rid = 'win-x64'

if (Test-Path $out) { Remove-Item -Recurse -Force $out }
New-Item -ItemType Directory -Force $out | Out-Null

Write-Host '1/4 BMS.API ...'
dotnet publish "$root\BMS.API\BMS.API.csproj" -c Release -r $rid --self-contained true -p:PublishSingleFile=false -o "$out\api" --nologo -v q
if ($LASTEXITCODE -ne 0) { throw 'Publish API that bai' }

Write-Host '2/4 BMS.AdminStaffWeb ...'
dotnet publish "$root\BMS.AdminStaffWeb\BMS.AdminStaffWeb.csproj" -c Release -r $rid --self-contained true -o "$out\admin" --nologo -v q
if ($LASTEXITCODE -ne 0) { throw 'Publish Admin that bai' }

Write-Host '3/4 BMS.PlayerWeb ...'
dotnet publish "$root\BMS.PlayerWeb\BMS.PlayerWeb.csproj" -c Release -o "$out\player" --nologo -v q
if ($LASTEXITCODE -ne 0) { throw 'Publish PlayerWeb that bai' }

Write-Host '4/4 BMS.exe (trinh khoi dong) ...'
dotnet publish "$root\BMS.Launcher\BMS.Launcher.csproj" -c Release -r $rid --self-contained true -p:PublishSingleFile=true -p:IncludeNativeLibrariesForSelfExtract=true -o "$out" --nologo -v q
if ($LASTEXITCODE -ne 0) { throw 'Publish Launcher that bai' }

New-Item -ItemType Directory -Force "$out\seed" | Out-Null
Copy-Item "$root\deploy\seed\bms-demo.db" "$out\seed\bms.db"
Copy-Item "$root\deploy\HUONG-DAN-SU-DUNG.txt" "$out\HUONG-DAN-SU-DUNG.txt"
# Không đóng gói dữ liệu/cấu hình riêng của máy lập trình.
Get-ChildItem $out -Recurse -Include 'bms.db','bms.db-*','appsettings.Development.json','*.pdb' -File |
    Where-Object { $_.DirectoryName -ne "$out\seed" } | Remove-Item -Force
Remove-Item -Recurse -Force "$out\api\wwwroot\uploads" -ErrorAction SilentlyContinue
Remove-Item -Force "$out\web.config", "$out\BMS.staticwebassets.endpoints.json" -ErrorAction SilentlyContinue

$zip = Join-Path $root 'dist\BMS-Windows.zip'
if (Test-Path $zip) { Remove-Item -Force $zip }
# tar.exe (có sẵn từ Windows 10) tạo zip chuẩn; Compress-Archive của PowerShell 5 ghi dấu \ trong đường dẫn.
& "$env:SystemRoot\System32\tar.exe" -a -c -f $zip -C $out @(Get-ChildItem $out -Name)
if ($LASTEXITCODE -ne 0) { throw 'Nen zip that bai' }
$size = [math]::Round((Get-ChildItem $out -Recurse -File | Measure-Object Length -Sum).Sum / 1MB)
Write-Host "Xong: $out ($size MB), file nen: $zip"
