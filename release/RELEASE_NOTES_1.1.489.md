# ServoERP 1.1.489

Release date: 2026-10-05

## User-facing summary
The website now helps prospective buyers understand what ServoERP is, how it fits service operations and how to evaluate it through a demo.

## Features and fixes
- Rebuilt the homepage around a clear product introduction, business questions, enquiry-to-payment workflow and four product groups.
- Added owner, service-office and accounts/purchasing outcomes, installation guidance and six buyer FAQs.
- Preserved creator content, the original portrait, original application screenshots, plan names, prices and device limits.
- Added responsive sales presentation, clear demo links, direct email contact and an explanation of the existing email fallback.
- Fixed secondary-page screenshot links; added lazy image loading, accessible lightbox semantics/focus containment, skip link, visible focus and reduced-motion support.
- Added canonical/social metadata, robots.txt and sitemap.xml. Public marketing pages can be indexed; screenshot asset indexing is discouraged.

## Affected modules
Marketing homepage, module page and form gallery presentation, shared screenshot preview, website discovery files and static deployment. VERSION and desktop assembly version tracking only.

## Database or migration impact
None. No business-data writes.

## Configuration and dependency changes
No new dependencies, services or trackers. Existing Cloudflare Pages project/branch and demo endpoint retained. Marketing-page noindex header removed; screenshot assets retain noindex/noimageindex. Download/update metadata is unchanged.

## Compatibility or breaking changes
The homepage replaces the long individual-module showcase with four workflow groups. Module details and twenty-screen gallery remain available. Some previous homepage module fragments no longer identify individual showcases; secondary-page links now open their corresponding full screenshots. No desktop public API changes.

## Security impact
Original screenshots remain public under the owner's previous explicit authorization. No credentials, configuration, private-review directories or development dependencies are deployed. robots.txt and image headers discourage screenshot indexing but do not restrict public access.

## Deployment and update steps
Deploy the allowlisted release/website-1.1.489 directory to project royal-moon-64ef, branch ServoERP. Website asset cache keys use sales-1.1.489. No installer update is required. Packaged bundle: ServoERP_Website_1.1.489.zip. See DEPLOYMENT_1.1.489.md for deployment details.

## Validation performed
Release MSBuild succeeded and SOURCE_CODE/bin/Release/HVAC_Pro_Desktop.exe exists. Four existing demo-service tests passed. JavaScript syntax check passed. Static validation checked 159 file/fragment references with zero failures in source and staged bundle. Browser checks covered desktop, 390px mobile and 820px tablet layouts; no horizontal overflow at checked widths; mobile navigation; FAQ expansion; screenshot dialog, Tab focus containment and Escape close; and enquiry layout. No real enquiry email was sent.

## Known limitations
The static site does not add a new enquiry backend. Its existing form uses /api/demo-request and prepares a mailto fallback when that endpoint is unavailable; a configured email client and sending the prepared message are required for that route. Email delivery was not tested with a real message. Original screenshots can contain business information and empty/configuration states. Public installer metadata remains 1.1.486. Search indexing and sales conversion are not guaranteed.

## Rollback guidance
Restore the preceding production deployment https://ae2fdad7.royal-moon-64ef.pages.dev to return to the previous website with original screenshots and creator section. No database rollback is required.
