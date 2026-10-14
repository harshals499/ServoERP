param(
    [string]$AndroidSdkDirectory = "$env:LOCALAPPDATA\Android\ServoEngineerSdk",
    [string]$JavaSdkDirectory = "$env:LOCALAPPDATA\Android\ServoEngineerJdk",
    [switch]$InstallDependencies
)
$ErrorActionPreference = 'Stop'
$taskRepositoryRoot = Split-Path -Parent $PSScriptRoot
Push-Location $taskRepositoryRoot
try {
    if ($InstallDependencies) {
        & dotnet workload install maui-android --skip-manifest-update
        if ($LASTEXITCODE) { throw 'MAUI workload installation failed.' }
        & dotnet build SOURCE_CODE/ServoERP.Engineer.Mobile -t:InstallAndroidDependencies -f net9.0-android "-p:AndroidSdkDirectory=$AndroidSdkDirectory" "-p:JavaSdkDirectory=$JavaSdkDirectory" -p:AcceptAndroidSDKLicenses=True
        if ($LASTEXITCODE) { throw 'Android SDK installation failed.' }
    }
    & dotnet test SOURCE_CODE/ServoERP.Engineer.Tests -c Release --logger 'trx;LogFileName=engineer-tests.trx'
    if ($LASTEXITCODE) { throw 'Engineer app tests failed.' }
    & dotnet build SOURCE_CODE/ServoERP.Api -c Release
    if ($LASTEXITCODE) { throw 'Office API Release build failed.' }
    & dotnet build SOURCE_CODE/ServoERP.Engineer.Mobile -c Release -f net9.0-android "-p:AndroidSdkDirectory=$AndroidSdkDirectory" "-p:JavaSdkDirectory=$JavaSdkDirectory"
    if ($LASTEXITCODE) { throw 'Android Release build failed.' }
    $taskMSBuild = Get-Command msbuild -ErrorAction SilentlyContinue
    if ($taskMSBuild) { $taskMSBuildPath = $taskMSBuild.Source }
    else {
        $taskVSWhere = Join-Path ${env:ProgramFiles(x86)} 'Microsoft Visual Studio\Installer\vswhere.exe'
        if (!(Test-Path -LiteralPath $taskVSWhere)) { throw 'Install Visual Studio Build Tools with the .NET Framework desktop workload.' }
        $taskMSBuildPath = & $taskVSWhere -latest -products '*' -requires Microsoft.Component.MSBuild -find 'MSBuild\**\Bin\MSBuild.exe' | Select-Object -First 1
        if (!$taskMSBuildPath) { throw 'MSBuild was not found in Visual Studio Build Tools.' }
    }
    & $taskMSBuildPath SOURCE_CODE/HVAC_Pro_Desktop.csproj /p:Configuration=Release /p:Platform=AnyCPU /v:minimal /nologo
    if ($LASTEXITCODE) { throw 'Desktop Release build failed.' }
    $taskVersion = (Get-Content -LiteralPath VERSION -Raw).Trim()
    $taskApk = Join-Path $taskRepositoryRoot 'SOURCE_CODE\ServoERP.Engineer.Mobile\bin\Release\net9.0-android\in.servoerp.engineer-Signed.apk'
    if (!(Test-Path -LiteralPath $taskApk)) { throw 'Signed APK was not generated.' }
    $taskRelease = Join-Path $taskRepositoryRoot "release\engineer-$taskVersion"
    New-Item -ItemType Directory -Path $taskRelease -Force | Out-Null
    Copy-Item -LiteralPath $taskApk -Destination (Join-Path $taskRelease "ServoERP-Engineer-$taskVersion.apk")
    Copy-Item -LiteralPath 'SOURCE_CODE\ServoERP.Api\Mobile\MobileSchema.sql' -Destination (Join-Path $taskRelease 'MobileSchema.sql')
    Copy-Item -LiteralPath 'docs\ENGINEER_APP.md' -Destination $taskRelease
    $taskNotes = "release\RELEASE_NOTES_$taskVersion.md"
    if (!(Test-Path -LiteralPath $taskNotes)) { throw "Release notes are required: $taskNotes" }
    Copy-Item -LiteralPath $taskNotes -Destination $taskRelease
    Get-FileHash -LiteralPath (Join-Path $taskRelease "ServoERP-Engineer-$taskVersion.apk") -Algorithm SHA256
    Write-Output 'APK is signed with the local development key. Production signing and SQL integration validation are separate release gates.'
} finally { Pop-Location }
