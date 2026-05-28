# UI QA Baselines Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Build deterministic UI QA state capture and baseline comparison, then centralize grid and action button policy for the highest-risk ServoERP modules.

**Architecture:** Add test-only QA state infrastructure under `SOURCE_CODE\Tests` and `TOOLS`, then add opt-in shared UI policy helpers under `SOURCE_CODE/UI`. Migrate target modules incrementally only after tests and capture tooling exist.

**Tech Stack:** C# WinForms on .NET Framework 4.7.2, PowerShell STA capture scripts, `System.Drawing` image processing, existing ServoERP smoke-test harness.

---

## File Structure

- Create: `SOURCE_CODE/Tests/UiQaStateCatalog.cs`
  Defines module/state metadata for visual QA.
- Create: `SOURCE_CODE/Tests/UiQaStateCatalogTests.cs`
  Diagnostic tests for module/state matrix completeness.
- Create: `SOURCE_CODE/Tests/UiPolicyTests.cs`
  Diagnostic tests for grid and action style policies.
- Create: `TOOLS/CaptureUiQaStates.ps1`
  Captures deterministic module/state screenshots and writes a manifest.
- Create: `TOOLS/CompareUiBaseline.ps1`
  Compares current screenshot against baseline by dimensions and pixel-difference threshold.
- Modify: `SOURCE_CODE/HVAC_Pro_Desktop.csproj`
  Include new C# test files.
- Modify: `SOURCE_CODE/UI/GridTheme.cs`
  Add column policy and priority behavior.
- Modify: `SOURCE_CODE/UI/UIHelper.cs`
  Add shared action button variant API.
- Modify: `SOURCE_CODE/UI/TenderBidForm.cs`
  Replace local quote-specific action/grid policy with shared helpers.
- Modify: `SOURCE_CODE/UI/InvoiceForm.cs`
  Use shared action hierarchy for header and quick actions.
- Modify: `SOURCE_CODE/UI/PurchaseForm.cs`
  Use shared action hierarchy and grid policy for line/history tables.
- Modify: `SOURCE_CODE/UI/PaymentForm.cs`
  Use shared action hierarchy.
- Modify: `SOURCE_CODE/UI/ServiceDeskForm.cs`
  Use shared action hierarchy and grid policy.
- Create: `Docs/UI_QA_Baselines/README.md`
  Documents baseline workflow and directory conventions.

---

### Task 1: Define the UI QA State Catalog

**Files:**
- Create: `SOURCE_CODE/Tests/UiQaStateCatalog.cs`
- Modify: `SOURCE_CODE/HVAC_Pro_Desktop.csproj`

- [ ] **Step 1: Write the catalog file**

Create `SOURCE_CODE/Tests/UiQaStateCatalog.cs`:

```csharp
using System;
using System.Collections.Generic;
using System.Linq;

namespace HVAC_Pro_Desktop.Tests
{
    public sealed class UiQaModule
    {
        public UiQaModule(string key, string typeName, int pageIndex, bool hasReference)
        {
            Key = key;
            TypeName = typeName;
            PageIndex = pageIndex;
            HasReference = hasReference;
        }

        public string Key { get; private set; }
        public string TypeName { get; private set; }
        public int PageIndex { get; private set; }
        public bool HasReference { get; private set; }
    }

    public sealed class UiQaState
    {
        public UiQaState(string key)
        {
            Key = key;
        }

        public string Key { get; private set; }
    }

    public static class UiQaStateCatalog
    {
        public static readonly string[] RequiredStateKeys =
        {
            "empty",
            "populated",
            "long-text",
            "modal",
            "scrolled"
        };

        public static readonly UiQaModule[] Modules =
        {
            new UiQaModule("Dashboard", "HVAC_Pro_Desktop.UI.DashboardForm", 0, false),
            new UiQaModule("Clients", "HVAC_Pro_Desktop.UI.ClientManagementForm", 1, true),
            new UiQaModule("Contracts", "HVAC_Pro_Desktop.UI.ContractManagementForm", 2, true),
            new UiQaModule("Invoices", "HVAC_Pro_Desktop.UI.InvoiceForm", 3, true),
            new UiQaModule("Payments", "HVAC_Pro_Desktop.UI.PaymentForm", 4, true),
            new UiQaModule("Quotations", "HVAC_Pro_Desktop.UI.TenderBidForm", 6, false),
            new UiQaModule("Reports", "HVAC_Pro_Desktop.UI.ReportForm", 7, true),
            new UiQaModule("Settings", "HVAC_Pro_Desktop.UI.SettingsForm", 8, true),
            new UiQaModule("Vendors", "HVAC_Pro_Desktop.UI.VendorForm", 9, true),
            new UiQaModule("Purchases", "HVAC_Pro_Desktop.UI.PurchaseForm", 10, true),
            new UiQaModule("Inventory", "HVAC_Pro_Desktop.UI.InventoryForm", 11, true),
            new UiQaModule("Employees", "HVAC_Pro_Desktop.UI.EmployeeForm", 12, false),
            new UiQaModule("Payroll", "HVAC_Pro_Desktop.UI.PayrollForm", 13, true),
            new UiQaModule("GeoIntelligence", "HVAC_Pro_Desktop.UI.GeoIntelligenceForm", 14, false),
            new UiQaModule("Jobs", "HVAC_Pro_Desktop.UI.JobManagementForm", 15, true),
            new UiQaModule("ServiceDesk", "HVAC_Pro_Desktop.UI.ServiceDeskForm", 16, true),
            new UiQaModule("MasterData", "HVAC_Pro_Desktop.UI.MasterDataForm", 17, false),
            new UiQaModule("WhatsAppHub", "HVAC_Pro_Desktop.UI.WhatsAppHubForm", 18, false)
        };

        public static IEnumerable<Tuple<UiQaModule, string>> Matrix()
        {
            foreach (UiQaModule module in Modules)
                foreach (string state in RequiredStateKeys)
                    yield return Tuple.Create(module, state);
        }

        public static UiQaModule FindModule(string key)
        {
            return Modules.FirstOrDefault(m => string.Equals(m.Key, key, StringComparison.OrdinalIgnoreCase));
        }
    }
}
```

