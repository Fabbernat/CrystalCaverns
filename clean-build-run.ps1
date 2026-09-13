$ErrorActionPreference = "Stop"

Write-Host "=== Cleaning ===" -ForegroundColor Cyan
dotnet clean

Write-Host "=== Removing bin/obj ===" -ForegroundColor Cyan
Remove-Item -Recurse -Force bin, obj -ErrorAction SilentlyContinue

Write-Host "=== Building ===" -ForegroundColor Cyan
dotnet build

Write-Host "=== Running ===" -ForegroundColor Cyan
dotnet run