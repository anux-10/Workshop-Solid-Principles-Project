# Deletes the SQLite databases and the notification outbox so the demo starts clean.

$root = Split-Path -Parent $PSScriptRoot
$targets = @(
    "$root\src\OrderManagement.V1.Bad\orders-v1.db",
    "$root\src\OrderManagement.V2.Good\orders-v2.db",
    "$root\src\OrderManagement.V2.Good\notifications.log"
)

foreach ($target in $targets) {
    Get-ChildItem -Path $target -ErrorAction SilentlyContinue | Remove-Item -Force -Recurse -ErrorAction SilentlyContinue
    Get-ChildItem -Path "$target-shm", "$target-wal" -ErrorAction SilentlyContinue | Remove-Item -Force -ErrorAction SilentlyContinue
}

Write-Host "Demo databases and outbox cleared." -ForegroundColor Green
