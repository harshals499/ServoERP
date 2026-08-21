param(
    [string]$ApiUrl = 'https://WIN-S0OB0IQUCPR:7443',
    [string]$ServiceName = 'ServoERP.Api',
    [string]$BackupDirectory = 'C:\ServoERP\Backups',
    [string]$CertificateThumbprint,
    [int]$BackupWarningHours = 30,
    [string]$OutputPath = 'C:\ServoERP\Operations\health.json'
)
$ErrorActionPreference = 'Stop'
$now = Get-Date
$service = Get-Service $ServiceName -ErrorAction SilentlyContinue
$backup = Get-ChildItem -LiteralPath $BackupDirectory -Filter '*.bak' -ErrorAction SilentlyContinue | Sort-Object LastWriteTimeUtc -Descending | Select-Object -First 1
$certificate = if ($CertificateThumbprint) { Get-ChildItem "Cert:\LocalMachine\My\$CertificateThumbprint" -ErrorAction SilentlyContinue } else { $null }
$watch = [System.Diagnostics.Stopwatch]::StartNew(); $api = $null; $apiError = $null
try { $api = Invoke-RestMethod ($ApiUrl.TrimEnd('/') + '/health') -TimeoutSec 10 } catch { $apiError = $_.Exception.Message }; $watch.Stop()
$drive = Get-Item -LiteralPath ($BackupDirectory.Substring(0, 3)) -ErrorAction SilentlyContinue
$result = [ordered]@{
    checkedAt = $now.ToUniversalTime().ToString('o'); service = if($service){$service.Status.ToString()}else{'NotInstalled'}; apiReachable = ($null -ne $api); apiLatencyMs = $watch.ElapsedMilliseconds; apiError = $apiError
    backupPath = if($backup){$backup.FullName}else{$null}; backupAgeHours = if($backup){[math]::Round(($now.ToUniversalTime()-$backup.LastWriteTimeUtc).TotalHours,2)}else{$null}; backupWarning = (-not $backup -or (($now.ToUniversalTime()-$backup.LastWriteTimeUtc).TotalHours -gt $BackupWarningHours))
    certificateExpires = if($certificate){$certificate.NotAfter.ToUniversalTime().ToString('o')}else{$null}; certificateWarning = if($certificate){$certificate.NotAfter -lt $now.AddDays(30)}else{$true}
    backupDriveFreeBytes = if($drive){$drive.PSDrive.Free}else{$null}; backupDriveUsedBytes = if($drive){$drive.PSDrive.Used}else{$null}
}
New-Item -ItemType Directory -Force -Path (Split-Path -Parent $OutputPath) | Out-Null
$result | ConvertTo-Json | Set-Content -LiteralPath $OutputPath -Encoding UTF8
$result | ConvertTo-Json
