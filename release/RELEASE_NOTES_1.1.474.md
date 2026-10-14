# ServoERP 1.1.474 Release Notes

Release date: 04/10/2026

## User-facing summary

ServoERP 1.1.474 makes Reports interactive and safer to review. Hovering over report charts now shows the exact business values behind each point, and selecting any report-table row opens a read-only PDF preview instead of an editable form.

## Features and fixes

- Added chart hover details to Job Profitability, including month, revenue, direct cost, gross profit, and gross margin.
- Added report-aware hover details to every other Reports library chart, including the category and matching record count.
- Made a single click on any Reports table row open a read-only PDF preview.
- Reused an existing linked PDF when one is attached to the selected business record.
- Generated the established invoice or job PDF when the selected row links to an invoice or job without a stored PDF.
- Added a branded ServoERP report-row PDF for AMC contracts, inventory, supplier advances, clients/sites, import review, and other records without a native document PDF.
- Included every report column in the generated PDF detail view, even when the grid has been configured to hide a column.
- Added visible “Click row for PDF” guidance above the table and a hand cursor over selectable rows.
- Added automated validation for chart tooltips and PDF output across all 11 Reports library destinations.
- Made CI smoke-test reports portable and ensured report preview tests do not require a live SQL Server.

## Affected modules

- Reports Explorer
- Report charts
- Report preview table
- PDF generation and document preview routing
- CI-safe smoke tests

## Database or migration impact

None. No tables, columns, indexes, or business records are added, changed, deleted, or renamed.

## Configuration and dependency changes

None. No packages or external services were added.

## Compatibility or breaking changes

None. Existing Reports filters, saved views, scheduled exports, Excel/CSV exports, and business document data remain compatible.

## Security impact

Report row previews are read-only and do not save, update, or delete business records. No authentication, licensing, machine-ID, or outbound-service behaviour changed.

## Deployment and update steps

1. Close ServoERP on the target PC.
2. Install ServoERP 1.1.474 using the public release installer or allow the automatic updater to apply it.
3. Start ServoERP and open **Reports**.
4. Hover over a chart point to inspect its business values.
5. Select a report row to open the related document or generated report summary as a PDF preview.

## Validation performed

- Release compilation with MSBuild 17.14 for .NET Framework 4.7.2.
- CI-safe smoke suite covering all 11 Reports library destinations.
- Verified every visible profitability chart point exposes month, revenue, direct cost, gross profit, and margin details.
- Verified every Reports schema generates a valid PDF preview payload.
- Rendered and visually inspected the read-only report PDF at 150 DPI.
- Captured and visually inspected the Reports Explorer at 1440 x 1024 pixels.
- Captured an actual chart hover tooltip on this PC showing exact financial values.
- Verified the release executable exists with file and product version 1.1.474.0.

## Known limitations

- PDF previews open in the Windows default PDF viewer.
- Rows without a stored or native business-document PDF use a generated read-only report summary containing the available report fields.

## Rollback guidance

- Reinstall ServoERP 1.1.473 if application rollback is required.
- No database rollback is required.
