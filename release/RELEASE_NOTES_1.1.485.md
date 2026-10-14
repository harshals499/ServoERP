# ServoERP release notes â€” 1.1.485

Release date: 05/10/2026
Assembly/file version: 1.1.485.0

## User-facing summary

AMC deletion is available inside Edit AMC. Client imports reject quotation artifacts and provide a reviewed repair workflow for existing suspect records. Site duplicate review and import identity preserve one retained site per client without merging unrelated clients.

## Features and fixes

- Edit AMC includes Delete AMC and an explicit ServoConfirmDialog confirmation. The transaction clears links on retained invoices, jobs, purchase orders and client assets, deletes contract equipment, visits, reminder drafts and SLA logs, then deletes the AMC. Failure rolls back all changes. Successful deletion returns to the refreshed AMC dashboard.
- Clients > + Add Client > Review Imported Client Records automatically scans for numeric-only names, known workbook headings and quotation validity terms. It shows quotation numbers with BidValue amounts in INR. Select suspected records and the correct client, then confirm relinking and archival. Existing duplicate cleanup reassigns declared foreign-key references transactionally; quotation ClientName is updated to the retained client's company name. Quotation identifiers and amounts are preserved.
- Archived client records no longer appear in the including-inactive client list.
- Quotation imports refuse numbers belonging to another client and ambiguous duplicate numbers. They also persist ClientName consistently with ClientID. This avoids accidental reassignment and amount overwrite; it does not recalculate historic quotation amounts.
- Site duplicate detection uses client/site or client/address identity. Equal site names owned by different clients remain separate. Equal addresses under one client are review candidates, not automatic merges.
- Site merges reject different-client ownership at execution time. Archived sites disappear from repository lists and AMC selectors; archived site names resolve to the retained site during imports. Later merges redirect earlier aliases to the newest survivor.
- Optional SiteID in Sites workbook imports updates a saved site's name without creating another site. IDs must belong to the selected client, be unarchived and not conflict with another site's name. Normal site saves reject another active site with the same normalized name under the client.
- Related client/site/quotation/contract/invoice/job/purchase caches refresh after confirmed duplicate cleanup.

## Affected modules

AMC, Clients, Quotations, Sites, Master Data Excel imports and existing duplicate cleanup. Version metadata and installer version definition are updated.

## Files changed

- CHANGELOG.md
- SOURCE_CODE/DAL/ClientRepository.cs
- SOURCE_CODE/DAL/ContractRepository.cs
- SOURCE_CODE/DAL/SiteRepository.cs
- SOURCE_CODE/HVAC_Pro_Desktop.csproj
- SOURCE_CODE/Installer/ServoERP.version.iss
- SOURCE_CODE/Properties/AssemblyInfo.cs
- SOURCE_CODE/SERVICES/ExcelImportService.cs
- SOURCE_CODE/SERVICES/SiteService.cs
- SOURCE_CODE/SERVICES/SmartImportDuplicateCleanupService.cs
- SOURCE_CODE/SERVICES/SmartImportDuplicateDetector.cs
- SOURCE_CODE/Tests/SmartImportDuplicateDetectorSmokeTests.cs
- SOURCE_CODE/UI/AMCDetailPage.cs
- SOURCE_CODE/UI/AMCPage.cs
- SOURCE_CODE/UI/AddAMCForm.cs
- SOURCE_CODE/UI/ClientImportRepairDialog.cs
- SOURCE_CODE/UI/ClientManagementForm.cs
- TOOLS/Build-ReleaseValidation-1.1.485.ps1
- TOOLS/ReleaseValidation-1.1.485.cs
- VERSION
- release/RELEASE_NOTES_1.1.485.md

## Database and migration impact

No tables or columns dropped or renamed. No new business tables or columns. The existing DuplicateMergeArchive table is created with an IF NOT EXISTS guard where required. LicenseInfo is unchanged. Installation does not automatically merge, delete or reassign customer records. Cleanup requires user review and confirmation. Existing duplicate cleanup can consolidate child rows that conflict with a unique index; use a backup before a confirmed merge.

## Configuration and dependency changes

None. No packages added. .NET Framework 4.7.2 and SQL Server Express remain required. Preserve the existing installation's connection and tenant configuration. The local update archive contains application binaries, runtime assets and these notes; it excludes local tenant settings and test harnesses.

## Compatibility and breaking changes

No public APIs renamed. Import behavior is deliberately stricter: numeric/quotation-term client names, duplicate quotation numbers, cross-client quotation overwrites, duplicate site names and invalid SiteID ownership are rejected with actionable messages. Existing quotation amounts, pricing rules, licence plans, PDF headers and licence validation behavior are unchanged.

## Security impact

No new outbound service calls. New queries use Dapper parameters. Client repair checks Clients/Edit permission and AMC deletion retains Contracts/Delete permission enforcement. Destructive actions use the shared confirmation dialog. No permanent business-data cleanup was executed during validation.

## Deployment/update steps

1. Back up HVAC_PRO and retain the previous application binaries and existing configuration.
2. Close ServoERP on the machine being updated.
3. Extract ServoERP_Update_1.1.485.zip into the existing application folder. Preserve HVACPro.config, connection configuration and other customer settings. This is a local binary update package, not a newly built installer or a published automatic update.
4. Start ServoERP and verify version 1.1.485.0. Check AMC edit and Clients review.
5. For the screenshot's suspect clients, open Clients > + Add Client > Review Imported Client Records. Review quotation numbers and amounts, select the correct retained client, and confirm only verified mappings.
6. Review Master Data > Sites > Check / Remove Duplicates. Keep the correct site and merge/archive only confirmed candidates. For future site renames in Excel, supply the existing SiteID.

## Validation performed

- MSBuild Release/AnyCPU succeeded; SOURCE_CODE/bin/Release/HVAC_Pro_Desktop.exe exists with version 1.1.485.0. Build log: release/build-1.1.485.log.
- SmartImportDuplicateDetectorSmokeTests and ImportPreflightSmokeTests passed, including client-scoped site identity, renamed-site address review, numeric and quotation-term detection, valid names containing numbers, multiline quotation handling and database scans across all twelve import modules.
- Real temporary Excel workbooks verified that invalid client rows create no clients and that a quotation number owned by another client is rejected without changing its stored amount.
- The shared production AMC deletion transaction was exercised with equipment/visit rows and rolled back; the contract was restored. No deletion was committed.
- Native WinForms harness loaded an existing AMC and the client review screen from the Release build, then captured rendered screenshots for visual inspection. The Delete AMC action is visible without clipping. Empty and synthetic populated review states were captured.
- git diff --check passed. No new dependencies.

## Known limitations

- The local database does not contain numeric client names matching the screenshot. The reported 218 excess records and their correct quotation/site ownership cannot be verified from this environment. This release supplies reviewed repair tools; it does not guess or automatically rewrite historical amounts.
- Site address matching is advisory because multiple buildings can share an address. Renames without a saved SiteID, retained alias or other reliable identity still require review.
- A committed client/site merge was not exercised against production data. The existing merge engine is reused with transactional guards; inspect the records and back up before confirming.
- No installer, signing, public upload or automatic-update feed publication was performed.

## Rollback guidance

Restore prior binaries and configuration after closing ServoERP. No new schema requires rollback. Confirmed merges move references and may consolidate unique child conflicts; restore a pre-cleanup database backup to reverse them. Confirmed AMC deletion is permanent and likewise requires a database backup to recover.