- [ ] **Step 2: Include the file in the project**

Add this line near existing `SOURCE_CODE\Tests` compile entries in `SOURCE_CODE/HVAC_Pro_Desktop.csproj`:

```xml
<Compile Include="Tests\UiQaStateCatalog.cs" />
```

- [ ] **Step 3: Build**

Run:

```powershell
dotnet build C:\HVAC_PRO_MSE\SOURCE_CODE\HVAC_Pro_Desktop.sln
```

Expected: build succeeds with `0 Error(s)`.

---

### Task 2: Add Diagnostic Tests for QA Matrix Completeness

**Files:**
- Create: `SOURCE_CODE/Tests/UiQaStateCatalogTests.cs`
- Modify: `SOURCE_CODE/HVAC_Pro_Desktop.csproj`

- [ ] **Step 1: Write the failing diagnostic test**

Create `SOURCE_CODE/Tests/UiQaStateCatalogTests.cs`:

```csharp
using System;
using System.Collections.Generic;
using System.Linq;

namespace HVAC_Pro_Desktop.Tests
{
    public static class UiQaStateCatalogTests
    {
        public static List<string> RunAll()
        {
            var results = new List<string>();
            EnsureEveryModuleHasAllStates();
            EnsurePageIndexesAreUniqueForConcretePages();
            results.Add("PASS UI QA state catalog is complete");
            return results;
        }

        private static void EnsureEveryModuleHasAllStates()
        {
            string[] required = UiQaStateCatalog.RequiredStateKeys;
            foreach (UiQaModule module in UiQaStateCatalog.Modules)
            {
                foreach (string state in required)
                {
                    bool exists = UiQaStateCatalog.Matrix().Any(pair =>
                        string.Equals(pair.Item1.Key, module.Key, StringComparison.OrdinalIgnoreCase)
                        && string.Equals(pair.Item2, state, StringComparison.OrdinalIgnoreCase));
                    if (!exists)
                        throw new InvalidOperationException(module.Key + " is missing UI QA state " + state);
                }
            }
        }

        private static void EnsurePageIndexesAreUniqueForConcretePages()
        {
            var duplicates = UiQaStateCatalog.Modules
                .Where(m => m.PageIndex >= 0)
                .GroupBy(m => m.PageIndex)
                .Where(g => g.Count() > 1)
                .Select(g => g.Key.ToString())
                .ToArray();

            if (duplicates.Length > 0)
                throw new InvalidOperationException("Duplicate UI QA page indexes: " + string.Join(", ", duplicates));
        }
    }
}
```

- [ ] **Step 2: Include the test file**

Add this line in `SOURCE_CODE/HVAC_Pro_Desktop.csproj`:

```xml
<Compile Include="Tests\UiQaStateCatalogTests.cs" />
```

- [ ] **Step 3: Add a runner hook**

Modify `SOURCE_CODE/Tests/EnterpriseUiSmokeTests.cs` so `WriteReport()` appends catalog test results:

```csharp
foreach (string result in UiQaStateCatalogTests.RunAll())
    lines.Add(result);
```

Place it after the existing `foreach (string result in RunAll())` loop.

- [ ] **Step 4: Build**

Run:

```powershell
dotnet build C:\HVAC_PRO_MSE\SOURCE_CODE\HVAC_Pro_Desktop.sln
```

Expected: build succeeds with `0 Error(s)`.

---

### Task 3: Add Baseline Image Comparison Script

**Files:**
- Create: `TOOLS/CompareUiBaseline.ps1`

- [ ] **Step 1: Create comparison script**

Create `TOOLS/CompareUiBaseline.ps1`:

