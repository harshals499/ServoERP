$ErrorActionPreference = 'Stop'

$sqlcmdCandidates = @(
    'C:\Program Files\Microsoft SQL Server\Client SDK\ODBC\170\Tools\Binn\SQLCMD.EXE',
    'C:\Program Files\Microsoft SQL Server\Client SDK\ODBC\180\Tools\Binn\SQLCMD.EXE',
    'C:\HVAC_PRO_MSE\TOOLS\sqlcmd\SqlCmd\sqlcmd.exe'
)
$sqlcmd = $sqlcmdCandidates | Where-Object { Test-Path $_ } | Select-Object -First 1
$server = 'localhost\SQLEXPRESS'
$backup = 'C:\HVAC_PRO_MSE\DATABASE\Backups\HVAC_PRO_before_test_demo_cleanup_20260519_182331.bak'
$database = 'HVAC_PRO'
$sqlFile = 'C:\HVAC_PRO_MSE\artifacts\restore-servoerp-database.sql'

if ([string]::IsNullOrWhiteSpace($sqlcmd)) {
    throw "sqlcmd was not found. Checked: $($sqlcmdCandidates -join ', ')"
}

if (-not (Test-Path $backup)) {
    throw "Backup file was not found at $backup"
}

New-Item -ItemType Directory -Force -Path (Split-Path $sqlFile -Parent) | Out-Null

@"
SET NOCOUNT ON;

DECLARE @BackupPath nvarchar(4000) = N'$backup';
DECLARE @DatabaseName sysname = N'$database';
DECLARE @DataPath nvarchar(4000) = CAST(SERVERPROPERTY('InstanceDefaultDataPath') AS nvarchar(4000));
DECLARE @LogPath nvarchar(4000) = CAST(SERVERPROPERTY('InstanceDefaultLogPath') AS nvarchar(4000));

IF @DataPath IS NULL SET @DataPath = N'C:\Program Files\Microsoft SQL Server\MSSQL16.SQLEXPRESS\MSSQL\DATA\';
IF @LogPath IS NULL SET @LogPath = @DataPath;

CREATE TABLE #FileList (
    LogicalName nvarchar(128),
    PhysicalName nvarchar(260),
    [Type] char(1),
    FileGroupName nvarchar(128) NULL,
    Size numeric(20,0),
    MaxSize numeric(20,0),
    FileId bigint,
    CreateLSN numeric(25,0) NULL,
    DropLSN numeric(25,0) NULL,
    UniqueId uniqueidentifier,
    ReadOnlyLSN numeric(25,0) NULL,
    ReadWriteLSN numeric(25,0) NULL,
    BackupSizeInBytes bigint,
    SourceBlockSize int,
    FileGroupId int,
    LogGroupGUID uniqueidentifier NULL,
    DifferentialBaseLSN numeric(25,0) NULL,
    DifferentialBaseGUID uniqueidentifier NULL,
    IsReadOnly bit,
    IsPresent bit,
    TDEThumbprint varbinary(32) NULL,
    SnapshotUrl nvarchar(360) NULL
);

INSERT INTO #FileList
EXEC (N'RESTORE FILELISTONLY FROM DISK = N''' + @BackupPath + N'''');

DECLARE @DataLogical sysname = (SELECT TOP 1 LogicalName FROM #FileList WHERE [Type] = 'D' ORDER BY FileId);
DECLARE @LogLogical sysname = (SELECT TOP 1 LogicalName FROM #FileList WHERE [Type] = 'L' ORDER BY FileId);

IF @DataLogical IS NULL OR @LogLogical IS NULL
BEGIN
    THROW 50001, 'Could not identify data/log logical names in the backup.', 1;
END;

DECLARE @DataFile nvarchar(4000) = @DataPath + @DatabaseName + N'.mdf';
DECLARE @LogFile nvarchar(4000) = @LogPath + @DatabaseName + N'_log.ldf';

IF DB_ID(@DatabaseName) IS NOT NULL
BEGIN
    EXEC (N'ALTER DATABASE [' + @DatabaseName + N'] SET SINGLE_USER WITH ROLLBACK IMMEDIATE;');
END;

DECLARE @RestoreSql nvarchar(max) =
    N'RESTORE DATABASE [HVAC_PRO]' + CHAR(13) +
    N'FROM DISK = N''' + REPLACE(@BackupPath, '''', '''''') + N'''' + CHAR(13) +
    N'WITH REPLACE, RECOVERY,' + CHAR(13) +
    N'MOVE N''' + REPLACE(@DataLogical, '''', '''''') + N''' TO N''' + REPLACE(@DataFile, '''', '''''') + N''',' + CHAR(13) +
    N'MOVE N''' + REPLACE(@LogLogical, '''', '''''') + N''' TO N''' + REPLACE(@LogFile, '''', '''''') + N''',' + CHAR(13) +
    N'STATS = 10;';

EXEC (@RestoreSql);

ALTER DATABASE [HVAC_PRO] SET MULTI_USER;

SELECT name, state_desc, recovery_model_desc
FROM sys.databases
WHERE name = @DatabaseName;
"@ | Set-Content -LiteralPath $sqlFile -Encoding UTF8

& $sqlcmd -S $server -E -i $sqlFile
