# ServoERP 1.1.484.0 Release Notes

Release date: 4 October 2026

## User-facing summary

ServoERP 1.1.484.0 restores the graph calculation preview and consolidates the public product history into one complete client build. Reports graphs now keep the selected point's exact value and mathematical calculation visible inside the card while also showing the hover tooltip. The release also includes the complete shipped feature history represented by the latest 50 version tags, plus the relationship and Site Monitor work from 1.1.479 through 1.1.483.

## Features and fixes

- Restored exact-value and calculation hover cards on Reports graphs and added a persistent in-card calculation preview strip.
- Added profitability formulas for revenue, direct cost, and gross-margin percentage.
- Added Relationship Health formulas for coverage, parent integrity, and client/site context consistency.
- Reintegrated every release represented by the latest 50 Git version tags into the current branch.
- Restored previously omitted shipped components, including chart-hover infrastructure, Site Monitor revenue attribution, automatic updates, document previews, duplicate cleanup, Porter delivery, invoice receivables, application theme/icon support, and their automated tests.
- Preserved all relationship integrity, Site Monitor entitlement, and All Sites by Region filter changes from 1.1.479 through 1.1.483.
- Added a publication gate that prevents future packaging if any of the latest 50 tagged releases is missing from the branch history.

## Affected modules

- Reports and Relationship Health
- Dashboard graph interactions
- Site Monitor and site revenue reporting
- Invoices, document previews, and receivables
- Smart Upload duplicate cleanup
- Porter delivery workflow
- Settings, theme, application icons, and automatic updates
- Release packaging and regression tests

## Database or migration impact

No new 1.1.484 schema change. The build restores the existing additive, guarded schema behavior from prior public releases and retains the relationship-integrity safeguards introduced in 1.1.479-1.1.481. There are no table or column drops, renames, or irreversible data actions.

## Configuration and dependency changes

The source tree again contains the previously shipped locked dependencies required by the cumulative release line, including Krypton Toolkit and the existing .NET compatibility packages. No new third-party service or outbound integration was added in 1.1.484.

## Compatibility or breaking changes

None. Existing databases, license snapshots, public interfaces, update packages, and saved application settings remain compatible.

## Security impact

No new security boundary was introduced. Existing license validation, tamper blocking, safe update behavior, parameterized data access, and relationship validation remain enabled.

## Deployment and update steps

1. Close ServoERP if it is running.
2. Install ServoERP 1.1.484.0 using the published installer, or accept the automatic in-app update.
3. Start ServoERP and sign in with an activated license.
4. Open **Reports > Relationship Health** and hover a graph point to verify the exact percentage and calculation text.
5. Open **Site Monitor** and confirm the All Sites by Region Search, Region, Work, count, and Clear controls remain available.

## Validation performed

- Verified all latest 50 Git version tags are ancestors of the integrated build: 50 included, 0 missing.
- Restored the locked NuGet package set and completed a Release rebuild with 0 warnings and 0 errors before the version bump.
- Full CI smoke passed, including chart hover calculations, Site Monitor revenue, Site Monitor filters, document preview schemas, and all 100 relationship checks.
- Rebuilt version 1.1.484.0 in Release configuration with 0 warnings and 0 errors.
- Re-ran the full CI-safe smoke suite against the versioned executable; chart calculations, Site Monitor behavior, document previews, and all 100 relationships passed.
- Rendered and visually inspected the Relationship Health card at 1537 x 861; the selected module, exact value, formula, 94/100 auto-connected count, review count, orphan count, and client/site issue count are visible.
- Packaging, hashes, production feed, and public download checks are recorded below after publication.

## Known limitations

- Empty reports have no graph point to inspect; populated reports retain the first or most recently hovered point in the in-card calculation preview.
- Site Monitor requires an activated ServoERP license.
- The Windows installer is not code-signed, so Windows may show a publisher warning.
- GitHub protected the auto-created `v1.1.484` release tag before the final source commit and rejected retargeting it. The published binaries are the verified cumulative build; its exact source is commit `877aea2` on `main`.

## Rollback guidance

If rollback is required, close ServoERP and reinstall version 1.1.483.0. No database rollback is required because 1.1.484 introduces no new schema migration.

## Publication verification

- Release workflow: passed every stage, including cleanup, cumulative release audit, versioning, Release build, Velopack packaging, update ZIP creation, GitHub publication, Cloudflare publication, and public verification.
- Cumulative release audit: latest 50 tagged releases included; 0 missing.
- Final executable: `HVAC_Pro_Desktop.exe` version 1.1.484.0, SHA-256 `300bcf1dbdf9f81f8858b9aaf8a13070ec33e6d9b831a92389a1f2987a43b514`.
- Public installer: `ServoERP_Setup_1.1.484.0.exe`, 66,450,044 bytes, HTTP 200, SHA-256 `b92401a57f46bacb9fd0672a5d18fde5ce0c3c2f696b80ae4473de98a51a8054`.
- Full update package: `ServoERP.Desktop-1.1.484-full.nupkg`, SHA-256 `ec72d9dc5bbcac104519a98ea2c0044e1c0ec1fe6279f03f36d43a744bf0474b`.
- Delta package from 1.1.483: `ServoERP.Desktop-1.1.484-delta.nupkg`, SHA-256 `909c9bccc9aa34f45293e34a2e5c557c01ef27c2f33f20a96e10dc7e169b008f`.
- Legacy update ZIP: `ServoERP_Update_1.1.484.0.zip`, 48,111,069 bytes, HTTP 200, SHA-256 `ee71b9ce299ee5b07944788ca18719f6667bcdad4f6092a1da5bb43b5c7e3f3f`.
- Production update feed returned `latestVersion` 1.1.484.0 with the correct public installer and full-package URLs.
- GitHub release `v1.1.484` is published, non-draft, and non-prerelease with the full, delta, portable, setup, and feed assets.
- The source used for the packaged cumulative build is committed and published on `main` and `codex/madhusuman-sync` at commit `877aea2`.
- Final packaged executable CI-safe smoke report: `TEST_RESULTS/ci-smoke-20261004-201558.txt`.
- Visual evidence: `TEST_RESULTS/reports-explorer-1.1.484/relationship-calculation-hover-20261004-201115.png` and its matching report.
