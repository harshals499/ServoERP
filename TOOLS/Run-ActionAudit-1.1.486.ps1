param([string[]]$Screens = @('ClientManagementForm','ContractManagementForm','VendorForm','JobManagementForm','PurchaseForm','PaymentForm','AMCPage','PorterDeliveriesForm','TenderBidForm','InvoiceForm','InventoryForm','EmployeeForm','AttendanceForm','MasterDataForm','ClientImportRepairDialog','SmartImportDuplicateCleanupDialog','PayrollForm','ReportForm','SLADashboardForm','DashboardForm','AMCEdit','ConfirmSummary'))
$ErrorActionPreference = 'Stop'
$auditWorkspace = (Resolve-Path (Join-Path $PSScriptRoot '..')).Path
Set-Location -LiteralPath $auditWorkspace
$auditOutput = Join-Path $auditWorkspace 'TEST_RESULTS\action-audit-1.1.486'
$auditRuntime = Join-Path $auditOutput 'runtime'
New-Item -ItemType Directory -Path $auditRuntime -Force | Out-Null
# A separately restored copy of HVAC_PRO named HVAC_PRO_ActionAudit_20261005 is required.
# The native harness refuses to run against any other database, including HVAC_PRO.
& msbuild SOURCE_CODE\HVAC_Pro_Desktop.csproj /m /p:Configuration=Release /p:Platform=AnyCPU /v:minimal *> (Join-Path $auditOutput 'build.log')
if ($LASTEXITCODE -ne 0) { throw 'Release build failed; see build.log.' }
Copy-Item -LiteralPath SOURCE_CODE\bin\Release\HVAC_Pro_Desktop.exe -Destination (Join-Path $auditRuntime 'HVAC_Pro_Desktop.exe')
Copy-Item -LiteralPath SOURCE_CODE\bin\Release\HVAC_Pro_Desktop.exe.config -Destination (Join-Path $auditRuntime 'ActionAuditHarness.exe.config')
Set-Content -LiteralPath (Join-Path $auditRuntime 'HVACPro.config') -Value '<HVACProConfig><Database><Server>.\SQLEXPRESS</Server><DatabaseName>HVAC_PRO_ActionAudit_20261005</DatabaseName><UseWindowsAuth>true</UseWindowsAuth><ServerRole>LocalSqlServer</ServerRole></Database></HVACProConfig>'
$auditHarness = Join-Path $auditRuntime 'ActionAuditHarness.exe'
& C:\Windows\Microsoft.NET\Framework64\v4.0.30319\csc.exe /nologo /target:winexe "/out:$auditHarness" /reference:SOURCE_CODE\bin\Release\HVAC_Pro_Desktop.exe /reference:System.Windows.Forms.dll /reference:System.Drawing.dll /reference:System.Data.dll TOOLS\ActionAuditHarness.cs
if ($LASTEXITCODE -ne 0) { throw 'Audit harness compilation failed.' }
foreach ($auditScreen in (@('shared') + $Screens)) {
    $auditProcess = Start-Process -FilePath $auditHarness -ArgumentList $auditScreen -WindowStyle Hidden -PassThru
    if (-not $auditProcess.WaitForExit(55000)) {
        Stop-Process -Id $auditProcess.Id
        throw "$auditScreen timed out; it is not verified."
    }
    if ($auditProcess.ExitCode -ne 0) { throw "$auditScreen failed; see its log in $auditOutput." }
    Write-Output "$auditScreen completed. Review screenshots and logs; rendered controls are not proof of every business action."
}
& python TOOLS\Inventory-ActionScreens.py
if ($LASTEXITCODE -ne 0) { throw 'Source inventory generation failed.' }
