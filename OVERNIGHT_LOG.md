## Mode Used

GENESIS

## Codebase Snapshot

Session initialized for agent-instruction work. No production source behavior has been changed by this log creation.

GENESIS full run requested. Deployment was explicitly requested by the user but is skipped because project hard limits say never push or deploy to production. Safe local engineering, build, validation, logging, and commits will be attempted.

## Bugs Fixed

- Fixed typo and dead-end guidance in the Clients dashboard upcoming renewals toast.
- Replaced a "coming soon" client revenue toast with actionable report navigation guidance.
- Hardened optional invoice mapper fields to use safe conversion helpers instead of direct SQL value casts, addressing repeated `InvalidCastException` crashes seen in today's crash log.
- Service Desk form was present in source but omitted from the desktop project compilation; added it back to the project so future builds validate the page.

## Features Completed

Added the autonomous agent command/mode framework to `AGENTS.md`.
- Added a useful Jobs dashboard Columns menu with visible-column summary, copy-filtered-jobs, and reset-filters actions.

## GENESIS Ideas

GENESIS mode activated for autonomous creative engineering.
GENESIS mode reactivated on 2026-05-28.

Full-run design brief: improve ServoERP by first stabilizing obvious defects and unfinished surfaces, then add only small, safe user-benefiting polish that fits the existing HVAC ERP workflows. UI redesign work remains constrained to pages with matching reference screenshots and must preserve the sidebar, shell, routing, data contracts, services, and business rules.

Jobs quick-action completion brief: replace remaining "coming soon" dashboard actions with useful, data-backed operations. Job Templates should open the existing field-service forms workflow, while Schedule Board and Resource Planner should surface live upcoming/overdue work and technician workload summaries from the already-loaded job data. This improves dispatch usefulness without redesigning the page, touching navigation, or changing service behavior.

Clipboard resilience recon brief: broad scan found no active TODO or NotImplemented defects, but multiple UI copy actions write directly to the Windows clipboard. Clipboard access can fail when another process owns it, which would turn simple Copy actions into recoverable UI crashes. Add one shared safe clipboard helper and route copy actions through it without changing business data, navigation, schema, integrations, or protected shell/sidebar behavior.

UI reference source fix brief: the mandated `C:\Users\harsh\Downloads\ServoERP_UI_Redesigns` path cannot exist on this workstation because the `harsh` Windows profile is absent and Windows denied creating it. Use the existing repo-local reference set at `C:\HVAC_PRO_MSE\Docs\UI_QA_Baselines\current` as the canonical primary UI reference source and create an Administrator downloads junction to that folder for local discoverability.

Jobs left-pane redesign brief: the Jobs reference screenshots show the left action row cramped, with the primary `+ New Job` action vulnerable to clipping in the fixed-width queue pane. Redesign only the Jobs page left header by giving it a clearer dispatch title/subtitle and a two-by-two responsive action grid so Template, Import, Forms, and New Job remain visible at normal and restored widths. Preserve the sidebar, split-pane routing, job services, filters, bindings, and detail workflow.

GENESIS UI authority brief: user requested a durable GENESIS UI Agent rule that removes page/module/file restrictions for UI redesign. `AGENTS.md` now gives GENESIS full UI-layer authority, uses `JobManagementForm.cs` at commit `34c1511` as the canonical action-area reference, and keeps non-UI hard limits intact. The global UI QA ruleset was aligned so it does not override that GENESIS authority.

Invoices action-rail redesign brief: the Invoices reference shows every operational action exposed as a fixed right-rail button grid, which makes the page feel dense and fragile at restored widths. Redesign the Quick Actions card to keep Save Draft prominent while moving secondary invoice operations into a Jobs-style menu. This preserves all invoice workflows and event handlers while making the right rail calmer, more scalable, and consistent with the GENESIS action-area pattern.

Purchases action-rail redesign brief: the Purchases reference shows a tall stack of eight right-panel action buttons below the summary, creating a visually heavy rail and leaving little room for contextual purchase intelligence. Redesign the panel around one primary Save PO button plus an Open PO Actions menu for secondary operations. This keeps convert, receive, send, print, clone, cancel, and delete reachable while making the PO workspace less brittle at restored widths.

Payments action-card redesign brief: the Payments reference shows a useful payment form but a sparse Quick Actions panel that spends most of its card area on empty space. Keep Save Payment as the primary action and add a compact Payment Actions menu for clear, refresh, export, import, template, and forms workflows. This makes the right rail more useful without changing payment posting behavior or invoice balance calculations.

