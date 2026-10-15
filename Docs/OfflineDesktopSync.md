# Approved desktop offline architecture - 1.1.516

Harshal approved in this chat on 15 October 2026: local offline startup/work, automatic synchronization when the office server returns, conflicts retained for office review, and invoice/payment drafts saved offline but finalized online only. This is the deliberate SQLite business-write/sync design required by root AGENTS.md. No additional approval is required for this scope.

Baseline: verified public 1.1.515, source c271e26. Use this isolated managed worktree; no primary-workspace changes. Publication of 1.1.515 and the client command precede this implementation.

## User experience and scope
After a successful normal online login and initial download, the same Windows user can sign into a dedicated offline workspace with their existing credentials when the server is unavailable. It uses shared ServoFormBase/design primitives and a persistent offline/pending/last-sync status. Cached clients, sites, jobs, invoices and payments remain available; clients/contact details, sites and job scheduling/notes can be created/edited locally. Financial entries are clearly provisional drafts, never authoritative payments, invoices, balances, stock reservations, official numbers or posted documents. Draft finalization calls the existing online invoice/payment service after explicit review and confirmation. Other workflows continue in the normal online shell; unsupported offline actions are clearly explained rather than silently written elsewhere.

## Local durability and security
Use an office-and-Windows-user scoped SQLite database outside OneDrive, with FULL synchronous transactions, a busy timeout, parameterized Dapper data access, encrypted JSON payloads through CurrentUser DPAPI, and stable GUID operation/entity IDs. Bind profiles/queues to pinned OfficeDatabaseId, configured database/server and authenticated user. Initial online authentication seeds a BCrypt verifier plus permissions, protected by DPAPI; never store plaintext passwords, SQL credentials, session/refresh tokens or license secrets. Offline login requires the same previously verified identity, no forced password change, a bounded 30-day profile, and durable five-attempt/15-minute lockout. Existing license validation, endpoint, fingerprint and entitlements are reused without modification. Reauthenticate and recheck active user, password verifier/current credential marker, role and license before upload; revoked/changed accounts retain their local work but cannot replay it.

Saving updates the local record and immutable/stably identified pending operation in one SQLite transaction. Pending edits can coalesce only before submission; records currently submitting reject a competing edit with a retry message. Startup recovers abandoned Sending entries for retry with the original operation ID. Snapshot refresh never deletes or overwrites pending/conflicted rows. No unsynced work is silently purged.

## Server transaction and reconciliation
SQL Server HVAC_PRO stays authoritative. Add guarded DesktopOfflineReceipts, DesktopOfflineDrafts and DesktopOfflineConflicts tables plus nullable, uniquely indexed OfflineDraftId columns on Invoices and Payments. Do not alter LicenseInfo or drop/rename existing schema. Reuse existing client/site/job tables, GUID identities, FluentValidation and financial services.

Each operation binds actor, office, payload hash, base fingerprint and operation ID. Check the pinned office identity and freshly read user permissions before synchronization. Serialize per workspace and per server entity; receipt lookup, optimistic conflict check, permitted business mutation and operation receipt commit in one SQL transaction. Duplicate operation IDs with changed payload are rejected. Conflicts record both copies and the base fingerprint centrally in the same transaction, so office review sees the issue after the client disconnects. A retry cannot duplicate a committed operation. Missing parent dependencies remain pending rather than being discarded. Existing metadata and all relevant row columns contribute to fingerprints; office changes are never silently rebased.

Queue client/site/job operations in dependency order, then financial draft uploads. Refresh downloaded snapshots after acknowledgements. Retry connectivity errors with bounded delay; do not auto-retry conflicts as overwrites. Office reviewers can retain the server copy or explicitly revise/re-submit the local proposal against the shown current revision; fresh conflicts are checked again. Financial draft upload only persists DesktopOfflineDrafts. Finalization is online-only, guarded by a per-draft server lock and unique OfflineDraftId to reconcile a lost response without double posting; existing pricing/GST/stock/payment logic remains in its current services.

## Integration and acceptance
Online sign-in seeds encrypted profile/cache. Normal shell exposes Offline workspace and starts a cancellable single-flight periodic sync. Mid-session server loss routes supported work to the offline workspace. Offline startup avoids online schema/seed/backup/database settings calls; it uses the existing license policy and previous online enrollment. Returning server triggers automatic sync without restarting; display last sync, pending drafts and office-review conflicts.

Acceptance: offline startup and cached login; wrong-office/user/cache rejection; account lockout/revocation; durable local save/restart; reconnect and bidirectional refresh; exactly-once retry/lost response; same-row edit conflict without lost data; dependent creates; financial drafts never post offline; online finalization never duplicates; SQL rollback and retries; no audit bypass leakage; native visual evidence at normal and compact sizes. Write-capable tests use disposable explicitly named databases and temporary local cache only, never HVAC_PRO. Release build, executable, versioned embedded notes and package guards are required. Do not claim publication before verification. Normal unattended release pacing remains applicable to the next version.

## Official engineering references
- https://learn.microsoft.com/en-us/dotnet/api/system.security.cryptography.protecteddata?view=netframework-4.7.2 (Windows CurrentUser DPAPI)
- https://www.sqlite.org/lang_transaction.html (durable transactions and concurrency)
- https://learn.microsoft.com/en-us/sql/t-sql/statements/set-xact-abort-transact-sql (transaction rollback semantics)

## Evidence and state
Implementation begins after this design record. No offline acceptance result has been claimed yet. Existing partial OfflineSyncService is not an approved replacement for this complete startup/cache/authentication/transaction pipeline; preserve old queues and do not silently replay an unscoped legacy queue into a different office.

## Final validation evidence

The implemented scope has passed ten regression groups in the explicitly disposable database named in the versioned release notes, with temporary office/account-scoped caches. The tests exercised actual offline startup/sign-in, automatic reconnect without a manual sync, two-PC conflicts, lost operation/finalization responses, financial non-posting while draft, server/local transaction failures, current-account revocation, license denial and native UI rendering. The normal-shell entry was separately visually diagnosed, corrected and clicked in a focused preview. No migrations or business saves were run against HVAC_PRO. Existing license endpoint/machine-ID policy and PDF header code are unchanged. No new NuGet packages or services were added.

The dedicated offline workspace is the first supported offline UI; unsupported online workflows are not claimed to function offline. The profile can be enrolled after online password authentication/legal acceptance; completed downloads govern historical data availability. Older unscoped queue files are retained for origin review rather than silently replayed. Normal online modules retain their existing refresh behavior.

Evidence directory: C:\Users\Administrator\.codex\worktrees\startup-recovery-removal\HVAC_PRO_MSE\TEST_RESULTS\offline-516-20261015-230345

Source tests: SOURCE_CODE/Tests/OfflineDesktopSmokeTests.cs. Release logs are under release/ for 1.1.516. The runner invokes the built assembly directly and does not launch Program.Main or terminate another running application. The native main-shell preview suppresses unrelated Shown backup/updater/onboarding handlers. Publication of this candidate follows the root release-pacing policy after verified 1.1.515.
