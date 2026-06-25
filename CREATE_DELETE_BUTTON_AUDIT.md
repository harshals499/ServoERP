# Create / Delete Button Audit

Date: 2026-06-25

Scope: static WinForms source audit for places where users can create, edit, browse, or select business records, but the Create/Delete action is missing, hidden, misleading, or only partially wired.

## Implementation Update - 2026-06-25

Resolved the biggest findings from this audit:

- Payroll salary history no longer shows fake `Delete` text. Rows now expose real View/Edit/Deactivate actions, with confirmed salary structure deactivation through the payroll service.
- Payroll loans and advances no longer show fake row actions. Rows now expose View/Edit/Stop recovery actions; loan stop uses inactive state and advance stop marks the advance recovered.
- Master Data now has guarded deactivate/remove actions for assets, document registrations, rates, lookup categories, lookup values, private server connections, and company document templates.
- Service Desk now has visible `New Incident` and `Cancel` actions. Cancel persists a `Cancelled` status and writes an incident note instead of deleting history.
- Attendance now has `Reset Selected` and `Clear Month` actions with confirmation, audit logging, and reload after completion.
- AMC detail now has a visible guarded `Delete` action that reuses the contract deletion service, preserving linked invoices/jobs/purchase orders by unlinking them.
- Client, Vendor, Purchase, and Invoice editors now expose visible delete/archive actions instead of relying only on context menus or shortcuts.

Release build verification passed after implementation. Existing Attendance unused-field compiler warnings remain.

## Verification Update - 2026-06-25

- Release build passed and produced `SOURCE_CODE/bin/Release/HVAC_Pro_Desktop.exe`.
- `/smoketest` passed: `TEST_RESULTS/enterprise-ui-smoke-20260625-133506.txt`.
- `/savebuttontest` passed: `TEST_RESULTS/save-button-smoke-20260625-134130.txt`.
- `/amctest` passed: `TEST_RESULTS/amc-smoke-20260625-134137.txt`.
- `/contractstest` passed: `TEST_RESULTS/contracts-page-smoke-20260625-134142.txt`.
- Targeted temporary-row checks passed for payroll salary structure deactivate, payroll loan stop recovery, payroll advance recovery, attendance reset/clear, master data deactivate/remove, and master lookup deactivate flows.
- The broad UI smoke initially found an unrelated WhatsApp Hub header button alignment issue; fixed in `SOURCE_CODE/UI/WhatsAppHubForm.cs` and reran `/smoketest` successfully.

## Priority Fix List

