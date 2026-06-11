$ErrorActionPreference = 'Continue'
$root = 'C:\HVAC_PRO_MSE'
$logDir = Join-Path $root 'LOGS'
$log = Join-Path $logDir 'dead-code-cleaner.log'
New-Item -ItemType Directory -Force -Path $logDir | Out-Null
function L($m) { Add-Content -LiteralPath $log -Value "[$(Get-Date -Format 'yyyy-MM-dd HH:mm:ss')] $m" -Encoding UTF8 }
L '[RUN] Dead code cleaner audit started'
L 'No files removed automatically; destructive cleanup requires manual review.'
Push-Location $root
dotnet build .\SOURCE_CODE\HVAC_Pro_Desktop.sln -c Release | ForEach-Object { L $_ }
if ($LASTEXITCODE -eq 0) { L '[BUILD: PASS] Build passed after dead-code audit.' } else { L '[BUILD: FAIL] Build failed during dead-code audit.' }
Pop-Location
