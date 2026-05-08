param(
    [string]$AppDir = "C:\HVAC_PRO_MSE",
    [int]$PumpSeconds = 4
)

$ErrorActionPreference = "Stop"

if ([System.Threading.Thread]::CurrentThread.GetApartmentState() -ne "STA") {
    throw "SmokeTestTenderBidInteractions.ps1 must run with powershell -STA."
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
    if (-not (Test-Path $configPath)) { return }

    [xml]$config = Get-Content $configPath
    $node = $config.configuration.connectionStrings.add | Where-Object { $_.name -eq "HVACPro_Connection" } | Select-Object -First 1
    if ($null -eq $node) { return }

    $collection = [System.Configuration.ConfigurationManager]::ConnectionStrings
    $field = $collection.GetType().BaseType.GetField("bReadOnly", [System.Reflection.BindingFlags] "Instance, NonPublic")
    if ($field) { $field.SetValue($collection, $false) }

    $existing = $collection["HVACPro_Connection"]
    if ($existing) { $collection.Remove("HVACPro_Connection") }

    $collection.Add((New-Object System.Configuration.ConnectionStringSettings(
        "HVACPro_Connection",
        [string]$node.connectionString,
        [string]$node.providerName
    )))
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

    $sessionType.GetMethod("SetSession").Invoke($null, @($user)) | Out-Null
}

function Pump([int]$ms) {
    $until = [DateTime]::Now.AddMilliseconds($ms)
    while ([DateTime]::Now -lt $until) {
        [System.Windows.Forms.Application]::DoEvents()
        Start-Sleep -Milliseconds 40
    }
}

function AllControls($root) {
    foreach ($child in $root.Controls) {
        $child
        foreach ($desc in (AllControls $child)) { $desc }
    }
}

function FindButton($root, [string]$contains) {
    AllControls $root |
        Where-Object { $_ -is [System.Windows.Forms.Button] -and (($_.Text -replace "`r?`n", " ") -like "*$contains*") } |
        Select-Object -First 1
}

$result = [ordered]@{
    Result = "FAIL"
    Detail = ""
    BeforeRows = 0
    AfterAddRows = 0
    AfterLabourRows = 0
}

try {
    Import-AppConnectionString
    [System.Windows.Forms.Application]::EnableVisualStyles()

    $threadErrors = New-Object System.Collections.Generic.List[string]
    [System.Windows.Forms.Application]::add_ThreadException({
        param($sender, $eventArgs)
        $threadErrors.Add($eventArgs.Exception.ToString())
    })

    $assembly = [System.Reflection.Assembly]::LoadFrom($exePath)
    Set-TestSession $assembly
    $type = $assembly.GetType("HVAC_Pro_Desktop.UI.TenderBidForm", $true)
    $control = [Activator]::CreateInstance($type)

    $form = New-Object System.Windows.Forms.Form
    $form.Text = "TenderBid interaction smoke"
    $form.StartPosition = [System.Windows.Forms.FormStartPosition]::Manual
    $form.Location = New-Object System.Drawing.Point(-30000, -30000)
    $form.Size = New-Object System.Drawing.Size(1440, 860)
    $form.ShowInTaskbar = $false

    $control.Dock = [System.Windows.Forms.DockStyle]::Fill
    $form.Controls.Add($control)
    $form.Show()
    Pump ($PumpSeconds * 1000)

    if ($control.IsDisposed) {
        throw "TenderBidForm was disposed during smoke load."
    }

    $grid = AllControls $control | Where-Object { $_ -is [System.Windows.Forms.DataGridView] } | Select-Object -First 1
    if ($null -eq $grid) { throw "Tender line-items grid not found." }

    $result.BeforeRows = $grid.Rows.Count

    $addItem = FindButton $control "Add Item"
    if ($null -eq $addItem) { throw "Add Item button not found." }
    $addItem.PerformClick()
    Pump 900
    $result.AfterAddRows = $grid.Rows.Count

    $addLabour = FindButton $control "Add Service Labour"
    if ($null -eq $addLabour) { throw "Add Service Labour button not found." }
    $addLabour.PerformClick()
    Pump 1200
    $result.AfterLabourRows = $grid.Rows.Count

    if ($threadErrors.Count -gt 0) {
        throw "Thread exceptions: " + ($threadErrors -join " || ")
    }
    if ($result.AfterAddRows -lt ($result.BeforeRows + 1)) {
        throw "Add Item did not add a row."
    }
    if ($result.AfterLabourRows -lt ($result.AfterAddRows + 1)) {
        throw "Add Service Labour did not add a row."
    }

    $result.Result = "PASS"
    $result.Detail = "Clicked Add Item and Add Service Labour without UI exceptions."

    $form.Close()
    $form.Dispose()
    $control.Dispose()
}
catch {
    $result.Result = "FAIL"
    $result.Detail = $_.Exception.ToString()
}

$result | ConvertTo-Json -Compress
