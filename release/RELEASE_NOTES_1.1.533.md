# ServoERP 1.1.533

Release date: 08/11/2026 (prepared for next release; not published).

## User-facing summary
Hide the offline-access/database status banner using its new Hide button.

## Features and fixes
Dismiss the current status notice for this app session. Routine health refreshes keep it hidden. A changed connection state, write-availability state or status message restores the banner. Restarting also restores normal notices. Add English/Marathi captions and translate the offline-preparation notice.

## Affected modules
Main window status banner and shared language resources.

## Database or migration impact
None. No schema, sync, authentication or connectivity behavior changes. QA uses an existing explicitly disposable database only.

## Configuration and dependency changes
None. Dismissal is in memory for the current window; no new preference, language pack or dependency.

## Compatibility or breaking changes
None. Existing offline workspace, support and Check actions remain. Public APIs, licensing, pricing and PDF headers remain unchanged.

## Security impact
None. Dismissal changes presentation only; connectivity checks, business-write protections and synchronization continue.

## Deployment/update steps
Include this change in the next verified release with matching satellite assemblies and notes. No publication is performed by this feature task.

## Validation performed
Release build succeeds and SOURCE_CODE/bin/Release/HVAC_Pro_Desktop.exe exists. Nineteen focused assertions pass against the actual Release assembly: English/Marathi captions, focusability and caption fit, hide action, reclaimed content space, suppression during same-status refresh, reappearance after connection transitions and exact embedded release notes. Four before/after native screenshots were captured and inspected in both languages. An external STA harness suppresses startup/update/backup hooks and uses disposable database ServoERP_MarathiQA_20261108_141741_191a1c; normal Program entry and customer business writes were not run. Evidence is retained in artifacts/banner-dismiss.

## Known limitations
Dismissal lasts for this window/session and is intentionally cleared by a changed status or restart. It does not enable offline access; prepare offline access through the existing sign-in flow when needed.

## Rollback guidance
Restore the verified 1.1.532 desktop package while preserving configuration and database. No data rollback is needed.
