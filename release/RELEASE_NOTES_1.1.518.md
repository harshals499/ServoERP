# ServoERP 1.1.518
Release date: 24 October 2026.

## User-facing summary
Business AMC includes every Pro and highest-tier feature. Jobs appears under Operations for Business AMC even when older entitlement lists omitted WorkOrders.

## Features and fixes
Business AMC (Enterprise) uses the complete module catalog instead of an incomplete cached/server module list. The Jobs sidebar entry uses the shared Jobs navigation index. All shipped 1.1.517 findings, handoff and return-visit workflows are preserved.

## Affected modules
License module entitlements; Operations sidebar.

## Database or migration impact
None.

## Configuration and dependency changes
None. No packages added.

## Compatibility or breaking changes
None. Existing prices, plan names, device/user limits, lower-tier module restrictions and license validation endpoints are unchanged.

## Security impact
Business AMC explicitly grants all known catalog modules. Unknown module names remain denied. Existing license status/write restrictions remain in LicenseService; authenticated session checks remain in SessionManager.

## Deployment/update steps
Install the versioned desktop setup or use the existing GitHub/Velopack updater after publication. Retain company configuration and business data. No API, mobile or database deployment is required for this desktop entitlement fix.

## Validation performed
Release rebuild passed and HVAC_Pro_Desktop.exe exists. Focused entitlement regressions passed: all Pro catalog modules enabled with a partial Business AMC cache, unknown modules denied, Basic cache restrictions preserved and missing-license Jobs denied. The built native sidebar was rendered and visually reviewed, confirming Jobs under Operations. Four embedded release-notes/notice regression checks passed. Fifty version tags passed cumulative ancestry verification. Packaging/feed guard and updater results are retained with the release artifacts; publication requires those checks to pass.

## Known limitations
The desktop updater feed and packages will be verified; an in-place upgrade of a customer's installed application will not be performed. No production business database writes are performed.

## Rollback guidance
Retain the previous verified v1.1.517 release and its assets. Reinstall those binaries if required; the automatic updater does not downgrade. Preserve all business data and private configuration. No database rollback is required.
