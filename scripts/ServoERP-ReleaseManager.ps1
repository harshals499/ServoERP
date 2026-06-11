$ErrorActionPreference = 'Continue'
$root = 'C:\HVAC_PRO_MSE'
$logDir = Join-Path $root 'LOGS'
$log = Join-Path $logDir 'release-manager.log'
New-Item -ItemType Directory -Force -Path $logDir | Out-Null
function L($m) { Add-Content -LiteralPath $log -Value "[$(Get-Date -Format 'yyyy-MM-dd HH:mm:ss')] $m" -Encoding UTF8 }
$version = 'unknown'
$versionPath = Join-Path $root 'VERSION'
if (Test-Path -LiteralPath $versionPath) { $version = (Get-Content -LiteralPath $versionPath -Raw).Trim() }
L "[DEPLOY] [$version] Release manager restored and ready."
L 'nightly pass: dashboard release agent is available.'
