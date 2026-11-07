param([string]$OutputDirectory)
$ErrorActionPreference = 'Stop'
$repoRoot = Split-Path $PSScriptRoot -Parent
if (!$OutputDirectory) { $OutputDirectory = Join-Path $repoRoot 'artifacts\marathi-localization' }
New-Item -ItemType Directory -Path $OutputDirectory -Force | Out-Null
$project = Join-Path $repoRoot 'SOURCE_CODE\HVAC_Pro_Desktop.csproj'
$buildOutput = Join-Path $repoRoot 'SOURCE_CODE\bin\Release'
$buildLog = Join-Path $OutputDirectory 'release-build.log'
& msbuild $project /p:Configuration=Release /verbosity:minimal /nologo *> $buildLog
if ($LASTEXITCODE -ne 0) { throw "Release build failed. See $buildLog" }
$appExe = Join-Path $buildOutput 'HVAC_Pro_Desktop.exe'
& (Join-Path $PSScriptRoot 'Assert-ServoLanguageResources.ps1') -BuildOutput $buildOutput
if (!(Test-Path -LiteralPath $appExe)) { throw 'Release executable is missing.' }
$csc = Join-Path ${env:ProgramFiles(x86)} 'Microsoft Visual Studio\2022\BuildTools\MSBuild\Current\Bin\Roslyn\csc.exe'
if (!(Test-Path -LiteralPath $csc)) { throw 'Visual Studio 2022 Build Tools C# compiler is required.' }
$testExe = Join-Path $OutputDirectory 'MarathiLocalizationTests.exe'
& $csc /nologo /target:exe /platform:x64 "/out:$testExe" "/reference:$appExe" /reference:System.Windows.Forms.dll /reference:System.Drawing.dll /reference:System.Core.dll /reference:System.Data.dll (Join-Path $repoRoot 'SOURCE_CODE\Tests\MarathiLocalizationTests.cs')
if ($LASTEXITCODE -ne 0) { throw 'Localization harness compilation failed.' }
# Copy only assembly redirects. Never copy connection strings or invoke the normal app entry
# point, which would stop the user's running app and initialize the business database.
$configuration = [xml](Get-Content -LiteralPath "$appExe.config")
$testConfiguration = [xml]'<configuration />'
[void]$testConfiguration.DocumentElement.AppendChild($testConfiguration.ImportNode($configuration.configuration.runtime, $true))
$testConfiguration.Save("$testExe.config")
$report = Join-Path $OutputDirectory 'regression.txt'
& $testExe $OutputDirectory $buildOutput *> $report
if ($LASTEXITCODE -ne 0) { Get-Content -LiteralPath $report -Tail 20; throw 'Localization regressions failed.' }
Get-Content -LiteralPath $report -Tail 1
Write-Output "Evidence: $OutputDirectory"
