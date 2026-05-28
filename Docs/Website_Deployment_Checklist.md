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
- `/version.txt`
- `/changelog.json`
- `/updates/ServoERP_Update_1.0.25.0.zip`

Upload the Windows setup EXE to the `servoerp-downloads` R2 bucket:

- `ServoERP.Setup.1.0.25.0.exe`

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
- `https://downloads.servoerp.in/ServoERP.Setup.1.0.25.0.exe`
- `https://servoerp.in/version.txt`
- `https://servoerp.in/changelog.json`

Expected results:

- Home page loads the ServoERP sales website.
- Download page redirects to the installer.
- Installer starts downloading from R2.
- `version.txt` shows `1.0.25.0`.
- `changelog.json` returns valid JSON.

## Sales Funnel Checks

- Header `Download Trial` downloads the installer.
- Pricing plan CTAs open an email to `support@servoerp.in`.
- Support form opens a prepared email with company/contact details.

## Release Notes

Current public pricing:

- Trial: free for 14 days.
- Starter AMC: ₹10,000/year, up to 3 PCs.
- Growth AMC: ₹18,000/year, up to 7 PCs.
- Business AMC: ₹30,000/year, up to 15 PCs.

Before uploading a new release:

1. Build ServoERP Release.
2. Run smoke tests.
3. Rebuild installer only after the app is stable.
4. Copy the new setup EXE into `marketing_site/download/`.
5. Update website download URL if the file name changes.
6. Update `version.txt`.
7. Update `changelog.json`.
8. Commit the website/app changes in Git.
