# ServoERP 1.1.525

Release date: 25 October 2026.

## User-facing summary
Mobile Connection provides separate QR codes to download the Android app and connect it to the office.

## Features and fixes
Download Mobile App opens the verified official mobile 1.1.506 APK. Sync App retains the existing office profile and sign-in workflow. Download remains available before office setup and when office fields change. Instructions distinguish installation from connection and authentication.

## Affected modules
Desktop Settings > Mobile Connection; native mobile connection regression checks.

## Database or migration impact
None.

## Configuration and dependency changes
None. Existing QRCoder and official GitHub release hosting are reused.

## Compatibility or breaking changes
None. Existing office profile format and user permissions are retained.

## Security impact
No credentials or access tokens are included in either QR. Sync continues to require office sign-in. QR generation stays local.

## Deployment/update steps
Install through the existing desktop updater or the versioned GitHub installer. No Office API, mobile or schema update is required. Configure or import your existing office profile in Settings > Mobile Connection if it is not already saved. The requested local installation of 1.1.525 has passed version/hash verification and the actual installed screen displays both QR codes.

## Validation performed
Public stable mobile feed checked and advertised APK returned HTTP 200. Release MSBuild passed with no warnings or errors and HVAC_Pro_Desktop.exe exists. Native administrator/user checks passed with synthetic profiles and no SQL access, including malformed-profile rejection, role permissions, independent download QR, sync QR invalidation and wide/narrow renders. Both QR images for both roles decoded to their exact expected payloads using cached ZXing in a separate test harness. Native wide and narrow download/sync screenshots were visually inspected. Installed executable version and SHA-256 matched the tested build. Both QR codes were visually verified side by side in the installed app after saving the existing office profile. Both existing office HTTPS addresses passed exact-certificate-pin and SQL readiness checks. No business records were changed.

## Known limitations
Download QR targets mobile 1.1.506, the verified stable version at build time. Android requires installation confirmation. Physical phone scanning and employee sign-in have not been tested for this desktop change. The installer is unsigned, as with this local packaging configuration. Publication was explicitly requested by Harshal after local acceptance.

## Rollback guidance
Keep desktop 1.1.524 and restore its binaries if required. Preserve configuration, SQL data and mobile storage. No data rollback is needed.
