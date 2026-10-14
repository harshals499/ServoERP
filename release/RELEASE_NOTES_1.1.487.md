# ServoERP 1.1.487

Release date: 2026-10-05

## User-facing summary
The website showcases twenty privacy-redacted application views and introduces Harshal Sonawane with his supplied founder story and original portrait.

## Features and fixes
- Replaced screenshot references across the homepage, module pages and gallery with the twenty reviewed redacted views.
- Added the responsive About the Creator section, philosophy quote and four vision statements; removed illustrative testimonials.
- Fixed tablet header overflow after adding Creator navigation.

## Affected modules
Marketing website homepage, module showcase, form gallery, navigation and static assets. Desktop assembly version tracking only.

## Database or migration impact
None. No business-data population or modification was performed.

## Configuration and dependency changes
None. Existing Cloudflare Pages project and production branch retained. Existing download/update metadata retained.

## Compatibility or breaking changes
No desktop public API changes. Unused legacy screenshot assets are excluded from the new deployment and their old URLs may return 404.

## Security impact
Reviewed redacted derivatives conceal confidential names, contacts, identifiers and financial details. Private original captures, configuration files, development dependencies and unused legacy screenshots are excluded from the deployment. The supplied founder photo is byte-identical to the original.

## Deployment and update steps
Deploy the allowlisted release/website-1.1.487 directory to Cloudflare Pages project royal-moon-64ef, production branch ServoERP. Website assets use new cache keys. No desktop installer update is required for this website release.

## Validation performed
Release MSBuild succeeded and SOURCE_CODE/bin/Release/HVAC_Pro_Desktop.exe exists. Four existing website demo-request tests passed. Browser checks covered desktop, 390px mobile and 820px tablet founder layouts, mobile navigation, twenty loaded gallery images and lightbox open/Escape close. Tablet horizontal overflow was corrected. Deployment details and final live verification are recorded in DEPLOYMENT_1.1.487.md.

## Known limitations
The redacted app views are AI-edited marketing derivatives and can differ from source pixels. Some captured application views contain empty states or configuration notices. Public desktop download/update metadata remains at the existing installer version 1.1.486. No desktop functional changes or new installer package are included.

## Rollback guidance
Restore the preceding Cloudflare Pages production deployment 3d32f267-d785-45f5-91eb-1416106cda50 (https://3d32f267.royal-moon-64ef.pages.dev). No database rollback is required. Restoring the previous website also restores its previous screenshot content.
