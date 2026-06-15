# Chạy auto-wire MP khi Unity Editor đang ĐÓNG project này.
# Tương đương menu: ProjectRTS/Netplay/★ Auto-Wire MP Prefabs & Scene (one-click)

$ErrorActionPreference = "Stop"
$projectPath = Split-Path $PSScriptRoot -Parent
$unityExe = "C:\Program Files\Unity\Hub\Editor\6000.3.6f1\Editor\Unity.exe"
$logDir = Join-Path $projectPath "Logs"
$logFile = Join-Path $logDir "mp-auto-setup.log"

if (-not (Test-Path $unityExe)) {
    Write-Error "Không tìm thấy Unity: $unityExe — sửa đường dẫn trong script."
}

if (-not (Test-Path $logDir)) {
    New-Item -ItemType Directory -Path $logDir | Out-Null
}

Write-Host "Đang chạy MP auto-setup (đóng Unity trước nếu project đang mở)..."
& $unityExe `
    -batchmode `
    -nographics `
    -quit `
    -projectPath $projectPath `
    -executeMethod GameDevTV.RTS.Editor.Netplay.RtsMpNetworkAutoSetupEditor.ExecuteBatch `
    -logFile $logFile

if ($LASTEXITCODE -ne 0) {
    Write-Error "Unity batch thất bại (exit $LASTEXITCODE). Xem log: $logFile"
}

Write-Host "Xong. Log: $logFile"
