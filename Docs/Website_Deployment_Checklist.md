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

Preferred automation path:

- `SOURCE_CODE\Installer\Publish-ServoERPR2Installer.ps1` for the installer upload
- `SOURCE_CODE\Installer\Publish-ServoERPCloudflare.ps1` for installer upload + Pages deploy
- Large installers can now publish without static R2 access keys by using the built-in Cloudflare Worker multipart fallback.
- Release verification is built into `Publish-ServoERPCloudflare.ps1`.

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
4. Set `CLOUDFLARE_API_TOKEN`, `R2_ACCOUNT_ID`, and optionally `R2_ACCESS_KEY_ID` + `R2_SECRET_ACCESS_KEY`.
5. Run `SOURCE_CODE\Installer\Publish-ServoERPCloudflare.ps1 -Version <x.x.x.x>`.
6. Verify `https://servoerp.in/version.txt`, `/latest.json`, `/download/`, the update ZIP URL, and the installer EXE URL under `https://downloads.servoerp.in/`.
