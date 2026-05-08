param(
    [string]$AppDir = "C:\HVAC_PRO_MSE",
    [int]$TimeoutSeconds = 22
)

$ErrorActionPreference = "Stop"

$pages = @(
    @{ Name = "Dashboard"; Type = "HVAC_Pro_Desktop.UI.DashboardForm" },
    @{ Name = "Clients"; Type = "HVAC_Pro_Desktop.UI.ClientManagementForm" },
    @{ Name = "Contracts"; Type = "HVAC_Pro_Desktop.UI.ContractManagementForm" },
    @{ Name = "Invoices"; Type = "HVAC_Pro_Desktop.UI.InvoiceForm" },
    @{ Name = "Payments"; Type = "HVAC_Pro_Desktop.UI.PaymentForm" },
    @{ Name = "SLA Dashboard"; Type = "HVAC_Pro_Desktop.UI.SLADashboardForm" },
    @{ Name = "Quotations"; Type = "HVAC_Pro_Desktop.UI.TenderBidForm" },
    @{ Name = "Reports"; Type = "HVAC_Pro_Desktop.UI.ReportForm" },
    @{ Name = "Settings"; Type = "HVAC_Pro_Desktop.UI.SettingsForm" },
    @{ Name = "Vendors"; Type = "HVAC_Pro_Desktop.UI.VendorForm" },
    @{ Name = "Purchases"; Type = "HVAC_Pro_Desktop.UI.PurchaseForm" },
    @{ Name = "Inventory"; Type = "HVAC_Pro_Desktop.UI.InventoryForm" },
    @{ Name = "Employees"; Type = "HVAC_Pro_Desktop.UI.EmployeeForm" },
    @{ Name = "Payroll"; Type = "HVAC_Pro_Desktop.UI.PayrollForm" },
    @{ Name = "Geo Intelligence"; Type = "HVAC_Pro_Desktop.UI.GeoIntelligenceForm" },
    @{ Name = "Jobs"; Type = "HVAC_Pro_Desktop.UI.JobManagementForm" },
    @{ Name = "Client Detail"; Type = "HVAC_Pro_Desktop.UI.ClientDetailPage" },
    @{ Name = "Job Detail"; Type = "HVAC_Pro_Desktop.UI.JobDetailPage" }
)

$scriptPath = Join-Path $PSScriptRoot "SmokeTestSinglePage.ps1"
$reportDir = "C:\HVAC_PRO_MSE\TEST_RESULTS"
if (-not (Test-Path $reportDir)) { New-Item -ItemType Directory -Path $reportDir | Out-Null }

$results = New-Object System.Collections.Generic.List[object]
$startedAt = Get-Date

foreach ($page in $pages) {
    $outFile = Join-Path $env:TEMP ("servo-smoke-" + [guid]::NewGuid().ToString("N") + ".json")
    $errFile = Join-Path $env:TEMP ("servo-smoke-" + [guid]::NewGuid().ToString("N") + ".err")
    $args = @(
        "-NoProfile",
        "-ExecutionPolicy", "Bypass",
        "-STA",
        "-File", $scriptPath,
        "-TypeName", $page.Type,
        "-AppDir", $AppDir
    )

    $proc = Start-Process -FilePath "powershell.exe" -ArgumentList $args -PassThru -WindowStyle Hidden -RedirectStandardOutput $outFile -RedirectStandardError $errFile
    $completed = $proc.WaitForExit($TimeoutSeconds * 1000)

    if (-not $completed) {
        try { Stop-Process -Id $proc.Id -Force -ErrorAction SilentlyContinue } catch { }
        $results.Add([pscustomobject]@{
            Page = $page.Name
            Type = $page.Type
            Result = "FAIL"
            Detail = "Timed out after $TimeoutSeconds seconds."
            ControlCount = 0
        })
        continue
    }

    $stdout = if (Test-Path $outFile) { Get-Content $outFile -Raw } else { "" }
    $stderr = if (Test-Path $errFile) { Get-Content $errFile -Raw } else { "" }
    Remove-Item $outFile, $errFile -ErrorAction SilentlyContinue

    try {
        $jsonLine = ($stdout -split "`r?`n" | Where-Object { $_.Trim().StartsWith("{") } | Select-Object -Last 1)
        if ([string]::IsNullOrWhiteSpace($jsonLine)) {
            throw "No JSON result. stdout=$stdout stderr=$stderr"
        }
        $item = $jsonLine | ConvertFrom-Json
        $results.Add([pscustomobject]@{
            Page = $page.Name
            Type = $page.Type
            Result = $item.Result
            Detail = $item.Detail
            ControlCount = $item.ControlCount
        })
    }
    catch {
        $results.Add([pscustomobject]@{
            Page = $page.Name
            Type = $page.Type
            Result = "FAIL"
            Detail = $_.Exception.Message
            ControlCount = 0
        })
    }
}

$endedAt = Get-Date
$reportPath = Join-Path $reportDir ("page-construction-smoke-" + $endedAt.ToString("yyyyMMdd-HHmmss") + ".csv")
$results | Export-Csv -NoTypeInformation -Path $reportPath

[pscustomobject]@{
    StartedAt = $startedAt
    EndedAt = $endedAt
    ReportPath = $reportPath
    Passed = @($results | Where-Object { $_.Result -eq "PASS" }).Count
    Failed = @($results | Where-Object { $_.Result -eq "FAIL" }).Count
}

$results | Format-Table Page, Result, ControlCount, Detail -AutoSize
