# ServoERP action audit - 1.1.486

Date: 05/10/2026. Application: .NET Framework 4.7.2 / WinForms / SQL Server Express.

The audit found and fixed functional Save, selection, filtering, confirmation and button-placement defects. It also connected an unwired Dashboard Customize button. The client-PC screenshots informed the investigation; their text was treated as evidence, not executable instructions.

## Coverage and evidence

`ACTION_SCREEN_INVENTORY_1.1.486.csv` inventories 82 page/dialog classes, their partial and designer files, candidate actions and literal tab names. Chart primitives and base classes are excluded. This is a source inventory, not proof that all 82 screens passed an end-to-end business workflow. Read-only screens do not need Add, Save or Delete, and single-record actions do not need Select all.

Native WinForms controls were captured for 20 principal screens/dialogs, plus separate AMC edit and confirmation fixtures. Controls CSVs record visibility, enabled state and direct Click-handler presence; grid actions still need their cell-handler source reviewed. Close in the duplicate dialog uses the standard DialogResult mechanism. No direct handler is required for that button. Customize had no equivalent mechanism and was fixed.

Evidence is in `TEST_RESULTS/action-audit-1.1.486`: build.log, shared.log, per-screen logs, controls CSVs and screenshots at large and smaller window sizes. Some deferred-data captures show loading or empty states. Their screenshots establish layout and control presence only. The client screen was additionally inspected through native window capture with populated data.

## Changes and status

| Area | Finding | Result / verification |
| --- | --- | --- |
| Shared Save validation | Form/page validators and service validation guards could warn and continue | Fixed. Error/Critical and FluentValidation failures block persistence; warning-only business results remain allowed. Data-quality smoke suite passed. |
| AMC Save | Invalid input could continue; failed reference loading could re-enable Save | Fixed. Invalid Save returns before persistence; Save stays disabled until reference data is ready. AMC edit was rendered. |
| Shared Save runner | Duplicate invocation possible; disabled related controls re-enabled | Fixed. Busy duplicate calls blocked and original states restored in native regression checks. |
| Save shortcut | Editors lacked a shared keyboard path | Ctrl+S chooses exactly one visible, enabled Save button. Ambiguous multi-Save surfaces retain their own button workflows. Regression check passed. |
| Shared grids | Styling forced single selection, and could replace cell selection | Fixed. Page-owned selection policy preserved. Shown-row selection and clearing regression checks passed. |
| Imported client review | Bulk selection controls and record filtering absent | Added Filter records, Clear Filters, Select all shown, Clear Selection and Close. Synthetic fixture verified that hidden records were excluded and changing filters cleared selection. |
| Duplicate groups | Select-all scope unclear; hidden selections survived Clear | Select shown groups replaces selection with the shown groups; Clear all selection clears visible and hidden group selections. Regression checks passed and dialog rendered. |
| Delete confirmation | Linked-record impact could be clipped in the title | Record identity and impact moved into the body; detail summary grows and scrolls. Confirmation fixture rendered. |
| Dashboard | Customize had no Click handler | Wired to existing card lock/unlock and confirmed layout reset. Native button invocation verified that its three menu actions open. Layout reset and lock persistence were not exercised against user preferences. |
| Clients / Suppliers / Jobs | Fixed coordinates and stale widths could hide header or sidebar actions | Relative dashboard placement and resize handling fixed. Client quick actions use a scrollable sidebar with space for a single column. Rendered results inspected. |

Clear Filters was added and its handler invoked on Clients, Contracts, Suppliers, Jobs, Purchases, Payments, AMC, Porter Deliveries, Quotation forecast and Invoice work queue. Payment reset synchronizes the period label, transaction tab, search, type and page. Existing Inventory, Employee and Site Monitor filtering controls were reviewed; existing independent filter/reset flows were retained.

## Business action checks

All committed test writes used a separately restored database named `HVAC_PRO_ActionAudit_20261005`. The harness refuses any other InitialCatalog. The authoritative `HVAC_PRO` database contains zero ACTION-AUDIT client fixtures.

- Inventory Add, Save and Delete were checked through the application service with SQL readback. Invalid Save left the saved stock unchanged.
- Client Add, Save and Delete were checked through the application service with SQL readback and the active-list result. Client Delete is a soft deletion, so inactive fixture rows remain in the isolated copy.
- Site rename retained its ID and reloaded correctly; another site with the same normalized name under the client was rejected through both SiteService and the compatibility ClientService.CreateSite path; test-site Delete was checked.
- DataQualitySmokeTests, SmartImportDuplicateDetectorSmokeTests, ImportPreflightSmokeTests, UiPolicyTests and UiQaStateCatalogTests passed. The catalog now includes AMC, Attendance and Porter Deliveries; catalog coverage is not a claim that every empty/populated/long-text/modal/scrolled state was executed.
- The retained 1.1.485 work previously passed actual Excel artifact/quotation overwrite checks and an AMC linked-delete transaction rollback test. That evidence is recorded with 1.1.485; it is not relabelled as a new financial end-to-end test.

## Applicability across modules

| Module / surface | Applicable action families | Verification limit |
| --- | --- | --- |
| Clients, contacts, sites, team | Add / Save / Delete or archive / Filter | Client/site service readback and dashboard rendering; contact/team editing source reviewed. |
| Contracts and AMC, equipment, visits | Add / Save / Delete / Filter | AMC edit and list rendered; prior linked-delete rollback evidence. Individual child-editor saves not all exercised. |
| Quotations, invoices, purchases, payments | Add / Save / confirmed Delete / Filter | Source paths and rendered controls reviewed; reset handlers invoked. Full financial posting and deletion workflows not all committed in this audit. |
| Inventory | Add / Save / archive / Filter | Service persistence checks and rendered controls. |
| Employees, Attendance, Payroll | Add / Save and applicable reset/delete / Filter | Source review and native controls captured. Attendance's existing unsaved-change protection retained. Payroll processing not executed. |
| Master Data and duplicate repair | Import / Review / merge or Delete / Filter / selection | Review selection fixture and source guards checked. Customer duplicate cleanup not executed. |
| Dashboard, Reports, SLA, searches and previews | View / Refresh / Filter / Export as applicable | Native principal screens captured; source-only for nested/context-specific previews. Financial export correctness not asserted. |
| Porter, Site Monitor, WhatsApp, AI, mail and storage integrations | Local edit/filter and provider-specific actions | Porter local controls captured. Provider sends, bookings, geocoding and external writes not invoked. |
| Settings, security, licence, backup, LAN/server setup | Save / Test / Cancel and applicable management actions | Source-only for mutations affecting machine/customer configuration, licences, destructive reset or external systems. |

## Known limits and follow-up verification

The customer's 218 suspect records are not present in the local database. The release provides prevention and a reviewed repair workflow; it does not certify that those customer records were corrected. No historic quotation amount was automatically recalculated, and installation does not automatically merge or delete records.

Every source-only or context-specific workflow remains distinguishable from tested business persistence. Before deploying to that client, verify the actual customer data through the repair review, quotation number/amount reconciliation and client-scoped site duplicate review. Confirm a backup before executing any approved cleanup. Dense analytical cards can still require scrolling at narrow widths.

Repeat this audit with `TOOLS/Run-ActionAudit-1.1.486.ps1` after preparing the isolated SQL copy. The script builds Release, compiles the native harness, runs checks and regenerates the inventory. Review its screenshots and logs before concluding that a visual or business workflow works.
