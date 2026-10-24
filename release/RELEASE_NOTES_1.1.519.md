# ServoERP 1.1.519
Release date: 24 October 2026.

## User-facing summary
Jobs has its own sidebar section immediately below Dashboard. Open Jobs and choose Add New Job to create a job.

## Features and fixes
Move Jobs out of Operations into a dedicated Jobs section. Reuse the existing job dashboard, creation form, validation and save workflow. Restore WorkOrders access already included in paid plans when older entitlement caches omit it. Preserve Business AMC full-catalog access from 1.1.518.

## Affected modules
Main sidebar; Jobs navigation; license module compatibility; regression scripts.

## Database or migration impact
None.

## Configuration and dependency changes
None. No packages added.

## Compatibility or breaking changes
None. Existing public APIs, pricing, plan names, trial defaults and license endpoints remain unchanged.

## Security impact
Jobs remains restricted to authenticated users. The paid-plan compatibility rule rejects missing/tampered licenses; existing frozen/expired business-write gates remain enforced by LicenseService.

## Deployment/update steps
Next-release candidate. Install the versioned setup or existing updater after publication. Retain private configuration and business data.

## Validation performed
Release rebuild passed and HVAC_Pro_Desktop.exe exists. A regression reproduced missing Jobs on Basic with an incomplete cache before the fix and passed afterwards for Basic, Standard, Pro and Enterprise. Missing/tampered and default-trial restrictions passed; Business AMC full-feature regressions passed. Eight built-sidebar previews (four paid plans at 720/900 heights) passed section/visibility checks. Screenshots were inspected, including the dedicated Jobs section and the actual Jobs dashboard. The real BeginNewJobAsync workflow opened an editable draft in an identified disposable ServoERP_JobsSidebar_519_* SQL database; Jobs row count remained zero. No production business database was used. Packaging/feed and embedded-notes guard results are retained with the candidate artifacts.

## Known limitations
No real business job will be saved during validation. Publication remains subject to the existing rolling 24-hour release limit after 1.1.518. Customer installation upgrade is not performed.

## Rollback guidance
Retain the previous verified v1.1.518 release. Restore its binaries while preserving private configuration and all business data. The updater does not downgrade automatically. No database rollback is required.
