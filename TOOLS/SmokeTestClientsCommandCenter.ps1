param(
    [string]$AppDir = "C:\HVAC_PRO_MSE",
    [int]$PumpSeconds = 8
)

$ErrorActionPreference = "Stop"

if ([System.Threading.Thread]::CurrentThread.GetApartmentState() -ne "STA") {
    throw "SmokeTestClientsCommandCenter.ps1 must run with powershell -STA."
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
    $collection.Add((New-Object System.Configuration.ConnectionStringSettings("HVACPro_Connection", [string]$node.connectionString, [string]$node.providerName)))
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

function Pump([int]$ms = 500) {
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
    AllControls $root | Where-Object { $_ -is [System.Windows.Forms.Button] -and ($_.Text -replace "`r?`n", " ") -like "*$contains*" } | Select-Object -First 1
}

function ClickWithAutoClose($button) {
    if ($null -eq $button) { throw "Button not found." }
    $timer = New-Object System.Windows.Forms.Timer
    $timer.Interval = 250
    $timer.Add_Tick({
        [System.Windows.Forms.SendKeys]::SendWait("{ESC}")
        foreach ($f in [System.Windows.Forms.Application]::OpenForms) {
            if ($f.Text -like "*New Client*" -or $f.Text -like "*Add Job*" -or $f.Text -like "*Create Invoice*" -or $f.Text -like "*Edit Profile*" -or $f.Text -like "*Log Activity*") {
                $f.DialogResult = [System.Windows.Forms.DialogResult]::Cancel
                $f.Close()
            }
        }
    })
    $timer.Start()
    try { $button.PerformClick() }
    finally {
        $timer.Stop()
        $timer.Dispose()
    }
    Pump 300
}

function ClickControl($control) {
    $method = $control.GetType().GetMethod("OnClick", [System.Reflection.BindingFlags] "Instance, NonPublic")
    if ($method) {
        $method.Invoke($control, @([System.EventArgs]::Empty)) | Out-Null
        return
    }
    if ($control -is [System.Windows.Forms.Button]) {
        $control.PerformClick()
        return
    }
    throw "Control is not clickable: $($control.GetType().FullName)"
}

$result = [ordered]@{
    Result = "FAIL"
    Detail = ""
    ClientCards = 0
    Clicked = @()
}

try {
    Import-AppConnectionString
    [System.Windows.Forms.Application]::EnableVisualStyles()
    $assembly = [System.Reflection.Assembly]::LoadFrom($exePath)
    Set-TestSession $assembly
    $type = $assembly.GetType("HVAC_Pro_Desktop.UI.ClientManagementForm", $true)
    $control = [Activator]::CreateInstance($type)

    $form = New-Object System.Windows.Forms.Form
    $form.Text = "Clients click smoke"
    $form.StartPosition = [System.Windows.Forms.FormStartPosition]::Manual
    $form.Location = New-Object System.Drawing.Point(-30000, -30000)
    $form.Size = New-Object System.Drawing.Size(1440, 860)
    $form.ShowInTaskbar = $false
    $control.Dock = [System.Windows.Forms.DockStyle]::Fill
    $form.Controls.Add($control)
    $form.Show()
    Pump ($PumpSeconds * 1000)

    $cards = @(AllControls $control | Where-Object { $_.GetType().Name -eq "ClientCardControl" })
    if ($cards.Count -lt 1) { throw "No client cards rendered." }
    $result.ClientCards = $cards.Count

    ClickControl $cards[0]
    $result.Clicked += "client card"
    Pump 400

    $search = AllControls $control | Where-Object { $_ -is [System.Windows.Forms.TextBox] -and $_.Text -like "Search by name*" } | Select-Object -First 1
    if ($null -eq $search) { throw "Search box not found." }
    $search.Focus()
    $search.Text = "A G"
    $result.Clicked += "search"
    Pump 500

    $tabs = AllControls $control | Where-Object { $_ -is [System.Windows.Forms.TabControl] } | Select-Object -First 1
    if ($null -eq $tabs) { throw "Tabs not found." }
    for ($i = 0; $i -lt $tabs.TabPages.Count; $i++) {
        $tabs.SelectedIndex = $i
        $result.Clicked += ("tab:" + $tabs.TabPages[$i].Text)
        Pump 250
    }

    $next = FindButton $control ">"
    if ($next -and $next.Enabled) {
        $next.PerformClick()
        $result.Clicked += "pagination next"
        Pump 300
    }

    foreach ($label in @("New Client", "Log Activity", "Add Job", "Create Invoice", "Edit Profile", "More")) {
        $button = FindButton $control $label
        if ($button) {
            ClickWithAutoClose $button
            $result.Clicked += $label
        }
        else {
            throw "Visible action not found: $label"
        }
    }

    $result.Result = "PASS"
    $result.Detail = "Opened Clients page and clicked cards, search, tabs, pagination, and visible actions."

    $form.Close()
    $form.Dispose()
    $control.Dispose()
}
catch {
    $result.Result = "FAIL"
    $result.Detail = $_.Exception.ToString()
}

$result | ConvertTo-Json -Compress
