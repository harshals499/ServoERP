# ServoERP 1.1.492

Release date: 2026-10-05

## User-facing summary
A richer navy and teal website with layered backgrounds, framed product screenshots and useful interactive exploration replaces the plain white presentation.

## Features and fixes
Restyled homepage, founder section, demo form, module directory, individual module pages and document galleries. Added a five-step workflow explorer that switches real screenshots, explanatory text and relevant module links. Added an office-PC slider that highlights the appropriate existing annual plan. Added hover feedback, restrained scroll reveals and reading progress. Preserved screenshot zoom, PDF privacy edits and founder portrait/story.

## Affected modules
Marketing website styles and markup; new creative.css and creative.js; TOOLS/BuildCreativeWebsite.cjs and existing builder integration. Desktop assembly version tracking only.

## Database or migration impact
None. No business data writes or schema changes.

## Configuration and dependency changes
None. No packages, new services or outbound integrations added. New static CSS/JS use versioned cache references.

## Compatibility or breaking changes
None. Existing prices, license plan names, PC limits, enquiry routes, original images and public installer metadata remain unchanged. The slider compares published plan limits; it does not change pricing logic.

## Security impact
None. No new data collection. Existing company blur remains in the PDF marketing images. Private source documents, configuration and unrelated artifacts are excluded from staging.

## Deployment and update steps
Deploy release/website-1.1.492 to Cloudflare Pages project royal-moon-64ef, production branch ServoERP. Bundle: ServoERP_Website_1.1.492.zip. No desktop installer update required. See DEPLOYMENT_1.1.492.md.

## Validation performed
Release MSBuild succeeded; SOURCE_CODE/bin/Release/HVAC_Pro_Desktop.exe exists. Four existing website tests passed; 760 file/fragment references checked without failures. JavaScript syntax check passed. Browser visual checks covered desktop hero, workflow explorer, pricing and enquiry form; mobile hero and workflow explorer fit a 390px viewport without horizontal overflow. Clicking and arrow-key navigation changed the displayed workflow. Slider values 3, 4 and 15 matched Starter, Growth and Business plan displays respectively. Final public verification is recorded in deployment notes.

## Known limitations
Existing static enquiry route/email fallback is retained; automated email delivery on production was not tested. Reveal and tilt effects are decorative and disabled for reduced-motion users. Website examples remain illustrative product views; this release does not change desktop behavior.

## Rollback guidance
Restore production deployment https://cd268f21.royal-moon-64ef.pages.dev. No database rollback required.
