# ServoERP Engineer 1.1.496 — Android MVP prerelease

Release date: 6 October 2026. Status: development-signed test APK; visual/device verification is incomplete and production deployment is gated.

## User-facing summary

Engineers can practise an entire service visit locally, or connect to an explicitly enabled office HTTPS API: receive an assigned job, travel, arrive, diagnose, record work/checklists/parts/photos, obtain customer sign-off and submit the service report. Work is durably queued on the phone and retained through connectivity failures. Field completion remains separate from office job closure and invoicing.

## Features and fixes

- Simple TODAY, JOBS, SCAN, SYNC and PROFILE navigation, light theme and large actions.
- Assigned job details, customer call/navigation actions and sequential visit lifecycle.
- Diagnosis, work performed, testing, checklist Pass/Fail/N/A with readings and notes.
- Provisional van availability and confirmed pending parts consumption; explicit no-parts review.
- Photo capture/picking, orientation correction, JPEG compression and private staged upload.
- Customer signature vectors bound to the current job/report content, or a recorded reasoned sign-off exception. Report edits invalidate existing sign-off.
- Account/server-scoped SQLite snapshots and an immutable durable outbox with retries, retained conflicts and ordered dependencies.
- Secure short-lived mobile access credentials, rotating refresh credentials, revocation, active membership/permission checks and engineer-assigned job isolation.
- Idempotent server command receipts and transactional stock, field report, activity and audit writes. Legacy office edits are detected with a fingerprint as well as SyncVersion.
- Local training job and stock exercise the workflow without changing office data.

## Affected modules

New Engineer.Contracts, Engineer.Core, Engineer.Mobile and Engineer.Tests projects; existing ServoERP.Api mobile extension; new Engineer solution/build script and architecture documentation. Desktop assembly/release metadata is incremented to 1.1.496. No desktop form or existing PDF header is changed by the mobile implementation.

## Database and migration impact

An explicit guarded SQL script is supplied at SOURCE_CODE/ServoERP.Api/Mobile/MobileSchema.sql. It adds mobile session/report/receipt/attachment/van allocation/audit data and additive checklist/photo-policy fields. Stock-allocation guards also protect against incompatible legacy stock writes. Existing company isolation and sync schema prerequisites must be independently verified. The script fails closed when prerequisites are missing. No script has been applied to production. LicenseInfo remains unchanged. Test stock-allocation triggers and existing office stock workflows on an isolated database before release.

## Configuration and dependency changes

Mobile routes default to disabled. Connected use requires ServoERP__EngineerMobileEnabled=true, a private absolute ServoERP__EngineerMediaDirectory, an existing configured SQL Server API and a valid HTTPS endpoint. Provision engineer identity, company membership, WorkOrders permissions, Inventory permissions for parts and explicit van allocations through controlled office administration.

New dependencies: sqlite-net-pcl 1.9.172, SQLitePCLRaw.bundle_green 2.1.11, Dapper 2.1.66, BCrypt.Net-Next 4.0.3, FluentValidation 11.11.0, Serilog.AspNetCore 9.0.0, SixLabors.ImageSharp 3.1.12 and Serilog.Sinks.File 6.0.0; xUnit/Test SDK/ASP.NET test host for isolated validation. See project files for exact dependency ownership. Android builds use .NET 9 MAUI and Android SDK 35. The APK includes arm64-v8a and x86_64 support.

## Compatibility and breaking changes

None to existing public desktop APIs, license plans, pricing, machine identity or PDF headers. Existing desktop API authentication is retained. Mobile credentials are separate. Stock guard behaviour must be checked against office stock adjustments before applying the schema. iOS compatibility is architectural only; iOS has not been built.

## Security impact

The APK does not contain SQL credentials, SQL connectivity or the office installation API key. HTTPS and Android cleartext prohibition are enabled; Android backup is disabled. Credentials use SecureStorage; server credentials are stored as hashes. API commands are company/engineer/permission scoped. Photos are staged in a private directory and decoded/re-encoded with limits and metadata removal. Local report/photo data remains on the device in the application's private storage; manage lost devices through your existing device policies. No new third-party automated service is added.

## Deployment and update steps

1. Install the development-signed APK on a compatible 64-bit Android device. Enable installation for the trusted download source if Android requests it.
2. Use EXPLORE TRAINING JOB to inspect the local sample workflow. This does not contact the office.
3. For connected testing, use an isolated copy of the office database, review/apply the guarded schema, configure a private media directory and HTTPS, then enable the mobile routes explicitly.
4. Provision engineer membership/permissions and van allocation. Sign in using the HTTPS office origin, company number and existing username/password.
5. Validate online/offline transitions, refresh/logout, reassignment, duplicate stock commands, conflicting office edits and legacy office stock writers before any production deployment.
6. Establish a production signing key and verify signing/update continuity before distribution to customers or Play Store submission. This prerelease uses a local development key.

## Validation performed

- Android Release build: success, zero warnings and zero errors.
- Desktop Release build: success; SOURCE_CODE/bin/Release/HVAC_Pro_Desktop.exe exists, file version 1.1.496.0.
- API Release compilation: success through the test build.
- Automated tests: 8 passed, 0 failed, 1 skipped. Covered durable restart, queued photos/checklists, invalid transitions, completion/signature rules, conflict retention/order, retry identity and rejected/disabled unauthenticated API paths.
- The real SQL fixture was not executed: isolated LocalDB is unavailable, and installation requires administrator rights. No production database was used as a substitute.
- APK signature verification: passed APK Signature Scheme v2 and v3; package ID in.servoerp.engineer, versionName 1.1.496, versionCode 496, minSdk 21, targetSdk 35, native arm64-v8a/x86_64.
- Android emulator visual/workflow validation could not be completed. Emulator 37.2.12, 35.6.5 and 33.1.24 failed before Android became available; software-renderer, hidden-window and CPU fallback attempts also failed or stalled. ADB reports no connected device. No screenshot or successful app-launch claim is made. Install and test on a real Android device before approval for engineer use.

## Known limitations

Actual app launch, rendered screens and device workflow remain unverified because the local emulator fails before boot. Camera QR/asset history is deferred; SCAN currently looks up assigned jobs by number. iOS is unvalidated. Real SQL integration/concurrency and office compatibility testing remain required. Office completion/closure stays separate. The development signing key is not a production distribution key. Training pending operations remain local and are not uploaded to the office. The APK is a test prerelease, not a claim of production readiness or completed visual QA.

## Rollback guidance

Disable ServoERP__EngineerMobileEnabled, then restore the preceding API deployment if needed. Keep additive mobile tables/columns and saved receipts for audit/reconciliation; do not drop them during rollback. Back up the database and private media before any schema work. Preserve or export pending mobile work before removing the app; uninstalling Android apps removes their private data. Existing office jobs and invoicing remain authoritative.
