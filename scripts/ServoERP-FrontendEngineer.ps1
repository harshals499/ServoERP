param(
    [string]$Root = 'C:\HVAC_PRO_MSE',
    [string]$Solution = 'SOURCE_CODE\HVAC_Pro_Desktop.sln',
    [switch]$SkipBuild,
    [switch]$SkipCardMenuAudit,
    [int]$AuditTimeoutSeconds = 180,
    [switch]$EmitPromptOnly
)

$ErrorActionPreference = 'Stop'

$logDir = Join-Path $Root 'LOGS'
$tempDir = Join-Path $Root 'TEMP'
$log = Join-Path $logDir 'frontend-engineer.log'
$promptPath = Join-Path $tempDir 'servoerp-frontend-engineer-prompt.txt'

New-Item -ItemType Directory -Force -Path $logDir | Out-Null
New-Item -ItemType Directory -Force -Path $tempDir | Out-Null

function Write-FrontendLog {
    param(
        [string]$Page,
        [string]$Screen,
        [string]$Issue,
        [string]$FixApplied,
        [ValidateSet('PASS', 'FAIL')]
        [string]$Build
    )

    $timestamp = Get-Date -Format 'yyyy-MM-dd HH:mm:ss'
    Add-Content -LiteralPath $log -Encoding UTF8 -Value "[$timestamp] [$Page] [$Screen] [$Issue] [$FixApplied] [BUILD: $Build]"
}

function Resolve-MSBuild {
    $cmd = Get-Command msbuild.exe -ErrorAction SilentlyContinue
    if ($cmd) {
        return $cmd.Source
    }

    $vswhere = Join-Path ${env:ProgramFiles(x86)} 'Microsoft Visual Studio\Installer\vswhere.exe'
    if (Test-Path -LiteralPath $vswhere) {
        $path = & $vswhere -latest -products * -requires Microsoft.Component.MSBuild -find 'MSBuild\**\Bin\MSBuild.exe' | Select-Object -First 1
        if ($path -and (Test-Path -LiteralPath $path)) {
            return $path
        }
    }

    throw 'MSBuild.exe was not found. Install Visual Studio Build Tools or add MSBuild to PATH.'
}

function Invoke-ReleaseBuild {
    param(
        [string]$Page,
        [string]$Screen,
        [string]$Issue,
        [string]$FixApplied
    )

    if ($SkipBuild) {
        Write-FrontendLog $Page $Screen $Issue "$FixApplied Build skipped by parameter." 'FAIL'
        return $false
    }

    Push-Location $Root
    try {
        $msbuild = Resolve-MSBuild
        & $msbuild (Join-Path $Root $Solution) /p:Configuration=Release | Tee-Object -FilePath (Join-Path $logDir 'frontend-engineer-msbuild.latest.log') | Out-Host
        $passed = $LASTEXITCODE -eq 0
        Write-FrontendLog $Page $Screen $Issue $FixApplied $(if ($passed) { 'PASS' } else { 'FAIL' })
        return $passed
    }
    finally {
        Pop-Location
    }
}

function Invoke-CardMenuAudit {
    $exe = Join-Path $Root 'SOURCE_CODE\bin\Release\HVAC_Pro_Desktop.exe'
    if ($SkipCardMenuAudit) {
        Write-FrontendLog 'Dialogs' 'Global card context menu audit' 'Shared card popup regression audit was requested.' 'Skipped by parameter.' 'FAIL'
        return $true
    }

    if (-not (Test-Path -LiteralPath $exe)) {
        Write-FrontendLog 'Dialogs' 'Global card context menu audit' 'Release executable was required before UI audit.' "Expected $exe." 'FAIL'
        return $false
    }

    $started = Get-Date
    $process = Start-Process -FilePath $exe -ArgumentList '/cardmenuaudit' -WorkingDirectory $Root -WindowStyle Hidden -PassThru
    if (-not $process.WaitForExit([Math]::Max(30, $AuditTimeoutSeconds) * 1000)) {
        try { Stop-Process -Id $process.Id -Force -ErrorAction SilentlyContinue } catch { }
        Write-FrontendLog 'Dialogs' 'Global card context menu audit' 'Card menu audit did not exit before timeout.' "Killed process $($process.Id) after $AuditTimeoutSeconds seconds." 'FAIL'
        return $false
    }

    $report = Get-ChildItem -LiteralPath (Join-Path $Root 'TEST_RESULTS') -Filter 'global-card-context-menu-audit-*.txt' -ErrorAction SilentlyContinue |
        Where-Object { $_.LastWriteTime -ge $started.AddSeconds(-5) } |
        Sort-Object LastWriteTime -Descending |
        Select-Object -First 1

    if ($null -eq $report) {
        Write-FrontendLog 'Dialogs' 'Global card context menu audit' 'Card menu audit completed without a fresh report.' 'No global-card-context-menu-audit report found for this run.' 'FAIL'
        return $false
    }

    $text = Get-Content -Raw -LiteralPath $report.FullName
    $passed = $text -match 'Failed:\s+0'
    Write-FrontendLog 'Dialogs' 'Global card context menu audit' 'Shared card popup behavior required regression coverage.' "Report=$($report.FullName)" $(if ($passed) { 'PASS' } else { 'FAIL' })
    return $passed
}

