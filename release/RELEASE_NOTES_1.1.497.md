# ServoERP 1.1.497

Release date: 2026-10-06. Public desktop release. Installation on individual client PCs is a separate step.

## User-facing summary

Saved office SQL Server settings now survive application updates. Connection setup saves and reloads the same configuration rather than verifying a different, stale file.

## Features and fixes

- Machine-wide configuration is stored at `%ProgramData%\ServoERP\Configuration\HVACPro.config`, outside replaceable installation folders.
- Existing configuration migrates once, retaining the legacy ConfigService priority: `C:\HVAC_PRO_MSE\HVACPro.config`, then the file beside the EXE. If both are absent, the newest local updater configuration backup is considered before the packaged sample.
- Database reads and writes use the same resolver. A corrupt or undecryptable saved database configuration reports an error instead of silently using EXE defaults.
- Connection setup writes database fields together through an atomic file replacement, reloads and tests the stored connection, and restores the previous configuration if save or verification fails.
- Updates back up the persistent configuration and refuse to proceed if that configuration is missing.
- This desktop build retains the existing generic client branding defaults and the fix preventing Start Trial from replacing an existing license.
- LAN deployment and support-generated client connection scripts write to the persistent location. Support scripts no longer write SQL passwords into EXE configuration files.

## Affected modules

ConfigService, DatabaseManager, ConnectionSetupForm, UpdateService, OfficeLanControlService, SupportCenterService; version metadata, changelog and isolated configuration regression checks.

## Database or migration impact

Database schema and business-data changes: None. Local XML configuration is copied once; legacy files remain available for rollback. Existing SQL server/database identity checks remain enabled.

## Configuration and dependency changes

Configuration path changes to the machine-wide location above. The configuration directory grants the Windows Users group Modify access so all accounts on the same office PC can save settings. No packages added.

## Compatibility or breaking changes

Public API renames: None. Older executables do not read the new location. External provisioning tools must use the new location after migration; editing a legacy file no longer changes active settings. Rebuild support setup ZIPs to obtain the updated provisioning script.

## Security impact

Newly saved SQL passwords remain protected with Windows machine-scoped DPAPI and are no longer written by connection setup into the EXE configuration. Existing plaintext legacy passwords retain backward compatibility until re-saved. Do not transfer machine-encrypted configurations between PCs. Shared configuration remains writable by local Windows users, consistent with machine-scoped office configuration.

## Deployment/update steps

1. Back up the current client configuration. Install/update with the normal ServoERP deployment process; the local update ZIP is in `update_output`.
2. Start ServoERP under the normal client Windows account. Migration is automatic; future application updates use the persistent copy.
3. If this client's previous settings were already incorrect, enter the correct server/login once, test and save. Then restart and verify normal database startup on that client.
4. Deploy this version to each PC. A successful migration preserves settings; subsequent application updates do not need connection setup merely because files were replaced.

## Validation performed

- Release build of `SOURCE_CODE/HVAC_Pro_Desktop.csproj`: succeeded, zero warnings and zero errors. `SOURCE_CODE/bin/Release/HVAC_Pro_Desktop.exe` exists, assembly version 1.1.497.0.
- `TOOLS/Test-PersistentConnectionConfig.ps1` in PowerShell 7: 10 isolated checks passed, covering legacy priority, persistent placement, batch reload, DPAPI password round-trip, other-section preservation, simulated installer replacement, explicit locked-file failure, unchanged original on failure, temporary-file cleanup, and migration from an older updater backup.
- Built app `/connectionsetupvisualtest`: exited 0. Rendered connection setup inspected; startup loaded the saved configuration. Evidence: `TEST_RESULTS/connection-setup-20261006-193154.png`.
- CI-safe `/cismoketest`: passed.
- Client PowerShell deployment script: successful isolated ZIP deployment with settings preserved; rollback exercised during failed validation. No client SQL calls were made.
- Local update ZIP generated from Release output; archive inspection recorded beside it.

## Known limitations

The affected remote client's SQL server, actual update/restart cycle and successful-save/failed-verification UI flows have not been exercised on that client. No claim of remote deployment is made. Server outages, rejected credentials, office identity mismatch, configuration removal/corruption or Windows reinstallation can still require recovery. This change preserves settings; it cannot guarantee SQL availability. The desktop build includes pre-existing generic-branding and trial-preservation fixes; mobile API/source projects are not part of the desktop installer. Installer/executable code signing is not configured for this local release.

## Rollback guidance

Back up the persistent configuration before rollback. Restore the previous application and its original legacy configuration from the PC-local update backup. If connection settings changed after migration, copy the current persistent configuration to the previous version's expected legacy location on the same PC, after preserving the old file. Do not delete the persistent configuration or restore any business database as part of this rollback.
