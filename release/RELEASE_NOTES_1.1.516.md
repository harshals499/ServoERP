# ServoERP 1.1.516

Release date: 15 October 2026

## User-facing summary

ServoERP can open a prepared offline workspace when the office server is off. Local work is retained and synchronized automatically when the server returns. Conflicts stay available for office review; invoice/payment entries remain provisional drafts until explicitly finalized online.

## Features and fixes

- Offline sign-in after an initial online password login on the same Windows account/PC, using a protected cached verifier and permissions.
- Office/account-scoped durable SQLite snapshots and transactional pending operations with Windows CurrentUser DPAPI-encrypted payloads. Local records survive restart and interrupted sends retain their original operation IDs.
- Cached clients, sites, jobs, invoices and payments; offline client/contact details, site details, job scheduling/notes and invoice/payment draft entry.
- Automatic reconnect/background synchronization, bidirectional snapshot refresh, dependency ordering, receipt/audit idempotency and atomic server rollback.
- Both-copy conflicts retained centrally for office review. Explicit keep-office or revised-proposal actions recheck concurrency instead of silently overwriting another PC.
- Financial draft upload does not post invoices, payments, stock or balances. Online finalization reuses the existing financial services, explicit confirmation, per-draft locks and unique source-draft identifiers to reconcile lost responses without duplicates.
- Current server account/credential/permission and existing license policy checks; offline five-attempt/15-minute lockout, 30-day credential refresh and known-revocation invalidation.
- Readable business details and a verified offline-workspace entry/status strip in the normal shell. Archived client/merged records remain protected.
- Preserve old unscoped queues for source-office review; do not add new business writes or automatically replay those queues into a differently configured office.
- A live financial save interrupted after submission is reported as unconfirmed rather than being converted into a possible duplicate offline posting.

## Affected modules

Startup, sign-in/session lifecycle, normal-shell status/navigation, new offline workspace and editors, scoped synchronization/cache/review, client/site/job synchronization, job validation/checklist/activity shared primitives, invoice/payment draft linkage and finalization, additive schema, automated and native UI regression coverage. Detailed files and evidence are in Docs/OfflineDesktopSync.md and the versioned validation report.

## Database or migration impact

Guarded additions: dbo.DesktopOfflineReceipts, dbo.DesktopOfflineDrafts and dbo.DesktopOfflineConflicts; nullable uniqueidentifier OfflineDraftId on Invoices and Payments, with filtered unique indexes. No tables/columns are dropped or renamed. LicenseInfo is unchanged. The script is embedded as 0005_OfflineDesktop and runs through ordinary guarded schema setup; the retired upgrade journal/forced-backup/recovery gate is not restored. SQLite stores are per office, database/server and Windows profile, outside OneDrive.

## Configuration and dependency changes

No new NuGet packages or third-party services. Existing SQLite, Dapper, FluentValidation and Windows DPAPI are reused; the existing .NET Framework Microsoft.CSharp assembly is referenced for typed/dynamic Dapper projection support. Existing SQL connection settings, license endpoint/fingerprint and PDF headers remain unchanged. Offline files are under %LOCALAPPDATA%\ServoERP\OfflineDesktop.

## Compatibility or breaking changes

Existing public methods remain. Older unscoped queue files/records are preserved, but new saves require a prepared office/account workspace and old rows no longer replay automatically without origin review. Added nullable financial source-draft identifiers are ignored by earlier online releases. Offline mode covers the listed workflows rather than every online page.

## Security impact

Business payloads, cached verifier and role/profile data are protected with CurrentUser DPAPI; record IDs/status metadata are scoped per account. No plaintext passwords or new SQL/API secrets are stored in the offline cache. Upload rechecks the pinned office, current credentials/account/role and existing license entitlements. A known changed/disabled account invalidates cached offline access. License validation decisions, plan names, API endpoint and machine fingerprint logic are unchanged; only SQL-backed license telemetry is deferred while disconnected.

## Deployment/update steps

1. Retain the 1.1.515 installer and normal SQL backups; prepare a coordinated first launch with the office SQL server available so additive schema guards can run.
2. Install 1.1.516 and sign in with the password online once on each Windows account/PC. A remembered session alone cannot create a new offline password verifier. Wait for the initial download/checkpoint before relying on historical records offline.
3. Use Offline workspace to view cached work and create supported local records/drafts. The server can then be off; existing licensing and cached-authentication validity still apply.
4. When the server returns, leave the app open for automatic sync. Review indicated conflicts at the office. Finalize invoice/payment drafts explicitly while online after reviewing current business validation.
5. Keep the Windows profile and offline directory, including unsynced work; do not put the SQLite files in OneDrive, purge them during an update, or copy them between Windows accounts.

## Validation performed

Release build succeeded with 0 warnings and 0 errors, and SOURCE_CODE/bin/Release/HVAC_Pro_Desktop.exe exists as 1.1.516.0. Ten regression groups passed on explicitly disposable ServoERP_StartupGateQA_20261015190253_bc787470 and temporary encrypted local caches: cached auth/lockout/expiry/isolation; local transaction rollback; interrupted-send and receipt/audit replay; identity tampering refusal; two-PC conflicts and office resolution; idempotent revised proposals and injected server-receipt rollback; dependent creates using shared job primitives; offline financial non-posting and online lost-response finalization; account revocation/license denial; automatic reconnect/download. Native test opened the real offline startup/sign-in/workspace with a refused SQL endpoint and restored connectivity; background sync completed without a manual command. Normal/compact workspace, sign-in, draft editor, offline/online state and normal-shell entry were rendered and visually inspected. A focused shell preview also passed geometry checks and clicked the normal-shell entry to open the workspace. Unrelated normal-shell backup/updater/onboarding Shown handlers were suppressed in that preview. No business saves, migrations, schema experiments or service restarts targeted HVAC_PRO. Logs: release/build-1.1.516.log, offline-regression-1.1.516.log and main-banner-preview-1.1.516.log. Packaging/embedded-note verification is retained beside the artifacts.

## Known limitations

Initial online enrollment and an existing activated license on the same Windows account/PC are required; cached identity refresh is required after 30 days. Available records reflect the last completed download. Inventory, payroll, contracts, vendors and other unimplemented offline workflows remain online-only; official financial posting/numbering remains online-only. Conflicting or blocked proposals require review and are never silently overwritten. Legacy unscoped queues require origin review before import. Unsynced work is machine/profile-local, so recovery requires preserving that Windows profile and encrypted cache; no cloud roaming or guaranteed recovery after profile loss is claimed. Server unavailability was tested with a refused SQL connection rather than physically powering down a shared server. An existing fresh-database Payments schema-ordering limitation remains outside this update. The normal shell retains its existing online page-refresh behavior. Distribution remains unsigned. Version 1.1.516 is prepared after public 1.1.515 and is not claimed published before remote verification.

## Rollback guidance

Close ServoERP and preserve every local offline cache and pending proposal before rollback. Retain the 1.1.515 installer and SQL backups. Earlier online releases can ignore the additive tables/nullable columns, but cannot process this new scoped offline queue; retain it for reinstallation/reconciliation. Never drop the new schema or automatically restore customer data to roll back the application.