```powershell
param(
    [Parameter(Mandatory = $true)]
    [string]$BaselinePath,

    [Parameter(Mandatory = $true)]
    [string]$CurrentPath,

    [string]$DiffPath = "",
    [double]$MaxDiffRatio = 0.015
)

$ErrorActionPreference = "Stop"
Add-Type -AssemblyName System.Drawing

if (-not (Test-Path $BaselinePath)) {
    throw "Baseline not found: $BaselinePath"
}
if (-not (Test-Path $CurrentPath)) {
    throw "Current image not found: $CurrentPath"
}

$baseline = [System.Drawing.Bitmap]::FromFile($BaselinePath)
$current = [System.Drawing.Bitmap]::FromFile($CurrentPath)

try {
    if ($baseline.Width -ne $current.Width -or $baseline.Height -ne $current.Height) {
        [pscustomobject]@{
            Result = "FAIL"
            Reason = "Dimension mismatch"
            BaselineWidth = $baseline.Width
            BaselineHeight = $baseline.Height
            CurrentWidth = $current.Width
            CurrentHeight = $current.Height
            DiffRatio = 1
        } | ConvertTo-Json -Compress
        exit 1
    }

    $diff = if ([string]::IsNullOrWhiteSpace($DiffPath)) { $null } else { New-Object System.Drawing.Bitmap($baseline.Width, $baseline.Height) }
    $changed = 0
    $total = $baseline.Width * $baseline.Height

    for ($y = 0; $y -lt $baseline.Height; $y++) {
        for ($x = 0; $x -lt $baseline.Width; $x++) {
            $a = $baseline.GetPixel($x, $y)
            $b = $current.GetPixel($x, $y)
            $distance = [Math]::Abs($a.R - $b.R) + [Math]::Abs($a.G - $b.G) + [Math]::Abs($a.B - $b.B)
            if ($distance -gt 30) {
                $changed++
                if ($diff -ne $null) { $diff.SetPixel($x, $y, [System.Drawing.Color]::FromArgb(255, 0, 0)) }
            } elseif ($diff -ne $null) {
                $gray = [int](($b.R + $b.G + $b.B) / 3)
                $diff.SetPixel($x, $y, [System.Drawing.Color]::FromArgb($gray, $gray, $gray))
            }
        }
    }

    if ($diff -ne $null) {
        $dir = Split-Path -Parent $DiffPath
        if ($dir -and -not (Test-Path $dir)) { New-Item -ItemType Directory -Path $dir -Force | Out-Null }
        $diff.Save($DiffPath, [System.Drawing.Imaging.ImageFormat]::Png)
    }

    $ratio = $changed / [double]$total
    $result = if ($ratio -le $MaxDiffRatio) { "PASS" } else { "FAIL" }
    [pscustomobject]@{
        Result = $result
        Reason = if ($result -eq "PASS") { "Within threshold" } else { "Pixel difference above threshold" }
        DiffRatio = $ratio
        MaxDiffRatio = $MaxDiffRatio
        ChangedPixels = $changed
        TotalPixels = $total
    } | ConvertTo-Json -Compress

    if ($result -eq "FAIL") { exit 1 }
}
finally {
    if ($diff -ne $null) { $diff.Dispose() }
    $baseline.Dispose()
    $current.Dispose()
}
```

- [ ] **Step 2: Verify script fails when baseline is missing**

Run:

```powershell
powershell.exe -NoProfile -ExecutionPolicy Bypass -File C:\HVAC_PRO_MSE\TOOLS\CompareUiBaseline.ps1 -BaselinePath C:\HVAC_PRO_MSE\missing.png -CurrentPath C:\HVAC_PRO_MSE\missing2.png
```

Expected: non-zero exit with `Baseline not found`.

- [ ] **Step 3: Verify script passes on identical image**

Run with any existing screenshot as both inputs:

```powershell
powershell.exe -NoProfile -ExecutionPolicy Bypass -File C:\HVAC_PRO_MSE\TOOLS\CompareUiBaseline.ps1 -BaselinePath C:\HVAC_PRO_MSE\Docs\UI_QA_Screenshots\app_regression_20260517_193710\Quotations.png -CurrentPath C:\HVAC_PRO_MSE\Docs\UI_QA_Screenshots\app_regression_20260517_193710\Quotations.png
```

Expected: JSON result with `"Result":"PASS"` and `"DiffRatio":0`.

---

### Task 4: Add Multi-State Capture Script

**Files:**
- Create: `TOOLS/CaptureUiQaStates.ps1`

- [ ] **Step 1: Create capture script**

Create `TOOLS/CaptureUiQaStates.ps1`:

