# ServoERP Relationship and Reporting Integrity Audit

Date: 04/10/2026  
Database inspected: `localhost\SQLEXPRESS / HVAC_PRO`  
Scope: read-only review of the live SQL Server schema, live record relationships, repository/service code, and reporting traceability.

## 1.1.481 cross-module extension

The automatic catalog now monitors 100 established relationships across every operational sidebar area: Quotations, Invoices, Purchases, Payments, Inventory, Clients/Sites, Suppliers, Jobs, AMC/Contracts, Employees, Attendance/Payroll, Master Data, Service Desk, and Reports. Opening or running Relationship Health retries safe enforcement for each relationship and recalculates the cards from the client database. On the inspected database, 94 of 100 relationships are fully protected and the same six legacy-data conflicts remain visible for review.

## Executive answer

Yes, Jobs are connected to Sites.

- `Jobs.SiteID` points to `ClientSites.SiteID` through an enabled and trusted SQL foreign key.
- `Jobs.ClientID` also points to `B2BClients.ClientID`.
- `Jobs.AssignedEmployeeID` points to `Employees.EmployeeID`.
- The job repository joins `Jobs.SiteID = ClientSites.SiteID` to display site details.

However, the two independent foreign keys do **not** prove that the selected site belongs to the selected client. SQL Server currently accepts this invalid combination:

`Job.ClientID = A` + `Job.SiteID = a site owned by Client B`.

This is the most important missing relationship rule in the application.

## Implementation status

Implemented in ServoERP 1.1.479 after this audit:

- Reports > Data Quality now includes a Relationship Health screen. It calculates coverage, direct-reference integrity, client/site context consistency, orphan counts, and SQL protection across 55 monitored operational relationships. The extra monitored relationship is the existing `ExpenseEntries.JobId → Jobs.JobID` link.
- Installed clients run an idempotent startup migration that attempts the 34 missing direct foreign keys and seven compound client/site relationships. Clean relationships are added with `WITH CHECK`; conflicts are written to `RelationshipIntegrityIssues` and startup continues without changing business data.
- Save-time validation now rejects cross-client or nonexistent Site, Contract, Invoice, Quotation, Job, Technician, Vendor, and Stock links in the affected workflows.
- The demo loader now stores Site IDs by Client and uses the same owner for contracts, quotations, invoices, jobs, incidents, and payments.

On the inspected database after the migration, 54 of 55 monitored direct relationships have enabled SQL protection. The remaining direct relationship is blocked by the 13 pre-existing `InvoiceLineItems.StockItemID = 0` values. Five compound client/site rules remain blocked by the 175 preserved demo mismatches. No business rows were automatically rewritten.

## Mathematical method

The audit uses three measures:

1. **Coverage %** = `populated relationship keys / child records × 100`
2. **Reference integrity %** = `keys matching a parent record / populated keys × 100`
3. **Client-site consistency %** = `site links whose Site.ClientID equals record.ClientID / populated site links × 100`

For core operational modules, 54 expected direct business references were identified from stored ID columns and the joins performed by the application:

- Enforced by a SQL foreign key: **20 / 54 = 37.04%**
- Not enforced by a SQL foreign key: **34 / 54 = 62.96%**

Across 30 populated-key checks in the live database:

- Populated relationship keys checked: **2,257**
- Keys resolving to a parent row: **2,244**
- Orphan keys: **13**
- Weighted direct-reference integrity: **2,244 / 2,257 = 99.42%**

The 99.42% result is misleading if used alone: it measures whether an ID exists, not whether the combination is correct. Client-site consistency is materially worse.

## Jobs-to-sites result

| Measure | Calculation | Result |
|---|---:|---:|
| Total jobs | count | 137 |
| Jobs with a site | 129 / 137 | 94.16% |
| Site IDs resolving to a real site | 129 / 129 | 100.00% |
| Jobs whose site belongs to another client | 76 / 129 | 58.91% |
| Jobs with a correct client/site pair | 53 / 129 | 41.09% of site-linked jobs |
| Fully valid client + site jobs | 53 / 137 | 38.69% of all jobs |
| Jobs with a technician | 126 / 137 | 91.97% |
| Jobs linked to an AMC/contract | 20 / 137 | 14.60% |
| Jobs linked directly to an invoice | 1 / 137 | 0.73% |

All 76 job client/site mismatches are records whose job number starts with `SD-`. Non-demo jobs have zero client/site mismatches among populated site links, but 8 non-demo/QA jobs have no site.

