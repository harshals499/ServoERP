# ServoERP 1.1.515

Release date: 15 October 2026

## User-facing summary

Removed database-upgrade recovery blocking after an interrupted upgrade.

## Features and fixes

Removed journal-based blocking, per-release upgrade identity/hash/downgrade checks, forced upgrade backups, exclusive upgrade lock and retry command. Existing incomplete entries are ignored. Legacy schema maintenance no longer replays business audit history; normal business connections restore audit/protection flags. Financial schema backfills are skipped when their required objects already exist.

## Affected modules

Database initialization, startup recovery handling, financial maintenance connection context, embedded schema resources and regression tests.

## Database or migration impact

No tables, columns, journal rows or backups are deleted. Existing schema guards and financial-protection schema remain. Retired safe-upgrade policy is no longer embedded or installed.

## Configuration and dependency changes

None. No packages added.

## Compatibility or breaking changes

Public Upgrade and DatabaseUpgradeException remain for API compatibility. Recovery retry and old upgrade smoke-test handlers are retired. Migration journal downgrade blocking is removed.

## Security impact

Automatic upgrade backup verification and journal fail-closed protection are removed at the owner request. Financial protection remains. Retain normal administrative backups.

## Deployment/update steps

Install 1.1.515 on office PCs. No journal repair or database restore is needed. Preserve existing data and backups.

## Validation performed

Release build on the isolated published-source baseline succeeded with 0 warnings and 0 errors; SOURCE_CODE/bin/Release/HVAC_Pro_Desktop.exe exists. Standalone regression passed on disposable ServoERP_StartupGateQA_20261015190253_bc787470: incomplete/future journal entries ignored and preserved; repeated complete schema initialization; financial protection retained; real errors propagate; maintenance flags reset; no audit replay or business bypass; retired policy not embedded and existing policy preserved. Logs: release/build-1.1.515.log and release/startup-gate-regression-1.1.515.log. No migrations or business saves against HVAC_PRO. Packaging, embedded-note and remote-publication evidence is retained beside the artifacts. No interactive desktop smoke test was performed because no visual UI layout changed.

## Known limitations

Original process interruption cause is unknown. An initial empty-database test exposed a pre-existing Payments schema ordering issue; fresh database provisioning was not repaired in this scoped update. An initial restored-copy test waited for SQL audit memory; maintenance audit replay was fixed and regression passed. Ordinary SQL/schema errors can still block startup. Built from the verified published 1.1.514 baseline in an isolated worktree; primary workspace changes are excluded. Distribution remains unsigned.

## Rollback guidance

Retain the previously published 1.1.514 installer and current backups. Never automatically restore business data. Older releases may block on preserved Running journal entries.
