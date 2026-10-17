# Mobile priority 1 — implementation record

Baseline: committed release cf15679458f8a0169fd1643b44e19330a5e5327d (1.1.516). Primary checkout and other worktrees are preserved. No ServiceTitan source is reused.

Evidence: mobile contracts only allow a linear successful visit; failed required checks block completion. Reports, photos, acknowledgement, receipts, SQL transactions, outbox and office job scheduling already exist.

Scope: append-only findings with stable IDs and explicit resolution; exception handoff with reason and next action; retained return-visit requests and resume history. Reuse EngineerReports JSON, EngineerReceipts, EngineerAudit, JobActivityLog and the existing outbox. No new database tables or business statuses; office dispatch continues to schedule through Jobs. No pricing, PDF, license or production database changes.

Acceptance: failed checks retain their outcomes; active exceptions and open findings prevent normal successful completion; exception handoff remains possible before work starts; edits invalidate acknowledgement; findings/return requests survive restart, lost acknowledgements and retries without duplicates; stale/reassigned jobs remain conflicts; server transaction rolls back all projections on error; office users can review the handoff through the existing job workflow.

Affected paths: Engineer.Contracts, Engineer.Core, Engineer.Mobile, Api/Mobile, Engineer.Tests, desktop job report display, version/release notes. Existing numeric enum values and public methods remain compatible. New operations require upgraded API; old clients must fail closed when a report contains active handoff.

Validation plan: shared-rule and real SQLite restart/retry tests, real SQL transactions in generated EngineerTests_* databases only (opt-in SQLEXPRESS test host or dedicated LocalDB), API authorization regressions, Android/API/desktop Release builds, built Android emulator screenshots and manual exception/finding/return flow. Retain actual results below before delivery. No production migration or publication is part of this change.

## Validation results

- Implemented on branch codex/mobile-findings-return-visits for version 1.1.517; no primary-checkout changes.
- 33 tests passed, zero skipped (priority1-verified.trx). Two opt-in real-SQL tests used generated EngineerTests_* databases on SQLEXPRESS; no customer database writes. Tests retain those databases for inspection.
- Concurrent duplicate finding submissions, lost responses after finding/handoff commit, process restart, payload-ID mismatch, transaction rollback after report/activity/audit writes, stale second device, cross-company access and reassignment passed. Normal Jobs status/schedule remained unchanged.
- Interrupted photo upload, retained upload receipt after lost acknowledgement, blocked dependent commands, independent-job progress and mismatched receipts passed against the real SQLite workspace.
- Android Release: zero warnings/errors; development-signed APK produced. API Release build: succeeded. Replaced ImageSharp with SkiaSharp 3.119.4 and Linux native assets; pinned Pkcs 9.0.21. API direct/transitive vulnerability audit is clean. Photo regression tests cover all eight EXIF orientations with pixel placement, metadata removal, JPEG/PNG limits, transparency and malformed/truncated inputs. Desktop Release: succeeded; required EXE present.
- Office card: actual built WinForms accordion rendered with synthetic handoff/return content and visually checked; no application startup or business save invoked. Diagnostic connection target was unreachable loopback.
- Android visual/touch checks passed on Android 11/API 30 emulator, APK version 1.1.517 (code 517), 720x1280 at density 280 (same logical phone width as native 1080x1920/420). AEHD 2.2 is installed and usable; sys.boot_completed=1. The checksum-verified Google driver became available after the manual installation request; automatic approval review had blocked elevated installation.
- Training-only UI checks: empty handoff validation; customer-unavailable handoff at Accepted; resume note; failed required checklist; linked finding with office owner and next action; return date picker changed to 19/10/2026; handoff hides ordinary work controls; force-stop/relaunch retains finding and date; empty resolution validation; retained resolved history; resume retains failed check; completion confirmation rejects outstanding checklist/report requirements. Actual screenshots and UI XML retained in artifacts/priority1/android-*. No office login or business writes used.
- Clean-checkout Android build initially exposed missing assets in the committed baseline. Restored only committed ServoERP artwork/Lucide icons and Open Sans fonts from the installed SDK template; no unfinished primary-workspace assets copied.
- Existing mobile schema reused unchanged; no new migrations, tables or services. Dependency replacements are listed in release notes. No production deployment was performed. Publication of 1.1.517 with API and APK was explicitly authorized on 17 October 2026.
- Local Velopack installer/package generated without skip switches. Assert-ServoReleaseNotes.ps1 and Test-VelopackReleaseArtifacts.ps1 passed; feed notes are nonblank and version-matched. Artifact hashes are retained in artifacts/priority1/SHA256.json.

Evidence: artifacts/priority1, version-matched release notes, and Engineer.Tests/TestResults/priority1-photo-verified.trx. Android visual gate is cleared for these training workflows. Production signing, real-device pilot and deployment are not performed; SQL/SQLite integration tests provide separate sync evidence, not a live-device office sync claim.

Release packaging: Windows x64 framework-dependent API publish; existing local-development-key Android signing; full desktop CI-safe routine passed in a separate harness with unreachable diagnostic SQL. Added the Open Sans Apache 2.0 notice to APK assets. An additional API process-start attempt was blocked by automatic approval review; no live office/API startup verification is claimed.
