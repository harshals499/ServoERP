# ServoERP 1.1.491

Release date: 2026-10-05

## User-facing summary
Added generated-document preview images alongside the application forms so prospective buyers can see quotations, tax invoices, purchase orders, job cards, reports and salary slips.

## Features and fixes
Six PDF layout examples appear in the main gallery; all twelve module pages show relevant document examples. Images expand in the existing accessible preview dialog. Added a homepage link to the document gallery and responsive portrait cards. Company letterheads, repeated names and registration/contact details are blurred in the marketing images where Madhusuman Enterprises appears.

## Affected modules
Marketing homepage, twelve module pages, gallery, preview script, styles and static asset indexing rules. Desktop assembly version tracking only. Added TOOLS/GenerateWebsitePdfExamples.cs and TOOLS/BuildPdfPreviewPages.cjs for reproduction.

## Database or migration impact
None. Five examples were generated from in-memory illustrative records through existing document builders; no business records were saved. The salary-slip image uses an existing blank, zero-value archived PDF.

## Configuration and dependency changes
None. No packages added. Existing Cloudflare Pages hosting retained. PDF preview assets carry noindex/noimageindex directives.

## Compatibility or breaking changes
None. Original app screenshots, creator section, pricing, public installer metadata and actual application PDF headers remain unchanged.

## Security impact
Only privacy-edited images are deployed. Original PDFs and unedited renders stay outside the deployment folder. Built-in image generation performed localized blur; rejected intermediate variants are excluded. Original company contact and government identifiers in these PDF previews are obscured.

## Deployment and update steps
Deploy release/website-1.1.491 to Cloudflare Pages project royal-moon-64ef, production branch ServoERP. Bundle: ServoERP_Website_1.1.491.zip. No installer update required. See DEPLOYMENT_1.1.491.md for the deployed URL.

## Validation performed
Release MSBuild succeeded; SOURCE_CODE/bin/Release/HVAC_Pro_Desktop.exe exists. Four existing website tests passed. Website validation checked 723 local file/fragment references with zero failures. Generated PDFs and selected edited images were visually inspected. Browser and public deployment validation are recorded in the deployment report.

## Known limitations
These are illustrative PDF layout previews, with localized image edits for privacy, rather than downloadable original PDFs. The salary-slip photo shows page 1 of a two-page archived blank example. The purchase-order sample applies a print-only flex-width correction in the example harness; this does not change the desktop application. Existing enquiry/email fallback remains unchanged. Current report generator includes company registration fields; those are blurred in the selected preview.

## Rollback guidance
Restore previous production deployment https://267c02a3.royal-moon-64ef.pages.dev. No database rollback needed.
