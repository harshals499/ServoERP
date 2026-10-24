$ErrorActionPreference='Stop'
$root=Split-Path $PSScriptRoot -Parent
$bin=Join-Path $root 'SOURCE_CODE/bin/Release'
Set-Location $bin
Add-Type -AssemblyName System.Windows.Forms,System.Drawing
$assembly=[Reflection.Assembly]::LoadFrom((Join-Path $bin 'HVAC_Pro_Desktop.exe'))
$resolve=[ResolveEventHandler]{param($sender,$eventArgs) try{$dll=Join-Path (Get-Location) (([Reflection.AssemblyName]::new($eventArgs.Name)).Name+'.dll');if(Test-Path $dll){return [Reflection.Assembly]::LoadFrom($dll)}}catch{};return $null}
[AppDomain]::CurrentDomain.add_AssemblyResolve($resolve)
Add-Type -TypeDefinition 'using System.Runtime.InteropServices; public static class SyncBannerNative { [DllImport("kernel32.dll", CharSet=CharSet.Unicode)] public static extern bool SetDllDirectory(string path); }'
[void][SyncBannerNative]::SetDllDirectory((Join-Path $bin 'x64'))
[Windows.Forms.Application]::EnableVisualStyles()
$output=Join-Path $root 'artifacts/sync-banner'
New-Item -ItemType Directory -Force $output | Out-Null
$flags=[Reflection.BindingFlags]'Public,NonPublic,Static'
$instanceFlags=[Reflection.BindingFlags]'Public,NonPublic,Instance'
$coordinator=$assembly.GetType('HVAC_Pro_Desktop.OfflineDesktop.OfflineDesktopCoordinator')
$storeType=$assembly.GetType('HVAC_Pro_Desktop.OfflineDesktop.OfflineWorkspaceStore')
$office=[Guid]::NewGuid()
$cache=Join-Path $output ('test-cache-'+[Guid]::NewGuid().ToString('N'))
$coordinator.GetProperty('TestOfficeId',$flags).SetValue($null,$office)
$coordinator.GetProperty('TestDirectory',$flags).SetValue($null,$cache)
$coordinator.GetProperty('TestServerKey',$flags).SetValue($null,'synthetic-sync-banner')
$diagnostic=[HVAC_Pro_Desktop.DAL.DatabaseConnectionFactory].GetProperty('DiagnosticConnectionString',$flags)
$diagnostic.SetValue($null,'Data Source=127.0.0.1,1;Initial Catalog=ServoERP_SyncBanner_Diagnostic;Integrated Security=True;Connect Timeout=1;Pooling=False')
$constructor=$storeType.GetConstructor([Type[]]@([string],[Guid],[string],[int]))
$store=$constructor.Invoke([object[]]@([string]$cache,[Guid]$office,[string]'synthetic-sync-banner',[int]1))
$coordinator.GetProperty('Store',$flags).SetValue($null,$store)
$user=New-Object HVAC_Pro_Desktop.Models.AppUserDto
$user.UserId=1;$user.Username='sync-preview';$user.DisplayName='Sync Preview';$user.RoleName='Admin';$user.IsActive=$true
[HVAC_Pro_Desktop.Services.SessionManager]::SetSession($user,$null,$null)
$license=New-Object HVAC_Pro_Desktop.Models.LicenseSnapshot
$license.PlanType=[HVAC_Pro_Desktop.Models.LicensePlanType]::Enterprise;$license.Status=[HVAC_Pro_Desktop.Models.LicenseStatus]::Active
$result=New-Object HVAC_Pro_Desktop.Models.LicenseValidationResult;$result.Success=$true;$result.Snapshot=$license
[HVAC_Pro_Desktop.Services.Licensing.LicenseService].GetField('_lastValidation',$flags).SetValue($null,$result)
$online=New-Object HVAC_Pro_Desktop.Services.DatabaseConnectionStateSnapshot
$online.State=[HVAC_Pro_Desktop.Services.DatabaseConnectionStateKind]::Online;$online.BusinessWritesAllowed=$true
$service=[HVAC_Pro_Desktop.Services.DatabaseConnectionStateService]
$service.GetField('_snapshot',$flags).SetValue($null,$online)
$main=New-Object HVAC_Pro_Desktop.UI.MainForm
$main.WindowState=[Windows.Forms.FormWindowState]::Normal
$hostForm=New-Object Windows.Forms.Form;$hostForm.ClientSize=[Drawing.Size]::new(1120,760)
$main.ClientSize=$hostForm.ClientSize
[void]$main.Handle
# Display the real shell controls without MainForm.Shown, which starts unrelated services.
foreach($control in @($main.Controls)){$hostForm.Controls.Add($control)}
$banner=$main.GetType().GetField('_pnlDatabaseStatusBanner',$instanceFlags).GetValue($main)
$content=$main.GetType().GetField('_content',$instanceFlags).GetValue($main)
$refresh=$main.GetType().GetMethod('RefreshDatabaseStatusBanner',$instanceFlags)
$announce=$coordinator.GetMethod('Announce',$flags)
$status=$coordinator.GetProperty('Status',$flags)
$timerField=$main.GetType().GetField('_lastSyncNotificationTimer',$instanceFlags)
$success='Last sync 24/10/2026 18:00. 0 pending; 0 need review.'
function PumpFor([int]$milliseconds){$clock=[Diagnostics.Stopwatch]::StartNew();while($clock.ElapsedMilliseconds -lt $milliseconds){[Windows.Forms.Application]::DoEvents();Start-Sleep -Milliseconds 10}}
function Until([Diagnostics.Stopwatch]$clock,[int]$milliseconds){while($clock.ElapsedMilliseconds -lt $milliseconds){[Windows.Forms.Application]::DoEvents();Start-Sleep -Milliseconds 10}}
function Capture([string]$name){$hostForm.Refresh();[Windows.Forms.Application]::DoEvents();$bitmap=[Drawing.Bitmap]::new($hostForm.Width,$hostForm.Height);try{$hostForm.DrawToBitmap($bitmap,[Drawing.Rectangle]::new(0,0,$hostForm.Width,$hostForm.Height));$bitmap.Save((Join-Path $output $name))}finally{$bitmap.Dispose()}}
try{
 $hostForm.Show();[Windows.Forms.Application]::DoEvents()
 $status.SetValue($null,$success);[void]$refresh.Invoke($main,[object[]]@($online.PSObject.BaseObject));$clock=[Diagnostics.Stopwatch]::StartNew()
 if(!$banner.Visible){throw 'Completed sync notice not visible'}
 if($timerField.GetValue($main).Interval -ne 5000){throw 'Notice interval is not five seconds'}
 $topBefore=$content.Top;Capture 'sync-visible.png'
 Until $clock 2000;[void]$refresh.Invoke($main,[object[]]@($online.PSObject.BaseObject))
 Until $clock 4000;[void]$refresh.Invoke($main,[object[]]@($online.PSObject.BaseObject))
 Until $clock 4500;if(!$banner.Visible){throw 'Sync notice hid too early'}
 while($banner.Visible -and $clock.ElapsedMilliseconds -lt 6500){[Windows.Forms.Application]::DoEvents();Start-Sleep -Milliseconds 10}
 $elapsed=$clock.ElapsedMilliseconds
 if($banner.Visible -or $elapsed -lt 4900){throw "Unexpected auto-hide timing $elapsed ms"}
 if(($topBefore-$content.Top) -ne $banner.Height){throw 'Hidden banner did not reclaim page height'}
 Capture 'sync-hidden.png'
 [void]$refresh.Invoke($main,[object[]]@($online.PSObject.BaseObject));if($banner.Visible){throw 'Health refresh redisplayed an expired notice'}
 "PASS: sync hides after $elapsed ms despite health refreshes; layout reclaims $($banner.Height)px; dismissed notice stays hidden."
 [void]$announce.Invoke($null,@($success));PumpFor 100
 if(!$banner.Visible -or !$timerField.GetValue($main).Enabled){throw 'Identical new completion did not restart the notice'}
 'PASS: new completion with the same displayed timestamp starts a fresh notice.'
 $warning=$online.Clone();$warning.State=[HVAC_Pro_Desktop.Services.DatabaseConnectionStateKind]::Offline;$warning.BusinessWritesAllowed=$false
 $service.GetField('_snapshot',$flags).SetValue($null,$warning)
 [void]$announce.Invoke($null,@('Office server is offline. Pending work is retained.'));PumpFor 100
 PumpFor 5300
 if(!$banner.Visible -or $timerField.GetValue($main).Enabled){throw 'Offline warning was hidden by the previous success timer'}
 Capture 'offline-warning.png'
 foreach($kind in @('Degraded','Reconnecting','ConfigError')){
  $warning.State=[Enum]::Parse([HVAC_Pro_Desktop.Services.DatabaseConnectionStateKind],$kind)
  [void]$refresh.Invoke($main,[object[]]@($warning.PSObject.BaseObject))
  if(!$banner.Visible -or $timerField.GetValue($main).Enabled){throw "$kind warning must persist"}
 }
 'PASS: offline/degraded/reconnecting/configuration warnings preempt the transient timer and remain visible.'
 $service.GetField('_snapshot',$flags).SetValue($null,$online)
 [void]$announce.Invoke($null,@('Sync waiting: office review is required.'));PumpFor 100;PumpFor 5300
 if(!$banner.Visible -or $timerField.GetValue($main).Enabled){throw 'Sync-failure warning expired'}
 'PASS: sync-failure notice remains visible beyond five seconds.'
 [void]$announce.Invoke($null,@($success));PumpFor 100
 if(!$timerField.GetValue($main).Enabled){throw 'No active timer for disposal check'}
 $hostForm.Close();$main.Dispose()
 if($timerField.GetValue($main) -ne $null){throw 'Timer not disposed with MainForm'}
 'PASS: closing MainForm disposes its notification timer.'
}finally{
 $hostForm.Dispose();if(!$main.IsDisposed){$main.Dispose()}
 $diagnostic.SetValue($null,$null)
}
