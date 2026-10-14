# ServoERP 1.1.498

Release date: 2026-10-06. Public desktop release. Installation on individual client PCs is a separate step.

## User-facing summary

The sidebar Refresh Page button now blends into the sidebar blue. Release notes are available after an update and can be reopened from Settings.

## Features and fixes

- Refresh Page keeps its blue background and white text when the application theme is applied; hover and pressed states also stay blue.
- Each application version includes its own release notes, available without an internet connection.
- What's New appears once for the installed version, including manual updates that did not stage an update notice. Notes are acknowledged only after the dialog closes.
- Settings > Updates includes a Release Notes button to reopen the installed version's notes.
- Build and packaging checks reject missing or blank release notes. The local release manager passes the versioned notes into the automatic-update feed.

## Affected modules

MainForm/sidebar, Settings/Updates, UpdateService, shared ReleaseNotesDialog, CI smoke tests, desktop project resources, release packaging and validation scripts.

## Database or migration impact

Database schema and business-data changes: None.

## Configuration and dependency changes

The persistent App configuration records the last acknowledged release-notes version and uses atomic pending-notice updates. Release notes are embedded in the EXE and copied alongside the Release output. Packages added: None.

## Compatibility or breaking changes

Public API renames and breaking changes: None. Existing pending notices remain supported. Three-part and four-part version numbers compare consistently.

## Security impact

None. Database settings, licensing endpoints and machine identity logic are unchanged by this release.

## Deployment/update steps

Install version 1.1.498 using the normal update process. After login, review What's New and dismiss it. Reopen notes from Settings > Updates > Release Notes. Future releases must supply a nonblank versioned release-notes file before building or packaging.

## Validation performed

- Release build succeeded with zero warnings and zero errors; `SOURCE_CODE/bin/Release/HVAC_Pro_Desktop.exe` exists at assembly version 1.1.498.0.
- CI-safe smoke suite passed, including embedded notes, manual-update notice selection, equivalent version handling, future-stage suppression and nonblank fallback checks.
- Negative checks confirmed that the build rejects missing/blank notes, the release validator rejects version-mismatched notes, and packaging rejects pending validation.
- Built-app `/releasenotesvisualtest` exited 0. The actual themed sidebar, release-notes dialog and Settings > About & Updates card were rendered and visually inspected. Evidence: `TEST_RESULTS/sidebar-refresh-1.1.498.png`, `TEST_RESULTS/release-notes-1.1.498.png`, `TEST_RESULTS/settings-release-notes-1.1.498.png`.
- Velopack package/feed checks passed: version-matched nonblank notes, matching tested EXE, and exclusion of mutable client settings/business files. The artifact validator also rejected an empty-note feed.
- No remote-client deployment or live automatic-update restart was performed.

## Known limitations

No deployment or update restart has been performed on the affected client PC. A missing/corrupt configuration can cause the notice to appear again because acknowledgment cannot be persisted. Older releases built before these guards may still omit feed notes. The locally packaged installer and executable are unsigned.

## Rollback guidance

Restore the previous application package while retaining the PC's persistent database configuration. The added release-notice marker can remain; older versions ignore it. No business database rollback is required.
