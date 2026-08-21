param(
    [Parameter(Mandatory=$true)][string]$CertificatePath,
    [Parameter(Mandatory=$true)][string]$ExpectedThumbprint
)
$ErrorActionPreference = 'Stop'
if (-not (Test-Path -LiteralPath $CertificatePath)) { throw "Certificate file not found: $CertificatePath" }
$certificate = New-Object System.Security.Cryptography.X509Certificates.X509Certificate2($CertificatePath)
if (($certificate.Thumbprint -replace '\s','').ToUpperInvariant() -ne ($ExpectedThumbprint -replace '\s','').ToUpperInvariant()) { throw 'The public certificate thumbprint does not match the approved Office API certificate.' }
Import-Certificate -FilePath $CertificatePath -CertStoreLocation 'Cert:\LocalMachine\Root' | Out-Null
$trusted = Get-ChildItem "Cert:\LocalMachine\Root\$ExpectedThumbprint" -ErrorAction SilentlyContinue
if ($null -eq $trusted) { throw 'The certificate was not found in the LocalMachine Trusted Root store after import.' }
if ($trusted.HasPrivateKey) { throw 'Unsafe certificate deployment detected: a worker trust certificate must not contain a private key.' }
Write-Output "PASS: Trusted Office API public certificate $($trusted.Thumbprint) in LocalMachine Root."
