# ServoERP App-Wide Visual Regression Report

Run date: 2026-05-17

## Validation Evidence

- Build: `dotnet build C:\HVAC_PRO_MSE\SOURCE_CODE\HVAC_Pro_Desktop.sln`
- Result: Passed, 0 warnings, 0 errors.
- Isolated page captures: `C:\HVAC_PRO_MSE\Docs\UI_QA_Screenshots\app_regression_20260517_193710`
- Full shell captures: `C:\HVAC_PRO_MSE\Docs\UI_QA_Screenshots\app_regression_20260517_193710\main_shell`
- Additional full shell captures: `C:\HVAC_PRO_MSE\Docs\UI_QA_Screenshots\app_regression_20260517_193710\main_shell_extra_corrected`
- Reference source: `C:\Users\harsh\Downloads\ServoERP_UI_Redesigns`

## Coverage

Captured and smoke-loaded:

- Dashboard
- Clients
- Contracts
- Invoices
- Payments
- Quotations
- Reports
- Settings
- Vendors
- Purchases
- Inventory
- Employees
- Payroll
- GeoIntelligence
- Jobs
- ServiceDesk
- MasterData
- WhatsAppHub

Official reference screenshots exist for:

- Clients
- Contracts
- Inventory
- Invoice page
- Jobs
- Payments
- Payroll
- Purchase Order
- Reports
- Service Desk
- Settings
- Vendors

No matching official reference screenshot was found for Dashboard, Quotations, Employees, GeoIntelligence, MasterData, or WhatsAppHub, so those were evaluated heuristically only.

## Key Findings

### P1 - No Blocking Render Failures Found

All 18 tested modules constructed, created handles, and pumped layout without a smoke-test failure. No page showed a blank or crashed surface in the captured states.

### P2 - App-Wide Density and Fixed-Viewport Pressure

Several pages remain more crowded than the official redesign references, especially in restored 1440x860 captures. This is visible on Contracts, Invoices, Purchases, Reports, ServiceDesk, Employees, and Quotations. Common symptoms:

- small table text
- multi-panel pages competing for width
- nested scrollbars inside content regions
- detail panels compressed beside list panes
- headers and button rows packed tightly

This is not a single-page bug; it is a responsive layout pattern risk.

### P2 - Table Header Clipping Still Appears in Dense Grids

Quotations now shows the `Total (₹)` column, but some adjacent headers are compressed in the full-shell screenshot, including `Category` and `Shortfall`. Similar density risks are visible in Payroll, Reports, Purchases, and ServiceDesk tables.

Recommended fix: standardize data-grid responsive behavior: either enable horizontal scrolling with full header widths, or use priority columns with optional hidden/detail expansion.

### P2 - Reference State Mismatch on Several Modules

The official references often show empty states, modals, or clean default states, while the current app captures show populated operational data. This makes strict pixel regression comparison unreliable for:

- Clients: reference shows New Client modal; actual shows populated client account view.
- Inventory: reference shows empty-state inventory table; actual shows populated inventory.
- Payments: reference shows empty payment history; actual shows payment history rows.
- Payroll: reference shows empty run table; actual shows populated payroll rows.
- Reports: reference shows empty detailed report table; actual shows populated detail report rows.

Recommended fix: add deterministic QA seed states for empty, populated, and modal states before enforcing pixel thresholds.

### P2 - Real Shell Confirms Sidebar Is Stable but Content Is Not Always Reference-Aligned

The full-shell screenshots show the sidebar remains intact and consistently positioned. However, several content pages do not match the spatial rhythm of the references:

- Clients actual has a large unused lower-right area after the activity/detail content.
- Contracts actual is visually close, but text density and list/detail balance differ from the reference.
- Purchases actual is denser and more compressed than the reference.
- Reports actual is much more data-heavy and busier than the reference.
- ServiceDesk actual is close structurally but left incident list density is higher.

