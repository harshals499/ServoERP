param(
    [Parameter(Mandatory=$true)][string]$SqlServer,
    [string]$Database = 'HVAC_PRO',
    [string]$BackupDirectory = 'C:\ServoERP\Backups',
    [int]$KeepDays = 14
)
$ErrorActionPreference='Stop'
New-Item -ItemType Directory -Force -Path $BackupDirectory | Out-Null
$stamp=Get-Date -Format 'yyyyMMdd_HHmmss'
$path=Join-Path $BackupDirectory ("$Database`_$stamp.bak")
$escaped=$path.Replace("'","''")
$sql="BACKUP DATABASE [$Database] TO DISK=N'$escaped' WITH COPY_ONLY,CHECKSUM,STATS=10; RESTORE VERIFYONLY FROM DISK=N'$escaped' WITH CHECKSUM;"
& sqlcmd -S $SqlServer -E -d master -b -Q $sql
if($LASTEXITCODE -ne 0){throw 'Backup or verification failed.'}
$verified=Get-Item -LiteralPath $path
# Retention is deliberately conservative: preserve the newest verified backup even when every
# other backup exceeds retention. This avoids deleting the only known recoverable copy.
$backups = @(Get-ChildItem -LiteralPath $BackupDirectory -Filter "$Database`_*.bak" | Sort-Object LastWriteTimeUtc -Descending)
$retentionCutoff = (Get-Date).ToUniversalTime().AddDays(-$KeepDays)
$backups | Select-Object -Skip 1 | Where-Object { $_.LastWriteTimeUtc -lt $retentionCutoff } | Remove-Item -Force
if(!(Test-Path -LiteralPath $verified.FullName)){throw 'Verified backup file is missing.'}
Write-Output $verified.FullName
