# Duplication Report

Date: 2026-06-23

## Summary

ServoERP has the most duplication in WinForms UI construction, save workflows, grid/card styling, and error/message handling. The Release build is currently healthy, so cleanup should be incremental and form-by-form.

## Helper/Styling Duplication

- Grid styling is split across:
  - `SOURCE_CODE/UI/GridTheme.cs`
  - `DS.StyleGrid`
  - `SOURCE_CODE/UI/UIHelper.cs`
  - form-local methods such as `StyleAttendanceGrid` and `StyleQuotationGrid`
- Card styling is split across:
  - `SharedUiPrimitives`
  - `ResizableCard`
  - `WorkforceModuleVisuals`
  - form-local `CreateCard` methods
- Button styling is split across:
  - `UIHelper.ApplyActionButton`
  - `MakeButton`
  - `MakeActionButton`
  - `NewPrimaryButton`
  - `NewOutlineButton`
  - many inline button initializers

## Save Logic Duplication

Common duplicated flow:

1. Disable save/create/update button.
2. Change button text to `Saving...`.
3. Validate form fields.
4. Save through service/repository.
5. Refresh dashboard/grid.
6. Show success/failure `MessageBox`.
7. Restore button enabled/text in `finally`.

Best target for standardization:

- Add a shared helper for `RunSaveAsync(Button button, string savingText, Func<Task> saveAction, ...)`.
- Centralize validation display through `FluentValidationGuard`/`ValidationMessageFormatter`.
- Centralize user-facing error logging through Serilog/AppRuntime and ServoERP wording.

## Error Handling Duplication

Current patterns include:

- Direct `MessageBox.Show(ex.Message, ...)`
- Direct `MessageBox.Show("... failed: " + ex.Message, ...)`
- `BrandingService.WindowTitle(...)`
- `AppRuntime.LogException(...)`
- `Serilog`/`AppLogger` wrappers

Recommended standard:

- Log full exception once with module/action context.
- Show concise professional ServoERP message to user.
- Avoid exposing raw exception text unless it is already a validation/business message.

## Form Size Duplication Risk

Large files combine data loading, validation, UI construction, formatting, save logic, and dashboard rendering. High-return extraction targets:

- `PurchaseForm.cs`
- `InvoiceForm.cs`
- `JobManagementForm.cs`
- `TenderBidForm.cs`
- `PayrollForm.cs`
- `VendorForm.cs`
- `EmployeeForm.cs`
- `SettingsForm.cs`
- `InventoryForm.cs`

## Safe Next Refactor Order

1. Extract a save-flow helper and apply to one low-risk form.
2. Consolidate grid styling by making `GridTheme.Apply` the default for new/touched grids.
3. Consolidate button creation through `UIHelper.ApplyActionButton`.
4. Consolidate card creation only after confirming visual parity form by form.
5. Split large forms by workflow, not by arbitrary regions.
