# ServoERP 1.1.531
Release date 05/11/2026.

## User-facing summary
The PDF import picker includes document library types and additional business paperwork.

## Features and fixes
Search or select among 19 PDF categories, including Proforma Invoice, Credit Note, Debit Note, Payment Receipt, Work Order and Service Report. AMC Contract covers AMC agreements. These categories save the PDF under Master Data Documents. Open Imported PDFs to change a saved category while keeping the original attachment.

## Affected modules
PDF Imports, Master Data Documents, Purchases, Invoices, and Quotations.

## Database or migration impact
No schema migration. Changing a category updates the existing category and document type in one transaction.

## Configuration and dependency changes
None.

## Compatibility or breaking changes
None. The original category keys remain valid.

## Security impact
Category changes require edit access to Master Data and the applicable source and destination modules.

## Deployment/update steps
Install through the standard ServoERP desktop update channel after verification and packaging.

## Validation performed
Release build and PDF review suite passed. All 19 categories were exercised against a disposable SQL Server database. The suite verified that a saved category and document type persisted on library reload while keeping the original PDF path. The built picker was inspected in a narrow window and the suite captured wide and narrow layouts.

## Known limitations
Category search matches the start of a label. PDF imports store attachments.

## Rollback guidance
Reinstall the prior verified desktop version. Previously stored PDFs remain accessible through the all-imports library. New category keys will not be available in the prior picker.


