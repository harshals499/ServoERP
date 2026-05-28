# ServoERP UI QA Baselines

This folder stores deterministic visual QA artifacts for ServoERP modules and states.

## Folders

- `baseline/`: approved reference screenshots used for threshold comparisons.
- `current/`: latest generated screenshots from `TOOLS/CaptureUiQaStates.ps1`.
- `current/manifest.csv`: capture and comparison output for the latest run.
- `debug/`: temporary investigation output; do not treat this as an approved baseline.

The capture script writes screenshots by module and state, for example:

```text
Docs/UI_QA_Baselines/current/Quotations/populated.png
Docs/UI_QA_Baselines/baseline/Quotations/populated.png
```

## Generate Current Screenshots

Build the WinForms app first, then run the capture script in STA mode:

```powershell
dotnet build C:\HVAC_PRO_MSE\SOURCE_CODE\HVAC_Pro_Desktop.sln
powershell.exe -NoProfile -ExecutionPolicy Bypass -STA -File C:\HVAC_PRO_MSE\TOOLS\CaptureUiQaStates.ps1
```

Useful optional parameters:

```powershell
powershell.exe -NoProfile -ExecutionPolicy Bypass -STA -File C:\HVAC_PRO_MSE\TOOLS\CaptureUiQaStates.ps1 `
  -OutputDir C:\HVAC_PRO_MSE\Docs\UI_QA_Baselines\current `
  -BaselineDir C:\HVAC_PRO_MSE\Docs\UI_QA_Baselines\baseline `
  -Width 1600 `
  -Height 1000 `
  -MaxDiffRatio 0.015
```

## Manifest Statuses

`manifest.csv` records one row per module/state capture.

- `CaptureResult = PASS`: screenshot capture completed.
- `CaptureResult = FAIL`: screenshot capture failed and the run should be investigated.
- `CompareResult = PASS`: a matching baseline existed and the pixel diff stayed within threshold.
- `CompareResult = FAIL`: a matching baseline existed and the pixel diff exceeded `MaxDiffRatio`.
- `CompareResult = NO_BASELINE`: no approved baseline exists yet for that module/state.
- `CompareResult = NO_COMPARE_SCRIPT`: the baseline image exists, but `TOOLS/CompareUiBaseline.ps1` was not found, so comparison could not run.
- `CompareResult = UNSUPPORTED`: the state is intentionally not comparable yet.
- `StateResult = UNSUPPORTED`: state setup is explicit but not automated yet; modal/dialog state currently uses this until native module dialogs are standardized.

`NO_BASELINE` is expected before the first approved baseline promotion. `NO_COMPARE_SCRIPT` is an infrastructure failure to fix before relying on threshold results. `UNSUPPORTED` rows must remain visible in the manifest until the state is implemented honestly.

## Compare One Screenshot

Use `TOOLS/CompareUiBaseline.ps1` for targeted checks:

```powershell
powershell.exe -NoProfile -ExecutionPolicy Bypass -File C:\HVAC_PRO_MSE\TOOLS\CompareUiBaseline.ps1 `
  -BaselinePath C:\HVAC_PRO_MSE\Docs\UI_QA_Baselines\baseline\Quotations\populated.png `
  -CurrentPath C:\HVAC_PRO_MSE\Docs\UI_QA_Baselines\current\Quotations\populated.png `
  -DiffPath C:\HVAC_PRO_MSE\Docs\UI_QA_Baselines\current\Quotations\populated.diff.png `
  -MaxDiffRatio 0.015
```

The compare script returns compact JSON. `Result = FAIL` exits non-zero. Review generated diff images before accepting or rejecting UI changes.

## Promote a Baseline

Only promote screenshots after visual review against the ServoERP UI ruleset and the approved reference screenshots in:

```text
C:\Users\harsh\Downloads\ServoERP_UI_Redesigns
```

Promotion rules:

- Promote only deterministic screenshots from `current/`.
- Promote reviewed `.png` screenshots only.
- Do not copy `manifest.csv` into `baseline/`.
- Do not copy `*.diff.png` into `baseline/`; diff images are review artifacts, not references.
- Do not baseline random production data states.
- Do not promote screenshots with visible sidebar/app shell regressions.
- Do not promote modal or `UNSUPPORTED` screenshots unless the manifest shows the native flow is implemented and the screenshot has been explicitly reviewed.
- Review `NO_BASELINE` rows module by module before first promotion.
- Review every threshold failure with the generated diff image.
- Keep `baseline/` as the approved source for future threshold-based visual regression.

After review, promote only approved screenshots. Prefer manual copy for the specific module/state files you reviewed:

```powershell
New-Item -ItemType Directory -Path C:\HVAC_PRO_MSE\Docs\UI_QA_Baselines\baseline -Force | Out-Null
New-Item -ItemType Directory -Path C:\HVAC_PRO_MSE\Docs\UI_QA_Baselines\baseline\Quotations -Force | Out-Null
Copy-Item C:\HVAC_PRO_MSE\Docs\UI_QA_Baselines\current\Quotations\populated.png `
  C:\HVAC_PRO_MSE\Docs\UI_QA_Baselines\baseline\Quotations\populated.png `
  -Force
```

For a reviewed batch, use a filtered loop and keep modal screenshots out unless they are supported and approved:

```powershell
$current = "C:\HVAC_PRO_MSE\Docs\UI_QA_Baselines\current"
$baseline = "C:\HVAC_PRO_MSE\Docs\UI_QA_Baselines\baseline"
$manifest = Import-Csv (Join-Path $current "manifest.csv")

$approved = $manifest | Where-Object {
    $_.CaptureResult -eq "PASS" -and
    $_.State -ne "modal" -and
    $_.CompareResult -ne "UNSUPPORTED" -and
    $_.Screenshot -like "*.png" -and
    $_.Screenshot -notlike "*.diff.png"
}

foreach ($row in $approved) {
    $relative = $row.Screenshot.Substring($current.Length).TrimStart("\")
    $target = Join-Path $baseline $relative
    New-Item -ItemType Directory -Path (Split-Path $target -Parent) -Force | Out-Null
    Copy-Item $row.Screenshot $target -Force
}
```

Then rerun capture to confirm comparisons are threshold-gated:

```powershell
powershell.exe -NoProfile -ExecutionPolicy Bypass -STA -File C:\HVAC_PRO_MSE\TOOLS\CaptureUiQaStates.ps1
```

Expected result after baseline promotion: comparable states should report `CompareResult = PASS` or `FAIL`; only genuinely unsupported states should remain `UNSUPPORTED`.
