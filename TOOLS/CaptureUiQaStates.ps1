param(
    [string]$AppDir = "C:\HVAC_PRO_MSE\SOURCE_CODE\bin\Debug",
    [string]$OutputDir = "C:\HVAC_PRO_MSE\Docs\UI_QA_Baselines\current",
    [string]$BaselineDir = "C:\HVAC_PRO_MSE\Docs\UI_QA_Baselines\baseline",
    [int]$Width = 1600,
    [int]$Height = 1000,
    [double]$MaxDiffRatio = 0.015,
    [int]$InitialPumpMilliseconds = 2500,
    [int]$PagePumpMilliseconds = 3500,
    [int]$StatePumpMilliseconds = 800
)

$ErrorActionPreference = "Stop"
$originalLocation = (Get-Location).ProviderPath

if ([System.Threading.Thread]::CurrentThread.GetApartmentState() -ne "STA") {
    throw "CaptureUiQaStates.ps1 must run with powershell -STA."
}

Add-Type -AssemblyName System.Windows.Forms
Add-Type -AssemblyName System.Drawing
Add-Type -AssemblyName System.Configuration

[System.Windows.Forms.Application]::SetUnhandledExceptionMode([System.Windows.Forms.UnhandledExceptionMode]::CatchException)
$script:ThreadExceptions = New-Object System.Collections.Generic.List[string]
[System.Windows.Forms.Application]::add_ThreadException({
    param($sender, $eventArgs)
    $script:ThreadExceptions.Add($eventArgs.Exception.ToString())
})

$exePath = Join-Path $AppDir "HVAC_Pro_Desktop.exe"
$configPath = $exePath + ".config"
$compareScript = Join-Path $PSScriptRoot "CompareUiBaseline.ps1"
$manifestPath = Join-Path $OutputDir "manifest.csv"

if (-not (Test-Path -LiteralPath $exePath)) {
    throw "Application executable not found: $exePath"
}

if (-not (Test-Path -LiteralPath $configPath)) {
    throw "Application config not found: $configPath"
}

if (-not (Test-Path -LiteralPath $OutputDir)) {
    New-Item -ItemType Directory -Path $OutputDir -Force | Out-Null
}

Set-Location $AppDir
$env:SERVOERP_UI_QA_SESSION = "1"
$env:SERVOERP_UI_QA_OUTPUT_DIR = $OutputDir

[AppDomain]::CurrentDomain.add_AssemblyResolve({
    param($sender, $args)
    $name = New-Object System.Reflection.AssemblyName($args.Name)
    $candidate = Join-Path $AppDir ($name.Name + ".dll")
    if (Test-Path -LiteralPath $candidate) {
        return [System.Reflection.Assembly]::LoadFrom($candidate)
    }
    return $null
})

