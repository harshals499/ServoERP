#Requires -Version 7.4
[CmdletBinding()]
param([Parameter(Mandatory)][string]$PackageRoot)
$ErrorActionPreference='Stop'
$identity=[Security.Principal.WindowsIdentity]::GetCurrent()
if(!([Security.Principal.WindowsPrincipal]::new($identity)).IsInRole([Security.Principal.WindowsBuiltInRole]::Administrator)){throw 'Windows elevation is required to update the existing office startup task.'}
$version='1.1.504'
$taskName='ServoERP-MobileOffice-1.1.499'
$old=Join-Path $env:LOCALAPPDATA 'ServoERP/MobileOffice/1.1.499'
$next=Join-Path $env:LOCALAPPDATA "ServoERP/MobileOffice/$version"
$deadline=[DateTime]::UtcNow.AddSeconds(60)
$stageLock=$null
while($null -eq $stageLock){
 try{$stageLock=[IO.File]::Open((Join-Path $PackageRoot 'API_STAGE.lock'),[IO.FileMode]::OpenOrCreate,[IO.FileAccess]::ReadWrite,[IO.FileShare]::None)}
 catch [IO.IOException]{if([DateTime]::UtcNow -ge $deadline){throw 'The office payload is being verified; the existing service was preserved.'};Start-Sleep -Milliseconds 500}
}
try {
$manifest=Get-Content (Join-Path $PackageRoot 'API_SHA256SUMS.json') -Raw | ConvertFrom-Json
if($manifest.Version -ne $version){throw 'Office package version mismatch.'}
if($manifest.SchemaSHA256 -ne '0987A0C38912D39EA924FD47A2EACBD00A5240E21065CB4F84B0BC1FA6531E36'){throw 'This upgrade requires the unchanged, already deployed mobile schema.'}
$payload=[IO.Path]::GetFullPath((Join-Path $PackageRoot 'api')).TrimEnd('\')+'\'
foreach($file in $manifest.Files){
 $path=[IO.Path]::GetFullPath((Join-Path $payload $file.Path))
 if(!$path.StartsWith($payload,[StringComparison]::OrdinalIgnoreCase) -or !(Test-Path -LiteralPath $path) -or (Get-FileHash -LiteralPath $path).Hash -ne $file.SHA256){throw 'Office payload verification failed.'}
}
if(!(Test-Path (Join-Path $payload 'ServoERP.Api.exe'))){throw 'Office executable is missing.'}
if(Test-Path $next){throw 'This office version is already staged. Inspect its deployment record before retrying.'}
$task=Get-ScheduledTask -TaskName $taskName
if($task.Actions.Count -ne 1 -or $task.Actions[0].Arguments -notlike ('*'+$old+'*')){throw 'The existing task does not identify the expected office companion.'}
$configuration=Get-Content (Join-Path $old 'appsettings.Production.json') -Raw | ConvertFrom-Json
$db=[System.Data.SqlClient.SqlConnectionStringBuilder]::new([string]$configuration.ServoERP.DatabaseConnectionString)
if($db.InitialCatalog -ne 'HVAC_PRO' -or $db.DataSource -notin @('.\SQLEXPRESS','localhost\SQLEXPRESS',"$env:COMPUTERNAME\SQLEXPRESS")){throw 'Unexpected office database identity.'}
$cert=[Security.Cryptography.X509Certificates.X509Certificate2]::new($configuration.Kestrel.Certificates.Default.Path,$configuration.Kestrel.Certificates.Default.Password)
$pin=$cert.GetCertHashString([Security.Cryptography.HashAlgorithmName]::SHA256)
if($pin -ne 'D2005E8ABBE89FC9B8B15B6B691C46ECB00759B1828BAD213A368B12669995B9' -or $cert.NotAfter -le (Get-Date)){throw 'The existing office certificate does not match the paired mobile authority.'}
$listeners=@(Get-NetTCPConnection -LocalPort 18446 -State Listen)
$hostIds=@($listeners.OwningProcess | Sort-Object -Unique)
if($hostIds.Count -ne 1 -or (Get-Process -Id $hostIds[0]).Path -ne (Join-Path $old 'ServoERP.Api.exe')){throw 'Office port belongs to an unexpected process.'}
# COPY_ONLY backup is deployment protection, not a business-save test. No migration runs here.
$backupRoot=(& sqlcmd -S .\SQLEXPRESS -d master -E -C -b -h -1 -W -Q "SET NOCOUNT ON; SELECT CONVERT(nvarchar(4000),SERVERPROPERTY('InstanceDefaultBackupPath'))") -join ''
if($LASTEXITCODE -or [string]::IsNullOrWhiteSpace($backupRoot)){throw 'SQL backup directory unavailable.'}
$backup=Join-Path $backupRoot.Trim() ('HVAC_PRO_before_mobile_1_1_504_'+(Get-Date -Format 'yyyyMMdd_HHmmss')+'.bak')
$literal=$backup.Replace("'","''")
& sqlcmd -S .\SQLEXPRESS -d master -E -C -b -Q "BACKUP DATABASE [HVAC_PRO] TO DISK=N'$literal' WITH COPY_ONLY,CHECKSUM; RESTORE VERIFYONLY FROM DISK=N'$literal' WITH CHECKSUM;"
if($LASTEXITCODE){throw 'Backup verification failed; office task was preserved.'}
New-Item -ItemType Directory -Path $next | Out-Null
$acl=[Security.AccessControl.DirectorySecurity]::new();$acl.SetAccessRuleProtection($true,$false)
foreach($principal in @($identity.Name,'NT AUTHORITY\SYSTEM')){$acl.AddAccessRule([Security.AccessControl.FileSystemAccessRule]::new($principal,'FullControl','ContainerInherit,ObjectInherit','None','Allow'))}
Set-Acl -LiteralPath $next -AclObject $acl
Get-ChildItem -LiteralPath $payload | Copy-Item -Destination $next -Recurse
Copy-Item -LiteralPath (Join-Path $old 'appsettings.Production.json') -Destination $next
$taskXml=Export-ScheduledTask -TaskName $taskName
$taskXml | Set-Content (Join-Path $next 'previous-task.xml')
# Reuse the established launcher and its singleton mutex; only its directory changes.
Copy-Item -LiteralPath (Join-Path $old 'Start-MobileOffice.ps1') -Destination $next
$record=[ordered]@{Version=$version;PreviousVersion='1.1.499';Folder=$next;Task=$taskName;Addresses=@('https://192.168.2.5:18446','https://100.97.226.80:18446');CertificateSHA256=$pin;VerifiedBackup=$backup;BackupSHA256=(Get-FileHash -LiteralPath $backup).Hash;Migration='None';BusinessWriteTests='Disposable database only';Status='Staged'}
$recordPath=Join-Path $next 'OFFICE_CONNECTION.json'
$record | ConvertTo-Json -Depth 5 | Set-Content $recordPath
try{
 Set-Content (Join-Path $old 'stop.requested') 'Companion binary upgrade'
 Stop-ScheduledTask -TaskName $taskName
 if((Get-Process -Id $hostIds[0] -ErrorAction SilentlyContinue).Path -eq (Join-Path $old 'ServoERP.Api.exe')){Stop-Process -Id $hostIds[0]}
 $launcherMutex=[Threading.Mutex]::new($false,'Local\ServoERP.MobileOffice.1.1.499');$ownsMutex=$false
 try{
  try{$ownsMutex=$launcherMutex.WaitOne([TimeSpan]::FromSeconds(15))}catch [Threading.AbandonedMutexException]{$ownsMutex=$true}
  if(!$ownsMutex){throw 'The previous office launcher has not stopped; restoring its startup task.'}
 }finally{if($ownsMutex){$launcherMutex.ReleaseMutex()};$launcherMutex.Dispose()}
 $action=New-ScheduledTaskAction -Execute (Get-Command pwsh).Source -Argument "-NoProfile -WindowStyle Hidden -File `"$(Join-Path $next 'Start-MobileOffice.ps1')`"" -WorkingDirectory $next
 Set-ScheduledTask -TaskName $taskName -Action $action | Out-Null
 Start-ScheduledTask -TaskName $taskName
 Start-Sleep -Seconds 5
 & (Join-Path $PSScriptRoot 'Test-MobileOfficeConnection.ps1') -ConnectionRecord $recordPath | Set-Content (Join-Path $next 'HTTPS_READINESS.json')
 $record.Status='Verified HTTPS/SQL readiness';$record.InstalledUtc=[DateTime]::UtcNow.ToString('o')
 $record | ConvertTo-Json -Depth 5 | Set-Content $recordPath
 Copy-Item -LiteralPath $recordPath -Destination (Join-Path $PackageRoot 'OFFICE_CONNECTION.json')
 Write-Output 'Office companion updated; certificate, credentials, address and existing firewall retained. No schema or business-data migration.'
}catch{
 $failure=$_.Exception.Message
 Set-Content (Join-Path $next 'stop.requested') 'Upgrade failed; restore prior companion'
 Stop-ScheduledTask -TaskName $taskName -ErrorAction SilentlyContinue
 foreach($listener in @(Get-NetTCPConnection -LocalPort 18446 -State Listen -ErrorAction SilentlyContinue)){if((Get-Process -Id $listener.OwningProcess -ErrorAction SilentlyContinue).Path -eq (Join-Path $next 'ServoERP.Api.exe')){Stop-Process -Id $listener.OwningProcess}}
 Register-ScheduledTask -TaskName $taskName -Xml $taskXml -Force | Out-Null
 Remove-Item -LiteralPath (Join-Path $old 'stop.requested') -ErrorAction SilentlyContinue
 Start-ScheduledTask -TaskName $taskName
 $record.Status='Prior startup task restored and restart requested; service readiness must be checked. No data rollback.';$record.Error=$failure;$record | ConvertTo-Json -Depth 5 | Set-Content $recordPath
 throw "Office upgrade failed; the previous startup task was restored and restart requested: $failure"
}
} finally {$stageLock.Dispose()}
