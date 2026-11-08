# ServoERP 1.1.532

Release date: 08/11/2026.

## User-facing summary
Restore English / मराठी selection in Settings and Marathi presentation across ServoERP desktop workflows, while preserving business data and India-first formatting.

## Features and fixes
- Persist language per Windows user and restore it before login; preserve English for users without a Marathi preference.
- Ship 3999 Marathi .NET resource entries, English fallback, formatted captions, validation and notification text, menus, grid headers, empty states and search hints.
- Complete the previously identified 86 English captions and additional dashboard, payroll, contract, service, guide and backup captions discovered during native review.
- Translate built-in dropdown presentation while retaining canonical item objects and saved values. Preserve custom lookup labels, customer names, identifiers and editable data.
- Repair Settings routing, workspace title clipping, invoice risk display and clean database initialization ordering.
- Translate invoice/job/report PDF body labels. Existing PDF headers remain unchanged.

## Affected modules
Settings, startup/login, navigation, clients, vendors, jobs, invoices, reports, purchasing, inventory, payments, contracts/AMC, employees, attendance, payroll, quotations, SLA, site monitoring, Service Desk, WhatsApp Hub shell, master data, PDF imports, user guide, backups and shared UI.

## Database or migration impact
Guarded additive nullable InvoiceLineItems.JobID column; existing uniqueness guards now run after their tables are created. No table/column drops or renames, LicenseInfo changes or destructive data migration. Write-capable QA uses explicitly named disposable ServoERP_MarathiQA databases only.

## Configuration and dependency changes
Per-user preference: %LOCALAPPDATA%/ServoERP/Preferences/language.txt. Include version-matched mr-IN and hi-IN satellite assemblies. No production package, downloaded language pack, new outbound service or credentials added. Existing Windows Nirmala UI renders Devanagari; icon and technical fonts remain intact. Review tooling uses existing Visual Studio Roslyn libraries.

## Compatibility or breaking changes
None. Public API names, licensing, pricing, plans, Fresh Start and PDF headers remain intact. en-IN date, currency and GST behavior remains. Apply controls immediately where supported; follow the Settings restart guidance to refresh every existing screen.

## Security impact
No new outbound service or secret store. User-entered data, database values and technical logs remain intact. Finalized invoice edit protections and persisted subjects are checked in both languages. No customer database writes were performed.

## Deployment/update steps
Install the verified Velopack Setup or use the existing desktop updater. Preserve company configuration and database backups. Restart after choosing मराठी to refresh all existing screens. Deploy satellites and these version-matched notes with the EXE. Publish only after final workflow, visual, notes and artifact guards pass.

## Validation performed
Release build and executable existence pass. The completed pre-final suite passed 4559 assertions, mostly resource and invariant assertions, not thousands of separate workflows. Fresh disposable databases started with zero user tables and initialized twice. Existing business E2E and ten create/update/payment/attendance regressions per language passed. Native captures cover login, Settings and 14 Settings routes, navigation, confirmation, invoice/report previews and 22 further module surfaces in both languages. Tests cover persistence/fallback, canonical choices, data preservation, notifications, dynamic controls and embedded notes. Final shipping evidence is recorded in the localization acceptance report. Four synthetic English/Marathi invoice and job-card PDFs were generated without normal startup; Marathi body glyphs were visually inspected. Cumulative last-50-tag ancestry and diff whitespace checks pass.

## Known limitations
Existing PDF headers are preserved under the approval restriction. Windows-owned dialogs/buttons and embedded external WhatsApp Web follow their own language settings. Customer-entered text, custom database lookup labels, business identifiers, technical abbreviations/logs and en-IN dates remain unchanged. Resource and rendered audits are conservative; native captures and service workflows do not prove every possible user state or click sequence. Normal Program entry was not invoked because it closes other app instances; a separate STA harness exercised the actual Release assembly while preserving the running user app. Some pre-existing Service Desk controls clip in both languages at the captured viewport. No claim of exhaustive linguistic certification is made.

## Rollback guidance
Retain verified 1.1.531 and its full package. Restore the prior desktop package through the existing deployment process if necessary; preserve user configuration and business database. The additive nullable column can remain. Never automatically roll back customer data. Language can be switched back to English in Settings.
