# ServoERP 1.1.517

Release date: 17 October 2026.

## User-facing summary
Engineers can record outstanding findings, hand blocked visits to office dispatch, request a return date and resume with an instruction note. Failed checks remain failed; office job closure remains separate.

## Features and fixes
- Stable finding IDs, responsible team, checklist links and explicit resolution evidence.
- Exception reasons for parts, approval, customer availability and return work; retained handoff and return history.
- Existing durable outbox and SQL receipt transaction carry findings and return requests without duplicate projections on retry.
- Reject mismatched sync receipts before replacing a local draft. Preserve legacy operation hashes and acknowledgement hashes for reports without new content.
- Office job details display the existing activity feed; dispatch reuses Scheduled Date and Technician.
- Replace vulnerable ImageSharp with MIT-licensed SkiaSharp for bounded, oriented, metadata-free JPEG photo uploads; reject invalid images with a client error.
- Pin the transitive System.Security.Cryptography.Pkcs dependency to patched 9.0.21.
- Restore omitted mobile assets using committed ServoERP artwork/icons and SDK-provided Open Sans fonts so clean checkouts can build.

## Affected modules
Engineer mobile, shared field contracts, local workspace, Office API mobile service, desktop job details and regression tests.

## Database or migration impact
None: reuse EngineerReports JSON, EngineerReceipts, EngineerAudit and JobActivityLog. Existing guarded MobileSchema.sql remains required on mobile-enabled installations. No production schema or data changes performed.

## Configuration and dependency changes
Removed SixLabors.ImageSharp 3.1.12. Added SkiaSharp 3.119.4, SkiaSharp.NativeAssets.Linux.NoDependencies 3.119.4 and System.Security.Cryptography.Pkcs 9.0.21. Publish the full API output including native runtime assets. No new external service. Existing MAUI assets and the Open Sans Apache 2.0 redistribution notice are included in source control and the notice is bundled in the APK. SQL integration tests have an explicit SQLEXPRESS test-host opt-in and create only generated EngineerTests_* databases.

## Compatibility or breaking changes
Existing numeric operation values and public APIs remain. Deploy the upgraded Office API before the new mobile client. Older APIs reject new actions and retain them as failed local operations. Legacy clients cannot advance a report with an active handoff through the upgraded server. No pricing, licensing or PDF header changes.

## Security impact
Company/engineer assignment and WorkOrders permission checks remain in the existing SQL transaction. Input lengths, finding ownership, checklist links and operation receipt identity are validated. Removed ImageSharp advisory warnings without suppressions. NuGet audit including transitive API dependencies reports no known vulnerable packages. Photo headers are bounded before pixel decoding; invalid, truncated and unsupported images return HTTP 400. Fresh JPEG output removes source metadata.

## Deployment/update steps
Update Office API first, then mobile; retain device outboxes. The API ZIP is a framework-dependent Windows x64 publish requiring the .NET 9 ASP.NET Core runtime. Extract the complete package, retaining private configuration, HTTPS certificate, database connection, API key and media directory; do not replace these with blank template values. Keep EngineerMobileEnabled and EngineerMediaDirectory configured on mobile-enabled installations. Preserve runtime/native assets. The APK uses the existing local development signing key; it is not a production/Play Store signing release. No new migration is required. Dispatch must confirm return appointments in Jobs; a requested date is not an automatic booking.

## Validation performed
33 tests passed with zero skips, including actual disposable SQL Server transactions: concurrent duplicate submissions, lost acknowledgement, rollback after projections, stale reports, company isolation and reassignment. SQLite restart, interrupted photo upload and retained upload receipt recovery also passed. Android Release built with zero warnings/errors and a development-signed APK. API Release build and fresh publish succeeded without warnings; direct/transitive dependency audit is clean. JPEG/PNG normalization, transparent-background flattening, size limits, invalid/truncated inputs and all eight EXIF orientations passed (pixel placement and metadata removal). Desktop Release succeeded and HVAC_Pro_Desktop.exe exists; the notes guard passed. The full desktop CI-safe suite passed through an isolated harness using the built assembly, correct native-library path and an unreachable loopback diagnostic database; ordinary application startup was not invoked. The built desktop field-activity accordion was rendered and visually reviewed with synthetic data and an unreachable diagnostic database target. Android API 30 emulator booted using AEHD 2.2; actual 1.1.517 APK touch tests passed for empty input validation, pre-work customer-unavailable handoff, failed-check finding, return date selection, resume, finding resolution, force-stop/relaunch retention, preservation of failed outcomes and blocked completion. Screenshots/UI XML retained locally. Training mode used no office writes; real SQL sync was tested separately.

## Known limitations
Training flows were visually tested on an Android 11 emulator, not a physical device or a live office connection. Existing light-theme navigation titles/placeholders have low contrast; theme values were preserved under repository instructions. Resume notes record engineer acknowledgement of office instruction, not a new office approval system. Production signing and a live-device pilot are not performed. Publishing these artifacts does not deploy a live office API. An additional API process-start smoke attempt was blocked by automatic approval review; no live API startup validation is claimed.

## Rollback guidance
Disable mobile routes if necessary, retain device outboxes and SQL report/receipt/audit rows, restore prior binaries, and review pending new operations before reconnecting old clients. Do not clear queues, drop tables, reset stock or roll back customer data.
