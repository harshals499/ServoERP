# ServoERP 1.1.526

Release date: 1 November 2026. Candidate; publication is not part of this change.

## User-facing summary
Review business PDFs and images locally, correct their extracted contents, and save approved drafts into the existing quotation, sales invoice or purchase module. No external AI service is enabled or contacted.

## Features and fixes
- Original Madhusuman quotation workbook/PDF, Sales/Purchases register and Tally e-invoice profiles preserve source field meanings and per-cell evidence without recalculating or changing originals. Historical payment columns are evidence only; register summaries never become invented quantity-one lines.
- Shared side-by-side original preview, editable fields/items, source evidence, conservative local classification, party/product selection and explicit review approval.
- PDF, scanned PDF, JPG, PNG and verified Madhusuman XLSX input, up to 20 MB and 40 PDF pages. Each page is proposed separately; merge continuation pages and review document boundaries before saving.
- FluentValidation required-field, exact total, GST, date and line arithmetic checks plus existing module business/calculation validators. Missing or conflicting values remain unresolved.
- SQL transaction covers header, lines, import metadata and attachment registration. File hashes, page overlap checks, business duplicate checks and a database application lock protect retries, including concurrent retries.
- Originals use the existing client upload vault convention. Module card menus include Original imported document; the import dialog also exposes saved import history.
- Async extraction/save with cancellation, safe error messages and progress. Imports remain draft/unpaid; no automatic accounting posting, inventory reservation/issue, receipt or payment action.

## Affected modules
Quotations, Invoices, Purchases, client document registrations and shared import menus. Existing workbook imports remain available.

## Database or migration impact
Guarded additive dbo.DocumentImports and dbo.DocumentImportAttempts metadata tables are created on first use. Reviewed records use existing SQL Server business tables through Dapper. No existing columns, tables, financial reporting views or LicenseInfo structures change. No production business-data migration is performed by installation.

## Configuration and dependency changes
No NuGet packages or credentials added. Requires Windows local PDF rendering and OCR, Windows PowerShell 5.1 and an installed OCR language. Existing PdfPig handles text PDFs. The packaged SERVICES/DocumentImport.LocalOcr.ps1 worker performs only local rendering/OCR. Original files remain in the existing C:\HVAC_PRO_MSE\CLIENT_DATA_UPLOADS vault convention.

## Compatibility or breaking changes
None to existing public APIs, PDF headers, pricing, licensing or theme. Existing module workflows control later posting, stock and payment effects. Vendor invoices are represented by existing PurchaseOrders with VendorInvoiceNumber and Pending status; this version adds no separate accounts-payable invoice entity.

## Security impact
No outbound extraction service. Source text is parsed as untrusted data and never executed as instructions. No source contents, credentials or SQL exception details are logged by the importer. Logs record stage/error type and record IDs only. Permission checks apply to saves and original retrieval. Temporary snapshots are removed when the dialog closes; originals remain in the existing vault. A failed SQL transaction may leave an unregistered content-addressed file for safe reuse; it never leaves a partial business record.

## Deployment/update steps
Deploy the complete verified package, including the SERVICES OCR worker. Back up SQL Server and the client upload vault using normal office procedures. Ensure Windows OCR language support is present. Open the relevant module's Import menu and choose Review PDF / image / MSE workbook. Review type, direction, page grouping, party, dates, every item and totals; set ReviewConfirmed and save. Use the normal module to post/finalize, receive goods or record payment explicitly. Originals on another PC require access to the existing vault path; no new cross-PC file-sync service is introduced.

## Validation performed
Release builds succeeded and the required executable exists. All 39 existing CI-safe checks passed using a harness that mirrors normal QuestPDF startup without interrupting the installed app. Fifteen disposable-SQL regression groups passed, including actual module list/detail repository visibility, all three draft mappings, attachment registration and durable hash checks, atomic rollback, business duplicates, concurrent retries, source tampering rejection, Windows OCR for PNG/JPG/scanned PDF, text PDF and page-separated documents. Six read-only original-corpus groups passed: eleven MSE quotation workbooks, one positioned printable quotation PDF, 647 active Sales-register rows, 537 Purchases-register rows and two original Tally e-invoices. Original workbook/PDF hashes were checked where recorded by the tests; no original records were saved. Native review and incomplete-review blocking were inspected, including a real MSE workbook; findings led to DD/MM/YYYY, toolbar and item-column corrections. Cached numeric source money is preserved even when whole-rupee worksheet formatting hides paise, with both formatted and cached values shown in evidence. Final candidate UI/build/package evidence is recorded separately in artifacts/document-import/evidence.md and only completed checks are reported there.

## Known limitations
Local heuristics/OCR are assistive, not a general AI model. Unrecognized layouts need manual field/item entry; line extraction supports explicit delimited/header tables plus verified Madhusuman printable quotations and Tally e-invoices. OCR may confuse letters and digits. No OCR language is downloaded automatically. Sales/purchase direction is left unresolved unless explicit in the source; review is always required. Sales quotations only; other recognized document types cannot be saved by this workflow yet. Multiple documents on one page require separate source files; page grouping is reviewed manually. Mixed GST rates on sales invoices, discounts/round-off on quotation and purchase mappings, quantities beyond two decimal places and automatic master-record creation are excluded. Use the existing module workflows for these cases. Existing financial reports retain their current draft handling: the FinancialInvoices view includes active drafts, so some reports can include an imported draft before explicit posting. IsPosted remains false and no accounting or payment entry is created. Verified original Madhusuman quotation XLSX and active Sales/Purchases register tabs now use this review workflow; summary register rows require actual item details before save. Wrong/master/proforma/tracking tabs are excluded. Existing other workbook imports accept XLS/XLSX; CSV is not newly added. QA schema copies do not reproduce all production triggers; production data was not written.

## Rollback guidance
Reinstall the previous verified 1.1.525 package if required. Preserve the additive import metadata tables, original vault files and saved business records; do not delete or automatically reverse customer data. Inspect any saved draft in its module before applying normal cancellation/archive workflows. No automatic data rollback is performed.
