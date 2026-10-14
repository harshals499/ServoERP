# ServoERP 1.1.505
Release date: 11/10/2026

## User-facing summary
A consistent, balanced numbered pagination control makes lists easier to browse across ServoERP.

## Features and fixes
Replace the shared footer with the selected Option 1 design: record range on the left, rows-per-page selection, Previous/Next buttons, numbered pages and a blue current-page highlight. Keep the current page visible among ellipses and disable navigation at boundaries. Adapt to two aligned rows on narrower cards. Preserve custom page sizes and clamp navigation after filtering. Add keyboard shortcuts: Alt+Left/Right and Ctrl+Home/End while the footer has focus, plus accessible button names and tooltips. Replace notification-specific navigation and fix scrollbar synchronization when page size changes. Replace payroll placeholder pagination with real row paging that retains row metadata. Reserve footer space through docking and shared host sizing; protect the footer from global button restyling.

## Affected modules
Clients, Contracts, Jobs, Inventory, Payments, Purchases, Suppliers, Payroll, Notifications, and modules derived from VirtualListModuleBase. Shared UI theme and button helpers now respect the pagination component's own geometry and styling.

## Database or migration impact
None. No schema changes or business-data writes are required by this pagination change.

## Configuration and dependency changes
Version metadata updated to 1.1.505. No packages added. Existing light-theme colours are reused. Release metadata now links to the public ServoERP-Releases package repository.

## Compatibility or breaking changes
None. GlobalPaginationControl public properties, methods and navigation events are retained. Programmatic SetState calls remain silent. Page-size changes reset to page one.

## Security impact
None. No new outbound services, permission changes, or changes to licensing.

## Deployment/update steps
Install the version-matched ServoERP installer or use the desktop updater. Publish the validated Velopack feed and packages together, with these notes included. Existing database and local configuration remain in place.

## Validation performed
The version-matched Release rebuild passed with zero warnings and errors; SOURCE_CODE/bin/Release/HVAC_Pro_Desktop.exe exists. The focused WinForms verification passed record counts from zero to one million, first/middle/last pages, numbered-button clicks, Previous/Next clicks, page-size reset, custom sizes, invalid-page clamping and silent state updates at 280, 300, 340, 420, 560, 600, 680, 760, 900 and 1200 pixels. Actual module footer fixtures passed at 420, 560, 680, 800 and 1000 pixels, including Clients, Contracts, Payments, Purchases, Jobs and Inventory; Supplier fixtures passed at 420, 560 and 900 pixels. Payroll and notification row paging and narrow layouts passed. Existing pagination policy regressions and the complete /cismoketest suite passed. Wide and narrow WinForms captures were inspected. The release pipeline validated the setup executable, full package, delta package and updater feed; notes in the feed are nonblank and version-matched.

## Known limitations
Fixtures use synthetic pagination states and sample grid rows rather than an end-to-end customer database session. Customer-PC rollout, update installation on customer PCs, and non-default Windows DPI settings have not been tested. Packages use the existing unsigned release configuration. At very narrow widths, fewer numbered buttons are shown; Previous/Next remain available.

## Rollback guidance
Reinstall the previous supported release, 1.1.504. No database migration needs reversal. Restore the previous public updater feed if a deployment must be rolled back.
