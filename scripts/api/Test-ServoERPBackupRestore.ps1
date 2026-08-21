param(
    [Parameter(Mandatory=$true)][string]$SqlServer,
    [Parameter(Mandatory=$true)][string]$BackupPath,
    [string]$ValidationDatabase = ('ServoERP_RestoreValidation_' + (Get-Date -Format 'yyyyMMddHHmmss')),
    [string]$RestoreDirectory,
    [string]$ReportPath = 'C:\ServoERP\Operations\restore-validation.json',
    [switch]$KeepValidationDatabase
)
$ErrorActionPreference = 'Stop'
if (-not (Test-Path -LiteralPath $BackupPath)) { throw "Backup file not found: $BackupPath" }
if ($ValidationDatabase -notmatch '^ServoERP_RestoreValidation_[A-Za-z0-9_]+$') { throw 'ValidationDatabase must use the ServoERP_RestoreValidation_ prefix.' }
function Invoke-Sql([string]$Query) { & sqlcmd -S $SqlServer -E -d master -b -W -h -1 -Q $Query; if ($LASTEXITCODE -ne 0) { throw 'sqlcmd command failed.' } }
if ([string]::IsNullOrWhiteSpace($RestoreDirectory)) {
    $RestoreDirectory = (& sqlcmd -S $SqlServer -E -d master -b -W -h -1 -Q "SET NOCOUNT ON; SELECT CONVERT(nvarchar(4000), SERVERPROPERTY('InstanceDefaultDataPath'));") | Select-Object -First 1
    if ($LASTEXITCODE -ne 0 -or [string]::IsNullOrWhiteSpace($RestoreDirectory)) { throw 'Could not determine SQL Server default data directory.' }
    $RestoreDirectory = $RestoreDirectory.Trim()
}
New-Item -ItemType Directory -Force -Path $RestoreDirectory,(Split-Path -Parent $ReportPath) | Out-Null
$restoreDirectoryInfo = Get-Item -LiteralPath $RestoreDirectory
if (($restoreDirectoryInfo.Attributes -band [IO.FileAttributes]::Compressed) -ne 0) { throw "Restore directory is NTFS-compressed and cannot hold writable SQL files: $RestoreDirectory" }
$escapedBackup = $BackupPath.Replace("'", "''")
$escapedDb = $ValidationDatabase.Replace(']', ']]')
$fileList = & sqlcmd -S $SqlServer -E -d master -b -W -h -1 -s '|' -Q "SET NOCOUNT ON; RESTORE FILELISTONLY FROM DISK=N'$escapedBackup';"
if ($LASTEXITCODE -ne 0 -or -not $fileList) { throw 'Could not read SQL backup file list.' }
$moves = @(); $dataIndex = 0
foreach ($row in $fileList) {
    $columns = $row -split '\|'
    if ($columns.Count -lt 3 -or [string]::IsNullOrWhiteSpace($columns[0])) { continue }
    $logical = $columns[0].Trim().Replace("'", "''")
    $type = $columns[2].Trim()
    $extension = if ($type -eq 'L') { '.ldf' } elseif ($dataIndex++ -eq 0) { '.mdf' } else { '.ndf' }
    $target = Join-Path $RestoreDirectory ($ValidationDatabase + '_' + $dataIndex + $extension)
    $moves += "MOVE N'$logical' TO N'$($target.Replace("'", "''"))'"
}
if ($moves.Count -eq 0) { throw 'No database files were discovered in the backup.' }
try {
    Invoke-Sql "IF DB_ID(N'$escapedDb') IS NOT NULL ALTER DATABASE [$escapedDb] SET SINGLE_USER WITH ROLLBACK IMMEDIATE; IF DB_ID(N'$escapedDb') IS NOT NULL DROP DATABASE [$escapedDb];"
    Invoke-Sql "RESTORE DATABASE [$escapedDb] FROM DISK=N'$escapedBackup' WITH REPLACE, $($moves -join ', '), STATS=10;"
    $checks = & sqlcmd -S $SqlServer -E -d $ValidationDatabase -b -W -h -1 -s '|' -Q "SET NOCOUNT ON; DBCC CHECKDB (N'$escapedDb') WITH NO_INFOMSGS; SELECT DB_NAME(), (SELECT COUNT(*) FROM sys.tables), (SELECT COUNT(*) FROM Companies), (SELECT COUNT(*) FROM Invoices), (SELECT COUNT(*) FROM StockItems);"
    if ($LASTEXITCODE -ne 0) { throw 'DBCC CHECKDB or required-table validation failed.' }
    $report = [ordered]@{ validatedAt=(Get-Date).ToUniversalTime().ToString('o'); sqlServer=$SqlServer; backupPath=$BackupPath; validationDatabase=$ValidationDatabase; result='PASS'; output=@($checks) }
    $report | ConvertTo-Json -Depth 4 | Set-Content -LiteralPath $ReportPath -Encoding UTF8
    Write-Output "PASS: restored and validated $ValidationDatabase. Report: $ReportPath"
}
finally {
    if (-not $KeepValidationDatabase) {
        try { Invoke-Sql "IF DB_ID(N'$escapedDb') IS NOT NULL ALTER DATABASE [$escapedDb] SET SINGLE_USER WITH ROLLBACK IMMEDIATE; IF DB_ID(N'$escapedDb') IS NOT NULL DROP DATABASE [$escapedDb];" } catch { Write-Warning "Validation database cleanup failed: $ValidationDatabase" }
    }
}
