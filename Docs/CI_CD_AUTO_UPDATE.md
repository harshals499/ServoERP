# ServoERP CI/CD and Auto-Update

ServoERP desktop releases are built by GitHub Actions and distributed through GitHub Releases with Velopack.

## Project Map

- Solution: `SOURCE_CODE/HVAC_Pro_Desktop.sln`
- Main project: `SOURCE_CODE/HVAC_Pro_Desktop.csproj`
- App type: Windows Forms desktop app
- Target framework: .NET Framework 4.7.2
- Platform: Windows x64
- Main executable: `HVAC_Pro_Desktop.exe`
- Package id: `ServoERP.Desktop`
- Test projects: none currently. Smoke test classes exist inside the desktop project and require an interactive Windows/SQL Server environment.

## Release Workflow

Workflow file: `.github/workflows/desktop-release.yml`

The workflow runs on:

- Push to `main`
- Manual `workflow_dispatch`

The job:

1. Restores NuGet packages.
2. Stamps a unique semantic version by adding the GitHub run number to the patch value in `VERSION`.
3. Builds `SOURCE_CODE/HVAC_Pro_Desktop.sln` in Release mode.
4. Runs test projects only when separate test projects are present.
5. Copies the build output to `artifacts/publish`.
6. Removes client-owned mutable files before packaging.
7. Builds Velopack installer/update packages.
8. Uploads the package output as a workflow artifact.
9. Publishes a GitHub Release tagged as `vMAJOR.MINOR.PATCH`.

## Versioning

Local source version is stored in:

- `VERSION`
- `SOURCE_CODE/Properties/AssemblyInfo.cs`
- `SOURCE_CODE/HVACPro.config`
- `SOURCE_CODE/Installer/ServoERP.version.iss`

Use this command to update the source version intentionally:

```powershell
.\scripts\Set-ServoVersion.ps1 -Version 1.1.15
```

In CI, the workflow computes a unique release version from `VERSION` plus `GITHUB_RUN_NUMBER`. Example: if `VERSION` is `1.1.15` and the run number is `42`, the released package version is `1.1.57`.

## Velopack Packaging

The workflow installs Velopack CLI `vpk` version `1.2.0`, matching the app's `Velopack` NuGet reference.

Packaging uses:

```powershell
vpk pack --packId ServoERP.Desktop --packTitle ServoERP --packVersion <version> --packDir artifacts/publish --mainExe HVAC_Pro_Desktop.exe --runtime win-x64
```

Velopack creates a setup executable plus update package files. Existing installs use the GitHub Release feed. New users download the latest setup executable from GitHub Releases.

## Client Data Safety

The release package intentionally excludes mutable client files and folders:

- `HVACPro.config`
- `.servoerp-license` files
- local database files such as `.sqlite`, `.mdf`, `.ldf`, `.bak`
- `DATABASE`, `LOGS`, `UPDATES`, `CONFIG`, `TEMP`, `TEST_RESULTS`
- business output folders such as `PAYSLIPS`, `RECEIPTS`, `Invoice`

The installer includes `HVACPro.sample.config`. On first launch only, ServoERP creates `HVACPro.config` from this sample if no app-local or machine-level config exists.

Before applying an update, ServoERP backs up local config files to:

```text
C:\HVAC_PRO_MSE\UPDATES\config-backup-v<version>-<timestamp>
```

If backup fails, the update is cancelled.

## App Update Flow

Startup calls Velopack before normal app initialization:

```csharp
VelopackApp.Build().SetAutoApplyOnStartup(false).Run();
```

After login, ServoERP can check GitHub Releases through `UpdateService`.

Settings > About & Updates provides:

- GitHub Releases repository
- Check for updates automatically
- Download updates automatically
- Current version
- Last update check status
- Check for Updates button

Updates can be downloaded in the background when enabled, but install/restart requires user confirmation. If another active ServoERP window is open, the update apply step is blocked so users can finish any save operation first.

## Manual Validation Checklist

Run before trusting a release:

1. Confirm Release build succeeds locally.
2. Confirm `SOURCE_CODE/bin/Release/HVAC_Pro_Desktop.exe` exists.
3. Run the GitHub Actions workflow from `main`.
4. Download the latest setup executable from GitHub Releases and test fresh install on a clean Windows machine.
5. Install version N, then publish version N+1 and confirm the app detects the update.
6. Confirm update download progress and restart prompt.
7. Cancel an update and confirm ServoERP continues normally.
8. Simulate update failure by disconnecting the network during download and confirm graceful failure.
9. Confirm SQL Server database, `HVACPro.config`, license files, logs, and local output folders are not deleted or overwritten.
10. Open ServoERP after update and verify login, dashboard, Settings, and one invoice/customer workflow.

## GitHub Repository Requirements

No paid service is required.

The workflow uses the built-in `GITHUB_TOKEN` and needs:

```yaml
permissions:
  contents: write
```

For private repositories, Velopack update checks may need a readable release source. Avoid embedding private tokens in the desktop app unless the deployment environment is trusted.
