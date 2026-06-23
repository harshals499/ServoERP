# ServoERP Cleanup Audit

Date: 2026-06-23
Scope: `C:\HVAC_PRO_MSE`

## Guardrails Applied

- Sidebar, navigation, sidebar icons, sidebar layout, sidebar colors, sidebar labels, sidebar spacing, and active states were not edited.
- No modules were removed.
- No database schema changes were made.
- No uncertain forms, controls, services, repositories, migrations, or production assets were deleted.

## Unused Or Generated Files Found

Clearly generated or temporary files found outside the compiled desktop project:

- `TEMP/` contained UI screenshots, smoke-test output images, temporary Excel files, temporary harness source/exe files, and visual proof artifacts.
- `BACKUPS/` contained dated manual update backup binaries/configs.
- Root temporary proof and harness files:
  - `DeploymentReadinessRunner.cs`
  - `PurchaseFormHarness.cs`
  - `SmokeInvoiceNew.cs`
  - `SupplierRecommendationFinal.cs`
  - `temp-pdf-proof.html`
  - `temp-pdf-proof.png`
  - `temp-render-proof.ps1`
- Generated script executables:
  - `scripts/HeaderInspectionCapture.exe`
  - `scripts/HeaderBarOnlyCapture.exe`

Items deliberately not removed:

- `lucide-main/` because `SOURCE_CODE/UI/LucideIconService.cs` references `C:\HVAC_PRO_MSE\lucide-main\icons` and `C:\HVAC_PRO_MSE\lucide-main\lucide-main\icons`.
- `LatoFont/` because it may be a packaged UI/document font asset.
- Source files not included in `HVAC_Pro_Desktop.csproj` but possibly used by installer/tools or pending work:
  - `SOURCE_CODE/Installer/Enterprise/tools/SqlExpressPrereqInstaller.cs`
  - `SOURCE_CODE/Installer/SQLServerSetupManager/Program.cs`
  - `SOURCE_CODE/Installer/SQLServerSetupManager/SQLServerSetupManager.cs`
  - `SOURCE_CODE/UI/SharedPageHeaderMode.cs`
  - `SOURCE_CODE/UI/SharedPageHeaderModel.cs`
  - `SOURCE_CODE/UI/SharedPageHeaderResult.cs`
  - `SOURCE_CODE/UI/WorkforceMetricCardResult.cs`

## Dead Forms/User Controls

No production form/user-control was removed.

Needs manual review:

- Designer partials with low direct text-reference counts are expected and not dead by themselves:
  - `AddAMCEquipmentForm.Designer.cs`
  - `AddAMCForm.Designer.cs`
  - `AMCDetailPage.Designer.cs`
  - `AMCPage.Designer.cs`
  - `MarkVisitCompleteForm.Designer.cs`
  - `TallyIntegrationForm.Designer.cs`
- `ClientUi.cs` is only 87 bytes and should be reviewed as a possible placeholder.
- `SharedPageHeader*.cs` and `WorkforceMetricCardResult.cs` are untracked/not all included in the project; review before either including or removing.

## Dead Event Handlers

No definitely dead production event handlers were removed.

Risk patterns found:

- Many buttons are wired through lambdas, making static dead-handler detection unreliable.
- Some handlers are attached from Designer partials.
- Some generated controls use local variables (`button`, `save`, `add`) with inline lambdas, so handler naming is inconsistent.

## Duplicate Helper Classes

Review candidates:

- `SOURCE_CODE/UI/GridTheme.cs`, `DS.StyleGrid`, `UIHelper` grid helpers, and form-local `Style*Grid` methods overlap.
- `SOURCE_CODE/UI/SharedUiPrimitives.cs`, `SOURCE_CODE/UI/UIHelper.cs`, `SOURCE_CODE/UI/DesignSystem.cs`, `SOURCE_CODE/UI/WorkforceModuleVisuals.cs`, and form-local `MakeButton`/`CreateCard` helpers overlap.
- `BaseForm` and `BaseUserControl` both apply global child-control styling patterns.

