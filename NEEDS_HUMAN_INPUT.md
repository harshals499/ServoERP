# ServoERP — Needs Human Input

## Git metadata is read-only in the active workspace

- Observed: source files are writable, but `git add` fails with `Unable to create 'C:/HVAC_PRO_MSE/.git/index.lock': Permission denied`. No stale lock file exists.
- Decision/action needed: allow this Codex workspace to write `C:\HVAC_PRO_MSE\.git` so the reviewed changes can be staged and committed atomically.
- Cost: EUR 0. This is a local workspace-permission change only.
- If unresolved: the working tree can be reviewed and built, but it cannot be made clean or committed.

## Permanent zero-incremental-cost constraint

- Harshal requires all current and future autonomous work to incur EUR 0 in new or incremental charges.
- Do not purchase subscriptions, licenses, domains, hosting, API credits, paid tiers, or billable third-party usage.
- Existing infrastructure may be used only when the action is confirmed not to create an additional charge. Free trials and ambiguous free-tier limits are not sufficient evidence.
- If any action could incur a charge now or later, stop that action, document the exact/estimated cost and consequence, and wait for explicit approval for that specific instance. Silence is never approval.

## Uncommitted source baseline after public 1.1.435 release

- Observed: the existing release log records successful GitHub and Cloudflare publication of version 1.1.435 at 2026-08-16 23:10:44 +02:00, while Git remains at commit `37529b1` (1.1.416) with a large uncommitted worktree.
- Protected scope: the uncommitted baseline includes `SOURCE_CODE/UI/MainForm.cs` (46 added and 10 removed lines) plus many other application, API, configuration, and release-metadata changes.
- Decision needed: confirm that the complete 1.1.435 source baseline is approved and should be committed, or identify which changes must be excluded/reworked. The autonomous agent will not absorb or rewrite protected MainForm changes without Harshal's decision.
- If unresolved: subsequent source commits and public packages cannot be proven to correspond atomically to their release version.

## Missing distinct beta/staging deployment channel

- Observed: the configured GitHub/Cloudflare release manager publishes to the public update channel. `SOURCE_CODE/Installer/Nightly-Build-Deploy.ps1` copies locally to `C:\Deploy\ServoERP`, is restricted to 23:30 or later, and contains the placeholder `PASTE_YOUR_CODEX_WEBHOOK_URL_HERE`.
- Decision needed: designate/configure a recoverable beta destination and its promotion mechanism, or approve the local nightly folder as the beta contract after the placeholder webhook is removed or configured.
- Cost: no new paid service is requested; expected cost is EUR 0 using existing infrastructure.
- If unresolved: verified builds remain local and public promotion remains gated rather than misusing the public R2 channel as beta.
