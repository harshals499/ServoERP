# ServoERP 1.1.527

Release date: 3 November 2026.

## User-facing summary
Import sales quotations, sales invoices and purchase invoices using local PDF/image extraction and Madhusuman historical XLSX/PDF profiles. Find files on a selected local drive, extract selected batches, review and correct fields, then explicitly save drafts with their original attachments.

## Features and fixes
- Includes the previously unpublished 1.1.526 document-import workflow: side-by-side original review, party/product matching, GST and total validation, duplicate/retry protection, transactional saves and visible original attachments. Madhusuman quotation workbooks/PDFs, invoice registers and Tally e-invoices retain source evidence and require review.
- Scan a selected fixed or removable drive recursively for PDF, JPG/JPEG, PNG and XLSX files; optionally filter filenames.
- Cancellable metadata discovery with live counts. Symbolic links, junctions and cloud reparse placeholders are not followed. Unavailable locations are counted without aborting other folders. System/application/cache folders can be skipped with an explicit option, enabled by default.
- Show filename, format, location, size, modification date and eligibility. Empty/oversized files remain visible with an explanation. Scans that reach 20,000 candidates or 250,000 folders are explicitly partial.
- Select the next 50 eligible files or individual files for content extraction/classification. Retain the detected-file list and successful review entries across batches; one failed file does not discard successful files.
- Recheck file metadata before extraction, reject changed/linked/unavailable sources, and deduplicate staged sources by file hash. Extracted documents still require explicit correction, party matching and approval before saving.
- No automatic disk watching, file movement/deletion, master creation, business save, accounting posting, stock action or payment action.

## Affected modules
Shared document review/import used by Quotations, Invoices and Purchases. The historical MSE profiles and existing transactional save workflows remain in use.

## Database or migration impact
None. Drive discovery performs no SQL operations. Explicit later saves continue through the existing import service and its existing guarded metadata tables.

## Configuration and dependency changes
No packages, credentials or outbound services added. Drive selection/filter settings and scan results are retained only while the review dialog remains open. Existing Windows OCR/PDF rendering and local MSE XLSX extraction dependencies apply when files are selected for review.

## Compatibility or breaking changes
None to existing public APIs, PDF headers, licensing, pricing or global theme. Selected roots must be local fixed/removable drive roots. Network drives/shares are excluded. Existing individual-file upload remains available.

## Security impact
Discovery reads file metadata only and does not execute document contents or trigger cloud placeholder hydration. Links are skipped to prevent loops and traversal outside the chosen drive. Logs contain scan counts and safe error types rather than paths or document contents. Windows access permissions are respected; the scanner does not elevate privileges.

## Deployment/update steps
Install the complete release package using normal office update procedures. Open Quotations, Invoices or Purchases, then Import > Review PDF / image / MSE workbook > Scan local drive. Select a drive and optional filename filter, choose whether to skip system/cache folders, and start the scan. Select up to 50 eligible files and choose Extract selected for review. Review and save each approved document explicitly. Reopen Scan local drive for retained results and the next batch.

## Validation performed
Release build and required EXE passed. Ten owned-filesystem and review-batch regression groups passed, including recursion, supported formats, empty/oversized eligibility, junction-loop exclusion, filtering and partial limits, unavailable paths, cancellation, root/network rejection, changed-source rechecks, batch failure/cancellation retention and review-queue hash deduplication. All 39 existing CI-safe checks passed through an isolated harness. The new native fixture screen and eligible-file selection were inspected; date-format and summary readability findings were corrected. Final native captures and package guard/identity evidence are recorded in artifacts/local-drive-import/evidence.md. No real personal drive was scanned and no production business data was written.

## Known limitations
Discovery identifies candidate file formats; document contents/types are classified only for selected files. Large scans can be partial at the stated limits; refine the filename filter and rescan. Links/cloud placeholders, network drives and unsupported formats are excluded. Extraction is limited to 50 files per batch and 5,000 staged documents. Existing MSE/layout, source-total reconciliation and module-save limitations still apply. Some existing financial reports include active drafts. This feature does not implement a background watcher, resume a cancelled scan, or persist unsaved review edits across application restarts.

## Rollback guidance
Return to the previously verified 1.1.526 local candidate or the prior installed/published version as appropriate (latest published baseline checked during earlier work: 1.1.525). Preserve existing business records, import metadata and original attachments. No new schema requires rollback and no customer data is automatically reversed.

