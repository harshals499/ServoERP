# Dashboard design QA — 1.1.506

Result: passed, 11 October 2026.

Compared the approved softer dashboard reference with the rendered Release WinForms dashboard. Native visual evidence: `TEST_RESULTS/dashboard-soft-1.1.506/dashboard-final.jpg` and `hover-final.jpg`.

- Layout: four summary cards, priority inbox and jobs on the left, assignments/upcoming/collections on the right, activity below. Narrow layouts stack and scroll. Checked widths 1440, 1000 and 760.
- Typography and colour: restrained blue accents, light neutral canvas, rounded white cards, subtle borders and compact Segoe UI hierarchy.
- Interaction: existing action routes retained; shared inbox pagination verified. All ten cards have record previews. Hover previews stay within the monitor and hide over action buttons; F1 is available on focusable card controls.
- Sidebar: used the actual MainForm sidebar in the visual fixture. MainForm.cs was not edited by this task; its before/after SHA256 is 2985FA62067D3BF15AC00AED2534E30417A4705A582218E352107EDFA29637E8.
- Content/assets: existing icon system and Indian business formatting retained. Real data replaces illustrative reference values. Assignments describe recorded jobs, not inferred availability. Collections intentionally avoid a misleading paid/pending percentage.
- Security and states: module restrictions, technician job ownership and empty states passed deterministic fixture checks. Seven dashboard and six action-center checks passed. No business-data writes were exercised.

During review, corrected covered header buttons, clipped search shortcut and off-screen rightmost hover previews. Final native review confirmed these corrections. This is a production WinForms adaptation, not a pixel-for-pixel reproduction of the illustrative image.

Changed implementation: DashboardForm.cs, DashboardForm.SoftDashboard.cs, DashboardForm.ActionCenter.cs, DashboardHoverPreview.cs; scoped dashboard styling guards in DesignSystem.cs, UIHelper.cs and SharedUiPrimitives.cs; Program.cs preview/test switches; DashboardSoftSmokeTests.cs and project compile entries. Release metadata: VERSION, AssemblyInfo.cs, ServoERP.version.iss, CHANGELOG.md and versioned release notes. Existing unrelated workspace changes were preserved.

# Global search design QA — 1.1.507

Final result: passed, 11 October 2026.

Selected target: second displayed ideation image, exec-6f8fd5ea-1b1a-4445-9e5c-3120b409e592.png. Compared the image and native search-final.jpg together in one comparison input. The implementation preserves the wide results/preview split, query and filter strip, selected blue row, module icons, record details and single primary Open action. Adaptations: native Windows dialog frame, no background dimming, shared Segoe UI/icon system and actual record fields; no invented client links or availability. The image is illustrative; fixture data is used for screenshots.

Earlier visual findings: shared input outlines altered the query, layout auditing clipped categories, result icons fell back to generic symbols, and details could collide with actions in small windows. Fixed by scoped styling/layout opt-outs, custom input shell, existing icon-font glyphs, and a scrollable details area above a fixed action bar. Revised native capture confirms readable filters, separate search icon, job and invoice details, and unobstructed Open action. Standard and minimum sizes checked.

Validation: eight fixture checks, three read-only SQL checks and seven dashboard regression checks passed. Native Up/Down changed the job/invoice preview, Ctrl+K selected the query, Enter opened the selected fixture job, and Escape closed the search. MainForm.cs hash still matches the pre-task value above; no sidebar code edits or business data writes. New packages: none.

Files changed for this release: UI/GlobalSearchDialog.cs, SERVICES/GlobalSearchService.cs, MODELS/FoundationModels.cs, Infrastructure/ServoFormBase.cs; search-specific guards in UI/DashboardForm.SoftDashboard.cs, UI/GlobalDashboardLayoutService.cs and UI/LayoutAuditService.cs; Tests/GlobalSearchSmokeTests.cs, Program.cs and HVAC_Pro_Desktop.csproj; VERSION, Properties/AssemblyInfo.cs, Installer/ServoERP.version.iss, CHANGELOG.md and release/RELEASE_NOTES_1.1.507.md. Existing unrelated changes preserved.

Release packaging: Release EXE exists and embeds the exact versioned notes. Assert-ServoReleaseNotes and Test-VelopackReleaseArtifacts passed for 1.1.507; installer, portable ZIP, full package and nonblank version-matched feed are in release/search-1.1.507/velopack. Prepared locally; not published. Installer unsigned.

## Header option 2 - ServoERP 1.1.508 - 11 October 2026

### Intent and reference
Implement the selected two-level soft professional header throughout module pages and detail views. Preserve the actual sidebar exactly. Keep global search, record previews, keyboard shortcuts, local filters and existing module actions functional.

Selected reference: C:/Users/Administrator/.codex/generated_images/01a12c3b-575d-7a72-9707-ea7bcab3e8ca/exec-8751b10d-ef3b-46a1-b4f3-e8663eaa2d57.png.

