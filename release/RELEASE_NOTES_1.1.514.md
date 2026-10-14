# ServoERP 1.1.514

Release date: 14 October 2026.

## User-facing summary
The global header shows only the original ServoERP logo, with no adjacent ServoERP wordmark.

## Features and fixes
- Remove the wordmark next to the global header logo on all shared main/detail/editor headers.
- Reduce the brand area to the logo's width so search reclaims the unused space.
- Keep the original artwork, accessible brand name, sidebar branding and existing header actions.

## Affected modules
Shared workspace header across main, detail and nested workflow pages.

## Database or migration impact
None. No schema or business-save changes.

## Configuration and dependency changes
None. Existing logo asset retained; no new packages or services.

## Compatibility or breaking changes
None. Existing public interfaces, shortcuts, search/account/notification handlers and sidebar remain.

## Security impact
None. No new permissions, credentials or outbound calls.

## Deployment/update steps
Install through the existing ServoERP installer/updater. Harshal explicitly requested immediate bumped publication. No manual configuration or database migration is required.

## Validation performed
Release build succeeded and SOURCE_CODE/bin/Release/HVAC_Pro_Desktop.exe exists. Native screenshots of Quotations, Client Detail and Add AMC Equipment were captured and inspected; the header shows only the logo and the search uses the reclaimed space. Sidebar code/artwork are unchanged. Evidence: TEST_RESULTS/header-514-logo-only and header-514-build.log. Render-capable checks use the disposable HVAC_PRO_UI_20261014 database; no customer business saves were invoked.

## Known limitations
Representative screenshots do not exhaust every DPI, role or data length. Existing distribution remains unsigned.

## Rollback guidance
Retain the verified 1.1.513 installer and normal backups. Close ServoERP and reinstall 1.1.513 if required. Do not automatically revert customer data.
