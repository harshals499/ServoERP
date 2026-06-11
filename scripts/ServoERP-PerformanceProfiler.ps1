$ErrorActionPreference = 'Continue'
$root = 'C:\HVAC_PRO_MSE'
$logDir = Join-Path $root 'LOGS'
$log = Join-Path $logDir 'performance-profiler.log'
New-Item -ItemType Directory -Force -Path $logDir | Out-Null
function L($m) { Add-Content -LiteralPath $log -Value "[$(Get-Date -Format 'yyyy-MM-dd HH:mm:ss')] $m" -Encoding UTF8 }
L '[RUN] Performance profiler started'
$largeForms = Get-ChildItem -Path (Join-Path $root 'SOURCE_CODE\UI') -Filter '*.cs' -ErrorAction SilentlyContinue | Where-Object { $_.Length -gt 200KB }
foreach ($form in $largeForms) { L "query review candidate: $($form.Name)" }
L 'improved 0 paths automatically; profile report only.'
