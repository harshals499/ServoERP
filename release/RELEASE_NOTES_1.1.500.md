# ServoERP 1.1.500
Release date: 10/10/2026

## User-facing summary
Settings now starts with a Company Header card. Upload a wide company letterhead image to apply it to generated business PDFs and print documents.

## Features and fixes
- Upload and preview PNG, JPG or BMP headers up to 10 MB; replacement applies to future documents.
- Uploaded letterheads take priority in generic company mode as well as existing document branding.
- Added header support to direct PDF output, supplier reports, inventory reports, client summaries and job details.

## Affected modules
Settings, shared document branding, invoice/job/report PDF generation, supplier performance, inventory, client and job print summaries. Existing quotation, purchase and payroll HTML templates share the uploaded header.

## Database or migration impact
None. Reuses existing company template storage and default letterhead manifest.

## Configuration and dependency changes
Header stored as a normalized PNG in the configured company template storage. No packages added.

## Compatibility or breaking changes
None. Existing header remains the fallback until an image is uploaded. Previously generated files are unchanged.

## Security impact
Image extension, file size, dimensions and decoding validated before saving. No outbound services added. Upload is restricted to administrators in Settings.

## Deployment/update steps
Install the version 1.1.500 desktop update. Open Settings and use Upload Header. Keep company template storage accessible to all office PCs.

## Validation performed
Release build succeeded; output EXE version 1.1.500.0 verified. QA harness passed upload/persistence, embedded HTML image and invalid-upload preservation checks. Settings was rendered and visually inspected with an uploaded sample header; invoice and job-card PDF samples were generated and rendered. Existing Settings visual smoke test passed. The original letterhead manifest was restored after QA.

## Known limitations
Image headers require a wide aspect ratio (at least 2:1), maximum dimensions 12000 x 4000 pixels. Structured data exports such as CSV retain their data schema. Synthetic agent simulation diagnostic PDFs use their own text format.

## Rollback guidance
Reinstall the prior desktop version and select the prior default Letterhead in Master Data if required. Existing company template files are retained.
