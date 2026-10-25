#requires -Version 7.2
$ErrorActionPreference = 'Stop'
$root = Split-Path $PSScriptRoot -Parent
Add-Type -Path @((Join-Path $root 'SOURCE_CODE/MODELS/LicenseModels.cs'), (Join-Path $root 'SOURCE_CODE/SERVICES/Licensing/LicensePlanCatalog.cs'), (Join-Path $root 'SOURCE_CODE/SERVICES/Licensing/LicenseFeatureCatalog.cs'))
$catalog = [HVAC_Pro_Desktop.Services.Licensing.LicenseFeatureCatalog]
$plan = [HVAC_Pro_Desktop.Models.LicensePlanType]
$snapshot = New-Object HVAC_Pro_Desktop.Models.LicenseSnapshot
$snapshot.PlanType = $plan::Enterprise
$snapshot.Status = [HVAC_Pro_Desktop.Models.LicenseStatus]::Active
$snapshot.EnabledModules.Add('Dashboard')
foreach ($module in $catalog::GetModulesForPlan($plan::Pro)) {
    if (!$catalog::IsModuleEnabled($snapshot, $module)) { throw "Business AMC missing Pro module: $module" }
}
if ($catalog::IsModuleEnabled($snapshot, 'UnknownModule')) { throw 'Unknown module granted' }
$snapshot.PlanType = $plan::Basic
if ($catalog::IsModuleEnabled($snapshot, 'Payroll')) { throw 'Restricted Basic cache was expanded' }
if ($catalog::IsModuleEnabled($null, 'WorkOrders')) { throw 'Missing license granted Jobs' }
if ([HVAC_Pro_Desktop.Services.Licensing.LicensePlanCatalog]::ParsePlan('Business AMC') -ne $plan::Enterprise) { throw 'Business AMC mapping changed' }
$shell = Get-Content (Join-Path $root 'SOURCE_CODE/UI/MainForm.cs') -Raw
if ($shell -notmatch 'OperationsItems = \{[^}]*JobsPageIndex') { throw 'Jobs missing from Operations' }
if ($shell -notmatch 'case 15: return "WorkOrders"') { throw 'Jobs entitlement mapping changed' }
'PASS: Business AMC includes all Pro modules with partial cache; unknown/lower-tier/missing-license checks; Jobs sidebar registration.'
