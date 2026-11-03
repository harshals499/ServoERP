# Local document review/import

Open **Import → Review PDF / image / MSE workbook** in Quotations, Invoices or Purchases. Choose a PDF, scanned PDF, JPG or PNG. Processing stays on the PC; Windows OCR language support must already be installed.

Each PDF page starts as a separate proposed document. Select a page and merge the next page only when it continues the same document. Use the page selector to inspect every original. Documents sharing one page must first be split into separate files.

Correct the type and sales/purchase direction, then choose **Match party**. Select an existing customer/supplier; create and review missing masters in their normal modules first. Enter missing fields rather than assuming defaults. Review all line items, HSN, quantity/unit, rate, discount and GST. **Match selected item** optionally links an existing product. Source evidence contains suggested values and warnings; OCR confidence is not an accuracy guarantee.

Dates use DD/MM/YYYY and money uses INR. Explicitly enter zero tax/discount/round-off when the source has none. A due date is required and is never inferred. Totals and line arithmetic must agree exactly. Set **ReviewConfirmed** only after checking every field, then choose **Save reviewed draft**. Repeating a save of the same source/page range returns the existing record; a likely business duplicate is blocked.

Open the saved record in its normal module list. Use its card menu's **Original imported document**, or **Saved imports / original** in the import dialog. Customer originals also have a client document registration; purchase originals use the existing receipt attachment field. Do not mark a purchase received just to store its invoice: ServoERP's normal received/payment business rules still apply.

Supported saves: sales quotations, sales invoice drafts, and vendor invoice records in Purchases. Imports do not post accounting, reserve/issue stock, receive goods or mark paid. Explicit normal-module actions control those effects. Existing reports retain their current draft handling and some can include active drafts; import does not create an accounting posting.

Other recognized types (challans, purchase orders, receipts and credit/debit notes) need their normal module workflows. Complex or unrecognized item tables need manual entry. Mixed-rate sales invoices and quotation/purchase discounts or round-off are excluded from this first version. Existing reviewed XLS/XLSX imports remain unchanged; no CSV importer or master auto-creation is added.

Implementation: shared DocumentImportDraft and evidence model; one local extractor and review dialog; FluentValidation plus existing BusinessRuleEngine/CalculationVerificationService; DocumentImportService owns durable snapshots and transactional Dapper mappings. Add future types to the enum/classifier and validation/mapping branches without duplicating the dialog. Metadata records source, page range, extraction/review status and safe error codes, without sensitive document contents in logs.

## Madhusuman historical profiles

The reviewed workflow also accepts original XLSX files. It recognises MSE printable quotations (Quotation No/Date, To, Sub, UOM/Quantity or Unit/Qty, Rate/Amount, location sections and separate Supply/Installation columns). Cached numeric values and both formatted/cached worksheet-cell evidence are preserved; formulas are not recalculated and originals are never modified.

The active Sales and Purchases register tabs map invoice numbers/dates, parties, tax buckets, totals, due dates and payment references. Sales_Wrong, masters, proforma and tracking tabs are excluded. Register rows have no invented quantity-one invoice lines: actual invoice details must be supplied before saving. Only explicitly populated tax buckets are summed; blank fields stay unresolved. Worksheet and row identify each retry-safe source segment.

Madhusuman Tally e-invoices distinguish seller GST from Buyer (Bill to), preserve HSN, service quantity/UOM/rate/amount, source dates and signed round-off. A separate printable PDF quotation profile handles the historical Total Rs. (taxable) / Total Amount (grand total) variant; workbook meanings differ. Exact source GST rounding and price precision are retained. Any mismatch with line arithmetic remains a review blocker.

Original XLSX review shows cached cells beside editable fields, with Open selected original available for the complete unchanged workbook layout. Register summary imports require actual item detail. Other supplier invoice layouts continue through conservative local extraction and human review; no universal exact-layout claim is made.

## Detect files on a local drive

Choose **Scan local drive**, select a fixed or removable drive, and click **Scan selected drive**. The scan checks metadata across that drive; it does not save business records. System/cache folders are skipped by default; links and cloud placeholders are always skipped. Use the filename filter to narrow large scans. Cancel stops discovery safely; a capped scan is labelled partial.

Choose **Select next 50** or tick individual eligible files, then **Extract selected for review**. Classification happens locally at this point. Review and correct each document in the existing import screen before saving. Reopen **Scan local drive** for the retained results and another batch. Failed files can be selected manually for retry. Empty files and files over 20 MB cannot be extracted. Network drives, automatic watching and CSV discovery are not supported.
