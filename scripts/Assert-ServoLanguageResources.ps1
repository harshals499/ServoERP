param([Parameter(Mandatory = $true)][string]$BuildOutput)
$ErrorActionPreference = 'Stop'
$buildRoot = (Resolve-Path -LiteralPath $BuildOutput).Path
$app = Join-Path $buildRoot 'HVAC_Pro_Desktop.exe'
if (!(Test-Path -LiteralPath $app -PathType Leaf)) { throw 'Desktop executable is missing.' }
$version = [Reflection.AssemblyName]::GetAssemblyName($app).Version
foreach ($culture in @('mr-IN', 'hi-IN')) {
    $satellite = Join-Path $buildRoot "$culture\HVAC_Pro_Desktop.resources.dll"
    if (!(Test-Path -LiteralPath $satellite -PathType Leaf)) { throw "Required $culture language resource assembly is missing." }
    $identity = [Reflection.AssemblyName]::GetAssemblyName($satellite)
    if ($identity.Version -ne $version -or $identity.CultureInfo.Name -ne $culture) {
        throw "The $culture resource assembly must match desktop version $version and culture."
    }
}
Write-Output "Language resource assemblies validated: $version (mr-IN, hi-IN)"
