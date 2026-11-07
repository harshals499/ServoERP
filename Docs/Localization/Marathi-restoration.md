# Marathi restoration — current checkpoint 07/11/2026

**Unreleased 1.1.532; INCOMPLETE; do not publish.**

The continuation expanded the Marathi resource catalogue from 579 to **3,191 entries**. Reviewed translation batches, identity entries and their exact key lists are retained under scripts/Localization and Docs/Localization. The source rewriter uses the project's Roslyn semantic model to distinguish editable controls/data-bearing text from captions and to convert interpolated presentation strings into resource templates. Production dependencies are unchanged; the Roslyn tool is developer-only.

The static caption audit now reports **zero missing candidate keys**, after correctly decoding C# escapes. The scan is still not exhaustive runtime coverage: SQL-generated notification content, variable-driven UI labels, closed-choice display, standard/third-party dialogs and print/export output need explicit review. Do not equate this audit result with app-wide completion.

Source changes include explicit data-text markers, caption setters, UI factory caption binding after metadata construction, neutral button-role resolution, Devanagari drawing fonts, and preserving raw Settings card-routing keys while localizing their visible titles/search text. Caption matches such as a customer named Save or Cancel are tested as data and remain unchanged.

## Actual continuation validation

- Release compilation succeeds; version/culture-matched resource assemblies exist.
- Last fully passing focused run before expanded workflow checks: **3,581 assertions**. Latest expanded run reached **3,564 PASS assertions** before setup failure. These totals include resource presence and placeholder checks; they are not thousands of business-workflow tests.
- Current expanded suite **fails**. Attempt 1: unauthenticated preview fixture closed its host. Attempt 2: adding an authenticated synthetic session exposed license-state persistence against the deliberately isolated, then-missing QA database. Attempt 3 (repair 2): creating the QA database and invoking the existing schema initializer failed with SQL 208: **Invalid object name dbo.Payments**. Attempt 4 (repair 3): supplying the exact existing guarded Payments definition as a schema-only fixture allowed initialization to progress, then it failed with SQL 208: **Invalid object name dbo.PurchaseOrders**. There have now been three failed repair attempts after the initial setup failure; no further retry is authorized by the standing run policy.
- QA database retained: **ServoERP_MarathiQA_20261107_PreviewOnly** on .\SQLEXPRESS. Diagnostic connection scope and an exact catalog assertion guard all write-capable workflow tests. It is partially initialized. No business write, migration, save, reset or Fresh Start was executed against HVAC_PRO/customer databases. No license endpoint, machine ID, pricing or plan logic was changed.
- Further retries of the unchanged workflow setup are stopped after three failed attempts, as AGENTS.md requires. Next bounded work must diagnose/repair the disposable fixture/schema setup before retrying UI workflows; do not bypass it or weaken the checks.
- Installed app PID 5200 stayed open. No normal application entry point was launched. No updater feed, package, installer, remote release or source push was produced.
- Login/Settings focused previews and unit-level persistence/format/role checks were previously captured. Full invoice/report previews and business-workflow checks have **not** passed. The last failing report is artifacts/marathi-localization/run2-workflow-blocker.txt.
- Whitespace validation passes after normalizing resource-file line endings. Candidate notes are still explicitly INCOMPLETE and packaging remains prohibited.

## Next acceptance work

1. Repair the isolated QA fixture/schema bootstrap without changing customer data, LicenseInfo structure, licensing/pricing rules or existing PDF headers. Check the existing initializer/order before changing anything; the exact failure is known, the underlying cause is not yet established.
2. Re-run the complete suite against that fixture, inspect native English/Marathi workflows, navigation, Settings search/card routing, saves, errors, dialogs, keyboard behavior, clipping and third-party controls. Do not mark preview constructors as full business workflow validation.
3. Audit dynamic closed-choice and service/SQL-generated presentation text beyond the static caption catalogue; preserve canonical database values and user-entered names/notes.
4. Review all metadata/caption-dependent logic affected by the broad migration. Translation must happen at presentation boundaries, never change persistent identifiers or routing keys.
5. Only after all acceptance passes: refresh final notes, remove INCOMPLETE, recheck public version availability, package, pass notes/artifact/security/data checks and verify the remote release/feed/assets. 1.1.532 remains a local candidate, not a public reservation.

Baseline is clean published 1.1.531 source 69de4c4731652f35464f33568d92a4885fd9a770. Branch codex/marathi-localization is isolated from the pre-existing main-workspace modifications. New packages/services: None. Continue using public 1.1.531. No customer-database rollback is required; retain the QA database as evidence rather than dropping anything automatically.

## Previous checkpoint record

# Marathi restoration — unreleased 1.1.532 candidate

Status on 07/11/2026: checkpointed, incomplete, **not for distribution**. Full app-wide Marathi acceptance is not satisfied. Packaging is explicitly blocked by `Release status: INCOMPLETE` in the version-matched notes.

## Source and scope

Branch: `codex/marathi-localization` in `C:\Users\Administrator\.codex\worktrees\marathi-localization\HVAC_PRO_MSE`.
Baseline: published 1.1.531, source `69de4c4731652f35464f33568d92a4885fd9a770`. Public metadata and release-note content were verified against https://github.com/harshals499/ServoERP-Releases/releases/tag/v1.1.531 . The source branch had lagged at 1.1.530; the task was rebased onto the clean committed 1.1.531 source. Main-workspace product changes and the running installed app were preserved.

Requested acceptance: English / मराठी in Settings; persistence and startup restoration; .NET mr-IN resources with English fallback; all UI surfaces and major workflows localized without changing business values, formatting, pricing, licensing or PDF headers; visual verification; a fully verified release with notes/artifacts.

## Implemented

