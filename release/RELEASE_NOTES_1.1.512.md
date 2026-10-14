# ServoERP 1.1.512

Release date: 14 October 2026.

## User-facing summary
The main dashboard now follows the selected Daily Dispatch design: a compact header, one summary band, schedule and attention panels, team assignments, upcoming work, collections and recent activity.

## Features and fixes
- Reorganize the main dashboard around today's schedule and actionable work, with a responsive stacked layout on smaller windows.
- Add a schedule time rail, assignment badges and clear Assign/Open actions using the existing job workflow.
- Retain all eight attention filters in a compact menu, pagination, hover previews, shortcuts and permission checks.
- Present overdue balances, monthly receipts and monthly invoice balances as distinct financial cohorts.
- Bundle official Lucide 0.468.0 icons and license, resolve icons beside the installed executable, and support SVG elliptical arcs in the existing renderer.
- Deliver the selected design in version 1.1.512.

## Affected modules
Main dashboard, optional compact shared workspace header, icon loading/rendering, shared dashboard primitives and dashboard regression tests. Other pages retain their existing header mode.

## Database or migration impact
None. No business-save or schema changes. Render-capable tests use the existing explicitly disposable HVAC_PRO_UI_20261014 diagnostic database.

## Configuration and dependency changes
Bundled Lucide 0.468.0 SVG assets and ISC/Feather license. No new NuGet packages, services or runtime network calls.

## Compatibility or breaking changes
None. Existing public module boundaries and dashboard callbacks are retained. Light theme, sidebar and business content remain.

## Security impact
No new outbound services, credentials or permissions. Dashboard tests verify restricted modules do not leak records and technician previews remain limited to assigned jobs.

## Deployment/update steps
Install through the existing ServoERP installer/updater and close the application normally when requested. Harshal explicitly authorized immediate publication of this verified release. Previous release: 1.1.511. No database migration or configuration change is required.

## Validation performed
Release build and native dashboard captures are retained in TEST_RESULTS/dispatch-final. Dashboard regression groups verify responsive 1440/1000/760 widths, summary typography, attention menus, pagination, routing, restricted modules, technician ownership and empty states. Tests use the explicitly disposable database; no customer business writes. Final check and artifact logs accompany the prepared candidate. Release-note and Velopack artifact guards passed for the prepared package; remote assets and feed are verified during publication.

## Known limitations
Native Windows control rendering and workflow-specific content remain. Screenshot coverage does not exhaust every DPI, role or record. The existing packaging pipeline is unsigned.

## Rollback guidance
Retain the verified 1.1.511 installer and configuration/database backups. If this candidate is deployed and a rollback is needed, close ServoERP and reinstall 1.1.511. Do not automatically revert customer data.
