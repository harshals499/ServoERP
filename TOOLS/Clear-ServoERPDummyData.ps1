param(
    [string]$Server = "localhost\SQLEXPRESS",
    [string]$Database = "HVAC_PRO",
    [switch]$SkipBackup
)

$ErrorActionPreference = "Stop"

$root = "C:\HVAC_PRO_MSE"
$backupDir = Join-Path $root "DATABASE\Backups"
$logDir = Join-Path $root "LOGS"
New-Item -ItemType Directory -Force -Path $backupDir, $logDir | Out-Null

$timestamp = Get-Date -Format "yyyyMMdd_HHmmss"
$backupPath = Join-Path $backupDir "HVAC_PRO_before_dummy_cleanup_$timestamp.bak"
$logPath = Join-Path $logDir "dummy-data-cleanup-$timestamp.log"
$connectionString = "Server=$Server;Database=$Database;Integrated Security=True;TrustServerCertificate=True;Connection Timeout=15;"

function Invoke-Sql {
    param(
        [string]$Sql,
        [string]$ConnectionString = $script:connectionString,
        [int]$Timeout = 300
    )

    $connection = New-Object System.Data.SqlClient.SqlConnection $ConnectionString
    $command = $connection.CreateCommand()
    $command.CommandText = $Sql
    $command.CommandTimeout = $Timeout
    $adapter = New-Object System.Data.SqlClient.SqlDataAdapter $command
    $table = New-Object System.Data.DataTable
    try {
        $connection.Open()
        [void]$adapter.Fill($table)
        return $table
    }
    finally {
        $connection.Close()
        $connection.Dispose()
    }
}

function Write-Log {
    param([string]$Message)
    $line = "$(Get-Date -Format s) $Message"
    Add-Content -Path $logPath -Value $line
    Write-Host $line
}

Write-Log "Starting ServoERP dummy/business data cleanup for $Server / $Database"

$machineConfig = Join-Path $root "HVACPro.config"
if (Test-Path $machineConfig) {
    [xml]$config = Get-Content $machineConfig
    $rootNode = $config.SelectSingleNode("/HVACProConfig")
    if (-not $rootNode) {
        $rootNode = $config.CreateElement("HVACProConfig")
        [void]$config.AppendChild($rootNode)
    }
    $tenantNode = $config.SelectSingleNode("/HVACProConfig/Tenant")
    if (-not $tenantNode) {
        $tenant = $config.CreateElement("Tenant")
        [void]$rootNode.AppendChild($tenant)
        $tenantNode = $tenant
    }
    $seedNode = $config.SelectSingleNode("/HVACProConfig/Tenant/SeedDemoData")
    if (-not $seedNode) {
        $node = $config.CreateElement("SeedDemoData")
        [void]$tenantNode.AppendChild($node)
        $seedNode = $node
    }
    $seedNode.InnerText = "false"
    $config.Save($machineConfig)
    Write-Log "Confirmed SeedDemoData=false in $machineConfig"
}

$beforeSql = @"
SELECT t.name AS TableName, SUM(p.rows) AS [RowCount]
FROM sys.tables t
JOIN sys.partitions p ON p.object_id = t.object_id AND p.index_id IN (0,1)
GROUP BY t.name
ORDER BY t.name;
"@
$before = Invoke-Sql $beforeSql
$before | Export-Csv -NoTypeInformation -Path (Join-Path $logDir "dummy-data-cleanup-before-$timestamp.csv")

if (-not $SkipBackup) {
    $masterConnection = "Server=$Server;Database=master;Integrated Security=True;TrustServerCertificate=True;Connection Timeout=15;"
    $escapedBackupPath = $backupPath.Replace("'", "''")
    Invoke-Sql -ConnectionString $masterConnection -Timeout 900 -Sql "BACKUP DATABASE [$Database] TO DISK = N'$escapedBackupPath' WITH INIT, COPY_ONLY;"
    Write-Log "Backup created: $backupPath"
}