- Settings > Display & layout now contains English / मराठी and Save Language. Preference is saved atomically per Windows user under `%LOCALAPPDATA%\ServoERP\Preferences\language.txt`, loaded before login and given precedence over the legacy SQL Language fallback. Restart guidance is shown. The legacy dashboard selector uses the same persistence path.
- Migrate the historical dictionaries to neutral, mr-IN and legacy hi-IN .NET resource assemblies. The Marathi set has 579 entries with no missing neutral keys. No external language pack, new package or new service was added. Nirmala UI is verified on this host; symbol and technical fonts are preserved.
- Localize safe presentation captions, newly added controls, grid headers, menus, toolbar tooltips, accessibility labels, login placeholders, login validation and UI alerts. Custom validator messages have Marathi resources. Keep input/composite-input values, combo Items and grid business data intact. The Settings display-mode dropdown formats its display without changing its persisted selection.
- Replace stale strong text caching with weak source/rendered state. Use neutral captions for existing action routing in shared controls and affected modules, including Settings mobile-save routing and Ctrl+S.
- Include satellite folders in legacy ZIP/Inno installer definitions and validate their culture/version during staging. Incomplete release notes now prevent packaging. No package or publication was performed.

## Validation actually performed

- Release 1.1.532 build succeeded, with no warnings/errors in the recorded build output. `SOURCE_CODE/bin/Release/HVAC_Pro_Desktop.exe` exists. mr-IN/hi-IN assembly versions and cultures match 1.1.532.0.
- `scripts/Test-MarathiLocalization.ps1`: **656 passing assertions**, including 579 resource-presence checks and behavior, formatting-placeholder, fallback, alert overload compatibility, neutral action routing, persistence, editable/composite data preservation, late-created captions/headers/toolbars, and embedded release-note checks. Embedded notes exactly match the versioned notes copied beside the built EXE.
- Native WinForms login and the actual Settings display-card controls were rendered in English and Marathi. Screenshots were inspected. New language captions fit and the input/button do not overlap. The display card is reparented into a test host; this is not a full Settings navigation/workflow test.
- Existing release-note smoke checks pass. Notes guard passes for building; packaging correctly rejects the INCOMPLETE candidate. Positive satellite checks and negative missing/wrong-culture probes pass.
- `scripts/Test-VelopackReleaseArtifacts.ps1` was run and **did not pass**: no Velopack RELEASES/package was generated. This is an artifact blocker, not a verified release.
- `scripts/Audit-MarathiLocalization.py --fail-on-missing` exits **1** with **1,331 candidates / 1,157 unique strings**. The scan is conservative and incomplete; candidates include proper nouns, protected content, technical identifiers and interpolated strings requiring classification.
- No normal application entry point, remembered-session authentication, business-save/reset/Fresh Start/migration experiment or customer-database write was run. The normal entry point stops other app instances, so tests use a standalone harness and a preview subclass. Installed app PID 5200 remained open.
- `git diff --check` passed. Evidence is retained in `artifacts/marathi-localization/`; changed-file inventory is beside this report.

## Remaining acceptance work / release holds

1. Complete module-by-module translation with explicit resource bindings/templates. Largest current candidate groups: Employees, Purchases, Invoices, Tender Bids, Reports, Settings, Payroll and Inventory. Audit helper-built and service-generated strings beyond the current static scan.
2. Review data-bearing labels so a customer-entered value that happens to equal a resource key can never be translated as presentation text. Generic traversal alone is insufficient proof of this requirement. Audit all remaining caption-dependent logic and preserve neutral identifiers.
3. Complete dynamic/interpolated messages, normal ToolTip components, presentation-only dropdown text, default FluentValidation messages, empty states and notification content. Preserve canonical business values.
4. Test full startup/persistence in a separate safe app session; visually and functionally exercise main navigation, major workflows, dialogs, keyboard access, narrow layouts and third-party controls. Use a disposable explicitly identified SQL database for any write-capable checks. None has been used for this task yet.
5. Existing PDF header content stays unchanged under the Hard Stop. Native OS dialogs/buttons follow Windows language. Identify any additional protected coverage before distribution.
6. Remove INCOMPLETE only after acceptance passes, refresh notes from actual results, inspect/package with existing scripts, run all release guards and artifact verification, recheck remote versions and publication limits, and verify public feed/assets. Candidate 1.1.532 is not a reservation of a public release number.

## Version, notes and rollback

VERSION, assembly metadata and installer version are 1.1.532 **only in this isolated candidate**. CHANGELOG and `release/RELEASE_NOTES_1.1.532.md` explicitly state the incomplete status. No remote release, updater feed or main-workspace build was changed. Continue using verified 1.1.531. If a test candidate was manually installed, restore the prior full package; no database rollback is needed.

## Research

- https://learn.microsoft.com/en-us/dotnet/core/extensions/resources
- https://learn.microsoft.com/en-us/dotnet/core/extensions/create-satellite-assemblies
- https://learn.microsoft.com/en-us/windows/win32/api/winbase/nf-winbase-movefileexa

Application strings ship in the application's own resource assemblies; installing a Windows language pack does not translate hardcoded ServoERP text.

## Continuation run 07/11/2026
User explicitly requested complete app-wide Marathi and publication after all checks pass. Entry source is clean 07f6174; installed app PID 5200 is running. Usage is 13%, below the 80% deferral limit. This run targets explicit data-text exclusions, dynamic caption templates and remaining resource translations, followed by scoped native/workflow checks. Candidate remains INCOMPLETE until coverage, safety, workflow, packaging and remote verification pass. Planned affected files: LanguageManager, UI source text assignments, resources, localization audit/tooling, focused tests and release evidence. No business database writes are authorized against HVAC_PRO; use a disposable test database for write-capable checks.