### P3 - Empty Space and Scrollbar Heuristics

Large blank areas or awkward scroll regions appear on:

- Clients: empty lower content area in the shell capture.
- Quotations: large grid whitespace remains by design, though it now includes an empty-state message.
- MasterData: large blank lower area after cards.
- WhatsAppHub: main conversation canvas is mostly empty in the captured state.

These are not crashes, but they reduce perceived polish.

### P3 - Visual Consistency Gaps

Some pages use different button color hierarchies and card densities. Examples:

- Dashboard uses many dense KPI strips and status tables.
- Quotations quick actions now have hierarchy, but the page still differs from invoice/purchase action panel styling.
- Settings uses many card types and colored action buttons in one viewport.

Recommended fix: centralize button intent styles and data-card spacing rules across modules.

## Page Notes

### Clients

Reference exists. Current page is structurally similar but is in a different state than the modal reference. Main-shell capture shows the sidebar is stable. Visual risk: lower content area is underused and the selected client card/list area is denser than reference.

### Contracts

Reference exists. Current page is close to the reference layout. Visual risk: restored viewport compresses left contract list and main form density.

### Inventory

Reference exists. Current page is close structurally. Visual risk: actual populated rows make the central table much denser than the empty-state reference, and the right item-details rail is compact.

### Invoices

Reference exists. Current page is close structurally. Visual risk: action rail and summary panel are dense; actual data state differs from reference.

### Jobs

Reference exists. Current page is close structurally. Visual risk: left work-order list is dense, and header actions are tightly packed.

### Payments

Reference exists. Current page is close structurally. Visual risk: actual history table is much denser than reference empty state.

### Payroll

Reference exists. Current page is structurally aligned but much denser than the reference. Table readability is the main risk.

### Purchases

Reference exists. Current page is structurally aligned but compressed. The action rail and form area are usable, but visual density is higher than the reference.

### Reports

Reference exists. Current page differs materially from the reference because populated charts/tables create a busier surface. Main risk is information overload and grid density.

### ServiceDesk

Reference exists. Current page is close structurally. Main risk is left incident-list density and red validation-heavy styling competing with page hierarchy.

### Settings

Reference exists. Current page is close structurally. Main risk is mixed card density and many colored controls in one viewport.

### Vendors

Reference exists. Current page is one of the closest matches structurally. Main risk is dense vendor list and compact form controls.

### Quotations

No official reference exists. Heuristic status: recent fixes are visible in the shell capture. Remaining risks are table header compression and large grid area.

### Dashboard

No official reference exists. Heuristic status: renders successfully but is visually dense, with many KPI strips and tables competing for attention.

### Employees

No official reference exists. Heuristic status: renders successfully but behaves like a dense list/detail editor; form controls and table need stronger hierarchy.

### GeoIntelligence

No official reference exists. Heuristic status: map/dashboard layout is operationally useful, but right-side cards and alert chips are dense.

### MasterData

No official reference exists. Heuristic status: card grid is readable, but the lower page has significant blank space in the captured state.

### WhatsAppHub

No official reference exists. Heuristic status: shell and columns render cleanly. Main risk is a very empty central canvas when no active conversation content is loaded.

## Recommended Next Work

1. Create deterministic UI QA states for each module: empty, populated, long-text, modal/dialog, and scrolled.
2. Add an app-wide grid policy for minimum header widths, horizontal scroll behavior, and priority columns.
3. Normalize action button hierarchy across Quotes, Invoices, Purchases, Payments, and ServiceDesk.
4. Reduce nested scrollbar dependency in list/detail pages by moving to more proportional TableLayoutPanel layouts.
5. Add per-module visual baselines after deterministic state setup so future visual regression can be threshold-based instead of manual.

## Sidebar/App Shell

The full-shell screenshots confirm the sidebar remained visible, stable, and consistently positioned during the tested module captures. No sidebar redesign or shell modification was performed as part of this evaluation.