The source of the demo mismatch is deterministic. The demo loader creates 25 clients and 40 sites because the first 15 clients receive two sites. It later addresses `$siteIds` using `$clientIndex`, as if there were one site per client. This shifts most site ownership associations. The same indexing pattern is used for demo contracts, quotations, invoices, jobs, and service incidents.

## Client-site consistency by module

| Module | Site-linked rows | Wrong client/site pairs | Consistency | Observation |
|---|---:|---:|---:|---|
| Jobs | 129 | 76 | 41.09% | All 76 mismatches are `SD-` demo records |
| AMC contracts | 60 | 19 | 68.33% | All 19 mismatches are demo records |
| Invoices | 112 | 24 | 78.57% | All 24 mismatches are `SD-` demo records |
| Quotations | 40 | 28 | 30.00% | All 28 mismatches are `SD-` demo records |
| Purchase orders | 36 | 0 | 100.00% | Current populated links are consistent |
| Service incidents | 29 | 28 | 3.45% | All 28 mismatches are `SD-` demo records |
| **Combined** | **406** | **175** | **56.90%** | **175 / 406 = 43.10% inconsistent** |

## Relationships already established

The live database contains 83 enabled foreign-key constraints. Eighty are trusted by SQL Server; three are untrusted.

Important established relationships include:

| Domain | Established relationship |
|---|---|
| Client | `ClientSites.ClientID → B2BClients.ClientID` |
| Jobs | `Jobs.ClientID → B2BClients.ClientID` |
| Jobs | `Jobs.SiteID → ClientSites.SiteID` |
| Jobs | `Jobs.AssignedEmployeeID → Employees.EmployeeID` |
| Job detail | Checklist, parts-used, and activity rows → `Jobs.JobID` |
| AMC | `AMCContracts.ClientID → B2BClients.ClientID` |
| AMC | `AMCContracts.SiteID → ClientSites.SiteID` |
| AMC | Equipment and visits → `AMCContracts.ContractID` |
| Sales | `Invoices.ContractID → AMCContracts.ContractID` |
| Sales | `InvoiceLineItems.InvoiceID → Invoices.InvoiceID` |
| Collections | Payments → invoice and client |
| Quotations | Quotation lines → quotation |
| Purchasing | Purchase order → vendor |
| Purchasing | Purchase lines → purchase order and inventory item |
| Inventory | Stock movement → stock item |
| Inventory | Supplier price → vendor and stock item |
| CRM | Contacts, team, activity, assets, documents, price memory, and rate cards have parent relationships where defined |
| Payroll | Attendance, salary, payroll, loan, advance, leave, TDS, and statutory rows have their primary employee/run relationships |
| Security | Users, roles, sessions, tokens, permissions, and company membership have primary relationships |

The three untrusted constraints are:

- `FK_AMCContracts_Clients` (a duplicate client constraint also exists and is trusted)
- `FK_AMCEquipment_AMC`
- `FK_AMCVisits_AMC`

An enabled but untrusted foreign key protects future writes but SQL Server has not certified all pre-existing rows under that specific constraint.

## Missing relationships that should be established

The table below lists the 34 missing direct business-data constraints. “Populated/orphans” describes the current live data. A zero orphan count does not remove the need for a constraint; it only means the relationship can probably be added after validation.

