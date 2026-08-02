# Changelog
## 1.1.403.0 - 2026-08-02

- Updated the invoice body with a compact GST compliance summary for recipient GSTIN, place of supply, delivery address, and reverse-charge status while retaining the approved document header.

## 1.1.402.0 - 2026-08-02

- Added an explicit Convert Prospect to Active action in client menus, backed by the existing persisted lifecycle update.
- Removed fixed-height filler space from generated Purchase Orders so short supplier, dispatch, and footer sections stay compact.

## 1.1.401.0 - 2026-08-02

- Removed duplicate company compliance identifiers from quotation PDF footers; registration details now appear once in the supplier information block.

## 1.1.400.0 - 2026-07-26

- Fixed Supplier dashboard totals so they match the visible active supplier list.
- Made Invoice Checklist / Tasks vertically scrollable for longer service checklists.
- Normalized pasted TAN values before validation, including spaces and hyphens.
- Prevented duplicate HSN/SAC master codes and improved quotation duration wording for days and weeks.

## 1.1.336.0 - 2026-06-30

- Moved PDF import support from Quotations to Purchase Orders.
- Purchase Order imports now accept text-based PDF files, preserve PO numbers when present, and attach multiple parsed line items to the same purchase order.
- Removed PDF selection from other sales import pickers so Quotations return to Excel workbook import only.

## 1.1.335.0 - 2026-06-30

- Added PDF import support to the shared sales import flow, starting with text-based quotation PDFs.
- Extended quotation imports to extract readable PDF quotation numbers, dates, client names, subjects, totals, GST, and line items into native ServoERP quotations.
- Updated sales import pickers and folder import messaging so Quotations, Invoices, Payments, Purchases, and Jobs can select Excel or PDF files cleanly while unsupported PDFs fail with clear guidance.

## 1.1.334.0 - 2026-06-29

- Simplified the Quotations dashboard to only show Recent Quotations and the Quotation Value Sent chart.
- Added dashboard filters for company and 1, 2, 3, or 12 month ranges.
- Added smoke coverage for the quotation dashboard company filter.

## 1.1.333.0 - 2026-06-29

- Fixed Zydus-style MSE quotations whose item table uses `Quantity` instead of `Qty`, so all supplied Zydus quotation workbooks are recognized as native quotations.
- Widened imported quotation line descriptions to support long technical item specifications without skipped rows.
- Fixed India-style quotation date parsing and changed the quotation dashboard default range to all quotation history so imported batches appear immediately.

## 1.1.332.0 - 2026-06-29

- Added Master Data folder import so users can select or drop a folder of Excel workbooks and let ServoERP scan and import each supported file automatically.
- Added quotation folder import smoke coverage that imports multiple MSE quotation workbooks in one batch and verifies every saved quotation in SQL.

## 1.1.331.0 - 2026-06-29

- Fixed MSE quotation imports so label-only cells like `To,` are never saved as the client name; the parser now reads the real client block below the label.
- Added support for MSE quotation variants that include HSN/UOM columns and two-row BOQ supply/installation grids.
- Hardened the quotation import smoke test to verify the parsed client name and allow real small quotations with fewer item rows.

## 1.1.330.0 - 2026-06-29

- Published the MSE printable quotation workbook import support as a client update so uploaded quotation files become native ServoERP quotations with item lines and GST totals.

## 1.1.329.0 - 2026-06-29

- Added support for importing MSE printable quotation workbooks as native ServoERP quotations, including quotation number, date, client, subject, taxable total, GST total, total with GST, and item lines.
- Added a quotation import smoke switch to verify the real MSE quotation workbook format against SQL import/save behavior.

## 1.1.328.0 - 2026-06-28

- Added shared storage configuration for office-wide ServoERP files using `\\SERVERPC\ServoERPShared`.
- Routed backups, company templates, quotation documents, supplier reports, form-template working copies, and dashboard layout files through the shared storage root with local fallback if the network share is unavailable.

## 1.1.327.0 - 2026-06-28

- Fixed the GitHub desktop release workflow so reruns for an already-published tagged commit exit cleanly instead of failing as a duplicate release.
- Kept the release guard strict for new commits that reuse an already-published version, with clearer guidance to bump VERSION before shipping.
- Fixed Recent Suppliers status dropdowns so closed rows do not show blue selected-text highlights.
- Added a scrollable Recent Suppliers table area with the paging controls fixed below the card.
- Enlarged the Master Data Data Control Center hero card for clearer import workflow visibility.

## 1.1.325.0 - 2026-06-28

- Published the Suppliers duplicate-cleanup, global page refresh, larger Recent Suppliers card, and Invoices dashboard chart fixes as a client update.

## 1.1.324.0 - 2026-06-28

- Fixed the Invoices dashboard overview chart so it draws per-period invoice totals with period labels instead of an unlabeled flat series.
- Fixed the Invoices by Status donut so Paid, Pending, and Overdue segments and legend counts come from the same status aggregation.
- Added invoice dashboard chart binding diagnostics for overview totals and status bucket counts.

## 1.1.323.0 - 2026-06-28

- Added a visible Remove duplicates action on Suppliers so duplicate cleanup is available without waiting for a warning state.
- Added a global Refresh Page button to the main sidebar for reloading the current page from anywhere in the app.
- Enlarged the Suppliers dashboard Recent Suppliers card so recent supplier records are wider and easier to scan.

## 1.1.322.0 - 2026-06-28

- Reissued the Supplier-first import release as a new client update so machines and download pages advance past the previous 1.1.321 GitHub release.

## 1.1.321.0 - 2026-06-28

- Made Master Data uploads Supplier-first by showing Suppliers in import dialogs, previews, generated templates, and upload cards while keeping legacy Vendor Excel columns accepted.
- Changed procurement import templates to use SupplierName/SupplierID for supplier master, purchase, and supplier price-book uploads without renaming existing database fields.

## 1.1.320.0 - 2026-06-27

- Made ServoERP open even when SQL Server is unavailable by skipping SQL startup failures and entering a local operator shell session instead of blocking on the startup retry dialog.
- Disabled SQLite fallback status writes and offline business-entry queueing so failed SQL saves no longer get diverted into local SQLite.
- Updated support/setup wording from locked business entries to SQL availability, with database-backed actions using SQL only when the connection is reachable.

## 1.1.319.0 - 2026-06-27

- Removed remaining stale Vendors navigation helper routing so Vendor/Vendors shortcuts now open Suppliers.
- Rebuilt the client update after verifying the installed Velopack app and local launcher use the Suppliers-only navigation.

## 1.1.318.0 - 2026-06-26

- Removed the separate Vendors page from navigation and routed legacy Vendors shortcuts to Suppliers.
- Kept Suppliers as the only supplier management surface, with Recent Suppliers search verified against the full supplier set and without card flicker.

## 1.1.317.0 - 2026-06-26

- Relaxed suspicious save blockers so blank employee code/PAN, supplier bank details, AMC number/site, and contract type/status no longer stop normal saves.
- Changed duplicate client and supplier names to warnings instead of blocking errors, while keeping duplicate GSTIN as a hard stop.

## 1.1.316.0 - 2026-06-26

- Added the visible Employees Delete action to the page header with the shared confirmation flow, preserving employee history by marking the selected employee inactive.
- Made supplier bank details optional during supplier creation so users can save new suppliers without account number or IFSC, while still validating IFSC when it is entered.

## 1.1.315.0 - 2026-06-26

- Published the client database repair hotfix as an updater-visible release so client PCs run schema repair automatically on startup and can manually run Repair Database from Settings or Help & Support.

## 1.1.314.0 - 2026-06-26

- Fixed client machines entering ServoERP with a partially upgraded SQL schema by running the idempotent database migration at startup, preventing Screen Error popups on Add Supplier, New Purchase Order, New Quotation, and other schema-dependent actions after updates.
- Added a manual database schema repair action in Settings > System Tools and Help & Support > System Health so support can resolve missing-column and sync-migration issues without SQL commands.

## 1.1.313.0 - 2026-06-25

- Added Attendance trust signals with today/month counts, visible saved/unsaved state, save-and-reload verification, guarded period/site changes, close warning for unsaved edits, and month CSV export.

## 1.1.312.0 - 2026-06-25

- Added repeatable command-line smoke gates for CI-safe checks, save flows, AMC, Contracts, dashboard recents, purchase view buttons, invoice buttons, payment buttons, and job workflow validation.
- Replaced remaining raw confirmation prompts outside the shared deletion helper with ServoConfirmDialog for restore, update, runtime download, and job-opening confirmations.
- Aligned root and source app version configuration for the client update channel.
- Kept Release builds warning-free by cleaning stale Attendance label declarations.

## 1.1.311.0 - 2026-06-25

- Added an Employees dashboard status double-click action that toggles employee records between Active and Inactive.
- Persisted the status change through the Employees service/repository path so KPIs, filters, roster, workspace, and related employee surfaces stay synchronized.
- Restored the Employees dashboard Action column to its non-edit command display.

## 1.1.310.0 - 2026-06-25

- Added visible create/delete/deactivate actions across Payroll, Master Data, Service Desk, Attendance, AMC, Client, Vendor, Purchase, and Invoice workflows.
- Replaced misleading Payroll row action labels with real View/Edit/Deactivate and Stop Recovery behavior.
- Added guarded reset, cancel, deactivate, and delete confirmations for the new destructive or history-affecting actions.
- Fixed WhatsApp Hub header button alignment found during the full enterprise UI smoke test.
- Verified Release build, broad UI smoke, save-button smoke, AMC smoke, contracts smoke, and targeted temporary-row delete/deactivate checks for the client update.

## 1.1.309.0 - 2026-06-25

- Bumped the release candidate version so GitHub Actions can publish a new release after the manually published 1.1.308 build.

## 1.1.308.0 - 2026-06-25

- Fixed Contracts page recursive sidebar selection that could show a Screen Error while loading contracts or opening New Contract.
- Added a focused Contracts page smoke test covering dashboard load, New Contract, and existing contract row selection before release.

## 1.1.307.0 - 2026-06-25

- Fixed Contracts loading on older or empty client databases by self-healing the AMC contract schema before reads, saves, counts, and deletes.
- Hardened contract deletion cleanup so missing optional invoice, job, purchase, or SLA link columns do not block the Contracts page.

## 1.1.306.0 - 2026-06-25

- Added a visible Delete Contract action on the Contracts editor with confirmation, disabled state for unsaved contracts, and safe unlinking of related invoices, jobs, and purchase orders.
- Fixed the Contracts new/edit flow so opening a new contract no longer hangs during list refresh/selection sync.
- Fixed Jobs row action clipping and improved shared header search rendering on Jobs and Contracts.

## 1.1.305.0 - 2026-06-24

- Permanently removed the Tally page from navigation, page creation, QA catalogs, support module lists, language labels, and the desktop project compilation.

## 1.1.304.0 - 2026-06-24

- Renamed the Dispatch Center navigation/page surface to Site Monitor across the sidebar, page header, status messages, support text, language labels, and navigation smoke expectations.

## 1.1.303.0 - 2026-06-24

- Fixed the Inventory page freeze/open failure by suppressing automatic item-detail dialog launches during grid data binding, and widened the filter action area so Clear Filters and Refresh render cleanly.

## 1.1.302.0 - 2026-06-24

- Fixed the Inventory page open path by moving material catalog loading back onto the deferred page loader after the item details editor was moved into an on-demand dialog.

## 1.1.301.0 - 2026-06-24

- Moved Inventory item details out of the built-in page layout and into an on-demand Add/Edit Material dialog opened from the Add New Material button or material row selection.

## 1.1.300.0 - 2026-06-24

- Added full exception/detail drilldowns with CSV export to Quotation, Invoice, Payment, Purchase, and Main Dashboard cards, showing complete underlying rows instead of card previews only.

## 1.1.299.0 - 2026-06-24

- Added full exception/detail drilldowns to every Site Monitor card in Dispatch Center, with complete rows behind each KPI and section plus CSV export.

## 1.1.298.0 - 2026-06-24

- Redesigned the existing Dispatch Center page as a Site Monitor dashboard with site KPIs, operations tables, HVAC equipment summary, technician presence, SLA performance, revenue, and site health trend views while keeping the navigation entry unchanged.

## 1.1.297.0 - 2026-06-24

- Added full drilldown detail dialogs for Reports command center cards, including summary metrics, complete sortable data grids, and CSV export for each card.

## 1.1.296.0 - 2026-06-24

- Refined the Reports command center layout with compact one-line report library buttons and priority-based card sizing for larger financial/action cards and smaller supporting cards.

## 1.1.295.0 - 2026-06-24

- Replaced the Reports dashboard cards with a combined owner command center covering business health, receivables, sales pipeline, jobs, AMC contracts, purchases, inventory, clients, suppliers, payroll, service desk, and daily action queue signals.

## 1.1.294.0 - 2026-06-24

- Added an explicit scrollbar range to the sidebar so all navigation groups remain reachable on smaller screens.

## 1.1.293.0 - 2026-06-24

- Restored Attendance as its own HR & Payroll sidebar page while keeping Payroll Dashboard as the payroll landing view.

## 1.1.292.0 - 2026-06-24

- Restored Payroll Dashboard as the HR & Payroll navigation entry and landing view.

## 1.1.291.0 - 2026-06-24

- Fixed shared grid theming so Attendance frozen columns cannot inherit or receive Fill autosizing during page navigation.

## 1.1.259.0 - 2026-06-24

- Published the Attendance and Quotations page-load fixes above the latest `1.1.258` client release so desktops can detect the next update.
- Fixed the Attendance page crash caused by inherited DataGridView fill autosizing on hidden/non-fill columns.
- Added a guarded quotation schema repair path so the Quotations page opens even when the customer database is missing the `Quotations` table.

## 1.1.228.0 - 2026-06-24

- Fixed the separate Attendance page so legacy `EmployeeAttendance` rows are shown when matching payroll `AttendanceRecords` rows do not exist yet.
- Updated the Attendance reconciliation banner to report only legacy rows that still need saving into the payroll attendance table.
- Removed hardcoded June/2026 placeholder text from the Attendance workspace.

## 1.1.199.0 - 2026-06-24

- Restored Attendance as its own HR & Payroll sidebar page between Employees and Payroll, opening the Attendance workspace directly.
- Added Attendance to HR/Payroll license/module entitlements so existing client licenses no longer hide the separate sidebar page.

## 1.1.170.0 - 2026-06-23

- Updated the AMC Equipment add dialog so Equipment Name is also an editable dropdown populated from active inventory item/material names.

## 1.1.169.0 - 2026-06-23

- Updated the AMC Add/Edit form so Equipment Covered is an editable dropdown populated from the active inventory item/material list while preserving existing custom equipment descriptions.

## 1.1.168.0 - 2026-06-23

- Redesigned the AMC dashboard from dense contract cards into a full-width operational contract grid with renewal status, days-left, value, visits, next-service, and one-click open actions.
- Added clickable AMC KPI quick filters plus a renewal-window filter so active, expired, and upcoming contracts can be scanned faster.
- Wired the AMC detail `Edit AMC` action to the existing edit form instead of the placeholder message, and rebuilt Release with visual smoke checks for dashboard filtering, detail open, and edit launch.

## 1.1.167.0 - 2026-06-23

- Added a generic Master Lookup system so HR, attendance, payroll, jobs, service desk, AMC, inventory, purchase, and sales document reference values can be managed centrally.
- Added Master Data reference cards for the missing lookup categories, with each card showing active value counts and opening the correct editable lookup category.
- Migrated key module dropdowns to the shared lookup service with safe fallbacks and rebuilt Release for client update rollout.

## 1.1.166.0 - 2026-06-23

- Published the next ServoERP desktop update package for client rollout through GitHub Releases.
- Rebuilt the Release desktop app so client PCs can detect and download the newer Velopack release.

## 1.1.165.0 - 2026-06-23

- Redesigned the Payroll Operations dashboard content into a compact close cockpit with period controls, KPI cards, command-center blockers, exception queues, and control signals aligned to the supplied modern payroll reference.
- Replaced the oversized payroll chart area with a compact horizontal payroll quality pulse while preserving the existing payroll data refresh, run, lock, import, forms, statutory, salary structure, and employee detail flows.
- Redesigned the Payroll `Salary Structures` and `Employee Details` tabs with modern employee pickers, selected-employee summary cards, compact salary editors, refreshed history/detail tables, and preserved existing payroll bindings and handlers.
- Added a shared Lucide SVG icon service that loads icons from the local Lucide pack, renders cached WinForms bitmaps, and routes content-area shared icon buttons through the new provider with safe fallback drawing.
- Restored Attendance as its own HR & Payroll page between Employees and Payroll, opening the existing Attendance workspace directly.
- Polished Payroll `Salary Structures` and `Employee Details` with tighter employee rails, cleaner summary cards, refined table spacing, aligned currency columns, compact action columns, and a stronger salary gross total band.
- Rebuilt Release successfully and visually checked the Payroll content surface with a rendered WinForms capture; the sidebar was not modified.