$prompt = @"
Act as a senior WinForms frontend engineer for ServoERP in $Root.

Audit every sidebar page, subpage, New form, Detail/Edit form, modal/dialog/popup, ClientDetailPage tabs, and JobDetailPage tabs. Inspect layout, spacing, clipping, alignment, typography, truncation, colors, enabled/disabled states, status badges, buttons, icons, empty/loading states, button behavior, dropdown defaults/placeholders, required field cues, unsaved-change prompts, tab order, and resize behavior.

Fix issues directly in source using repository rules. Do not touch MainForm.cs sidebar navigation or JobManagementForm unless explicitly needed and allowed. Do not perform hard-stop business-data or licensing changes. After each page batch, run:
msbuild SOURCE_CODE\HVAC_Pro_Desktop.sln /p:Configuration=Release

Append LOGS\frontend-engineer.log entries exactly as:
[TIMESTAMP] [PAGE] [SCREEN/SUBPAGE] [ISSUE] [FIX APPLIED] [BUILD: PASS/FAIL]

End with total screens audited, issues found, issues fixed, issues skipped with reasons, final build status, changed files, packages added, version/changelog changes, and manual verification performed.
"@

Set-Content -LiteralPath $promptPath -Encoding UTF8 -Value $prompt
Write-FrontendLog 'FRONTEND_ENGINEER' 'Session prompt' 'Audit prompt refreshed for Codex-driven repair pass.' "Prompt written to $promptPath" 'PASS'

if ($EmitPromptOnly) {
    Write-Output $promptPath
    exit 0
}

