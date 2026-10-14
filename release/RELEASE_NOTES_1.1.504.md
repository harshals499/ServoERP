# ServoERP 1.1.504
Release date: 11/10/2026

## User-facing summary
Settings has a wider, better-aligned directory with readable descriptions and consistent sections.

## Features and fixes
Prevent shared layout rules from clipping or shifting settings descriptions. Use a single clickable row with icon, title, wrapped description, hover and keyboard focus. Adapt directory width to available space, align section heights and wrap quick links on narrow screens. Disable dashboard resize grips on fixed directory sections. Existing navigation, search, editors and permissions are retained.

## Affected modules
Settings page only. Application sidebar and shell are unchanged by this release change.

## Database or migration impact
None.

## Configuration and dependency changes
Version metadata updated to 1.1.504. No packages added.

## Compatibility or breaking changes
None. Existing settings values and public methods are retained.

## Security impact
Administrator-only destinations remain hidden for other users. No new outbound services.

## Deployment/update steps
Install the version-matched desktop update package or installer. Publish the complete verified Velopack feed together with its packages when deploying this release.

## Validation performed
Release build passed with zero warnings and errors; HVAC_Pro_Desktop.exe exists. WinForms verification passed all 13 administrator and 9 staff destinations, search, empty results, Back to Settings and unsaved input retention. Full-row bounds, description wrapping and absence of directory resize grips passed after shared styling at 1920, 1440, 1280, 1024, 820 and 640 pixels for both roles. Desktop and laptop captures were visually reviewed. No save, restore, reset or business-data writes were invoked.

## Known limitations
Customer-PC rollout and cross-version update installation have not been performed. Existing editors keep their existing field layouts.

## Rollback guidance
Reinstall the previous supported desktop release. No database migration requires reversal.
