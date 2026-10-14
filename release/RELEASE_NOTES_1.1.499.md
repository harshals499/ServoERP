# ServoERP 1.1.499

Release date: 2026-10-06.

## User-facing summary

The Porter Deliveries page has been removed from ServoERP navigation.

## Features and fixes

- Operations no longer shows Porter Deliveries.
- Page creation and module-name shortcuts no longer open the removed page.
- Existing navigation indices for other pages remain unchanged.

## Affected modules

MainForm navigation and page routing; UI QA module catalog.

## Database or migration impact

None. Existing delivery tables and records are retained. No business-data deletion or schema migration is included.

## Configuration and dependency changes

None. Existing persistent database connection and public update channel are retained. Packages added: None.

## Compatibility or breaking changes

The Porter Deliveries page is no longer accessible. Existing public delivery classes remain available for compatibility. Other pages retain their navigation indices.

## Security impact

None.

## Deployment/update steps

Update using Settings > Check for Updates or install the public ServoERP installer. Clients still using the old private update channel must run the previously published client repair script once. Review the installed release notes after login.

## Validation performed

- Release build succeeded; the desktop executable exists at version 1.1.499.0.
- CI smoke suite passed using an isolated harness with the application binding redirects, theme and PDF initialization.
- Built sidebar rendered and visually inspected; Operations shows Site Monitor, Inventory, Clients, Suppliers, Jobs and AMC with no Porter Deliveries. Evidence: TEST_RESULTS/sidebar-refresh-1.1.499.png.
- Installed-version release notes and Settings notes controls rendered successfully.
- Public package/feed validation is recorded beside the release artifacts before publication.

## Known limitations

Existing delivery records are retained but cannot be managed through the removed page. The Site Monitor external Porter booking link remains available. Remote client deployment is not part of this release. Installer and executable are unsigned.

## Rollback guidance

Reinstall the previous public application version while preserving the persistent database configuration. No database rollback is needed.