## 1.1.164.0 - 2026-06-23

- Restyled the Employees first-layer dashboard to closely follow the supplied reference with a colored KPI band, a flatter readiness pulse row, a more structured follow-up queue, and a tighter roster action bar.
- Strengthened the roster table presentation so live presence and readiness states stand out more clearly at a glance from the dashboard.
- Rebuilt Release successfully and visually rechecked the updated Employees dashboard from the rendered window capture.

## 1.1.163.0 - 2026-06-23

- Simplified the Employees first-layer dashboard to match the cleaner roster-led direction by removing the oversized hero control block and starting the page directly with workforce KPI signals.
- Moved workspace actions into the `Roster launchpad` area so the employee table becomes the main decision surface while keeping the second-layer employee workspace one click away.
- Rebuilt Release successfully and visually rechecked the final Employees dashboard against the cleaner reference layout.

## 1.1.162.0 - 2026-06-23

- Redesigned the Employees first-layer dashboard into a more professional HR operations command center with a stronger hero section, executive workforce signal cards, cleaner deployment and follow-up summaries, and a roster launchpad that leads into the detailed employee workspace.
- Kept the second-layer employee workspace flow intact while making the dashboard actions explicit on-screen through `Open Employee Workspace`, `Add New Employee`, and payroll entry points.
- Rebuilt Release successfully and visually rechecked the rendered Employees dashboard after the final layout pass.

## 1.1.161.0 - 2026-06-23

- Reworked the Employees module so `HR & Workforce` now opens on a first-layer employee dashboard instead of dropping users straight into the detailed employee workspace.
- Added a dashboard-led flow with new workforce cards, readiness and attention summaries, roster preview, and a clear `Open Employee Workspace` action that takes users into the existing profile/work/compliance/pay screen as the second layer.
- Rebuilt Release successfully for the new Employees dashboard-to-workspace flow.

## 1.1.160.0 - 2026-06-23

- Reworked the Settings workspace card reflow so normal desktop widths use a roomier two-column layout, while three columns are reserved for genuinely wide screens.
- This broader Settings card polish gives the control center, company, update, HSN/SAC, unit, system tools, backup, and admin cards more readable widths and calmer spacing across the page.
- Rebuilt Release successfully and visually rechecked both the top and lower Settings card regions from the fresh desktop binary.

## 1.1.159.0 - 2026-06-23

- Fixed the Settings `Unit Management` card so the add-unit form, aliases area, action buttons, and units grid fit cleanly inside the card instead of feeling clipped and cramped.
- Added a more responsive unit-card layout for narrower widths, improved aliases entry space, and clarified the unit list section heading and column labels.
- Rebuilt Release successfully and visually rechecked the updated Settings unit card from the fresh desktop binary.

## 1.1.158.0 - 2026-06-23

- Reworked the Settings page into a more guided admin workspace with a new `Settings Control Center`, faster action shortcuts, and clearer release, database, backup, license, and assistant health cues.
- Refreshed the Tally workspace header and connection overview so export, import, inventory sync, and audit actions feel more task-driven while long connection values stay readable in the summary strip.
- Rebuilt Release successfully and visually rechecked the shipped app plus the hosted Settings and Tally pages against the fresh binary.

## 1.1.157.0 - 2026-06-22

- Reworked the Employees module into a clearer `HR & Workforce` workspace with dedicated `Profile`, `Work`, `Compliance`, and `Pay` tabs instead of forcing HR users to bounce between fragmented employee sub-pages.
- Added a richer employee hero, workforce scan metrics, readiness cues, and grouped work/compliance/pay flows so HR can spot blockers and move into action faster from one screen.
- Rebuilt Release successfully and visually verified the updated HR workspace tabs in the desktop app.

## 1.1.156.0 - 2026-06-22

- Reworked the Payroll dashboard so the vague `who?` detail lines are replaced by dedicated people-focused roster panels for attendance gaps, salary blockers, late or absent employees, payslip follow-up, and live field availability.
- Removed lower-signal Payroll dashboard cards that duplicated chart insight without helping HR or accounts decide who needs action next.
- Rebuilt Release successfully for the Payroll dashboard roster-view cleanup.

## 1.1.155.0 - 2026-06-22

- Fixed the Payroll dashboard crash that still occurred after navigation when WinForms chart controls were temporarily laid out at `0px` width during tab creation.
- Wrapped the Payroll dashboard charts in a guarded chart host so early layout passes cannot throw `Width must be greater than 0px` while the page is still sizing.
- Rebuilt Release successfully for the Payroll chart-layout fix.

## 1.1.154.0 - 2026-06-22

- Fixed the Payroll screen opening failure that could leave the page blank and show the generic `This action is not available in the current state` dialog during navigation.
- Hardened deferred page polish scheduling so heavy module pages no longer rely on an unsafe pre-handle `BeginInvoke` timing path while the shell is still creating the page.
- Rebuilt Release successfully for the Payroll navigation fix.

## 1.1.153.0 - 2026-06-22

- Simplified the Payroll dashboard by removing low-value cards so the page focuses on the decisions HR and accounts actually need during review and close.
- Reworked the remaining dashboard cards to identify affected employees, crews, or statutory items directly instead of showing vague states that leave users asking who needs action.
- Rebuilt Release successfully and visually rechecked the updated Payroll dashboard after the card cleanup.


## 1.1.152.0 - 2026-06-22

- Added visual charts to the Payroll dashboard so HR and accounts can read compliance pulse, payroll blockers, field capacity mix, and coverage-versus-load through graphs instead of relying only on cards and raw figures.
- Kept the interpreted card layer in place while adding chart cards beneath the section headers, creating a better balance between quick judgment states and visual trend scanning.
- Rebuilt Release successfully and visually verified the dashboard with the new chart band rendered in the proof harness.

## 1.1.151.0 - 2026-06-22

- Reworked the Payroll dashboard card language so most operational and compliance tiles now speak in interpreted states such as readiness, risk, pressure, coverage, and balance instead of showing raw counts as the primary message.
- Kept the supporting detail lines underneath each card so HR and accounts can still inspect the underlying employee counts, due dates, and hours when needed without losing the quick-read interpretation layer.
- Rebuilt Release successfully after the dashboard interpretation update.

## 1.1.150.0 - 2026-06-22

- Wired the new Payroll dashboard compliance and field-availability cards to live `EmployeeAttendance`, payroll `AttendanceRecords`, employee-status, and open-job data instead of leaving them as visual-only placeholders.
- Added a future-ready fallback path so the dashboard automatically prefers richer check-in/check-out data when it exists, but still shows usable metrics from payroll attendance when live field data is incomplete.
- Rebuilt Release successfully and visually rechecked the dashboard after the data-bridging changes.

## 1.1.149.0 - 2026-06-22

- Expanded the Payroll dashboard with dedicated `Schedule Compliance` and `Field Availability & Deployment` sections so HR and accounts can monitor punctuality, absenteeism, leave load, overtime exposure, live crew availability, deployment utilization, open field jobs, and attendance coverage from one place.
- Kept the dashboard content-fitting behavior while separating payroll close controls from HR operations signals, making the page easier to scan without oversized empty cards.
- Rebuilt Release successfully and visually verified the updated Payroll dashboard render with the new metric bands.

## 1.1.148.0 - 2026-06-22

- Refined the Payroll dashboard layout so the workflow cards, KPI row, accountant cards, and guidance panel size closer to their actual content instead of relying on oversized fixed heights.
- Kept the separate Payroll dashboard structure while making the new overview feel tighter and more natural on screen.
- Rebuilt Release and visually rechecked the updated Payroll dashboard sizing.

## 1.1.147.0 - 2026-06-22

- Split Payroll into clearer sub-pages with a dedicated `Dashboard` tab, a focused `Run Payroll` workspace, plus separate `Salary Structures`, `Attendance`, `Statutory Payments`, and `Employee Details` tabs.
- Moved the accountant workflow checklist, month-readiness KPIs, and payroll blocker cards out of the run grid and into the new dashboard so review and execution are no longer mixed on one page.
- Rebuilt Release successfully and visually verified the new Payroll tab structure with a rendered proof capture.

## 1.1.146.0 - 2026-06-22

- Added an accountant-focused dashboard band to Payroll with at-a-glance cards for payroll status, employees in payroll, attendance exceptions, salary setup gaps, pending statutory items, pending payslips, recovery totals, and the next due date.
- Wired those cards to live month data so accountants can spot blocked payroll runs and month-close risk without opening each sub-tab first.
- Source changes are ready for release validation; the current environment is blocking overwrite of the running `bin\Release\HVAC_Pro_Desktop.exe` until the open ServoERP process is closed.

## 1.1.145.0 - 2026-06-22

- Reworked the Employees page for HR operators with a new `HR action queue`, direct `Needs Action` visibility in the employee list, and selected-employee readiness guidance for KYC, emergency contact, probation, site assignment, and salary setup gaps.
- Reworked the Payroll page for accountants with a new month-close checklist, stronger month status messaging, employee-context summaries on salary/details views, and attendance exception guidance for blank, leave, half-day, and absent entries.
- Rebuilt the Release desktop executable and visually verified the updated Payroll workspace render after fixing the new attendance-grid runtime regression in the shipped build.

## 1.1.144.0 - 2026-06-22

- Expanded Payroll import to support real-world attendance workbooks that use employee-block monthly status layouts like `WorkDurationReport - 01-05-26.xls`, including day headers such as `1 F` and per-employee `Employee:` / `Status` sections.
- Hardened Payroll attendance imports for register-style monthly sheets like `02-Attendance & Payroll_ May 26.xls` by recognizing vendor status codes such as `W`, `WO`, `H`, `PH`, `WHO`, `C`, `E`, and `P/2`.
- Rebuilt Release and validated the compiled importer in read-only checks against both supplied Deccan attendance/payroll workbook formats before any live database import.

## 1.1.143.0 - 2026-06-22

- Added direct Payroll import actions for selected `.xls`, `.xlsx`, and `.csv` payroll or attendance files instead of relying only on the historical source folder or CSV-only attendance upload.
- Hardened payroll import detection so sheets are identified from real header content as payroll, attendance, or employee master layouts even when vendors use different tab names or mixed workbook structures.
- Rebuilt the Release desktop executable and verified the new Payroll import flow is available from the page header and Attendance tab in the shipped app.

## 1.1.142.0 - 2026-06-22

- Fixed sales tax invoice Excel imports that were failing during preflight with `ExecuteReader requires the command to have a transaction` when ServoERP opened a SQL transaction for the import batch.
- Updated import preflight lookups to join the active SQL transaction, so invoice and other guarded import flows can safely validate reference data before committing rows.
- Rebuilt Release and re-tested invoice import with transaction-enabled preflight against the live SQL database to confirm the crash is gone.

## 1.1.141.0 - 2026-06-22

- Fixed Master Data import actions so stale page-refresh state no longer throws the generic `This action is not available in the current state` screen error when opening Import Excel menus or starting import flows.
- Hardened Master Data import and template actions to verify the live control/form owner before showing popup menus or file dialogs, reducing WinForms invalid-state crashes during page refreshes.
- Rebuilt the Release desktop executable so this import-state fix is ready for client rollout as a proper update.

## 1.1.140.0 - 2026-06-22

- Fixed Dashboard recent quotation `View` so it no longer opens the wrong quotation after list rebinding or deferred page initialization.
- Hardened quotation recent-activity navigation state so dashboard-triggered quotation opens keep the intended selected record through the quotations page lazy-load flow.
- Added clean AMC save validation for duplicate AMC numbers and real database column-length limits so over-length AMC numbers are rejected before SQL truncation errors can occur.
- Updated AMC smoke coverage to exercise the actual Add AMC save path instead of raw SQL writes, matching the real client workflow more closely.

## 1.1.139.0 - 2026-06-21

- Audited the requested module pages for duplicate header implementations and moved live `WhatsApp Hub` and `Tally Integration` headers onto the shared page-header primitive while keeping their existing actions.
- Replaced the leftover bespoke quotation-dashboard header implementation with the shared header pattern so the quotations module no longer carries a dead parallel top-bar implementation.
- Removed duplicate page-level dependency creation in `InvoiceForm`, `TenderBidForm`, `JobManagementForm`, and `ContractManagementForm` by reusing the services and repositories each page already owns.

## 1.1.138.0 - 2026-06-21

- Fixed duplicated header action rails so shared page headers no longer get an extra auto-injected `Preview` or `Refresh` cluster layered on top of their own page-specific controls.
- Corrected clipped and inconsistent top-bar actions across Quotations, Invoices, Purchases, Payments, Reports, and Settings so search boxes and header buttons render fully inside the real sidebar-constrained app shell.
- Rebuilt and visually rechecked the Release desktop app against the shipped `bin\Release` output so this update packages the actual on-screen header fixes instead of the stale duplicated layout.

## 1.1.137.0 - 2026-06-21

- Added stronger dashboard recent-activity behavior so newly saved quotations, invoices, jobs, and purchase orders are surfaced more reliably with direct `View` actions from the recent cards.
- Refined dashboard and purchase UI polish by repositioning and resizing action cards and buttons, including the `Backup Now` control and the recent purchase-order card layout.
- Reworked purchase-order viewing so recent `View` actions open the proper in-app purchase preview window with a cleaner, more professional document layout instead of falling back to poorly formatted file-open behavior.

## 1.1.136.0 - 2026-06-21

- Fixed Invoice `New Invoice` so the editor opens promptly instead of appearing stuck while background invoice data is still loading.
- Restored immediate client loading in the new-invoice form and hardened invoice editor data binding so client/template dropdowns populate reliably.
- Improved invoice editor usability by fitting the invoice-items section more responsively, keeping amount-in-words aligned with `Grand Total` in generated PDFs, and rendering the item-description dropdown list on a white background.

## 1.1.135.0 - 2026-06-21

- Hardened startup and SQL connection handling so the desktop app is more reliable on office machines using the shared `.\SQLEXPRESS` business database.
- Standardized shared unit normalization and import preflight behavior across inventory, purchase, quotation, invoice, and job-related flows to reduce inconsistent saved data and brittle imports.
- Strengthened unattended smoke coverage, pagination responsiveness, and visual verification workflows so release builds are easier to validate before shipping.

## 1.1.134.0 - 2026-06-19

- Fixed the Inventory item-details layout so supplier-rate actions no longer disappear below the visible panel area on client-sized screens.
- Added a visible `Manage Supplier Rates` action inside the supplier price book section so users can always add, remove, and mark preferred supplier rows even when the inline panel is tight.
- Rebuilt the Release desktop app and visually rechecked the updated Inventory details panel after the layout fix.

## 1.1.133.0 - 2026-06-19

- Added a multi-supplier price book to item details so each material can now store multiple vendor-rate combinations instead of only one supplier mapping.
- Kept backward compatibility by syncing one preferred supplier back to the existing material record while using the saved price book for supplier comparison and recommendation flows.
- Practically verified the new supplier-price source against the local SQL-backed app logic by inserting temporary supplier price rows, confirming comparison results, and restoring the original data.

## 1.1.132.0 - 2026-06-19

- Fixed Desktop-installer config precedence so ServoERP now prefers the established `C:\HVAC_PRO_MSE\HVACPro.config` when it exists, instead of accidentally drifting to a packaged app-local config after an update.
- Reduced false “inventory became zero” confusion by auto-selecting the first loaded material row so the item detail panel shows a real current quantity instead of staying on the empty `0.00` state after load.
- Prepared this release as an inventory recovery rollout for clients moving from the older install channel to the Desktop update channel.

## 1.1.131.0 - 2026-06-19

- Improved supplier-rate handling for inventory and purchase flows so missing supplier price history no longer feels like a failed fetch.
- When no saved supplier rate exists for a material, ServoERP now shows a professional in-app notice that supplier and price details are not yet available and the rate can be entered manually.
- Updated supplier comparison empty states to use the same clearer wording for missing supplier-price data.

## 1.1.130.0 - 2026-06-19

- Standardized the shared PDF and HTML document branding fallback so company headers default to `Madhusuman Enterprises` instead of placeholder or app-name text when company settings are blank.
- Upgraded the Jobs preview document surface to use the official top letterhead and a cleaner quotation-style service-report layout for client-facing previews.
- Rebuilt the Release desktop app for client distribution and revalidated startup on the shipped Release executable.

