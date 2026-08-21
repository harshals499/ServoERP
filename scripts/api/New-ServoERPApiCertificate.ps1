param(
 [Parameter(Mandatory=$true)][string]$ServerName,
 [string]$FriendlyName='ServoERP Office API',
 [int]$ValidYears=5
)
$ErrorActionPreference='Stop'
$cert=New-SelfSignedCertificate -DnsName $ServerName,"localhost" -CertStoreLocation 'Cert:\LocalMachine\My' -FriendlyName $FriendlyName -NotAfter (Get-Date).AddYears($ValidYears) -KeyAlgorithm RSA -KeyLength 2048 -HashAlgorithm SHA256 -KeyExportPolicy Exportable
$path=Join-Path $env:TEMP 'ServoERP-OfficeApi-Root.cer'
Export-Certificate -Cert $cert -FilePath $path | Out-Null
Write-Output "Certificate thumbprint: $($cert.Thumbprint)"
Write-Output "Trust this public certificate on every client using: Import-Certificate -FilePath '$path' -CertStoreLocation Cert:\LocalMachine\Root"
Write-Output "Certificate export: $path"
