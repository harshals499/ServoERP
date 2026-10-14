# ServoERP 1.1.483.0 Release Notes

Release date: 4 October 2026

## User-facing summary

The filter row in **Site Monitor > All Sites by Region - Live Work** is restored. Users can again search sites and narrow the table by region or current work condition, see the live result count, and clear all filters in one action.

## Features and fixes

- Restored free-text Search across region, site, client, current work, and technician.
- Restored the Region selector, including an explicit **All regions** default even when the database contains no sites.
- Restored Work shortcuts for **All work**, **Active work**, **No active work**, **Critical / SLA risk**, and **Unassigned technician**.
- Restored the live site count and **Clear** action.
- Preserved the selected region when Site Monitor data refreshes.
- Kept filter controls separate from the table's site-detail drill-down behavior.
- Added regression coverage for combined filter behavior and reset behavior.

## Affected modules

- Site Monitor
- All Sites by Region - Live Work
- Automated CI smoke tests

## Database or migration impact

None. This release does not add, rename, or remove tables, columns, indexes, or business data.

## Configuration and dependency changes

None. No packages or external services were added.

## Compatibility or breaking changes

None. Existing public interfaces and stored data remain compatible.

## Security impact

None identified. License activation requirements remain unchanged.

## Deployment and update steps

1. Close ServoERP if it is running.
2. Install ServoERP 1.1.483.0 using the published installer, or accept the in-app update when offered.
3. Start ServoERP and sign in with an activated license.
4. Open **Site Monitor** and verify the Search, Region, Work, count, and Clear controls above **All Sites by Region - Live Work**.

## Validation performed

- Release rebuild succeeded with 0 warnings and 0 errors.
- `HVAC_Pro_Desktop.exe` exists in `SOURCE_CODE/bin/Release`.
- CI smoke coverage passed for search, region, every work-state shortcut, live count, and Clear behavior.
- The built application was opened at Site Monitor and visually checked at 3456 x 1408; the complete filter bar rendered in the correct card and did not overlap the grid.
- Public packaging completed successfully across all release stages.
- A fresh smoke run against the final release binary passed, including all 100 registered cross-module relationships and the Site Monitor region-filter regression.
- The production update feed reports 1.1.483.0, the public installer returned HTTP 200 with the expected 61,347,733-byte length, and the GitHub release is public rather than draft or prerelease.

## Known limitations

- Site Monitor requires an activated ServoERP license.
- The Windows installer is not code-signed, so Windows may show a publisher warning.

## Rollback guidance

If rollback is required, close ServoERP and reinstall version 1.1.482.0. No database rollback is necessary because this release contains no schema or data migration.

## Publication verification

- GitHub release: <https://github.com/harshals499/ServoERP/releases/tag/v1.1.483>
- Public installer: <https://downloads.servoerp.in/ServoERP_Setup_1.1.483.0.exe>
- Production update feed: <https://servoerp.in/latest.json>
- Installer SHA-256: `1B6958E9C251295868CFDD3CF1F629CEF279706AB8FA429842777DA919124C4F`
- Update ZIP SHA-256: `E68344BD95E18827913D5E930AEC5628A2DB9D7BDC4C328B4F66D24506AE5F88`
- Full update package SHA-256: `2CF3E71F8DD0B6939284E7B7FB6D7762B2FA1DC0E29520DF9A806A3FB4F1684B`
- Delta update package SHA-256: `1FAE9C0AFB34946875CD615A3E758A335587975BC2E6DA4FA31B9A6CAB9D0DE7`
- Release report: `artifacts/release/1.1.483.0/release-report.json` — passed.
