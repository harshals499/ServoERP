# ServoERP 1.1.480 Release Notes

Release date: 04/10/2026

## User-facing summary

ServoERP 1.1.480 automatically connects every safe operational relationship when Relationship Health is opened or refreshed and immediately reflects the result in the report cards. Relationships blocked by existing conflicts remain visible for review and no customer data is silently reassigned.

## Features and fixes

- Automatically retries safe direct and Client/Site relationship enforcement when Relationship Health opens or runs.
- Detects compound Client/Site ownership constraints separately from direct foreign keys.
- Shows Auto-connected, Needs review, Orphan references, Client/site issues, and Protection cards calculated from live SQL Server metadata and business rows.
- Shows Connected, Needs data review, or Pending auto-connect for each relationship.
- Adds automated smoke coverage for connected, missing-protection, needs-review, and protection-percentage calculations.

## Affected modules

- Reports > Data Quality > Relationship Health
- SQL Server relationship migration
- Relationship integrity repository and service
- Visual-smoke tooling

## Database or migration impact

No new tables or columns. The existing guarded relationship migration is rerun on report refresh. Missing constraints are added only when existing rows pass validation. Conflicted rows are preserved and reported.

## Configuration and dependency changes

None. No packages, credentials, endpoints, or third-party services were added.

## Compatibility or breaking changes

None. Existing public tables, columns, classes, and methods remain compatible.

## Security impact

The app more promptly applies available SQL relationship protection after administrators correct legacy data. No authentication, licensing, network, or credential behaviour changed.

## Deployment and update steps

1. Close ServoERP on the target PC.
2. Install ServoERP 1.1.480 using the installer or automatic updater.
3. Start ServoERP and sign in.
4. Open Reports > Data Quality > Relationship Health.
5. Confirm the Auto-connected and Protection cards reflect the database and review any highlighted conflicts.

## Validation performed

- Release compilation with MSBuild 17.14 for .NET Framework 4.7.2 completed with zero errors and zero warnings before packaging.
- CI-safe smoke suite passed, including the new relationship protection-card calculations.
- The guarded connector ran against SQL Server Express without changing conflicted business rows.
- The live test database displayed 49 of 55 relationships protected, six needing review, 13 orphan references, 179 Client/Site issues, and 89.09% protection.
- Relationship Health was rendered at 1537 × 861 and visually inspected with the updated cards and connection statuses.

## Known limitations

- Existing orphan or cross-client rows are intentionally not repaired automatically because their correct business ownership cannot be inferred safely.
- Those conflicts must be reviewed and corrected by an authorised user; the next report refresh will then add any newly safe constraint automatically.
- The installer is not Authenticode-signed because no private code-signing key is configured on the release machine; Windows may display an unknown-publisher warning.

## Rollback guidance

- Reinstall the previous approved ServoERP installer if application rollback is required.
- Additive relationship constraints remain compatible with older application builds.
- Do not remove constraints ad hoc. Any schema rollback must use a reviewed DBA migration during an approved release window.