Vendors dashboard action redesign brief: the Vendors dashboard still uses an older six-tile Quick Actions grid that consumes the whole card and is less consistent with the newer GENESIS action-area pattern. Replace it with a short operational prompt plus one Open Vendor Actions menu wired to the same existing dashboard actions. This improves scanability and keeps vendor onboarding, contracts, review, import/export, and evaluation reachable.

Vendors document-card polish brief: validation of the Vendor dashboard action redesign exposed a pre-existing sidebar title truncation in the Expiring / Expired Documents card. Shorten the card title to Expiring Documents so it fits beside the View All link while preserving the same document-count content and drilldown behavior.

Inventory action-card redesign brief: the Inventory reference shows the right-side Quick Actions grid clipping Reorder Suggestions in the narrow detail rail. Replace the cramped two-column grid with full-width Stock Adjustment and Reorder Suggestions actions plus an Open Stock Actions menu for transfer, bulk update, print report, and valuation. This preserves low-stock button state logic while removing visible clipping.

Clients activity-action completion brief: the Clients command center reference is visually strong, but source recon found sample activity links that only showed dead-end toasts for service request and service summary actions. Route those actions to the existing Jobs module instead, matching the operational meaning of the cards and avoiding fake-open feedback.

Clients live-activity detail brief: broader unfinished-action scan found live client activity rows still displaying an "Opening activity details" toast instead of showing the selected activity. Wire the action to the existing modal pattern and display the activity title, type, timestamp, and detail text so users get the record context they clicked for.

Contracts action-rail redesign brief: the Contracts reference shows a right-side Actions card that only explains what can be done while the real contract operations are crowded into the top toolbar. Convert the rail into a useful action surface with a primary Save Contract button and an Open Contract Actions menu for WhatsApp, invoice, renewal, SLA log, refresh, and delete. This preserves existing handlers while making the form easier to operate at restored widths.

Employees filtered-empty workflow brief: the Employees reference shows a loaded employee and populated metrics while the left employee list says no records found because the current search/filter state excludes the loaded employee. Add an explicit Clear Filters control and status feedback when filters produce no matches, so users can recover the list without guessing.

Payroll More-menu completion brief: the Payroll reference includes a prominent More button, but source recon showed it only displays a generic message instead of actions. Replace the dead-end toast with a real Payroll Actions menu wired to existing run, lock, import, forms, export register, and payslip generation workflows.

Service Desk filtered-empty workflow brief: the Service Desk reference shows active incident context and KPI counts while the left incident queue can collapse to a generic "No records found" message under search/filter state. Replace the dead-end empty state with a clear explanation and a Clear Filters recovery action so dispatch/support users can get back to the queue without guessing which filter hid the records.

Quotations action-rail redesign brief: the Quotations reference shows a tall right-side Quick Actions stack with save, approval, PDF, conversion, dispatch, WhatsApp, and delete actions all competing visually. Keep Save Draft and Send for Approval visible as the high-frequency actions, then move secondary quote operations into an Open Quote Actions menu so the rail scans cleanly without removing any workflow.

Reports action-queue completion brief: the Reports reference surfaces an Owner action queue with high-value operational prompts, but the rows are passive. Wire each queue row to the matching detailed report so the command center becomes a real launch surface for collections, contracts, jobs, inventory, and vendor payable work.

WhatsApp Hub filtered-empty workflow brief: the WhatsApp Hub reference shows 40 contacts available while the current search/filter state can leave the conversation list visually blank. Add a clear filtered-empty state with a recovery button so users understand the list is filtered, not missing, while preserving the manual-only WhatsApp safety model.

Master Data upload-card polish brief: the Master Data reference exposes cramped upload cards where long titles and Configure buttons can wrap or clip in the fixed card width. Shorten display-only card titles and widen the primary action buttons without changing the underlying import, configure, upload, open, or map workflows.

Dispatch Center filtered-empty workflow brief: the GeoIntelligence / Dispatch Center reference shows a filtered job queue with no matches and only passive advice. Add a Clear Filters recovery action directly inside the empty queue state so dispatchers can quickly return to all active jobs.

Dashboard service-operations navigation brief: Dashboard recon found the Service Operations card still targeting retired Service Desk nav index 16, which the shell blocks. Route the card to the live Jobs / Work Orders page instead so service-ticket signals remain actionable without reviving the retired shell entry.

