param(
    [Parameter(Mandatory=$true)][ValidatePattern('^ServoERP_MarathiQA_[A-Za-z0-9_]+$')][string]$TestDatabase,
    [string]$OutputDirectory
)
$ErrorActionPreference='Stop'
$root=Split-Path $PSScriptRoot -Parent
if(!$OutputDirectory){$OutputDirectory=Join-Path $root 'artifacts/banner-dismiss'}
New-Item -ItemType Directory -Force -Path $OutputDirectory | Out-Null
$build=Join-Path $root 'SOURCE_CODE/bin/Release'
& msbuild (Join-Path $root 'SOURCE_CODE/HVAC_Pro_Desktop.csproj') /p:Configuration=Release /verbosity:minimal /nologo *> (Join-Path $OutputDirectory 'build.log')
if($LASTEXITCODE){throw 'Release build failed'}
$csc=Join-Path ${env:ProgramFiles(x86)} 'Microsoft Visual Studio/2022/BuildTools/MSBuild/Current/Bin/Roslyn/csc.exe'
$test=Join-Path $OutputDirectory 'BannerTest.exe'
& $csc /nologo /platform:x64 "/out:$test" "/reference:$build/HVAC_Pro_Desktop.exe" /reference:System.Windows.Forms.dll /reference:System.Drawing.dll /reference:System.Data.dll (Join-Path $PSScriptRoot 'Tests/DatabaseStatusBannerTests.cs')
if($LASTEXITCODE){throw 'Banner harness compile failed'}
# Do not run Program.Main (it closes other app instances), or copy private connection strings.
$cfg=[xml](Get-Content -LiteralPath "$build/HVAC_Pro_Desktop.exe.config")
$copy=[xml]'<configuration />'
[void]$copy.DocumentElement.AppendChild($copy.ImportNode($cfg.configuration.runtime,$true))
$copy.Save("$test.config")
& $test $OutputDirectory $build $TestDatabase *> (Join-Path $OutputDirectory results.txt)
if($LASTEXITCODE){Get-Content (Join-Path $OutputDirectory results.txt) -Tail 8;throw 'Banner tests failed'}
Get-Content (Join-Path $OutputDirectory results.txt) -Tail 1
