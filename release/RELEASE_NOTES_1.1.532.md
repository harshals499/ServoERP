# ServoERP 1.1.532
Release date: 07/11/2026 (unreleased candidate; not approved for distribution).
Release status: INCOMPLETE

## User-facing summary
Restore Marathi selection in Settings and the resource-based localization foundation. This candidate is incomplete: app-wide Marathi acceptance has not passed and no product release has been published.

## Features and fixes
Add English / मराठी selection under Settings > Display & layout, save the preference per Windows user and restore it before login. Migrate the existing dictionaries into neutral, mr-IN and legacy hi-IN resources. Add Marathi login, Settings, custom validation and shared workflow captions. Expand reviewed caption resources across the desktop modules. Explicitly separate data-bearing text from translatable captions, use resource templates for interpolated UI sentences and preserve neutral captions for presentation roles and Settings routing. Apply presentation text to dynamic controls, grid headers, menus, toolbar tooltips and accessibility labels. Preserve text inputs, composite search/login inputs, combo values and grid data. Repair stale source-text caching, avoid strong references to disposed controls and use neutral captions for existing action-routing comparisons. Route UI alerts through compatible WinForms overloads without translating technical logs. Keep India-first business formatting.

## Affected modules
Startup, Login, Settings, shared form/page localization, UI alerts, validation presentation and caption-dependent navigation/actions in Clients, Inventory, Payments, Vendors, Attendance and shared workspace controls. Baseline includes the published 1.1.531 PDF category improvements.

## Database or migration impact
None. No schema changes or business-data migrations. The existing SQL Language setting remains a fallback for users without a local preference. No business database was used for these localization checks.

## Configuration and dependency changes
Add %LOCALAPPDATA%\ServoERP\Preferences\language.txt, containing a culture code. Language selection is per Windows user rather than changing the legacy shared SQL setting. Bundle mr-IN and hi-IN satellite resource assemblies with the EXE. No packages, services or downloaded language packs added. Legacy ZIP and Inno installers now include both satellite folders; staging validates their version and culture. Packaging refuses notes marked INCOMPLETE. Nirmala UI is used for Devanagari normal text; icon and technical fonts are retained.

## Compatibility or breaking changes
No public APIs renamed. Existing English default, business values, en-IN number/date/currency formatting, pricing, licensing logic and PDF header content retained. New local language preferences override the legacy SQL language setting. Reopen ServoERP to apply language consistently to all screens; some presentation controls update immediately.

## Security impact
No new outbound services, credentials or secret storage. Local UI preference stores only the culture code. Existing destructive confirmation semantics are unchanged. Customer/business data was not written during testing.

## Deployment/update steps
Do not distribute this candidate. Complete translation coverage, dynamic-data-label review, remaining workflow/visual verification and localization packaging verification first. The complete eventual package must include mr-IN/HVAC_Pro_Desktop.resources.dll and the version-matched release notes. Standard update destinations remain unchanged.

## Validation performed
Release compilation succeeds. The resource catalogue contains 3,191 Marathi entries, including reviewed additions and unchanged technical identifiers. The expanded static caption audit reports zero missing candidate keys; this is not proof of complete runtime coverage. The last fully passing focused run before extending the workflow suite recorded 3,581 assertions. The latest expanded run reached 3,564 passing assertions, including data-bearing captions that exactly match UI keys, before failing during disposable QA schema initialization: SQL error 208, invalid object dbo.Payments. No customer database was used for these write-capable checks. Workflow fixture database: ServoERP_MarathiQA_20261107_PreviewOnly. Native login previews were rendered; invoice/report preview verification remains blocked. No package, feed, installer or remote assets were produced for this candidate. INCOMPLETE packaging hold remains in effect.

## Known limitations
Not release-ready. Do not distribute. Static source caption coverage passes, but variable-driven captions, controlled dropdown display, SQL-generated notification text, default framework messages, full navigation, business saves, print/export language and third-party/native dialogs require runtime review. The workflow fixture bootstrap fails before previews because dbo.Payments is missing; three setup attempts were recorded and further retries of the unchanged path are stopped under AGENTS.md. A separate bounded fixture/schema repair is required before resuming the release verification. Existing PDF header content remains unchanged and protected. Windows-owned dialogs use Windows display language. The full regression suite does not currently pass.

## Rollback guidance
Continue using the verified 1.1.531 desktop release. If this test candidate was manually installed, reinstall the prior complete verified package. No database rollback or automatic customer-data rollback is required. The new local language file contains only a UI preference and is ignored by older versions.
