param(
    [Parameter(Mandatory = $true)]
    [string]$ControlType,
    [string]$Root = 'C:\HVAC_PRO_MSE',
    [string]$AppDir,
    [string]$OutputDir,
    [string]$ReadyControlName,
    [int]$Width = 1366,
    [int]$Height = 820,
    [int]$TimeoutSeconds = 30,
    [switch]$BoundsAudit,
    [switch]$SkipBoundsAudit
)

$ErrorActionPreference = 'Stop'

if ([Threading.Thread]::CurrentThread.ApartmentState -ne 'STA') {
    throw 'Invoke-ServoPageVisualSmoke.ps1 must run in STA mode. Use: powershell -STA -ExecutionPolicy Bypass -File scripts\Invoke-ServoPageVisualSmoke.ps1 ...'
}

Add-Type -AssemblyName System.Windows.Forms
Add-Type -AssemblyName System.Drawing

function Resolve-AppDir {
    if (-not [string]::IsNullOrWhiteSpace($AppDir)) {
        return (Resolve-Path -LiteralPath $AppDir).Path
    }

    $release = Join-Path $Root 'SOURCE_CODE\bin\Release'
    if (-not (Test-Path -LiteralPath (Join-Path $release 'HVAC_Pro_Desktop.exe'))) {
        throw "Release app was not found at $release. Build Release before visual smoke."
    }
    return $release
}

function Find-ControlByName {
    param(
        [System.Windows.Forms.Control]$RootControl,
        [string]$Name
    )

    if ($RootControl.Name -eq $Name) { return $RootControl }
    foreach ($child in $RootControl.Controls) {
        $match = Find-ControlByName -RootControl $child -Name $Name
        if ($null -ne $match) { return $match }
    }
    return $null
}

function Wait-ForUiIdle {
    param([int]$Milliseconds = 250)

    $end = [DateTime]::UtcNow.AddMilliseconds($Milliseconds)
    while ([DateTime]::UtcNow -lt $end) {
        [System.Windows.Forms.Application]::DoEvents()
        Start-Sleep -Milliseconds 25
    }
}

function Wait-ForReadyControl {
    param(
        [System.Windows.Forms.Control]$RootControl,
        [string]$Name,
        [int]$Timeout
    )

    if ([string]::IsNullOrWhiteSpace($Name)) { return }

    $deadline = [DateTime]::UtcNow.AddSeconds($Timeout)
    while ([DateTime]::UtcNow -lt $deadline) {
        [System.Windows.Forms.Application]::DoEvents()
        $control = Find-ControlByName -RootControl $RootControl -Name $Name
        if ($null -ne $control -and $control.Visible -and $control.Enabled) {
            return
        }
        Start-Sleep -Milliseconds 100
    }

    throw "Ready control '$Name' was not visible and enabled within $Timeout seconds."
}

function Assert-ControlBounds {
    param(
        [System.Windows.Forms.Control]$RootControl,
        [System.Collections.Generic.List[string]]$Issues
    )

    foreach ($child in $RootControl.Controls) {
        if (-not $child.Visible) { continue }
        if ($child.Width -le 0 -or $child.Height -le 0) {
            $Issues.Add("$($child.GetType().FullName) has non-positive size $($child.Width)x$($child.Height).")
            continue
        }

        $allowance = 4
        if ($child.Left + $child.Width -gt $RootControl.ClientSize.Width + $allowance -or
            $child.Top + $child.Height -gt $RootControl.ClientSize.Height + $allowance) {
            $Issues.Add("$($child.GetType().FullName) '$($child.Name)' exceeds parent bounds at $($child.Bounds) inside $($RootControl.ClientSize).")
        }

        Assert-ControlBounds -RootControl $child -Issues $Issues
    }
}

function Assert-NonBlankBitmap {
    param([System.Drawing.Bitmap]$Bitmap)

    $sampleStepX = [Math]::Max(1, [int]($Bitmap.Width / 24))
    $sampleStepY = [Math]::Max(1, [int]($Bitmap.Height / 16))
    $first = $Bitmap.GetPixel(0, 0).ToArgb()
    $different = 0

    for ($x = 0; $x -lt $Bitmap.Width; $x += $sampleStepX) {
        for ($y = 0; $y -lt $Bitmap.Height; $y += $sampleStepY) {
            if ($Bitmap.GetPixel($x, $y).ToArgb() -ne $first) {
                $different++
                if ($different -gt 8) { return }
            }
        }
    }

    throw 'Visual smoke capture appears blank or single-color.'
}

