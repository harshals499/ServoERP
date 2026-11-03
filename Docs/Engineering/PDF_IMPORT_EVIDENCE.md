# PDF Import implementation evidence — 03/11/2026

Approved design: review PDFs, select categories manually, separate final confirmation; dedicated sidebar page named PDF Import. No automatic classification or business records.
Base: 2dc4ef3 (clean committed 1.1.527 document-import branch). Main workspace has unrelated unfinished work and is untouched. Account usage at entry: 5% weekly.

Acceptance: recursive PDF discovery and default inclusion; independent readable page preview with navigation/zoom/password handling; editable saved categories and next/previous/skip; accurate counts; explicit summary/confirmation; duplicate and file failures isolated; import attachments atomically without financial/stock changes; imported documents accessible from their module; responsive native page and sidebar; relevant regression and visual evidence; Release EXE and version notes guards.

Affected files planned: new PdfReview model/service/repository/page/viewer/library/tests; MainForm navigation; shared module header action; csproj/Program test switch; VERSION/AssemblyInfo/changelog/notes.
Validation plan: no business writes to HVAC_PRO; model/file/renderer tests; SQL integration only against disposable ServoERP_PdfReviewQA_* database with minimal attachment schema; native built page review+summary at wide/narrow sizes; sidebar construction; Release build and notes/artifact checks. Existing local OCR and Windows PDF runtime reused without new NuGet packages or external services.
Official references: https://learn.microsoft.com/en-us/uwp/api/windows.data.pdf.pdfdocument.loadfromfileasync ; https://learn.microsoft.com/en-us/uwp/api/windows.data.pdf.pdfpage.rendertostreamasync . These API references were requested but the web reader returned an access error; verify runtime behavior with local rendering tests.
Publication: build review candidate first; no existing version overwritten, no customer data test writes. Record all actual results below.

## Validation evidence
- Release solution build succeeds; HVAC_Pro_Desktop.exe exists in SOURCE_CODE/bin/Release (1.1.528.0). No added NuGet packages.
- CI-safe suite passed (TEST_RESULTS/ci-smoke-20261103-093900.txt). Its entry points were inspected for business-write behavior before execution.
- Dedicated suite passed: TEST_RESULTS/pdf-review-final-validation/results.txt. Actual 2-page PDF rendering, unreadable file isolation and password-required/local unlock were tested. Tested source fixtures are fictional QuestPDF output; password fixture generated locally with bundled pypdf, which is not an application dependency.
- Confirmed SQL QA only: ServoERP_PdfReviewQA_20261103_* catalogs. Five category destinations, idempotent schema, duplicate refusal, metadata removal/re-import, forced-trigger rollback, snapshot tamper rejection, cancellation and no-session access rejection passed. No business tables were created. Minimal QA databases and local evidence retained; no production migration or save performed.
- Built native UI inspected at wide and narrow sizes. Final screenshots: TEST_RESULTS/pdf-review-final-validation/review-wide.png, review-narrow.png, summary-wide.png, summary-narrow.png, sidebar.png and shell-pdf-import.png. Sidebar location is Data & Compliance > PDF Import. Shell navigation, global refresh retention and cache-clear retention passed.
- An initial sidebar assertion was corrected to inspect AccessibleName because ServoERP custom-paints sidebar button labels. A later native lifecycle check exposed a preview callback after WinForms synchronization-context teardown; explicit UI-thread dispatch and disposal guards fixed it. Final dedicated suite completed without a new crash. No checks were bypassed.
- Source/security review: Dapper parameterised SQL; additive table only; new metadata cascades only with its attachment registration; no business-record or stock/payment writes; no new outbound services; passwords remain in memory/stdin; SHA256 snapshots and stored files verified; content duplicates serialized under database application lock.
- Account usage remained 5% in the available weekly window before packaging; no credits or increased limits used.

## Candidate artifacts
- Final Release solution build and CI-safe suite passed after final source/notes changes: 0 warnings, 0 errors (smoke-pdf-review.log; latest TEST_RESULTS/ci-smoke-*.txt).
- scripts/Assert-ServoReleaseNotes.ps1 passed with version 1.1.528, AssemblyInfo and ForPackaging; embedded notes match source exactly.
- Existing scripts/Prepare-VelopackPublish.ps1 and the existing vpk pack arguments produced artifacts/pdf-import-1.1.528/velopack. No publishing command was executed, and no required guard was skipped.
- scripts/Test-VelopackReleaseArtifacts.ps1 passed: setup/full package/feed exist, version matches and updater notes are nonblank/version-matched.
- Additional package review passed: packaged EXE SHA256 equals the Release build; packaged renderer equals source; no client configuration, license files, business database files or test/log directories are packaged; updater notes equal versioned notes exactly. SHA256SUMS.txt and packaging-evidence.txt are beside artifacts.
- Installer uses the existing unsigned packaging configuration (Authenticode NotSigned); no signing credentials were provisioned. This is a local candidate, not a published release.
- Rollback: retain the previously installed desktop package. If this candidate is installed and needs reverting, restore that application package; keep additive PdfReviewDocuments metadata and imported attachment files. No customer data rollback or automatic file deletion.
- Task-owned source commit and full artifact sizes/checksums are recorded in artifacts/pdf-import-1.1.528/candidate-metadata.json.