$batches = @(
    @{ Page = 'Dashboard'; Screen = 'Dashboard page'; Issue = 'Sidebar dashboard page requires recurring layout, action, empty-state, and resize audit.'; Fix = 'Run Codex audit prompt, inspect DashboardForm, and apply scoped fixes before this build.' },
    @{ Page = 'AMC Contracts'; Screen = 'AMC dashboard, Add/Edit AMC, equipment dialog, AMCDetailPage tabs'; Issue = 'AMC surfaces require recurring required-field, badge, card, grid, empty-state, and resize audit.'; Fix = 'Run Codex audit prompt, inspect AMCPage, AddAMCForm, AddAMCEquipmentForm, and AMCDetailPage before this build.' },
    @{ Page = 'Clients'; Screen = 'Client dashboard, New/Edit forms, ClientDetailPage tabs'; Issue = 'Client pages require recurring layout, status, dialog, tab, and required-field audit.'; Fix = 'Run Codex audit prompt, inspect client surfaces, and apply scoped fixes before this build.' },
    @{ Page = 'Contracts'; Screen = 'Contract dashboard and contract editor'; Issue = 'Contract pages require recurring filter, grid, button, and edit-form audit.'; Fix = 'Run Codex audit prompt, inspect ContractManagementForm, and apply scoped fixes before this build.' },
    @{ Page = 'Invoices'; Screen = 'Invoice dashboard and invoice editor'; Issue = 'Invoice pages require recurring wide-form, totals, action, and PDF workflow audit.'; Fix = 'Run Codex audit prompt, inspect InvoiceForm, and apply scoped fixes before this build.' },
    @{ Page = 'Payments'; Screen = 'Payment dashboard, history, and entry form'; Issue = 'Payment pages require recurring chart, history-card, required-field, and action audit.'; Fix = 'Run Codex audit prompt, inspect PaymentForm, and apply scoped fixes before this build.' },
    @{ Page = 'SLA Dashboard'; Screen = 'SLA dashboard'; Issue = 'SLA dashboard requires recurring badge, grid, status, and empty-state audit.'; Fix = 'Run Codex audit prompt, inspect SLADashboardForm, and apply scoped fixes before this build.' },
    @{ Page = 'Quotations'; Screen = 'Quotation dashboard and editor'; Issue = 'Quotation pages require recurring action, line-item, PDF, and dialog audit.'; Fix = 'Run Codex audit prompt, inspect TenderBidForm, and apply scoped fixes before this build.' },
    @{ Page = 'Reports'; Screen = 'Reports form'; Issue = 'Reports page requires recurring card, empty-state, export, and resize audit.'; Fix = 'Run Codex audit prompt, inspect ReportForm, and apply scoped fixes before this build.' },
    @{ Page = 'Settings'; Screen = 'Settings tabs and dialogs'; Issue = 'Settings screens require recurring tabs, destructive prompts, diagnostics, and setup audit.'; Fix = 'Run Codex audit prompt, inspect SettingsForm, and apply scoped fixes before this build.' },
    @{ Page = 'Vendors'; Screen = 'Vendor dashboard, detail, duplicate merge dialog'; Issue = 'Vendor screens require recurring supplier terminology, duplicate flow, badge, and form audit.'; Fix = 'Run Codex audit prompt, inspect VendorForm, and apply scoped fixes before this build.' },
    @{ Page = 'Purchases'; Screen = 'Purchase dashboard, PO editor, receive/bill flows'; Issue = 'Purchase screens require recurring line-item, attachment, destructive prompt, and totals audit.'; Fix = 'Run Codex audit prompt, inspect PurchaseForm, and apply scoped fixes before this build.' },
    @{ Page = 'Inventory'; Screen = 'Inventory dashboard and stock item flows'; Issue = 'Inventory screens require recurring stock badges, duplicate merge, transfer, and import audit.'; Fix = 'Run Codex audit prompt, inspect InventoryForm, and apply scoped fixes before this build.' },
    @{ Page = 'Employees'; Screen = 'Employee form'; Issue = 'Employee screens require recurring field, certification, archive, and grid audit.'; Fix = 'Run Codex audit prompt, inspect EmployeeForm, and apply scoped fixes before this build.' },
    @{ Page = 'Payroll'; Screen = 'Payroll form and payslip flows'; Issue = 'Payroll screens require recurring import, progress, payslip, and button-state audit.'; Fix = 'Run Codex audit prompt, inspect PayrollForm, and apply scoped fixes before this build.' },
    @{ Page = 'Geo Intelligence'; Screen = 'Geo intelligence dispatch page'; Issue = 'Dispatch map/list screens require recurring action, selection, assignment, and empty-state audit.'; Fix = 'Run Codex audit prompt, inspect GeoIntelligenceForm, and apply scoped fixes before this build.' },
    @{ Page = 'Jobs'; Screen = 'Jobs dashboard, New/Edit forms, JobDetailPage tabs'; Issue = 'Job screens require recurring detail-tab, checklist, pipeline, material, and close-flow audit.'; Fix = 'Run Codex audit prompt, inspect job surfaces without changing JobManagementForm unless explicitly required, and apply scoped fixes before this build.' },
    @{ Page = 'Master Data'; Screen = 'Master data form'; Issue = 'Master data screens require recurring table, action, import, and validation audit.'; Fix = 'Run Codex audit prompt, inspect MasterDataForm, and apply scoped fixes before this build.' },
    @{ Page = 'WhatsApp Hub'; Screen = 'WhatsApp hub and quick action dialog'; Issue = 'WhatsApp screens require recurring contact, attachment, dialog, and disabled-state audit.'; Fix = 'Run Codex audit prompt, inspect WhatsAppHubForm and WhatsAppQuickActionDialog, and apply scoped fixes before this build.' },
    @{ Page = 'Tally'; Screen = 'Tally integration form'; Issue = 'Tally screens require recurring import/export, log, prompt, and button-state audit.'; Fix = 'Run Codex audit prompt, inspect TallyIntegrationForm, and apply scoped fixes before this build.' },
    @{ Page = 'Dialogs'; Screen = 'Shared modal/dialog/popup surfaces'; Issue = 'Shared modal surfaces require clipping, button-state, tab-order, and resize verification.'; Fix = 'Run Codex audit prompt, inspect shared modal/dialog surfaces, and apply scoped fixes before this build.' }
)

$allPassed = $true
foreach ($batch in $batches) {
    $passed = Invoke-ReleaseBuild $batch.Page $batch.Screen $batch.Issue $batch.Fix
    if (-not $passed) {
        $allPassed = $false
        break
    }
}

$exe = Join-Path $Root 'SOURCE_CODE\bin\Release\HVAC_Pro_Desktop.exe'
if ($allPassed -and (Test-Path -LiteralPath $exe)) {
    $allPassed = Invoke-CardMenuAudit
}

if ($allPassed -and (Test-Path -LiteralPath $exe)) {
    Write-FrontendLog 'FRONTEND_ENGINEER' 'Release artifact' 'Release executable verification required.' "Verified $exe exists." 'PASS'
    exit 0
}

Write-FrontendLog 'FRONTEND_ENGINEER' 'Release artifact' 'Release executable verification failed.' "Expected $exe after Release build." 'FAIL'
exit 1