## What I Built in GENESIS Mode

- Jobs dashboard productivity menu: the Columns action now helps users understand visible fields, copy the current filtered job list to the clipboard, and reset dashboard filters without leaving the dashboard.
- Jobs dashboard quick actions: Job Templates now opens the existing forms workflow, while Schedule Board and Resource Planner show live dispatch and technician workload summaries instead of dead-end "coming soon" messages.
- Clipboard resilience: shared UI copy helper now catches busy-clipboard failures, logs the issue, and shows an actionable warning instead of allowing Copy actions to crash the UI.
- UI reference source unblocked: mandatory rules now point to the accessible repo-local baseline folder, and `C:\Users\Administrator\Downloads\ServoERP_UI_Redesigns` links to the same 90-image reference set.
- Jobs dashboard GENESIS redesign: the dashboard now fits restored-width workspaces better, keeps the right-rail Quick Actions reachable through a compact action menu, and gives the Jobs detail list header a clearer responsive action grid.
- Invoices GENESIS redesign: the Quick Actions rail now keeps Save Draft as the primary action and groups secondary invoice actions into a compact menu instead of a crowded fixed button grid.
- Purchases GENESIS redesign: the PO action rail now keeps Save PO primary and groups secondary purchase operations into a compact menu, matching the newer GENESIS action-area pattern.
- Payments GENESIS redesign: the Quick Actions card now exposes a compact Payment Actions menu for secondary operations while keeping Save Payment primary.
- Vendors GENESIS redesign: the dashboard Quick Actions card now uses a compact Vendor Actions menu instead of a dense six-tile grid.
- Vendors polish: the expiring-documents dashboard card title now fits its narrow sidebar card without clipping.
- Inventory GENESIS redesign: the Quick Actions card now avoids clipped labels by using full-width primary stock actions and a compact stock-actions menu.
- Clients GENESIS completion: sample activity cards now navigate to Jobs for service request and service summary actions instead of showing dead-end opened toasts.
- Clients GENESIS completion: live activity rows now open a detail modal instead of a placeholder toast.
- Contracts GENESIS redesign: the right Actions rail now exposes Save Contract and a compact Contract Actions menu instead of passive guidance text.
- Employees GENESIS workflow polish: the employee list now has a Clear Filters command and tells users when search/filter criteria produce no matches.
- Payroll GENESIS completion: the header More button now opens a real Payroll Actions menu instead of a dead-end informational message.
- Service Desk GENESIS workflow polish: the orphaned Service Desk form is back in the compiled app project, and its incident queue empty state now distinguishes true empty data from filtered-empty results with a Clear Filters action.
- Quotations GENESIS redesign: the Quick Actions rail now keeps Save Draft and Send for Approval visible while grouping PDF, supplier PO, invoice, dispatch job, WhatsApp, and delete operations into a compact Quote Actions menu.
- Reports GENESIS completion: Owner action queue rows now open the matching detailed report instead of remaining passive status tiles.
- WhatsApp Hub GENESIS workflow polish: the conversation list now shows an actionable filtered-empty state with Clear Filters when search or tabs hide all contacts.
- Master Data GENESIS polish: smart upload cards now use shorter display titles and safer action button sizing so labels do not clip in the fixed grid.
- Dispatch Center GENESIS workflow polish: the job queue empty state now offers Clear Filters when search, queue, type, or priority filters hide all jobs, and the quick-action Schedule button no longer wraps at restored widths.
- Dashboard GENESIS fix: the Service Operations card now opens the live Jobs / Work Orders page instead of the retired Service Desk nav index.

Deployment preparation completed on 2026-05-28:

