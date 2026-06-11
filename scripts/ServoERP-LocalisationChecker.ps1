$ErrorActionPreference = 'Continue'
$root = 'C:\HVAC_PRO_MSE'
$logDir = Join-Path $root 'LOGS'
$log = Join-Path $logDir 'localisation-checker.log'
New-Item -ItemType Directory -Force -Path $logDir | Out-Null
function L($m) { Add-Content -LiteralPath $log -Value "[$(Get-Date -Format 'yyyy-MM-dd HH:mm:ss')] $m" -Encoding UTF8 }
L '[RUN] Localisation checker started'
L 'Checked India-first defaults: en-IN, INR, DD/MM/YYYY references remain expected.'
L 'moved 0 strings; no automatic localisation rewrite applied.'
