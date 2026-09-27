$Project = "C:\HVAC_PRO_MSE"

Clear-Host

Write-Host "==========================================" -ForegroundColor Cyan
Write-Host " ServoERP Engineer Launcher" -ForegroundColor Cyan
Write-Host "==========================================" -ForegroundColor Cyan

if (!(Test-Path $Project)) {
    Write-Host "ERROR: Project folder not found: $Project" -ForegroundColor Red
    exit 1
}

Set-Location $Project

$Codex = Get-Command codex.cmd -ErrorAction SilentlyContinue

if (!$Codex) {
    $Codex = Get-Command codex -ErrorAction SilentlyContinue
}

if (!$Codex) {
    Write-Host ""
    Write-Host "ERROR: Codex was not found in PATH." -ForegroundColor Red
    Write-Host "Run: where.exe codex" -ForegroundColor Yellow
    exit 1
}

Write-Host ""
Write-Host "Project: $Project" -ForegroundColor Green
Write-Host "Codex:   $($Codex.Source)" -ForegroundColor Green
Write-Host ""
Write-Host "Starting ServoERP engineering agent..." -ForegroundColor Cyan
Write-Host ""

& $Codex.Source
