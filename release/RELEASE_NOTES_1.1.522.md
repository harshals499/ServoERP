# ServoERP 1.1.522
Release date: 25 October 2026.

## User-facing summary
Settings now includes Mobile Connection for connecting ServoERP Mobile to an existing office server by QR or manual office sign-in.

## Features and fixes
Configure up to four office HTTPS addresses, company number and optional exact private-certificate fingerprint. Show a locally generated QR, copy the connection code, and import/export the public office profile. Reuse existing office user IDs/passwords and open Users & Logins to manage accounts. Connection edits require an administrator; other signed-in users can view the configured profile. Editing clears the old QR. Save Settings saves the connection when this tab is selected. Instructions explain password changes, employee/company permissions, office Wi-Fi/VPN and online office-PC requirements. Availability is explicitly unverified, rather than falsely claiming a working connection.

## Affected modules
Desktop Settings, existing settings repository, mobile office profile interchange.

## Database or migration impact
No schema changes or migrations. Explicit Save connection stores public connection metadata in the existing settings store under MobileConnectionProfileV1. No accounts are automatically created and no business records are modified during validation.

## Configuration and dependency changes
Add QRCoder 1.8.0 (MIT, .NET Framework net40 assembly) for local QR rendering. An administrator supplies the existing office HTTPS service address/company and certificate fingerprint where needed. Publicly trusted certificates may omit a fingerprint. No new third-party service, firewall changes or server installation.

## Compatibility or breaking changes
None to existing desktop APIs or login behavior. QR/profile format is the existing servoerp-office:v1 format. QR requires a compatible mobile app with Connect to desktop / Photograph office QR or profile/code import. Older mobile apps may require an update; manual office sign-in remains governed by their existing capabilities and server requirements. No mobile binary or API is changed by this desktop release.

## Security impact
The QR and exported profile contain only office addresses, company number and certificate fingerprint; never usernames, passwords, access tokens, installation keys or SQL connection strings. HTTPS origin validation rejects credentials, URL paths, query strings and fragments. Malformed or credential-bearing imported profiles are rejected. Login remains required and existing office account/company/employee/role restrictions remain authoritative. No certificate validation bypass or automatic credential provisioning.

## Deployment/update steps
Use the existing desktop installer/updater with the verified release package. Preserve configuration and data. An office administrator opens Settings > Mobile Connection, imports the public office profile or enters verified connection details, reviews the QR, and saves the connection. Mobile users review the QR/profile then sign in with their own office accounts; alternatively enter the office details manually. Deploying the office API, its additive schema, certificate and account/employee linking remains a separate reviewed prerequisite. Installing this release requires the normal desktop update/restart; no running desktop is modified during release validation.

## Validation performed
Release build and executable existence; strict profile round trip and normalization; fifteen malformed/insecure/credential-bearing profile cases rejected; non-admin save denied before database access; administrator and standard-user UI actions checked; edited fields invalidate QR; invalid HTTPS cannot generate QR. Native WinForms wide and narrow screenshots captured and visually reviewed. Independent ZXing decoded both QR images and both rendered wide screenshots to the expected code. The unchanged mobile OfficeConnectionProfile parser accepted the generated code and JSON. Tests use synthetic .invalid office details and no production SQL operations. Artifact/notes verification results are retained with the candidate artifacts.

## Known limitations
No physical-phone camera/sign-in or live office HTTPS reachability test. No server provisioning or migration execution. Manual/QR login succeeds only when the office API is already configured, reachable and compatible, and the user's existing account has the required access. No guaranteed continuous office-PC hosting.

## Rollback guidance
Retain the verified public 1.1.521 installer/package and restore those binaries if needed, preserving all configuration and business data. The older desktop ignores the added public settings key. No database rollback is required. Never automatically roll back customer data. Updater does not automatically downgrade.
