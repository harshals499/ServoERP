# ServoERP 1.1.520
Release date: 24 October 2026.

## User-facing summary
Jobs is now in the Operations section of the sidebar. Open Operations > Jobs and choose Add New Job to create a draft.

## Features and fixes
Move the existing Jobs entry from its separate section into Operations, between Suppliers and AMC. Remove the separate Jobs heading. Keep the existing Jobs dashboard, creation workflow and 1.1.519 entitlement compatibility unchanged.

## Affected modules
Main sidebar navigation; sidebar regression scripts.

## Database or migration impact
None.

## Configuration and dependency changes
None. No packages added.

## Compatibility or breaking changes
None. Public APIs, pricing, plan names and license entitlements unchanged.

## Security impact
None. Existing authentication, module entitlements and business-write checks are preserved.

## Deployment/update steps
Next-release candidate; install through existing setup/updater after publication. Preserve private configuration and business data.

## Validation performed
Release rebuild succeeded and HVAC_Pro_Desktop.exe exists. Jobs/Business AMC entitlement regressions passed. Eight built sidebar previews across Basic, Standard, Pro and Enterprise at 720/900 heights passed: exactly one Jobs entry inside Operations and no standalone Jobs heading. Screenshot inspection confirmed Jobs between Suppliers and AMC. The existing Add New Job workflow opened an editable draft in an explicitly generated disposable SQL database with zero saved jobs. Embedded-note and package/updater guard results are retained with candidate artifacts. No customer database was used.

## Known limitations
Not published yet. No customer installation is upgraded and no business job is saved during validation. Publication is subject to the normal rolling 24-hour release limit unless Harshal requests another explicit exception.

## Rollback guidance
Retain the previous verified v1.1.519 binaries and assets. Preserve all private configuration and business data. The updater does not downgrade automatically. No database rollback required.