## 1.1.129.0 - 2026-06-18

- Fixed Desktop update detection for older/manual installs such as `1.1.91` that are not running as Velopack-managed app shortcuts.
- When ServoERP cannot apply an in-place package update, it now checks the latest GitHub release, reports the newer version correctly, and offers to open the latest installer instead of leaving the client apparently stuck.
- Release build completed successfully and the updater fallback path was rebuilt for distribution.

## 1.1.128.0 - 2026-06-18

- Reframed Inventory around job-based procurement so the client UI now reflects on-demand material buying instead of warehouse-first replenishment.
- Updated Materials / Procurement, job material guidance, dashboard summaries, reports, and import descriptions to use procurement-ready, supplier-ready, and direct-purchase language that fits the actual operating model.
- Rebuilt the Release desktop package and prepared a fresh GitHub Releases update so client desktops can receive the new procurement-first experience through the updater.

## 1.1.107.0 - 2026-06-18

- Superseded the numerically older `1.1.98` updater package with a new Velopack release above the already-published `1.1.106` feed version so client desktops can detect the update correctly.
- Preserved the units-of-measure master rollout and packaging fix in a release number the auto-updater will actually accept.

## 1.1.98.0 - 2026-06-18

- Fixed release packaging/version alignment so GitHub Releases and Velopack update packages now advertise the same shipped app version to client installs.
- Republished the units-of-measure master rollout through a valid updater package so existing client desktops can actually detect and receive the release.

## 1.1.97.0 - 2026-06-18

- Rewired every audited unit-of-measure picker in Inventory, Purchase, Invoice, Tender/Quotation, and supplier-comparison flows to load from the shared `UnitMeasurements` master instead of local hardcoded fallback lists.
- Expanded the seeded unit master with grouped HVAC-friendly coverage across length, area, volume, mass, pressure, temperature, energy, electrical, airflow, refrigerant, concentration, packaging, run-length, time, service billing, and consumable-dispensing units, including short codes and measurement-system metadata.
- Standardized unit normalization so legacy values such as `Nos`, `Mtr`, running-meter aliases, and symbol-based inputs like `%` resolve back to canonical master-table units while dropdowns display `full name (short_code)` consistently.
- Release build completed successfully and the desktop executable startup path was smoke-tested from the built Release output.

## 1.1.96.0 - 2026-06-18

- Added quotation-to-purchase conversion with approved-quotation safeguards, automatic supplier carry-forward, and generated purchase-order lines based on quoted material items.
- Upgraded supplier recommendation and price comparison flows to rank vendors using purchase history, lead-time and fulfilment signals, stock coverage hints, and weighted supplier scoring instead of rate alone.
- Enhanced purchase entry guidance with automatic best-supplier application, visible supplier rate-drift warnings, and a richer supplier comparison dialog for material buying decisions.
- Added supplier performance scorecards and exportable PDF reporting so vendor pages can show purchase value, delivery reliability, fulfilment quality, and 12-month item price history.
- Refined inventory and dashboard workflow polish with stronger inventory filtering/search controls, category filters, and cleaned-up dashboard top-bar/search presentation.

## 1.1.95.0 - 2026-06-17

- Restored shared supplier recommendation logic so material pricing can rank vendors using lowest mapped rate plus preferred material vendor mapping, exact material match, saved supplier-price freshness, and vendor purchase history.
- Auto-applied the recommended supplier path in purchase-order material selection, quotation analysis, job material costing hints, and invoice material rate defaults so vendor-guided costing behaves consistently across procurement and execution workflows.

## 1.1.94.0 - 2026-06-17

- Moved the dashboard `Alerts & Notifications` card above `Quick create` so operational alerts appear earlier in the landing-page flow.

## 1.1.93.0 - 2026-06-17

- Standardized the visible inside text for search inputs across the app so search boxes now show only `Search`.
- Updated dashboard, list, tender item, payments, purchasing, vendor, WhatsApp, client, contract, AMC, and global search entry points to preserve existing filtering behavior with the simplified text.

## 1.1.92.0 - 2026-06-17

- Added a visible `Preview` action inside the invoice creation header so invoice drafting keeps preview access in the editor itself.
- Fixed the default invoice-items row state so the row under `Description` renders white instead of staying blue-selected when opening a new invoice.
- Rechecked both invoice fixes visually in the built desktop app before shipping this release.

## 1.1.91.0 - 2026-06-17

- Fixed Operations module header regressions so Clients, Suppliers, Vendors, Jobs, Dispatch Center, Inventory, and AMC now use consistent action spacing and stable top-right header controls.
- Verified the shipped desktop build visually across the Operations pages so the client update includes the corrected header layout instead of the stale overlapping controls.

## 1.1.90.0 - 2026-06-16

- Fixed AMC navigation so reopening the AMC module restores the dashboard instead of leaving the page blank after detail navigation.
- Fixed Invoice preview so the Preview action now opens a safe draft preview even when the form is still partially filled.

## 1.1.89.0 - 2026-06-16

- Reduced page-load contention by staging app warmup work instead of front-loading heavy cache population during startup and first navigation.
- Optimized supplier summary aggregation and supplier page selection rendering to cut repeated vendor purchase-order work and improve Suppliers navigation responsiveness.
- Deferred lower-priority Settings cards, cached invoice checklist suggestions, and parallelized report loading to improve first-open performance across slow modules.

## 1.1.88.0 - 2026-06-16

- Added shared header `Refresh` and `Preview` actions across dashboards, pages, and supported forms so key actions stay in each screen header instead of the sidebar.
- Wired the global preview action to use existing module preview flows where available, with a visual fallback preview for screens that do not yet have a dedicated document preview.

## 1.1.87.0 - 2026-06-16

- Moved the shared page refresh action out of the sidebar and into page headers so module refresh stays near each page title and actions.
- Added a visible local `Refresh` button to the Materials / Procurement page so the unit/materials screen can reload directly from its own header actions.

## 1.1.86.0 - 2026-06-16

- Added a shared shell-level `Refresh Page` button plus `F5` support so users can reload the active module from anywhere in the app.
- Wired the global refresh action through the page cache so each module reopens with a fresh instance instead of relying on inconsistent local refresh logic.

## 1.1.85.0 - 2026-06-16

- Standardized generated PDF branding so Madhusuman Enterprises company-registration details now flow through shared PDF rendering for quotations, invoices, purchase orders, payroll PDFs, job reports, and QuestPDF-generated documents.
- Replaced remaining placeholder company text in PDF outputs with the official Madhusuman Enterprises identity and compliance details.

## 1.1.84.0 - 2026-06-15

- Renamed the Invoice editor primary action from `Save Draft` to `Save` so the workflow matches normal invoice entry.
- Updated invoice save defaults so new saves land as invoices with `Pending` status instead of being treated as drafts by default.

## 1.1.83.0 - 2026-06-15

- Fixed Jobs navigation so returning from New Job to the Jobs Dashboard renders immediately instead of showing a blank dashboard or stalling on a blocking reload.
- Added focused smoke coverage plus an explicit one-line smoke report entry for the Jobs `New Job -> Back to Dashboard` return path.

## 1.1.82.0 - 2026-06-15

- Improved Client Detail lifecycle actions so clicking the current status does nothing and status changes use the shared ServoERP confirmation dialog.
- Added required-field cues and validation to Job Detail for job title and client selection.
- Added a clear Job Detail client dropdown placeholder to avoid silent default selection.

## 1.1.81.0 - 2026-06-12

- Standardized shared logging through a central ServoERP log spine so UI, diagnostics, and legacy error paths all land in `C:\HVAC_PRO_MSE\LOGS`.
- Added shared FluentValidation guards plus inventory item validation so client, vendor, employee, and inventory saves follow one validation path.
- Introduced shared async safe-execution helpers on ServoERP base pages/forms and applied them to client and inventory flows for more consistent error handling.

## 1.1.80.0 - 2026-06-12

- Replaced remaining UI `BackgroundWorker` flows with task-based loading and save paths across Settings, Master Data, Inventory, Quotations, WhatsApp Hub, AMC, Dashboard, Backup, and setup screens.
- Fixed modal update/navigation debt by converting the Payroll payslip progress flow to `ShowDialog()` behavior and aligning several supporting forms with ServoERP base form/page infrastructure.
- Hardened release-readiness by logging previously silent failures, replacing swallow-heavy client/invoice data mapping with defensive field readers, and adding guarded performance indexes for payments, quotations, client sites, and technician scheduling queries.

## 1.1.71.0 - 2026-06-12

- Lengthened the Jobs dashboard All Jobs card and priority queue area so more job rows are visible at once with less cramped scrolling.

## 1.1.70.0 - 2026-06-12

- Fixed the Jobs dashboard All Jobs priority-queue card so populated job rows no longer clip their text, pills, or open action button during normal dashboard rendering.

## 1.1.69.0 - 2026-06-12

- Redesigned the Jobs dashboard All Jobs card with a clearer operations summary band, cleaner priority-queue job rows, stronger status and priority pills, and a more readable one-click open flow.

## 1.1.68.0 - 2026-06-12

- Fixed Jobs material entry so users can add materials while creating a new job, complete the form, and save once at the end instead of being forced to save the job before adding materials.

## 1.1.67.0 - 2026-06-12

- Added a local SQLite offline sync queue for client, job, job material, invoice, payment, and inventory-impacting save actions when office SQL Server is unavailable.
- Added automatic replay of pending offline saves when SQL Server reconnects, with validation conflicts retained for review instead of repeatedly throwing screen errors.
- Updated affected forms to treat negative local IDs as pending-sync records so users can continue working without modal database-error loops.
## 1.1.67.0 - 2026-06-12

- Fixed Client Detail status actions so lifecycle buttons remain reachable on narrower layouts.
- Moved client site removal and job closing to the ServoERP destructive confirmation dialog.
- Improved the shared confirmation dialog layout so longer action/detail messages do not clip.

## 1.1.66.0 - 2026-06-12

- Reduced repeated Screen Error popups during SQL Server outages by routing database failures to a throttled Database Connection warning and the existing connection status banner.

## 1.1.65.0 - 2026-06-12

- Added a bottom horizontal scrollbar to the New Invoice editor so wide invoice fields and totals remain reachable on smaller client screens.

## 1.1.64.0 - 2026-06-12

- Fixed Contracts dashboard `All Time` and `All Status` filters so refresh/pagination updates no longer retrigger flickering combo-box refreshes.
- Fixed the Contracts `Active` status filter so active contracts that display as expiring soon are still included correctly.

## 1.1.55.0 - 2026-06-12

- Added clear Material, Qty, Rate, and Action headings to the Jobs material entry row.

## 1.1.47.0 - 2026-06-12

- Fixed Jobs material Add so selected materials with low or zero stock still add to the job as out-of-stock planning rows without posting negative inventory.
- Improved job material add feedback for low/out-of-stock rows and preserved global material rate updates from the Rate field.
- Fixed Contracts Back to Dashboard so returning from the contract form rebuilds and refreshes the dashboard instead of showing blank cards/tables.
- Added explicit smoke coverage for enabled Add/Save buttons so critical actions must be wired before release.

## 1.1.40.0 - 2026-06-12

- Fixed job material entry so selecting a material reliably adds it to the saved job, refreshes stock, and shows the added part in job details.
- Added an editable Rate field beside job material quantity; changing it while adding a selected material updates that material's global purchase rate.
- Added smoke coverage for job material add plus global material-rate persistence.
- Fixed report library tile sizing and hardened global card-menu clipboard audit behavior so `/smoketest` completes reliably.

## 1.1.34.0 - 2026-06-12

- Made `RMT` available through the shared unit catalog and all major material/line-item unit selectors, including Inventory, Purchases, Invoices, and Quotations.
- Added shared unit aliases for running meter, square meter, and box-style units so imports and saved rows normalize consistently.
- Fixed the Contracts workspace so contracts, clients, and sites load as one background snapshot instead of repeatedly querying SQL during dashboard rendering.
- Hardened contract reads for older client databases by repairing missing additive contract columns before loading the dashboard.

## 1.1.30.0 - 2026-06-12

- Fixed the Suppliers and Vendors workspace so the dashboard and grid reload correctly when reopened from navigation instead of reusing stale cached page state.
- Fixed supplier dashboard loading to use a lightweight full master-data read, preventing SQL timeout fallbacks that could show `0` suppliers even when supplier records existed.
- Cleared vendor-related cache entries after Vendor and Purchase imports so newly imported suppliers appear without waiting for cache expiry.

## 1.1.29.0 - 2026-06-11

- Improved first-open speed for dashboard, master data, reports, employee, purchase, payment, tender, AMC, WhatsApp, and other heavy pages by deferring non-critical polish and startup data refreshes until after the page is visible.
- Added short-lived shared data caching for repeated dashboard/reference reads so switching between ERP modules avoids duplicate database work.
- Fixed the Modern ERP combo box constructor crash caused by an invalid autocomplete/dropdown-style combination.

## 1.1.28.0 - 2026-06-11

- Added a shared ServoERP modal form shell for small view, setup, edit, and action dialogs.
- Migrated client, inventory, service desk, and SLA popups to the shared modal shell so clicked View/Details dialogs inherit the global theme and input polish.

## 1.1.27.0 - 2026-06-11

- Fixed the Inventory item details card focus-outline bug that could paint the entire card as an input field.
- Widened and inset the Inventory item details labels, dropdowns, numeric fields, and section bars so they no longer render cramped against the card edge.

## 1.1.26.0 - 2026-06-11

- Removed the duplicated inner `ITEM DETAILS` heading from the Inventory item details card so the form fields no longer overlap the card title.

## 1.1.25.0 - 2026-06-11

- Removed the visible prepared-message text block from the WhatsApp Hub composer while preserving message prefill internally.
- Reworked the WhatsApp Hub composer into compact wired actions for template selection, document reference, phone status, send, mark sent, copy message, and quick templates.

## 1.1.24.0 - 2026-06-11

- Redesigned WhatsApp Hub into a connected workspace with embedded WhatsApp Web as the main chat surface.
- Wired WhatsApp Hub filters, chat settings, browser fallback, call action, related-record hint, message copy, and contact edit guidance.
- Improved the WhatsApp composer with selected template and contact context while keeping manual-send safeguards.

## 1.1.23.0 - 2026-06-11

- Removed stray trailing literal `v` characters from dashboard, purchase, payment, vendor, tender, client, job, and login button/filter labels.

## 1.1.22.0 - 2026-06-11

- Added a reusable supplier price comparison dialog that ranks material suppliers from saved supplier prices and purchase-history fallback data.
- Wired supplier comparison into purchase order line items so users can compare suppliers, apply the selected rate, unit, and supplier directly to the PO.
- Wired supplier comparison into inventory purchase requests so the best known supplier/rate is prefilled and can be reviewed before creating a draft request.

## 1.1.21.0 - 2026-06-11

- Redesigned the dashboard search bar with a rounded white container, aligned search icon, cleaner placeholder text, and a polished Ctrl+K keycap.

## 1.1.20.0 - 2026-06-11

- Redesigned the dashboard notification button as a compact bell icon with an inset unread badge so the badge no longer clips above the button.

## 1.1.19.0 - 2026-06-11

- Added a dedicated always-visible right-side scrollbar inside the Alerts & Notifications table and wired it to move between notification pages.

## 1.1.18.0 - 2026-06-11

- Added an internal vertical scrollbar to the Alerts & Notifications table so notification rows scroll inside the notification center.

## 1.1.17.0 - 2026-06-11

- Redesigned the global Alerts & Notifications dialog into a table view with priority pills, category/reference/details/date columns, pagination, and Refresh/Dismiss/Open actions.
- Added smoke coverage for the notification center table and dashboard notification entry point.

## 1.1.16.0 - 2026-06-11

- Added a global Alerts & Notifications center for operational exceptions across invoices, payments, purchases, inventory, quotations, jobs, technicians, AMC contracts, service desk SLA, clients, vendors, employee compliance, and backup health.
- Added a dashboard notification icon with active-count badge and made the dashboard alerts strip open the notification center.
- Added smoke coverage to keep the dashboard notification entry point accessible.

## 1.1.14.1 - 2026-06-11

- Restored Payroll to open the full salary processing workspace immediately, including month controls, payroll actions, KPI cards, tabs, and register grid.
- Improved Global Card Context Menu clipboard actions so Copy as Path, Share, Cut, and Copy write reliably during UI smoke runs and normal use.
- Fixed Invoice dropdowns so customer, GST, item, unit, status, template, and line-item ComboBoxes no longer close immediately from focus stealing, overlay labels, or recalculation/rebinding during dropdown interaction.
- Reworked the New Quotation Quote Details card into a cleaner capped 4-column details grid with readable input boxes, clearer labels, and unclipped dropdown/date text.