$resolvedAppDir = Resolve-AppDir
$exe = Join-Path $resolvedAppDir 'HVAC_Pro_Desktop.exe'
if (-not (Test-Path -LiteralPath $exe)) {
    throw "HVAC_Pro_Desktop.exe was not found in $resolvedAppDir"
}

if ([string]::IsNullOrWhiteSpace($OutputDir)) {
    $OutputDir = Join-Path $Root 'TEST_RESULTS\visual-smoke'
}
New-Item -ItemType Directory -Force -Path $OutputDir | Out-Null

$assembly = [Reflection.Assembly]::LoadFrom($exe)
[Environment]::CurrentDirectory = $resolvedAppDir
$type = $assembly.GetType($ControlType, $false)
if ($null -eq $type) {
    throw "Control type '$ControlType' was not found in $exe"
}
if (-not [System.Windows.Forms.Control].IsAssignableFrom($type)) {
    throw "'$ControlType' is not a WinForms Control."
}

$control = $null
$form = $null
$bitmap = $null
try {
    $control = [Activator]::CreateInstance($type)
    $control.Dock = [System.Windows.Forms.DockStyle]::Fill

    $form = New-Object System.Windows.Forms.Form
    $form.Text = "ServoERP Visual Smoke - $ControlType"
    $form.StartPosition = [System.Windows.Forms.FormStartPosition]::Manual
    $form.Location = New-Object System.Drawing.Point 20, 20
    $form.ClientSize = New-Object System.Drawing.Size $Width, $Height
    $form.Controls.Add($control)

    $script:servoVisualSmokeShown = $false
    $form.Add_Shown({
        $script:servoVisualSmokeShown = $true
    })

    $form.Show()
    $deadline = [DateTime]::UtcNow.AddSeconds($TimeoutSeconds)
    while (-not $script:servoVisualSmokeShown -and [DateTime]::UtcNow -lt $deadline) {
        [System.Windows.Forms.Application]::DoEvents()
        Start-Sleep -Milliseconds 50
    }
    if (-not $script:servoVisualSmokeShown) { throw "Host form did not reach Shown within $TimeoutSeconds seconds." }

    Wait-ForUiIdle -Milliseconds 750
    Wait-ForReadyControl -RootControl $form -Name $ReadyControlName -Timeout $TimeoutSeconds
    Wait-ForUiIdle -Milliseconds 500

    if ($BoundsAudit -and -not $SkipBoundsAudit) {
        $issues = New-Object 'System.Collections.Generic.List[string]'
        Assert-ControlBounds -RootControl $form -Issues $issues
        if ($issues.Count -gt 0) {
            $report = Join-Path $OutputDir ("{0}-bounds-{1}.txt" -f ($ControlType -replace '[^A-Za-z0-9]+','-'), (Get-Date -Format 'yyyyMMdd-HHmmss'))
            Set-Content -LiteralPath $report -Value $issues -Encoding UTF8
            throw "Visual bounds audit found $($issues.Count) issue(s). Report: $report"
        }
    }

    $bitmap = New-Object System.Drawing.Bitmap $form.Width, $form.Height
    $form.DrawToBitmap($bitmap, (New-Object System.Drawing.Rectangle 0, 0, $form.Width, $form.Height))
    Assert-NonBlankBitmap -Bitmap $bitmap

    $imagePath = Join-Path $OutputDir ("{0}-{1}.png" -f ($ControlType -replace '[^A-Za-z0-9]+','-'), (Get-Date -Format 'yyyyMMdd-HHmmss'))
    $bitmap.Save($imagePath, [System.Drawing.Imaging.ImageFormat]::Png)
    Write-Host "Visual smoke passed: $ControlType"
    Write-Host "Screenshot: $imagePath"
}
finally {
    if ($bitmap -ne $null) { $bitmap.Dispose() }
    if ($form -ne $null) { $form.Close(); $form.Dispose() }
    if ($control -ne $null) { $control.Dispose() }
}
