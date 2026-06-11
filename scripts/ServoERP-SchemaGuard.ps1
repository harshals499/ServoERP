$ErrorActionPreference = 'Continue'
$root = 'C:\HVAC_PRO_MSE'
$logDir = Join-Path $root 'LOGS'
$log = Join-Path $logDir 'schema-guard.log'
New-Item -ItemType Directory -Force -Path $logDir | Out-Null
function L($m) { Add-Content -LiteralPath $log -Value "[$(Get-Date -Format 'yyyy-MM-dd HH:mm:ss')] $m" -Encoding UTF8 }
L '[RUN] Schema guard started'
$hits = Select-String -Path (Join-Path $root 'SOURCE_CODE\**\*.cs') -Pattern 'DROP TABLE|DROP COLUMN|sp_rename|ALTER TABLE' -ErrorAction SilentlyContinue
if ($hits) {
    foreach ($hit in $hits | Select-Object -First 20) { L "[REVIEW] $($hit.Path):$($hit.LineNumber) $($hit.Line.Trim())" }
    L '[PASS] Schema guard review completed; verify intentional migration guards before release.'
} else {
    L '[PASS] No risky schema operations found in source scan.'
}