## 1.1.14.0 - 2026-06-11

- Fixed AMC Excel import detection so AMC uploads are no longer misclassified as Payments and skipped for missing AmountPaid values.
- Added the missing AMC import save path to create or refresh AMC contracts by contract number, including client/site resolution, contract dates, value, status, equipment, and notes.
- Added AMC header aliases for common PO/contract workbook columns and created a one-sheet AMC upload workbook for the current client file.

## 1.1.13.0 - 2026-06-10

- Cleaned up global WinForms border rendering so input hosts, cards, grids, and reusable panels use a single #D1D5DB outline with safer 12 px section spacing and no stacked native/overlay input borders.
- Standardized ServoERP base forms/pages and Tally integration to DPI autoscaling, and hardened the Release build to run as x64 so QuestPDF native dependencies load correctly.
- Verified the priority pages and detail pages through Release `/navtiming` and `/smoketest` coverage.

## 1.1.12.0 - 2026-06-10

- Purchase Orders dashboard now loads its data on first open (stat cards, recent PO table, aging, and top-supplier panels previously stayed empty until Refresh was pressed).
- New PO form: removed grey backgrounds from read-only fields (Supplier GSTIN, Created By/On, Currency, Payment Terms, Delivery Address, line Category) by opting them out of the global input-theming watcher that re-applied the grey every 750 ms; fixed the line-item column header overlapping the "4 Items" title.
- Inventory: the Save Item / New Item action bar was covered by the fixed-height detail panel and Quick Actions card on smaller screens, making saving impossible on typical laptops. The detail editor now fills and scrolls, Quick Actions is a compact 2x2 grid, and the Save bar is always pinned and visible. Verified at 1366x768.
- Invoices: first open froze the app for 20-30 seconds ("Not Responding"). The dashboard analytics snapshot (full invoice + contract reload from SQL) now builds on a background thread, the 1,500-item catalog rebuild and checklist-template queries moved off the UI thread, and the item-picker combos bind via AddRange. Invoices now opens with zero UI freezing.
- Audited every module page (Quotations, Invoices, Purchases, Payments, Dispatch, Inventory, Clients, Suppliers, Vendors, Jobs, AMC, Employees, Payroll, Master Data, Contracts) to confirm Save/Add actions exist, are wired, and are reachable.

## 1.1.11.0 - 2026-06-09

- Added `/amctest` smoke test entry point that verifies the Add AMC save path (insert, update, fallback-insert, duplicate rejection) without requiring the full enterprise test suite. Fixed `ContractRepository.MapContract` to handle NULL values in newly-added schema columns (MonthlyValue, AnnualValue, SLAResponseTimeHours, SLAUptimePercent, SLARepairTimeHours, ContractStatus) that were NULL for pre-migration rows. All 7 AddAMC smoke tests pass in under 55 seconds.

## 1.1.10.0 - 2026-06-09

- Fixed "+ Add AMC" lag, blank, and dead-form symptoms by (1) guarding the client SelectedIndexChanged handler so the 3 spurious BeginLoadSitesAsync calls fired during SetReferenceLoadingState(true) no longer race with the main reference load, (2) reducing ReferenceLoadTimeoutMilliseconds from 8 s to 5 s so failures surface faster, (3) enabling DoubleBuffered on AddAMCForm to eliminate white-flash on open, (4) moving OpenDetailPage out of the AddAMCForm using block in AMCPage so the form is fully disposed before the control tree is rebuilt, and (5) making the dashboard "+ New AMC" shortcut navigate to the AMC module and open the newly created contract instead of silently refreshing the dashboard.

## 1.1.9.0 - 2026-06-09

- Fixed the AMC detail crash after Add/View flows by handling stale or invalid contract IDs gracefully, refreshing the AMC list instead of throwing, and allowing detail headers to load even if a linked client row is missing.

## 1.1.8.0 - 2026-06-09

- Fixed the Add AMC button lag by deferring modal launch out of the page click event, preventing duplicate opens, showing the form before database lookup work starts, and caching active client lookup data for repeated opens.

## 1.1.7.0 - 2026-06-09

- Permanently suppressed the developer-folder update warning and hardened the client installer to remove stale portable/developer shortcuts so ServoERP opens through the installed Velopack update channel.

## 1.1.6.0 - 2026-06-09

- Added no-touch background updates: ServoERP now checks, downloads, notifies, installs, and restarts automatically from the installed Velopack channel without requiring the user to press an update button.

## 1.1.5.0 - 2026-06-09

- Kept Add AMC and Add AMC Equipment as owner-launched modal dialogs only, removing them from page/navigation timing flows that were opening full shell-style windows.

## 1.1.4.0 - 2026-06-09

- Improved Add AMC open performance by caching the guarded AMC schema setup after first use and preventing duplicate client-site loads while the form binds reference data.

## 1.1.3.0 - 2026-06-08

- Applied shared WinForms frontend polish for text clipping, grid readability, compact input heights, disabled action states, and dense flow/table spacing across ServoERP screens.
- Verified Release build output and recorded the frontend audit summary in `LOGS/frontend-engineer-summary.json`.

## 1.1.2.0 - 2026-06-08

- Verified the connected Client → Contract → Quotation → Job → Invoice → Payment chain against SQL Server with correct ID handoffs, INR totals, partial payment recalculation, and downstream cleanup.
- Confirmed payment deletion restores invoice balance, invoice/quotation deletion removes list-visible records, and job deletion decrements the job counter.
- Bumped ServoERP assembly and app version metadata to 1.1.2.0 for the clean Release build.

## 1.1.1.0 - 2026-06-08

- Fixed quotation save after visible line-item entry by committing active grid edits before collection and filtering null/empty line records before analysis and save.
- Fixed invoice save validation so visible amount-bearing line rows are collected even when the description cell has not been committed, with safe India-first default descriptions for service/material charges.
- Moved quotation and invoice line-item delete/insert persistence to Dapper inside the existing SQL transactions and added null-safe line filtering.
- Repaired existing invoice balances by updating `Invoices.BalanceDue = TotalAmount - PaidAmount` for unpaid positive-total invoices where the balance had been stored as zero.

## 1.1.0.0 - 2026-06-08

- Unlocked the ServoERP engineering foundation for Dapper data access, FluentValidation form validation, Serilog rolling file logging, direct Designer edits, and targeted rewrites of broken forms or methods.
- Installed Dapper 2.1.66, FluentValidation 11.11.0, Serilog 4.2.0, Serilog.Sinks.File 6.0.0, and required .NET Framework compatibility dependencies.
- Replaced the long-form AGENTS manifest with compact current engineering rules and bumped ServoERP to the 1.1 release line.

## 1.0.215.0 - 2026-06-08

- Retired confirmed BackgroundWorker hang paths by moving Client Save, Employees initial load, AMC dashboard load, and Add/Edit AMC load/save/site loading to async/await with Task.Run.
- Added QuestPDF 2026.5.0, startup community license setup, and a shared null-safe PDFGenerator for invoice and job-card PDF generation.
- Migrated quotation table references from TenderBids/TenderBidLineItems to Quotations/QuotationLineItems with guarded startup sp_rename migration.
- Kept AMC navigation cached through MainForm's central navigation pipeline and normalized local SQL configuration to .\SQLEXPRESS.

## 1.0.214.0 - 2026-06-08

- Fixed payment amount entry so typed INR values are not clamped to Rs.0.01 by zero-balance invoice selection, and saved payments now bind `AmountPaid` as an explicit `DECIMAL(12,2)` parameter.
- Added a visible invoice delete action on the dashboard recent-invoices list and refreshed the invoice dashboard immediately after deletion.
- Improved quotation save/list handling by surfacing save errors, returning to the refreshed quotation dashboard after save, and adding delete actions on quotation list surfaces.

## 1.0.213.0 - 2026-06-08

- Hardened E2E smoke-test paths for Clients, AMC, Employees, Vendors, and Jobs so SQL or validation failures return visible WinForms feedback instead of leaving pages blank or modal buttons disabled.
- Ensured the AMC dashboard and Add/Edit AMC form call the guarded AMC schema setup before load/save, keep AMC Number editable, and show clear duplicate AMC Number warnings.
- Moved the initial Employees navigation load onto a `BackgroundWorker` and kept the grid query aligned to the real `Employees.Name` column.
- Saved a selected technician before advancing a job from Created to Assigned, preserving the technician-required validation while letting valid assignments progress.

## 1.0.212.0 - 2026-06-08

- Moved the New Client modal save operation into a `BackgroundWorker` so SQL validation, insert/update, cache invalidation, and audit work no longer block the WinForms UI thread.
- Kept the New Client dialog open and restored the Save button when SQL or validation errors occur, while duplicate clients show the direct validation message and successful saves still close with `DialogResult.OK`.
- Updated source SQL connection configuration to use portable `.\SQLEXPRESS` for local SQL Server Express installations.

## 1.0.211.0 - 2026-06-08

- Made the AMC Number field editable in the Add/Edit AMC form while preserving the generated default number.
- Added AMC Number validation and duplicate-number handling so users get a clear warning instead of a raw SQL unique-index failure.
- Saved edited AMC numbers on existing contracts while preserving the existing `AMCContracts.AMCNumber` unique index and parameterised SQL flow.

## 1.0.210.0 - 2026-06-08

- Added permanent `ServoPageBase` and expanded `ServoFormBase` foundation classes for automatic double-buffering, safe BackgroundWorker creation, UI-thread marshaling, branded error dialogs, load logging, batch panel rebuilds, and automatic DataGridView optimisation.
- Routed existing page infrastructure through `ServoPageBase` by updating `BaseUserControl`, preserving deferred page behaviour while making the safety/performance layer unavoidable for all module pages.
- Migrated existing modal/sub forms and direct page controls to the ServoERP base classes where applicable, excluding `MainForm`, `LoginForm`, and reusable non-page controls.
- Replaced UI-layer `new BackgroundWorker()` construction with base `CreateWorker()` usage and kept worker completion handlers guarded with error checks plus `RunOnUI`.
- Documented mandatory foundation rules in `AGENTS.md` so future ServoERP pages, forms, workers, grid setup, panel reloads, dropdowns, and navigation handlers follow the same base patterns.

## 1.0.209.0 - 2026-06-08

- Added shared ServoERP exception-safety infrastructure for UI-thread marshaling, monthly exception logs, safe BackgroundWorker completion, and friendly in-app error dialogs.
- Registered global WinForms, AppDomain, and Task exception hooks at process startup before any other startup work.
- Hardened existing BackgroundWorker completion handlers across Settings, Payments, Inventory, Invoices, Master Data, AMC, WhatsApp, Tally, backup, and setup flows with `e.Error` handling and UI-thread guarded updates.
- Added Settings diagnostics controls to open the log folder, view the current monthly log, and clear old logs older than 90 days.

## 1.0.208.0 - 2026-06-08

- Added AMC equipment register, service visit schedule, service history, and AMC health detail page.
- Added guarded `AMCEquipment`, `AMCVisits`, and `AMCContracts.CoverageType` schema through `DbHelper.EnsureAMCSchema()`.
- Added automatic visit scheduling after new AMC creation plus dashboard equipment/visit/coverage indicators.

## 1.0.207.0 - 2026-06-08

- Completed AMC end-to-end workflow with real View/Edit support from dashboard cards.
- Added AMC dashboard search, status/type filters, site/equipment details, derived renewal status, and days-left indicators.
- Added edit-mode save, Renew 1 Year action, and parameterised update SQL while preserving existing contract table compatibility.

## 1.0.206.0 - 2026-06-08

- Added a separate AMC dashboard under Operations with KPI cards, scrollable AMC contract cards, and an Add AMC workflow.
- Added guarded AMC schema compatibility columns on the existing `AMCContracts` table without renaming or dropping legacy contract columns.
- Added a BackgroundWorker-based New AMC Contract form with generated AMC numbers, client/site dropdowns, live summary, validation, and parameterised insert SQL.

## 1.0.205.0 - 2026-06-08

- Reduced full navigation first-open times by deferring non-visible module work in WhatsApp Hub, Employees, Payments, and Master Data.
- Stopped WhatsApp Hub from initializing embedded WebView2 on page open; it now loads contacts in a `BackgroundWorker` and starts WhatsApp Web only when a chat is opened.
- Lazy-loaded Employee Jobs, Attendance, Skills, Documents, and Payroll tab data so the Employee page opens on the profile view first.
- Delayed Payments and Master Data background refreshes until the user remains on those pages, preventing rapid navigation from carrying heavy SQL work into the next form.

## 1.0.204.0 - 2026-06-08

- Fixed Settings first-open blank/non-responsive behaviour by batching the large lazy card build with suspended drawing and applying shared polish once after construction.
- Moved the Settings SQL Server health check, HSN/SAC master load, and Users & Logins summary load off the UI thread with `BackgroundWorker` so slow office SQL reads cannot freeze the app shell.
- Restored the base visibility pipeline for Settings so cached page and deferred-load handling stays consistent when the page is shown again.

## 1.0.203.0 - 2026-06-08

- Rewrote terminal/client SQL connection repair scripts to update `HVACPro.config` and installed `HVAC_Pro_Desktop.exe.config` files with backups and support logs.
- Added SQL connection validation, clearer operator errors, and optional SQL-login support for terminal PCs where Windows Authentication is not accepted.
- Synced the repaired scripts and updated instructions into the existing terminal install pack.

## 1.0.202.0 - 2026-06-07

- Added `/navtiming` automation to open Settings first, then every ServoERP module and safe tab/subpage, writing page-open timings to `TEST_RESULTS`.
- Logged Settings load failures through the shared crash logger so Settings no longer appears silently blank when its deferred load fails.
- Exposed deferred page load state for QA timing so module pages are measured after their background-safe load completes.

## 1.0.201.0 - 2026-06-07

- Fixed Help & Support Center layout when opened as the app's narrow right-side support drawer.
- Made Dashboard, Knowledge Base, System Health, and App Information content switch to single-column compact layouts below drawer width.
- Resized support-card titles, summaries, status labels, buttons, and article steps so text stays inside the visible card area.

## 1.0.200.0 - 2026-06-07

- Changed the login owner lifecycle so closing ServoERP closes the hidden login form instead of reopening the login screen.
- Locked login display to fresh shortcut/exe launches only; app close and logout now end the running process.

## 1.0.199.0 - 2026-06-07

- Locked the guided tour to a true first-login-only rule by recording a version-independent LocalAppData marker before the tour opens.
- Kept the existing completion setting for Skip/Finish while preventing app updates or settings-version resets from reopening the tour.

## 1.0.198.0 - 2026-06-07

- Added shared UI performance helpers for double-buffered grids and redraw-suspended card/list rebuilds.
- Moved inventory save/refresh, invoice list refresh, payment invoice lookup fallback, and payment recording refresh work off the UI thread with `BackgroundWorker` where synchronous DB calls still remained.
- Reduced repaint lag in Inventory, Invoice, Payment, and Purchase card/list surfaces without touching MainForm, sidebar, or modal classes.

## 1.0.197.0 - 2026-06-07

- Added a self-contained WinForms first-launch tour for the ServoERP main shell with a dimmed GDI+ spotlight overlay and floating tooltip navigation.
- Stored the tour completion state in per-user `Properties.Settings` so finishing or skipping the tour prevents it from reopening.
- Covered 19 core navigation steps across Dashboard, Clients, Jobs, Invoices, Dispatch, GST/sales, operations, payroll, master data, WhatsApp, and Tally workflows.

## 1.0.196.0 - 2026-06-07

- Removed stale resize-grip overlay artifacts from the right side of the New Job editor after card layout.
- Kept the New Job form on one clean card outline per section while preserving the existing spacious layout.

## 1.0.195.0 - 2026-06-07

- Added main dashboard Quick Create shortcut buttons for New Job, New Quotation, and New Invoice.
- Wired dashboard shortcuts through MainForm to open the real module editors in fresh-draft mode.
- Guarded the Jobs shortcut against deferred page loading overwriting the new job editor state.
- Corrected the New Job header date fallback so shortcut-created jobs show the scheduled date instead of 01/01/0001.

## 1.0.194.0 - 2026-06-07

- Removed the technician workload summary card from the New Job Technician section so the form shows only Assign technician, Priority, and Status fields.
- Kept technician selection updating the job header without rendering the old Unassigned / jobs-this-week mini card.

## 1.0.193.0 - 2026-06-07

