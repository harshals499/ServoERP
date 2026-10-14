# ServoERP 1.1.493

Release date: 2026-10-05

## User-facing summary
Website PDF examples now show Your Company Header. The gallery heading and creator vision cards are readable against the dark website design. Creator contact harshals499@gmail.com is linked in the founder section.

## Features and fixes
Replaced company letterheads, repeated company names and identifying registration/contact details in six PDF preview images with clean generic placeholders. Updated gallery and module captions to describe the generic branding. Fixed the gallery hero's leftover white background and four creator principle cards' leftover white surfaces. Added a creator-contact mailto link.

## Affected modules
Website homepage/founder section, form gallery, twelve PDF module sections, creative styles/cache references, six preview assets and website builder tools. Added TOOLS/BuildWebsitePdfPlaceholders.py. Desktop assembly version tracking only.

## Database or migration impact
None. No business records or schema changed.

## Configuration and dependency changes
No desktop or website packages added. PyMuPDF was installed as a local artifact-generation tool under tmp/pdf-tooling; it is excluded from deployment and desktop packaging. Existing pdfplumber is used for locating source text.

## Compatibility or breaking changes
None. Actual application PDF generators, company settings, original source PDFs, founder portrait/story, pricing and installer metadata remain unchanged. Only website document previews use the placeholders.

## Security impact
Only flattened PNG previews are published. Intermediate PDF overlays retain source text and stay private; they must never be uploaded. Original company letterhead, registration identifiers and footer contact details do not appear in the published document images. The creator's supplied email is intentionally public.

## Deployment and update steps
Deploy release/website-1.1.493 to Cloudflare Pages project royal-moon-64ef, production branch ServoERP. New CSS/JS and PDF images use 1.1.493 cache keys. Bundle: ServoERP_Website_1.1.493.zip. No installer update required.

## Validation performed
Release MSBuild succeeded; SOURCE_CODE/bin/Release/HVAC_Pro_Desktop.exe exists. Four existing website tests passed; 760 file/fragment references passed. All six final PDF PNGs were visually inspected. Browser verification and deployment URL are recorded in DEPLOYMENT_1.1.493.md.

## Known limitations
Website document examples are illustrative layouts. The salary-slip preview remains page 1 of an archived blank, zero-value example. Existing enquiry route/email fallback remains unchanged; no production emails sent. Private overlay PDFs retain hidden original content and are excluded from the deployment allowlist.

## Rollback guidance
Restore previous production deployment https://4611bd93.royal-moon-64ef.pages.dev. This restores earlier blurred previews and contrast issues. No database rollback needed.
