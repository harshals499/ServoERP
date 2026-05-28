# AGENTS.md - UI\ Subfolder

> Extends root AGENTS.md. Root rules apply everywhere. These rules apply only to files inside UI\.

---

## UI ARCHITECTURE

- Every form inherits from `Form` (WinForms base class). No custom base form unless one already exists.
- All controls are created and configured in code. No `.Designer.cs` files, no drag-and-drop layout.
- Forms open from the MainForm navigation sidebar or as modal dialogs.
- All forms must call `InitializeComponent()` only if that method exists in the file - do not generate a stub.

---

## LAYOUT PATTERNS

| Pattern | When to use | Reference implementation |
|---|---|---|
| `DataGridView` with column definitions | Tabular list views (invoices, clients, jobs) | `InvoiceForm` |
| `FlowLayoutPanel` of `Panel` cards | Line item entry where each row needs multiple controls | `PurchaseForm` |
| Tabbed profile layout (`TabControl`) | Entity detail views with multiple categories | `EmployeesForm` |
| KPI strip (row of `Label` panels) | Summary numbers at top of a module | `EmployeesForm`, `DashboardForm` |

---

## COMBOBOX RULES (critical - broken ComboBoxes are the #1 regression source)

### The catalog (_invoiceCatalog) - what it is and where it lives

InvoiceForm does NOT pull directly from StockItems alone. It builds an internal
`_invoiceCatalog` from FOUR sources merged together:
1. Inventory stock items (StockItems table)
2. Active service rate cards (ServiceRateCard via MasterDataModels.cs)
3. HSN/SAC lookup entries (HsnSacMasterEntry)
4. Fallback HVAC items (hardcoded defaults)

Key locations in InvoiceForm.cs:
- Inventory loads at line 143
- Description combo items bound at line 184
- Catalog rebuild at line 207
- Grid ComboBox columns (Description, Category, Unit, TaxType) defined at line 1765
- Searchable DropDown behavior (DropDown, SuggestAppend, ListItems) applied at line 1873
- On edit completion, catalog match and apply at line 2499
- `ApplyCatalogItemToRow()` auto-fill logic at lines 2530-2552

When building any new form that uses a similar item picker, clone this entire
catalog pattern - do not just wire a ComboBox to StockItems directly.

### Auto-fill fields on item select (9 fields - not just Rate/HSNCode/Unit)

`ApplyCatalogItemToRow()` fills ALL of these when an item is selected:
- Description
- HSNCode
- Category
- Unit
- Rate (only if current rate is <= 0 - does not overwrite a manually entered rate)
- GSTPercent
- TaxType
- CoverageNote
- StockItemID (hidden column)
- IsStockItem (hidden column)

Any new form cloning this pattern must fill all 9. Partial implementation
(e.g. only Rate/HSNCode/Unit) will cause silent data loss on save.

### Critical naming trap - catalog internal vs line item column names

Catalog items use internal names: `StockItemId`, `HsnSacCode`
InvoiceLineItem columns use: `StockItemID`, `HSNCode`

The mapping happens inside `ApplyCatalogItemToRow()`. If you reference
catalog properties directly on a line item object or SQL column without
going through this method, you will write to the wrong field silently.
Always go through `ApplyCatalogItemToRow()` - never map catalog to line item manually.

### General ComboBox rules

- Always allow free text: `DropDownStyle = ComboBoxStyle.DropDown`, never `DropDownList`
- Searchable behavior: `AutoCompleteMode = SuggestAppend`, `AutoCompleteSource = ListItems`
- Category filter: in-memory only - never re-query DB on every keystroke
- Cache `_invoiceCatalog` in memory per form session - rebuild only on form load or explicit refresh
- Do not rebuild or rewire InvoiceForm or QuotationForm ComboBox unless the task explicitly says so

---

## REFERENCE IMPLEMENTATIONS

When building a new feature, clone from these - do not reinvent:

| Feature | Clone from |
|---|---|
| Line item grid with ComboBox | `InvoiceForm` (DataGridView pattern) |
| Line item card-based entry | `PurchaseForm` (FlowLayoutPanel pattern) |
| Excel export button | `EmployeesForm` EPPlus export |
| PDF generation button | `InvoiceForm` iTextSharp export |
| Alert/notification dialog | `NotificationCenter` (Acknowledge + OK flow) |
| Role-based button visibility | Any form that checks `SessionManager.CurrentUserRole` |
| Real-time search textbox | `EmployeesForm` search handler |
| Tabbed entity profile | `EmployeesForm` TabControl pattern |

---

## COLOR AND STATUS CODING

| Status | Color |
|---|---|
| Active / Completed / Paid | Green family |
| Pending / In Progress | Amber/orange family |
| Overdue / Cancelled / Breach | Red family |
| Neutral / Inactive | Gray family |

Use `Color.FromArgb()` - do not hardcode named colors unless they match the above intent exactly.

---

## GRID RULES

- Every `DataGridView` must have `ReadOnly = true` unless it is an editable entry grid.
- `AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill` on all list grids.
- Double-click on a grid row opens the detail/edit dialog for that record.
- Delete key on a selected row prompts `MessageBox.Show()` confirm before deletion.

---

## DIALOG RULES

- All confirmations use `MessageBox.Show()` with `MessageBoxButtons.YesNo`.
- Destructive actions (delete, fresh start, restore) require an explicit typed confirmation - not just a Yes/No button.
- Error messages must include the module name and action context, not just the exception message.

---

## PERFORMANCE RULES

- Load `DataGridView` data on a background thread for any query that could return more than 200 rows.
- Never query the database in a loop inside a UI event handler.
- Cache `StockItems` list in memory per form session - do not re-fetch on every ComboBox open.

---

## DO NOT DO IN UI FILES

- Do not put business logic in form event handlers - call a method in `SERVICES\`.
- Do not open `SqlConnection` directly in any form - use `DbExecutor`.
- Do not use `Application.DoEvents()`.
- Do not use `Thread.Sleep()` on the UI thread.