- Fixed the New Job status/pipeline dropdown so invalid forward moves show a clear Jobs message instead of a generic Screen Error.
- Reset the status dropdown back to the saved pipeline value when a pipeline move is cancelled or blocked.
- Added form-side checks for technician assignment, checklist completion, and invoice availability before advancing job status.

## 1.0.192.0 - 2026-06-07

- Removed duplicate wrapper/card outlines from the Add Job editor so each textbox, dropdown, date field, numeric field, and notes field keeps one clean visible outline only.
- Added shared `NO_INPUT_HOST` / `NO_CARD_SURFACE` opt-out handling for form wrapper panels that must not be mistaken for editable input boxes.
- Kept the spacious Add Job layout while reducing visual noise from stacked panel, focus, and input-border painters.

## 1.0.191.0 - 2026-06-07

- Reworked the Add Job editor into a spacious full-width vertical form layout so Job Details, Technician, Cost, Checklist, Parts, Nudges, and Notes no longer fight for cramped columns.
- Enlarged the Add Job card headers, form rows, input heights, checklist rows, parts entry row, and empty states while preserving the shared light input theme.
- Removed editor resize-grip interference and blank header action pills from the Add Job form, and reset the editor scroll to the top whenever a new job draft opens.

## 1.0.190.0 - 2026-06-06

- Enforced the New Quotation Quote Details input theme globally for dynamically created inside forms, including text boxes, dropdowns, date pickers, numeric boxes, rich text fields, and checkboxes.
- Added a global open-form input watcher so subpages such as New Job and Payment Recording receive the same visible light bordered input treatment after their layouts rebuild.
- Tightened dashboard card resize-grip attachment so editor/form cards no longer regain dotted resize handles while true dashboard card surfaces remain managed.

## 1.0.189.0 - 2026-06-06

- Fixed the Dashboard compact-width card clipping by switching home-page department rows to a three-column wrap when the workspace cannot safely show five cards.
- Preserved the lighter Dashboard header and side spacing while ensuring every card border and action button remains visible.

## 1.0.188.0 - 2026-06-06

- Fixed the main Dashboard card row overflow by resetting stale saved Dashboard card layout, disabling dashboard resize grips on fixed home-page cards, and recalculating five-card row widths with card margins included.
- Kept the Dashboard header responsive while preventing right-side card clipping at compact desktop widths.

## 1.0.187.0 - 2026-06-06

- Fixed the main Dashboard header layout so the search box, date/time, user block, and backup/customize actions no longer overlap on compact widths.
- Reduced the dashboard side gutter and softened resize-grip dot rendering so dashboard cards sit closer to the content edge without heavy visual artifacts.

## 1.0.186.0 - 2026-06-06

- Fixed the Payment Recording right-column card cleanup at the global policy layer so fixed payment editor sections are never treated as dashboard-resizable cards.
- Added explicit resize-grip detachment for Payment Recording editor cards after the base/global layout pass, and restored the Save Payment action styling locally.

## 1.0.185.0 - 2026-06-06

- Fixed the Payment Recording right-column cards so Quick Actions, Invoice Summary, and Recent Status render as fixed editor panels instead of dashboard resize surfaces.
- Removed dashboard resize grip interference from Payment Recording editor cards and repainted their borders with the shared light input/card outline.

## 1.0.184.0 - 2026-06-06

- Applied Quote Details-style input shells to the Payment Details card so client, invoice, amount, payment date, mode, reference, and notes fields render as visible white bordered boxes.
- Added focused-state outline painting for Payment Details input shells while preserving existing payment data binding and validation.

## 1.0.183.0 - 2026-06-06

- Fixed the Payment Recording details card so client, invoice, amount, date, mode, reference, and notes fields stay inside a polished capped form grid instead of stretching across the card.
- Applied the shared ServoERP input styling to the Payment Details card after field layout so dropdown and textbox ends remain visible.

## 1.0.182.0 - 2026-06-06

- Fixed the Purchase Orders editor header so action buttons no longer overlap the title, breadcrumb, or draft status badge on compact widths.
- Moved the live purchase-order status message into its own header line and hide lower-priority toolbar actions when the header is narrow.

## 1.0.181.0 - 2026-06-06

- Fixed the Purchase Orders left-rail filter card so status tabs use a compact two-row grid instead of fixed absolute positions that clipped at narrow widths.
- Reworked the Purchase Orders search/status filter row to stay inside the card with a stable responsive width.

## 1.0.180.0 - 2026-06-06

- Promoted the New Quotation Quote Details textbox/dropdown look into the global input styling layer so standard text boxes, dropdowns, date pickers, numeric fields, rich text boxes, and modern search/input wrappers use the same white fill and light ServoERP outline.
- Replaced older native fixed input borders with the shared rounded outline painter for consistent focus, disabled, and read-only states across forms and module pages.

## 1.0.179.0 - 2026-06-06

- Redesigned the New Quotation Quote Details panel to match the supplied clean reference layout with a larger section card, DRAFT badge, divider bands, icon-led fields, and 4/5/3 field grouping.
- Added a shared custom-input-shell escape hatch and flattened global ComboBox styling so modern field surfaces are not overwritten by native Windows chrome.
- Preserved existing quotation controls, date format, client/site binding, status workflow refresh, save/load behaviour, and supplier/customer document tracking.
- Kept the change UI-only with no database, schema, PDF, pricing, or business workflow changes.

## 1.0.178.0 - 2026-06-06

- Matched the Tally Integration Connection page more closely to the supplied reference image with a compact header, flat tab spacing, overview title/status row, Tally Prime illustration, and balanced tile strip.
- Reworked the Connection Settings, Connection Health, Recent Activity, and Quick Actions layout so the page fits the desktop viewport without clipped action buttons.
- Preserved the existing Tally form behavior, BackgroundWorker operations, SQL guards, Excel export, and sidebar navigation while changing only the visual layout.

## 1.0.177.0 - 2026-06-06

- Fixed the Tally sidebar page crash caused by the Tally grid selection checkbox column being frozen while shared grid fill styling was applied.
- Kept Tally selection checkboxes fixed-width without freezing them, allowing the Tally Integration Hub to open normally.
- Removed dashboard resize grip overlays from the fixed-layout Tally hub so the page renders cleanly after global UI services run.

## 1.0.176.0 - 2026-06-06

- Fully rebuilt `TallyIntegrationForm` into a five-tab Tally Integration Hub with a dashboard-style Connection tab, custom status tiles, health checks, recent activity, quick actions, export, import, inventory sync, and logs.
- Added guarded first-load Tally schema setup for `TallyActivityLog`, `TallySettings`, and existing `StockItems` inventory sync flags using additive SQL only.
- Added a designer partial for the Tally hub and wired it into the project without changing `MainForm.cs`, sidebar navigation, PDF, Excel, or any modal/dialog forms.

## 1.0.175.0 - 2026-06-06

- Rebuilt the Tally Integration Hub form with clearer India-first TallyPrime wording, DD/MM/YYYY date filters, shared ServoERP grid/button/input styling, and safer BackgroundWorker operation locking.
- Added browse/open support for the XML export folder so accountants can quickly find manual Tally import files.
- Improved Tally export and inventory grids with committed checkbox selection, fixed selection-column sizing, empty-state guidance, and preserved operation result messages.

## 1.0.174.0 - 2026-06-06

- Removed duplicate inner dashboard headers from Quotations and Invoices so each page keeps one clear top header only.
- Let quotation KPIs and invoice KPI cards start directly below the page header for a cleaner single-page hierarchy.
- Preserved existing quotation and invoice page actions, dashboard cards, workflow sections, business logic, and data refresh behavior.

## 1.0.173.0 - 2026-06-06

- Made the header polish visible on Dashboard by turning the compact top search card into a clearer white page header with title, subtitle, search, date, user, and backup actions.
- Polished the Invoice Management header directly with a white top band, tighter typography, aligned actions, and a bottom separator.
- Polished both Quotation editor and Quotation Dashboard headers directly so quotation screens no longer depend only on the generic header polish pass.

## 1.0.172.0 - 2026-06-06

- Added a shared `PageHeaderPolishService` that improves existing page headers without replacing module-specific header layouts.
- Normalized header title/subtitle typography, action button sizing, action rail spacing, and subtle bottom dividers across deferred pages and WhatsApp Hub.
- Preserved all existing page header structures, button handlers, sidebar navigation, business logic, database schema, and PDF output.

## 1.0.171.0 - 2026-06-06

- Fixed silent uninstall so `/SILENT` and `/VERYSILENT` no longer block on the keep/delete business data prompt, defaulting to preserve ServoERP data.
- Suppressed uninstall completion dialogs during silent uninstall while preserving the interactive data-choice prompt for normal user uninstall.
- Updated the VM fresh-install harness to stage the installer locally before running, avoiding Windows network-share security warnings during automated testing.

## 1.0.170.0 - 2026-06-06

- Fixed the installer-shipped `HVACPro.config` version so fresh installs report the current ServoERP version.
- Hardened WhatsApp WebView2 startup so smoke-test/form shutdown cannot post UI work after the form handle is gone.

## 1.0.169.0 - 2026-06-06

- Improved first-install server PC setup into one guided office readiness checklist covering SQL Server, license activation, owner/admin login, legal approval, terminal sync pack generation, business-write lock status, and diagnostics fallback readiness.
- Added direct setup actions for license activation, admin account creation, legal acceptance, terminal pack generation, pack-folder opening, and support-report copy from the same first-run screen.
- Locked Finish Start until SQL, license, admin login, legal approval, and terminal connection pack are all ready, reducing confusing mid-startup prompts for first-time users.

## 1.0.168.0 - 2026-06-05

- Updated the end-to-end workflow smoke to report a controlled skip when the active license does not enable the required business-write modules, instead of failing with an unauthorized vendor/client write.
- Kept `/smoketest` strict: real `FAIL` lines still return a non-zero process exit code.

## 1.0.167.0 - 2026-06-05

- Fixed `/smoketest` so any `FAIL` line in the generated report returns a non-zero process exit code.
- Updated the end-to-end workflow smoke to activate a local QA trial when a clean VM has no license cache, so terminal setup tests validate business workflows instead of stopping at the license gate.

## 1.0.166.0 - 2026-06-05

- Fixed fresh server database initialization by creating `SupplierItemPrices` before vendor/supplier migration logic references it.
- Verified on AD-Lab-DC01 that SQL Express installs, runs, accepts local SQL connections, and exposes TCP 1433 before rerunning the terminal harness.
- Preserved guarded schema behavior with `IF NOT EXISTS`; no existing data, tables, columns, or PDF headers changed.

## 1.0.165.0 - 2026-06-05

- Fixed first-run automation so installer `/firstrun` returns a non-zero exit code when SQL Server/database initialization fails instead of reporting false success.
- Fixed `/smoketest` startup failure handling so unavailable SQL writes a smoke failure report and exits, rather than hanging behind a modal startup error dialog.
- Added AD-Lab-DC01 VM test harness diagnostics for server-first-run and simulated terminal SQL connectivity checks.

## 1.0.164.0 - 2026-06-05

- Added a first-run `Server Setup & Terminal Sync` wizard for the office server PC, inspired by SQL Server setup, QuickBooks Database Server Manager, and Sage multi-user connection-manager flows.
- Added guided SQL readiness checks, database initialization, backup/fallback preparation, firewall rule setup, terminal connection-pack generation, and a one-time setup completion gate before business use.
- Added `/serversetup` for support/admin relaunch and kept installer `/firstrun` database initialization compatible with the new visible first-launch process.

## 1.0.163.0 - 2026-06-05

- Added shared `DatabaseConnectionStateService` for terminal SQL status, background retries, write-lock decisions, and common support messaging.
- Routed SQL open failures, DB executor failures, business write guards, MainForm SQL status banner, and Help & Support database diagnostics through the shared connection-state service.
- Kept local SQLite fallback diagnostics-only while locking business writes when the office SQL Server is offline or misconfigured.
- Added a backend decision map with Mermaid diagrams, module ownership tables, SQL verification patterns, and a reusable prompt template for targeted ServoERP change planning.

## 1.0.162.0 - 2026-06-05

- Documented the SQL Server resilience architecture for smoother multi-terminal operation, keeping SQL Server as the only business database and SQLite as diagnostics-only fallback.
- Added a manifest rule that future terminal/server error handling must use shared connection-state services instead of form-level fallback logic.

## 1.0.162.0 - 2026-06-04

- Fixed page-header action layout globally by excluding header, toolbar, and action rails from the automatic layout audit wrapping pass.
- Updated Employee Operations, Jobs Dashboard, and Master Data headers so action buttons stay visible, text remains readable, and compact widths use a controlled second row instead of hiding buttons.
- Kept ServoERP on the locked light theme and preserved the existing sidebar colour.

## 1.0.161.0 - 2026-06-04

- Fixed the Employee Operations header so action buttons stay in one compact row instead of wrapping into a tall right-side block.
- Moved employee screen status text out of the action-button flow and reduced the header band height so KPI cards sit directly below the title area.

## 1.0.160.0 - 2026-06-04

- Fixed dense module dashboard overflow by changing the shared `DeferredPageControl` canvas to vertical-only scrolling.
- Removed the forced 1360px page width that caused Employees, Jobs, Master Data, Vendors/Suppliers, Contracts, and similar pages to show horizontal scrollbars and clip right-side dashboard content.
- Preserved the locked light theme and existing sidebar palette while improving page reachability across the app.

## 1.0.159.0 - 2026-06-04

- Reverted the shared ServoERP workspace theme from the design-audit dark pass back to the permanent light theme.
- Locked the project UI policy so future theme work must keep ServoERP light unless Harshal explicitly asks for a theme change.
- Restored light grids, cards, inputs, page surfaces, empty states, and modern ERP controls through the shared design-system layer.

## 1.0.158.0 - 2026-06-04

- Added silent Velopack auto-update checks that download GitHub Release packages in the background and apply the prepared update when ServoERP exits.
- Added configurable silent update switches for update checks, check interval, and apply-on-exit behaviour through the existing ServoERP config service.
- Added a one-command local GitHub Releases deployment script that builds, packages, uploads, and publishes Velopack assets without GitHub Actions or paid storage.

## 1.0.157.0 - 2026-06-04

- Applied the design-audit dark workspace pass through shared UI tokens, grid styling, inputs, buttons, cards, empty states, and base form/user-control theme hooks.
- Preserved the existing MainForm sidebar blue palette while standardizing non-sidebar module surfaces away from hardcoded light UI chrome.

## 1.0.156.0 - 2026-06-04

- Fixed Settings > About & Updates so the current version label refreshes from the running assembly version whenever the cached Settings page is shown.
- Clarified manual update-check messaging for developer-folder builds so non-Velopack runs no longer imply that a client update is available.

## 1.0.155.0 - 2026-06-04

- Expanded the dense ERP layout canvas and targeted the marked cramped UI zones across Jobs, Clients, Suppliers, Inventory, Dispatch, Payments, Quotations, Invoices, and Purchase Orders.
- Widened right-side action rails, quick-action cards, purchase line-item rows, quote details, invoice HVAC workflow cards, client pagination, and dashboard header action spacing so controls stop overlapping and pages remain reachable through the main scrollbar.

## 1.0.154.0 - 2026-06-04

- Added a shared main scroll canvas for deferred module pages so Jobs, Clients, Inventory, Dispatch, Payments, Quotations, Invoices, Purchases, Suppliers, and similar dense ERP screens stay reachable on smaller client desktops.
- Applied the shared canvas to top-level filled page roots so fixed right-side action panels and toolbar rows can stretch instead of clipping controls.

## 1.0.153.0 - 2026-06-04

- Fixed the Employee Operations header so the title text is no longer clipped by the action button area.
- Compact-aligned Employee Operations header buttons and status text to keep the top row readable at normal client desktop sizes.

## 1.0.152.0 - 2026-06-04

- Fixed Master Data smart upload cards so record counts read live module tables instead of matching against setup-status labels.
- Added supplier-role-aware counting for the Data Control Center so Suppliers, Jobs, Employees, Purchases, Payments, Quotations, Clients, Sites, and Invoices reflect existing app data.

## 1.0.151.0 - 2026-06-04

- Changed Employee Operations list filtering from Department to Client / Site so workforce records can be filtered by client assignment.
- Updated the Employee Operations grid to show Client / Site in the list while keeping Department available inside the employee profile.

## 1.0.150.0 - 2026-06-04

- Fixed Materials / Procurement loading on upgraded client databases by repairing missing supplier role columns before Inventory joins `Vendors.IsSupplier`.
- Added the same guarded compatibility repair inside stock movement updates so older client schemas remain usable after update.

