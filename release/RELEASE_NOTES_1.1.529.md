# ServoERP 1.1.529
Release date: 03/11/2026

## User-facing summary
Restore the normal enabled appearance of PDF review controls so Save/Next and navigation no longer misleadingly look unavailable after scanning.

## Features and fixes
Let the existing action-style helper restore enabled colours instead of overwriting them with cached disabled colours. Keep the established disabled styling and cursor feedback. Cover initially disabled Save, repeated parent disable/re-enable cycles, sequential PDF review, page navigation, zoom, previous selections and rapid file switching.

## Affected modules
Shared button primitives; PDF Import and other existing screens using the same enable/disable lifecycle.

## Database or migration impact
None. No business logic, schema, financial records or attachment-import behavior changed.

## Configuration and dependency changes
None. No packages added.

## Compatibility or breaking changes
None. No existing public API renamed and no theme redesign.

## Security impact
None. No new outbound services, licensing changes or credential handling changes.

## Deployment/update steps
Use the complete verified desktop package after verification. Finish or explicitly cancel the current review before restarting ServoERP; in-memory review sessions do not survive an application restart.

## Validation performed
A new regression reproduced the defect against published 1.1.528: initially disabled Save failed to regain the enabled palette through combined shared styling. After the fix: Release build/executable and CI-safe suite passed with zero warnings/errors. Native PDF review tests passed for the real scan disable/re-enable cycle, five sequential Save/Next operations, page navigation/zoom, Previous/reclassification, rapid file switching/cancellation and final summary. Categorisation did not create document registrations. Existing PDF/password, duplicate, SQL rollback, tamper and access checks passed in disposable ServoERP_PdfReviewQA_20261103_Controls529 only. Built screenshots were visually inspected. No customer-data test writes, public 1.1.529 release or remote installation performed.

## Known limitations
The existing unsigned distribution configuration remains. This fix does not change PDF text, storage, category destinations, automatic classification or business-record creation. Existing PDF Import session/storage limits still apply.

## Rollback guidance
Retain the 1.1.528 package and return to it if needed. No database rollback or attachment deletion is required. Never overwrite published 1.1.528 artifacts.
