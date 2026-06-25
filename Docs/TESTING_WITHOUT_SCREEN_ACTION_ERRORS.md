# ServoERP testing without screen-action errors

ServoERP confidence should come from command-line smoke checks first and visual proof second.

## Logic and persistence checks

Use command-line smoke switches instead of clicking through the app:

```powershell
powershell -ExecutionPolicy Bypass -File scripts\Invoke-ServoSmokeTests.ps1 -Suite CiSafe
powershell -ExecutionPolicy Bypass -File scripts\Invoke-ServoSmokeTests.ps1 -Suite BusinessSave,Amc
```

`CiSafe` runs before SQL startup through `/cismoketest`, so it is suitable for GitHub Actions and clean local checks.
`BusinessSave`, `Amc`, `Contracts`, `DashboardRecents`, `PurchaseViewButtons`, and `FullUi` exercise heavier app paths and should run only where the ServoERP SQL environment is available.

## Visual proof

Use one-page visual smoke instead of full-app traversal:

```powershell
powershell -STA -ExecutionPolicy Bypass -File scripts\Invoke-ServoPageVisualSmoke.ps1 `
  -ControlType HVAC_Pro_Desktop.UI.DashboardForm
```

The visual smoke script:

- loads the built Release assembly directly;
- hosts one WinForms page at a time;
- waits for `Shown`, UI idle, and an optional named ready control;
- captures a screenshot into `TEST_RESULTS\visual-smoke`;
- checks for blank captures;
- can run an opt-in parent-bounds audit with `-BoundsAudit` when the task is specifically layout/clipping;
- avoids coordinate clicks, cursor movement, and `SendKeys`.

## Rules

- Test business logic through services or command switches.
- Use visual automation only to prove rendered layout, clipping, button visibility, and dialog presence.
- Prefer one page per visual test.
- Wait for readiness before inspecting or capturing.
- Prefer stable control names, public form methods, or command-line switches over screen coordinates.
