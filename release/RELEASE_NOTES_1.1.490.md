# ServoERP 1.1.490

Release date: 2026-10-05

## User-facing summary
Prospective buyers can open individual module pages and inspect application screens, including the forms used to prepare quotations, invoices, purchase orders and inventory materials.

## Features and fixes
- Added twelve individual module pages with overview screenshots, relevant entry/detail screens, related modules and demo links.
- Added six original archived captures: quotation creation, invoice creation, purchase-order creation, material entry, supplier-rate management and receivable details.
- Included these captures in the main gallery and clearly labelled earlier application examples.
- Updated homepage workflow links, module directory links and sitemap. Added a module directory index and removed the catch-all rewrite that prevented nested static pages from resolving. Creator section and original screenshots remain intact.

## Affected modules
Website homepage navigation, module directory, twelve module detail pages, screenshot gallery, static assets and sitemap. Desktop assembly version tracking only.

## Database or migration impact
None. No application records were created or changed.

## Configuration and dependency changes
No packages added. Existing hosting and enquiry routes retained; nested static pages now use Cloudflare Pages default routing and the existing 404.html fallback.

## Compatibility or breaking changes
None. Existing directory module anchors remain valid. New module pages use /modules/<module>.html.

## Security impact
Unblurred screenshots follow the owner's established preference for original public application views. The six additional files are original existing application captures, without image generation or pixel editing. Credentials, configuration and unrelated artifacts remain excluded from deployment.

## Deployment and update steps
Deploy release/website-1.1.490 to Cloudflare Pages project royal-moon-64ef, production branch ServoERP. New page/assets use forms-1.1.490 cache keys. Bundle: ServoERP_Website_1.1.490.zip. No installer update is required. DEPLOYMENT_1.1.490.md records the deployment URL.

## Validation performed
Release MSBuild succeeded and SOURCE_CODE/bin/Release/HVAC_Pro_Desktop.exe exists. Four existing website tests passed. Final staged validation checked 682 file and fragment references with no failures. Every added capture was visually inspected; a black invoice-entry capture was rejected. Browser verification covered quotation and invoice pages, mobile invoice layout without horizontal overflow, screenshot preview and Escape close. Public page verification is recorded in the deployment report.

## Known limitations
The Windows capture helper could not initialize, including after reset. The form/detail images therefore come from existing earlier application captures and are labelled accordingly; they do not represent fresh captures of the current version. Modules without an available entry-form capture show relevant existing overview/related workflow screens. Existing static enquiry/email fallback and installer metadata remain unchanged.

## Rollback guidance
Restore the previous production deployment https://e7ff202b.royal-moon-64ef.pages.dev to remove the new module pages and form screenshots. No database rollback is needed.
