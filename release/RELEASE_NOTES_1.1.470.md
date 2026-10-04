# ServoERP 1.1.470 Release Notes

Release date: 03/10/2026

## User-facing summary

ServoERP can now copy completed SQL Server backups into the customer's locally synced OneDrive folder and can safely retain client, site, and job create/update work when the office SQL Server connection is interrupted. SQL Server remains the authoritative business database; ServoERP never runs the live database from OneDrive.

This build also introduces an Accepted Quote to Delivery Wizard with human confirmation before creating delivery records.

## Features and fixes

- Added automatic detection of Microsoft OneDrive for Business or Personal local sync folders, plus a configurable folder override.
- Added tenant-isolated `OneDrive\ServoERP Backups\<company>` folders and optional copying of completed SQL backup files to OneDrive.
- Enabled the machine-local SQLite offline queue for Clients, Sites, and Jobs create/update operations.
- Added stable public identities, idempotent create replay, ordered dependency remapping for records created offline, and update coalescing.
- Added optimistic server-version checks. Stale offline edits remain in Conflict status for review and do not overwrite newer office records.
- Kept invoices, payments, payroll, inventory movements, and job-material operations online-only.
- Added OneDrive and offline-work controls to Backup & Recovery, including detection and visible scope guidance.
- Added the accepted-quotation delivery workflow, planning safeguards, and confirmation boundary.

## Affected modules

- Backup & Recovery
- Clients
- Client Sites
- Jobs / Work Orders
- Settings and support diagnostics
- Accepted Quotation Delivery

## Database or migration impact

- SQL Server: no destructive migration. Existing additive sync identity and outbox columns/tables remain in use.
- Local machine: creates `%LOCALAPPDATA%\ServoERP\Offline\ServoERP_Offline.sqlite` when offline work is enabled.
- OneDrive: creates a tenant-isolated `ServoERP Backups\<company>` folder when enabled.
- The `LicenseInfo` table is unchanged.

## Configuration and dependency changes

- Added OneDrive backup settings plus `OneDriveStorage.RootPath` for offline-file safety checks.
- `Fallback.Mode` is now `LocalSQLiteOfflineQueue`; `Fallback.AllowBusinessWrites` controls the approved offline queue.
- No new NuGet packages were added.
- A signed-in Microsoft OneDrive desktop client is required for automatic folder detection and cloud synchronization.

## Compatibility or breaking changes

- None. Existing SQL Server deployments remain authoritative and continue to work without OneDrive.
- Existing installations can leave OneDrive and offline work disabled.

## Security impact

- Live SQL Server `.mdf`/`.ldf` files and the local SQLite offline queue are explicitly excluded from OneDrive storage.
- Offline replay accepts only Clients, Sites, and Jobs create/update operations.
- Financial, payroll, inventory, and job-material mutations require a live SQL Server connection.
- Conflict detection prevents stale offline updates from silently overwriting newer server records.

## Deployment and update steps

1. Install or update ServoERP normally.
2. Open Settings, then Backup Settings.
3. Under Company OneDrive, select **Detect**, verify the local OneDrive folder, and enable OneDrive backup copies if required.
4. Under Offline Work, enable offline client/site/job work if approved for that installation, then save settings.
5. Run **Backup Now** and confirm that a completed `.bak` copy appears under `OneDrive\ServoERP Backups\<company>`.
6. Keep the office SQL Server online whenever financial, inventory, payroll, or job-material work is performed.

## Validation performed

- Built `HVAC_Pro_Desktop.csproj` successfully in Debug and Release configurations with MSBuild 17.14.40.
- Verified `SOURCE_CODE\bin\Release\HVAC_Pro_Desktop.exe` exists with file version `1.1.470.0`.
- Ran the Release CI smoke harness successfully, including offline scope/conflict policy, UI policy, office-database handshake, startup recovery, accepted-quotation delivery, and Smart Dispatch checks.
- Captured and visually reviewed the built Backup & Recovery form at 996 x 981 pixels; OneDrive/offline controls, safety guidance, backup actions, and backup log render without overlap or clipping.
- Captured and visually reviewed all five built Accepted Quote to Delivery Wizard steps, including the 1180 x 780 review screen; job, material, purchase-need, billing, and confirmation content renders correctly.
- Did not upload a real customer backup to OneDrive during automated validation; the setting remains opt-in.

## Known limitations

- OneDrive is a backup/document destination, not the live multi-user database and not a substitute for SQL Server.
- Phase-one offline work covers only Clients, Sites, and Jobs create/update operations.
- Conflicts are retained for support review; an interactive merge editor is not included in this release.
- Offline lists rely on records already loaded in the running application; a complete restart while disconnected does not provide a full local replica of all server records.

## Rollback guidance

1. Disable OneDrive backup copies and offline work in Backup Settings.
2. Allow any pending non-conflicting offline items to sync before downgrading.
3. Preserve `%LOCALAPPDATA%\ServoERP\Offline\ServoERP_Offline.sqlite` if unresolved conflicts require support review.
4. Reinstall the previous ServoERP release. No SQL rollback is required because this feature adds no destructive SQL schema change.
