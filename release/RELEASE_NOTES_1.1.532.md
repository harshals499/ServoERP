# ServoERP 1.1.532
Release date: 07/11/2026 (unreleased candidate; not approved for distribution).
Release status: INCOMPLETE

## User-facing summary
Restore Marathi selection in Settings and the resource-based localization foundation. This candidate is incomplete: app-wide Marathi acceptance has not passed and no product release has been published.

## Features and fixes
Add English / मराठी selection under Settings > Display & layout, save the preference per Windows user and restore it before login. Migrate the existing dictionaries into neutral, mr-IN and legacy hi-IN resources. Add Marathi login, Settings and custom validation text. Apply presentation text to dynamic controls, grid headers, menus, toolbar tooltips and accessibility labels. Preserve text inputs, composite search/login inputs, combo values and grid data. Repair stale source-text caching, avoid strong references to disposed controls and use neutral captions for existing action-routing comparisons. Route UI alerts through compatible WinForms overloads without translating technical logs. Keep India-first business formatting.

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
Release 1.1.532 build succeeded with the desktop EXE and version/culture-matched mr-IN and hi-IN assemblies present. The focused harness passed 655 assertions before the final embedded-notes equality assertion was added; final results and checksums accompany the candidate in artifacts/marathi-localization. Checks cover resource presence, formatting placeholders, culture fallback, input/business-value preservation, menu/grid/control updates, alert overload compatibility, validation text, preference persistence, neutral action routing, embedded release-note smoke checks and login/Settings native previews. Missing and wrong-culture satellite probes were rejected. The notes guard passes for building and rejects packaging because this candidate is INCOMPLETE. Velopack artifact verification was attempted and failed because no RELEASES/package was generated. No full business-workflow, actual normal startup, write-capable database, installer or remote artifact validation is claimed.

## Known limitations
Not release-ready. Static audit still has over 1,300 untranslated candidates and is not exhaustive. Dynamic/interpolated messages, standard ToolTip components, presentation-only dropdown text, default FluentValidation messages and data-bearing labels require further resource binding and review. Module-by-module visual and keyboard testing remains incomplete. Native Windows dialog/button language follows Windows. Existing PDF headers are excluded by the Hard Stop. No package, feed or remote assets produced or verified for this candidate.

## Rollback guidance
Continue using the verified 1.1.531 desktop release. If this test candidate was manually installed, reinstall the prior complete verified package. No database rollback or automatic customer-data rollback is required. The new local language file contains only a UI preference and is ignored by older versions.
