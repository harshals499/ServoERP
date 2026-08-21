param(
    [Parameter(Mandatory=$true)][string]$PublishPath,
    [Parameter(Mandatory=$true)][string]$DatabaseConnection,
    [Parameter(Mandatory=$true)][string]$ApiKey,
    [Parameter(Mandatory=$true)][string]$CertificateThumbprint,
    [string]$ServiceName = 'ServoERP.Api',
    [string]$Urls = 'https://0.0.0.0:7443'
)
$exe = Join-Path $PublishPath 'ServoERP.Api.exe'
if (!(Test-Path -LiteralPath $exe)) { throw "Published API executable not found: $exe" }
$productionConfig = Join-Path $PublishPath 'appsettings.Production.json'
@{
    ServoERP = @{
        DatabaseConnectionString = $DatabaseConnection
        ApiKey = $ApiKey
    }
    Urls = $Urls
    Kestrel = @{ Endpoints = @{ Https = @{ Url = $Urls; Certificate = @{ Thumbprint = $CertificateThumbprint; Store = 'My'; Location = 'LocalMachine' } } } }
} | ConvertTo-Json -Depth 4 | Set-Content -LiteralPath $productionConfig -Encoding UTF8
$acl = Get-Acl -LiteralPath $productionConfig
$acl.SetAccessRuleProtection($true, $false)
foreach ($identity in @('BUILTIN\Administrators', 'NT AUTHORITY\SYSTEM')) {
    $acl.AddAccessRule((New-Object System.Security.AccessControl.FileSystemAccessRule($identity, 'FullControl', 'Allow')))
}
Set-Acl -LiteralPath $productionConfig -AclObject $acl
$existing = Get-Service -Name $ServiceName -ErrorAction SilentlyContinue
if ($existing) { Stop-Service -Name $ServiceName -Force; sc.exe delete $ServiceName | Out-Null; Start-Sleep -Seconds 2 }
sc.exe create $ServiceName binPath= "`"$exe`"" start= auto | Out-Null
sc.exe description $ServiceName 'ServoERP private office API. SQL Server remains private to this service.' | Out-Null
Start-Service -Name $ServiceName
Write-Host "Installed and started $ServiceName. Test: https://SERVER:7443/api/v1/health with X-ServoERP-Api-Key."
