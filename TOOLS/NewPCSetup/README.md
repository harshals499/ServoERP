# ServoERP New-PC Setup

Use this package on a new Windows computer to install ServoERP as either a client application or a developer workspace.

## Before you start

- Internet access is required.
- The developer setup needs Git and Visual Studio 2022 with the **.NET desktop development** workload. The wizard can install missing Git and start the Visual Studio installer.
- A database backup is **not** included. To use live business data, restore an authorised `HVAC_PRO` SQL Server backup or use the existing office SQL Server.
- Sign in to the ChatGPT desktop app yourself with the same Harshal ChatGPT account. Account sign-in is intentionally never automated.

## Developer setup

1. Copy this folder to the new PC. For non-technical users, start with `PLEASE_READ_FIRST.txt`.
2. Double-click `Start-ServoERP-NewPCSetup.cmd`.
3. Accept installation of missing prerequisites when asked.
4. The wizard clones `https://github.com/harshals499/ServoERP.git` into `C:\HVAC_PRO_MSE`, restores NuGet packages, and creates a Release build.
5. In the ChatGPT desktop app, open `C:\HVAC_PRO_MSE` as a local Codex project.

To configure the database during setup, start PowerShell in this folder and run:

```powershell
.\ServoERP-NewPCSetup.ps1 -ConfigureDatabase -SqlServer 'SERVERPC\SQLEXPRESS' -DatabaseName 'HVAC_PRO'
```

The configuration uses Windows authentication and deliberately clears SQL usernames and passwords. Do not commit the resulting local `SOURCE_CODE\HVACPro.config` change.

## Client-only installation

To install the published application without downloading source code:

```powershell
.\ServoERP-NewPCSetup.ps1 -Mode Client
```

## Optional switches

```powershell
# Install missing Git/Visual Studio packages without prompting.
.\ServoERP-NewPCSetup.ps1 -InstallPrerequisites

# Install SQL Server Express, then configure this machine to use it.
.\ServoERP-NewPCSetup.ps1 -InstallSqlExpress -ConfigureDatabase -SqlServer '.\SQLEXPRESS'

# Use a different folder and start the app after a successful build.
.\ServoERP-NewPCSetup.ps1 -InstallRoot 'D:\ServoERP' -LaunchApp
```

## What success means

The wizard only reports success after the Release executable exists at `SOURCE_CODE\bin\Release\HVAC_Pro_Desktop.exe`. A successful build does not confirm that a database is available; configure and test the authorised SQL Server separately before entering business data.
