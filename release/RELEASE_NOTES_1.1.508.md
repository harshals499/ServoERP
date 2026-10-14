# ServoERP 1.1.508

Release date: 11 October 2026

## User-facing summary
Option 2's softer two-level header is shared across module pages and detail views: global search, notifications and account controls above the page breadcrumb, title, description, date and existing actions. The sidebar is exactly unchanged.

## Features and fixes
- Added a centred, keyboard-focusable search command with a contained Ctrl+K hint. It opens the existing record-preview search; module filters remain editable and independent. User Guide also handles Ctrl+K outside its header and returns to the main app when a search record is opened.
- Added account menus with the signed-in name and role, change-password and Settings actions. Alerts open the existing notification centre. Reused the dashboard's existing notification count and Customize workflow, and added a real dashboard refresh action that prevents overlapping refresh requests.
- Preserved existing page action controls, event handlers, permissions, enabled states and changing status labels. Softened action borders and supplied reliable header icons for common actions.
- Headers wrap according to available space. Compact search, headings, local filters, metadata and action rows have separate space. Absolute table rows adapt to the new header height.
- Converted custom Reports, Site Monitor, Porter Deliveries, User Guide, Client Detail and Job Detail headers; added matching chrome to the retained Service Desk view. Fixed Site Monitor's Filters action to focus its region filter.
- Protected header styling from general page polish and layout passes. Removed duplicate account metadata and the extra outline around local header filters.

## Affected modules
My Work, Clients, Contracts, Invoices, Payments, SLA, Quotations, Reports, Settings, Suppliers, Purchases, Inventory, Employees, Payroll, Site Monitor/Dispatch, Jobs, Master Data, WhatsApp Hub, AMC, Attendance, Porter Deliveries, User Guide, client/job detail views and retained Service Desk. Shared header and UI-polish infrastructure. Sidebar implementation unchanged.

## Database or migration impact
None. No schema changes or business-data migrations. Existing search and module actions retain their existing data services.

## Configuration and dependency changes
None. No new packages or third-party services.

## Compatibility or breaking changes
None. Public classes, methods and record-navigation interfaces retained. Existing shared-header result labels and action hosts remain available to their callers.

## Security impact
None. Existing module permissions, search restrictions and validation remain in effect. No new outbound service calls.

## Deployment/update steps
Close ServoERP and install the version-matched package. Existing connection settings and SQL Server business data are retained. No migration is required.

## Validation performed
Release build succeeded with HVAC_Pro_Desktop.exe version 1.1.508. Shared-header checks passed for 21 module titles at 1440, 1000, 760 and 600 pixels, retaining handlers, disabled states, filters, status and live breadcrumbs without utility/title/status/action overlaps. Coverage checks constructed all 25 module/detail/guide views. Account actions are enabled in the actual shell and owned dialogs; User Guide-wide Ctrl+K and cancelled-search retention passed. Eight search and seven dashboard regression checks passed. Three read-only SQL search checks passed, including technician and module restrictions. Native review compared built dashboard, client, report and invoice headers against option 2 and verified click/Ctrl+K search entry, Escape, notification loading, Customize's menu, dashboard refresh and the change-password dialog. MainForm.cs SHA256 remains identical to the task baseline.

## Known limitations
Validation uses fixtures and safe UI interactions; save, delete, password changes and other business writes were not executed. The underlying modules retain their existing database requirements. Long titles use ellipsis; dense actions wrap on smaller screens. Installer signing remains unconfigured.

## Rollback guidance
Close ServoERP and reinstall 1.1.507. No database rollback is required.
