# ServoERP 1.1.476 Release Notes

Release date: 04/10/2026

## User-facing summary

ServoERP 1.1.476 adds practical filters to **All Sites by Region - Live Work**. Site Monitor users can now find a site or job quickly, narrow the table to one region, and focus on sites with active work, no active work, critical or SLA-risk work, or unassigned technicians.

## Features and fixes

- Added a site and work search covering region, site, client, current work, job number, technician, status, and priority.
- Added a Region selector populated from all regions in the current site list.
- Added Work filters for **Active work**, **No active work**, **Critical / SLA risk**, and **Unassigned technician**.
- Allowed Search, Region, and Work filters to be combined.
- Added a live **shown of total sites** count and a one-click **Clear** action.
- Preserved the selected region when Site Monitor data refreshes if that region remains available.
- Prevented interaction with the filter bar from opening the existing work-details drill-down.
- Kept the primary site table full-width and retained all operational cards below it.

## Affected modules

- Site Monitor
- All Sites by Region - Live Work
- Site Monitor CI-safe smoke coverage

## Database or migration impact

None. No tables, columns, indexes, business records, or migrations are added, changed, deleted, or renamed.

## Configuration and dependency changes

None. No packages, external services, or configuration keys were added or changed.

## Compatibility or breaking changes

None. Existing customer sites, jobs, technicians, invoices, region resolution, table drill-downs, and Site Monitor KPIs remain compatible.

## Security impact

None. This release does not change authentication, authorization, licensing, machine identification, credential storage, or outbound network behaviour.

## Deployment and update steps

1. Close ServoERP on the target PC.
2. Install ServoERP 1.1.476 using the public installer or allow the automatic updater to apply it.
3. Start ServoERP and open **Site Monitor**.
4. Use Search, Region, or Work above **All Sites by Region - Live Work**; select **Clear** to restore all sites.

## Validation performed

- Release compilation with MSBuild 17.14 for .NET Framework 4.7.2 with zero warnings and zero errors before release packaging.
- CI-safe smoke suite passed, including combined site search and work-state filter coverage.
- Rendered Site Monitor at 1512 x 900 pixels against the configured SQL Server data and visually inspected the filter labels, selectors, live count, Clear action, table width, and lower-card placement.
- Confirmed filter controls are excluded from the table work-details drill-down handler.
- Confirmed `SOURCE_CODE/bin/Release/HVAC_Pro_Desktop.exe` is produced.

## Known limitations

- Filters apply to the currently loaded live Site Monitor data; use the page refresh control to load newer server data.
- The primary row continues to show the most urgent active job plus an additional-work count; double-click the table to see every active job as a separate record.

## Rollback guidance

- Reinstall ServoERP 1.1.475 if application rollback is required.
- No database rollback is required.
