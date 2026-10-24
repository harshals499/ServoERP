#requires -Version 7.2
$ErrorActionPreference='Stop'
$root=Split-Path $PSScriptRoot -Parent
Add-Type -Path @((Join-Path $root 'SOURCE_CODE/MODELS/LicenseModels.cs'),(Join-Path $root 'SOURCE_CODE/SERVICES/Licensing/LicenseFeatureCatalog.cs'))
$catalog=[HVAC_Pro_Desktop.Services.Licensing.LicenseFeatureCatalog]
foreach($planName in @('Basic','Standard','Pro','Enterprise')) {
 $snapshot=New-Object HVAC_Pro_Desktop.Models.LicenseSnapshot
 $snapshot.PlanType=[Enum]::Parse([HVAC_Pro_Desktop.Models.LicensePlanType],$planName)
 $snapshot.Status=[HVAC_Pro_Desktop.Models.LicenseStatus]::Active
 $snapshot.EnabledModules.Add('Dashboard')
 if(!$catalog::IsModuleEnabled($snapshot,'WorkOrders')){throw "Jobs missing with incomplete cache on $planName"}
 foreach($status in @('Missing','Tampered')){
  $snapshot.Status=[Enum]::Parse([HVAC_Pro_Desktop.Models.LicenseStatus],$status)
  if($catalog::IsModuleEnabled($snapshot,'WorkOrders')){throw "Jobs granted for $status license on $planName"}
 }
}
$trial=New-Object HVAC_Pro_Desktop.Models.LicenseSnapshot
$trial.PlanType=[HVAC_Pro_Desktop.Models.LicensePlanType]::Trial
if($catalog::IsModuleEnabled($trial,'WorkOrders')){throw 'Default trial entitlements expanded'}
$shell=Get-Content (Join-Path $root 'SOURCE_CODE/UI/MainForm.cs') -Raw
if($shell -notmatch 'JobsItems = \{ JobsPageIndex \}'){throw 'Dedicated Jobs group missing'}
if($shell -notmatch 'AddNavGroup\(LanguageManager.Get\("Jobs"\), JobsItems, true\)'){throw 'Jobs group not rendered'}
if($shell -match 'OperationsItems = \{[^}]*JobsPageIndex'){throw 'Jobs still nested in Operations'}
'PASS: dedicated Jobs group and all paid-plan Jobs entitlements with incomplete caches; missing/tampered/trial restrictions retained.'
