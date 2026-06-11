param(
    [string]$Version,
    [int]$PatchIncrement = 0
)

$ErrorActionPreference = "Stop"

$root = Resolve-Path (Join-Path $PSScriptRoot "..")
$versionFile = Join-Path $root "VERSION"
$assemblyInfo = Join-Path $root "SOURCE_CODE\Properties\AssemblyInfo.cs"
$appConfig = Join-Path $root "SOURCE_CODE\HVACPro.config"
$installerVersion = Join-Path $root "SOURCE_CODE\Installer\ServoERP.version.iss"

if ([string]::IsNullOrWhiteSpace($Version)) {
    $Version = (Get-Content -LiteralPath $versionFile -Raw).Trim()
}

$match = [regex]::Match($Version.Trim(), "^(?<major>\d+)\.(?<minor>\d+)\.(?<patch>\d+)(?:\.\d+)?$")
if (-not $match.Success) {
    throw "Version '$Version' is not semantic version MAJOR.MINOR.PATCH."
}

$major = [int]$match.Groups["major"].Value
$minor = [int]$match.Groups["minor"].Value
$patch = [int]$match.Groups["patch"].Value + [Math]::Max(0, $PatchIncrement)
$semver = "$major.$minor.$patch"
$assemblyVersion = "$semver.0"

Set-Content -LiteralPath $versionFile -Value $semver -NoNewline

$assemblyText = Get-Content -LiteralPath $assemblyInfo -Raw
$assemblyText = [regex]::Replace($assemblyText, 'AssemblyVersion\("[^"]+"\)', "AssemblyVersion(`"$assemblyVersion`")")
$assemblyText = [regex]::Replace($assemblyText, 'AssemblyFileVersion\("[^"]+"\)', "AssemblyFileVersion(`"$assemblyVersion`")")
Set-Content -LiteralPath $assemblyInfo -Value $assemblyText -NoNewline

if (Test-Path -LiteralPath $appConfig) {
    [xml]$xml = Get-Content -LiteralPath $appConfig
    if ($xml.HVACProConfig.App.Version) {
        $xml.HVACProConfig.App.Version = $semver
        $xml.Save($appConfig)
    }
}

if (Test-Path -LiteralPath $installerVersion) {
    Set-Content -LiteralPath $installerVersion -Value "#define AppVersion `"$semver`"" -NoNewline
}

"VERSION=$semver"
