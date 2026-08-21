param(
    [string]$Server = '.\SQLEXPRESS',
    [string]$Database = 'HVAC_PRO',
    [string]$LoginName = 'servoerp_app',
    [switch]$ResetPassword
)

$ErrorActionPreference = 'Stop'

function Test-ServoSqlCredential {
    param(
        [string]$TargetServer,
        [string]$TargetDatabase,
        [System.Management.Automation.PSCredential]$Credential
    )

    $builder = New-Object System.Data.SqlClient.SqlConnectionStringBuilder
    $builder['Data Source'] = $TargetServer
    $builder['Initial Catalog'] = $TargetDatabase
    $builder['User ID'] = $Credential.UserName
    $builder['Password'] = $Credential.GetNetworkCredential().Password
    $builder['Integrated Security'] = $false
    $builder['Connect Timeout'] = 10
    $builder['TrustServerCertificate'] = $true
    $connection = New-Object System.Data.SqlClient.SqlConnection $builder.ConnectionString
    try {
        $connection.Open()
        $command = $connection.CreateCommand()
        $command.CommandText = 'SELECT 1'
        return [int]$command.ExecuteScalar() -eq 1
    }
    catch {
        return $false
    }
    finally {
        $connection.Close()
    }
}

$credential = Get-Credential -UserName $LoginName -Message 'Enter the SQL login and password that ServoERP terminal PCs should use.'
if ($null -eq $credential) {
    throw 'Credential entry was cancelled. No SQL Server changes were made.'
}

if (Test-ServoSqlCredential -TargetServer $Server -TargetDatabase $Database -Credential $credential) {
    Write-Host "SQL login '$($credential.UserName)' is already valid for $Server / $Database." -ForegroundColor Green
    Write-Host 'Use this same verified credential in LAN Control or the generated client connection pack.' -ForegroundColor Cyan
    exit 0
}

if (-not $ResetPassword) {
    throw "The supplied login is not currently valid. No changes were made. If you intend to create/repair it, rerun this script as Administrator with -ResetPassword. This can invalidate a different password already saved on other PCs."
}

$adminBuilder = New-Object System.Data.SqlClient.SqlConnectionStringBuilder
$adminBuilder['Data Source'] = $Server
$adminBuilder['Initial Catalog'] = 'master'
$adminBuilder['Integrated Security'] = $true
$adminBuilder['Connect Timeout'] = 15
$adminBuilder['TrustServerCertificate'] = $true
$adminConnection = New-Object System.Data.SqlClient.SqlConnection $adminBuilder.ConnectionString

try {
    $adminConnection.Open()
    $command = $adminConnection.CreateCommand()
    # DDL identifiers and password literals cannot be SqlCommand parameters. QUOTENAME is used
    # inside SQL Server to delimit both values before executing the additive login/user DDL.
    $command.CommandText = @'
DECLARE @login sysname = @LoginName;
DECLARE @password nvarchar(128) = @Password;
DECLARE @database sysname = @DatabaseName;
DECLARE @ddl nvarchar(max);

IF DB_ID(@database) IS NULL
    THROW 51000, 'The configured ServoERP database does not exist.', 1;

IF EXISTS (SELECT 1 FROM sys.sql_logins WHERE name = @login)
    SET @ddl = N'ALTER LOGIN ' + QUOTENAME(@login) + N' WITH PASSWORD = ' + QUOTENAME(@password, '''') + N', CHECK_POLICY = ON; ALTER LOGIN ' + QUOTENAME(@login) + N' ENABLE;';
ELSE
    SET @ddl = N'CREATE LOGIN ' + QUOTENAME(@login) + N' WITH PASSWORD = ' + QUOTENAME(@password, '''') + N', CHECK_POLICY = ON, CHECK_EXPIRATION = OFF;';
EXEC sys.sp_executesql @ddl;

SET @ddl = N'USE ' + QUOTENAME(@database) + N';
IF NOT EXISTS (SELECT 1 FROM sys.database_principals WHERE name = ' + QUOTENAME(@login, '''') + N')
    CREATE USER ' + QUOTENAME(@login) + N' FOR LOGIN ' + QUOTENAME(@login) + N';
ELSE
    ALTER USER ' + QUOTENAME(@login) + N' WITH LOGIN = ' + QUOTENAME(@login) + N';
IF IS_ROLEMEMBER(''db_owner'', ' + QUOTENAME(@login, '''') + N') <> 1
    ALTER ROLE [db_owner] ADD MEMBER ' + QUOTENAME(@login) + N';';
EXEC sys.sp_executesql @ddl;
'@
    [void]$command.Parameters.Add('@LoginName', [System.Data.SqlDbType]::NVarChar, 128)
    [void]$command.Parameters.Add('@Password', [System.Data.SqlDbType]::NVarChar, 128)
    [void]$command.Parameters.Add('@DatabaseName', [System.Data.SqlDbType]::NVarChar, 128)
    $command.Parameters['@LoginName'].Value = $credential.UserName
    $command.Parameters['@Password'].Value = $credential.GetNetworkCredential().Password
    $command.Parameters['@DatabaseName'].Value = $Database
    [void]$command.ExecuteNonQuery()
}
finally {
    $adminConnection.Close()
}

if (-not (Test-ServoSqlCredential -TargetServer $Server -TargetDatabase $Database -Credential $credential)) {
    throw 'SQL login repair completed but verification still failed. Check the SQL Server error log and mixed-mode authentication setting.'
}

Write-Host "SQL login '$($credential.UserName)' was repaired and verified for $Server / $Database." -ForegroundColor Green
Write-Host 'Redeploy the same verified credential through LAN Control or run the new client connection pack on affected PCs.' -ForegroundColor Cyan
