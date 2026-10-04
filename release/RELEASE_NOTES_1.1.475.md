# ServoERP 1.1.475 Release Notes

Release date: 04/10/2026

## User-facing summary

ServoERP 1.1.475 makes Site Monitor site-first. The page now begins with a full-width **All Sites by Region - Live Work** table showing every customer site and the work currently happening there. Supporting cards remain available immediately below the primary site list.

## Features and fixes

- Promoted the regional site list to the primary full-width Site Monitor view.
- Listed every master customer site, including sites that currently have no job.
- Added region, site, client, current work, technician, scheduled date/time, status, priority, and open-work count columns.
- Displayed a clear **No active work / Available** state for sites without current jobs.
- Summarized the most urgent current job and showed the number of additional active jobs on the same site.
- Added work-detail tooltips and a double-click drill-down containing every active job per site.
- Joined jobs to customer sites by exact site ID, preventing similarly named sites from receiving one another's work.
- Counted critical and SLA-risk work from active jobs only, so completed emergencies no longer colour an available site as critical.
- Preserved invoice-based site revenue attribution, including credit notes, cancelled invoices, ambiguous company-level invoices, and invoice-only companies.
- Moved Site Status, Upcoming Maintenance, Problematic Sites, Immediate Attention, Equipment, Technician Presence, Revenue, SLA, and Health Trend cards below the primary site table.

## Affected modules

- Site Monitor
- Job summary projection
- Customer site and client reference loading
- Site Monitor work-details dialog
- Site Monitor CI-safe smoke coverage

## Database or migration impact

None. No tables, columns, indexes, business records, or migrations are added, changed, deleted, or renamed.

## Configuration and dependency changes

None. No packages, external services, or configuration keys were added.

## Compatibility or breaking changes

None. Existing jobs, customer sites, technicians, invoices, revenue attribution, Site Monitor KPIs, and drill-down cards remain compatible.

## Security impact

None. This release does not change authentication, authorization, licensing, machine identification, credential storage, or outbound network behaviour.

## Deployment and update steps

1. Close ServoERP on the target PC.
2. Install ServoERP 1.1.475 using the public installer or allow the automatic updater to apply it.
3. Start ServoERP and open **Site Monitor**.
4. Review **All Sites by Region - Live Work** at the top of the page.
5. Hover over a work cell for a concise job summary or double-click the table for the complete active-work detail list.

## Validation performed

- Release compilation with MSBuild 17.14 for .NET Framework 4.7.2 with zero warnings and zero errors.
- CI-safe smoke suite passed, including Site Monitor billed-revenue reconciliation.
- Added and passed coverage confirming a master site without jobs remains visible with **No active work**.
- Rendered Site Monitor at 1512 x 900 pixels against the configured SQL Server data and visually inspected the full-width primary table, column readability, status colours, scrolling, and card placement.
- Confirmed `SOURCE_CODE/bin/Release/HVAC_Pro_Desktop.exe` is produced.

## Known limitations

- The primary row shows the most urgent active job plus an additional-work count; double-click the table to see every active job as a separate record.
- Region uses the site's configured city when available and falls back to the existing Site Monitor region resolver for incomplete master data.

## Rollback guidance

- Reinstall ServoERP 1.1.474 if application rollback is required.
- No database rollback is required.
