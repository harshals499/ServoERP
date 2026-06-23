# Build Report

Date: 2026-06-23

## Baseline Build

Command:

```powershell
msbuild SOURCE_CODE\HVAC_Pro_Desktop.sln /p:Configuration=Release /m /v:minimal
```

Result:

- Passed.
- Output produced: `SOURCE_CODE/bin/Release/HVAC_Pro_Desktop.exe`

## Cleanup Build

Command:

```powershell
msbuild SOURCE_CODE\HVAC_Pro_Desktop.sln /p:Configuration=Release /m /v:minimal
```

Result:

- Passed.
- Output produced: `SOURCE_CODE/bin/Release/HVAC_Pro_Desktop.exe`
- Output exists and was updated after cleanup.

## Tests

Command:

```powershell
SOURCE_CODE\bin\Release\HVAC_Pro_Desktop.exe /savebuttontest
```

Result:

- Passed.
- Report: `TEST_RESULTS/save-button-smoke-20260623-152155.txt`

Passed save flows:

- Attendance save persists monthly record.
- Client save persists create and update.
- Employee save persists create, update, and salary profile.
- Vendor save persists create and update.
- Inventory save persists create and update.
- Contract save persists create and update.
- Job save persists create and update.
- Purchase save persists create and update.
- Invoice save persists create and update.
- Payment save persists record and invoice status refresh.

## Sidebar Verification

- No sidebar/navigation files were intentionally edited by this cleanup pass.
- The tree already had pre-existing modifications in `SOURCE_CODE/UI/MainForm.cs`, `SOURCE_CODE/UI/NavigationHelper.cs`, and `SOURCE_CODE/UI/ModernIconSystem.cs` before this task. Those files were not edited for cleanup.
- The cleanup removed only generated/temp/backup/harness artifacts and added audit reports.
