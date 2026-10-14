# ServoERP 1.1.510
Release date: 11/10/2026

## User-facing summary
Posted invoices are locked against financial edits. Archive invoices and jobs without losing their records, and review changes with old/new values in Audit history.

## Features and fixes
Invoice financial fields and line items are protected in SQL Server and desktop save/import paths. Draft header and line changes save atomically; finalisation commits stock issue and posting together. Payments and existing credit-note adjustments remain available, but posted invoices cannot return to Draft or Cancelled. Use credit notes for financial corrections.

Deletion now archives invoices and jobs, preserving payments, lines, checklists, stock issue history and linked records. Draft archiving releases reservations without changing physical stock. Archived posted invoices remain in financial reports and receivable balances; active lists and global search hide archives. Restore archived records without unlocking posted invoices. Draft reservations return on the next save.

Audit history records the actor, UTC timestamp, action and full before/after snapshots within the business transaction. Quick actions provide Audit history and Archived records; row menus provide record-specific history. Multi-line imports assemble documents before posting and refuse posted reimports. Invoice numbering includes archived records.

## Affected modules
Invoices, Jobs/Work Orders, payments and financial reporting, inventory reservations, Excel import, global search, operational lists, startup migration and record-history screens.

## Database or migration impact
Numbered migration 0004_FinancialRecordProtection runs transactionally after the verified backup gate. Adds guarded archive metadata to Invoices and Jobs, posting metadata to Invoices, dbo.FinancialRecordAudit, ActiveInvoices/FinancialInvoices/ActiveJobs views, and protection/audit triggers. Existing non-Draft invoices become posted; their original posting time remains unknown. Existing Jobs.DeletedUtc values are preserved and mapped to archive state. LicenseInfo structure is unchanged. No existing business table or column is dropped or renamed.

## Configuration and dependency changes
No packages or new third-party services added. SQL Server 2016 SP1 or newer is required for CREATE OR ALTER, JSON snapshots and SESSION_CONTEXT; validated on the installed SQL Server Express instance. SQL backup permission and server backup-directory access remain required. Audit storage grows with changes and belongs in database backups.

## Compatibility or breaking changes
Public repository delete method names are retained, with archive behaviour. Physical invoice/job deletion and posted financial edits are refused. Use credit notes for financial corrections. Existing explicit Fresh Start and seed/simulation maintenance paths retain scoped behaviour; Fresh Start destructive statements were not changed. Gate-aware older releases cannot open an upgraded database. Older pre-gate executables and external SQL tools should not be used with the upgraded schema.

## Security impact
Transactional SQL guards prevent ordinary application paths from bypassing locks or hard-deleting invoices/jobs. Actor context resets on every pooled desktop connection. SQL login and machine are retained alongside application identity. Audit viewing requires module View permission; restoration requires Edit. SQL administrators can alter database objects or use maintenance context, so this is not a cryptographic or administrator-proof ledger.

## Deployment/update steps
Install the version-matched setup or use the existing updater. Close older office terminals, confirm backup permissions, and let first startup finish the verified backup and migration before reopening terminals. Open Invoice or Job Quick actions > Audit history / Archived records. Treat legacy non-Draft invoices as locked documents.

## Validation performed
Release build succeeded with HVAC_Pro_Desktop.exe in SOURCE_CODE/bin/Release. Financial smoke testing on a separately restored, verified database copy passed: migration, atomic draft header/lines, posting and direct SQL locks, unposting/delete refusal, payment compatibility, payment/line/checklist/job-link retention, archive/restore, draft reservation release/recreation, atomic stock/posting, issued-stock preservation, multi-line import and posted reimport refusal, pooled actor reset, old/new snapshots and transactional maintenance rollback. History and archive screens were rendered and visually inspected. Normal startup and invoice/job list, balance, form and history data smoke paths passed against the configured database. The 39 CI smoke checks and the verified-backup, actual-restore, failed-migration/retry, downgrade and concurrency regression suite passed.

## Known limitations
Audit history starts with this release; historical old/new values cannot be reconstructed. Viewer shows the latest 500 matching events and 500 archives. Legacy posting timestamps remain unknown. Restored draft reservations return on the next save. The existing full Enterprise UI smoke test has a previously known dashboard assertion referring to the retired pnlDashboardAlertsNotifications control; that unrelated check is not claimed as passing.

## Rollback guidance
Keep the server-side verified pre-upgrade backup and matching application package. Do not downgrade the executable against an upgraded database. Recovery needs a planned restore of the pre-upgrade backup and matching older application across office terminals, after reconciling later writes. A failed numbered migration rolls back its transaction and blocks startup; correct the failure and use the same-release explicit retry. No automatic business-data restore is performed.
