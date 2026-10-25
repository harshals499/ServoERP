# ServoERP 1.1.521
Release date: 24 October 2026.

## User-facing summary
Completed Last sync notifications hide automatically after five seconds.

## Features and fixes
Reclaim the notification-bar space after the timer expires. Ordinary database health refreshes do not extend or redisplay the dismissed notice. A new completed sync starts a fresh five-second notice. Offline, degraded, reconnecting, configuration and sync-failure warnings remain visible. Dispose the UI timer when the main form closes.

## Affected modules
Main window database/offline sync notification bar.

## Database or migration impact
None.

## Configuration and dependency changes
None. Five-second timeout is built in; no packages added.

## Compatibility or breaking changes
None. Existing Jobs placement under Operations, Add New Job, license entitlements, sync protocol and business-data workflows remain unchanged.

## Security impact
None. Warning visibility and sync/data protections are preserved.

## Deployment/update steps
Install with the existing setup or updater. Preserve company data and private configuration. No database migration or additional setup is required.

## Validation performed
Release rebuild passed and HVAC_Pro_Desktop.exe exists. The real WinForms timer hid the notification at 5004ms despite intervening health refreshes; the page reclaimed 44px and repeated refreshes did not resurrect it. A new completion with identical displayed text started a fresh notice. Offline/degraded/reconnecting/configuration warnings preempted the timer; a sync-failure notice remained visible beyond five seconds. MainForm disposal cleared its timer. Before/after and offline-warning screenshots were captured and inspected. Testing used a synthetic isolated SQLite cache and unreachable diagnostic SQL target, with no production SQL or customer cache. Embedded-note, package/feed and local updater checks are retained with the candidate artifacts.

## Known limitations
No customer's installation is upgraded during validation. Sync protocol and business-save behavior are outside this presentation change.

## Rollback guidance
Restore the previous verified v1.1.520 binaries while preserving all configuration and business data. The updater does not downgrade automatically. No database rollback is required.
