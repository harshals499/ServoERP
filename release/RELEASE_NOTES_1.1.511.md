# ServoERP 1.1.511

Release date: 14 October 2026

## User-facing summary
ServoERP now uses the Quotations dashboard visual rules throughout its shared page and dialog surfaces: white outlined cards, a pale neutral canvas, integrated headings, blue actions and calm table selection.

## Features and fixes
- Share quotation card outline and canvas tokens across modules and dialogs.
- Apply appearance to controls created later inside nested containers, tab pages and modal editors without changing entered values or business actions.
- Bring My Work card headings, borders and action colours into the same visual family; reserve heading space for card actions.
- Keep semantic status colours and improve text contrast on dark action buttons.
- Apply consistent table backgrounds, header colours and readable pale selection.
- Apply quotation surfaces to expandable detail sections and clear inherited docking on shared header actions.
- Add regression coverage for late-created nested controls, preserving geometry, input values and warning colours.

## Affected modules
Shared theme, main module pages, My Work, Quotations, detail pages, expandable sections, nested controls and dialogs. The validation inventory is retained in Docs/UI_QUOTATION_CONSISTENCY.md and TEST_RESULTS/quotation-consistency.

## Database or migration impact
None for the UI change. The internal diagnostic connection override also covers direct connection resolution so render tests use the explicitly disposable HVAC_PRO_UI_20261014 database. UI tests do not save business records to the production database. The approved 1.1.510 baseline and its existing upgrade behavior are retained.

## Configuration and dependency changes
No new packages. The existing clean client packaging configuration is included in the isolated source snapshot; tenant credentials are not packaged.

## Compatibility or breaking changes
None. Public classes/methods and business workflows retain their names and behavior. Light theme and sidebar remain.

## Security impact
No new third-party services, credential provisioning, licensing endpoint changes or business-data permissions. Publication artifacts are checked for tenant-local configuration/data exclusions.

## Deployment/update steps
Install the version-matched Setup or update through the existing ServoERP GitHub updater. Close ServoERP normally when the updater requests installation. Retain existing office configuration and database backups.

## Validation performed
- Release 1.1.511 build passed with executable present in SOURCE_CODE/bin/Release (0 reported build errors).
- 39 CI-safe checks passed through the existing test entrypoint in an STA reflection host using normal theme/PDF initialization and an explicitly disposable SQL diagnostic scope.
- 24 native WinForms screen captures passed and were reviewed, including main modules, loaded Settings, populated Client/Job details and AMC/editor dialogs. Dark-button contrast and heading/action overlaps were corrected and recaptured.
- Nested late-created card/grid inheritance regression passed while preserving bounds, entered values and warning colours.
- Version/assembly/embedded release-note guard and Velopack artifact validation passed. Setup, portable ZIP, full update package and updater feed were generated with nonblank version-matched notes. Checksums and validation output are retained beside artifacts.

## Known limitations
The existing packaging pipeline is unsigned. Native Windows controls retain platform rendering. Recorded screenshot coverage does not exhaust every record, role, nested dialog, scroll position or DPI setting. Semantic status colours are preserved. This is a visual consistency release, not a redesign of business content or charts.

## Rollback guidance
Retain the previous verified v1.1.510 installer and release reference. Close ServoERP and reinstall that version if rollback is required, preserving configuration and database backups. Do not roll back or delete customer business data automatically.
