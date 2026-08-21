param(
    [Parameter(Mandatory=$true)][string]$SqlServer,
    [string]$Database = 'HVAC_PRO',
    [string]$BackupDirectory = 'C:\ServoERP\Backups',
    [ValidateRange(7,3650)][int]$KeepDays = 30,
    [ValidatePattern('^([01]\d|2[0-3]):[0-5]\d$')][string]$DailyAt = '21:00',
    [string]$TaskName = 'ServoERP - Verified SQL Backup'
)
$ErrorActionPreference = 'Stop'
$scriptPath = Join-Path $PSScriptRoot 'Backup-ServoERPDatabase.ps1'
if (-not (Test-Path -LiteralPath $scriptPath)) { throw "Backup script not found: $scriptPath" }
$arguments = "-NoProfile -ExecutionPolicy Bypass -File `"$scriptPath`" -SqlServer `"$SqlServer`" -Database `"$Database`" -BackupDirectory `"$BackupDirectory`" -KeepDays $KeepDays"
$action = New-ScheduledTaskAction -Execute 'powershell.exe' -Argument $arguments
$trigger = New-ScheduledTaskTrigger -Daily -At ([DateTime]::ParseExact($DailyAt, 'HH:mm', [Globalization.CultureInfo]::InvariantCulture))
$settings = New-ScheduledTaskSettingsSet -StartWhenAvailable -ExecutionTimeLimit (New-TimeSpan -Hours 4) -RestartCount 2 -RestartInterval (New-TimeSpan -Minutes 15)
$principal = New-ScheduledTaskPrincipal -UserId 'SYSTEM' -LogonType ServiceAccount -RunLevel Highest
Register-ScheduledTask -TaskName $TaskName -Action $action -Trigger $trigger -Settings $settings -Principal $principal -Force | Out-Null
$task = Get-ScheduledTask -TaskName $TaskName
Write-Output "PASS: $($task.TaskName) runs daily at $DailyAt as SYSTEM. Backup retention: $KeepDays days."
