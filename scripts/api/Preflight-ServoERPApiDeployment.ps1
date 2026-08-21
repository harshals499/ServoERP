param([Parameter(Mandatory=$true)][string]$SqlServer,[string]$Database='HVAC_PRO',[int]$Port=7443,[Parameter(Mandatory=$true)][string]$PublishPath)
$ErrorActionPreference='Stop'
if(!(Test-Path (Join-Path $PublishPath 'ServoERP.Api.exe'))){throw 'Published API executable is missing.'}
if(Get-NetTCPConnection -LocalPort $Port -State Listen -ErrorAction SilentlyContinue){throw "API port $Port is already in use."}
& sqlcmd -S $SqlServer -E -d master -b -Q "IF DATABASEPROPERTYEX('$Database','Status') <> 'ONLINE' THROW 51000,'Database is not online.',1; SELECT @@SERVERNAME AS ServerName,DB_NAME(DB_ID('$Database')) AS DatabaseName;"
if($LASTEXITCODE -ne 0){throw 'SQL Server preflight failed.'}
$drive=(Get-Item $PublishPath).PSDrive; if($drive.Free -lt 2GB){throw 'Less than 2 GB free disk space for API deployment.'}
Write-Output 'PASS API deployment preflight.'
