# Marathi restoration work record

Date: 2026-11-07
Baseline: b534e489d6b833c500f26a9dc31ae4ff732abc27 (published 1.1.530).
Evidence: historical LanguageManager still contains Marathi/Hindi dictionaries; Settings lacks a language selector; ServoPageBase does not apply localization; combo item translation mutates business values; original-text cache holds controls indefinitely and restores stale dynamic labels. Startup reads Language from SQL after initialization.
Acceptance: English / Marathi in Settings; persistent preference before login; mr-IN satellite resources with English fallback; live translation of safe presentation controls; preserve editable/business values and en-IN formatting; no changed PDF headers; full string coverage audit and passing build/regression/visual evidence before release.
Affected files planned: LanguageManager, language preference service, SettingsForm, Program, shared form/page bases, resources, localization tests/audit, version/release notes when releasable.
Validation: isolated read-only WinForms harness and resource tests with no business DB connection; Settings language persistence tested using temporary preference path; native rendering/screenshots; Release build and release guards. Do not launch the normal EXE entry point while Harshal's installed app is running: it calls ShutdownExistingAppInstances.
Research: https://learn.microsoft.com/en-us/dotnet/core/extensions/create-satellite-assemblies and https://learn.microsoft.com/en-us/dotnet/core/extensions/resources . Use bundled .NET resources; no external translation API or Windows language pack required for application strings.
Release status: blocked until app-wide coverage and required visual checks pass. PDF header localization excluded by explicit Hard Stop.

## Implementation checkpoint (not release approval)

Implemented the Settings English / मराठी selector, per-Windows-user local preference with legacy SQL fallback, early startup UI culture, neutral/mr-IN/hi-IN satellite resources, safe editable/composite input exclusions, live presentation control/menu/grid-header updates, accessibility text, input placeholders, UI alert localization and custom validation message resources. Existing technical logs and business-save logic are unchanged. Captions used for action routing now resolve neutral text.

Validation so far: Release build succeeded and EXE exists. Focused harness passed 551 assertions before the latest Settings resource additions (resource presence plus behavior/format/compatibility checks). Native login and Settings display-card previews were inspected in English and Marathi. The harness does not invoke the normal startup entry point, remembered-session authentication, OnShellActivated, database writes, or destructive action handlers. Saved preference replacement was repaired after File.Replace failed on this environment; MoveFileEx replacement and fallback tests pass. The running installed app was left open.

Coverage remains incomplete. Last audit: 1,326 untranslated candidates, 1,152 unique; this is a conservative partial static scan, not an exhaustive count. Full module-by-module resource binding, dynamic/interpolated messages, standard ToolTip components, presentation-only dropdown formatting, default FluentValidation messages and business-data-label exclusions still need review. Native operating-system dialog buttons follow Windows language. Existing PDF header content remains protected and was not translated.

Current source migration: public release 1.1.531 was verified through GitHub on 07/11/2026. Its clean source commit is 69de4c4 (child of original baseline b534e48). Rebase this task onto that source before assigning a candidate version. Do not deploy this checkpoint, package incomplete Marathi coverage, or claim the app-wide acceptance criteria are satisfied.
