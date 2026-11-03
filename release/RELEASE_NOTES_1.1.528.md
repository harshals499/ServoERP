# ServoERP 1.1.528
Release date: 03/11/2026

## User-facing summary
Add a dedicated PDF Import sidebar page for manual PDF review and categorised document attachments.

## Features and fixes
Recursive folder scan; default inclusion checkboxes; local multipage preview with zoom, fit to width, password entry and retry; PO, Invoice, Quotation, Challan and Other choices; save-and-next, previous, skip and earlier edits; progress counts; separate destination summary and explicit bulk confirmation; content duplicate protection; isolated per-file errors. Module Imported PDFs actions provide attachment access without creating business transactions.

## Affected modules
PDF Import, sidebar, shared Invoices/Purchase Orders/Quotations headers, Master Data Documents.

## Database or migration impact
Add dbo.PdfReviewDocuments on first confirmed import with guarded creation; register attachments in existing ClientDocuments. No renames, drops, LicenseInfo changes or automatic business-record creation.

## Configuration and dependency changes
None. Reuses Windows local PDF renderer and existing dependencies. New local renderer script must ship in SERVICES. Files up to 200 MB; 10,000 PDFs per scan.

## Compatibility or breaking changes
None. Existing document extraction and business-draft import remain available.

## Security impact
Local rendering only; no new external calls or classification. Passwords kept in memory and sent to renderer stdin. Source snapshots, content hashes, destination permissions, transactional metadata and duplicate checks protect imports. Reparse-point directories are not followed.

## Deployment/update steps
Deploy the complete verified desktop package, including SERVICES/PdfReview.Render.ps1. Open PDF Import under Data & Compliance. Review included PDFs and the final summary, then explicitly confirm import. PDFs remain attachments; create financial records through existing workflows when needed.

## Validation performed
Release build and executable existence passed. CI-safe smoke suite passed. Dedicated regression tests passed for recursive scan, default selection, cancellation, actual multipage rendering, local password unlock, manual category edits, duplicate protection, unresolved acknowledgement, all five destination libraries, transactional rollback, tamper refusal and access checks. Native built review and summary screens were inspected at 1440x940 and 1080x760; actual sidebar navigation and review retention on refresh/cache clearing were tested. SQL writes were confined to explicitly named disposable ServoERP_PdfReviewQA_* databases; no HVAC_PRO or customer-data write was performed. Artifact/feed guard output accompanies the local candidate.

## Known limitations
Windows local PDF rendering required. No automatic classification or data extraction. A PDF category does not create or link a financial transaction. Imported PDF lists show the latest 1,000 attachments per category. Unresolved files require acknowledgement before being left out; inaccessible scan locations are reported. Source snapshots and passwords are not persisted as resumable sessions. Imported PDFs use the existing local attachment-storage convention; other workstations require access to those stored files.

## Rollback guidance
Retain the prior desktop package and database backup. Restore the prior application package if necessary; keep the additive metadata table and imported files. Do not automatically roll back business data or delete attachments.
