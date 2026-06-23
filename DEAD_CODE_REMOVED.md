# Dead Code Removed

Date: 2026-06-23

## Removed

Only clearly generated, temporary, backup, or harness artifacts were removed.

- Cleared contents of `TEMP/`.
- Cleared contents of `BACKUPS/`.
- Removed root harness/proof files:
  - `DeploymentReadinessRunner.cs`
  - `PurchaseFormHarness.cs`
  - `SmokeInvoiceNew.cs`
  - `SupplierRecommendationFinal.cs`
  - `temp-pdf-proof.html`
  - `temp-pdf-proof.png`
  - `temp-render-proof.ps1`
- Removed generated executables:
  - `scripts/HeaderInspectionCapture.exe`
  - `scripts/HeaderBarOnlyCapture.exe`

## Not Removed

- No production module was removed.
- No production form/user-control was removed.
- No service, repository, model, migration, or database script was removed.
- No sidebar or navigation file was intentionally edited.
- `lucide-main/` was kept because `LucideIconService` references it directly.
- `LatoFont/` was kept because it may be a packaged font asset.

## Rationale

The removed items were not part of the Release build output path and matched temporary proof, smoke-test, backup, or generated executable patterns. Anything requiring semantic product knowledge was left in place and listed in `CLEANUP_AUDIT.md`.
