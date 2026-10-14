# Quotation visual consistency task — 14 October 2026

Approved baseline: d48037cfb656b158efc3418622b3e4c7ac41ca86 (local 1.1.510 source snapshot explicitly approved by Harshal). Original checkout/index preserved. Local credentials and machine configuration excluded from snapshot.

Evidence: DashboardForm.SoftDashboard uses independent green-grey canvas/borders and muted blue buttons; DesignSystem explicitly bypasses it. QuotationForecastCard uses white surfaces, #E0E6EE outlines, 9px radius, integrated headings. Many modules suppress automatic geometry polish. Base classes reach pages/dialogs; an appearance-only policy must also reach late-created nested containers without enabling geometry changes.

Acceptance: white outlined cards and quotation canvas across modules, nested editors and dialogs; preserve business behavior, input geometry and sidebar; preserve meaningful status colours; dynamic controls inherit policy; pale selection with readable table text; no customer writes. Release build and executable; regression tests and screenshots at desktop widths; version/notes and artifact guards; remote feed/assets checked before and after publication.

Validation plan: reflection-driven CI-safe tests without normal startup (normal startup can interrupt existing app); disposable HVAC_PRO_UI_20261014 database for render-capable tests; show native WinForms controls in isolated hidden hosts, capture DrawToBitmap and inspect rendered main/detail/dialog states. Leave existing running application untouched. No save/reset actions against production.

Affected: shared DesignSystem/GridTheme, QuotationVisualPolicy, page/form/theme lifecycle, dashboard tokens, quotation card token, UI policy tests, project inclusion, version and release notes. No new packages, business SQL changes, license changes or PDF changes.

## Recorded acceptance evidence

- Release 1.1.511 build: passed; executable exists in this worktree SOURCE_CODE/bin/Release; ui-final-build.log.
- CI-safe checks: 39 PASS, no FAIL; ui-final-policy-tests.log and TEST_RESULTS/quotation-consistency/ci-safe.txt. Normal startup was not launched by tests.
- Screens: 24 successful captures; ui-511-screenshots.log and TEST_RESULTS/quotation-consistency/screenshots.txt. Full-size captures and six review sheets retained locally (not shipped; may contain business information from the disposable clone).
- Reviewed all six sheets against the Quotations reference: white/pale surfaces, integrated headings, quiet outlines, matching action/table colours. Reviewed full-size My Work, Quotations, Invoice, Settings, Client/Job details and AMC Equipment screens.
- Corrected My Work title/action overlap, dark-button text contrast, inherited header docking, late-created controls and expandable detail outlines.
- vpk generated full nupkg, Setup, portable and feeds; artifact guard passed. Final rebuilt notes are rechecked before publication.
- No new packages. No source-side PDF, business save/delete, licensing or production schema changes.
- Internal diagnostic scope now routes direct DatabaseManager connection resolution to the disposable database, protecting render-capable fixtures that instantiate legacy repositories.
- Test DB HVAC_PRO_UI_20261014 and COPY_ONLY backup ServoERP_UI_20261014.bak retained locally. No automatic customer data rollback or destructive cleanup.
- Remaining verification limits: no exhaustive every-record, every-modal, role or multi-DPI matrix. Main/subpage layouts retain workflow-specific content. Source snapshot is approved; original primary checkout remains unchanged.