### Rendered comparison and iterations
Compared the reference with the built WinForms app together, including the full dashboard with the original sidebar and a native preview of dashboard, client, report and invoice headers. The implementation retains the two white levels, centred rounded search, account avatar/name/role, divider, hierarchy, title, description, date and page actions. Typography uses the application's native Segoe UI. Breadcrumbs use the shared Workspace hierarchy, while the purchase editor retains its existing changing document hierarchy. Real signed-in account data and the current date replace mockup sample values. Existing operational status labels and page filters are retained.

Iterated after visual review: removed duplicated account metadata, prevented general polish from changing header layouts, removed the extra local-filter outline, retained preferred filter width after resizing, separated compact title/filter/action rows, explicitly placed the heading below the expanded utility bar at 600 pixels, corrected menu disposal so account callbacks run, replaced fallback icons for common header actions, and removed the duplicated glyph from the icon-only Settings button. The corrected final Settings button is named accessibly and visually reviewed in the final EXE.

### Verification
- Release build succeeded: C:/HVAC_PRO_MSE/SOURCE_CODE/bin/Release/HVAC_Pro_Desktop.exe, file version 1.1.508.0.
- 84 shared-header layout cases (21 module titles at 1440, 1000, 760 and 600 pixels) passed: controls contained; utility/title/status/filter/action rows do not overlap; existing callbacks, disabled states, editable filters, live titles and status references retained.
- Constructed all 25 module/detail/guide views and confirmed shared header coverage, including the retained Service Desk.
- Actual MainForm and owned-dialog account menus enable Settings and Change password; User Guide-wide Ctrl+K opens search and cancelling search retains the guide.
- Eight global-search fixture checks and seven dashboard regression checks passed on 1.1.508. Three read-only SQL checks passed for parameterized search, technician assignment restrictions and module restrictions.
- Native checks verified click and Ctrl+K search entry from a local filter, Escape, notification loading from the configured database, Customize's real menu, dashboard refresh and opening the existing change-password dialog. Passwords, records and card layouts were not changed through these checks.
- The final EXE's embedded release notes exactly match release/RELEASE_NOTES_1.1.508.md.
- Assert-ServoReleaseNotes.ps1 and Test-VelopackReleaseArtifacts.ps1 passed. Updater feed notes are nonblank and version-matched.
- MainForm.cs SHA256 remained exactly 2985FA62067D3BF15AC00AED2534E30417A4705A582218E352107EDFA29637E8. The visual harness reuses the actual sidebar control, not copied markup.
- No new packages, third-party services, schema changes, business-data migrations or public API renames.

### Evidence
- Native final headers: C:/HVAC_PRO_MSE/TEST_RESULTS/headers-1.1.508/module-headers-native.png.
- Native full dashboard and sidebar: C:/HVAC_PRO_MSE/TEST_RESULTS/headers-1.1.508/dashboard-native.png.
- Compact header captures: C:/HVAC_PRO_MSE/TEST_RESULTS/headers-1.1.508/header-600.png and header-760.png.
- Automated results: C:/HVAC_PRO_MSE/TEST_RESULTS/headers-1.1.508/smoke.txt, search-smoke.txt, dashboard-smoke.txt and sql-read.txt.
- Release evidence: C:/HVAC_PRO_MSE/TEST_RESULTS/headers-1.1.508/embedded-notes.txt, artifact-validation.txt and artifact-hashes.json.

### Files changed for this task
- New shared implementation: SOURCE_CODE/UI/WorkspaceHeaderPanel.cs.
- Shared integration and scoped guards: SOURCE_CODE/UI/SharedPageHeader.cs, DashboardForm.SoftDashboard.cs, PageHeaderPolishService.cs, LayoutAuditService.cs and GlobalDashboardLayoutService.cs.
- Page integrations and retained actions/status: SOURCE_CODE/UI/DashboardForm.cs, ReportForm.cs, PorterDeliveriesForm.cs, UserGuideForm.cs, ClientDetailPage.cs, JobDetailPage.cs, ServiceDeskForm.cs, GeoIntelligenceForm.cs and PurchaseForm.cs.
- Guide search return state: SOURCE_CODE/UI/GlobalSearchDialog.cs.
- QA/build registration: SOURCE_CODE/Tests/WorkspaceHeaderSmokeTests.cs, SOURCE_CODE/Program.cs and SOURCE_CODE/HVAC_Pro_Desktop.csproj.
- Release metadata: VERSION, SOURCE_CODE/Properties/AssemblyInfo.cs, SOURCE_CODE/Installer/ServoERP.version.iss, CHANGELOG.md and release/RELEASE_NOTES_1.1.508.md.
- This report: design-qa.md. Existing unrelated workspace changes were retained.

### Final release artifacts and limits
Use only C:/HVAC_PRO_MSE/release/headers-1.1.508/velopack-final for this release. It contains the final Setup EXE, full nupkg, portable ZIP, updater feed and adjacent versioned notes. The sibling velopack directory is an earlier superseded visual pass; it is not the release candidate. No publishing, installation, Git push or website deployment was performed in this task.

The fixture and safe interaction checks do not execute save, delete, password changes or other irreversible business actions. Existing module database requirements and body canvas sizing remain. Long titles use ellipsis; dense actions wrap. Signing is still unconfigured. Roll back by reinstalling 1.1.507; no database rollback is required.