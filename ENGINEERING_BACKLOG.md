# ServoERP Engineering Backlog

Last reviewed: 2026-08-16

## Release D — production operations

- [x] Verified SQL backup script with checksum and `RESTORE VERIFYONLY`.
- [x] Daily Task Scheduler registration script; installed as `ServoERP - Verified SQL Backup` at 21:00.
- [x] Isolated restore-validation script using a temporary `ServoERP_RestoreValidation_*` database.
- [x] Local API/service/backup/certificate health snapshot script.
- [x] Retention hardening: preserve the newest verified backup regardless of age.
- [x] Server recovery and migration runbook using isolated restore validation.
- [ ] Deploy and verify the HTTPS Office API Windows Service (requires elevated server action).
- [ ] Add an Admin/IT operations dashboard using API-backed health and backup status.
- [ ] Add a restricted audit viewer with company/user/action filtering.
- [ ] Add workstation onboarding package and controlled client-rollout reporting.

## Release C — API-first migration

- [ ] Migrate remaining direct-SQL P0 writes (invoice, purchase, inventory master, payroll, job postings).
- [ ] Migrate P1 master-data writes and P2/P3 reads through the Office API.
- [ ] Replace normal desktop startup/database-monitoring paths with API readiness.
- [ ] Prove normal worker operation with direct SQL network access blocked.

## Constraints

- Preserve company isolation, HTTPS validation, and fail-closed business writes.
- Do not delete production data or publish/deploy broadly without an explicit release instruction.
- Mandatory recurring cloud, API, and software cost remains EUR 0.