## 1.0.149.0 - 2026-06-04

- Fixed the Velopack update check so client PCs do not keep seeing the same update after restart when the release target matches the running ServoERP version.
- Normalized three-part Velopack package versions and four-part assembly versions before deciding whether an update is newer.

## 1.0.148.0 - 2026-06-04

- Hardened Inventory screen actions by guarding shared DataGridView header sizing against WinForms click/hover crashes and removing a duplicate delete-error prompt.

## 1.0.147.0 - 2026-06-04

- Added Velopack-based GitHub Releases update checks, download prompts, and restart/apply flow for ServoERP desktop releases.
- Added configuration backup before applying updates and disabled the legacy ZIP updater path.
- Added local release packaging/upload scripts and GitHub Releases documentation with zero-cost guardrails.
- Added a sales-focused UI polish pass for Quotations, Invoices, Payments, Clients, and Client Detail surfaces, plus local FlaUI sales-audit screenshots for quick regression checks.

## 1.0.146.0 - 2026-06-04

- Fixed grey background patches in the Quotation editor by forcing quote-detail field wrappers, search/filter controls, and quotation input fills onto the white quotation surface.

## 1.0.145.0 - 2026-06-04

- Fixed Materials / Procurement loading so the inventory list still opens when the supplier dropdown refresh fails or client data contains older imported value shapes.
- Hardened stock-item read mapping for legacy/imported inventory rows while preserving the existing `StockItems` table and supplier role model.

## 1.0.144.0 - 2026-06-04

- Fixed Database Connection Setup so client PCs can enter a full SQL named instance such as `PC-5\SQLEXPRESS` without ServoERP building a doubled target like `PC-5\SQLEXPRESS\SQLEXPRESS`.
- Added guarded validation when the server field and instance field contain conflicting named-instance values, preventing stale client SQL targets from being saved.

## 1.0.143.0 - 2026-06-03

- Removed dead Windows Explorer placeholder actions from the global dashboard/card right-click menu so every visible item now has an executable ServoERP action.
- Added real card actions for favorites, shortcuts, share links, send-to shortcut/email link, copy, properties, and persistent card locking with permission checks, user feedback, and audit logging.
- Added smoke-test coverage that fails if the global card context menu exposes obsolete placeholder items or any menu item without an executable handler.
- Fixed the Supplier dashboard recent list so stale category filters and rewritten search placeholders reset safely instead of hiding all 352 suppliers.
- Removed Recent Activity/feed widgets from normal dashboards and module screens while preserving backend audit, job, client, invoice, and Tally log data.
- Moved derived pending-approval supplier records into inactive status and kept active supplier counts visible on dashboard and supplier screens.

## 1.0.142.0 - 2026-06-03

- Restored a separate Vendors page in the main Operations navigation for service providers, subcontractors, labour partners, and external service vendors.
- Kept Suppliers as a separate page for purchase, inventory, stock, material, and PO workflows using the same guarded partner role flags.
- Made the shared partner form role-aware so Supplier pages filter `IsSupplier` records and Vendor pages filter `IsServiceVendor` records without duplicating tables or renaming existing `VendorID` links.

## 1.0.141.0 - 2026-06-03

- Added supplier/vendor role flags to the existing business partner master so purchase, inventory, and material workflows select Suppliers while service/subcontracting workflows can use Vendors.
- Renamed procurement-facing UI labels across Suppliers, Purchases, Inventory, Reports, Dashboard, and AI material guidance from Vendor to Supplier where the workflow is material or stock related.
- Added guarded sample Supplier records and service Vendor records, plus regression coverage for supplier/service-vendor role separation.

## 1.0.140.0 - 2026-06-03

- Fixed the Invoice dashboard PDF button so it generates and opens a temporary invoice PDF when no saved PDF path exists.

## 1.0.139.0 - 2026-06-03

- Added the shared Vendor Workflow / Client Workflow sent-and-received card layout to Purchases, Invoices, Payments, and Jobs dashboards.
- Added visible page-level Import actions with the same directional import menu on Purchases, Invoices, Payments, and Jobs.
- Reused the shared dashboard workflow primitive so the four workflow buckets stay aligned consistently across these modules.

## 1.0.138.0 - 2026-06-03

- Re-aligned the Quotations workflow dashboard into Vendor Workflow and Client Workflow cards, each stacked with Sent on top and Received below.
- Kept Work Attached aligned beside those workflow sections at the same height.

## 1.0.137.0 - 2026-06-03

- Added `Sent to Vendors` and `Received from Clients` workflow cards to the Quotations dashboard.
- Expanded the Quotations Import menu to route imports into all four directional workflow buckets: vendor received, vendor sent, client sent, and client received.
- Tightened quotation workflow filters so sent/received cards stay distinct and old saved card order does not hide new workflow cards.

## 1.0.136.0 - 2026-06-03

- Moved the quotation import type selector inside the Quotations Import button menu instead of showing it as a separate toolbar dropdown.

## 1.0.135.0 - 2026-06-03

- Added a permanently visible quotation import type dropdown directly on the Quotations toolbar beside Import.
- Moved the quotation import type prompt before file selection and wired the toolbar selection into the import workflow.

## 1.0.134.0 - 2026-06-03

- Added a quotation import type dropdown with "Received from Vendors" and "Sent to Clients" options before importing quotation Excel files.
- Stored the selected quotation workflow into imported TenderBids so dashboard cards classify imported quotes under the right workflow bucket.

## 1.0.133.0 - 2026-06-03

- Added an end-to-end HVAC business workflow smoke test covering Client -> Site -> Job -> Quotation -> Purchase -> Inventory -> Invoice -> Payment -> Reports and major detail pages.
- Fixed purchase receiving so linked inventory materials are posted into stock once, with stock movement history, instead of only changing PO status.
- Added a generated QA report artifact under `TEST_RESULTS` for the full user-flow pass.

## 1.0.132.0 - 2026-06-03

- Reworked the Quotation Dashboard recent quotation area into three workflow cards: Received from Vendors, Sent to Clients, and Work Attached.
- Removed the standalone Status and Conversion dashboard cards from the quotation page so the layout focuses on operational quotation flow.
- Reset the saved quotation dashboard order so old local card order state does not restore removed cards.

## 1.0.131.0 - 2026-06-03

- Added a global text/button overlap guard in `UIHelper` so fixed-position labels, checkboxes, and radio text reserve space beside action buttons instead of hiding under them.
- Strengthened app-wide button text fitting so real text buttons expand to their measured label width in dense action rows, including toolbar and fixed-width button cases where possible.
- Added enterprise smoke-test coverage that scans major forms and dialogs for visible text controls overlapping visible text buttons.

## 1.0.130.0 - 2026-06-03

- Added centralized pooled SQL Server connection creation through `DatabaseConnectionFactory` with normalized pooling, min/max pool size, 15-second connect timeout, and consistent health-check diagnostics.
- Routed database startup, schema migration, Support Center checks, Settings database status, backup restore, and shared `DbExecutor` operations through the pooled connection factory.
- Added one safe transient retry for connection-open failures and slow SQL logging for shared executor operations over 2 seconds.
- Added Max Pool Size configuration to the Database Connection Setup screen so client PCs can tune pooled SQL usage without manual config edits.

## 1.0.129.0 - 2026-06-03

- Centralized app-wide button role styling through `UIHelper.ApplyButtonStyle` and shared design-system button factories.
- Standardized primary, secondary, danger, and neutral button colors, borders, hover/focus states, sizing, padding, and text fitting across forms, dialogs, dashboards, cards, and custom button controls.
- Strengthened global button row/container normalization and smoke-test audits so major forms reject clipped, misaligned, undersized, or wrongly styled action buttons.
- Fixed the Dispatch Center header button row so its custom resize layout participates in the shared button alignment pass.

## 1.0.128.0 - 2026-06-03

- Fixed the Jobs editor header so the job number, job title, and status summary keep proper spacing on compact client screens.
- Made the Jobs top action buttons wrap cleanly without stealing space from the job title.

## 1.0.127.0 - 2026-06-03

- Fixed the Purchase Orders editor top toolbar so action buttons no longer overlap the page title or breadcrumb on compact screens.
- Added responsive wrapping and bounded status text for purchase editor header actions.

## 1.0.126.0 - 2026-06-03

- Made the Invoice Management HVAC Workflow card extendable with shared bottom-edge and bottom-right resize grips.
- Added the same resize grips to the Checklist / Tasks, Asset / Equipment, and Payment History workflow mini cards.
- Kept workflow grids resizing with their cards so expanded workflow space remains usable.

## 1.0.125.0 - 2026-06-03

- Added vendor-style client status controls to the Clients dashboard grid, recent client rows, client cards, client header badge, and Client Detail status section.
- Added shared client lifecycle persistence through `ClientService.UpdateLifecycleStatus` and `ClientRepository.SetLifecycleStatus` so Active, Prospect, On Hold, Inactive, and Blacklisted update consistently across the app.
- Registered client status with the global status editor so client status columns using shared grid styling can be changed through the same app-wide status workflow.

## 1.0.124.0 - 2026-06-03

- Added an inline status dropdown to the Vendor Management Recent Vendors card so vendors can be changed between Active, Pending Approval, Inactive, and Blocked directly from the dashboard.
- Persisted vendor dashboard status changes through the vendor service and repository using existing `IsActive` / `IsArchived` lifecycle flags, with cache invalidation and audit logging.

## 1.0.123.0 - 2026-06-03

- Added guarded `StockItems.IsActive` and `StockItems.LastUpdated` repair to Inventory repository reads and writes so old client databases load the Materials / Procurement screen before imports run.

## 1.0.122.0 - 2026-06-03

- Added guarded inventory import schema repair for older client databases missing `StockItems.IsActive` or `StockItems.LastUpdated`, preventing every inventory row from being skipped with “Invalid column name 'IsActive'.”
- Improved SQL connection error popups to show the configured server/database target so client-PC setup issues can be diagnosed from screenshots.

## 1.0.121.0 - 2026-06-03

- Added client lifecycle status editing so clients can be set to Active, Inactive, Prospect, On Hold, or Blacklisted from the Clients module.
- Persisted the client `IsActive` flag on both create and update, so status changes immediately affect dashboards, filters, and active-client lookups.
- Kept inactive clients visible in Clients for history while removing them from new quotation client selection and other active-only operational flows.

## 1.0.120.0 - 2026-06-03

- Refreshed the Clients dashboard automatically after successful Excel client imports so imported rows appear immediately instead of waiting for cache expiry or manual navigation.
- Hardened the client-PC SQL connection script to require a real office SQL Server target and update both `HVACPro.config` and `HVAC_Pro_Desktop.exe.config`.
- Updated generated support client-connection packs so future multi-PC setup exports carry the same safer database configuration script.

## 1.0.119.0 - 2026-06-03

- Restored global bottom-edge and bottom-right card resize grips across dashboard/card surfaces.
- Made the card resize grip service self-heal when card content is rebuilt after initial attachment.
- Broadened centralized card detection for white card-style panels while preserving editor/input exclusions.

## 1.0.118.0 - 2026-06-03

- Centralized global input styling so TextBox, ComboBox, DateTimePicker, NumericUpDown, RichTextBox, search boxes, and custom modern input controls use visible white editable fields with dark text.
- Added consistent gray normal input borders, blue focus frames, and disabled/read-only gray surfaces across base forms, deferred pages, and WhatsApp Hub.
- Routed design-system theme input handling through `UIHelper.ApplyInputStyle` to prevent future screens from reverting to label-like borderless fields.

## 1.0.117.0 - 2026-06-03

- Added Inventory duplicate item detection from the More Actions menu, showing duplicate material groups by normalized item name.
- Added Inventory duplicate item merge cleanup that keeps the best master row, moves linked stock/job/invoice/PO references, combines quantities, and archives duplicate rows.
- Removed known ServoERP development/test records from the live database while preserving client master data, license records, settings, users, and reference tables.

## 1.0.116.0 - 2026-06-03

- Removed the Purchase Orders dashboard fallback sample rows so a reset database now displays a genuinely empty purchase-order screen.
- Kept Purchase Orders tied only to real database rows after the Madhusuman fresh-start reset.

## 1.0.115.0 - 2026-06-03

- Changed Forgot Password on the login screen into a direct self-service reset flow for every active user type.
- Added username/email based password reset that sets the new password immediately, clears failed-login lockouts, and does not require administrator approval.
- Reused the password policy and password-change dialog so reset passwords still require 8+ characters, one uppercase letter, and one number.

## 1.0.114.0 - 2026-06-03

- Added a one-time Madhusuman purchase-order fresh-start reset that clears old PO headers, PO line items, pending PO charges, and PO links on vendor advances during the client update.
- Cleared vendor purchase totals after the PO reset so Vendor Management starts from zero purchase history while keeping vendor master data intact.
- Removed stored/imported purchase-order PDFs and PO receipt/archive files from ServoERP-controlled folders during the same one-time reset.

## 1.0.113.0 - 2026-06-03

- Removed the Viewer role from role-selection flows so users are no longer offered a read-only account type.
- Changed ServoERP role access policy so every active logged-in user receives full create, view, edit, and delete access across licensed modules.
- Updated security seeding so all existing role rows receive full permissions, including any legacy roles already present in older client databases.
- Opened Settings user management, payroll import, payroll lock, and admin credential validation to all active logged-in users under the new full-access policy.

## 1.0.112.0 - 2026-06-03

- Fixed the Vendor Management detail page top action strip so action buttons remain in a compact horizontal toolbar instead of stacking down the right side.
- Reworked the Vendor details form into a responsive two-column layout with enough height for tax and payment fields, preventing overlap between vendor identity, contact, compliance, and bank details.

## 1.0.111.0 - 2026-06-03

- Reworked the shared dashboard pagination control so navigation buttons, page number, page size, and record summary align cleanly without clipping on compact cards.
- Fixed the Vendor Management `Recent Vendors` card footer spacing so the pager sits below the vendor table instead of overlapping or cutting off.

## 1.0.110.0 - 2026-06-03

- Fixed Vendor Management dashboard search placeholder handling so `Recent Vendors` no longer treats `Search vendor name, code, email...` as a real filter after re-rendering.
- Preserved the vendor dashboard search term explicitly and suppressed filter refresh while dashboard controls are being rebuilt.

## 1.0.109.0 - 2026-06-03

- Fixed the Vendor Management `Recent Vendors` dashboard card so vendor master rows still appear even if purchase-order metrics or duplicate analysis cannot be loaded.
- Logged purchase-summary and duplicate-analysis failures separately instead of returning an empty vendor dashboard.

## 1.0.108.0 - 2026-06-03

- Made the client detail page `Dashboard` return button permanently visible on the left side of the header instead of hiding it inside the crowded action strip.
- Added a `Client Dashboard` button to the new/edit client modal so users can return to the dashboard directly from client creation or editing.

## 1.0.107.0 - 2026-06-03

- Expanded the Master Data upload dropdown and dropped-file import router to include every supported Excel upload type: Clients, Vendors, Sites, Inventory, Purchases, Invoices, Payments, Quotations, Jobs, and Employees.
- Rebuilt the Master Data smart upload cards from the same shared upload list so newly supported upload types are visible from the dashboard instead of hidden in module-specific screens.

## 1.0.106.0 - 2026-06-01

- Centralized HTML-to-PDF generation through a shared Chromium exporter with safer browser arguments, isolated temporary browser profile, longer timeout, output-folder creation, and PDF file stability validation.
- Routed Purchase Orders, HTML previews, payroll PDFs, job reports, and quotation PDFs through the shared exporter so PDF generation/opening behaves consistently across the app.

## 1.0.105.0 - 2026-06-01

- Fixed Purchase Orders dashboard pager clipping by making the shared pagination footer compact itself inside narrow cards.
- Changed the PO Number link in the Purchase Orders table to open the stored PDF linked to that PO, with a clear message when no stored PDF path is available.

## 1.0.104.0 - 2026-06-01

- Fixed a post-lock-card performance regression by preventing repeated dynamic-control event hooks in the shared card context-menu and dashboard drag layers.
- Moved card lock persistence outside the dashboard layout service lock so right-click Lock Card / Unlock Card cannot stall the UI thread longer than necessary.

## 1.0.103.0 - 2026-06-01

- Added right-click Lock Card / Unlock Card actions for dashboard cards so a resized card can be frozen in place.
- Persisted the card lock flag with saved layout JSON so locked cards stay locked across app restarts until the user explicitly unlocks them.
- Disabled drag and resize grips while a card is locked, then restored normal movement and resizing immediately after Unlock Card.

## 1.0.102.0 - 2026-06-01

