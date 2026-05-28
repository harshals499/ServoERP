$ErrorActionPreference = 'Stop'

$appPath = 'C:\HVAC_PRO_MSE\SOURCE_CODE\bin\Release\HVAC_Pro_Desktop.exe'
$appDir = Split-Path -Parent $appPath
Set-Location $appDir
[Reflection.Assembly]::LoadFrom($appPath) | Out-Null

$validator = New-Object HVAC_Pro_Desktop.Services.Licensing.OfflineLicenseValidator
$fingerprint = New-Object HVAC_Pro_Desktop.Services.Licensing.DeviceFingerprintService
$machineHash = $fingerprint.GetFingerprintHash()

Get-ChildItem -Path 'C:\HVAC_PRO_MSE' -Recurse -File -Filter '*.servoerp-license' -ErrorAction SilentlyContinue |
    Where-Object { $_.FullName -notmatch '\\node_modules\\|\\TOOLS\\PortableGit\\|\\TOOLS\\node\\' } |
    Sort-Object FullName |
    ForEach-Object {
        $result = $validator.ValidateLicenseFile($_.FullName, $machineHash)
        $snapshot = $result.Snapshot
        [pscustomobject]@{
            Success = $result.Success
            File = $_.FullName
            Message = $result.Message
            Company = if ($snapshot) { $snapshot.CompanyName } else { $null }
            Plan = if ($snapshot) { $snapshot.PlanType } else { $null }
            Status = if ($snapshot) { $snapshot.Status } else { $null }
            ExpiryUtc = if ($snapshot) { $snapshot.ExpiryDateUtc } else { $null }
        }
    } | Format-Table -AutoSize -Wrap
