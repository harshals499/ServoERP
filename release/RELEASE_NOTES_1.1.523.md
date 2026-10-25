# ServoERP 1.1.523

Release date: 25 October 2026.

## User-facing summary
Automatic shared client, employee and job data refresh across desktop and mobile, with permitted client creation/editing and preserved draft input.

## Features and fixes
Mobile refreshes necessary reference lists every 30 seconds while active, including in an open form without replacing entered fields. It refreshes on resume/reconnection. Confirmed saves remain successful if the following refresh disconnects. Clients can be created and edited through existing role permissions, alongside employees/jobs. Desktop visible Clients/Employees/Jobs lists refresh automatically while editors and active input are protected. Data uses the same office SQL tables and optimistic version checks. New desktop clients and employees use the configured active office company; employee linking is transactional.

## Affected modules
Mobile forms/reference options, Office API client workspace, desktop Clients/Employees/Jobs lists and shared refresh helper.

## Database or migration impact
None. No schema migrations or customer-data experiments. Integration writes use a named EngineerTests disposable database cloned from an existing synthetic MobileE2E test database.

## Configuration and dependency changes
No new third-party service. Mobile adds no packages. The approved deployed API baseline restores its Core project reference, FluentValidation and sqlite-net-base/SQLitePCLRaw.bundle_e_sqlite3 dependencies relative to the committed desktop baseline. Existing SkiaSharp Linux assets are retained. Matching Office API 1.1.506 is required for mobile client editing. Mobile build 528; desktop candidate 1.1.523. Existing HTTPS, SQL, credentials and signing identity retained.

## Compatibility or breaking changes
Existing permissions, contract identities, field queues and findings/handoff/return-visit workflows retained. Administrative saves require a live office connection; no new offline business-write architecture.

## Security impact
Company/role scopes, parameters, save receipts, client validation and stale-version rejection retained. No permission grants or production configuration changes.

## Deployment/update steps
Install through the existing desktop updater or verified installer. Deploy matching Office API 1.1.506 and mobile 1.1.506 through their guarded release procedures. The office startup task requires Windows administrator approval and verifies a COPY_ONLY SQL backup first. Preserve configuration, SQL data and app storage.

## Validation performed
87 standalone mobile/core tests pass; Android Release and ARM64/x64 assembly-store verification pass. Office API and desktop Release builds pass, with HVAC_Pro_Desktop.exe present. Disposable SQL round trips verify shared client/employee/job records, desktop edits, idempotent retries, stale conflicts and company isolation. Real desktop repository and native Clients/Employees/Jobs checks pass, including refresh overlap/editor/disposal checks. New desktop reference records appear in the mobile snapshot; assigned jobs without sites load correctly. The exact final APK picked up newly desktop-created client/employee options in an open job form, preserved its title, and saved job 16 with client 7/employee 22 verified in disposable SQL. The desktop harness assembly binding configuration was corrected; all 13 native desktop checks now pass on 1.1.523, with fresh Clients/Employees/Jobs screenshots. Dependency audit output is retained. No production business-save tests were performed. Matching mobile/API activation is validated separately before its update feed is advertised.

## Known limitations
Refresh runs while the application is active; it is not guaranteed background execution on a closed phone app. Administrative creates/edits are online-only. Existing unmapped employees in multi-company databases require intentional company assignment. Office API startup activation and the mobile update feed are separate release steps; this desktop installer alone does not configure or deploy the office service.

## Rollback guidance
Retain previous desktop 1.1.522, mobile 1.1.504 and Office API 1.1.504 artifacts. Restore prior binaries/configuration if needed; preserve all SQL data and device drafts. Use a forward Android corrective build when a version downgrade is not permitted.
