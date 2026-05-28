$ErrorActionPreference = 'Continue'
$logRoot = 'C:\HVAC_PRO_MSE\artifacts\prereq-install'
New-Item -ItemType Directory -Force -Path $logRoot | Out-Null
$stamp = Get-Date -Format 'yyyyMMdd_HHmmss'
$transcript = Join-Path $logRoot "install_$stamp.log"
Start-Transcript -Path $transcript -Append

function Run-Step {
    param(
        [Parameter(Mandatory = $true)][string]$Name,
        [Parameter(Mandatory = $true)][scriptblock]$Action
    )

    Write-Host ""
    Write-Host "==== $Name ===="
    try {
        & $Action
        Write-Host "==== $Name complete, exit code: $LASTEXITCODE ===="
    }
    catch {
        Write-Host "==== $Name failed ===="
        Write-Host $_
    }
}

Run-Step 'Install Microsoft Edge WebView2 Runtime' {
    winget install --id Microsoft.EdgeWebView2Runtime --exact --silent --accept-package-agreements --accept-source-agreements --disable-interactivity
}

Run-Step 'Install Microsoft Sqlcmd tools' {
    winget install --id Microsoft.Sqlcmd --exact --silent --accept-package-agreements --accept-source-agreements --disable-interactivity
}

Run-Step 'Install SQL Server 2022 Express SQLEXPRESS' {
    winget install --id Microsoft.SQLServer.2022.Express --exact --silent --accept-package-agreements --accept-source-agreements --disable-interactivity --override "/QS /ACTION=Install /FEATURES=SQLEngine /INSTANCENAME=SQLEXPRESS /SQLSVCSTARTUPTYPE=Automatic /TCPENABLED=1 /NPENABLED=1 /SQLSYSADMINACCOUNTS=`"BUILTIN\Administrators`" /IACCEPTSQLSERVERLICENSETERMS"
}

Run-Step 'Install Visual Studio 2022 Build Tools for .NET desktop' {
    winget install --id Microsoft.VisualStudio.2022.BuildTools --exact --silent --accept-package-agreements --accept-source-agreements --disable-interactivity --override "--wait --quiet --norestart --nocache --add Microsoft.VisualStudio.Workload.ManagedDesktopBuildTools --add Microsoft.Net.Component.4.7.2.TargetingPack --add Microsoft.Net.Component.4.7.2.SDK --includeRecommended"
}

Run-Step 'Show installed command paths' {
    Get-Command git, node, npm, sqlcmd, msbuild -ErrorAction SilentlyContinue | Select-Object Name, Source, Version | Format-List
}

Run-Step 'Restore ServoERP HVAC_PRO database backup' {
    powershell.exe -NoProfile -ExecutionPolicy Bypass -File 'C:\HVAC_PRO_MSE\TOOLS\Restore-ServoERPDatabase.ps1'
}

Stop-Transcript
