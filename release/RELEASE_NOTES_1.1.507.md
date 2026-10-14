# ServoERP 1.1.507

Release date: 11 October 2026

## User-facing summary
Global search now uses the approved softer split layout: results on the left and the selected record's preview on the right. The sidebar is exactly unchanged.

## Features and fixes
- Added category filters and readable result rows with module icons, status, dates and invoice amounts.
- Job previews show client, site, technician, job type, scheduled date, status and description. Invoice previews show client/site, dates, total and balance due in INR. Other modules show their existing available record details.
- Ctrl+K focuses/selects the query; Up/Down select results; Enter opens the selected record; Escape closes. Double-click and the primary Open button retain existing navigation.
- Search is debounced and asynchronous. Stale responses are discarded, searches are serialized and loading, empty and error states are explicit.
- Permission checks apply before querying and opening results; technician job searches are scoped to recorded assignments. Results are interleaved across modules within the limit to avoid client records crowding out jobs or invoices.

## Affected modules
Global search, existing dashboard/header search entry points, shared form styling guards scoped to this dialog. Sidebar implementation unchanged.

## Database or migration impact
None. Read-only queries use existing SQL Server tables and parameterized Dapper. No business-data writes or schema changes.

## Configuration and dependency changes
None. No new packages or services.

## Compatibility or breaking changes
None. Existing public GlobalSearchDialog constructor, GlobalSearchService.Search and record-navigation interfaces retained. Search result metadata additions are compatible.

## Security impact
Added module view permission enforcement, technician job assignment filtering and permission rechecking before navigation. No new outbound service calls.

## Deployment/update steps
Close ServoERP and install the version-matched package. Existing connection configuration and business data are retained. No migration required.

## Validation performed
Release build succeeded with HVAC_Pro_Desktop.exe. Eight fixture checks passed for previews, Indian invoice amounts, filters, Ctrl+K, arrows/Enter, Escape, responsive actions, stale results and denied permissions. Three read-only SQL checks passed against the configured database, including real job-assignment and module restrictions. Seven dashboard regression checks also passed. Native WinForms review verified the selected job and invoice previews and keyboard navigation. MainForm.cs SHA256 is unchanged from before this task.

## Known limitations
Search requires two characters and shows up to 120 results; category counts apply to that returned set. Refine broad searches. Other modules preview the details already supplied by their search query. Long previews scroll at small dialog sizes. Windows native dialog framing replaces the image's custom modal frame; the underlying dashboard is not dimmed. Installer signing remains unconfigured.

## Rollback guidance
Close ServoERP and reinstall 1.1.506. No database rollback required.
