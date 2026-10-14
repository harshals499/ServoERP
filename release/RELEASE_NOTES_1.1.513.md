# ServoERP 1.1.513

Release date: 14 October 2026.

## User-facing summary
A consistent modern global header across main dashboards, detail pages and nested workflow editors, with the actual ServoERP logo, broad search and clear account controls. Existing sidebar branding is retained.

## Features and fixes
- Use the original ServoERP artwork and wordmark in the white global toolbar.
- Add a wider search surface, Ctrl+K key capsule, borderless notifications and readable account identity.
- Adapt the toolbar to narrower editors with a second search row and compact account control.
- Extend the same shared header to standalone workflow editors without duplicating chrome in embedded pages.
- Preserve module actions, local filters, account navigation and existing keyboard save/cancel behavior.
- Protect header controls from module-specific button restyling.
- Keep nested editor content scrollable and correct clipped AMC editor heading/field space.

## Affected modules
Shared workspace header; main and detail pages that consume it; base page/form workflow editors; header regression and screenshot tooling. Sidebar branding remains unchanged.

## Database or migration impact
None. No schema or business-save changes. Render-capable validation uses the explicitly disposable HVAC_PRO_UI_20261014 database.

## Configuration and dependency changes
The existing official transparent logo is distributed with the application. No new packages, third-party services or configuration settings.

## Compatibility or breaking changes
None. Existing public module interfaces and business workflows are retained. Light theme remains the default.

## Security impact
No new permissions, credentials or outbound services. Existing search/account/notification routing remains in use.

## Deployment/update steps
Install using the existing ServoERP installer/updater. Close the application normally when prompted. No database migration or manual configuration is required. Immediate bumped publication was explicitly requested by Harshal.

## Validation performed
Release build succeeded with the EXE present. Thirty-nine CI-safe checks and nine dashboard regression groups passed. Header tests cover 21 module titles at 1440/1000/760/600px, 25 main/detail constructors, nested editors at 430/600/1440px, action/disabled/input preservation, account routing and Ctrl+K search/cancel. Native screenshots of 32 distinct main/detail/editor surfaces were captured and header contact sheets reviewed against the selected reference. Nested editor bottom-scroll captures confirm Notes/Save/Cancel remain reachable. Payment header restyling and editor docking/scroll issues were corrected and recaptured. Sidebar source and asset are unchanged. No customer business-save tests were performed. Release-note and Velopack artifact guards are required before publication.

## Known limitations
Native Windows control rendering remains; representative screenshots do not exhaust every DPI, user role or data length. The existing distribution pipeline remains unsigned. Transient search, alert and confirmation dialogs retain their focused layouts.

## Rollback guidance
Retain the verified 1.1.512 installer and normal configuration/database backups. Close ServoERP and reinstall 1.1.512 if rollback is needed. Do not automatically revert customer data.
