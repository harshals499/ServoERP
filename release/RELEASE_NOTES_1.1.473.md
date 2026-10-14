# ServoERP 1.1.473 Release Notes

Release date: 04/10/2026

## User-facing summary

ServoERP 1.1.473 rebuilds Reports as a productive Report Explorer. Owners and office teams can locate a report quickly, apply useful business filters, understand profitability visually, refine the preview, and export the result without scrolling through a large card dashboard.

## Features and fixes

- Added a categorized, searchable report library covering financial, service, AMC, inventory, purchase, client/site, and import-review reports.
- Made Job Profitability the focused default workspace with financial-year, client, site, job-status, and incomplete-cost filters.
- Added a monthly revenue, direct-cost, and gross-margin trend using live reporting data.
- Added live revenue, direct-cost, gross-profit, margin, and outstanding summaries.
- Added searchable report results, configurable visible columns, and client/site/status grouping.
- Added Excel and CSV export for the visible rows and selected columns.
- Added saved report-view preferences for the current Windows user.
- Added daily, weekly, or monthly scheduled exports to a user-selected folder while ServoERP is open.
- Preserved Add Expense, profitability workbook import, Service Forms, refresh, and monthly P&L export actions.

## Affected modules

- Reports
- Job profitability reporting
- Report export and local report preferences
- CI smoke-test suite

## Database or migration impact

None. No tables, columns, indexes, or business records are added, changed, deleted, or renamed.

## Configuration and dependency changes

None. No packages or external services were added. Saved views and report schedules are stored in the current Windows user's ServoERP application-data directory.

## Compatibility or breaking changes

None. Existing report data sources, exports, business services, and public module interfaces remain compatible.

## Security impact

No authentication, licensing, machine-ID, or outbound-service behavior changed. Scheduled exports write only to the folder explicitly selected by the user and run only while ServoERP is open.

## Deployment and update steps

1. Close ServoERP on the target PC.
2. Install ServoERP 1.1.473 using the release installer.
3. Start ServoERP and open **Reports**.
4. Run the required report, confirm the filters, and select Excel or CSV before exporting.
5. If automatic export is required, choose a controlled folder and configure the schedule from **Report setup**.

## Validation performed

- Release compilation with MSBuild 17.14 for .NET Framework 4.7.2.
- ServoERP CI-safe smoke suite, including Reports Explorer navigation, chart binding, search, incomplete-cost filtering, and Excel/CSV export.
- Rendered Reports Explorer comparison at 1440 x 1024 pixels against the approved option-2 visual target.
- Verified the release executable exists with file and product version 1.1.473.0.

## Known limitations

- Scheduled exports run only while ServoERP is open.
- Report scheduling currently supports one active local schedule per Windows user.
- Report preview density depends on the available desktop workspace; narrow windows hide the global search/status area before report controls.

## Rollback guidance

- Reinstall ServoERP 1.1.472 if application rollback is required.
- No database rollback is required.
- Saved report-view and schedule files may remain in the Windows user application-data directory; version 1.1.472 does not use them.