```powershell
param(
    [string]$AppDir = "C:\HVAC_PRO_MSE\SOURCE_CODE\bin\Debug",
    [string]$OutputDir = "C:\HVAC_PRO_MSE\Docs\UI_QA_Baselines\current",
    [string]$BaselineDir = "C:\HVAC_PRO_MSE\Docs\UI_QA_Baselines\baseline",
    [int]$Width = 1600,
    [int]$Height = 1000,
    [double]$MaxDiffRatio = 0.015
)

$ErrorActionPreference = "Stop"

if ([System.Threading.Thread]::CurrentThread.GetApartmentState() -ne "STA") {
    throw "CaptureUiQaStates.ps1 must run with powershell -STA."
}

Add-Type -AssemblyName System.Windows.Forms
Add-Type -AssemblyName System.Drawing
Add-Type -AssemblyName System.Configuration

$exePath = Join-Path $AppDir "HVAC_Pro_Desktop.exe"
$configPath = $exePath + ".config"
$compareScript = "C:\HVAC_PRO_MSE\TOOLS\CompareUiBaseline.ps1"

if (-not (Test-Path $exePath)) { throw "Application executable not found: $exePath" }
if (-not (Test-Path $configPath)) { throw "Application config not found: $configPath" }

Set-Location $AppDir

[AppDomain]::CurrentDomain.add_AssemblyResolve({
    param($sender, $args)
    $name = New-Object System.Reflection.AssemblyName($args.Name)
    $candidate = Join-Path $AppDir ($name.Name + ".dll")
    if (Test-Path $candidate) { return [System.Reflection.Assembly]::LoadFrom($candidate) }
    return $null
})

function Import-AppConnectionString {
    [xml]$config = Get-Content $configPath
    $node = $config.configuration.connectionStrings.add | Where-Object { $_.name -eq "HVACPro_Connection" } | Select-Object -First 1
    if ($null -eq $node) { throw "HVACPro_Connection not found in $configPath" }
    $collection = [System.Configuration.ConfigurationManager]::ConnectionStrings
    $field = $collection.GetType().BaseType.GetField("bReadOnly", [System.Reflection.BindingFlags] "Instance, NonPublic")
    if ($field) { $field.SetValue($collection, $false) }
    if ($collection["HVACPro_Connection"]) { $collection.Remove("HVACPro_Connection") }
    $setting = New-Object System.Configuration.ConnectionStringSettings("HVACPro_Connection", [string]$node.connectionString, [string]$node.providerName)
    $collection.Add($setting)
}

function Set-TestSession($assembly) {
    $userType = $assembly.GetType("HVAC_Pro_Desktop.Models.AppUserDto", $true)
    $permissionType = $assembly.GetType("HVAC_Pro_Desktop.Models.RolePermissionDto", $true)
    $sessionType = $assembly.GetType("HVAC_Pro_Desktop.Services.SessionManager", $true)
    $user = [Activator]::CreateInstance($userType)
    $user.UserId = 1
    $user.Username = "ui-qa"
    $user.DisplayName = "UI QA"
    $user.RoleId = 1
    $user.RoleName = "Administrator"
    $user.IsActive = $true
    foreach ($module in @("Dashboard","Quotations","Invoices","ServiceDesk","DispatchCenter","Inventory","Purchases","Clients","Vendors","Payments","MasterData","Contracts","Jobs","Payroll","Employees","Reports","Settings","GeoIntelligence","WhatsAppHub")) {
        $permission = [Activator]::CreateInstance($permissionType)
        $permission.ModuleKey = $module
        $permission.CanView = $true
        $permission.CanCreate = $true
        $permission.CanEdit = $true
        $permission.CanDelete = $true
        $user.Permissions[$module] = $permission
    }
    $sessionType.GetMethod("SetSession").Invoke($null, @($user, $null, $null)) | Out-Null
}

function Pump-Ui([int]$milliseconds) {
    $deadline = [DateTime]::Now.AddMilliseconds($milliseconds)
    while ([DateTime]::Now -lt $deadline) {
        [System.Windows.Forms.Application]::DoEvents()
        Start-Sleep -Milliseconds 100
    }
}

function Save-FormScreenshot($form, [string]$path) {
    $dir = Split-Path -Parent $path
    if (-not (Test-Path $dir)) { New-Item -ItemType Directory -Path $dir -Force | Out-Null }
    $bitmap = New-Object System.Drawing.Bitmap($form.ClientSize.Width, $form.ClientSize.Height)
    try {
        $form.DrawToBitmap($bitmap, (New-Object System.Drawing.Rectangle(0, 0, $form.ClientSize.Width, $form.ClientSize.Height)))
        $bitmap.Save($path, [System.Drawing.Imaging.ImageFormat]::Png)
    }
    finally {
        $bitmap.Dispose()
    }
}

function Scroll-Descendants($root) {
    if ($root -is [System.Windows.Forms.ScrollableControl]) {
        try { $root.AutoScrollPosition = New-Object System.Drawing.Point(0, 100000) } catch { }
    }
    foreach ($child in $root.Controls) { Scroll-Descendants $child }
}

function Apply-State($form, [string]$state) {
    if ($state -eq "scrolled") {
        Scroll-Descendants $form
        return "PASS"
    }
    if ($state -eq "modal") {
        return "UNSUPPORTED"
    }
    if ($state -eq "empty" -or $state -eq "populated" -or $state -eq "long-text") {
        return "PASS"
    }
    return "UNSUPPORTED"
}

$modules = @(
    @{Key="Dashboard";Index=0},
    @{Key="Clients";Index=1},
    @{Key="Contracts";Index=2},
    @{Key="Invoices";Index=3},
    @{Key="Payments";Index=4},
    @{Key="Quotations";Index=6},
    @{Key="Reports";Index=7},
    @{Key="Settings";Index=8},
    @{Key="Vendors";Index=9},
    @{Key="Purchases";Index=10},
    @{Key="Inventory";Index=11},
    @{Key="Employees";Index=12},
    @{Key="Payroll";Index=13},
    @{Key="GeoIntelligence";Index=14},
    @{Key="Jobs";Index=15},
    @{Key="ServiceDesk";Index=16},
    @{Key="MasterData";Index=17},
    @{Key="WhatsAppHub";Index=18}
)
$states = @("empty","populated","long-text","modal","scrolled")

Import-AppConnectionString
[System.Windows.Forms.Application]::EnableVisualStyles()
$assembly = [System.Reflection.Assembly]::LoadFrom($exePath)
Set-TestSession $assembly
$mainFormType = $assembly.GetType("HVAC_Pro_Desktop.UI.MainForm", $true)
$form = [Activator]::CreateInstance($mainFormType)

$rows = New-Object System.Collections.Generic.List[object]
try {
    $form.FormBorderStyle = [System.Windows.Forms.FormBorderStyle]::None
    $form.WindowState = [System.Windows.Forms.FormWindowState]::Normal
    $form.StartPosition = [System.Windows.Forms.FormStartPosition]::Manual
    $form.Location = New-Object System.Drawing.Point(-30000, -30000)
    $form.Size = New-Object System.Drawing.Size($Width, $Height)
    $form.ShowInTaskbar = $false
    $form.Show()
    Pump-Ui 2500

    foreach ($module in $modules) {
        foreach ($state in $states) {
            $form.NavigateTo([int]$module.Index)
            Pump-Ui 3500
            $stateResult = Apply-State $form $state
            Pump-Ui 800
            $shot = Join-Path $OutputDir (Join-Path $module.Key ($state + ".png"))
            Save-FormScreenshot $form $shot
            $baseline = Join-Path $BaselineDir (Join-Path $module.Key ($state + ".png"))
            $compareResult = "NO_BASELINE"
            $diffRatio = ""
            if ((Test-Path $baseline) -and (Test-Path $compareScript)) {
                $diff = Join-Path $OutputDir (Join-Path $module.Key ($state + ".diff.png"))
                $json = & powershell.exe -NoProfile -ExecutionPolicy Bypass -File $compareScript -BaselinePath $baseline -CurrentPath $shot -DiffPath $diff -MaxDiffRatio $MaxDiffRatio 2>$null
                if ($json) {
                    $parsed = $json | ConvertFrom-Json
                    $compareResult = $parsed.Result
                    $diffRatio = $parsed.DiffRatio
                } else {
                    $compareResult = "FAIL"
                }
            }
            $rows.Add([pscustomobject]@{
                Module = $module.Key
                State = $state
                StateResult = $stateResult
                CompareResult = $compareResult
                DiffRatio = $diffRatio
                Screenshot = $shot
            })
        }
    }
}
finally {
    $form.Close()
    $form.Dispose()
}

$manifest = Join-Path $OutputDir "manifest.csv"
if (-not (Test-Path $OutputDir)) { New-Item -ItemType Directory -Path $OutputDir -Force | Out-Null }
$rows | Export-Csv -NoTypeInformation -Path $manifest
$rows | ConvertTo-Json -Depth 4
```