## Duplicate Styling Code

Repeated styling areas:

- Grid styling exists in `GridTheme.Apply`, `DS.StyleGrid`, `UIHelper`, and large forms such as `AttendanceForm`, `TenderBidForm`, `InvoiceForm`, `PurchaseForm`, and `VendorForm`.
- Button styling is repeated through local helpers like `MakeButton`, `MakeActionButton`, `NewPrimaryButton`, `NewOutlineButton`, and inline `Button` initializers.
- Card styling is repeated across dashboard/forms through local `CreateCard` methods and `ResizableCard`/`SharedUiPrimitives`.

## Duplicate Save Logic

Repeated save-flow patterns:

- Disable save button.
- Change text to `Saving...`.
- Validate.
- Save via service/repository.
- Show `MessageBox`.
- Re-enable button in `finally`.

High-value standardization targets:

- `AddAMCForm.SaveAMC`
- `ClientDetailPage.SaveClient`
- `ClientManagementForm` editor save lambda
- `ContractManagementForm.BtnSave_Click`
- `VendorForm.SaveVendorAsync`
- `TenderBidForm.SaveAsync`
- `BackupSettingsForm.SaveClicked`
- `ConnectionSetupForm.SaveConnection`

## Unused Imports/Usings

No broad automated removal was applied because this is a .NET Framework WinForms project with partial classes and generated designer patterns. Recommended safe follow-up:

- Use Visual Studio/Roslyn cleanup per file after each touched form/service.
- Avoid mass using-removal in designer or generated files.

## Hardcoded Test/Sample Data

Review candidates:

- `DatabaseManager.InsertSampleData` and `IsSampleDataReady` are real startup/demo-data paths; do not remove without product decision.
- `ExcelImportService.GetSampleRow` generates import templates; keep unless templates are redesigned.
- `WhatsAppHubService.BuildSampleContacts` appears to provide fallback/sample contacts; review business behavior before removal.
- `LanguageManager` contains many `TODO(mr)` / `TODO(hi)` translations; not dead code, but localization debt.

## Broken References

Baseline Release build passed before cleanup.

Known risky references:

- `LucideIconService` hardcodes absolute local icon roots. This works only when `lucide-main` exists at `C:\HVAC_PRO_MSE`.
- Several untracked source files appear in `git status`; some are included in the project and some are not. Review before deciding whether they belong in source control.

## Large God Classes/Forms

Largest cleanup/refactor targets by file size:

- `SOURCE_CODE/UI/PurchaseForm.cs`
- `SOURCE_CODE/DAL/DatabaseManager.cs`
- `SOURCE_CODE/UI/InvoiceForm.cs`
- `SOURCE_CODE/UI/JobManagementForm.cs`
- `SOURCE_CODE/UI/TenderBidForm.cs`
- `SOURCE_CODE/UI/PayrollForm.cs`
- `SOURCE_CODE/UI/VendorForm.cs`
- `SOURCE_CODE/UI/EmployeeForm.cs`
- `SOURCE_CODE/UI/SettingsForm.cs`
- `SOURCE_CODE/UI/InventoryForm.cs`
- `SOURCE_CODE/UI/ClientManagementForm.cs`
- `SOURCE_CODE/UI/PaymentForm.cs`
- `SOURCE_CODE/UI/MainForm.cs`
- `SOURCE_CODE/UI/TallyIntegrationForm.cs`

## Risky Cleanup Areas Needing Manual Review

- Any sidebar or `MainForm` navigation change.
- Any module removal or dashboard button removal.
- `DatabaseManager` sample data and schema guard logic.
- Hardcoded local paths that may be relied on by smoke tests or release tooling.
- Untracked source files already present before this task.
- UI visual standardization in large forms; must be smoke-tested visually form by form.
