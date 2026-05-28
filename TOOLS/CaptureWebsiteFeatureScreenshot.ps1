param(
    [Parameter(Mandatory = $true)]
    [int]$PageIndex,

    [Parameter(Mandatory = $true)]
    [string]$OutputPath,

    [string]$AppDir = "C:\HVAC_PRO_MSE",
    [int]$Width = 1600,
    [int]$Height = 1000,
    [int]$InitialPumpSeconds = 6,
    [int]$PagePumpSeconds = 7
)

$ErrorActionPreference = "Stop"

if ([System.Threading.Thread]::CurrentThread.GetApartmentState() -ne "STA") {
    throw "CaptureWebsiteFeatureScreenshot.ps1 must run with powershell -STA."
}

Add-Type -AssemblyName System.Windows.Forms
Add-Type -AssemblyName System.Drawing
Add-Type -AssemblyName System.Configuration

$exePath = Join-Path $AppDir "HVAC_Pro_Desktop.exe"
$configPath = $exePath + ".config"
Set-Location $AppDir

if (-not (Test-Path $exePath)) {
    throw "Application executable not found: $exePath"
}

if (-not (Test-Path $configPath)) {
    throw "Application config not found: $configPath"
}

[AppDomain]::CurrentDomain.add_AssemblyResolve({
    param($sender, $args)
    $name = New-Object System.Reflection.AssemblyName($args.Name)
    $candidate = Join-Path $AppDir ($name.Name + ".dll")
    if (Test-Path $candidate) {
        return [System.Reflection.Assembly]::LoadFrom($candidate)
    }
    return $null
})

function Import-AppConnectionString {
    [xml]$config = Get-Content $configPath
    $node = $config.configuration.connectionStrings.add | Where-Object { $_.name -eq "HVACPro_Connection" } | Select-Object -First 1
    if ($null -eq $node) {
        throw "HVACPro_Connection not found in $configPath"
    }

    $collection = [System.Configuration.ConfigurationManager]::ConnectionStrings
    $field = $collection.GetType().BaseType.GetField("bReadOnly", [System.Reflection.BindingFlags] "Instance, NonPublic")
    if ($null -ne $field) {
        $field.SetValue($collection, $false)
    }

    $existing = $collection["HVACPro_Connection"]
    if ($null -ne $existing) {
        $collection.Remove("HVACPro_Connection")
    }

    $setting = New-Object System.Configuration.ConnectionStringSettings(
        "HVACPro_Connection",
        [string]$node.connectionString,
        [string]$node.providerName
    )
    $collection.Add($setting)
}

function Set-MarketingSession($assembly) {
    $userType = $assembly.GetType("HVAC_Pro_Desktop.Models.AppUserDto", $true)
    $permissionType = $assembly.GetType("HVAC_Pro_Desktop.Models.RolePermissionDto", $true)
    $sessionType = $assembly.GetType("HVAC_Pro_Desktop.Services.SessionManager", $true)

    $user = [Activator]::CreateInstance($userType)
    $user.UserId = 1
    $user.Username = "marketing"
    $user.DisplayName = "Arjun Shah"
    $user.RoleId = 1
    $user.RoleName = "Administrator"
    $user.IsActive = $true

    foreach ($module in @("Dashboard", "Quotations", "Invoices", "ServiceDesk", "DispatchCenter", "Inventory", "Purchases", "Clients", "Vendors", "Payments", "MasterData", "Contracts", "Jobs", "WorkOrders", "Payroll", "Employees", "Reports", "Settings", "GeoIntelligence", "WhatsAppHub")) {
        $permission = [Activator]::CreateInstance($permissionType)
        $permission.ModuleKey = $module
        $permission.CanView = $true
        $permission.CanCreate = $true
        $permission.CanEdit = $true
        $permission.CanDelete = $true
        $user.Permissions[$module] = $permission
    }

    $sessionType.GetMethod("SetSession").Invoke($null, @($user, $null, $null)) | Out-Null
}

function Pump-Ui([int]$seconds) {
    $deadline = [DateTime]::Now.AddSeconds($seconds)
    while ([DateTime]::Now -lt $deadline) {
        [System.Windows.Forms.Application]::DoEvents()
        Start-Sleep -Milliseconds 100
    }
}

Import-AppConnectionString
[System.Windows.Forms.Application]::EnableVisualStyles()
$assembly = [System.Reflection.Assembly]::LoadFrom($exePath)
Set-MarketingSession $assembly

$mainFormType = $assembly.GetType("HVAC_Pro_Desktop.UI.MainForm", $true)
$form = [Activator]::CreateInstance($mainFormType)

try {
    $form.FormBorderStyle = [System.Windows.Forms.FormBorderStyle]::None
    $form.WindowState = [System.Windows.Forms.FormWindowState]::Normal
    $form.StartPosition = [System.Windows.Forms.FormStartPosition]::Manual
    $form.Location = New-Object System.Drawing.Point(-30000, -30000)
    $form.Size = New-Object System.Drawing.Size($Width, $Height)
    $form.ShowInTaskbar = $false
    $form.Show()
    Pump-Ui $InitialPumpSeconds

    $form.NavigateTo($PageIndex)
    Pump-Ui $PagePumpSeconds

    $directory = Split-Path -Parent $OutputPath
    if (-not (Test-Path $directory)) {
        New-Item -ItemType Directory -Path $directory -Force | Out-Null
    }

    $bitmap = New-Object System.Drawing.Bitmap($form.ClientSize.Width, $form.ClientSize.Height)
    try {
        $bounds = New-Object System.Drawing.Rectangle(0, 0, $form.ClientSize.Width, $form.ClientSize.Height)
        $form.DrawToBitmap($bitmap, $bounds)
        $bitmap.Save($OutputPath, [System.Drawing.Imaging.ImageFormat]::Png)
    }
    finally {
        $bitmap.Dispose()
    }

    [pscustomobject]@{
        PageIndex = $PageIndex
        Path = $OutputPath
        Width = $form.ClientSize.Width
        Height = $form.ClientSize.Height
    } | ConvertTo-Json -Compress
}
finally {
    $form.Close()
    $form.Dispose()
}
