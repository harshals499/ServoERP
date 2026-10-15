# Startup recovery removal - release 1.1.515

Authorized by Harshal: remove the upgrade feature blocking startup; publish the verified fix before implementing the separately approved offline design.

Evidence: authoritative database has 0002_LegacySchema_1.1.513.0 Running with NULL completion/error. Original interruption cause unknown. Restored-copy testing exposed maintenance audit replay waiting for SQL memory. Public 1.1.514 is an existing logo-only header release, source 4ac695e236ab8d10c6155a10310d3630ac2d1396, so it must not be replaced.

Baseline: exact published 1.1.514 source commit in an isolated managed worktree. Preserve primary workspace modifications. No unrelated primary-workspace source will be published.

Acceptance: incomplete/future migration journal entries do not block startup or get modified; no forced upgrade backup, journal/hash/downgrade gate, retry dialog/command or retired policy resource remains active; legacy and financial schema still work; maintenance audit replay suppressed without leaking bypass flags to business connections; real schema errors propagate.

Affected files: DatabaseUpgradeService, DatabaseManager private schema helper, Program retired recovery handlers, FinancialRecordProtection maintenance context, financial test backup helper, retired policy and old tests, new regression harness, project resource list, VERSION/AssemblyInfo/installer version, changelog and release notes.

Validation plan: Release build and executable/version verification; standalone regression on explicitly identified disposable QA database restored from existing backup, never migrations/business saves against HVAC_PRO; embedded notes exact match; Assert-ServoReleaseNotes and Test-VelopackReleaseArtifacts; customer-file exclusion checks; attributable task source commit and checksums; verify remote release/feed/assets and preserve previous release. No requested visual UI change.

Offline follow-up approval: local cached operation, automatic reconciliation, conflicts preserved for office review, and offline invoice/payment drafts finalized online only. This is a separate next version after publication of the startup fix.
