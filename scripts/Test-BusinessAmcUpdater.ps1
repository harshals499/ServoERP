param(
 [string]$PackageDirectory,
 [string]$ExpectedVersion = '1.1.518',
 [string]$CurrentVersion = '1.1.517',
 [switch]$Download
)
$ErrorActionPreference='Stop'
$root=Split-Path $PSScriptRoot -Parent
$bin=Join-Path $root 'SOURCE_CODE/bin/Release'
[Reflection.Assembly]::LoadFrom((Join-Path $bin 'Velopack.dll')) | Out-Null
$packages=Join-Path $root ('artifacts/jobs-business-amc/updater-test-' + [Guid]::NewGuid().ToString('N'))
New-Item -ItemType Directory -Path $packages | Out-Null
$locator=[Velopack.Locators.TestVelopackLocator]::new('ServoERP.Desktop',$CurrentVersion,$packages,$null)
if($PackageDirectory) {
 $source=[Velopack.Sources.SimpleFileSource]::new([IO.DirectoryInfo](Resolve-Path $PackageDirectory).Path)
} else {
 $source=[Velopack.Sources.GithubSource]::new('https://github.com/harshals499/ServoERP-Releases',$null,$false,$null)
}
$manager=[Velopack.UpdateManager]::new($source,$null,$locator)
$update=$manager.CheckForUpdates()
if(!$update -or $update.TargetFullRelease.Version.ToString() -ne $ExpectedVersion){throw 'Updater did not detect the expected newer release'}
if([string]::IsNullOrWhiteSpace($update.TargetFullRelease.NotesMarkdown) -or $update.TargetFullRelease.NotesMarkdown -notmatch ([regex]::Escape("# ServoERP $ExpectedVersion"))){throw 'Updater release notes are missing/mismatched'}
"PASS: Velopack detects $CurrentVersion -> $ExpectedVersion with version-matched notes."
if($Download) {
 $manager.DownloadUpdates($update,$null)
 $package=Get-ChildItem $packages -Filter $update.TargetFullRelease.FileName -File -Recurse | Select-Object -First 1
 if(!$package){throw 'Updater package download was not retained'}
 $expectedHash=$update.TargetFullRelease.SHA256
 if([string]::IsNullOrWhiteSpace($expectedHash)){throw 'Remote updater package SHA256 absent'}
 if((Get-FileHash $package.FullName -Algorithm SHA256).Hash -ne $expectedHash){throw 'Updater package SHA256 mismatch'}
 "PASS: updater downloaded and verified $($package.Name); no installed application was modified."
}