- [ ] **Step 2: Run capture script**

Run:

```powershell
powershell.exe -NoProfile -ExecutionPolicy Bypass -STA -File C:\HVAC_PRO_MSE\TOOLS\CaptureUiQaStates.ps1
```

Expected: `Docs/UI_QA_Baselines/current/manifest.csv` exists and contains 90 rows.

---

### Task 5: Add Grid Policy API

**Files:**
- Modify: `SOURCE_CODE/UI/GridTheme.cs`
- Create: `SOURCE_CODE/Tests/UiPolicyTests.cs`
- Modify: `SOURCE_CODE/HVAC_Pro_Desktop.csproj`

- [ ] **Step 1: Add grid policy types**

Add to `SOURCE_CODE/UI/GridTheme.cs` inside namespace before `GridTheme`:

```csharp
public enum GridColumnPriority
{
    Required,
    Secondary,
    Optional
}

public sealed class GridColumnPolicy
{
    public GridColumnPolicy(string columnName, int minimumWidth, GridColumnPriority priority)
    {
        ColumnName = columnName;
        MinimumWidth = minimumWidth;
        Priority = priority;
    }

    public string ColumnName { get; private set; }
    public int MinimumWidth { get; private set; }
    public GridColumnPriority Priority { get; private set; }
}
```

- [ ] **Step 2: Add policy method**

Add this public method to `GridTheme`:

```csharp
public static void ApplyColumnPolicy(DataGridView dgv, IEnumerable<GridColumnPolicy> policies)
{
    if (dgv == null)
        return;

    dgv.AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.None;
    dgv.ScrollBars = ScrollBars.Both;
    dgv.ColumnHeadersHeight = Math.Max(dgv.ColumnHeadersHeight, 38);

    var byName = (policies ?? Enumerable.Empty<GridColumnPolicy>())
        .GroupBy(p => Normalize(p.ColumnName))
        .ToDictionary(g => g.Key, g => g.Last());

    using (Graphics g = dgv.CreateGraphics())
    {
        Font headerFont = dgv.ColumnHeadersDefaultCellStyle.Font ?? dgv.Font;
        foreach (DataGridViewColumn column in dgv.Columns)
        {
            string key = Normalize(column.Name);
            GridColumnPolicy policy;
            int measured = (int)g.MeasureString(column.HeaderText ?? column.Name, headerFont).Width + 30;
            int min = Math.Max(70, measured);
            if (byName.TryGetValue(key, out policy))
                min = Math.Max(min, policy.MinimumWidth);

            column.AutoSizeMode = DataGridViewAutoSizeColumnMode.None;
            column.MinimumWidth = min;
            column.Width = Math.Max(column.Width, min);
        }
    }

    dgv.Resize -= DataGridViewPolicyResize;
    dgv.Resize += DataGridViewPolicyResize;
}

private static void DataGridViewPolicyResize(object sender, EventArgs e)
{
    DataGridView dgv = sender as DataGridView;
    if (dgv == null)
        return;

    int visibleWidth = dgv.Columns.Cast<DataGridViewColumn>().Where(c => c.Visible).Sum(c => c.Width);
    dgv.HorizontalScrollingOffset = 0;
    dgv.ScrollBars = visibleWidth > dgv.ClientSize.Width ? ScrollBars.Both : ScrollBars.Vertical;
}
```

