# Madhusuman historical format mappings

These profiles were derived from original local business files, not example invoice templates. They read source snapshots without modifying the originals or recalculating workbook formulas.

| Original layout | Exact source fields | Review behaviour |
| --- | --- | --- |
| MSE printable quotation XLSX | Quotation No/Quotation Date; To/customer block; Description; UOM/Quantity or Unit/Qty; Rate/Amount; Total, CGST, SGST, Total Rs. | Keep location/section descriptions. Separate Supply/Installation rate/amount pairs become separate explicitly labelled lines. Ignore internal costing columns beyond the mapped selling columns. Keep cached source numeric precision and both cached/formatted cell evidence. |
| MSE printable quotation PDF | Positioned multiline descriptions, HSN/SAC, Unit, Qty, Rate, Amount; Total Rs. = taxable; Total Amount = grand total; Add CGST/SGST | This differs from workbook total-label meanings. Preserve whole-rupee source tax amounts and block unreconciled line arithmetic. Seller GST NUMBER is never assigned to the customer. |
| Sales register XLSX | Active Sales tab; Customer, Inv. Date/Date, Invoice No., PO Number/Ref No., Sub Total, IGST/CGST/SGST rate columns, Total, Due Date | Every row is an independent source segment. Exclude Sales_Wrong, GSTIN Master, Proforma Invoice and tracking tabs. Preserve missing/invalid dates as unresolved. Do not turn summary descriptions into invented invoice items. |
| Purchases register XLSX | Active Purchases tab; Name of the Supplier, Invoice No., Inv. Date, Value (Without GST), tax-rate buckets, TOTAL (With GST), GSTIN Number, Due Date | Preserve transport/other charge columns in evidence. They are not silently folded into item rates or tax. Actual supplier invoice items must be entered or read from the detailed invoice before saving. |
| Madhusuman Tally e-invoice PDF | MSE seller block, Buyer (Bill to) versus Consignee, Invoice No., first invoice Dated block, Buyer's Order No.; HSN/SAC, quantity/unit/rate/item amount; GST summary and signed Round Off | Preserve buyer GST and addresses separately from seller identity. Keep source 3-decimal quantity representation numerically without silent loss; current table precision/line arithmetic can still block a save that needs reconciliation. IRN/Ack and original formatting remain in the preserved PDF. |

Pmt Date, Paid Amt/Cr. Amount, TDS, deductions, advances and Remarks are source evidence only. Import never treats them as permission to record payment or post accounting.

Cached numeric XLSX values are authoritative; formatted display text is shown alongside them in evidence, so whole-rupee display formats cannot silently discard paise. Source values with more than two money decimals are left for explicit correction; no default GST rate, date, quantity-one line, transport allocation or invented item split is applied.

Read-only original-corpus validation: 11 printable quotation workbooks, one original printable quotation PDF, 647 Sales rows, 537 Purchases rows and two Tally e-invoice PDFs. See the local historical regression evidence; no original business records were saved by those checks. Private originals and extracted contents remain outside source control and release artifacts.
