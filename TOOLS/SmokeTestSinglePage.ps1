param(
    [Parameter(Mandatory = $true)]
    [string]$TypeName,

    [string]$AppDir = "C:\HVAC_PRO_MSE",
    [int]$PumpSeconds = 6,
    [string]$ScreenshotPath = "",
    [string]$TabText = ""
)

$ErrorActionPreference = "Stop"

if ([System.Threading.Thread]::CurrentThread.GetApartmentState() -ne "STA") {
    throw "SmokeTestSinglePage.ps1 must run with powershell -STA."
}

Add-Type -AssemblyName System.Windows.Forms
Add-Type -AssemblyName System.Drawing
Add-Type -AssemblyName System.Configuration

$exePath = Join-Path $AppDir "HVAC_Pro_Desktop.exe"
$configPath = $exePath + ".config"
Set-Location $AppDir

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
    if (-not (Test-Path $configPath)) {
        throw "App config not found: $configPath"
    }

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

function Set-TestSession($assembly) {
    $userType = $assembly.GetType("HVAC_Pro_Desktop.Models.AppUserDto", $true)
    $permissionType = $assembly.GetType("HVAC_Pro_Desktop.Models.RolePermissionDto", $true)
    $sessionType = $assembly.GetType("HVAC_Pro_Desktop.Services.SessionManager", $true)

    $user = [Activator]::CreateInstance($userType)
    $user.UserId = 1
    $user.Username = "codex-smoke"
    $user.DisplayName = "Codex Smoke Tester"
    $user.RoleId = 1
    $user.RoleName = "Administrator"
    $user.IsActive = $true

    foreach ($module in @("Dashboard", "Quotations", "Invoices", "ServiceDesk", "DispatchCenter", "Inventory", "Purchases", "Clients", "Vendors", "Payments", "MasterData", "Contracts", "Jobs", "Payroll", "Employees", "Reports", "Settings", "GeoIntelligence")) {
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

function Select-TabByText {
    param(
        [System.Windows.Forms.Control]$RootControl,
        [string]$Text
    )

    foreach ($child in $RootControl.Controls) {
        if ($child -is [System.Windows.Forms.TabControl]) {
            foreach ($page in $child.TabPages) {
                if ([string]::Equals($page.Text, $Text, [StringComparison]::OrdinalIgnoreCase)) {
                    $child.SelectedTab = $page
                    return $true
                }
            }
        }
        if ($child -is [System.Windows.Forms.Label] -and
            [string]::Equals($child.Text, $Text, [StringComparison]::OrdinalIgnoreCase)) {
            $onClick = [System.Windows.Forms.Control].GetMethod(
                "OnClick",
                [Reflection.BindingFlags] "Instance,NonPublic"
            )
            $onClick.Invoke($child, @([EventArgs]::Empty)) | Out-Null
            return $true
        }
        if (Select-TabByText -RootControl $child -Text $Text) {
            return $true
        }
    }
    return $false
}

$result = [ordered]@{
    Page = $TypeName
    Result = "FAIL"
    Detail = ""
    ControlCount = 0
}

try {
    Import-AppConnectionString
    [System.Windows.Forms.Application]::EnableVisualStyles()
    $assembly = [System.Reflection.Assembly]::LoadFrom($exePath)
    Set-TestSession $assembly
    $type = $assembly.GetType($TypeName, $true)
    $control = [Activator]::CreateInstance($type)

    $form = New-Object System.Windows.Forms.Form
    $form.Text = "Smoke test - " + $TypeName
    $form.StartPosition = [System.Windows.Forms.FormStartPosition]::Manual
    $form.Location = New-Object System.Drawing.Point(-30000, -30000)
    $form.Size = New-Object System.Drawing.Size(1440, 860)
    $form.ShowInTaskbar = $false

    $control.Dock = [System.Windows.Forms.DockStyle]::Fill
    $form.Controls.Add($control)
    $form.Show()

    $deadline = [DateTime]::Now.AddSeconds($PumpSeconds)
    while ([DateTime]::Now -lt $deadline) {
        [System.Windows.Forms.Application]::DoEvents()
        Start-Sleep -Milliseconds 100
    }

    if (-not [string]::IsNullOrWhiteSpace($TabText)) {
        if (-not (Select-TabByText -RootControl $control -Text $TabText)) {
            throw "Tab '$TabText' was not found."
        }
        [System.Windows.Forms.Application]::DoEvents()
        Start-Sleep -Milliseconds 250
    }

    if ($control.IsDisposed) {
        throw "Page was disposed during smoke load."
    }

    if (-not [string]::IsNullOrWhiteSpace($ScreenshotPath)) {
        $directory = Split-Path -Parent $ScreenshotPath
        if (-not [string]::IsNullOrWhiteSpace($directory) -and -not (Test-Path $directory)) {
            New-Item -ItemType Directory -Path $directory | Out-Null
        }

        $bitmap = New-Object System.Drawing.Bitmap($form.Width, $form.Height)
        try {
            $form.DrawToBitmap($bitmap, (New-Object System.Drawing.Rectangle(0, 0, $form.Width, $form.Height)))
            $bitmap.Save($ScreenshotPath, [System.Drawing.Imaging.ImageFormat]::Png)
        }
        finally {
            $bitmap.Dispose()
        }
    }

    $result.Result = "PASS"
    $result.Detail = "Constructed, handle created, layout pumped."
    $result.ControlCount = $control.Controls.Count

    $form.Close()
    $form.Dispose()
    $control.Dispose()
}
catch {
    $result.Result = "FAIL"
    $result.Detail = $_.Exception.ToString()
}

$result | ConvertTo-Json -Compress
