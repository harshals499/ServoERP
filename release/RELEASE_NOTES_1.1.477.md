# ServoERP 1.1.477 Release Notes

Release date: 04/10/2026

## User-facing summary

ServoERP 1.1.477 fixes Site Monitor visibility across licensed PCs. A client PC now recognizes Site Monitor entitlements issued under the current public name, the previous Dispatch Center name, or the internal GeoIntelligence key.

## Features and fixes

- Fixed Site Monitor being omitted from the sidebar when a valid license uses `Site Monitor`, `SiteMonitor`, `Dispatch Center`, or `DispatchCenter` as its module key.
- Preserved the existing canonical `GeoIntelligence` entitlement.
- Preserved plan enforcement; a license with no matching Site Monitor entitlement still does not receive access.
- Added regression tests covering every supported entitlement alias, explicit denial, and Starter AMC defaults.

## Affected modules

- Main sidebar module visibility
- Licensing entitlement compatibility
- Site Monitor
- CI-safe UI policy smoke tests

## Database or migration impact

None. No tables, columns, indexes, business records, or migrations are added, changed, deleted, or renamed.

## Configuration and dependency changes

None. No packages, external services, or configuration keys were added or changed.

## Compatibility or breaking changes

No breaking changes. Existing `GeoIntelligence` entitlements remain valid. The release adds compatibility for the public Site Monitor name and the earlier Dispatch Center name.

## Security impact

License enforcement remains active. Alias matching only treats equivalent names as the same Site Monitor entitlement and does not grant access when none of those names is present.

## Deployment and update steps

1. Close ServoERP on the target PC.
2. Install ServoERP 1.1.477 using the public installer or allow the automatic updater to apply it.
3. Start ServoERP and sign in.
4. If Site Monitor is licensed, confirm it appears under Operations in the sidebar and opens successfully.
5. If it is still absent, open Settings and refresh the license, then confirm the subscription includes Site Monitor.

## Validation performed

- Release rebuild with MSBuild 17.14 for .NET Framework 4.7.2 completed with zero warnings and zero errors.
- CI-safe smoke suite passed, including Site Monitor entitlement aliases, explicit denial, and Starter AMC regression coverage.
- Verified `SOURCE_CODE/bin/Release/HVAC_Pro_Desktop.exe` exists with file and product version 1.1.477.0.
- Generated the Velopack full and delta packages, portable archive, setup executable, release manifests, and standalone update ZIP.
- Verified package generation used the public 1.1.476 full package as the delta base.

## Known limitations

- Site Monitor remains unavailable when the active subscription does not include a Site Monitor/GeoIntelligence entitlement.
- An already-running ServoERP process must restart after the update before the compatibility logic becomes active.

## Rollback guidance

- Reinstall ServoERP 1.1.476 if application rollback is required.
- No database rollback is required.
