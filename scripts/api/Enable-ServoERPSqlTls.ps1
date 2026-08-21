param(
    [Parameter(Mandatory=$true)][string]$Thumbprint,
    [string]$InstanceKey = 'MSSQL16.SQLEXPRESS',
    [switch]$Apply
)
$ErrorActionPreference='Stop'
$thumb=$Thumbprint.Replace(' ','').ToUpperInvariant()
$cert=Get-Item "Cert:\LocalMachine\My\$thumb" -ErrorAction Stop
if(!$cert.HasPrivateKey){throw 'The SQL Server certificate has no private key.'}
if($cert.NotAfter -le (Get-Date)){throw 'The SQL Server certificate is expired.'}
$eku=$cert.Extensions | Where-Object {$_.Oid.Value -eq '2.5.29.37'} | Select-Object -First 1
if($null -eq $eku -or $eku.Format($false) -notmatch 'Server Authentication'){throw 'Certificate is not valid for server authentication.'}
$key="HKLM:\SOFTWARE\Microsoft\Microsoft SQL Server\$InstanceKey\MSSQLServer\SuperSocketNetLib"
if(!(Test-Path $key)){throw "SQL instance registry path was not found: $key"}
if(!$Apply){ Write-Output "READY: bind $thumb to $key and restart MSSQL`$SQLEXPRESS. Run again with -Apply in an elevated PowerShell maintenance window."; exit 0 }
Set-ItemProperty -Path $key -Name Certificate -Value $thumb
Set-ItemProperty -Path $key -Name ForceEncryption -Value 1
Import-Certificate -FilePath 'C:\Users\ADMINI~1\AppData\Local\Temp\ServoERP-OfficeApi-Root.cer' -CertStoreLocation 'Cert:\LocalMachine\Root' | Out-Null
Restart-Service -Name 'MSSQL$SQLEXPRESS' -Force
Start-Sleep -Seconds 5
if((Get-Service 'MSSQL$SQLEXPRESS').Status -ne 'Running'){throw 'SQL Server did not restart successfully.'}
Write-Output 'PASS SQL TLS binding applied. Verify with a trusted encrypted client before API deployment.'
