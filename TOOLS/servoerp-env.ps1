$ErrorActionPreference = 'Stop'

$toolPaths = @(
    'C:\HVAC_PRO_MSE\TOOLS\PortableGit\cmd',
    'C:\HVAC_PRO_MSE\TOOLS\node',
    'C:\HVAC_PRO_MSE\TOOLS\dotnet',
    'C:\HVAC_PRO_MSE\TOOLS\sqlcmd\SqlCmd',
    'C:\Users\Administrator\AppData\Local\Programs\Python\Python312',
    'C:\Users\Administrator\AppData\Local\Programs\Python\Python312\Scripts',
    'C:\Users\Administrator\AppData\Local\Programs\Microsoft VS Code\bin',
    'C:\Users\Administrator\AppData\Local\Programs\Inno Setup 6'
)

$existing = $env:Path -split ';' | Where-Object { -not [string]::IsNullOrWhiteSpace($_) }
$env:Path = (($toolPaths + $existing) | Select-Object -Unique) -join ';'
$env:DOTNET_ROOT = 'C:\HVAC_PRO_MSE\TOOLS\dotnet'

Write-Host 'ServoERP tool environment loaded.'
Write-Host 'Workspace: C:\HVAC_PRO_MSE'