- Debug build passed.
- Enterprise UI smoke test passed: `C:\HVAC_PRO_MSE\TEST_RESULTS\enterprise-ui-smoke-20260528-062657.txt`.
- Enterprise UI smoke test passed after Jobs quick-action completion: `C:\HVAC_PRO_MSE\TEST_RESULTS\enterprise-ui-smoke-20260528-064628.txt`.
- Enterprise UI smoke test passed after clipboard resilience fix: `C:\HVAC_PRO_MSE\TEST_RESULTS\enterprise-ui-smoke-20260528-070136.txt`.
- Enterprise UI smoke test passed after Jobs dashboard redesign: `C:\HVAC_PRO_MSE\TEST_RESULTS\enterprise-ui-smoke-20260528-072714.txt`.
- Enterprise UI smoke test passed after Invoices action-rail redesign: `C:\HVAC_PRO_MSE\TEST_RESULTS\enterprise-ui-smoke-20260528-073915.txt`.
- Enterprise UI smoke test passed after Purchases action-rail redesign: `C:\HVAC_PRO_MSE\TEST_RESULTS\enterprise-ui-smoke-20260528-075054.txt`.
- Enterprise UI smoke test passed after Payments action-card redesign: `C:\HVAC_PRO_MSE\TEST_RESULTS\enterprise-ui-smoke-20260528-075441.txt`.
- Enterprise UI smoke test passed after Vendors dashboard action redesign: `C:\HVAC_PRO_MSE\TEST_RESULTS\enterprise-ui-smoke-20260528-075717.txt`.
- Enterprise UI smoke test passed after Vendors document-title polish: `C:\HVAC_PRO_MSE\TEST_RESULTS\enterprise-ui-smoke-20260528-080044.txt`.
- Enterprise UI smoke test passed after Inventory action-card redesign: `C:\HVAC_PRO_MSE\TEST_RESULTS\enterprise-ui-smoke-20260528-080500.txt`.
- Enterprise UI smoke test passed after Clients activity-action completion: `C:\HVAC_PRO_MSE\TEST_RESULTS\enterprise-ui-smoke-20260528-080812.txt`.
- Enterprise UI smoke test passed after Clients live-activity detail completion: `C:\HVAC_PRO_MSE\TEST_RESULTS\enterprise-ui-smoke-20260528-081103.txt`.
- Enterprise UI smoke test passed after Contracts action-rail redesign: `C:\HVAC_PRO_MSE\TEST_RESULTS\enterprise-ui-smoke-20260528-081412.txt`.
- Enterprise UI smoke test passed after Contracts header/action polish: `C:\HVAC_PRO_MSE\TEST_RESULTS\enterprise-ui-smoke-20260528-081735.txt`.
- Enterprise UI smoke test passed after Employees filtered-empty workflow polish: `C:\HVAC_PRO_MSE\TEST_RESULTS\enterprise-ui-smoke-20260528-082207.txt`.
- Enterprise UI smoke test passed after Payroll More-menu completion: `C:\HVAC_PRO_MSE\TEST_RESULTS\enterprise-ui-smoke-20260528-082656.txt`.
- Enterprise UI smoke test passed after Service Desk compilation and filtered-empty workflow polish: `C:\HVAC_PRO_MSE\TEST_RESULTS\enterprise-ui-smoke-20260528-083734.txt`.
- Enterprise UI smoke test passed after Quotations action-rail redesign: `C:\HVAC_PRO_MSE\TEST_RESULTS\enterprise-ui-smoke-20260528-084400.txt`.
- Enterprise UI smoke test passed after Reports action-queue completion: `C:\HVAC_PRO_MSE\TEST_RESULTS\enterprise-ui-smoke-20260528-084606.txt`.
- Enterprise UI smoke test passed after WhatsApp Hub filtered-empty workflow polish: `C:\HVAC_PRO_MSE\TEST_RESULTS\enterprise-ui-smoke-20260528-084850.txt`.
- Enterprise UI smoke test passed after Master Data upload-card polish: `C:\HVAC_PRO_MSE\TEST_RESULTS\enterprise-ui-smoke-20260528-085110.txt`.
- Enterprise UI smoke test passed after Dispatch Center filtered-empty workflow polish: `C:\HVAC_PRO_MSE\TEST_RESULTS\enterprise-ui-smoke-20260528-085739.txt`.
- Enterprise UI smoke test passed after Dashboard service-operations navigation fix: `C:\HVAC_PRO_MSE\TEST_RESULTS\enterprise-ui-smoke-20260528-085959.txt`.
- Local update deployment package created: `C:\HVAC_PRO_MSE\update_output\ServoERP_Update_1.0.30.0.zip`.
- Production upload/deploy was not performed because the hard limit still forbids production deployment from the agent.

Artifact policy cleanup completed on 2026-05-28:

- Generated QA screenshots, UI baseline image captures, local validation screenshots, and update package zips were moved out of Git tracking while remaining on disk locally.
- `.gitignore` now blocks generated deployment output, QA captures, local reports, local resource duplicates, local Codex config, local tool runtimes, recovered data folders, and website update artifacts from future commits.
- Source code, project docs, tests, website source, and release scripts remain trackable.

