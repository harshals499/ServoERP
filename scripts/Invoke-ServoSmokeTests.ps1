param(
    [ValidateSet('CiSafe', 'BusinessSave', 'FullUi', 'Amc', 'Contracts', 'DashboardRecents', 'PurchaseViewButtons', 'InvoiceButton', 'PaymentButton', 'JobWorkflow')]
    [string[]]$Suite = @('CiSafe'),
    [string]$Root,
    [string]$AppPath,
    [switch]$SkipBuild,
    [int]$TimeoutSeconds = 300
)

$ErrorActionPreference = 'Stop'

if ([string]::IsNullOrWhiteSpace($Root)) {
    $Root = (Resolve-Path -LiteralPath (Join-Path $PSScriptRoot '..')).Path
}

Write-Host 'ServoERP smoke tests use command-line switches rather than screen clicks.'
Write-Host 'Use CiSafe for CI/no-SQL checks; use BusinessSave/Amc/FullUi only where the SQL environment is ready.'

function Resolve-MSBuild {
    $cmd = Get-Command msbuild.exe -ErrorAction SilentlyContinue
    if ($cmd) { return $cmd.Source }

    $vswhere = Join-Path ${env:ProgramFiles(x86)} 'Microsoft Visual Studio\Installer\vswhere.exe'
    if (Test-Path -LiteralPath $vswhere) {
        $path = & $vswhere -latest -products * -requires Microsoft.Component.MSBuild -find 'MSBuild\**\Bin\MSBuild.exe' | Select-Object -First 1
        if ($path -and (Test-Path -LiteralPath $path)) { return $path }
    }

    throw 'MSBuild.exe was not found. Install Visual Studio Build Tools or add MSBuild to PATH.'
}

function Invoke-SmokeSwitch {
    param(
        [Parameter(Mandatory = $true)][string]$Exe,
        [Parameter(Mandatory = $true)][string]$Switch,
        [Parameter(Mandatory = $true)][int]$Timeout
    )

    Write-Host "Running $Switch"
    $process = Start-Process -FilePath $Exe -ArgumentList $Switch -WorkingDirectory $Root -WindowStyle Hidden -PassThru
    if (-not $process.WaitForExit([Math]::Max(30, $Timeout) * 1000)) {
        try { Stop-Process -Id $process.Id -Force -ErrorAction SilentlyContinue } catch { }
        throw "Smoke test $Switch timed out after $Timeout seconds."
    }

    if ($process.ExitCode -ne 0) {
        throw "Smoke test $Switch failed with exit code $($process.ExitCode). Check TEST_RESULTS and LOGS for the report."
    }
}

Push-Location $Root
try {
    if (-not $SkipBuild) {
        $msbuild = Resolve-MSBuild
        & $msbuild SOURCE_CODE\HVAC_Pro_Desktop.sln /m /p:Configuration=Release /p:Platform="Any CPU"
        if ($LASTEXITCODE -ne 0) { throw 'Release build failed before smoke tests.' }
    }

    if ([string]::IsNullOrWhiteSpace($AppPath)) {
        $AppPath = Join-Path $Root 'SOURCE_CODE\bin\Release\HVAC_Pro_Desktop.exe'
    }

    if (-not (Test-Path -LiteralPath $AppPath)) {
        throw "ServoERP executable was not found: $AppPath"
    }

    $switches = New-Object System.Collections.Generic.List[string]
    foreach ($item in $Suite) {
        switch ($item) {
            'CiSafe' { $switches.Add('/cismoketest') }
            'BusinessSave' { $switches.Add('/savebuttontest') }
            'FullUi' { $switches.Add('/smoketest') }
            'Amc' { $switches.Add('/amctest') }
            'Contracts' { $switches.Add('/contractstest') }
            'DashboardRecents' { $switches.Add('/dashboardrecentstest') }
            'PurchaseViewButtons' { $switches.Add('/poviewbuttontest') }
            'InvoiceButton' { $switches.Add('/invoicebuttontest') }
            'PaymentButton' { $switches.Add('/paymentbuttontest') }
            'JobWorkflow' { $switches.Add('/jobworkflowtest') }
        }
    }

    foreach ($switch in $switches) {
        Invoke-SmokeSwitch -Exe $AppPath -Switch $switch -Timeout $TimeoutSeconds
    }

    Write-Host "Smoke tests passed: $($Suite -join ', ')"
}
finally {
    Pop-Location
}
