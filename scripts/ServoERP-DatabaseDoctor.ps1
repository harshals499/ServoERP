$ErrorActionPreference = 'Continue'
$root = 'C:\HVAC_PRO_MSE'
$logDir = Join-Path $root 'LOGS'
$log = Join-Path $logDir 'database-doctor.log'
New-Item -ItemType Directory -Force -Path $logDir | Out-Null
function L($m) { Add-Content -LiteralPath $log -Value "[$(Get-Date -Format 'yyyy-MM-dd HH:mm:ss')] $m" -Encoding UTF8 }
L '[RUN] Database doctor started'
$dbScripts = Get-ChildItem -Path (Join-Path $root 'DATABASE') -File -ErrorAction SilentlyContinue
L "Checked database folder; files found: $($dbScripts.Count)"
L 'fixed 0 issues; no automatic database changes were applied.'