- Fixed Purchase Orders dashboard row clicks so selecting a PO opens its linked PDF or generates and opens a fresh PO PDF when no stored PDF path exists.
- Replaced the blank/ellipsis Purchase Orders Actions cell with a visible `View` button, while keeping secondary actions available from right-click.

## 1.0.101.0 - 2026-06-01

- Added a reusable global pagination control with first/previous/next/last navigation, page-size selection, direct page entry, empty-state handling, and clamped page validation.
- Replaced duplicated pagers in Clients, Contracts, Vendors, Payments, Purchases, Jobs, and Inventory with the shared control while preserving each page's active search, filters, and sort/order logic.
- Fixed the All Clients dashboard search to filter the grid in place with real paging, preventing the screen rebuild and focus loss seen while typing.
- Removed the non-functional quotation line-item pager buttons so editable quotation rows no longer show blank or dead pagination controls.

## 1.0.100.0 - 2026-06-01

- Fixed the All Clients card search so typing filters the table in place instead of recreating the dashboard and stealing keyboard focus.
- Replaced fake dashboard search placeholder text with native textbox cue text, so users can click and type without first deleting `Search clients...`.
- Converted the All Clients card list to a stable scrollable grid so filtered client rows remain reachable without pagination flicker.

## 1.0.99.0 - 2026-06-01

- Fixed Clients page search so typing no longer rebuilds the whole dashboard after every letter and the active search box keeps focus and caret position.
- Reworked the classic Clients list to scroll through all matching clients instead of hiding records behind small fixed pages.
- Added vertical scrolling to the Clients dashboard table, client detail workspace, and Add Client dialog so client records remain reachable on smaller screens.

## 1.0.98.0 - 2026-05-30

- Fixed quotations so `Send for Approval` now saves and persists the approval status immediately instead of only changing the on-screen status.
- Replaced the raw invoice payment exception popup with a user-friendly recovery message while preserving internal error logging.

## 1.0.97.0 - 2026-05-30

- Continued the live end-to-end HVAC workflow pass beyond client creation and verified that jobs can be created successfully through the real Jobs screen against SQL Server.
- Fixed the client command-center `+ Add Job` workflow so it now carries the selected client into a new job draft instead of opening Jobs on the wrong default client.
- Added a small shared workflow launch context to support safe cross-page handoff for existing modules without changing schema, license logic, or module structure.

## 1.0.96.0 - 2026-05-30

- Fixed a real client-save blocker by replacing invalid sample GSTIN values with a checksum-valid example across client UI, validation guidance, import samples, and smoke data.
- Fixed Clients navigation so `View client` and the header `Profile` action now open the full client detail page instead of dead-ending in edit-only flows.
- Expanded client-site creation to collect site name, address, and city, and hardened layout persistence by replacing the failing `System.Web.Script.Serialization` path with `DataContractJsonSerializer`.

## 1.0.95.0 - 2026-05-30

- Applied page-local wording and workflow cleanups in Payments, Inventory, Purchases, Contracts, and Reports without changing schema or module structure.
- Renamed generic toolbar actions such as `Import`, `Template`, `Forms`, `More`, and `Review` to clearer labels like `Import Excel`, `Excel Template`, `Service Forms`, `More Actions`, and `Prepare Renewal`.
- Added next-step save guidance in operational pages so users are told what to do after recording a payment, saving a material item, duplicating a PO, opening a new PO, or saving a contract.

## 1.0.94.0 - 2026-05-30

- Fixed high-friction error handling across import, invoice, payment, purchase, inventory, dispatch, contract, and reports so users now see recovery guidance instead of raw exception text or dead-end status labels.
- Updated deferred-load failures in key operational pages to keep the page visible and tell users to refresh or review their inputs instead of exposing technical `Load error` messages.
- Smoothed common failure paths such as invoice email/preview/credit note, payment save/delete, purchase RFQ import and PO actions, inventory stock transfer/import, dispatch assignment, contract invoice generation, and report export.

## 1.0.93.0 - 2026-05-30

- Improved ease-of-use in the highest-traffic operational screens by renaming generic action buttons to clearer business labels such as `Filter Clients`, `Filter Vendors`, `Filter Jobs`, `View Reports`, `Excel Template`, and `Service Forms`.
- Added reusable workflow and empty-state guidance through shared UI helpers so grids and no-data states now guide operators toward the next action instead of stopping at `No records found`.
- Added next-step save guidance for jobs, clients, and vendors, and replaced raw operational failure text in the touched workflows with calmer recovery messaging.

## 1.0.92.0 - 2026-05-30

- Replaced the old Master Data map-first Excel workflow with a shared automated ingestion pipeline that reads messy workbooks, detects the right data type, auto-maps headers, cleans values, and stages canonical import data.
- Added transactional import execution with row-level savepoints so unsafe rows roll back cleanly while safe rows still import, and missing clients, vendors, sites, and technicians are auto-created or refreshed where safe.
- Added import batch diagnostics using the existing `DataImportBatches` and `DataImportErrors` tables, with user-friendly import summaries and simplified Master Data copy centered on automatic Excel import.

## 1.0.91.0 - 2026-05-30

- Added a shared global status editor for status-bearing grids so users can double-click a status cell and change it from a small context picker instead of opening the full record.
- Added reusable status option detection and guarded save handling for service desk incidents, employee jobs, employee attendance, recent invoices, and recent quotations.
- Patched invoice and quotation dashboard grids so status cells no longer trigger PDF-open clicks before the status editor can appear.

## 1.0.90.0 - 2026-05-30

- Made `Fully Received` purchase orders count as payment-completed across payables, overdue PO checks, vendor summaries, dashboard command-center data, and support-center operations output.
- Updated the purchase receipt action to save `Fully Received` with `PaidAmount = TotalAmount` so old overdue/vendor payable screens immediately stop treating received POs as unpaid.
- Added a guarded startup data repair that sets old `Received`, `Fully Received`, `Paid`, and `Closed` purchase orders to fully paid without changing schema.
- Added a data-quality smoke check for the fully received purchase-order payment rule.

## 1.0.89.0 - 2026-05-30

- Added typed UI exception classes for validation, database, navigation, and file-processing failures.
- Added a central `Logger` facade that writes to `LOGS\servoerp_errors.log` and the existing app log without risking a secondary crash.
- Strengthened the global crash handler to track the last clicked UI action, log every caught screen failure, restore button state in safe actions, and show calmer typed user messages.
- Added a reusable safe page-load helper that renders an inline refreshable error state instead of leaving a blank screen.
- Updated Error Log Viewer to include the central `servoerp_errors.log`.
- Added UI error-handling smoke tests for central logging, safe action button restoration, and inline load errors.

## 1.0.88.0 - 2026-05-30

- Fixed a Clients screen-action error after saving client address details by making the post-save refresh path safe for both dashboard and detail layouts.
- Added null-safe client input cleanup and capped generated geocode address text to the database-supported length.
- Added a smoke check that verifies a client address save refresh from the dashboard path does not throw a screen-action error.

## 1.0.87.0 - 2026-05-30

- Added a full navigation click smoke test covering sidebar routes 0-19, the retired Service Desk form, Client Detail, and Job Detail.
- Expanded UI QA coverage to include SLA Dashboard, retired Service Desk, and Tally route entries.
- Fixed quotation dashboard quick-action button sizing so the action row stays aligned under UI smoke checks.
- Fixed Dispatch Center employee classification so DCS Officer field staff are included while office/accounting profiles remain excluded.
- Tightened UI smoke diagnostics so chart/dashboard cards with filters are not mistaken for editor field containers.

## 1.0.86.0 - 2026-05-30

- Added a shared navigation helper so visible records can open the most specific ServoERP destination available from any form, dialog, or card.
- Wired Dashboard recent activity, Global Search results, Notification Center items, and Client Detail activity/site timelines to clickable record navigation.
- Fixed recent payment activity to navigate with the Payment ID instead of the linked invoice ID.

## 1.0.85.0 - 2026-05-30

- Swapped Dispatch Center layout so the Technicians board takes the left-side primary position and Job Queue moves into the central operations stack above map and timeline.

## 1.0.84.0 - 2026-05-30

- Updated Dispatch Center technicians to show all active non-office employees as dispatch-ready field staff instead of only narrow technician-designation matches.
- Added a designation filter to the Dispatch Center technician board and applied it to technician cards, assignment choices, suggestions, KPIs, and timeline rows.

## 1.0.83.0 - 2026-05-30

- Fixed recurring Jobs screen load failures on client PCs by making startup verify the full Jobs workflow schema before skipping database migration.
- Added guarded schema repair for Jobs checklist, parts-used, activity-log, and checklist-template columns so older client databases can self-heal on startup.

## 1.0.82.0 - 2026-05-30

- Fixed the Master Data Inventory upload card so it opens a real map/validate/import workflow instead of the placeholder mapping message on client PCs.
- Added Inventory to the bulk import, template, and drag-drop routing lists with auto-mapping for stock list columns such as item name, quantity, unit, rate, and reorder level.

## 1.0.81.0 - 2026-05-30

- Reworked client/server setup language and generated packages around a dedicated always-on office server PC instead of a personal laptop host.
- Added local SQLite fallback diagnostics under `C:\HVAC_PRO_MSE\DATABASE\ServoERP_Fallback.sqlite` to record SQL Server availability and outage context without allowing split business writes.
- Updated connection setup, support-center database checks, and client setup exports to include the always-on server role and SQLite fallback status.

## 1.0.80.0 - 2026-05-30

- Added shared Tetris-style packing for dashboard card rows so card-only FlowLayoutPanel dashboards automatically fill horizontal gaps.
- Re-runs the shared card packer after page load, resize, drag reorder, and card resize while preserving existing card data bindings and saved card order.

## 1.0.79.0 - 2026-05-30

- Tightened the Quotation Dashboard layout so KPI cards and dashboard rows fill the available page width instead of leaving unfinished gaps.
- Made single-row quotation dashboard cards expand across the row while preserving existing card content, chart bindings, resize grips, and saved card order.

## 1.0.78.0 - 2026-05-30

- Fixed the Payments Management Cash Flow Trend card so it keeps a usable minimum width after dashboard card resizing is enabled.
- Made the Cash Flow Trend legend, date filter, and chart area resize responsively instead of clipping into the Receipts vs Payments card.

## 1.0.77.0 - 2026-05-29

- Added a shared modern main-content scroll host so module pages scroll as one unified area while the sidebar, top banners, and support drawer remain fixed.
- Added mouse wheel and keyboard scrolling for the main content area, with per-screen scroll position memory and reset-to-top behavior for transient detail pages.
- Added `LOGS\ScrollAudit.txt` logging to flag vertical overflow as scroll-needed and horizontal overflow as a layout issue.

## 1.0.76.0 - 2026-05-29

- Added shared UI comfort handling for cramped action button grids so compact button tables reflow away from awkward empty slots and narrow columns.
- Improved global button/text readability by expanding fixed labels where space exists, keeping meaningful buttons from clipping, and preserving icon/pager buttons as compact controls.
- Extended the enterprise UI smoke test to fail on clipped buttons and awkward action grids across parent pages and subforms at 1366x768 and 1920x1080.
- Standardized shared UI typography for labels, buttons, checks, radios, groups, and tabs, while forcing white text on dark filled boxes/buttons for readable contrast.

## 1.0.75.0 - 2026-05-29

- Added a global input outline service so textboxes, dropdowns, date fields, numeric fields, and compact input hosts keep a visible soft border across parent pages and subforms.
- Updated shared BaseForm/BaseUserControl and UIHelper styling passes so subforms receive the same input-outline correction after older borderless styling runs.
- Added a smoke-test guard that fails if visible textboxes/dropdowns/numeric fields are left borderless without an outlined host.
- Tightened quotation dashboard card headers and prevented global card grips from attaching to internal quotation tile header/content panels.
- Made bottom-edge height grips global across resizable cards and quotation dashboard tiles, with a smoke-test guard requiring both bottom height and corner resize grips on card surfaces.
- Introduced `CardSurfacePolicy` as the shared card/exclusion layer used by dashboard layout, global card context menus, and smoke verification.
- Centralized card grip names, drawing, positioning, and grip verification in `CardResizeGripService` so custom card classes no longer carry private grip rules.
- Tightened Dispatch Center readability by widening technician cards, improving technician list view, giving Quick Actions a cleaner two-column layout, and keeping job queue right-side labels aligned on resize.

## 1.0.74.0 - 2026-05-29

- Added `SubFormRegistry.txt`, generated from form construction and show/navigation calls, to keep parent pages and child dialogs mapped during future changes.
- Added global crash protection with friendly user messages, detailed `LOGS\CrashLog_yyyyMMdd.txt` logging, recurring failure tracking, and duplicate-click suppression for buttons.
- Added shared DataGridView `DataError` protection through `BaseForm` and `BaseUserControl` so grid formatting issues are logged instead of crashing screens.
- Added `SafeGet` null-safe conversion helpers and `DbHelper.SafeExecute()` / `ConnectionHealthCheck()` for guarded database work.
- Added a Settings > Help & Support > View Error Logs popup for reading and clearing local crash logs.
- Wrote the child-form retrospective audit report under `LOGS\ChildFormAudit_*.txt`.
- Fixed the global card grip/card-menu detector so transaction editor fields in quotations, invoices, payments, and purchases are no longer treated as draggable dashboard cards.
- Removed floating Reset layout launchers from the shell and page-level dashboard layout service, while keeping reset actions available from Settings/support tools.
- Added a bottom-edge height grip to card resize handling so real dashboard/module cards can be stretched taller or shorter by dragging up/down.

## 1.0.73.0 - 2026-05-29

- Tightened the global layout audit so utility controls, managed layout internals, and mixed form-field panels are logged without being counted as empty page gaps.
- Added broad `/smoketest` layout auditing at both 1366x768 and 1920x1080 for all known module pages.
- Fixed Tally Integration page construction by lazy-building heavy tab contents after the page is hosted.
- Fixed quotation summary label ownership so dashboard metric updates cannot overwrite the Quote Summary heading.

## 1.0.72.0 - 2026-05-29

- Added a global recursive layout audit service that logs before/after control geometry to `C:\HVAC_PRO_MSE\LOGS\LayoutAudit.txt` and `C:\HVAC_PRO_MSE\LOGS\LayoutAuditAfter.txt`.
- Applied global layout tightening for card-like panels, GroupBoxes, FlowLayoutPanels, TableLayoutPanels, and DataGridViews without changing data bindings or business logic.
- Set DataGridViews to stretch with their parent screens and fill available columns so tables use the available workspace.
- Added global open-form auditing so direct WinForms dialogs are covered in addition to shared dashboard pages.

## 1.0.71.0 - 2026-05-29

- Made the dashboard command bar shorter and cleaner so it uses less vertical space.
- Repositioned and reduced the Reset layout launcher so it no longer floats below the top bar or overlaps page actions.
- Added responsive dashboard top-bar behavior so date/time shortcuts hide on tighter widths instead of colliding with action buttons.
- Tightened the dashboard language selector and right-side controls so labels remain readable after DPI scaling.

## 1.0.70.0 - 2026-05-29

- Added the in-app Agent Simulation system from Settings with a live progress panel, pause/resume, one-day tick, report generation, and tracked cleanup.
- Added a global `[AGENT SIM]` top status bar showing simulated day, date, quotation/invoice/payment/purchase counts, and score.
- Added isolated `[AGENT]` data generation for clients, sites, vendors, inventory, quotations, jobs, invoices, payments, purchases, and PDF previews under `C:\HVAC_PRO_MSE\outputs`.
- Added crash recovery and report/state persistence under `C:\HVAC_PRO_MSE\LOGS\AgentState.json` and `AgentReport_*.txt`, with cleanup limited to exact tracked IDs.

## 1.0.69.0 - 2026-05-29

- Added a global recursive text-fit policy for buttons, tab controls, and filter/tab button rows.
- Disabled button ellipsis globally and measured labels with `TextRenderer.MeasureText()` so button minimum widths fit full captions.
- Enabled wrapping and width-first growth for left-to-right/right-to-left button flow panels so filters move to a second row only when horizontal space is truly unavailable.
- Expanded fixed tab item widths dynamically so long tab labels such as Skills & Certifications are fully visible.

## 1.0.68.0 - 2026-05-29

- Fixed Employees header actions so Delete, Import, Forms, Template, Export, WhatsApp, New Employee, and Save stay in one clean row without hiding the selected-employee status.
- Fixed Materials / Procurement header actions so Export, Import, Forms, and Add Item no longer wrap into a second row or overlap the KPI area.
- Replaced fragile fixed-width FlowLayoutPanel action rows with explicit right-side header action hosts for stable scaling.

