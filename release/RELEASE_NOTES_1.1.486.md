# ServoERP release notes - 1.1.486

Release date: 05/10/2026. Assembly/file version: 1.1.486.0.

## User-facing summary

Save, selection, filter reset and confirmation behaviours are more reliable. Important dashboard actions remain reachable when space is limited. This update includes the AMC Delete, imported-client repair, quotation import protection and client-scoped site identity work from 1.1.485.

## Features and fixes

- Form/page FluentValidation failures and service Error/Critical validation now stop saves. Warning-only business-rule results remain nonblocking. Invalid AMC input returns before persistence; failed AMC reference loading leaves Save disabled.
- Shared save operations reject duplicate invocation and restore each control's original enabled state. Ctrl+S invokes a single visible, enabled editor Save; ambiguous multi-Save surfaces keep their button workflows.
- Grid styling preserves multiple selection and cell selection. Client import repair adds record filtering, Clear Filters, Select all shown, Clear Selection and Close. Filter changes clear the previous selection.
- Duplicate cleanup selects only the shown groups, replacing older hidden group selection. Clear all selection includes filtered-out groups.
- Clear Filters is available in Clients, Contracts, Suppliers, Jobs, Purchases, Payments, AMC, Porter Deliveries, Quotation forecast and Invoice work queue. Payment reset updates the period label, transaction scope, search, type, page and tab indication together.
- Shared headers wrap actions when space is insufficient. Dashboard, Clients and Jobs respond to resizing. Client/Supplier content follows the actual header height, and client quick actions have a scrollable narrow-sidebar layout.
- Dashboard Customize now opens card unlock, lock and confirmed layout reset actions.
- Delete confirmation retains the record identity and linked-record impact in a growing, scrollable summary.
- Normalized same-client site-name protection is enforced by both SiteService and the compatibility ClientService.CreateSite entry point.
- The QA state catalog includes AMC, Attendance and Porter Deliveries. A repeatable source inventory and isolated-database native audit harness are included in the source workspace.
- Retained 1.1.485 features: Delete AMC in Edit AMC with confirmation and transactional linked-record cleanup; numeric/quotation-term client import rejection and reviewed client relinking/archival; quotation-number conflict protection; client-scoped site duplicate detection, archive aliases and optional SiteID-based rename import. Existing quotation identifiers and amounts are preserved during reviewed repair.

## Affected modules

Shared forms/pages, validation, save runner, grids, headers and confirmation dialogs; Dashboard, Clients, Contracts/AMC, Suppliers, Jobs, Purchases, Payments, Invoices, Quotations, Porter Deliveries and Master Data duplicate review. Inventory, Employees, Attendance, Payroll, Reports and SLA were included in principal-screen audit coverage.

## Database and migration impact

No table/column drops or renames. No new business table/column in this update. The retained import-repair code guards creation of the existing DuplicateMergeArchive table where absent. LicenseInfo structure is unchanged. Installation does not automatically merge, delete, relink or recalculate customer records. Approved cleanup still requires review and confirmation.

## Configuration and dependency changes

None. No packages added. Existing .NET Framework 4.7.2 and SQL Server Express requirements apply. Preserve installed tenant, database and licence configuration. Dashboard customization uses existing local layout persistence. Test database and test runtime configuration are development-only and excluded from the update archive.

## Compatibility and breaking changes

No public APIs renamed. Saves that previously continued after validation errors now fail with validation feedback until the invalid fields are corrected. Warning-only business results remain allowed. Import conflict protection is intentionally stricter. Existing pricing calculations, licence plans/endpoints/machine ID logic, PDF headers and Fresh Start behaviour were not changed.

## Security impact

Validation errors are enforced before business persistence. Existing business-service permissions and destructive-action confirmation remain in place. No new third-party outbound calls or credential/configuration exports. The package excludes customer configuration, database backups, actual customer-data screenshots and native test executables.

## Deployment/update steps

1. Back up the customer's SQL database and installed application/configuration.
2. Close ServoERP and apply ServoERP_Update_1.1.486.zip through the existing update deployment procedure. Preserve existing connection, tenant and licence files. Public distribution uses the standard Velopack installer and GitHub update feed. Preserve existing customer configuration during updates.
3. Confirm executable file version 1.1.486.0. Open the affected modules and verify the customer's normal Add/Save/filter workflows.
4. For the client-PC issue, use Clients > Add Client > Review Imported Client Records. Review quotation identities and amounts, select the correct retained client and confirm only the intended repair. Review site duplicates within the same client before merging. AMC deletion requires its own explicit confirmation.

## Validation performed

- Release build succeeded; SOURCE_CODE/bin/Release/HVAC_Pro_Desktop.exe exists with file version 1.1.486.0.
- DataQualitySmokeTests, SmartImportDuplicateDetectorSmokeTests, ImportPreflightSmokeTests, UiPolicyTests and UiQaStateCatalogTests passed, plus shared selection/save-runner regression checks.
- Inventory Add/Save/Delete, blocked invalid inventory Save, client Add/Save/Delete and site rename/duplicate rejection/Delete were verified through application services with SQL readback on HVAC_PRO_ActionAudit_20261005. Zero ACTION-AUDIT client fixtures exist in authoritative HVAC_PRO.
- Native principal-screen controls and screenshots were captured for 20 screens/dialogs, plus AMC edit and confirmation fixtures. Filter reset handlers were invoked, repair filtering/selection was verified with synthetic rows, and Customize menu opening was verified. Populated Clients was also inspected through native window capture. Source inventory covers 82 page/dialog classes; source-only, rendered-control and business-persistence evidence are explicitly separated in ACTION_AUDIT_1.1.486.md.
- Actual Excel artifact/cross-client quotation-overwrite checks and AMC linked-delete transaction rollback passed in retained 1.1.485 validation; those results remain labelled 1.1.485 rather than being claimed as fresh financial workflow tests.
- Update archive integrity and packaged executable checksum were verified after local packaging.
- A fresh Release build and CiSafe smoke suite passed for public packaging. Velopack setup, full package, portable archive and update-feed artifacts passed the release artifact check.

## Known limitations

The customer's 218 suspect records are absent from the local database, so their repair is not certified. Financial posting/deletion, every nested editor/tab, payroll processing, provider bookings/sends, settings/security/licence mutations and customer cleanup were not all executed end-to-end. Some native captures are loading/empty states and establish layout only. Dense analytical cards can still require scrolling at narrow widths. Catalogue completeness and button presence do not imply every business workflow passed. The public installer is unsigned; Windows may show an unknown-publisher warning.

## Rollback guidance

Close ServoERP and restore prior application binaries while preserving configuration. No new schema rollback is required. Approved AMC deletion is permanent; restore a pre-deletion database backup to recover it. Approved client/site merges move references and may consolidate unique child conflicts; restore a pre-cleanup database backup to reverse them. Dashboard layout changes can be reset through Customize or restored from the previous layout configuration.

## Public distribution

- GitHub release: https://github.com/harshals499/ServoERP/releases/tag/v1.1.486
- Windows installer: https://downloads.servoerp.in/ServoERP_Setup_1.1.486.0.exe
- Production metadata: https://servoerp.in/latest.json
- Publication verification is recorded separately in the public-release validation report after upload.