| Priority | Missing relationship | Populated / orphan keys | Why it matters |
|---|---|---:|---|
| P0 | `ClientSites.AssignedTechnicianID → Employees.EmployeeID` | 0 / 0 | Prevent invalid default technician assignment |
| P0 | `Jobs.LinkedContractId → AMCContracts.ContractID` | 20 / 0 | Contract profitability and AMC visit traceability |
| P0 | `Jobs.InvoiceId → Invoices.InvoiceID` | 1 / 0 | Job revenue and invoicing status |
| P0 | `AMCVisits.JobID → Jobs.JobID` | 0 / 0 | Prevent a visit from pointing at a deleted/nonexistent job |
| P0 | `Invoices.ClientID → B2BClients.ClientID` | 764 / 0 | Customer ledger correctness |
| P0 | `Invoices.SiteID → ClientSites.SiteID` | 112 / 0 | Site revenue and service history |
| P0 | `Invoices.QuotationBidID → Quotations.BidID` | 29 / 0 | Quote-to-invoice conversion traceability |
| P0 | `InvoiceLineItems.StockItemID → StockItems.ItemID` | 19 / **13** | Current orphan value `0` makes item reporting unreliable |
| P0 | `InvoiceLineItems.JobID → Jobs.JobID` | 0 / 0 | Line-level job revenue allocation |
| P0 | `Quotations.ClientID → B2BClients.ClientID` | 56 / 0 | Quote pipeline by client |
| P0 | `Quotations.SiteID → ClientSites.SiteID` | 40 / 0 | Quote pipeline by site |
| P1 | `Quotations.RecommendedVendorID → Vendors.VendorID` | 7 / 0 | Supplier recommendation integrity |
| P1 | `Quotations.TemplateId → QuoteTemplates.TemplateId` | 0 / 0 | Template lineage |
| P1 | `QuotationLineItems.InventoryItemId → StockItems.ItemID` | 8 / 0 | Availability and costing |
| P1 | `QuotationLineItems.VendorID → Vendors.VendorID` | 8 / 0 | Selected supplier integrity |
| P1 | `QuotationLineItems.BestSupplierId → Vendors.VendorID` | 8 / 0 | Supplier comparison integrity |
| P0 | `PurchaseOrders.ClientID → B2BClients.ClientID` | 36 / 0 | Client/project purchasing |
| P0 | `PurchaseOrders.SiteID → ClientSites.SiteID` | 36 / 0 | Site cost allocation |
| P1 | `PurchaseOrders.RelatedContractID → AMCContracts.ContractID` | 0 / 0 | Contract cost allocation |
| P1 | `PurchaseOrders.RecommendedByBidID → Quotations.BidID` | 4 / 0 | Quote-to-procurement lineage |
| P1 | `PurchaseOrders.AssignedTechnicianId → Employees.EmployeeID` | 0 / 0 | Delivery responsibility |
| P0 | `PurchaseLineItems.LinkedWorkOrderId → Jobs.JobID` | 10 / 0 | Direct job-cost attribution |
| P1 | `PurchaseLineItems.VendorID → Vendors.VendorID` | 72 / 0 | Line supplier integrity |
| P0 | `JobPartsUsed.InventoryItemId → StockItems.ItemID` | 173 / 0 | Materials cost and stock traceability |
| P1 | `JobPartsUsed.VendorID → Vendors.VendorID` | 110 / 0 | Supplier performance by job |
| P1 | `JobPartsUsed.LinkedPoId → PurchaseOrders.POID` | 0 / 0 | PO-to-consumption traceability |
| P0 | `ServiceDeskIncidents.ClientId → B2BClients.ClientID` | 30 / 0 | Customer support history |
| P0 | `ServiceDeskIncidents.SiteId → ClientSites.SiteID` | 29 / 0 | Site incident history and SLA reporting |
| P1 | `ServiceDeskIncidents.AssignedEmployeeId → Employees.EmployeeID` | 30 / 0 | Assignment and workload reporting |
| P1 | `ServiceDeskIncidents.LinkedJobId → Jobs.JobID` | 0 / 0 | Incident-to-work-order conversion |
| P1 | `ExpenseEntries.ClientId → B2BClients.ClientID` | 0 / 0 | Client profitability |
| P1 | `ExpenseEntries.SiteId → ClientSites.SiteID` | 0 / 0 | Site profitability |
| P1 | `ProfitabilityImportRows.MatchedInvoiceId → Invoices.InvoiceID` | 0 / 0 | Imported P&L reconciliation |
| P1 | `ProfitabilityImportRows.MatchedJobId → Jobs.JobID` | 0 / 0 | Imported P&L job matching |

## Missing compound business rules

Direct foreign keys must be supplemented by compound validation. These are the rules that reporting actually depends on:

1. A Site used by a Job, Contract, Invoice, Quotation, Purchase Order, Expense, or Service Incident must belong to that record’s Client.
2. A Contract linked to a Job, Invoice, or Purchase Order must belong to the same Client and, when supplied, the same Site.
3. An Invoice linked to a Job must belong to the same Client and Site.
4. A Payment’s Client must equal its Invoice’s Client. The current data has 0 violations, and the existing composite company-aware foreign keys partially protect this path.
5. A Purchase line linked to a Job should inherit or agree with the purchase order’s Client/Site/project context.
6. An AMC Visit linked to a Job should use a Job linked to the same AMC contract.
7. A quotation converted to an invoice or PO must preserve Client and Site.

The strongest SQL design for client/site consistency is a composite relationship. For example, make `(ClientID, SiteID)` unique on `ClientSites`, then reference both columns from `Jobs`, `Invoices`, `Quotations`, `PurchaseOrders`, and `ServiceDeskIncidents`. Service-layer validation should remain for professional user messages before SQL rejects the save.

