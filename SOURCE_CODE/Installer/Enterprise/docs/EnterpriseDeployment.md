# ServoERP Enterprise Installer

ServoERP now has a WiX Toolset enterprise installer path:

- `ServoERP.App.<version>.msi` installs the application payload.
- `ServoERP.Setup.<version>.exe` is the Burn bootstrapper that installs prerequisites and chains the MSI.

The old Inno Setup installer is left in place only as a fallback. Enterprise deployments should use the Burn bootstrapper.

## Build

Run from the repository root or from this folder:

```powershell
powershell -ExecutionPolicy Bypass -File C:\HVAC_PRO_MSE\SOURCE_CODE\Installer\Enterprise\Build-EnterpriseInstaller.ps1 -ForceCloseRunningApp
```

Outputs:

```text
C:\HVAC_PRO_MSE\installer_output\enterprise\ServoERP.App.<version>.msi
C:\HVAC_PRO_MSE\installer_output\enterprise\ServoERP.Setup.<version>.exe
```

The build script restores WiX as a repo-local .NET tool and compiles both packages.

## Prerequisites

The bootstrapper detects and installs:

- Microsoft .NET Framework 4.7.2 or later
- SQL Server Express `SQLEXPRESS`
- Microsoft Edge WebView2 Runtime
- ServoERP application MSI

Prerequisite installers are stored in:

```text
C:\HVAC_PRO_MSE\SOURCE_CODE\Installer\Prerequisites
```

## Interactive Install

```powershell
.\ServoERP.Setup.<version>.exe
```

Default install path:

```text
C:\HVAC_PRO_MSE
```

## Silent Install

```powershell
.\ServoERP.Setup.<version>.exe /quiet /norestart
```

Override install path:

```powershell
.\ServoERP.Setup.<version>.exe /quiet /norestart InstallFolder="D:\ServoERP"
```

## MSI-Only Enterprise Deployment

Use this only when prerequisites are already deployed by SCCM, Intune, GPO, or another enterprise tool:

```powershell
msiexec /i ServoERP.App.<version>.msi /qn /norestart /L*v C:\ProgramData\ServoERP\Logs\ServoERP-MSI-Install.log
```

Repair:

```powershell
msiexec /fa ServoERP.App.<version>.msi /qn /norestart /L*v C:\ProgramData\ServoERP\Logs\ServoERP-MSI-Repair.log
```

Uninstall:

```powershell
msiexec /x ServoERP.App.<version>.msi /qn /norestart /L*v C:\ProgramData\ServoERP\Logs\ServoERP-MSI-Uninstall.log
```

## Logging

Burn writes package logs to the standard WiX bundle log location under the installing user temp directory. For enterprise deployments, call setup with:

```powershell
.\ServoERP.Setup.<version>.exe /quiet /norestart /log C:\ProgramData\ServoERP\Logs\ServoERP-Bundle.log
```

The MSI supports standard Windows Installer logging through `/L*v`.

## Rollback and Recovery

The MSI uses Windows Installer transactional rollback for application files, shortcuts, and registry entries.

Data directories are marked permanent:

- `CONFIG`
- `DATABASE`
- `Invoice`
- `LOGS`
- `PAYROLL_EXPORTS`
- `PAYSLIPS`
- `RECEIPTS`

This prevents uninstall or rollback from deleting customer business data.

## Upgrade Model

The MSI uses a stable `UpgradeCode` and `MajorUpgrade` scheduling. Each release should increment `AssemblyVersion` / `AssemblyFileVersion` in:

```text
C:\HVAC_PRO_MSE\SOURCE_CODE\Properties\AssemblyInfo.cs
```

Then rebuild the enterprise installer.

## Auto Updates

ServoERP keeps the existing in-app update mechanism:

```text
https://servoerp.in/version.txt
https://servoerp.in/changelog.json
```

The MSI writes install metadata under:

```text
HKLM\SOFTWARE\ServoERP
```

Update service settings remain in `HVACPro.config` and Settings.

## SQL Server

The Burn bundle installs SQL Server Express as `SQLEXPRESS` if missing. The application initializes the `HVAC_PRO` database on first run and the MSI also invokes the app with `/firstrun` after files are installed.

For multi-tenant/customer deployments, provision the database with:

```powershell
powershell -ExecutionPolicy Bypass -File C:\HVAC_PRO_MSE\TOOLS\Provision-TenantDatabase.ps1 -TenantCode <tenant> -Server ".\SQLEXPRESS"
```

## Recommended Enterprise Validation

Before sending to customers, test on a clean Windows 10/11 VM:

1. Install interactively.
2. Install silently with `/quiet /norestart`.
3. Repair from Apps & Features or `msiexec /fa`.
4. Upgrade from the previous version.
5. Uninstall and confirm business data remains.
6. Break SQL service startup and confirm installer/app diagnostics are useful.
7. Confirm WebView2-dependent screens open.
