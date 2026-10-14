# ServoERP 1.1.481 Release Notes

Release date: 04/10/2026

## User-facing summary

ServoERP 1.1.481 interconnects the data behind every operational sidebar area through a 100-relationship catalog. The app automatically establishes safe links and reflects the live result in Relationship Health cards without guessing or rewriting conflicted business ownership.

## Features and fixes

- Expanded monitored and automatically enforced relationships from 55 to 100.
- Added client contact, activity, team, asset, document, rate-card, and price-memory relationships.
- Added supplier price, stock supplier, supplier advance, and purchase-order relationships.
- Added inventory reservation, usage, invoice, job, stock, purchase, and pending-charge relationships.
- Added employee skills, documents, attendance, salary, payroll, loans, advances, leave, TDS, and statutory-payment relationships.
- Added contract SLA, quotation template, master lookup, unit alias, import batch, and expense category relationships.
- Added compound Client/Site protection for client assets and client documents.
- Added automated coverage that locks the catalog at 100 registered relationships.

## Affected modules

- Quotations, Invoices, Purchases, Payments
- Inventory, Clients/Sites, Suppliers
- Jobs, AMC, Contracts, Service Desk
- Employees, Attendance, Payroll
- Master Data, Profitability, Reports
- SQL Server relationship migration

## Database or migration impact

No tables or columns are dropped, renamed, or rewritten. Existing foreign keys are detected and reported. Missing relationships are added with `WITH CHECK` only when current rows are valid. Client asset and document ownership gain compound Client/Site constraints where current data permits. Conflicts are recorded in `RelationshipIntegrityIssues`.

## Configuration and dependency changes

None. No packages, credentials, endpoints, or third-party services were added.

## Compatibility or breaking changes

None. Existing public tables, columns, classes, and methods remain compatible.

## Security impact

Broader SQL-level relationship enforcement reduces the risk of operational, customer, supplier, inventory, and payroll records being attached to nonexistent or inconsistent parent records. Authentication, licensing, and network behaviour are unchanged.

## Deployment and update steps

1. Close ServoERP on the target PC.
2. Install ServoERP 1.1.481 using the installer or automatic updater.
3. Start ServoERP and connect to the configured `HVAC_PRO` SQL Server database.
4. Open Reports > Data Quality > Relationship Health.
5. Confirm the Auto-connected, Needs review, Orphan references, Client/site issues, and Protection cards reflect that client database.

## Validation performed

- Release compilation with MSBuild 17.14 for .NET Framework 4.7.2 completed with zero errors and zero warnings before packaging.
- CI-safe smoke suite passed, including the exact 100-relationship catalog guard and protection-card calculations.
- The automatic connector ran against SQL Server Express and recorded 85 enforced migration rules plus six relationships blocked by existing data.
- The live report returned 100 monitored relationships, 94 fully protected relationships, six needing review, 13 orphan references, 179 Client/Site issues, and 94.00% protection.
- The 1537 × 861 Relationship Health view was rendered and visually inspected with the expanded module chart and cards.

## Known limitations

- Existing orphan or cross-client rows are preserved because their correct business owner cannot be inferred safely.
- The inspected development database has six such blocked relationships. After authorised correction, refreshing the report automatically retries protection.
- Reports consume the linked data but remain read-only; operational records continue to be edited in their owning modules.
- The installer is not Authenticode-signed because no private signing key is configured on the release machine; Windows may display an unknown-publisher warning.

## Rollback guidance

- Reinstall the previous approved ServoERP installer if application rollback is required.
- Additive relationship constraints remain compatible with older application builds.
- Do not remove constraints ad hoc. Any schema rollback must use a reviewed DBA migration during an approved release window.