function Import-AppConnectionString {
    [xml]$config = Get-Content -Path $configPath
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

function Set-UiQaSession($assembly) {
    $userType = $assembly.GetType("HVAC_Pro_Desktop.Models.AppUserDto", $true)
    $permissionType = $assembly.GetType("HVAC_Pro_Desktop.Models.RolePermissionDto", $true)
    $sessionType = $assembly.GetType("HVAC_Pro_Desktop.Services.SessionManager", $true)

    $user = [Activator]::CreateInstance($userType)
    $user.UserId = 1
    $user.Username = "ui-qa"
    $user.DisplayName = "UI QA"
    $user.RoleId = 1
    $user.RoleName = "Admin"
    $user.IsActive = $true

    foreach ($module in @(
        "Dashboard", "Quotations", "Invoices", "ServiceDesk", "DispatchCenter", "Inventory",
        "Purchases", "Clients", "Vendors", "Payments", "MasterData", "Contracts", "Jobs",
        "WorkOrders", "Payroll", "Employees", "Reports", "Settings", "GeoIntelligence",
        "WhatsAppHub"
    )) {
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

function Get-UiQaMatrix($assembly) {
    $catalogType = $assembly.GetType("HVAC_Pro_Desktop.Tests.UiQaStateCatalog", $false)
    if ($null -eq $catalogType) {
        throw "UiQaStateCatalog was not found in the built assembly. Build the solution before capture."
    }

    $modules = $catalogType.GetField("Modules").GetValue($null)
    $states = $catalogType.GetField("RequiredStateKeys").GetValue($null)
    $matrix = New-Object System.Collections.Generic.List[object]

    foreach ($module in $modules) {
        foreach ($state in $states) {
            $matrix.Add([pscustomobject]@{
                Module = [string]$module.Key
                TypeName = [string]$module.TypeName
                PageIndex = [int]$module.PageIndex
                HasReference = [bool]$module.HasReference
                State = [string]$state
            })
        }
    }

    return $matrix
}

function Pump-Ui([int]$milliseconds) {
    $deadline = [DateTime]::Now.AddMilliseconds($milliseconds)
    while ([DateTime]::Now -lt $deadline) {
        [System.Windows.Forms.Application]::DoEvents()
        Start-Sleep -Milliseconds 100
    }
}

function Invoke-ControlTree {
    param(
        [System.Windows.Forms.Control]$Root,
        [scriptblock]$Action
    )

    if ($null -eq $Root) {
        return
    }

    & $Action $Root
    foreach ($child in $Root.Controls) {
        Invoke-ControlTree -Root $child -Action $Action
    }
}

function Get-VisibleContentRoot($form) {
    $contentField = $form.GetType().GetField("_content", [System.Reflection.BindingFlags] "Instance, NonPublic")
    if ($null -eq $contentField) {
        return $form
    }

    $content = $contentField.GetValue($form)
    if ($null -eq $content) {
        return $form
    }

    foreach ($child in $content.Controls) {
        if ($child.Visible) {
            return $child
        }
    }

    return $content
}

function Reset-ScrollPositions($root) {
    Invoke-ControlTree -Root $root -Action {
        param($control)
        if ($control -is [System.Windows.Forms.ScrollableControl]) {
            try { $control.AutoScrollPosition = New-Object System.Drawing.Point(0, 0) } catch { }
        }
        if ($control -is [System.Windows.Forms.DataGridView]) {
            try { $control.FirstDisplayedScrollingRowIndex = 0 } catch { }
            try { $control.HorizontalScrollingOffset = 0 } catch { }
        }
    }
}

function Scroll-Descendants($root) {
    $script:UiQaChangedCount = 0
    Invoke-ControlTree -Root $root -Action {
        param($control)
        if ($control -is [System.Windows.Forms.ScrollableControl]) {
            try {
                $control.AutoScrollPosition = New-Object System.Drawing.Point(0, 100000)
                $script:UiQaChangedCount++
            } catch { }
        }
        if ($control -is [System.Windows.Forms.DataGridView]) {
            try {
                if ($control.Rows.Count -gt 0) {
                    $control.FirstDisplayedScrollingRowIndex = [Math]::Max(0, $control.Rows.Count - 1)
                    $script:UiQaChangedCount++
                }
            } catch { }
            try {
                $control.HorizontalScrollingOffset = 100000
                $script:UiQaChangedCount++
            } catch { }
        }
    }
    return $script:UiQaChangedCount
}

function Apply-TextState {
    param(
        [System.Windows.Forms.Control]$Root,
        [string]$State,
        [string]$Module
    )

    $script:UiQaChangedCount = 0
    $sample = "QA " + $Module
    $longSample = "UI QA long text sample for " + $Module + " - " + ("operational layout validation " * 8)

    Invoke-ControlTree -Root $Root -Action {
        param($control)
        if ($control -is [System.Windows.Forms.TextBoxBase] -and -not $control.ReadOnly -and $control.Enabled) {
            try {
                if ($State -eq "empty") {
                    $control.Text = ""
                }
                elseif ($State -eq "populated" -and [string]::IsNullOrWhiteSpace($control.Text)) {
                    $control.Text = $sample
                }
                elseif ($State -eq "long-text") {
                    $control.Text = $longSample
                }
                $script:UiQaChangedCount++
            } catch { }
        }
        elseif ($control -is [System.Windows.Forms.ComboBox] -and $control.Enabled) {
            try {
                if ($State -eq "empty") {
                    $control.SelectedIndex = -1
                    $control.Text = ""
                    $script:UiQaChangedCount++
                }
                elseif ($State -eq "populated" -and $control.Items.Count -gt 0 -and $control.SelectedIndex -lt 0) {
                    $control.SelectedIndex = 0
                    $script:UiQaChangedCount++
                }
            } catch { }
        }
        elseif ($control -is [System.Windows.Forms.DataGridView]) {
            try {
                $control.ClearSelection()
                if ($State -eq "populated" -and $control.Rows.Count -gt 0) {
                    $control.Rows[0].Selected = $true
                    $script:UiQaChangedCount++
                }
            } catch { }
        }
    }

    return $script:UiQaChangedCount
}

function Save-FormScreenshot {
    param(
        $Form,
        [string]$Path
    )

    $directory = Split-Path -Parent $Path
    if (-not (Test-Path -LiteralPath $directory)) {
        New-Item -ItemType Directory -Path $directory -Force | Out-Null
    }

    $bitmap = New-Object System.Drawing.Bitmap($Form.ClientSize.Width, $Form.ClientSize.Height)
    try {
        $bounds = New-Object System.Drawing.Rectangle(0, 0, $Form.ClientSize.Width, $Form.ClientSize.Height)
        $Form.DrawToBitmap($bitmap, $bounds)
        $bitmap.Save($Path, [System.Drawing.Imaging.ImageFormat]::Png)
    }
    finally {
        $bitmap.Dispose()
    }
}

function Compare-WithBaseline {
    param(
        [string]$BaselinePath,
        [string]$CurrentPath,
        [string]$DiffPath
    )

    if (-not (Test-Path -LiteralPath $BaselinePath)) {
        return @{ Result = "NO_BASELINE"; DiffRatio = ""; Detail = "Baseline not found" }
    }

    if (-not (Test-Path -LiteralPath $compareScript)) {
        return @{ Result = "NO_COMPARE_SCRIPT"; DiffRatio = ""; Detail = "CompareUiBaseline.ps1 not found" }
    }

    $json = & powershell.exe -NoProfile -ExecutionPolicy Bypass -File $compareScript -BaselinePath $BaselinePath -CurrentPath $CurrentPath -DiffPath $DiffPath -MaxDiffRatio $MaxDiffRatio 2>$null
    $exit = $LASTEXITCODE
    if ([string]::IsNullOrWhiteSpace(($json -join ""))) {
        return @{ Result = "FAIL"; DiffRatio = ""; Detail = "Compare script exited $exit without JSON" }
    }

    $parsed = $json | ConvertFrom-Json
    return @{ Result = [string]$parsed.Result; DiffRatio = [string]$parsed.DiffRatio; Detail = [string]$parsed.Reason }
}

function Save-Manifest {
    param($Rows)

    $manifestDirectory = Split-Path -Parent $manifestPath
    if (-not (Test-Path -LiteralPath $manifestDirectory)) {
        New-Item -ItemType Directory -Path $manifestDirectory -Force | Out-Null
    }

    $Rows | Export-Csv -NoTypeInformation -Path $manifestPath
}

function Clear-CachedPages {
    param($Form)

    try {
        $Form.ClearCachedPagesExceptCurrent()
        [System.Windows.Forms.Application]::DoEvents()
        [GC]::Collect()
        [GC]::WaitForPendingFinalizers()
    }
    catch {
    }
}

$rows = New-Object System.Collections.Generic.List[object]
$form = $null

try {
    Import-AppConnectionString
    [System.Windows.Forms.Application]::EnableVisualStyles()
    $assembly = [System.Reflection.Assembly]::LoadFrom($exePath)
    Set-UiQaSession $assembly
    $matrix = Get-UiQaMatrix $assembly

    $mainFormType = $assembly.GetType("HVAC_Pro_Desktop.UI.MainForm", $true)
    $form = [Activator]::CreateInstance($mainFormType)
    $form.FormBorderStyle = [System.Windows.Forms.FormBorderStyle]::None
    $form.WindowState = [System.Windows.Forms.FormWindowState]::Normal
    $form.StartPosition = [System.Windows.Forms.FormStartPosition]::Manual
    $form.Location = New-Object System.Drawing.Point(-30000, -30000)
    $form.Size = New-Object System.Drawing.Size($Width, $Height)
    $form.ShowInTaskbar = $false
    $form.Show()
    Pump-Ui $InitialPumpMilliseconds

    foreach ($item in $matrix) {
        $script:ThreadExceptions.Clear()
        $stateResult = "PASS"
        $captureResult = "PASS"
        $compareResult = "NOT_RUN"
        $diffRatio = ""
        $detail = ""
        $screenshot = Join-Path $OutputDir (Join-Path $item.Module ($item.State + ".png"))
        $baseline = Join-Path $BaselineDir (Join-Path $item.Module ($item.State + ".png"))
        $diff = Join-Path $OutputDir (Join-Path $item.Module ($item.State + ".diff.png"))

        try {
            $env:SERVOERP_UI_QA_MODULE = $item.Module
            $env:SERVOERP_UI_QA_STATE = $item.State
            $form.NavigateTo([int]$item.PageIndex)
            Pump-Ui $PagePumpMilliseconds

            if ($script:ThreadExceptions.Count -gt 0) {
                throw "UI thread exception: " + $script:ThreadExceptions[$script:ThreadExceptions.Count - 1]
            }

            $root = Get-VisibleContentRoot $form
            Reset-ScrollPositions $root

            if ($item.State -eq "modal") {
                $stateResult = "UNSUPPORTED"
                $compareResult = "UNSUPPORTED"
                $detail = "Native module dialog automation is not implemented for this state."
            }
            elseif ($item.State -eq "scrolled") {
                $changed = Scroll-Descendants $root
                $stateResult = if ($changed -gt 0) { "PASS_SCROLLED" } else { "PASS_NO_SCROLL_TARGET" }
            }
            elseif ($item.State -eq "empty" -or $item.State -eq "populated" -or $item.State -eq "long-text") {
                $changed = Apply-TextState -Root $root -State $item.State -Module $item.Module
                $stateResult = "PASS_MUTATED_CONTROLS_$changed"
            }
            else {
                $stateResult = "UNSUPPORTED"
            }

            Pump-Ui $StatePumpMilliseconds
            if ($script:ThreadExceptions.Count -gt 0) {
                throw "UI thread exception: " + $script:ThreadExceptions[$script:ThreadExceptions.Count - 1]
            }

            Save-FormScreenshot -Form $form -Path $screenshot
            if ($item.State -ne "modal") {
                $comparison = Compare-WithBaseline -BaselinePath $baseline -CurrentPath $screenshot -DiffPath $diff
                $compareResult = $comparison.Result
                $diffRatio = $comparison.DiffRatio
                $detail = $comparison.Detail
            }
        }
        catch {
            $captureResult = "FAIL"
            $stateResult = if ($stateResult -eq "PASS") { "FAIL" } else { $stateResult }
            $detail = $_.Exception.Message
        }
        finally {
            Clear-CachedPages -Form $form
        }

        $rows.Add([pscustomobject]@{
            Module = $item.Module
            TypeName = $item.TypeName
            PageIndex = $item.PageIndex
            HasReference = $item.HasReference
            State = $item.State
            StateResult = $stateResult
            CaptureResult = $captureResult
            CompareResult = $compareResult
            DiffRatio = $diffRatio
            Screenshot = $screenshot
            Baseline = $baseline
            Detail = $detail
        })
        Save-Manifest -Rows $rows
    }
}
catch {
    $rows.Add([pscustomobject]@{
        Module = "SESSION"
        TypeName = ""
        PageIndex = -1
        HasReference = $false
        State = "startup"
        StateResult = "FAIL"
        CaptureResult = "FAIL"
        CompareResult = "NOT_RUN"
        DiffRatio = ""
        Screenshot = ""
        Baseline = ""
        Detail = $_.Exception.ToString()
    })
}
finally {
    if ($null -ne $form) {
        $form.Close()
        $form.Dispose()
    }
    Set-Location $originalLocation
}

Save-Manifest -Rows $rows
$rows | ConvertTo-Json -Depth 4

$failedRows = @($rows | Where-Object { $_.CaptureResult -eq "FAIL" -or $_.CompareResult -eq "FAIL" })
if ($failedRows.Count -gt 0) {
    exit 1
}
