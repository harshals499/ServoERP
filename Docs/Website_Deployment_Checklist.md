# ServoERP Website Deployment Checklist

Use this when publishing `C:\HVAC_PRO_MSE\marketing_site` to `servoerp.in`.

## Files to Upload

Upload the full contents of:

`C:\HVAC_PRO_MSE\marketing_site`

Required paths after upload:

- `/index.html`
- `/styles.css`
- `/script.js`
- `/assets/servoerp-logo.png`
- `/assets/field-service-van.png`
- `/download/index.html`
- `/download/ServoERP.Setup.1.0.20.0.exe`
- `/version.txt`
- `/changelog.json`
- `/updates/ServoERP_Update_1.0.20.0/`

## Hosting Requirements

- Keep the folder structure exactly the same.
- Allow `.exe` downloads from `/download/`.
- Serve `.json`, `.txt`, `.css`, `.js`, `.png`, and `.exe` files normally.
- Keep `/version.txt`, `/changelog.json`, and `/download/*` uncached or short-cached.
- Do not upload private license authority files.
- Do not upload `C:\ServoERP-LicenseAuthority`.

## Post-Upload Checks

Open these URLs in a browser:

- `https://servoerp.in/`
- `https://servoerp.in/download/`
- `https://servoerp.in/download/ServoERP.Setup.1.0.20.0.exe`
- `https://servoerp.in/version.txt`
- `https://servoerp.in/changelog.json`

Expected results:

- Home page loads the ServoERP sales website.
- Download page redirects to the installer.
- Installer starts downloading.
- `version.txt` shows `1.0.20.0`.
- `changelog.json` returns valid JSON.

## Sales Funnel Checks

- Header `Download Trial` downloads the installer.
- Pricing `Start Standard` opens an email to `harshalsonawane@servoerp.com`.
- Pricing `Talk to Sales` opens an email to `harshalsonawane@servoerp.com`.
- Support form opens a prepared email with company/contact details.

## Release Notes

Current public pricing:

- Trial: free for 14 days.
- Standard: Rs. 14,999 launch, Rs. 24,999 renewal.
- Enterprise: Rs. 49,999 launch, Rs. 74,999 renewal.

Before uploading a new release:

1. Build ServoERP Release.
2. Run smoke tests.
3. Rebuild installer only after the app is stable.
4. Copy the new setup EXE into `marketing_site/download/`.
5. Update website download URL if the file name changes.
6. Update `version.txt`.
7. Update `changelog.json`.
8. Commit the website/app changes in Git.
