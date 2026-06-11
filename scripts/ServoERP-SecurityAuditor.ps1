$ErrorActionPreference = 'Continue'
$root = 'C:\HVAC_PRO_MSE'
$logDir = Join-Path $root 'LOGS'
$log = Join-Path $logDir 'security-auditor.log'
New-Item -ItemType Directory -Force -Path $logDir | Out-Null
function L($m) { Add-Content -LiteralPath $log -Value "[$(Get-Date -Format 'yyyy-MM-dd HH:mm:ss')] $m" -Encoding UTF8 }
L '[RUN] Security auditor started'
$findings = Select-String -Path (Join-Path $root 'SOURCE_CODE\**\*.cs') -Pattern 'password\s*=|secret|api[_-]?key' -ErrorAction SilentlyContinue
if ($findings) {
    L "warn medium: review $($findings.Count) potential secret/password references."
} else {
    L 'No high or medium security findings in basic source scan.'
}
