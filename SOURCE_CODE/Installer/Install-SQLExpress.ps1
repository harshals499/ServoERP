# ============================================================
#  HVAC PRO - SQL Server Express Installer
#  Run as Administrator
# ============================================================

param(
    [string]$InstanceName = "SQLEXPRESS",
    [string]$SaPassword   = ""          # leave blank to use Windows Auth only
)

$ErrorActionPreference = "Stop"

function Write-Step($msg) {
    Write-Host "`n[HVAC PRO SETUP] $msg" -ForegroundColor Cyan
}

function Write-OK($msg) {
    Write-Host "  OK  $msg" -ForegroundColor Green
}

function Write-Fail($msg) {
    Write-Host "  !! $msg" -ForegroundColor Red
}

# ----------------------------------------------------------
# 1. Check Admin
# ----------------------------------------------------------
Write-Step "Checking administrator privileges..."
if (-not ([Security.Principal.WindowsPrincipal] [Security.Principal.WindowsIdentity]::GetCurrent()).IsInRole(
        [Security.Principal.WindowsBuiltInRole]::Administrator)) {
    Write-Fail "Please run this script as Administrator."
    exit 1
}
Write-OK "Running as Administrator."

# ----------------------------------------------------------
# 2. Check if SQL Express already installed
# ----------------------------------------------------------
Write-Step "Checking for existing SQL Server instance..."
$existing = Get-Service -Name "MSSQL`$$InstanceName" -ErrorAction SilentlyContinue
if ($existing) {
    Write-OK "SQL Server ($InstanceName) is already installed. Skipping install."
} else {

    # ----------------------------------------------------------
    # 3. Download SQL Server Express
    # ----------------------------------------------------------
    Write-Step "Downloading SQL Server 2022 Express..."
    $installerUrl  = "https://go.microsoft.com/fwlink/p/?linkid=2216019&clcid=0x409&culture=en-us&country=us"
    $bootstrapper  = "$env:TEMP\SQL2022-SSEI-Expr.exe"

    try {
        $wc = New-Object System.Net.WebClient
        $wc.DownloadFile($installerUrl, $bootstrapper)
        Write-OK "Downloaded to $bootstrapper"
    } catch {
        Write-Fail "Download failed: $_"
        exit 1
    }

    # ----------------------------------------------------------
    # 4. Run bootstrapper to get full installer
    # ----------------------------------------------------------
    Write-Step "Extracting full installer..."
    $extractPath = "$env:TEMP\SQLExpressMedia"
    Start-Process -FilePath $bootstrapper `
        -ArgumentList "/ACTION=Download", "/MEDIAPATH=$extractPath", "/QUIET" `
        -Wait -NoNewWindow

    $setupExe = Get-ChildItem "$extractPath\*.exe" | Select-Object -First 1
    if (-not $setupExe) {
        Write-Fail "Extraction failed — setup.exe not found in $extractPath"
        exit 1
    }
    Write-OK "Installer ready: $($setupExe.FullName)"

    # ----------------------------------------------------------
    # 5. Install SQL Express silently
    # ----------------------------------------------------------
    Write-Step "Installing SQL Server Express (this may take 5-10 minutes)..."

    $args = @(
        "/Q",
        "/ACTION=Install",
        "/INSTANCENAME=$InstanceName",
        "/FEATURES=SQLEngine",
        "/SQLSVCACCOUNT=`"NT AUTHORITY\NETWORK SERVICE`"",
        "/SQLSYSADMINACCOUNTS=`"BUILTIN\Administrators`"",
        "/TCPENABLED=1",
        "/NPENABLED=1",
        "/IACCEPTSQLSERVERLICENSETERMS"
    )

    if ($SaPassword -ne "") {
        $args += "/SECURITYMODE=SQL"
        $args += "/SAPWD=`"$SaPassword`""
    }

    $proc = Start-Process -FilePath $setupExe.FullName `
        -ArgumentList $args `
        -Wait -NoNewWindow -PassThru

    if ($proc.ExitCode -notin @(0, 3010)) {
        Write-Fail "SQL Server install failed with exit code: $($proc.ExitCode)"
        Write-Fail "Check logs at: C:\Program Files\Microsoft SQL Server\160\Setup Bootstrap\Log"
        exit 1
    }
    Write-OK "SQL Server Express installed successfully."
}

# ----------------------------------------------------------
# 6. Enable TCP/IP and start service
# ----------------------------------------------------------
Write-Step "Configuring SQL Server network..."
try {
    $smo = [System.Reflection.Assembly]::LoadWithPartialName("Microsoft.SqlServer.SqlWmiManagement")
    $wmi = New-Object Microsoft.SqlServer.Management.Smo.Wmi.ManagedComputer
    $tcp = $wmi.ServerInstances[$InstanceName].ServerProtocols["Tcp"]
    $tcp.IsEnabled = $true
    $tcp.Alter()
    Write-OK "TCP/IP enabled."
} catch {
    Write-Host "  (WMI config skipped — may already be set)" -ForegroundColor Yellow
}

# ----------------------------------------------------------
# 7. Start SQL Server Browser (for named instance)
# ----------------------------------------------------------
Write-Step "Starting SQL Server Browser..."
Set-Service -Name "SQLBrowser" -StartupType Automatic -ErrorAction SilentlyContinue
Start-Service -Name "SQLBrowser" -ErrorAction SilentlyContinue
Write-OK "SQL Browser running."

# ----------------------------------------------------------
# 8. Open Firewall for SQL Server
# ----------------------------------------------------------
Write-Step "Adding Windows Firewall rules..."
netsh advfirewall firewall add rule `
    name="SQL Server Express (HVAC PRO)" `
    dir=in action=allow protocol=TCP localport=1433 | Out-Null
netsh advfirewall firewall add rule `
    name="SQL Server Browser (HVAC PRO)" `
    dir=in action=allow protocol=UDP localport=1434 | Out-Null
Write-OK "Firewall rules added."

# ----------------------------------------------------------
# 9. Restart SQL Service
# ----------------------------------------------------------
Write-Step "Restarting SQL Server service..."
Restart-Service -Name "MSSQL`$$InstanceName" -Force
Write-OK "SQL Server is running."

# ----------------------------------------------------------
# 10. Verify connection
# ----------------------------------------------------------
Write-Step "Verifying connection to (local)\$InstanceName ..."
try {
    $conn = New-Object System.Data.SqlClient.SqlConnection
    $conn.ConnectionString = "Server=(local)\$InstanceName;Integrated Security=true;Connect Timeout=10;"
    $conn.Open()
    $conn.Close()
    Write-OK "Connection successful!"
} catch {
    Write-Host "  Warning: Could not verify connection. A reboot may be required." -ForegroundColor Yellow
}

# ----------------------------------------------------------
# Done
# ----------------------------------------------------------
Write-Host "`n============================================================" -ForegroundColor Green
Write-Host "  SQL Server Express ($InstanceName) is ready for HVAC PRO!" -ForegroundColor Green
Write-Host "  Connection string: Server=(local)\$InstanceName;Integrated Security=true;" -ForegroundColor Green
Write-Host "============================================================`n" -ForegroundColor Green

if ($proc -and $proc.ExitCode -eq 3010) {
    Write-Host "  NOTE: A system REBOOT is required to complete installation." -ForegroundColor Yellow
}
