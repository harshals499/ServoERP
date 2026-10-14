# ServoERP 1.1.503
Release date: 11/10/2026

## User-facing summary
Settings now opens as a grouped directory for Business, Administration, and System & Support.

## Features and fixes
Search settings by name or description. Open related settings together and return through Back to Settings. Frequently used links open company profile, backups, and user management. Existing settings editors and save actions are retained; administrator-only entries remain restricted.

## Affected modules
Settings page only. Application sidebar and shell are unchanged by this release change.

## Database or migration impact
None.

## Configuration and dependency changes
Version metadata updated to 1.1.503. No packages added.

## Compatibility or breaking changes
None. Existing settings values and public methods are retained.

## Security impact
Administrator-only destinations remain hidden for other users. No new outbound services.

## Deployment/update steps
Install the version-matched desktop update package or installer. Publish the complete verified Velopack feed together with its packages when deploying this release.

## Validation performed
Release build succeeded with zero warnings and errors; HVAC_Pro_Desktop.exe exists. Built WinForms verification passed all 13 administrator destinations and 9 staff destinations, search filtering and empty results, Back to Settings, unsaved input retention, and permission checks. Captured and visually reviewed desktop and compact Settings layouts; refined directory links after rendered review. No save, restore, reset, or business-data writes were invoked.

## Known limitations
Customer-PC rollout and cross-version update installation have not been performed. Existing editors keep their existing field layouts.

## Rollback guidance
Reinstall the previous supported desktop release. No database migration requires reversal.
