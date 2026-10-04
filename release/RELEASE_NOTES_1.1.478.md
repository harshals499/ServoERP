# ServoERP 1.1.478 Release Notes

Release date: 04/10/2026

## User-facing summary

ServoERP 1.1.478 makes Site Monitor available with every activated ServoERP license. Client PCs on Trial, Starter AMC, Growth AMC, or Business AMC will see Site Monitor under Operations after updating and restarting.

## Features and fixes

- Included Site Monitor in every license-plan module catalog, including Trial and Starter AMC.
- Added upgrade compatibility for older client license snapshots whose cached module list does not contain Site Monitor or GeoIntelligence.
- Kept license activation enforcement in place; missing or tampered licenses do not receive Site Monitor access.
- Added regression coverage for Trial, Basic, Standard, Pro, Enterprise, legacy explicit-module snapshots, missing licenses, and absent license data.

## Affected modules

- Main sidebar module visibility
- Licensing entitlement catalog
- Site Monitor
- CI-safe UI policy smoke tests

## Database or migration impact

None. No tables, columns, indexes, business records, or migrations are added, changed, deleted, or renamed.

## Configuration and dependency changes

None. No packages, external services, or configuration keys were added or changed.

## Compatibility or breaking changes

No breaking changes. Site Monitor is now included with every activated license. Existing licenses and cached module lists remain compatible without reactivation.

## Security impact

License activation remains required. Missing and tampered license states remain excluded from Site Monitor access.

## Deployment and update steps

1. Close ServoERP on the target PC.
2. Install ServoERP 1.1.478 using the public installer or allow the automatic updater to apply it.
3. Restart ServoERP and sign in.
4. Confirm Site Monitor appears under Operations in the sidebar.

## Validation performed

- Compiled the .NET Framework 4.7.2 Release configuration with MSBuild 17.14: 0 warnings and 0 errors.
- Passed the CI-safe smoke suite, including every license plan, legacy cached entitlement lists, missing licenses, and tampered licenses.
- Verified `HVAC_Pro_Desktop.exe` file and product version `1.1.478.0` in `SOURCE_CODE/bin/Release`.
- Generated the Velopack full package, delta package, portable ZIP, setup executable, release manifests, and standalone update ZIP.
- Built the delta package against the public ServoERP 1.1.477 release.

## Known limitations

- ServoERP must be restarted after the update before the sidebar is rebuilt with the new entitlement policy.
- A missing or tampered license must still be activated or repaired before Site Monitor becomes available.

## Rollback guidance

- Reinstall ServoERP 1.1.477 if application rollback is required.
- No database rollback is required.