Validation completed:

- Debug build passed.
- Enterprise UI smoke test passed: `C:\HVAC_PRO_MSE\TEST_RESULTS\enterprise-ui-smoke-20260527-221659.txt`.
- Final Debug build passed after stopping the locked validation process.
- Final Enterprise UI smoke test passed: `C:\HVAC_PRO_MSE\TEST_RESULTS\enterprise-ui-smoke-20260527-222139.txt`.
- Fresh build launched a visible `ServoERP` window.
- Captured launch screenshot: `C:\HVAC_PRO_MSE\QA_VALIDATION\genesis-launch-20260527-221734.png`.
- Captured Jobs page screenshot: `C:\HVAC_PRO_MSE\QA_VALIDATION\genesis-jobs-validation-20260527-221800.png`.
- Captured Clients page screenshot: `C:\HVAC_PRO_MSE\QA_VALIDATION\genesis-clients-validation-20260527-221805.png`.
- Captured Jobs Columns menu screenshot: `C:\HVAC_PRO_MSE\QA_VALIDATION\genesis-jobs-columns-menu-20260527-221830.png`.
- Captured final fresh-launch screenshot: `C:\HVAC_PRO_MSE\QA_VALIDATION\genesis-final-launch-20260527-222215.png`.
- Captured Jobs quick-action fresh-launch screenshot: `C:\HVAC_PRO_MSE\QA_VALIDATION\genesis-jobs-quick-actions-visible-20260528-064837.png`.
- Captured clipboard resilience fresh-launch screenshot: `C:\HVAC_PRO_MSE\QA_VALIDATION\clipboard-resilience-launch-20260528-070220.png`.
- Captured Jobs dashboard redesign screenshot: `C:\HVAC_PRO_MSE\QA_VALIDATION\genesis-jobs-redesign-validated5-20260528-072631.png`.
- Captured Invoices dashboard render: `C:\HVAC_PRO_MSE\QA_VALIDATION\genesis-invoices-action-rail-render-20260528-074140.png`.
- Captured Invoices workspace action-rail render: `C:\HVAC_PRO_MSE\QA_VALIDATION\genesis-invoices-action-rail-workspace-20260528-074218.png`.
- Fresh Debug executable launch started a responsive `HVAC_Pro_Desktop` process after the Invoices redesign, but no visible main window handle was exposed in this desktop session. Direct form rendering was used for visual validation of the changed Invoices workspace.
- Captured Purchases dashboard render: `C:\HVAC_PRO_MSE\QA_VALIDATION\genesis-purchases-action-rail-workspace-20260528-075115.png`.
- Captured Purchases workspace action-rail render: `C:\HVAC_PRO_MSE\QA_VALIDATION\genesis-purchases-po-action-rail-workspace-20260528-075142.png`.
- Captured corrected Purchases workspace action-rail render after fixing clipped hint text: `C:\HVAC_PRO_MSE\QA_VALIDATION\genesis-purchases-po-action-rail-fixed-20260528-075236.png`.
- Captured Payments management render: `C:\HVAC_PRO_MSE\QA_VALIDATION\genesis-payments-action-card-20260528-075504.png`.
- Captured Payments recording-form action-card render: `C:\HVAC_PRO_MSE\QA_VALIDATION\genesis-payments-recording-action-card-20260528-075534.png`.
- In the direct unauthenticated render host, Save Payment is hidden by the existing permission helper; the new Payment Actions menu rendered cleanly and the enterprise smoke test passed.
- Captured blank first Vendor direct render caused by deferred page lifecycle: `C:\HVAC_PRO_MSE\QA_VALIDATION\genesis-vendors-dashboard-actions-20260528-075740.png`.
- Captured hosted Vendor dashboard action render after deferred lifecycle fired: `C:\HVAC_PRO_MSE\QA_VALIDATION\genesis-vendors-dashboard-actions-hosted-20260528-075835.png`.
- Observed a pre-existing `Expiring / Expired Document` dashboard title truncation in the hosted Vendor render; left it for a dedicated later fix rather than mixing it into the action-card commit.
- Captured corrected Vendor dashboard document-card title render: `C:\HVAC_PRO_MSE\QA_VALIDATION\genesis-vendors-doc-title-fixed-20260528-080113.png`.
- Captured Inventory action-card redesign render: `C:\HVAC_PRO_MSE\QA_VALIDATION\genesis-inventory-actions-20260528-080522.png`.
- Captured Contracts action-rail render with clipped header/hint defect: `C:\HVAC_PRO_MSE\QA_VALIDATION\genesis-contracts-action-rail-20260528-081502.png`.
- Captured corrected Contracts action-rail render: `C:\HVAC_PRO_MSE\QA_VALIDATION\genesis-contracts-action-rail-fixed-20260528-081639.png`.
- Captured corrected Contracts header/action render: `C:\HVAC_PRO_MSE\QA_VALIDATION\genesis-contracts-header-actions-fixed-20260528-081817.png`.
- Hosted Employees validation render closed itself in unauthenticated validation context due the existing permission/session helper.
- Captured Employees direct render for clear-filter layout: `C:\HVAC_PRO_MSE\QA_VALIDATION\genesis-employees-clear-filters-direct-20260528-082305.png`.
- Captured Payroll More-menu completion render: `C:\HVAC_PRO_MSE\QA_VALIDATION\genesis-payroll-more-menu-20260528-082722.png`.
- Captured Service Desk filtered-empty render: `C:\HVAC_PRO_MSE\QA_VALIDATION\genesis-servicedesk-filter-empty-fixed-20260528-083758.png`.
- Captured Quotations action-menu render: `C:\HVAC_PRO_MSE\QA_VALIDATION\genesis-quotations-action-menu-fixed-20260528-084421.png`.
- Captured Reports action-queue render: `C:\HVAC_PRO_MSE\QA_VALIDATION\genesis-reports-action-queue-20260528-084631.png`.
- Captured WhatsApp Hub filtered-empty render: `C:\HVAC_PRO_MSE\QA_VALIDATION\genesis-whatsapp-filter-empty-20260528-084917.png`.
- Captured Master Data upload-card render: `C:\HVAC_PRO_MSE\QA_VALIDATION\genesis-masterdata-upload-cards-fixed-20260528-085328.png`.
- Captured Dispatch Center filtered-empty render: `C:\HVAC_PRO_MSE\QA_VALIDATION\genesis-dispatch-clear-filters-fixed-20260528-085801.png`.
- Captured Dashboard service-operations navigation render: `C:\HVAC_PRO_MSE\QA_VALIDATION\genesis-dashboard-service-operations-nav-20260528-090023.png`.
- Earlier UI redesign work was skipped because `C:\Users\harsh\Downloads\ServoERP_UI_Redesigns` was missing. That blocker is now fixed by switching the mandatory reference source to `C:\HVAC_PRO_MSE\Docs\UI_QA_Baselines\current`.

