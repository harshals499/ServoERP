# ServoERP 1.1.482 Release Notes

Release date: 2026-10-04

## User-facing summary

Site Monitor is restored in the Operations sidebar for every activated ServoERP license. The fix also covers client PCs whose protected license cache was created before Site Monitor became a core workspace.

## Features and fixes

- Restored the established Site Monitor entitlement for Trial, Starter AMC, Growth AMC, Business AMC, and legacy activated licenses.
- Added upgrade compatibility when an older cached module list does not contain `GeoIntelligence`.
- Added regression coverage for every license plan and activated license state.
- Missing and tampered licenses remain blocked from Site Monitor.

## Affected modules

- Main navigation / Operations sidebar
- Site Monitor (`GeoIntelligence`)
- Subscription entitlement policy
- CI UI policy tests

## Database or migration impact

None. No tables, columns, rows, relationships, or migrations are changed by this release.

## Configuration and dependency changes

None. No new package or third-party dependency was added. Existing license endpoints, machine fingerprint logic, plan names, and prices are unchanged.

## Compatibility or breaking changes

None. This restores behavior previously released in ServoERP 1.1.478 and is compatible with older protected license snapshots.

## Security impact

Activated licenses receive Site Monitor regardless of the age of their cached module list. Missing or tampered license states remain denied. License validation endpoints and machine identity checks are unchanged.

## Deployment and update steps

1. Close ServoERP on the target PC.
2. Install ServoERP 1.1.482 through the automatic updater or the public installer.
3. Start ServoERP and sign in.
4. Confirm **Operations > Site Monitor** is visible, then open it once to verify the live site dashboard.

## Validation performed

- .NET Framework 4.7.2 Release rebuild: passed with 0 warnings and 0 errors.
- CI smoke suite: passed, including the all-plan/all-state Site Monitor entitlement policy and all 100 relationship checks.
- Rendered `MainForm` visual smoke: passed with a legacy activated snapshot containing only `Dashboard`; Site Monitor remained visible and enabled.
- Rendered Site Monitor navigation smoke: passed; navigation opened `GeoIntelligenceForm` with live site data.
- Velopack full, delta, portable, and setup packages: built successfully.
- GitHub release: published and verified as a non-draft public release.
- Production update metadata: verified at `https://servoerp.in/latest.json` with version `1.1.482.0`.
- Public installer: HTTP 200 verified with the expected 61,340,606-byte content length.
- Installer SHA-256: `69CC8CA51421B9449678B5596EF92D62592FCDC2143E7DAB9476C1863E512E3A`.
- Update ZIP SHA-256: `1F442CCE573DE4EF39A2F604FD3E758A36CDE4498F57F6995153A86AAC32AD68`.

## Known limitations

- An installation with a missing or tampered license must still be activated or repaired before Site Monitor is available.
- The standard Windows installer is not code-signed in this release environment.

## Rollback guidance

Reinstall ServoERP 1.1.481 only if rollback is unavoidable. Rollback does not change business data, but it reintroduces the Site Monitor visibility regression for older or lower-tier activated license snapshots. Back up the SQL Server `HVAC_PRO` database before any application rollback as normal operational practice.
