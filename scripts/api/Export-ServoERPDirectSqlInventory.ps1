param(
    [string]$SourceRoot = (Join-Path $PSScriptRoot '..\..\SOURCE_CODE'),
    [string]$OutputPath = (Join-Path $PSScriptRoot '..\..\artifacts\architecture\direct-sql-inventory.csv')
)
$ErrorActionPreference = 'Stop'
$SourceRoot = (Resolve-Path -LiteralPath $SourceRoot).Path.TrimEnd('\\')
$OutputPath = [System.IO.Path]::GetFullPath($OutputPath)
$patterns = 'SqlConnection|SqlCommand|SqlDataAdapter|SqlTransaction|ExecuteReader|ExecuteScalar|ExecuteNonQuery|DatabaseConnectionFactory|DatabaseConnectionStateService|SqlConnectionStringBuilder'
$items = foreach ($file in Get-ChildItem -LiteralPath $SourceRoot -Recurse -Filter *.cs) {
    if ($file.FullName -match '\\ServoERP\.Api\\|\\Tests\\') { continue }
    $class = ''
    $lineNumber = 0
    foreach ($line in Get-Content -LiteralPath $file.FullName) {
        $lineNumber++
        if ($line -match 'class\s+(?<name>[A-Za-z0-9_]+)') { $class = $matches.name }
        if ($line -notmatch $patterns) { continue }
        $module = if ($file.FullName -match '\\DAL\\') { 'Repository/DAL' } elseif ($file.FullName -match '\\UI\\') { 'WinForms UI' } elseif ($file.FullName -match '\\SERVICES\\') { 'Application service' } elseif ($file.FullName -match '\\Installer\\') { 'Installer/admin' } elseif ($file.Name -eq 'Program.cs') { 'Startup' } else { 'Infrastructure' }
        $operation = if ($line -match 'ExecuteNonQuery|INSERT|UPDATE|DELETE|BeginTransaction|SqlTransaction') { 'Write/transaction' } elseif ($line -match 'ExecuteReader|ExecuteScalar|SqlDataAdapter|SELECT') { 'Read/query' } else { 'Connection/configuration' }
        $risk = if ($file.Name -match 'Payment|Invoice|Inventory|Purchase|Payroll|Tender') { 'P0' } elseif ($file.Name -match 'Client|Vendor|Employee|Master|Settings|Site') { 'P1' } elseif ($operation -eq 'Read/query') { 'P2/P3' } else { 'P4' }
        [pscustomobject]@{
            File = $file.FullName.Substring($SourceRoot.Length).TrimStart('\')
            Line = $lineNumber
            Class = $class
            Module = $module
            Operation = $operation
            Risk = $risk
            CurrentSqlDependency = $line.Trim()
            TargetApiEndpointOrService = ''
            MigrationStatus = 'Not migrated'
        }
    }
}
New-Item -ItemType Directory -Force -Path (Split-Path -Parent $OutputPath) | Out-Null
$items | Sort-Object Risk,File,Line | Export-Csv -LiteralPath $OutputPath -NoTypeInformation -Encoding UTF8
$summary = $items | Group-Object Risk | Sort-Object Name | ForEach-Object { "$($_.Name): $($_.Count)" }
Write-Output "Wrote $($items.Count) direct-SQL dependencies to $OutputPath"
$summary
