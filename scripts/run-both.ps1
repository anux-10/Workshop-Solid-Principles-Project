# Starts both versions side by side.
#   V1 (bad)  -> http://localhost:5081
#   V2 (good) -> http://localhost:5082

$root = Split-Path -Parent $PSScriptRoot

Write-Host "Starting V1 (intentionally bad) on http://localhost:5081 ..." -ForegroundColor Red
Start-Process dotnet -ArgumentList "run --project `"$root\src\OrderManagement.V1.Bad`"" -NoNewWindow

Write-Host "Starting V2 (fixed) on http://localhost:5082 ..." -ForegroundColor Green
Start-Process dotnet -ArgumentList "run --project `"$root\src\OrderManagement.V2.Good`"" -NoNewWindow

Write-Host ""
Write-Host "Both applications are starting." -ForegroundColor Cyan
Write-Host "  V1: http://localhost:5081"
Write-Host "  V2: http://localhost:5082"
Write-Host ""
Write-Host "Close this window (or press Ctrl+C) to stop them."
