param(
    [string]$Root = (Split-Path $PSScriptRoot -Parent),
    [ValidateSet('Policies','Screenshots')][string]$Mode = 'Policies',
    [string]$Database = 'HVAC_PRO_UI_20261014',
    [string[]]$Pages = @('DashboardForm','TenderBidForm','InvoiceForm','PaymentForm','PurchaseForm','InventoryForm','ClientManagementForm','VendorForm','ContractManagementForm','JobManagementForm','EmployeeForm','PayrollForm','AttendanceForm','AMCPage','MasterDataForm','ReportForm','SettingsForm','GeoIntelligenceForm','UserGuideForm','ClientDetailPage','JobDetailPage','AddAMCForm','AddAMCEquipmentForm','ConnectionSetupForm')
)
$ErrorActionPreference = 'Stop'
if ([Threading.Thread]::CurrentThread.ApartmentState -ne 'STA') { throw 'Run with powershell -STA.' }
if ($Database -notmatch '^HVAC_PRO_UI_[A-Za-z0-9_]+$') { throw 'An explicitly disposable UI database is required.' }
Add-Type -AssemblyName System.Windows.Forms
Add-Type -AssemblyName System.Drawing
$appDir = Join-Path $Root 'SOURCE_CODE/bin/Release'
Add-Type -TypeDefinition @'
using System;
using System.IO;
using System.Reflection;
public static class ServoUiDependencyResolver {
    public static string DirectoryPath;
    public static void Install(string path) {
        DirectoryPath = path;
        AppDomain.CurrentDomain.AssemblyResolve += (sender,args) => {
            string file = Path.Combine(DirectoryPath, new AssemblyName(args.Name).Name + ".dll");
            return File.Exists(file) ? Assembly.LoadFrom(file) : null;
        };
    }
}
'@
[ServoUiDependencyResolver]::Install($appDir)
$assembly = [Reflection.Assembly]::LoadFrom((Join-Path $appDir 'HVAC_Pro_Desktop.exe'))
$quest = [Reflection.Assembly]::LoadFrom((Join-Path $appDir 'QuestPDF.dll'))
$licenseType = $quest.GetType('QuestPDF.Infrastructure.LicenseType')
$quest.GetType('QuestPDF.Settings').GetProperty('License').SetValue($null,[Enum]::Parse($licenseType,'Community'),$null)
[Environment]::CurrentDirectory = $appDir
$env:GITHUB_WORKSPACE = $Root
$flags = [Reflection.BindingFlags]'Static,Public,NonPublic'
$factory = $assembly.GetType('HVAC_Pro_Desktop.DAL.DatabaseConnectionFactory')
$factory.GetProperty('DiagnosticConnectionString',$flags).SetValue($null,"Data Source=.\SQLEXPRESS;Initial Catalog=$Database;Integrated Security=True;TrustServerCertificate=True", $null)
[Windows.Forms.Application]::EnableVisualStyles()
$assembly.GetType('ServoERP.Infrastructure.ServoTheme').GetMethod('Initialize',$flags).Invoke($null,@()) | Out-Null
$output = Join-Path $Root 'TEST_RESULTS/quotation-consistency'
New-Item -ItemType Directory -Force -Path $output | Out-Null
if ($Mode -eq 'Policies') {
    $program = $assembly.GetType('HVAC_Pro_Desktop.Program')
    $method = $program.GetMethods($flags) | Where-Object { $_.Name -eq 'WriteCiSmokeReport' }
    if (!$method) { throw 'CI-safe test entrypoint missing.' }
    $report = $method.Invoke($null,@())
    Copy-Item -LiteralPath $report -Destination (Join-Path $output 'ci-safe.txt') -Force
    $text = Get-Content -LiteralPath $report -Raw
    Write-Output $text
    if ($text -match '(?m)^FAIL ') { throw 'CI-safe policies failed.' }
    exit
}
$user = [Activator]::CreateInstance($assembly.GetType('HVAC_Pro_Desktop.Models.AppUserDto'))
$user.UserId=1; $user.Username='ui-test-admin'; $user.DisplayName='UI Test Admin'; $user.RoleId=1; $user.RoleName='Admin'; $user.IsActive=$true
$session = $assembly.GetType('HVAC_Pro_Desktop.Services.SessionManager')
$session.GetMethod('SetSession',$flags).Invoke($null,@($user,$null,$null)) | Out-Null
$policy = $assembly.GetType('HVAC_Pro_Desktop.UI.QuotationVisualPolicy').GetMethod('Apply',$flags)
function Pump([int]$ms) {
    $until=[DateTime]::UtcNow.AddMilliseconds($ms)
    while ([DateTime]::UtcNow -lt $until) { [Windows.Forms.Application]::DoEvents(); Start-Sleep -Milliseconds 20 }
}
$lines = New-Object 'System.Collections.Generic.List[string]'
foreach ($pageName in $Pages) {
    $control=$null; $captureForm=$null; $bitmap=$null
    try {
        $type=$assembly.GetType("HVAC_Pro_Desktop.UI.$pageName")
        if (!$type) { throw "Missing page $pageName" }
        if ($pageName -eq 'AddAMCEquipmentForm') { $control=[Activator]::CreateInstance($type,@([int]1)) }
        else { $control=[Activator]::CreateInstance($type) }
        if ($pageName -eq 'DashboardForm') { $control.LoadActionCenterPreviewForVisualTest() }
        if ($pageName -eq 'TenderBidForm') { $control.LoadActionBoardPreviewForVisualTest() }
        if ($pageName -eq 'ClientDetailPage') { $control.LoadClient(122) }
        if ($pageName -eq 'JobDetailPage') { $control.JobId=1; $control.LoadJob() }
        if ($control -is [Windows.Forms.Form]) { $captureForm=$control }
        else {
            $captureForm=New-Object Windows.Forms.Form
            $control.Dock='Fill'; $captureForm.Controls.Add($control)
        }
        $captureForm.ShowInTaskbar=$false; $captureForm.StartPosition='Manual'
        $captureForm.Location=New-Object Drawing.Point -10000,-10000
        if ($control -isnot [Windows.Forms.Form]) { $captureForm.ClientSize=New-Object Drawing.Size 1710,1032 }
        $captureForm.Show();
        if ($control.PSObject.Methods["OnShellActivated"]) { $control.OnShellActivated() }
        Pump 2200
        Pump 350
        $bitmap=New-Object Drawing.Bitmap $captureForm.Width,$captureForm.Height
        $captureForm.DrawToBitmap($bitmap,(New-Object Drawing.Rectangle 0,0,$bitmap.Width,$bitmap.Height))
        $path=Join-Path $output "$pageName.png"
        $bitmap.Save($path,[Drawing.Imaging.ImageFormat]::Png)
        $lines.Add("PASS $pageName : $path")
        Write-Output "PASS $pageName"
    }
    catch { $lines.Add("FAIL $pageName : $_"); Write-Output "FAIL $pageName : $_" }
    finally {
        if ($bitmap) { $bitmap.Dispose() }
        if ($captureForm) { $captureForm.Close(); $captureForm.Dispose() }
        if ($control -and $control -ne $captureForm) { $control.Dispose() }
    }
}
$lines | Set-Content -LiteralPath (Join-Path $output 'screenshots.txt')
if (@($lines | Where-Object { $_ -match '^FAIL' }).Count) { throw 'Screenshot captures failed; see screenshots.txt.' }

