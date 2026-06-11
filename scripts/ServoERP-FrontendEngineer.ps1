$ErrorActionPreference = 'Continue'
$root = 'C:\HVAC_PRO_MSE'
$logDir = Join-Path $root 'LOGS'
$log = Join-Path $logDir 'frontend-engineer.log'
New-Item -ItemType Directory -Force -Path $logDir | Out-Null
function L($m) { Add-Content -LiteralPath $log -Value "[$(Get-Date -Format 'yyyy-MM-dd HH:mm:ss')] $m" -Encoding UTF8 }
L '[RUN] Frontend engineer started'
Push-Location $root
dotnet build .\SOURCE_CODE\HVAC_Pro_Desktop.sln -c Release | ForEach-Object { L $_ }
if ($LASTEXITCODE -eq 0) { L '[BUILD: PASS] Release build completed.' } else { L '[BUILD: FAIL] Release build failed.' }
Pop-Location
