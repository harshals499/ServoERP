param(
    [Parameter(Mandatory = $true)][string]$Version,
    [string]$NotesPath,
    [string]$AssemblyInfoPath,
    [switch]$ForPackaging
)
$ErrorActionPreference = 'Stop'
if ([string]::IsNullOrWhiteSpace($NotesPath)) {
    $NotesPath = Join-Path (Split-Path $PSScriptRoot -Parent) "release\RELEASE_NOTES_$Version.md"
}
if (!(Test-Path -LiteralPath $NotesPath -PathType Leaf)) { throw "Versioned release notes are required: $NotesPath" }
$notes = [IO.File]::ReadAllText((Resolve-Path -LiteralPath $NotesPath).Path)
if ([string]::IsNullOrWhiteSpace($notes)) { throw 'Release notes cannot be blank.' }
if ($notes -notmatch ('(?m)^# ServoERP ' + [regex]::Escape($Version) + '\s*$')) { throw 'Release notes do not match the release version.' }
if ($AssemblyInfoPath) {
    $assemblyText = [IO.File]::ReadAllText((Resolve-Path -LiteralPath $AssemblyInfoPath).Path)
    if ($assemblyText -notmatch ('AssemblyVersion\("' + [regex]::Escape($Version + '.0') + '"\)')) {
        throw 'AssemblyVersion and VERSION must match before embedding release notes.'
    }
}
$sections = @('User-facing summary','Features and fixes','Affected modules','Database or migration impact',
    'Configuration and dependency changes','Compatibility or breaking changes','Security impact',
    'Deployment/update steps','Validation performed','Known limitations','Rollback guidance')
foreach ($section in $sections) {
    $match = [regex]::Match($notes, '(?ms)^## ' + [regex]::Escape($section) + '\s*\r?\n(?<content>.*?)(?=^## |\z)')
    if (!$match.Success -or [string]::IsNullOrWhiteSpace($match.Groups['content'].Value)) { throw "Release notes require content for: $section" }
}
if ($notes -match '(?im)^\s*(?:TODO|TBD|No release notes were provided|No changelog details were provided)\b') {
    throw 'Release notes contain a placeholder instead of completed changes.'
}
if ($ForPackaging -and $notes -match 'Pending final Release build') { throw 'Record actual validation results before packaging.' }
Write-Output "Release notes validated: $Version"
