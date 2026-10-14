# ServoERP 1.1.479 Release Notes

Release date: 04/10/2026

## User-facing summary

ServoERP 1.1.479 adds continuous relationship-integrity protection. Reports now shows which operational links are established, missing, orphaned, or inconsistent, while startup safely adds database constraints only where existing customer data permits.

## Features and fixes

- Added Reports > Data Quality > Relationship Health with mathematical coverage, reference-integrity, client/site-consistency, orphan, and SQL-protection measures across 55 operational relationships.
- Added guarded startup enforcement for 34 missing direct foreign keys and seven compound client/site relationships.
- Added blocking relationship validation to Jobs, AMC contracts, Invoices, Quotations, Purchases, Sites, Expenses, and Service Desk.
- Fixed the demo loader so generated contracts, quotations, invoices, jobs, incidents, and payments remain under the correct Client and Site.
- Re-checks and trusts three existing AMC foreign keys when their current data passes validation.

## Affected modules

- Reports and Data Quality
- Jobs and Sites
- AMC contracts and visits
- Invoices and Quotations
- Purchases and Inventory
- Expenses and Profitability
- Service Desk
- SQL Server startup migration
- Demo-data tooling

## Database or migration impact

- Adds `dbo.RelationshipIntegrityIssues` when absent.
- Adds direct foreign keys only when their tables and columns exist and current data has no orphan references.
- Adds a unique `(ClientID, SiteID)` index on `ClientSites` and compound Client/Site foreign keys only where existing rows are consistent.
- Existing conflicts are recorded for review. No business rows are automatically changed or deleted.

## Configuration and dependency changes

None. No packages, credentials, endpoints, or third-party services were added.

## Compatibility or breaking changes

No public table, column, class, or method was renamed. New saves containing cross-client or nonexistent references are now rejected with a professional validation message. Databases with clean data gain SQL-level protection against future invalid references.

## Security impact

Relationship validation reduces the risk of operational and financial records being attributed to the wrong customer, site, job, invoice, or supplier. No authentication, licensing, network, or credential behavior changed.

## Deployment and update steps

1. Close ServoERP on the target PC.
2. Install ServoERP 1.1.479 using the public installer or automatic updater.
3. Start ServoERP and sign in; the guarded migration runs automatically against the configured authoritative SQL Server database.
4. Open Reports > Data Quality > Relationship Health.
5. Review any orphan or Client/Site issue counts. ServoERP intentionally does not guess or rewrite existing customer ownership.

## Validation performed

- Release compilation with MSBuild 17.14 for .NET Framework 4.7.2 completed with zero errors and zero warnings.
- CI-safe smoke suite passed, including all Relationship Health calculation and blocking-validation cases.
- A live Job service validation smoke confirmed that a cross-client Job/Site combination is blocked before save.
- The guarded migration was executed idempotently against SQL Server Express: 38 rules were enforced or re-trusted and six data-conflicted rules were recorded without changing business rows.
- The live report returned 55 monitored relationships, 54 protected direct relationships, 13 orphan references, and 175 preserved Client/Site inconsistencies.
- Relationship Health was rendered at 1537 × 861 and visually inspected.
- The PowerShell demo loader passed parser validation with no remaining flat Site-array ownership lookup.
- The full enterprise UI smoke was attempted but stopped at the existing Quotation dashboard follow-up-board assertion. The quotation dashboard was not changed by this release; the CI-safe suite passed.
- GitHub release publication and Cloudflare deployment completed successfully.
- The public installer and update package endpoints returned HTTP 200 with the expected artifact sizes after deployment.

## Known limitations

- Existing invalid records are reported and preserved rather than automatically rewritten.
- The inspected development database contains 13 `InvoiceLineItems.StockItemID = 0` references and 175 demo Client/Site inconsistencies, so the corresponding constraints remain blocked pending data review.
- Profitability traceability still depends on linking invoices and purchase lines to Jobs.
- The enterprise smoke currently reports a separate Quotation dashboard follow-up-board assertion.
- The installer is not Authenticode-signed because no private code-signing key is configured on the release machine; Windows may display an unknown-publisher warning.

## Rollback guidance

- Reinstall the previous approved ServoERP installer if application rollback is required.
- The additive schema remains compatible with older application builds and does not require a business-data rollback.
- Do not remove relationship constraints ad hoc. Any schema rollback must use a reviewed DBA migration during an approved release window.
