# Marathi restoration — ServoERP 1.1.532

Acceptance completed 08/11/2026. Publication is performed only after the checks below; the artifact provenance/checkpoint records the final remote result.

## Changes
English / मराठी in Settings, per-user atomic preference restoration before login, 3999 mr-IN resources, caption-only formatted templates, dynamic controls/menus, accessibility text, grids, empty states, search hints, built-in choices and custom dashboard captions. The previous 86 known captions and additional findings were corrected. Untouched system lookup defaults translate only in presentation; custom database labels remain exact. Employee and Service Desk canonical choice selection is preserved, with a Medium-priority regression test. PDF body labels translate while protected headers remain unchanged. Fresh initialization guards now follow table creation; missing nullable InvoiceLineItems.JobID is added with an existing guarded additive pattern.

## Passing evidence
- Final Release build and SOURCE_CODE/bin/Release/HVAC_Pro_Desktop.exe existence; matching language satellites.
- 4608 focused assertions, primarily resource/invariant assertions, not 4608 workflows. Report: artifacts/marathi-localization/run4-choice-regression-final/regression.txt.
- Disposable SQL database ServoERP_MarathiQA_20261108_141741_191a1c: zero initial user tables, initializer twice without table priming; existing business E2E in English and Marathi and ten save/update regressions per language. No HVAC_PRO/customer writes. Posted invoice edits remain blocked without changing the persisted subject.
- 58 native screenshots: login, navigation, Settings, directory, confirmation, invoice/report previews and 22 module surfaces, both languages. All 14 Settings routes are exercised. Screenshots are retained under artifacts/marathi-localization/run4-choice-regression-final. Selected visual inspections include Settings, Payroll, Guide, AMC, Geo, Backup, PDF import, Service Desk and invoice/job-card PDFs.
- Preference persistence/fallback, canonical choice lookup/selection, customer text preservation, notification templates, late menus, font/layout checks and exact embedded notes.
- Rendered audit zero resource-backed English captions in the captured review files; static audit zero missing candidates, expanded to grid-column and tab/menu constructors. Both are conservative, not exhaustive linguistic certification. Remaining ASCII review text is synthetic/customer data, identifiers, dates or technical abbreviations.
- Four synthetic English/Marathi invoice/job-card PDFs rendered; Devanagari body inspected; header pixels identical across languages. Evidence: artifacts/marathi-localization/pdf-acceptance. PDF text extraction was unavailable and is not claimed.
- Notes guard, satellite guard, Velopack artifact/feed-notes guard, whitespace check and cumulative ancestry of 50 available version tags pass. Packaged EXE, satellites and notes hashes exactly match tested output. No client config, license, database, backup or company-header-image files are in the full package.

## Limits and protected coverage
Existing PDF headers remain English as required by the Hard Stop. Windows-owned controls/dialog buttons and external WhatsApp Web use their own language. Names, business identifiers, custom database labels, logs, abbreviations and en-IN dates/currency/GST remain intact. Normal Program startup was not invoked because it closes other app instances; an external STA harness exercised the actual Release assembly and preference restoration. Installed user app PID 10676 was preserved. Captures and service tests do not prove every possible state or click sequence. Some pre-existing Service Desk clipping exists in both languages at the captured viewport; no theme redesign was made. No new package, downloaded language pack, service, credentials or paid dependency was added. No approval is needed for the shipped scope; translating existing PDF header content remains outside it.

## Release and rollback
Artifacts: artifacts/velopack-marathi-1.1.532-verified. Source branch: codex/marathi-localization. Versioned notes: release/RELEASE_NOTES_1.1.532.md. File inventory: Docs/Localization/changed-files.txt. Final source commit, checksums, remote verification and publication URL are recorded with artifacts and checkpoint.json. Retain verified 1.1.531 for rollback; restore the prior desktop package while preserving configuration and database. Leave the additive nullable column in place; never automatically roll back customer data.

## Research
- https://learn.microsoft.com/en-us/dotnet/framework/resources/creating-satellite-assemblies-for-desktop-apps
- https://learn.microsoft.com/en-us/dotnet/core/extensions/create-satellite-assemblies
- https://learn.microsoft.com/en-us/windows/win32/api/winbase/nf-winbase-movefileexa

Application resource assemblies translate ServoERP text; a Windows language pack does not translate hardcoded application strings. Existing framework/controls did not require a downloaded Marathi pack.