| Priority | Area | Finding | Evidence | Recommended action |
|---|---|---|---|---|
| P0 | Payroll - salary structures | The salary history grid displays `View  Edit  Delete`, but the grid has no click handler for salary history actions. The visible Delete text is misleading and not wired. | `SOURCE_CODE/UI/PayrollForm.cs:1695` writes `View  Edit  Delete`; only payslip/TDS/loan/Form16 grids have action handlers around `SOURCE_CODE/UI/PayrollForm.cs:711-799`. | Replace fake action text with real action buttons/menus. Add delete only if payroll rules allow removing old structures; otherwise rename to `View` and add an explicit `End structure` / `Deactivate` flow. |
| P0 | Payroll - loans/advances | Loans and advances show `View  Edit  Delete`, but `HandleLoansGridAction` only opens a detail dialog. Delete/edit do not exist. | `SOURCE_CODE/UI/PayrollForm.cs:1807-1809` writes `View  Edit  Delete`; `SOURCE_CODE/UI/PayrollForm.cs:1813-1824` only shows details. | Add real row actions: View, Edit, Stop Recovery/Delete. Use confirmation and preserve payroll history if the item was already used in a locked payroll run. |
| P0 | Master Data | Assets, documents, service rates, lookup categories, lookup values, server connections, and company document templates have New/Save/Upload actions but no Delete/Deactivate action. | `SOURCE_CODE/UI/MasterDataForm.cs:215-217`, `241`, `271-273`, `299-322`, `354-357`, `2143-2177`. | Add explicit `Deactivate` or `Delete` per data type. Prefer soft delete/deactivate for lookup/rate/config records. Add hard delete only for safe uploaded template files after confirmation. |
| P1 | Service Desk | There is Save/Create Job/Close/Resolve/Start, but no obvious `New Incident` button after selecting an existing incident. Creating a fresh incident depends on load/empty state behavior, not an explicit primary action. | `SOURCE_CODE/UI/ServiceDeskForm.cs:238-254`; new incident state exists at `SOURCE_CODE/UI/ServiceDeskForm.cs:662-671`. | Add `New Incident` to the right action bar and empty state. Keep `Create Job` separate because it creates a work order from an incident, not the incident itself. |
| P1 | Service Desk | No Delete/Cancel Incident action exists. Close/Resolve are present, but operators may need an intentional cancel/void path for wrongly created incidents. | `SOURCE_CODE/UI/ServiceDeskForm.cs:239-254`; service layer exposes save/job/note/status operations but no delete. | Add `Cancel Incident` instead of hard delete unless Harshal approves destructive incident deletion. Persist status/audit note rather than deleting rows. |
| P1 | Attendance | Attendance has Mark All Present, Import, Save, and Open Payroll, but no clear row/month reset/delete affordance. Grid row deletion is disabled. | `SOURCE_CODE/UI/AttendanceForm.cs:143-149`; `SOURCE_CODE/UI/AttendanceForm.cs:694-695`; `SOURCE_CODE/UI/AttendanceForm.cs:1788-1789`. | Add a guarded `Clear Month` or `Reset Selected Employee` action only after deciding business behavior. This should delete/blank attendance rows for the chosen month with confirmation and audit logging. |
| P1 | AMC contracts | AMC list has Add AMC and Open/Edit, but no delete/archive action visible from the list/detail page. | `SOURCE_CODE/UI/AMCPage.cs:130`, `SOURCE_CODE/UI/AMCPage.cs:256-295`, `SOURCE_CODE/UI/AMCDetailPage.cs:84-94`. | Add `Cancel/Archive AMC` if contracts should remain historically visible; add hard delete only if linked visits/equipment/invoices are safely handled. |
| P2 | Client management | Client delete exists only through a context menu; there is no obvious visible Delete button in the main client editor/list. | `SOURCE_CODE/UI/ClientManagementForm.cs:1425`, `SOURCE_CODE/UI/ClientManagementForm.cs:1510-1528`. | Add visible `Delete Client` in detail/action area, disabled until a saved client is selected. Keep context menu as secondary. |
| P2 | Vendor management | Vendor delete exists from dashboard context menu only; primary vendor editor does not show an obvious delete button. | `SOURCE_CODE/UI/VendorForm.cs:1286`; service delete exists in `SOURCE_CODE/Services/VendorService.cs`. | Add visible `Delete Vendor` or `Archive Vendor` to the vendor detail actions, disabled for new records. |
| P2 | Purchase orders | PO delete exists through row/action menus, not as an always-visible editor button. | `SOURCE_CODE/UI/PurchaseForm.cs:1401`, `SOURCE_CODE/UI/PurchaseForm.cs:2109`, `SOURCE_CODE/UI/PurchaseForm.cs:4234-4252`. | Consider a visible `Delete PO` editor action beside Save/Preview for draft/cancelled POs only. |
| P2 | Invoices | Invoice delete is present in grid/card/menu paths and keyboard shortcut, but not a visible primary editor button. | `SOURCE_CODE/UI/InvoiceForm.cs:150`, `SOURCE_CODE/UI/InvoiceForm.cs:1194-1220`, `SOURCE_CODE/UI/InvoiceForm.cs:1671-1736`, `SOURCE_CODE/UI/InvoiceForm.cs:4777`. | Optional: add visible `Delete Invoice` in editor actions for draft/unpaid invoices only. Existing coverage is better than most modules. |

## Confirmed Better-Covered Areas

| Area | Create present | Delete present | Notes |
|---|---:|---:|---|
| Employees | Yes | Yes | Visible `_btnDelete` and `DeleteCurrentEmployee()` soft-delete path. |
| Inventory | Yes | Yes | Visible `Delete Item`; supplier price rows have `Remove Row`. |
| Jobs / Work Orders | Yes | Yes | Visible detail delete, dashboard context delete, delete shortcut. |
| Contracts | Yes | Yes | Visible `Delete Contract`, context menu, and shortcut. |
| Payments | Yes | Yes | New payment and row-level delete exist. |
| Quotations / Tenders | Yes | Yes | Quote delete and line-item delete exist. |
| Purchases | Yes | Partial | Delete exists, but mostly menu/context-driven. |
| Invoices | Yes | Partial | Delete exists in row/card/menu/shortcut, but not as a top-level visible editor button. |
| Clients | Yes | Partial | Delete exists in context menu; client site delete exists from detail. |
| Vendors | Yes | Partial | Delete exists in dashboard context menu; editor visibility should improve. |

## Recommended Implementation Order

1. Fix misleading Payroll action text first. A visible `Delete` that does nothing is worse than a missing button.
2. Add Master Data deactivate/delete actions because this page owns many small records and currently has Save/New only.
3. Add explicit `New Incident` and `Cancel Incident` in Service Desk.
4. Decide Attendance reset semantics before coding. This touches monthly payroll evidence, so prefer a reset/blank flow with audit logging over silent row deletion.
5. Add visible delete/archive actions to AMC, Client, Vendor, Purchase, and Invoice editors where the action already exists in a hidden menu.

## Delete UX Standard For ServoERP

- Use `ServoConfirmDialog` or `RecordDeletionUi.ConfirmPermanentDelete` for destructive actions.
- Disable delete buttons for unsaved/new records.
- Prefer soft delete, archive, cancel, or deactivate when records are part of history, GST/accounting, payroll, tickets, contracts, or audit trails.
- Hard delete is acceptable for accidental drafts, unattached uploads, and temporary rows only after explicit confirmation.
- After delete/deactivate, refresh the list, clear the editor, and show a professional status message.