$cleanupSql = @"
SET NOCOUNT ON;

DECLARE @Keep TABLE (TableName SYSNAME PRIMARY KEY);
INSERT INTO @Keep (TableName) VALUES
(N'AppUsers'),
(N'AppRoles'),
(N'RolePermissions'),
(N'CompanySettings'),
(N'HsnSacMaster'),
(N'JobChecklistTemplates'),
(N'InvoiceTemplates'),
(N'QuoteTemplates'),
(N'QuoteTemplateItems'),
(N'LeaveTypes'),
(N'ProfessionalTaxSlabs'),
(N'LicenseState'),
(N'LicenseEvents'),
(N'ActivatedDevices'),
(N'FeatureEntitlements');

DECLARE @CountsBefore TABLE (TableName SYSNAME PRIMARY KEY, [RowCount] BIGINT);
INSERT INTO @CountsBefore (TableName, [RowCount])
SELECT t.name, SUM(p.rows)
FROM sys.tables t
JOIN sys.partitions p ON p.object_id = t.object_id AND p.index_id IN (0,1)
WHERE SCHEMA_NAME(t.schema_id) = N'dbo'
GROUP BY t.name;

DECLARE @sql NVARCHAR(MAX) = N'';
SELECT @sql = @sql + N'ALTER TABLE ' + QUOTENAME(SCHEMA_NAME(schema_id)) + N'.' + QUOTENAME(name) + N' NOCHECK CONSTRAINT ALL;' + CHAR(13)
FROM sys.tables;
EXEC sp_executesql @sql;

SET @sql = N'';
SELECT @sql = @sql + N'DELETE FROM ' + QUOTENAME(SCHEMA_NAME(t.schema_id)) + N'.' + QUOTENAME(t.name) + N';' + CHAR(13)
FROM sys.tables t
LEFT JOIN @Keep k ON k.TableName = t.name
WHERE SCHEMA_NAME(t.schema_id) = N'dbo'
  AND k.TableName IS NULL
ORDER BY t.name;
EXEC sp_executesql @sql;

SET @sql = N'';
SELECT @sql = @sql + N'DBCC CHECKIDENT (''' + QUOTENAME(SCHEMA_NAME(t.schema_id)) + N'.' + QUOTENAME(t.name) + N''', RESEED, 0) WITH NO_INFOMSGS;' + CHAR(13)
FROM sys.tables t
LEFT JOIN @Keep k ON k.TableName = t.name
WHERE SCHEMA_NAME(t.schema_id) = N'dbo'
  AND k.TableName IS NULL
  AND EXISTS (SELECT 1 FROM sys.identity_columns ic WHERE ic.object_id = t.object_id);
EXEC sp_executesql @sql;

SET @sql = N'';
SELECT @sql = @sql + N'ALTER TABLE ' + QUOTENAME(SCHEMA_NAME(schema_id)) + N'.' + QUOTENAME(name) + N' WITH CHECK CHECK CONSTRAINT ALL;' + CHAR(13)
FROM sys.tables;
EXEC sp_executesql @sql;

SELECT b.TableName, b.[RowCount] AS RowsBefore, SUM(p.rows) AS RowsAfter, b.[RowCount] - SUM(p.rows) AS RowsRemoved
FROM @CountsBefore b
JOIN sys.tables t ON t.name = b.TableName
JOIN sys.partitions p ON p.object_id = t.object_id AND p.index_id IN (0,1)
GROUP BY b.TableName, b.[RowCount]
ORDER BY b.TableName;
"@

$after = Invoke-Sql $cleanupSql -Timeout 900
$afterCsv = Join-Path $logDir "dummy-data-cleanup-after-$timestamp.csv"
$after | Export-Csv -NoTypeInformation -Path $afterCsv

Write-Log "Cleanup complete. Report: $afterCsv"
Write-Log "Preserved security, license, settings, lookup, and template tables."

$after | Where-Object { [int64]$_.RowsRemoved -gt 0 } | Sort-Object TableName | Format-Table -AutoSize
