$ErrorActionPreference = 'Continue'

$root = 'C:\HVAC_PRO_MSE'
$logDir = Join-Path $root 'LOGS'
$log = Join-Path $logDir 'codex-source-watcher.log'
New-Item -ItemType Directory -Force -Path $logDir | Out-Null

function Write-AgentLog {
    param([string]$Message)
    $stamp = Get-Date -Format 'yyyy-MM-dd HH:mm:ss'
    Add-Content -LiteralPath $log -Value "[$stamp] $Message" -Encoding UTF8
}

Write-AgentLog 'Watcher started'
Write-AgentLog "Watching $root\SOURCE_CODE"

$watchPath = Join-Path $root 'SOURCE_CODE'
if (-not (Test-Path -LiteralPath $watchPath)) {
    Write-AgentLog "Codex run failed: source path missing: $watchPath"
    exit 1
}

$watcher = New-Object System.IO.FileSystemWatcher
$watcher.Path = $watchPath
$watcher.IncludeSubdirectories = $true
$watcher.Filter = '*.*'
$watcher.NotifyFilter = [System.IO.NotifyFilters]'FileName, LastWrite, DirectoryName'
$watcher.EnableRaisingEvents = $true

$action = {
    $path = $Event.SourceEventArgs.FullPath
    if ($path -match '\\bin\\|\\obj\\|\.vs\\|\.git\\') {
        return
    }
    $stamp = Get-Date -Format 'yyyy-MM-dd HH:mm:ss'
    Add-Content -LiteralPath $using:log -Value "[$stamp] Triggered by: $path" -Encoding UTF8
}

Register-ObjectEvent -InputObject $watcher -EventName Changed -SourceIdentifier ServoERPSourceChanged -Action $action | Out-Null
Register-ObjectEvent -InputObject $watcher -EventName Created -SourceIdentifier ServoERPSourceCreated -Action $action | Out-Null
Register-ObjectEvent -InputObject $watcher -EventName Renamed -SourceIdentifier ServoERPSourceRenamed -Action $action | Out-Null

while ($true) {
    Start-Sleep -Seconds 30
}