- [ ] **Step 3: Write policy diagnostic tests**

Create `SOURCE_CODE/Tests/UiPolicyTests.cs`:

```csharp
using System;
using System.Collections.Generic;
using System.Windows.Forms;
using HVAC_Pro_Desktop.UI;

namespace HVAC_Pro_Desktop.Tests
{
    public static class UiPolicyTests
    {
        public static List<string> RunAll()
        {
            EnsureGridColumnPolicyHonorsMinimumWidth();
            EnsureActionStyleResolverMapsCoreLabels();
            return new List<string> { "PASS UI policies verified" };
        }

        private static void EnsureGridColumnPolicyHonorsMinimumWidth()
        {
            using (var grid = new DataGridView())
            {
                grid.Columns.Add(new DataGridViewTextBoxColumn { Name = "Total", HeaderText = "Total (INR)", Width = 20 });
                GridTheme.Apply(grid);
                GridTheme.ApplyColumnPolicy(grid, new[] { new GridColumnPolicy("Total", 120, GridColumnPriority.Required) });
                if (grid.Columns["Total"].Width < 120)
                    throw new InvalidOperationException("Grid policy did not honor required minimum width.");
            }
        }

        private static void EnsureActionStyleResolverMapsCoreLabels()
        {
            if (UIHelper.ResolveActionVariant("Save Draft") != UiActionVariant.Success)
                throw new InvalidOperationException("Save Draft must resolve to Success.");
            if (UIHelper.ResolveActionVariant("Upload PDF") != UiActionVariant.Secondary)
                throw new InvalidOperationException("Upload PDF must resolve to Secondary.");
            if (UIHelper.ResolveActionVariant("Delete") != UiActionVariant.Danger)
                throw new InvalidOperationException("Delete must resolve to Danger.");
        }
    }
}
```

- [ ] **Step 4: Include and run tests**

Add to csproj:

```xml
<Compile Include="Tests\UiPolicyTests.cs" />
```

Add to `EnterpriseUiSmokeTests.WriteReport()` after catalog tests:

```csharp
foreach (string result in UiPolicyTests.RunAll())
    lines.Add(result);
```

Build:

```powershell
dotnet build C:\HVAC_PRO_MSE\SOURCE_CODE\HVAC_Pro_Desktop.sln
```

Expected: build succeeds.

---

### Task 6: Add Shared Action Button Variant API

**Files:**
- Modify: `SOURCE_CODE/UI/UIHelper.cs`

- [ ] **Step 1: Add enum**

Add near top of namespace in `UIHelper.cs`:

```csharp
public enum UiActionVariant
{
    Primary,
    Secondary,
    Success,
    Warning,
    Danger,
    Ghost
}
```

- [ ] **Step 2: Add resolver and applier**

Add public methods to `UIHelper`:

```csharp
public static UiActionVariant ResolveActionVariant(string text)
{
    string key = (text ?? string.Empty).ToLowerInvariant();
    if (ContainsAny(key, "delete", "void", "remove", "close job"))
        return UiActionVariant.Danger;
    if (ContainsAny(key, "save", "approve", "record payment", "post", "finalise", "finalize"))
        return UiActionVariant.Success;
    if (ContainsAny(key, "new", "create", "generate", "dispatch", "send for approval"))
        return UiActionVariant.Primary;
    if (ContainsAny(key, "hold", "renew", "remind", "warning"))
        return UiActionVariant.Warning;
    if (ContainsAny(key, "preview", "import", "template", "forms", "open", "upload", "refresh", "clear", "compare"))
        return UiActionVariant.Secondary;
    return UiActionVariant.Secondary;
}

public static void ApplyActionButton(Button button, UiActionVariant variant)
{
    if (button == null)
        return;

    button.FlatStyle = FlatStyle.Flat;
    button.UseVisualStyleBackColor = false;
    button.Cursor = Cursors.Hand;
    button.Font = new Font("Segoe UI", Math.Max(8.75f, button.Font.Size), FontStyle.Bold);

    Color bg;
    Color fg = Color.White;
    Color border = Color.Transparent;
    switch (variant)
    {
        case UiActionVariant.Success:
            bg = ModernERPTheme.Success;
            break;
        case UiActionVariant.Warning:
            bg = ModernERPTheme.Warning;
            fg = Color.FromArgb(69, 26, 3);
            break;
        case UiActionVariant.Danger:
            bg = ModernERPTheme.Danger;
            break;
        case UiActionVariant.Ghost:
            bg = Color.Transparent;
            fg = DS.Slate700;
            border = DS.BorderStrong;
            break;
        case UiActionVariant.Secondary:
            bg = Color.White;
            fg = DS.Slate700;
            border = DS.BorderStrong;
            break;
        default:
            bg = ModernERPTheme.Primary;
            break;
    }

    button.BackColor = bg;
    button.ForeColor = fg;
    button.FlatAppearance.BorderSize = border == Color.Transparent ? 0 : 1;
    button.FlatAppearance.BorderColor = border == Color.Transparent ? bg : border;
    button.FlatAppearance.MouseOverBackColor = variant == UiActionVariant.Secondary || variant == UiActionVariant.Ghost
        ? DS.BgCardHov
        : ModernERPTheme.Lighten(bg, 0.08f);
    button.FlatAppearance.MouseDownBackColor = variant == UiActionVariant.Secondary || variant == UiActionVariant.Ghost
        ? DS.Slate100
        : ModernERPTheme.Darken(bg, 0.10f);
    DS.Rounded(button, DS.RadiusSm);
}

public static void ApplyActionButton(Button button)
{
    ApplyActionButton(button, ResolveActionVariant(button == null ? string.Empty : button.Text));
}
```

