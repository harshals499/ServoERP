# ServoERP 1.1.509
Release date: 11/10/2026

## User-facing summary
Database upgrades now require a verified SQL Server backup and block business startup if an upgrade fails.

## Features and fixes
Added numbered embedded SQL migrations, SHA-256 script integrity checks with consistent line-ending handling, a durable migration journal and an office-wide SQL upgrade lock. Completed migrations are skipped. Failed or interrupted upgrades require an explicit retry using the same release and the original verified backup. This gate-aware release refuses a database journalled by a newer release.

## Affected modules
Database initialization, desktop startup, post-update maintenance and SQL upgrade diagnostics.

## Database or migration impact
Additive dbo.ServoSchemaMigrations and dbo.ServoDatabaseUpgradePolicy tables. Existing schema guards form the legacy compatibility step and remain nontransactional. New numbered SQL migrations run transactionally. LicenseInfo structure is unchanged by this feature. No business data is deleted or restored automatically.

## Configuration and dependency changes
No packages added. SQL login requires backup permission and access to the SQL Server default backup directory. Pre-upgrade COPY_ONLY backups use CHECKSUM and RESTORE VERIFYONLY WITH CHECKSUM. Backups are stored on the SQL host, not on a client PC, and are excluded from scheduled-backup cleanup.

## Compatibility or breaking changes
Database upgrades fail closed if backup verification or migration fails. Gate-aware application releases block startup when the journal records a newer release. Older pre-gate releases do not enforce this check. Existing public APIs retained.

## Security impact
No new outbound service. Recovery diagnostics redact exception details. Backup files contain office data; administrators must restrict SQL backup directory access. No automatic restore or destructive recovery.

## Deployment/update steps
Close ServoERP on all office PCs before the first upgraded terminal starts. Install the published installer/update. Allow time for the SQL backup and verification. On failure, retain the backup shown in the recovery message; fix SQL permissions, disk space or the reported error. Run the same release with /retrydatabaseupgrade for a controlled retry. Do not edit shipped migration scripts.

## Validation performed
Release build succeeded and HVAC_Pro_Desktop.exe exists. Isolated SQL regressions passed verified backup identity/checksums, actual restore preserving data, numbered journal, repeat startup, hash integrity, transactional DDL rollback, failure and interrupted-startup gates, controlled retry, original backup preservation, missing-backup refusal, downgrade refusal and concurrent upgrade serialization. Configured HVAC_PRO startup completed all three journal entries after a verified backup. CI smoke report contains 39 passes and no failures. The optional broad Enterprise UI smoke suite stopped on an existing dashboard alerts-strip assertion; this task does not change the dashboard. Release notes and Velopack package/feed guards passed; embedded notes match the versioned Markdown, the packaged EXE matches the tested build and mutable client files are excluded.

## Known limitations
RESTORE VERIFYONLY checks backup readability and checksums but is not a full test restore. Legacy schema guards cannot be rolled back as one transaction; the retained pre-upgrade backup supports administrator recovery. Existing terminals running older versions must be closed during maintenance; the SQL upgrade lock coordinates upgrading terminals, not old business sessions. Module-level runtime schema guards outside DatabaseManager remain outside this startup migration journal. Recovery backups require administrator retention management and are not automatically removed. The optional broad Enterprise UI smoke suite has a pre-existing assertion for a dashboard alerts strip that the current dashboard no longer renders; targeted upgrade tests and CI smoke passed.

## Rollback guidance
Do not launch an older release against the upgraded database. If recovery requires rollback, an administrator must first preserve any subsequent business changes, obtain the owner's approval for data replacement, restore the verified pre-upgrade backup on the office SQL Server, and install the previous supported release. Never restore automatically over live customer data.
