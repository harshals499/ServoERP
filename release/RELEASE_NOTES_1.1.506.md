# ServoERP 1.1.506

Release date: 11 October 2026

## User-facing summary
My Work now uses a softer, light dashboard with compact operational cards and live hover previews. The sidebar is unchanged.

## Features and fixes
- Added priority inbox, today's jobs, team assignments, upcoming obligations, collections and recent activity in a compact two-column layout, stacking on narrower windows.
- Added a shared hover/F1 preview showing the meaning of each card and up to four matching records, without database queries on hover.
- Retained existing action routing, permission checks, priority filters and automatic refresh.
- Team cards describe recorded assignments; they do not infer employee availability. Collections distinguish payment receipts from invoice balances instead of combining unrelated cohorts into a percentage.

## Affected modules
My Work dashboard. Sidebar implementation is unchanged.

## Database or migration impact
None.

## Configuration and dependency changes
None. No new packages or external services.

## Compatibility or breaking changes
None. Existing public dashboard callbacks and navigation remain compatible.

## Security impact
Dashboard summaries and previews are filtered by module view permissions. No new outbound calls.

## Deployment/update steps
Install the version-matched desktop package after closing ServoERP. Existing data and connection configuration are retained. No migration is required.

## Validation performed
Release build succeeded and HVAC_Pro_Desktop.exe was produced. Seven dashboard smoke checks and six existing action-center smoke checks passed. Native WinForms visual review verified the actual unchanged sidebar, softer layout, readable card actions and screen-bounded hover previews. Responsive layouts were checked at 1440, 1000 and 760 pixels. Fixture tests exercised permission filtering, technician ownership, pagination, callbacks and empty states; no business records were changed.

## Known limitations
Hover previews show up to four records plus the remaining count. The team panel reports scheduled assignments, not live attendance or technician location. Smaller windows scroll vertically.

## Rollback guidance
Close ServoERP and reinstall the previous approved desktop version. No database rollback is needed for this dashboard change.
