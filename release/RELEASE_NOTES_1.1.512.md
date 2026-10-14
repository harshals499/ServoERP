# ServoERP 1.1.512

Release date: Not published; prepared 14 October 2026.

## User-facing summary
The main My Work dashboard now uses the same actual card, metric, heading, action-button and filter primitives as Quotations, extending the previous visual consistency work beyond matching colours.

## Features and fixes
- Share quotation-style outlined card surfaces and integrated icon headings with My Work.
- Match metric title/value/subtitle typography, 42-pixel icon containers, alignment and semantic accent colours.
- Match primary/secondary buttons and selected/inactive filter chips.
- Use a pale table-heading strip for today's jobs and reserve subtitle space beside card actions.
- Wrap metrics into two rows at narrower desktop widths so complete currency values have room.
- Retain navigation shortcuts, role filtering, technician ownership, inbox pagination, hover previews and empty-data behavior.
- Dispose per-control icon images when their dashboard controls are disposed.

## Affected modules
My Work/main dashboard, Quotations shared dashboard presentation, dashboard regression tests and screenshot fixture. DashboardVisuals.cs is the shared implementation used by both pages.

## Database or migration impact
None. No business-save or schema changes. Render-capable tests use the existing explicitly disposable HVAC_PRO_UI_20261014 diagnostic database.

## Configuration and dependency changes
None. No new packages or services.

## Compatibility or breaking changes
None. Existing public module boundaries and dashboard callbacks are retained. Light theme, sidebar and business content remain.

## Security impact
No new outbound services, credentials or permissions. Dashboard tests verify restricted modules do not leak records and technician previews remain limited to assigned jobs.

## Deployment/update steps
This is an unpublished release candidate. Publish only after the existing rolling 24-hour release window permits it; the previous verified release is 1.1.511 published 14 October 2026 at 20:00:06 UTC. Once published, install through the existing ServoERP installer/updater and close the application normally when requested.

## Validation performed
Release 1.1.512 build passed with the executable present in SOURCE_CODE/bin/Release. All 39 CI-safe checks and nine dashboard regression groups passed. Dashboard regressions cover responsive widths 1440/1000/760, shared metric/card presentation, working filter clicks, pagination, shortcuts, permissions, ownership and empty states. Native My Work and Quotations captures were compared at matching 1710px viewports; additional 1000px hosted captures and populated/empty responsive snapshots were inspected. Action/subtitle overlap and narrow toolbar ordering were corrected and recaptured. Version/embedded-note and Velopack artifact results are retained beside the candidate artifacts before any publication. No public release is claimed.

## Known limitations
Not published yet; no claim of remote release verification. Native Windows control rendering and workflow-specific content remain. Screenshot coverage does not exhaust every DPI, role or record. The existing packaging pipeline is unsigned.

## Rollback guidance
Retain the verified 1.1.511 installer and configuration/database backups. If this candidate is deployed and a rollback is needed, close ServoERP and reinstall 1.1.511. Do not automatically revert customer data.
