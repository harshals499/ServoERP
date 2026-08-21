param(
    [string]$Root = 'C:\HVAC_PRO_MSE',
    [string]$AppPath
)

$ErrorActionPreference = 'Stop'
if ([string]::IsNullOrWhiteSpace($AppPath)) {
    $AppPath = Join-Path $Root 'SOURCE_CODE\bin\Release\HVAC_Pro_Desktop.exe'
}

$process = Start-Process -FilePath $AppPath -ArgumentList '/pdfregressiontest' -Wait -PassThru
if ($process.ExitCode -ne 0) { throw "PDF template regression application exited with code $($process.ExitCode)." }

$report = Get-ChildItem (Join-Path $Root 'TEST_RESULTS\pdf-regression') -Recurse -Filter report.txt |
    Sort-Object LastWriteTime -Descending | Select-Object -First 1
if ($null -eq $report) { throw 'PDF regression report was not produced.' }
if (((Get-Date) - $report.LastWriteTime).TotalSeconds -gt 30) { throw 'PDF regression report was not freshly produced.' }
if ((Get-Content -LiteralPath $report.FullName -Raw).StartsWith('FAIL ')) { throw 'PDF template regression document generation failed.' }

$pdfs = Get-Content -LiteralPath $report.FullName | Where-Object { $_ -like 'PDF *' } | ForEach-Object { $_.Substring(4) }
$pdftoppm = (Get-Command pdftoppm.exe -ErrorAction SilentlyContinue).Source
if ([string]::IsNullOrWhiteSpace($pdftoppm)) {
    $bundled = 'C:\Users\Administrator\.cache\codex-runtimes\codex-primary-runtime\dependencies\native\poppler\Library\bin\pdftoppm.exe'
    if (Test-Path -LiteralPath $bundled) { $pdftoppm = $bundled }
}
if ([string]::IsNullOrWhiteSpace($pdftoppm)) { throw 'pdftoppm.exe is required for rendered PDF regression checks.' }

foreach ($pdf in $pdfs) {
    if (-not (Test-Path -LiteralPath $pdf)) { throw "Regression PDF was not found: $pdf" }
    $prefix = Join-Path $report.DirectoryName ([IO.Path]::GetFileNameWithoutExtension($pdf))
    & $pdftoppm -png -f 1 -singlefile $pdf $prefix
    $png = $prefix + '.png'
    if (-not (Test-Path -LiteralPath $png) -or (Get-Item -LiteralPath $png).Length -lt 1024) { throw "Rendered PDF image is missing or blank: $pdf" }
    Write-Host "Rendered PDF regression passed: $pdf -> $png"
}

Write-Host "PDF template regression passed. Report: $($report.FullName)"
