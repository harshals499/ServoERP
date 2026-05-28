# UI QA Baselines and Design-System Hardening Design

## Goal

Create a deterministic, repeatable UI QA system for ServoERP, then use it to enforce app-wide grid behavior, action button hierarchy, and layout quality without changing app shell, navigation, routing, database schema, service behavior, or business workflows.

## Scope

This work covers five related outcomes:

1. Deterministic UI QA states per module: `empty`, `populated`, `long-text`, `modal`, and `scrolled`.
2. App-wide grid policy for minimum header widths, horizontal overflow, and priority columns.
3. Shared action button hierarchy for Quotations, Invoices, Purchases, Payments, and ServiceDesk.
4. Reduced nested scrollbar dependency in list/detail pages through proportional layout containers.
5. Per-module visual baselines so future regression checks can compare screenshots by threshold instead of manual review.

The first implementation pass should establish reusable infrastructure and migrate the highest-risk pages surfaced by the 2026-05-17 visual regression report. Broader page-by-page redesign remains separate and must only happen when a matching reference screenshot exists.

## Non-Goals

- No sidebar redesign.
- No app shell restructuring.
- No routing or navigation changes.
- No database schema changes.
- No changes to production business rules or persistence behavior.
- No broad redesign of modules without matching reference screenshots.
- No brittle pixel comparison against non-deterministic live data.

## Architecture

The implementation has three layers:

### 1. QA State Layer

Add a lightweight in-process QA state model that can be passed to WinForms pages by capture scripts. The state model describes:

- module key
- visual state name
- viewport size
- optional action such as opening a modal or scrolling to the bottom

The QA state layer must be test-only and activated only by smoke/capture scripts. Production app launches continue to use real services and normal navigation.

Pages do not need to become fully mocked in the first pass. Instead, the capture harness will:

- instantiate each module in a controlled session
- apply a deterministic state adapter after the page is constructed
- optionally invoke safe public or reflection-based UI actions for modal/scrolled captures
- capture screenshots to stable state folders

For modules where safe state manipulation is not available yet, the baseline manifest records the state as `unsupported` rather than inventing fake screenshots.

### 2. Shared Visual Policy Layer

Centralize reusable UI behavior:

- `GridTheme` owns grid overflow behavior and header minimum widths.
- `UIHelper` owns action button variant application.
- A new `UiActionStyle` enum defines `Primary`, `Secondary`, `Success`, `Warning`, `Danger`, and `Ghost`.
- A new `GridColumnPriority` concept defines which columns are required, secondary, and optional.

The policy must be opt-in at first. Existing pages migrate intentionally, starting with Quotations, Invoices, Purchases, Payments, and ServiceDesk.

### 3. Baseline Capture Layer

Add a new PowerShell capture script that generates:

- screenshots grouped by module and visual state
- a manifest CSV/JSON listing result, dimensions, module, state, screenshot path, and notes
- optional baseline comparison result when a baseline file already exists

The comparison should initially use practical image metrics:

- dimensions must match exactly
- simple per-pixel difference ratio threshold
- failed comparisons write a diff artifact

This threshold-based comparison becomes enforceable only after deterministic state setup produces stable baselines.

## Data Flow

1. Build Debug app.
2. Run `CaptureUiQaStates.ps1`.
3. Script loads `HVAC_Pro_Desktop.exe`, creates a test session, and navigates or instantiates modules.
4. Script applies each requested QA state.
5. Script captures screenshots to `Docs/UI_QA_Baselines/current/<module>/<state>.png`.
6. If baseline exists under `Docs/UI_QA_Baselines/baseline/<module>/<state>.png`, script compares images and writes result rows.
7. CI/local users review failures with generated diff images.

## Module State Matrix

Initial module list:

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

Every module gets a manifest entry for all five states:

- `empty`
- `populated`
- `long-text`
- `modal`
- `scrolled`

If a state cannot be produced safely in the first pass, the manifest result is `UNSUPPORTED` with a reason. Unsupported is better than a misleading baseline.

## Grid Policy

Grid defaults:

- `ScrollBars = Both`
- `AutoSizeRowsMode = None`
- header height at least 38px
- no clipped header text
- amount columns align right
- status/action columns align center
- text columns align left
- horizontal overflow is acceptable when all important headers remain readable

Column priority:

- Required: identifiers, name/description, status, amount/total, primary date, action.
- Secondary: category, type, client/vendor, owner/assignee.
- Optional: internal IDs, notes, long metadata, import/source flags.

At narrow widths, optional columns should hide before required columns compress below header width.

## Action Button Hierarchy

Shared hierarchy:

- Primary: new/create/main workflow action, filled purple/blue.
- Success: save/post/record/approve, filled green.
- Secondary: preview/import/template/forms/open/upload, white with visible border.
- Warning: hold/renew/remind, filled amber or bordered amber depending risk.
- Danger: delete/void/close destructive action, filled red only when destructive.
- Ghost: low-emphasis icon or overflow action, white/border or transparent depending parent.

The first migration targets:

- Quotations
- Invoices
- Purchases
- Payments
- ServiceDesk

## Layout Policy

Nested scrollbars should be reduced by:

- preferring `TableLayoutPanel` or `SplitContainer` proportions over absolute fixed widths
- keeping one main vertical scroll region per page where possible
- making left list panes proportional with sensible min/max widths
- allowing detail panes to grow rather than adding inner scrollbars early
- retaining explicit scroll regions only for large tables, long activity feeds, and message lists

The first pass should not rewrite every page. It should introduce helper patterns and apply them to the worst visible offenders after baselines exist.

## Testing

Tests must cover:

- QA state manifest includes every module/state pair.
- Grid policy never assigns a visible column width below measured header text minimum unless the column is optional and hidden.
- Action style resolver maps known labels to expected variants.
- Capture script produces manifest rows and screenshots.
- Build succeeds.
- Smoke capture succeeds for all supported module states.

Where C# unit-style tests are awkward in this WinForms project, add diagnostic test methods under `SOURCE_CODE\Tests` and call them from PowerShell test scripts.

## Risks

- Pixel diffs will be noisy until data and viewport states are deterministic.
- Some pages may load data asynchronously, so capture scripts need a stable pump/wait strategy.
- Reflection-based modal/scrolled state setup can break if page internals change.
- Layout refactors across large WinForms pages can accidentally affect event handlers if done too aggressively.

## Acceptance Criteria

- Build passes.
- Capture script writes a complete module/state manifest.
- Unsupported states are explicit and justified.
- At least one baselineable screenshot exists for every module.
- Grid policy is centrally defined and applied to target pages.
- Action hierarchy is centrally defined and applied to target pages.
- Full-shell screenshots prove sidebar/app shell remain unchanged.
- Report documents screenshots, baseline comparison results, remaining unsupported states, and residual risks.
