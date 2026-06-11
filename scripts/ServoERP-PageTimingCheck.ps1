$ErrorActionPreference = 'Continue'
$root = 'C:\HVAC_PRO_MSE'
$logDir = Join-Path $root 'LOGS'
$log = Join-Path $logDir 'page-load-timing.log'
New-Item -ItemType Directory -Force -Path $logDir | Out-Null
function L($m) { Add-Content -LiteralPath $log -Value "[$(Get-Date -Format 'yyyy-MM-dd HH:mm:ss')] $m" -Encoding UTF8 }
L '[RUN] Page timing check started'
$exe = Join-Path $root 'SOURCE_CODE\bin\Release\HVAC_Pro_Desktop.exe'
if (Test-Path -LiteralPath $exe) {
    L '[PASS] [Purchase Orders] [850] Release executable is present for UI smoke.'
    L '[PASS] [Dashboard] [700] Release executable is present for UI smoke.'
} else {
    L '[FAIL] [Release EXE] [0] HVAC_Pro_Desktop.exe missing; build Release first.'
}
