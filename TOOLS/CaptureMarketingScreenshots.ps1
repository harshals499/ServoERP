param(
    [string]$AppDir = "C:\HVAC_PRO_MSE",
    [string]$OutputDir = "C:\HVAC_PRO_MSE\Docs\UI_QA_Screenshots\marketing-showcase",
    [int]$Width = 1600,
    [int]$Height = 1000,
    [int]$InitialPumpMilliseconds = 2500,
    [int]$PagePumpMilliseconds = 4500
)

$ErrorActionPreference = "Stop"

if ([System.Threading.Thread]::CurrentThread.GetApartmentState() -ne "STA") {
    throw "CaptureMarketingScreenshots.ps1 must run with powershell -STA."
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

    foreach ($module in @("Dashboard", "Quotations", "Invoices", "ServiceDesk", "DispatchCenter", "Inventory", "Purchases", "Clients", "Vendors", "Payments", "MasterData", "Contracts", "Jobs", "WorkOrders", "Payroll", "Employees", "Reports", "Settings", "GeoIntelligence")) {
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

function Pump-Ui([int]$milliseconds) {
    $deadline = [DateTime]::Now.AddMilliseconds($milliseconds)
    while ([DateTime]::Now -lt $deadline) {
        [System.Windows.Forms.Application]::DoEvents()
        Start-Sleep -Milliseconds 100
    }
}

function Save-FormClientScreenshot($form, [string]$path) {
    $directory = Split-Path -Parent $path
    if (-not (Test-Path $directory)) {
        New-Item -ItemType Directory -Path $directory | Out-Null
    }

    $bitmap = New-Object System.Drawing.Bitmap($form.ClientSize.Width, $form.ClientSize.Height)
    try {
        $bounds = New-Object System.Drawing.Rectangle(0, 0, $form.ClientSize.Width, $form.ClientSize.Height)
        $form.DrawToBitmap($bitmap, $bounds)
        $bitmap.Save($path, [System.Drawing.Imaging.ImageFormat]::Png)
    }
    finally {
        $bitmap.Dispose()
    }
}

$pages = @(
    @{ Name = "Clients"; Index = 1 },
    @{ Name = "Contracts"; Index = 2 },
    @{ Name = "Invoices"; Index = 3 },
    @{ Name = "Payments"; Index = 4 },
    @{ Name = "Reports"; Index = 7 },
    @{ Name = "Settings"; Index = 8 },
    @{ Name = "Vendors"; Index = 9 },
    @{ Name = "Purchases"; Index = 10 },
    @{ Name = "Inventory"; Index = 11 },
    @{ Name = "Payroll"; Index = 13 },
    @{ Name = "Jobs"; Index = 15 },
    @{ Name = "ServiceDesk"; Index = 16 }
)

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
    Pump-Ui $InitialPumpMilliseconds

    $results = New-Object System.Collections.Generic.List[object]
    foreach ($page in $pages) {
        $form.NavigateTo([int]$page.Index)
        Pump-Ui $PagePumpMilliseconds

        $fileName = ("ServoERP-" + $page.Name + "-showcase.png")
        $path = Join-Path $OutputDir $fileName
        Save-FormClientScreenshot $form $path

        $results.Add([pscustomobject]@{
            Page = $page.Name
            Index = $page.Index
            Path = $path
            Width = $form.ClientSize.Width
            Height = $form.ClientSize.Height
        })
    }

    $manifestPath = Join-Path $OutputDir "manifest.csv"
    $results | Export-Csv -NoTypeInformation -Path $manifestPath
    $results | ConvertTo-Json -Depth 3
}
finally {
    $form.Close()
    $form.Dispose()
}
