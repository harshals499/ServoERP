$ErrorActionPreference='Stop'
$root=Split-Path $PSScriptRoot -Parent
$bin=Join-Path $root 'SOURCE_CODE/bin/Release'
Set-Location $bin
Add-Type -AssemblyName System.Windows.Forms,System.Drawing,System.Data
[Reflection.Assembly]::LoadFrom((Join-Path $bin 'HVAC_Pro_Desktop.exe')) | Out-Null
$resolve=[ResolveEventHandler]{param($sender,$eventArgs) try { $dll=Join-Path (Get-Location) (([Reflection.AssemblyName]::new($eventArgs.Name)).Name+'.dll'); if(Test-Path $dll){return [Reflection.Assembly]::LoadFrom($dll)} } catch { return $null }; return $null}
[AppDomain]::CurrentDomain.add_AssemblyResolve($resolve)
[Windows.Forms.Application]::EnableVisualStyles()
# Suppress background page loading: the preview uses only its explicitly created diagnostic database.

$output=Join-Path $root 'artifacts/restore-jobs'
New-Item -ItemType Directory -Force $output | Out-Null
$dbName='ServoERP_JobsSidebar_519_'+[Guid]::NewGuid().ToString('N')
if($dbName -notmatch '^ServoERP_JobsSidebar_519_[a-f0-9]{32}$'){throw 'Unsafe disposable database name'}
$master=[Data.SqlClient.SqlConnection]::new('Data Source=.\SQLEXPRESS;Initial Catalog=master;Integrated Security=True;TrustServerCertificate=True;Connect Timeout=5;Pooling=False')
$master.Open()
try {
 # Raw ADO.NET is used for DDL in an explicitly generated disposable test database.
 $cmd=$master.CreateCommand(); $cmd.CommandText='CREATE DATABASE ['+$dbName+']'; [void]$cmd.ExecuteNonQuery(); $cmd.Dispose()
} finally {$master.Dispose()}
$testString='Data Source=.\SQLEXPRESS;Initial Catalog='+$dbName+';Integrated Security=True;TrustServerCertificate=True;Connect Timeout=5;Pooling=False'
$db=[Data.SqlClient.SqlConnection]::new($testString); $db.Open()
try {
 $cmd=$db.CreateCommand(); $cmd.CommandText='CREATE TABLE dbo.Jobs (JobNumber nvarchar(80)); CREATE TABLE dbo.JobChecklistTemplates (TemplateId int, JobType nvarchar(80), ItemText nvarchar(500), SortOrder int);'; [void]$cmd.ExecuteNonQuery(); $cmd.Dispose()
} finally {$db.Dispose()}
$dbName | Set-Content (Join-Path $output 'disposable-database.txt')
$property=[HVAC_Pro_Desktop.DAL.DatabaseConnectionFactory].GetProperty('DiagnosticConnectionString',[Reflection.BindingFlags]'NonPublic,Static')
$property.SetValue($null,$testString)
$snapshot=New-Object HVAC_Pro_Desktop.Models.LicenseSnapshot
$snapshot.PlanType=[HVAC_Pro_Desktop.Models.LicensePlanType]::Enterprise
$snapshot.Status=[HVAC_Pro_Desktop.Models.LicenseStatus]::Active
$snapshot.EnabledModules.Add('Dashboard')
$result=New-Object HVAC_Pro_Desktop.Models.LicenseValidationResult; $result.Snapshot=$snapshot; $result.Success=$true
[HVAC_Pro_Desktop.Services.Licensing.LicenseService].GetField('_lastValidation',[Reflection.BindingFlags]'NonPublic,Static').SetValue($null,$result)
$user=New-Object HVAC_Pro_Desktop.Models.AppUserDto
$user.UserId=1; $user.Username='jobs-preview'; $user.DisplayName='Jobs Preview'; $user.RoleName='Admin'; $user.IsActive=$true
[HVAC_Pro_Desktop.Services.SessionManager]::SetSession($user,$null,$null)
$main=New-Object HVAC_Pro_Desktop.UI.MainForm
$page=New-Object HVAC_Pro_Desktop.UI.JobManagementForm
# Stop this preview's newly queued timer synchronously in Load before message pumping.
$page.add_Load({
 $timer=[HVAC_Pro_Desktop.UI.JobManagementForm].GetField('_initialJobsLoadTimer',[Reflection.BindingFlags]'NonPublic,Instance').GetValue($page)
 if($timer){$timer.Stop()}
})
$hostForm=New-Object Windows.Forms.Form; $hostForm.ClientSize=[Drawing.Size]::new(1360,850)
$sidebar=$main.GetType().GetField('_sidebar',[Reflection.BindingFlags]'NonPublic,Instance').GetValue($main)
$main.Controls.Remove($sidebar); $hostForm.Controls.Add($page); $hostForm.Controls.Add($sidebar)
function Capture($name) { $hostForm.Refresh(); [Windows.Forms.Application]::DoEvents(); $bitmap=[Drawing.Bitmap]::new($hostForm.Width,$hostForm.Height); try{$hostForm.DrawToBitmap($bitmap,[Drawing.Rectangle]::new(0,0,$hostForm.Width,$hostForm.Height));$bitmap.Save((Join-Path $output $name))}finally{$bitmap.Dispose()} }
function Descendants($control) { foreach($child in $control.Controls){$child; Descendants $child} }
try {
 $hostForm.Show(); [Windows.Forms.Application]::DoEvents()
 $add=@(Descendants $page | Where-Object {$_ -is [Windows.Forms.Button] -and $_.Text -eq '+ Add New Job' -and $_.Visible})
 if(!$add.Count){throw 'Jobs dashboard has no visible Add New Job action'}
 Capture 'jobs-dashboard.png'
 $method=[HVAC_Pro_Desktop.UI.JobManagementForm].GetMethod('BeginNewJobAsync',[Reflection.BindingFlags]'NonPublic,Instance')
 $task=$method.Invoke($page,@()); [void]$task.GetAwaiter().GetResult()
 $title=[HVAC_Pro_Desktop.UI.JobManagementForm].GetField('_txtJobTitle',[Reflection.BindingFlags]'NonPublic,Instance').GetValue($page)
 if(!$title.Visible -or !$title.Enabled){throw 'New Job draft editor did not become editable'}
 Capture 'new-job-editor.png'
 $db=[Data.SqlClient.SqlConnection]::new($testString);$db.Open()
 try{$cmd=$db.CreateCommand();$cmd.CommandText='SELECT COUNT(*) FROM dbo.Jobs';if([int]$cmd.ExecuteScalar() -ne 0){throw 'Draft preview unexpectedly saved a job'};$cmd.Dispose()}finally{$db.Dispose()}
 'PASS: Jobs dashboard exposes Add New Job; real BeginNewJobAsync opens an editable draft against an identified disposable SQL database; no job saved.'
} finally {
 $hostForm.Close();$hostForm.Dispose();$main.Dispose();$property.SetValue($null,$null)
}
