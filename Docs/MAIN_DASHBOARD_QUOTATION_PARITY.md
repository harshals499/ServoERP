# Main dashboard — Daily Dispatch option 2

The user selected the second generated concept for the same unpublished version 1.1.512. This supersedes the earlier parity-only layout, retaining the shared light card and action rules while adopting the selected composition.

Implementation: compact utility/title header; four-column pale metric band (two columns below 1050px); schedule-left and attention-right above 1100px; team/followup row; full-width collections and activity. Narrow windows stack the sections. Schedule rows show times, client/site, assignment badge and existing record actions. Attention preserves all eight filters in a menu and retains pagination. Financial cohorts remain separate. Role permissions, technician ownership, hover previews and callbacks remain enforced.

Official Lucide 0.468.0 icons and their license are bundled beside the executable. The existing renderer now handles elliptical arcs. No packages or runtime services were added. The compact header mode is opt-in, so other pages retain their existing layout.

Validation: Release build with executable present; 39 CI-safe checks; nine dashboard regression groups; native captures including actual sidebar, populated/empty data and 1440/1000/760 widths. The selected reference and built screenshots were reviewed together. Corrected attention overflow, missing/partial icons, badge backgrounds and wrapped-pagination overlap. See design-qa.md and TEST_RESULTS/dispatch-* plus TEST_RESULTS/dashboard-parity. No customer business data writes or migrations. Diagnostic tests use HVAC_PRO_UI_20261014.

Final artifacts: artifacts/velopack-dispatch-final-1.1.512. Earlier candidate folders are superseded; do not publish them. Packaging excludes mutable customer files. Release-note and artifact guards are required. The existing distribution remains unsigned.

Publication remains pending under the standing one-version-per-rolling-24-hours rule: earliest 15 October 2026 20:00:06 UTC (22:00:06 Berlin), unless explicitly overridden. Do not overwrite a published version. Recheck remote releases/feed at publication. Retain 1.1.511 for rollback and do not automatically revert customer data.
