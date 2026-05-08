param(
    [string]$PrerequisiteDir = (Join-Path $PSScriptRoot 'Prerequisites')
)

$ErrorActionPreference = 'Stop'
[Net.ServicePointManager]::SecurityProtocol = [Net.SecurityProtocolType]::Tls12

New-Item -ItemType Directory -Force -Path $PrerequisiteDir | Out-Null

$downloads = @(
    @{
        Name = '.NET Framework 4.7.2 offline installer'
        File = 'NDP472-KB4054530-x86-x64-AllOS-ENU.exe'
        Url  = 'https://go.microsoft.com/fwlink/?linkid=863262'
        MinBytes = 1MB
    },
    @{
        Name = 'SQL Server Express 2022 x64 offline media'
        File = 'SQLEXPR_x64_ENU.exe'
        Url  = 'https://go.microsoft.com/fwlink/?LinkID=2213259&clcid=0x409'
        MinBytes = 100MB
    },
    @{
        Name = 'Microsoft Edge WebView2 Runtime x64'
        File = 'MicrosoftEdgeWebView2RuntimeInstallerX64.exe'
        Url  = 'https://go.microsoft.com/fwlink/?linkid=2124701'
        MinBytes = 100MB
    }
)

foreach ($download in $downloads) {
    $target = Join-Path $PrerequisiteDir $download.File
    $minBytes = if ($download.ContainsKey('MinBytes')) { [int64]$download.MinBytes } else { 1MB }
    if (Test-Path -LiteralPath $target -PathType Leaf) {
        if ((Get-Item -LiteralPath $target).Length -ge $minBytes) {
            Write-Host "Already present: $($download.File)"
            continue
        }
    }

    Write-Host "Downloading $($download.Name)..."
    Invoke-WebRequest -Uri $download.Url -OutFile $target -UseBasicParsing

    if ((Get-Item -LiteralPath $target).Length -lt $minBytes) {
        throw "Downloaded file is too small and may be invalid: $target"
    }
}

Write-Host "Prerequisites are ready in $PrerequisiteDir"
