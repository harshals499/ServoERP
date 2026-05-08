# ServoERP Data Quality Audit

Date: 2026-05-07

## Scope

This audit inspected the existing WinForms service boundaries, save flows, import flow, calculation logic, and ERP module models before adding protections. The current app already has useful localized checks in some places, but the checks are inconsistent and many high-risk saves still accept bad data until SQL Server or later workflows fail.

## Risky Modules

- Clients and Sites: client identity, GST/PAN/contact data, site linkage, duplicate customer names and GSTINs.
- Vendors: duplicate suppliers, GST/PAN/IFSC/contact data, open PO/payable relationships.
- Inventory: negative stock, reserved stock greater than current stock, bad rates, weak item naming.
- Quotations: required client/site links, line item math, GST, locked/default quotation number behavior, conversion to PO/invoice.
- Invoices: line item totals, GST/tax totals, paid amount, client/site links, due dates, draft inventory reservations.
- Purchase Orders: vendor links, PO totals, paid amount, pay-by dates, pending charge conversion into invoices.
- Contracts: impossible dates, missing covered site, negative monthly/annual values, job/invoice generation from contracts.
- Service Jobs: broken client/site links, impossible dates, part usage that can drive stock negative, incomplete checklist/attachment context.
- Master Data imports: Excel/CSV header mismatch, bad row formats, silent default dates, missing duplicate review.
- HVAC Assets: missing client linkage, warranty date inconsistencies, weak equipment identity checks.

## Risky Forms And Save Buttons

- `ClientForm` and client/site save actions depend mostly on `ClientService`.
- `VendorForm` imports and saves route through `VendorService`.
- `InventoryForm` saves and stock movements route through `InventoryService`.
- `TenderBidForm` quotation saves route through `TenderService.SaveTenderBid`.
- `InvoiceForm` saves route through `InvoiceService.CreateInvoiceWithLineItems` and `UpdateInvoiceWithLineItems`.
- `PurchaseForm` saves route through `PurchaseService.Create` and `Update`.
- `ContractManagementForm` can generate invoices and depends on contract/invoice validation.
- `JobManagementForm` uses `JobService.Create`, `Update`, and `AddPartUsed`.
- `MasterDataForm` saves assets/documents/rate cards and runs bulk imports through `ExcelImportService`.

## Risky Import Flows

- `ExcelImportService.Import` reads headers and rows directly, then runs per-row import methods.
- Missing headers can silently produce empty values because `GetCell` returns empty string.
- Invalid dates fall back to `DateTime.Today`, which can hide bad source data.
- Row errors are collected, but there is no reusable import review score or header pre-check.
- Imports can create duplicate clients/vendors when the source data uses small spelling or casing differences.

## Missing Validations

- Centralized required-field checks before all module saves.
- Unified email, phone, GSTIN, PAN, date, and money validation.
- Duplicate GSTIN/name detection for clients and vendors at save time.
- Reference integrity checks for client-site relationships.
- Negative stock prevention before inventory deduction.
- Paid amount cannot exceed invoice/PO total.
- GST/tax/line total verification at the service boundary.
- Import header validation before importing rows.
- Asset warranty/install date sanity checks.

## Weak Business Rules

- Purchase orders allowed weak validation before repository save.
- Inventory stock deduction could happen without checking resulting stock.
- Quotation and invoice calculations were handled locally but not independently verified.
- Contracts allowed missing site as a soft business issue without a shared warning model.
- Jobs could consume parts even when available stock was below the requested quantity.

## Duplicate Risks

- Client duplicates by GSTIN are business-critical because invoices, sites, and contracts attach to client IDs.
- Vendor duplicates by GSTIN or normalized name can split purchase history, payables, and supplier pricing.
- Imports are the main duplicate entry point because they can create many records quickly.

## Calculation Risks

- Invoice line amount, tax amount, subtotal, GST split, total amount, and balance due need independent verification.
- PO totals can drift from line totals when tax/charges are edited manually.
- Quotation taxable and GST totals can drift from line items after manual edits.
- Paid amount greater than total can make balances misleading.

## Reference Integrity Risks

- A site can be selected without proving it belongs to the selected client.
- Jobs, contracts, invoices, quotations, and POs rely on client/site IDs to stay consistent.
- Pending PO charges can flow into work orders/invoices only if links remain valid.

## Crash And Recovery Risks

- Failed startup and SQL errors are logged, but operational validation failures are not consistently audited.
- Forms do not yet have a full reusable unsaved-change recovery workflow.
- Import failures are visible after completion, but not stored as a durable review queue.

## Priority Ranking

### Critical Now

- Add reusable validation result models and service engines.
- Block hard-invalid client/vendor/site/inventory/quotation/PO/invoice/contract/job/asset saves.
- Block duplicate client/vendor GSTIN saves.
- Prevent negative stock from inventory and job part usage.
- Verify invoice, PO, and quotation calculations before save.
- Add import header review before row import.
- Add audit/error/recovery service foundations.

### Important Next

- Add visible validation summary panels inside each major form.
- Add inline validation labels near fields.
- Add soft-warning ignore-with-reason flow.
- Add duplicate review UI for imports before committing rows.
- Add data health score panel to Master Data and module dashboards.
- Add persistent import batch review records.

### Later

- Stronger PO-vs-invoice and quotation-vs-invoice conversion checks.
- Attachment-required policies by document type and workflow stage.
- Configurable validation rules per company.
- Admin dashboard for data quality trend reporting.

### Overkill

- AI/OCR-driven anomaly detection before basic deterministic rules are complete.
- Fully custom workflow engine for every save action.
- Blocking every soft warning; operational ERP data often needs controlled exceptions.