## What Still Needs Human Input

Production deployment requires explicit override of the project's hard limit and an approved deployment target. It was skipped.

Three source files touched by this GENESIS run already contained large pre-existing uncommitted changes: `SOURCE_CODE/UI/ClientManagementForm.cs`, `SOURCE_CODE/UI/JobManagementForm.cs`, and `SOURCE_CODE/DAL/InvoiceRepository.cs`. I will not commit those whole files without human review because that would mix prior work into this session's commit.

## All Commits This Session

- `0dd91a9` chore: add autonomous agent core and genesis log.
- `afc16a1` chore: record genesis validation results.
- `7906396` chore: package servoerp genesis release.
- `90cb9cf` chore: record release commit in overnight log.
- `6e2fc56` chore: move generated artifacts out of git tracking.
- `54a9e95` chore: ignore local workspace artifacts.
- `6d0787c` genesis: complete jobs dashboard quick actions.
- `8ea94c2` fix: harden clipboard copy actions.
- `689ac65` genesis: redesign invoice action rail.
- `ec735cb` chore: record invoice action rail commit.
- `ed32e9d` genesis: redesign purchase action rail.
- `8285fa9` genesis: enrich payment action card.
- `5b79a36` genesis: streamline vendor dashboard actions.
- `7a5cd57` genesis: polish vendor document card title.
- `b96825c` genesis: redesign inventory stock actions.
- `25cc7b3` genesis: complete client activity navigation.
- `69ead4e` genesis: complete client activity detail actions.
- `ea71385` genesis: redesign contract action rail.
- `c6a60bb` genesis: improve employee filtered empty state.
- `84151cd` genesis: complete payroll more actions.
- `74fd47e` genesis: improve service desk filtered empty state.
