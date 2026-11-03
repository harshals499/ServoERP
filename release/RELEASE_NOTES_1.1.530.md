# ServoERP 1.1.530
Release date: Not published (candidate prepared 03/11/2026).

## User-facing summary
Make the PDF Import preview easier to read and navigate with compact controls, a clear document heading and more space for the page.

## Features and fixes
Add a current filename/folder heading and full-path tooltips, compact native PDF toolbar, page-boundary controls, visible zoom percentage, centred paper, Fit page and an expanded preview with a return-to-file-list action. Zoom starts from the current displayed scale. Show loading and per-file errors within the preview; reserve password/retry space only when needed.

## Affected modules
PDF Import preview and its review layout.

## Database or migration impact
None. No category destinations, import writes, schema or business records changed.

## Configuration and dependency changes
Version metadata updated. No packages or services added.

## Compatibility or breaking changes
None. Existing manual categorisation, final import confirmation and light theme retained.

## Security impact
None. Local PDF rendering and memory-only passwords retained. No new network calls or licensing changes.

## Deployment/update steps
Candidate for the next desktop release; not publicly published. Finish or cancel the current review before updating because in-memory choices do not survive restart. Ship the complete package when release validation is complete.

## Validation performed
Release build and executable passed with zero warnings/errors; CI-safe smoke suite passed. Native PDF checks passed for five sequential Save/Next operations, Previous/reclassification, page boundaries, relative zoom and horizontal panning, centred whole-page fit, expanded/normal/narrow transitions without retained horizontal scrollbars, rapid selection/cancellation, loading/error/recovery and final summary. Built screenshots were inspected at 1440×940 and 1080×760. Existing password, duplicate, SQL rollback, tamper and access checks passed in disposable ServoERP_PdfReviewQA_20261103_Preview530d only; categorisation created no document registrations. No customer-data writes or remote installation performed. Packaging/publication is deferred to the next release operation.

## Known limitations
Existing unsigned distribution and PDF Import storage/session limits remain. Very narrow preview toolbars may use the standard overflow menu. No automatic classification or financial record creation.

## Rollback guidance
Return to the retained 1.1.529 desktop package if required. No database rollback or attachment deletion needed.