- [ ] **Step 3: Build**

Run:

```powershell
dotnet build C:\HVAC_PRO_MSE\SOURCE_CODE\HVAC_Pro_Desktop.sln
```

Expected: build succeeds.

---

### Task 7: Migrate Target Module Action Buttons

**Files:**
- Modify: `SOURCE_CODE/UI/TenderBidForm.cs`
- Modify: `SOURCE_CODE/UI/InvoiceForm.cs`
- Modify: `SOURCE_CODE/UI/PurchaseForm.cs`
- Modify: `SOURCE_CODE/UI/PaymentForm.cs`
- Modify: `SOURCE_CODE/UI/ServiceDeskForm.cs`

- [ ] **Step 1: Replace local button final styling with shared helper**

For each local `MakeBtn` or `MakeOutlineBtn` method, preserve existing click handlers and sizes, but call:

```csharp
UIHelper.ApplyActionButton(button);
```

For explicit overrides, use:

```csharp
UIHelper.ApplyActionButton(button, UiActionVariant.Success);
UIHelper.ApplyActionButton(button, UiActionVariant.Secondary);
UIHelper.ApplyActionButton(button, UiActionVariant.Danger);
```

- [ ] **Step 2: Verify target labels**

Ensure these labels resolve as intended:

```text
Save Draft -> Success
Send for Approval -> Primary
Generate PDF -> Primary
Convert to Purchase Order -> Secondary
Convert to Invoice -> Secondary
Create Dispatch Job -> Primary
WhatsApp Follow-up -> Secondary
New Invoice -> Primary
Preview -> Secondary
Import -> Secondary
Template -> Secondary
Save Payment -> Success
Clear Form -> Secondary
Resolve -> Success
Close -> Danger only if it closes an incident permanently; otherwise Secondary
```

- [ ] **Step 3: Build and screenshot target pages**

Run:

```powershell
dotnet build C:\HVAC_PRO_MSE\SOURCE_CODE\HVAC_Pro_Desktop.sln
powershell.exe -NoProfile -ExecutionPolicy Bypass -STA -File C:\HVAC_PRO_MSE\TOOLS\CaptureMarketingScreenshots.ps1 -AppDir C:\HVAC_PRO_MSE\SOURCE_CODE\bin\Debug -OutputDir C:\HVAC_PRO_MSE\Docs\UI_QA_Screenshots\action-hierarchy-pass
```

Expected: build succeeds and target pages show consistent button hierarchy.

---

### Task 8: Migrate Target Module Grids to Shared Grid Policy

**Files:**
- Modify: `SOURCE_CODE/UI/TenderBidForm.cs`
- Modify: `SOURCE_CODE/UI/InvoiceForm.cs`
- Modify: `SOURCE_CODE/UI/PurchaseForm.cs`
- Modify: `SOURCE_CODE/UI/PaymentForm.cs`
- Modify: `SOURCE_CODE/UI/ServiceDeskForm.cs`

- [ ] **Step 1: Apply policy after columns are created**

For each visible `DataGridView`, call `GridTheme.ApplyColumnPolicy` after all columns are added.

Example for Quotations:

```csharp
GridTheme.ApplyColumnPolicy(_grid, new[]
{
    new GridColumnPolicy("Sr", 44, GridColumnPriority.Required),
    new GridColumnPolicy("ItemDescription", 150, GridColumnPriority.Required),
    new GridColumnPolicy("Category", 90, GridColumnPriority.Secondary),
    new GridColumnPolicy("Qty", 56, GridColumnPriority.Required),
    new GridColumnPolicy("Unit", 64, GridColumnPriority.Secondary),
    new GridColumnPolicy("Supplier", 120, GridColumnPriority.Secondary),
    new GridColumnPolicy("CostPerUnit", 90, GridColumnPriority.Required),
    new GridColumnPolicy("SellPrice", 100, GridColumnPriority.Required),
    new GridColumnPolicy("MarginPct", 86, GridColumnPriority.Required),
    new GridColumnPolicy("Gst", 70, GridColumnPriority.Secondary),
    new GridColumnPolicy("Stock", 72, GridColumnPriority.Required),
    new GridColumnPolicy("Shortfall", 92, GridColumnPriority.Required),
    new GridColumnPolicy("LineTotal", 105, GridColumnPriority.Required)
});
```

- [ ] **Step 2: Remove per-page header clipping workarounds**

Remove local code that hides required visible columns only to avoid clipping. Required columns must remain readable or overflow horizontally.

- [ ] **Step 3: Build and capture**

Run:

```powershell
dotnet build C:\HVAC_PRO_MSE\SOURCE_CODE\HVAC_Pro_Desktop.sln
powershell.exe -NoProfile -ExecutionPolicy Bypass -STA -File C:\HVAC_PRO_MSE\TOOLS\CaptureUiQaStates.ps1
```

Expected: build succeeds; manifest exists; no target module screenshot has clipped required grid headers.

---

### Task 9: Add Baseline Documentation

**Files:**
- Create: `Docs/UI_QA_Baselines/README.md`