## Reporting readiness

The present relationships are not sufficient for fully reliable job profitability reports.

| Reporting path | Calculation | Readiness |
|---|---:|---|
| Job → Site | 129 / 137 = 94.16% populated | Structurally high, semantically only 38.69% of all jobs are valid client/site triples |
| Job → Technician | 126 / 137 = 91.97% | Good for workload reporting |
| Job → Contract | 20 / 137 = 14.60% | Limited AMC attribution |
| Job → Invoice | 1 / 137 = 0.73% | Not adequate for job revenue attribution |
| Invoice line → Job | 0 / 780 = 0.00% | No line-level revenue attribution |
| Purchase line → Job | 10 / 72 = 13.89% | Weak direct-cost attribution |
| Job part → inventory | 173 / 173 = 100.00% | Good data coverage, but no SQL FK protection |
| Job part → vendor | 110 / 173 = 63.58% | Partial supplier attribution |
| Job part → PO | 0 / 173 = 0.00% | No PO-to-consumption lineage |

Recommended mathematical reporting model:

- **Job revenue** = linked invoice-line taxable amounts, falling back to `Jobs.ActualRevenue`, then `Jobs.QuotedRevenue` only when no invoice link exists.
- **Direct material cost** = sum of `JobPartsUsed.TotalCost` plus purchase lines explicitly linked to the job, avoiding double counting when both identify the same PO/item.
- **Other direct cost** = sum of `ExpenseEntries.Amount` where `JobId` matches.
- **Gross profit** = `Job revenue − direct material cost − direct labour cost − travel cost − other direct cost`.
- **Gross margin %** = `Gross profit / Job revenue × 100`, with zero-revenue jobs reported separately rather than divided by zero.
- **Site profitability** = sum of valid jobs/invoices/expenses grouped through a client-consistent Site relationship.
- **AMC profitability** = contract revenue minus all Jobs, Parts, Purchase Lines, and Expenses linked to the same Contract.

Until invoice/job and purchase/job coverage improves, reports should show a **traceability percentage** beside every profit figure. A monetary total without its linked-data coverage can appear precise while being incomplete.

## Required cleanup and implementation order

No data or schema was changed during this audit.

1. Fix the demo loader to store site IDs per client instead of indexing one flat site array by client position.
2. Rebuild or correct only the identified `SD-` demo rows; do not touch customer records as part of demo cleanup.
3. Convert the 13 `InvoiceLineItems.StockItemID = 0` values to `NULL` only after confirming they represent non-stock/service lines.
4. Add service-layer client/site validation to Jobs, AMC, Invoice, Quotation, Purchase, Expense, and Service Desk saves.
5. Add the missing direct foreign keys with `WITH CHECK`, starting with P0 relationships.
6. Add composite client/site constraints after the 175 demo mismatches have been corrected.
7. Recheck and trust the three untrusted AMC constraints; remove the duplicate AMC client constraint only in an approved schema-change release.
8. Add automated tests that attempt cross-client Site, Contract, Invoice, Quotation, and Job combinations and require rejection.
9. Add relationship coverage and orphan counts to the Data Quality report so degradation becomes visible before financial reports are used.

## Code evidence

- Job/client/site/employee foreign keys are declared in `SOURCE_CODE/DAL/DatabaseManager.cs` around lines 1601–1608.
- `LinkedContractId` and `InvoiceId` are added as plain nullable columns around lines 1621 and 1627.
- Job queries join Sites by `j.SiteID = s.SiteID` in `SOURCE_CODE/DAL/JobRepository.cs`.
- `JobValidator` checks numeric ranges but does not prove that Site belongs to Client.
- `ReferenceIntegrityService.CheckClientSite` contains the correct cross-client rule, but it is not used by `JobService.ValidateJobForSave`.
- Demo site assignment uses the faulty flat-array indexing in `TOOLS/DemoData/Load-ServoERPDemoData.ps1` around lines 334, 348, 367, and the equivalent Service Desk insertion.

## Conclusion

ServoERP has a useful relational foundation, and Jobs do have a real database relationship to Sites. The missing layer is **contextual integrity**: the application must enforce that Client, Site, Contract, Quote, Job, Invoice, PO, and Service Incident all describe the same commercial/service chain. The first release should correct the demo loader and enforce client/site consistency; the next should establish the missing P0 foreign keys and improve job-level revenue and cost traceability.