## 1.0.67.0 - 2026-05-29

- Added a reusable `DraggableCard` panel for new dashboard cards with grid-snapped drag behaviour and the shared resize grip.
- Added a global dashboard layout layer that attaches drag, resize, grid snap, auto-reflow, and Reset Layout support to card-like Panels, GroupBoxes, and ResizableCards across loaded screens.
- Added local JSON layout persistence under `%LOCALAPPDATA%\ServoERP\layouts\{Screen}Layout.json` so card positions, sizes, order, and table cells restore on next launch.
- Updated the shared resize grip artwork to the requested bottom-right dotted grip style.

## 1.0.66.0 - 2026-05-29

- Locked the update version endpoint to the production ServoERP URL `https://servoerp.in/version.txt`.
- Made the Settings update URL field read-only so client PCs cannot accidentally save a wrong update endpoint.
- Forced Settings save/check flows to restore the production update URL before running update checks.
- Hardened update version parsing so the live version file is accepted even when served with a UTF-8 BOM.

## 1.0.65.0 - 2026-05-29

- Removed the floating shell/sidebar AI Copilot launcher so it no longer appears in the wrong place.
- Added the AI Copilot launch action inside Settings under the ServoERP Assistant card beside the assistant health check.

## 1.0.64.0 - 2026-05-29

- Added live resize grip support to the shared card resize service so dock-filled dashboard cards can update their parent layout while dragging.
- Added resize grips to the Dispatch Center technician board, individual technician cards, and Today's Schedule Overview.
- Updated Dispatch Center row resizing so those panels visibly grow or shrink instead of staying locked inside fixed table rows.

## 1.0.63.0 - 2026-05-29

- Added a repeatable visual clipping audit that opens the Release build, visits main pages, captures screenshots, and exports hidden/clipped button and card findings.
- Fixed global card resize grips so the grip no longer gets detected as a card and expands over card content.
- Reduced the Help & Support drawer width, wrapped its navigation in drawer mode, and auto-closed it when navigating away from Settings so operations pages do not get squeezed.
- Improved Settings card reflow for narrow drawer layouts and stopped card body panels from receiving unwanted inner auto-scroll bars.
- Increased Clients dashboard middle section height so Quick Actions and Smart Alerts are visible together.
- Increased Inventory Quick Actions height so Update Quantity, Ordering Plan, Open Actions, and Delete Item remain visible.

## 1.0.62.0 - 2026-05-29

- Moved the Help & Support entry point into Settings as a dedicated General settings card.
- Wired the Settings Help & Support button to open the same right-docked support drawer in the main shell.
- Hid the old sidebar Help & Support launcher so support access now lives under Settings.

## 1.0.61.0 - 2026-05-29

- Changed Help & Support from a centered modal window to a right-docked slide-in drawer hosted inside the main application shell.
- Reflowed the main content area while the support drawer is open so the drawer sits flush against the right edge without floating over the left or center of the workspace.
- Added an in-drawer close action for the embedded support center.

## 1.0.60.0 - 2026-05-29

- Replaced harsh black app-wide border tokens with soft light gray `Color.FromArgb(220, 220, 220)` for panels, cards, buttons, grids, and shared controls.
- Updated DataGridView grid lines and border policy tests to preserve the softer 1px outline standard.
- Softened Modern ERP control borders, ModernTextBox focus outlines, and custom quotation border pens that bypassed shared tokens.
- Applied the shared input/card styling pass from base forms and user controls so newly opened pages inherit the soft border policy automatically.

## 1.0.59.0 - 2026-05-29

- Added a global bottom-right resize grip for card panels using MouseDown, MouseMove, and MouseUp drag handling.
- Applied the resize pattern to the Jobs editor cards, including the Technician panel, with dragged card heights preserved by the Jobs custom layout.
- Kept existing Report dashboard ResizableCard behavior intact while extending plain Panel-based cards across the app.

## 1.0.58.0 - 2026-05-29

- Fixed the Jobs form Job details layout so the Client selector is visually placed above the optional Site selector in the editor.
- Preserved optional site behaviour and the existing Add Site action beside the site dropdown.
- Added a guarded nightly build-and-deploy PowerShell script with corrected ServoERP workspace defaults, Amsterdam 23:30 time gate, recursive deployment backup, and rollback support.

## 1.0.57.0 - 2026-05-29

- Expanded Dispatch Center technician detection from designation-only matching to a centralized field-role classifier that includes HVAC/AC technicians, fitters, electricians, operators, supervisors, helpers, assistants, trainees, DCS officers, and skilled/semi-skilled field staff while excluding accounts, HR, admin, sales, purchase, finance, billing, and stores roles.
- Sorted technician rosters by dispatch role priority so core technician positions appear first in Dispatch Center cards and assignment dropdowns.
- Kept live employee technicians visible even when Dispatch falls back to sample jobs, preventing the roster from being replaced by six sample technicians.
- Added visible role labels on Dispatch technician cards and test coverage for technician classification and sorting.

## 1.0.56.0 - 2026-05-29

- Standardized app-wide card, panel, input, table, and secondary-action outlines on black design-system border tokens.
- Patched operational outliers across Jobs, Clients, Contracts, Invoices, Payments, Quotations, Purchases, Inventory, Vendors, Employees, Payroll, Reports, Settings, and detail pages that bypassed shared border tokens.
- Added UI policy coverage to keep shared border tokens and secondary action borders black in future builds.
- Captured and visually checked all 18 department/detail pages after the change, with extra full-size checks on Jobs, Purchases, Quotations, Invoices, Inventory, and Payments.

## 1.0.55.0 - 2026-05-29

- Audited delete coverage across core modules and standardized more user-facing delete actions on the guarded confirmation workflow.
- Added payment deletion from payment history with invoice paid amount, balance, and status recalculation after removal.
- Added inventory item delete as a soft archive using guarded StockItems.IsActive schema support so historical jobs, purchases, invoices, and movements stay preserved.
- Updated client dashboard delete to use the ClientService soft-delete path and shared delete confirmation instead of an unguarded inactive update.

## 1.0.54.0 - 2026-05-29

- Fixed Jobs dashboard delete so dashboard-mode rows no longer call detail-list refresh/status controls that are not mounted.
- Added null-safe Jobs selection/status guards around delete refresh paths to prevent Object reference failures after a row is removed.

## 1.0.53.0 - 2026-05-29

- Upgraded client billing address entry to a larger multiline field in the client editor and exposed it in client detail, search, and CSV export views.
- Applied client registered address rendering to invoice and quotation previews while still showing optional site/site address details when selected.
- Reused the existing guarded B2BClients.BillingAddress schema support; no new database column was required.

## 1.0.52.0 - 2026-05-29

- Enlarged the New Quotation editor dropdowns, input text, row spacing, and dropdown item height so client, site, validity, status, and workflow selections are easier to read and click.
- Applied the same sizing to quotation line-item combo editors so item and supplier dropdowns no longer feel cramped while building quotations.

## 1.0.51.0 - 2026-05-29

- Improved inner-page guidance for Inventory, Purchases, Quotations, Payroll, Settings, Reports, WhatsApp, Support, Clients, Jobs, Contracts, and Invoices after the full user journey QA pass.
- Clarified optional site/project/location behaviour across client-owned workflows so jobs, invoices, contracts, purchases, and quotations no longer look blocked when a site is not yet decided.
- Added workflow guide strips and helper copy for dense forms so required fields, optional follow-up fields, and next actions are easier for Indian HVAC operators to understand.
- Tightened discoverability for report tiles, WhatsApp filtering, support escalation context, and payroll salary validation without changing database schema or guarded business flows.

## 1.0.50.0 - 2026-05-29

- Audited inner save/import pages for stale mandatory-site behaviour after the client-level record workflow change.
- Fixed Jobs import preflight and header validation so SiteName is optional while still validating it when supplied.
- Cleaned inner-page copy and neutral selections in Service Desk, Invoices, and Purchases so optional site/project fields no longer look mandatory.
- Tightened Excel import row validation for quotations, invoices, payments, purchases, and jobs to block blank titles, zero payments, and empty purchase lines before data is saved.

## 1.0.49.0 - 2026-05-29

- Made service site selection optional across jobs, invoices, quotations, contracts, purchases, and Excel job imports so client-level records can be saved before the exact site is known.
- Added a Jobs page Add Site action beside the site dropdown, allowing a new client site to be created and selected without leaving the job editor.
- Added guarded database migration support to make existing SiteID references nullable for affected transaction tables.

## 1.0.48.0 - 2026-05-29

- Redesigned the ServoERP Legal Agreements window with a branded header, India-first trust badge, bordered reading surface, and clearer action bar.
- Added owner-drawn agreement tabs and richer legal text formatting for headings, sections, and bullet points without changing legal content.
- Improved accept-button visual state so acceptance is visibly gated until the checkbox is selected.

## 1.0.47.0 - 2026-05-28

- Added a global button alignment normalizer for repeated action areas across forms, module pages, dialogs, flow layouts, and table layouts.
- Standardized grouped button height, center text alignment, padding, margins, and row alignment while preserving existing button colors and commands.
- Added smoke-test coverage that fails when visible button rows in instantiated pages have mismatched top alignment or height.
- Corrected the Geo Intelligence Filter/Date action-row alignment discovered by the new button alignment check.

## 1.0.46.0 - 2026-05-28

- Consolidated dashboard department cards onto the global card context-menu system to prevent duplicate right-click paths.
- Added working dashboard recent-activity Copy as path and Properties actions for support and QA traceability.
- Replaced Debug-only dashboard Cut and Share handlers with clipboard-safe support metadata actions.

## 1.0.45.0 - 2026-05-28

- Made global card context menus more support-ready with working Copy as path and Properties actions.
- Added stable `servoerp://card/...` paths for cards so QA and support can identify the exact screen element.
- Added card metadata properties for title, page, key, control type, size, and path without changing business records.

## 1.0.44.0 - 2026-05-28

- Added a global Windows-style card context menu so right-click card actions are available beyond the dashboard.
- Wired global card-menu attachment through shared forms, user controls, deferred pages, and design-system cards.
- Preserved guarded module delete flows by keeping global Cut, Copy, Delete, Rename, and Share actions non-destructive.
- Added UI smoke coverage to verify detected module cards receive the global context-menu hook.

## 1.0.43.0 - 2026-05-28

- Expanded language switching from dashboard-only labels to a global control-tree refresh across open forms and module pages.
- Added generic translation refresh for labels, buttons, group boxes, tab pages, grid headers, combo box items, menus, and tool strips.
- Applied language refresh automatically to dynamically added controls in shared base forms and deferred module pages.
- Added common Marathi and Hindi keys for app-wide commands, Settings, Jobs, compliance, and list-view labels.

## 1.0.42.0 - 2026-05-28

- Added Module Catalog for installed modules and extension-ready roadmap review.
- Added Compliance Export Pack to generate local legal, license, module, and readiness ZIP files.
- Added saved Jobs dashboard views for search, status, and type filters via UserSettings.
- Added Jobs Workflow Board for Kanban-style field-service pipeline review.

## 1.0.41.0 - 2026-05-28

- Added Open Source & License Center for third-party component review and disclosure export.
- Added component metadata for EPPlus, BCrypt.Net-Next, WebView2, iTextSharp, and .NET Framework.
- Added Settings access for client IT, procurement, and audit review without exposing business data.

## 1.0.40.0 - 2026-05-28

- Added client-owned automated SQL backups with network, local folder, and external drive fallback.
- Added BackupLog persistence and default backup preferences in UserSettings.
- Added Backup & Recovery settings UI with UNC testing, schedule, retention, manual backup, status, and log review.
- Added non-blocking toast notifications for background backup results.
- Added scheduled end-of-day backup checks, dashboard Backup Now shortcut, and best-effort backup on app close.

## 1.0.39.0 - 2026-05-28

- Added first-launch legal agreement acceptance before ServoERP opens the main dashboard.
- Embedded EULA, Privacy Policy, Data Processing Policy, and Disclaimer text directly in the app.
- Added a read-only Legal Agreements viewer in Settings for later review.
- Persisted LegalAccepted in UserSettings after the user accepts the agreements.

## 1.0.38.0 - 2026-05-28

- Added an English, Marathi, and Hindi language selector to the dashboard top bar.
- Added persisted per-user language selection through the guarded UserSettings table.
- Added LanguageManager with dashboard/navigation/common-control translations and Devanagari-capable font switching.
- Wired base forms and module user controls to refresh when the selected language changes.

## 1.0.37.0 - 2026-05-28

- Added Tally Prime integration for voucher export, master import, and stock item sync through a dedicated Tally page.
- Added guarded Tally mapping, export status, import batch, and sync log schema additions for clients, vendors, stock items, invoices, payments, and purchases.
- Added Tally XML generation for sales, receipt, purchase, supplier payment, and stock item master payloads, with direct HTTP push or offline XML export.
- Added Tally master pull/import flow to map ledgers and stock items back into ServoERP.

## 1.0.96.0 - 2026-05-30

- Fixed dashboard/client layout persistence runtime failures by moving saved page layout JSON handling off the failing `System.Web` serializer path.
- Corrected ServoERP's GSTIN sample values so client entry, placeholders, import samples, and smoke data all use a checksum-valid example instead of the app's previous invalid sample.
- Fixed the Clients workflow so `View client` and the header `Profile` action now open the full client profile page, allowing site management from the main client journey.
- Expanded the client-site creation prompt to capture site name, address, and city in one step instead of creating half-empty site records.

## 1.0.36.0 - 2026-05-28

- Added Materials-to-Purchase Request flow so a selected material can create a draft supplier request with vendor, quantity, rate, and line-item details.
- Added monthly P&L Excel export in Reports with revenue, purchases, salaries, expenses, profit, and margin for the last 12 months.
- Cleaned user-visible stock-risk wording across reports, jobs, quotations, and material validation to match procurement planning.

## 1.0.35.0 - 2026-05-28

- Reframed dashboard materials signals around procurement readiness with To Order and Priced Items metrics instead of warehouse stock alerts.
- Updated ServoERP Assistant material prompts and context so it discusses vendor ordering plans instead of low-stock warehouse alerts.
- Added renewal countdowns to contracts table, sidebar cards, and export output so expiring AMCs are visible before revenue is lost.
- Added a due-age signal to the jobs dashboard table so delayed field work is visible at a glance.
- Fixed installed-version reporting so Settings and support metadata read the running assembly version instead of stale config text.

## 1.0.34.0 - 2026-05-28

- Removed the Ollama local-model dependency from ServoERP Copilot.
- Replaced the AI provider path with a built-in ServoERP Assistant that uses ERP context, deterministic guidance, and preview-only suggested actions.
- Updated Settings and Copilot messaging so users no longer see Ollama, model, endpoint, or local-server setup instructions.

## 1.0.33.0 - 2026-05-28

- Reframed Inventory as a procurement catalog for ordering materials from vendors and suppliers.
- Removed warehouse-style out-of-stock criteria, filters, badges, alerts, and export/report wording.
- Replaced stock-alert language with supplier-ordering states: To Order, Vendor Linked, and Needs Vendor.
- Replaced inventory valuation KPI with Priced Items so the page focuses on supplier catalog readiness.

## 1.0.32.0 - 2026-05-28

- Moved AI Copilot and Help & Support launchers into the sidebar footer area so they no longer cover module content or bottom-right actions.
- Made payroll empty-month status explicit by showing the active employee count and the Run Payroll next step.
- Opened invoices on the dashboard/list experience instead of landing users directly in an unsaved draft.
- Kept inventory alerts on the existing low-stock and out-of-stock model without adding separate negative-stock criteria.
- Added WhatsApp Hub missing-phone empty state and tooltips for icon-only chat actions.

## 1.0.31.0 - 2026-05-28

- Fixed the login password visibility icon state so hidden passwords show the crossed-eye state and visible passwords show the open-eye state.
- Improved the dashboard top-bar user identity layout so the Administrator name fits without being clipped.
- Aligned the installer version include with the application assembly version.
# 1.1.15 - 2026-06-11

- Added GitHub Actions desktop release pipeline for Release builds, artifact upload, Velopack packaging, and GitHub Releases publishing.
- Added CI version stamping with semantic versions and unique patch versions per workflow run.
- Added Velopack publish preparation that excludes client-owned configuration, license, database, log, and output files from update packages.
- Hardened ServoERP update flow so background update checks/downloads do not restart the app without user confirmation.
- Added Settings controls for current version, update checks, automatic update download preference, and last update status.
- Added CI/CD and auto-update operating documentation.
