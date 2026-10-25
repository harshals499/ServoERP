# Mobile Connection settings

Open Settings > Mobile Connection (also available from the General settings directory). This is setup for the existing office HTTPS API, not an API installer or a public tunnel.

An office administrator imports a public `office.servo-office.json` profile or enters one to four HTTPS origins, the company number and certificate SHA-256 fingerprint from the verified deployment. Do not enter database credentials, passwords, installation API keys or private certificate files. For a publicly trusted certificate only, the fingerprint may be blank. Show QR previews without saving; Save connection (or Save Settings while this tab is selected) persists the public profile through the existing settings service. Import is a review step and does not save automatically.

The QR, copied code and exported profile use the existing `servoerp-office:v1:` base64url JSON wire format. A compatible ServoERP Mobile client can use Connect to desktop > Photograph office QR, import the profile or paste the code. Review the address/company/certificate on the phone, then sign in with the office-created user ID and password. Manual office sign-in requires the same connection details first. QR is not an authentication token or an account invitation. Older mobile builds without profile/QR support need a compatible update or their manual sign-in flow.

Manage office users opens the existing Users & Logins tab for administrators. Existing API requirements still apply: active office account, company membership, appropriate permissions and linked employee for assigned field work. Complete a required password change in the desktop app first. This change creates no accounts, changes no passwords and deploys no schema.

Standard signed-in users may view and copy/export the configured public profile; they cannot edit/import/save connection settings or manage accounts from this surface. The service independently rejects non-administrator saves.

The office computer/service must be reachable using office Wi-Fi or the existing office VPN. Saving a profile does not verify availability, install certificates, open ports, start an API or make the service continuously available. Confirm the existing office service's deployment separately before testing real sign-in. No private data is sent to an external QR service.

Validation evidence is retained under `artifacts/mobile-connection` in the isolated candidate checkout: native administrator/standard-user screenshots, strict profile tests, independent QR decode and existing mobile-parser interoperability. All fixtures use synthetic `.invalid` addresses. No live database writes, phone authentication, API deployment or physical camera validation were performed.

QR library documentation: https://www.nuget.org/packages/QRCoder (MIT; package license is distributed with the desktop application).
