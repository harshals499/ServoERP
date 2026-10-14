# ServoERP 1.1.471 Release Notes

Release date: 03/10/2026

## User-facing summary

ServoERP now provides an AMC Preventive Planner that turns active maintenance-contract obligations into a capacity-aware visit calendar for dispatcher review. It highlights missed obligations and prepares customer reminder drafts, while keeping visit creation and all outbound communication under human control.

## Features and fixes

- Added a **Preventive Planner** action to the AMC Contracts dashboard.
- Generates missing visit proposals from contract dates and visits-per-year commitments.
- Balances proposals across combined open-job and AMC-visit daily workload, avoids Sundays, and shows the planned load for each date.
- Identifies overdue scheduled visits and past-due missing visits as missed obligations requiring follow-up.
- Prepares WhatsApp or email reminder text for visits within the next 45 days.
- Requires dispatcher confirmation before creating visits.
- Saves reminder content with `Draft` status only; ServoERP does not auto-send messages.
- Prevents duplicate visits on retry by rechecking contract and visit number inside a serializable transaction.

## Affected modules

- AMC Contracts dashboard
- AMC visit scheduling
- Customer reminder preparation
- SQL Server schema initialization
- CI and visual smoke-test harness

## Database or migration impact

- Adds `dbo.AMCVisitReminderDrafts` when it does not already exist.
- Adds `IX_AMCVisitReminderDrafts_Visit` for contract, visit, date, and draft-status lookup.
- Existing AMC contract and visit records are not deleted, renamed, or rewritten by the migration.
- Confirming a planner proposal adds missing rows to `AMCVisits` and corresponding unsent reminder drafts. Reopening the planner without confirmation is read-only.

## Configuration and dependency changes

None. No NuGet packages or external services were added.

## Compatibility or breaking changes

None. Existing AMC pages, public types, database tables, and visit-completion workflows remain compatible.

## Security impact

None. The feature uses the existing authenticated SQL Server connection and does not add outbound network calls. Customer contact details remain in the existing business database.

## Deployment and update steps

1. Close ServoERP on the target PC.
2. Deploy the ServoERP 1.1.471 application files using the normal update process.
3. Start ServoERP with access to the authoritative `HVAC_PRO` SQL Server database.
4. Allow startup schema guards to add the reminder-draft table and index.
5. Open **AMC Contracts → Preventive Planner** and review proposals before confirming them.

## Validation performed

- Release compilation with MSBuild 17.14 for .NET Framework 4.7.2.
- Complete ServoERP CI smoke suite, including three deterministic AMC planner scenarios.
- Live SQL Server schema initialization against `HVAC_PRO` and verification of the new table and index.
- Live read-query verification against the current AMC, client, site, visit, and job schema.
- Standalone visual capture of all three planner tabs at 1220 × 790 pixels.

## Known limitations

- Capacity is currently a shared daily slot count across open jobs and AMC visits; technician-specific skills, leave, route distance, and parts availability remain dispatcher review inputs.
- Reminder messages are stored as drafts only. Sending through WhatsApp or email remains an explicit follow-up action.
- The planner does not implement offline field execution or sync-conflict handling for technician visits.

## Rollback guidance

- Reinstall the ServoERP 1.1.470 application files if application rollback is required.
- Leave the additive `AMCVisitReminderDrafts` table and index in place; version 1.1.470 does not use them and they do not need to be dropped.
- Visits already confirmed by a dispatcher are business records and must not be deleted automatically. Review any required schedule correction through the normal AMC visit workflow.
