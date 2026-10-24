param([ValidateSet('Basic','Standard','Pro','Enterprise')][string]$Plan = 'Enterprise', [int]$PreviewHeight = 900)
$ErrorActionPreference='Stop'
$root=Split-Path $PSScriptRoot -Parent
Set-Location (Join-Path $root 'SOURCE_CODE/bin/Release')
Add-Type -AssemblyName System.Windows.Forms,System.Drawing
$assembly=[Reflection.Assembly]::LoadFrom((Join-Path $pwd 'HVAC_Pro_Desktop.exe'))
$resolve = [ResolveEventHandler] {
 param($sender,$eventArgs)
 $dll = Join-Path (Get-Location) ((New-Object Reflection.AssemblyName($eventArgs.Name)).Name + '.dll')
 if (Test-Path $dll) { return [Reflection.Assembly]::LoadFrom($dll) }
 return $null
}
[AppDomain]::CurrentDomain.add_AssemblyResolve($resolve)
[Windows.Forms.Application]::EnableVisualStyles()
$snapshot=New-Object HVAC_Pro_Desktop.Models.LicenseSnapshot
$snapshot.PlanType=[Enum]::Parse([HVAC_Pro_Desktop.Models.LicensePlanType],$Plan)
$snapshot.PlanName=[HVAC_Pro_Desktop.Services.Licensing.LicensePlanCatalog]::GetDisplayName($snapshot.PlanType)
$snapshot.Status=[HVAC_Pro_Desktop.Models.LicenseStatus]::Active
$snapshot.EnabledModules.Add('Dashboard')
$result=New-Object HVAC_Pro_Desktop.Models.LicenseValidationResult
$result.Snapshot=$snapshot
$result.Success=$true
[HVAC_Pro_Desktop.Services.Licensing.LicenseService].GetField('_lastValidation',[Reflection.BindingFlags]'NonPublic,Static').SetValue($null,$result)
$user=New-Object HVAC_Pro_Desktop.Models.AppUserDto
$user.UserId=1; $user.Username='sidebar-preview'; $user.DisplayName='Sidebar Preview'; $user.RoleName='Admin'; $user.IsActive=$true
[HVAC_Pro_Desktop.Services.SessionManager]::SetSession($user,$null,$null)
try { $main=New-Object HVAC_Pro_Desktop.UI.MainForm } catch { Write-Output $_.Exception.ToString(); throw }
$sidebar=$main.GetType().GetField('_sidebar',[Reflection.BindingFlags]'NonPublic,Instance').GetValue($main)
$main.Controls.Remove($sidebar)
$hostForm=New-Object Windows.Forms.Form
$hostForm.ClientSize=New-Object Drawing.Size(340,$PreviewHeight)
$hostForm.Controls.Add($sidebar)
$hostForm.Show()
[Windows.Forms.Application]::DoEvents()
function Find-Jobs($control) {
 if ($control.AccessibleName -eq 'Jobs' -and $control.Visible) { return $true }
 foreach ($child in $control.Controls) { if (Find-Jobs $child) { return $true } }
 return $false
}
if (!(Find-Jobs $sidebar)) { throw 'Jobs is absent from rendered Business AMC sidebar' }
function Get-Descendants($control) {
 foreach($child in $control.Controls) { $child; Get-Descendants $child }
}
$all=@(Get-Descendants $sidebar)
$heading=@($all | Where-Object {$_ -is [Windows.Forms.Label] -and $_.Text -eq 'Jobs'})
$button=@($all | Where-Object {$_ -is [Windows.Forms.Button] -and $_.AccessibleName -eq 'Jobs'})
if($heading.Count -ne 1 -or $button.Count -ne 1){throw 'Exactly one dedicated Jobs heading and entry are required'}
$headingTop=$heading[0].PointToScreen([Drawing.Point]::Empty).Y
$buttonTop=$button[0].PointToScreen([Drawing.Point]::Empty).Y
$dashboard=@($all | Where-Object {$_ -is [Windows.Forms.Button] -and $_.AccessibleName -eq 'Dashboard'})
if($headingTop -ge $buttonTop -or $headingTop -le $dashboard[0].PointToScreen([Drawing.Point]::Empty).Y){throw 'Jobs section must follow Dashboard'}
if(!$hostForm.RectangleToScreen($hostForm.ClientRectangle).Contains($button[0].RectangleToScreen($button[0].ClientRectangle))){throw 'Jobs entry is clipped from the visible sidebar'}
$bitmap=New-Object Drawing.Bitmap($hostForm.Width,$hostForm.Height)
$hostForm.DrawToBitmap($bitmap,(New-Object Drawing.Rectangle(0,0,$hostForm.Width,$hostForm.Height)))
$output = Join-Path $root 'artifacts/restore-jobs'
New-Item -ItemType Directory -Path $output -Force | Out-Null
$bitmap.Save((Join-Path $output ("sidebar-{0}-{1}.png" -f $Plan,$PreviewHeight)))
$bitmap.Dispose(); $hostForm.Close(); $main.Dispose()
"PASS: built $Plan sidebar renders Jobs with a partial cached module list at height $PreviewHeight."