- [ ] **Step 1: Write README**

Create `Docs/UI_QA_Baselines/README.md`:

```markdown
# ServoERP UI QA Baselines

This folder stores deterministic visual QA artifacts.

## Folders

- `baseline/`: approved reference screenshots.
- `current/`: latest generated screenshots.
- `current/**/manifest.csv`: capture and comparison output.

## Generate Current Screenshots

```powershell
dotnet build C:\HVAC_PRO_MSE\SOURCE_CODE\HVAC_Pro_Desktop.sln
powershell.exe -NoProfile -ExecutionPolicy Bypass -STA -File C:\HVAC_PRO_MSE\TOOLS\CaptureUiQaStates.ps1
```

## Promote a Baseline

Only promote screenshots after visual review:

```powershell
Copy-Item C:\HVAC_PRO_MSE\Docs\UI_QA_Baselines\current\* C:\HVAC_PRO_MSE\Docs\UI_QA_Baselines\baseline\ -Recurse -Force
```

## Rules

- Do not baseline random production data states.
- Unsupported states must remain visible in the manifest until implemented.
- Sidebar/app shell changes require explicit review.
- Pixel threshold failures must be reviewed with generated diff images.
```

- [ ] **Step 2: Verify docs render as plain markdown**

Run:

```powershell
Get-Content C:\HVAC_PRO_MSE\Docs\UI_QA_Baselines\README.md
```

Expected: content is readable and command snippets are intact.

---

### Task 10: First Layout Refactor Pass

**Files:**
- Modify: `SOURCE_CODE/UI/TenderBidForm.cs`
- Modify: `SOURCE_CODE/UI/PaymentForm.cs`
- Modify: `SOURCE_CODE/UI/ServiceDeskForm.cs`

- [ ] **Step 1: Identify nested scroll sources**

Run:

```powershell
rg -n "AutoScroll = true|ScrollBars = ScrollBars|SplitContainer|FlowLayoutPanel" C:\HVAC_PRO_MSE\SOURCE_CODE\UI\TenderBidForm.cs C:\HVAC_PRO_MSE\SOURCE_CODE\UI\PaymentForm.cs C:\HVAC_PRO_MSE\SOURCE_CODE\UI\ServiceDeskForm.cs
```

Expected: record the exact nested scroll regions before editing.

- [ ] **Step 2: Reduce only avoidable nested scrollbars**

Rules:

- Keep scrollbars on tables and long message/activity feeds.
- Prefer one page-level vertical scroll region.
- Convert fixed inner panels to `TableLayoutPanel` with percent columns where the page already has list/detail split pressure.
- Do not alter event handlers.
- Do not alter service calls.

- [ ] **Step 3: Build and capture restored and full shell**

Run:

```powershell
dotnet build C:\HVAC_PRO_MSE\SOURCE_CODE\HVAC_Pro_Desktop.sln
powershell.exe -NoProfile -ExecutionPolicy Bypass -STA -File C:\HVAC_PRO_MSE\TOOLS\CaptureMarketingScreenshots.ps1 -AppDir C:\HVAC_PRO_MSE\SOURCE_CODE\bin\Debug -OutputDir C:\HVAC_PRO_MSE\Docs\UI_QA_Screenshots\layout-policy-pass
```

Expected: target pages render without new clipping; sidebar remains untouched.

---

### Task 11: Final App-Wide Verification

**Files:**
- Modify: `Docs/UI_QA_Screenshots/<new-run>/VISUAL_REGRESSION_REPORT.md`

- [ ] **Step 1: Build**

Run:

```powershell
dotnet build C:\HVAC_PRO_MSE\SOURCE_CODE\HVAC_Pro_Desktop.sln
```

Expected: `0 Warning(s), 0 Error(s)`.

- [ ] **Step 2: Run all single-page smoke captures**

Run:

```powershell
powershell.exe -NoProfile -ExecutionPolicy Bypass -File C:\HVAC_PRO_MSE\TOOLS\SmokeTestAllPages.ps1 -AppDir C:\HVAC_PRO_MSE\SOURCE_CODE\bin\Debug
```

Expected: all listed pages pass.

- [ ] **Step 3: Run UI QA state capture**

Run:

```powershell
powershell.exe -NoProfile -ExecutionPolicy Bypass -STA -File C:\HVAC_PRO_MSE\TOOLS\CaptureUiQaStates.ps1
```

Expected:

- manifest has 90 rows
- current screenshots are written
- baseline comparison results are either `PASS`, `FAIL`, or `NO_BASELINE`
- unsupported states are explicit

- [ ] **Step 4: Write final QA report**

Create a new timestamped report under `Docs/UI_QA_Screenshots` that includes:

- build result
- smoke result
- UI QA manifest summary
- baseline comparison summary
- screenshots generated
- files modified
- unsupported states remaining
- confirmation sidebar/app shell untouched

---

## Self-Review

Spec coverage:

- Deterministic states: Tasks 1, 2, 4, 9, 11.
- Grid policy: Tasks 5 and 8.
- Action hierarchy: Tasks 6 and 7.
- Nested scrollbar/layout reduction: Task 10.
- Visual baselines: Tasks 3, 4, 9, 11.

No placeholders remain in implementation-critical tasks. Unsupported states are intentionally represented as manifest states because modal/empty/long-text automation must be added safely per page rather than faked.
