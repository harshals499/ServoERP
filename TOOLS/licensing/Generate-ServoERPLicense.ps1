param(
    [Parameter(Mandatory=$true)][string]$PrivateKeyXmlPath,
    [Parameter(Mandatory=$true)][string]$OutputPath,
    [Parameter(Mandatory=$true)][ValidateSet("Trial","Standard","Enterprise")][string]$Plan,
    [Parameter(Mandatory=$true)][string]$CompanyName,
    [Parameter(Mandatory=$true)][string]$MachineFingerprintHash,
    [int]$Days = 365,
    [int]$MaxDevices = 3,
    [int]$GracePeriodDays = 3
)

Add-Type -AssemblyName System.Web.Extensions

$modulesByPlan = @{
    Trial = @("Dashboard","Clients","Quotations","Invoices","Reports","Settings","MasterData")
    Standard = @("Dashboard","Clients","Contracts","Invoices","Payments","Quotations","Reports","Settings","Vendors","Purchases","Inventory","Employees","WorkOrders","ServiceDesk","MasterData")
    Enterprise = @("Dashboard","Clients","Contracts","Invoices","Payments","Quotations","Reports","Settings","Vendors","Purchases","Inventory","Employees","Payroll","GeoIntelligence","WorkOrders","ServiceDesk","MasterData")
}

if ($Plan -eq "Trial") { $Days = 14; $MaxDevices = 1 }
if ($Plan -eq "Enterprise" -and $MaxDevices -lt 10) { $MaxDevices = 10 }

$payload = @{
    LicenseKey = "SERVO-$($Plan.ToUpper())-" + [Guid]::NewGuid().ToString("N").Substring(0,12).ToUpper()
    PlanType = $Plan
    CompanyName = $CompanyName
    MaxCompanies = 1
    MaxDevices = $MaxDevices
    MaxUsers = $MaxDevices
    IssueDateUtc = [DateTime]::UtcNow
    ExpiryDateUtc = [DateTime]::UtcNow.AddDays($Days)
    GracePeriodDays = $GracePeriodDays
    MachineFingerprintHash = $MachineFingerprintHash
    ActivatedDeviceId = $MachineFingerprintHash
    ActivatedDeviceCount = 1
    SupportLevel = if ($Plan -eq "Enterprise") { "Priority support" } elseif ($Plan -eq "Standard") { "Core support" } else { "Limited support" }
    EnabledModules = $modulesByPlan[$Plan]
}

$serializer = New-Object System.Web.Script.Serialization.JavaScriptSerializer
$payloadJson = $serializer.Serialize($payload)
$payloadBytes = [Text.Encoding]::UTF8.GetBytes($payloadJson)

$rsa = New-Object System.Security.Cryptography.RSACryptoServiceProvider 2048
$rsa.PersistKeyInCsp = $false
$rsa.FromXmlString([IO.File]::ReadAllText($PrivateKeyXmlPath))
$signature = $rsa.SignData($payloadBytes, [Security.Cryptography.CryptoConfig]::MapNameToOID("SHA256"))

$envelope = @{
    Algorithm = "RS256"
    Payload = [Convert]::ToBase64String($payloadBytes)
    Signature = [Convert]::ToBase64String($signature)
}

$dir = Split-Path -Parent $OutputPath
if ($dir) { New-Item -ItemType Directory -Force $dir | Out-Null }
[IO.File]::WriteAllText($OutputPath, $serializer.Serialize($envelope), [Text.Encoding]::UTF8)
Write-Host "Created signed ServoERP license: $OutputPath"
