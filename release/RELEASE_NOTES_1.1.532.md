# ServoERP 1.1.532

Release date: 08/11/2026 (unreleased candidate; not for distribution).
Release status: INCOMPLETE

## User-facing summary
Restore English / मराठी in Settings and expand Marathi across existing desktop workflows. Publication remains blocked by untranslated rendered UI; this candidate is not an app-wide Marathi release.

## Features and fixes
- Persist the language per Windows user and restore it before login; retain English for users without a Marathi preference.
- Ship 3604 Marathi .NET resource entries; replace 52 inherited TODO(mr) placeholders. Preserve resource format arguments and English fallback.
- Localize registered dropdown choices by presentation without changing item objects; read canonical choices for business comparisons and saves. Preserve data-bearing names, identifiers, editable values and grid data.
- Repair Marathi Settings tab routing, workspace title line-height clipping, invoice risk presentation and notification-detail templates. Expand custom/default import-validation messages and pagination captions.
- Repair clean database initialization: move uniqueness checks after their tables exist; add the existing required nullable InvoiceLineItems.JobID column through the guarded additive migration.

## Affected modules
Settings, login, navigation, invoices, reports, notifications, shared UI, master data, clients, suppliers, jobs, purchasing, inventory, payments, contracts, attendance, employees and payroll. Changes in these modules do not imply complete translated coverage.

## Database or migration impact
Guarded additive nullable InvoiceLineItems.JobID column; uniqueness guards run after table creation, retaining duplicate-check behavior. No drops, public renames, LicenseInfo structure changes, or new business policies. Tests wrote only to explicitly named disposable SQL databases, most recently ServoERP_MarathiQA_20261108_124537_91babf. Earlier QA databases are retained; no customer migration/save/reset experiment was performed.

## Configuration and dependency changes
Per-user language file: %LOCALAPPDATA%/ServoERP/Preferences/language.txt. Include version-matched mr-IN and hi-IN satellite assemblies. No production dependency, downloaded language pack, new service, credential or paid service added. Roslyn review tools use existing Visual Studio tooling only. Devanagari uses the existing Nirmala UI font; icon/technical fonts are preserved.

## Compatibility or breaking changes
No public API names, pricing, plan names, license endpoint/machine-ID logic, Fresh Start behavior or PDF header content changed. en-IN business formatting remains. Restart guidance ensures the selected language is restored consistently; practical controls update immediately. Canonical dropdown strings and IDs remain the values used by business operations.

## Security impact
No new outbound service or secret store. Language preference contains only the UI language. Technical exception details remain untranslated. The test suite verifies that finalized invoices remain protected and blocked edits leave stored subjects intact. No customer data was written during testing.

## Deployment/update steps
Do not distribute. Resolve the rendered-caption findings and remaining dynamic/custom/print/export/third-party coverage, repeat acceptance, then package with the existing scripts and pass all notes/artifact/publication guards. Include both satellite folders and version-matched notes. Public verified release remains 1.1.531; 1.1.532 is only a local candidate.

## Validation performed
Release build succeeds and SOURCE_CODE/bin/Release/HVAC_Pro_Desktop.exe exists. Latest functional harness: 4,153 assertions, including 3604 resource-presence checks; this is not thousands of workflow tests. A new disposable database was asserted to have zero user tables, then initialized twice without priming. Existing end-to-end client/site/job/quotation/purchase/inventory/invoice/payment/report/import workflows passed in English and Marathi. Ten create/update/attendance/payment regression cases passed in each language, including draft edits and rejection of posted-invoice edits. Native captures cover login, navigation, all 14 Settings routes, confirmations, invoice/report previews and 13 further modules in both languages. Preference reload/fallback, data preservation, canonical choice reads, risk formatting, notification templates and embedded notes were checked. Expanded static audit: zero missing candidates, not exhaustive runtime coverage. Rendered linguistic audit fails: 86 resource-backed captions remain English. Additional English presentation strings require review. No distribution package or remote upload was produced.

## Known limitations
INCOMPLETE: 86 rendered resource-backed captions remain unbound to Marathi, and the broader captured-English worklist includes other missing templates and data requiring classification. Custom dashboards/tables, dynamic messages, deeper dialogs, print/export bodies and third-party controls need completion and review. Functional/native construction tests do not prove full linguistic coverage or exhaustive click-by-click acceptance. Normal Program entry was not launched because it shuts down other app instances; a separate STA harness used the actual Release assembly and suppressed updater/offline/backup startup in the preview shell. Direct computer-use tooling could not initialize, so captures and actions were performed by the native QA harness. Existing PDF headers remain unchanged under the Hard Stop; Windows-owned dialog buttons use Windows language. Packaging guard must continue rejecting this candidate. Artifact verification cannot pass without a package.

## Rollback guidance
Continue using verified 1.1.531. If a test candidate was manually installed, restore the prior complete verified package; retain additive schema for compatibility. Never automatically roll back business data. Older versions ignore the local UI-language preference. No customer database rollback is required for this QA run.
