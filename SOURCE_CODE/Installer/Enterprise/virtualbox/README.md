# ServoERP VirtualBox Installer QA

This folder contains the repeatable clean-machine test harness for ServoERP enterprise installer validation.

## Current Host Status

Verified on this PC:

- VirtualBox CLI is installed: `7.2.8`
- No registered test VM currently exists
- No Windows install ISO/OVA was found locally, except `VBoxGuestAdditions.iso`
- Host RAM is limited, so close heavy apps before creating or running the VM

## Option A: Create a Clean Windows VM from ISO

Use a Microsoft Windows evaluation or licensed Windows ISO:

```powershell
powershell -ExecutionPolicy Bypass -File C:\HVAC_PRO_MSE\SOURCE_CODE\Installer\Enterprise\virtualbox\New-ServoERPTestVm.ps1 `
  -IsoPath D:\ISO\Win11.iso `
  -MemoryMb 4096 `
  -CpuCount 2 `
  -Start
```

The script creates a Windows 11 VM with EFI, TPM 2.0, NAT networking, Guest Additions, and a shared folder pointing to:

```text
C:\HVAC_PRO_MSE\installer_output\enterprise
```

## Option B: Import a Microsoft Windows OVA

If using a Microsoft developer/evaluation VirtualBox appliance:

```powershell
powershell -ExecutionPolicy Bypass -File C:\HVAC_PRO_MSE\SOURCE_CODE\Installer\Enterprise\virtualbox\New-ServoERPTestVm.ps1 `
  -OvaPath D:\VMs\WinDev.VirtualBox.ova `
  -MemoryMb 4096 `
  -CpuCount 2 `
  -Start
```

## Run Smoke Test

After Windows finishes setup and Guest Additions are active:

```powershell
powershell -ExecutionPolicy Bypass -File C:\HVAC_PRO_MSE\SOURCE_CODE\Installer\Enterprise\virtualbox\Invoke-ServoERPVirtualBoxSmoke.ps1
```

The smoke test:

- Starts the VM if needed
- Waits for VirtualBox Guest Control
- Copies `ServoERP.Setup.1.0.20.0.exe` into the guest
- Runs silent install with bundle logging
- Verifies app files, shortcuts, registry metadata, SQL service, WebView2 registry, and app startup
- Runs repair silently
- Copies logs and `smoke-result.json` back to:

```text
C:\HVAC_PRO_MSE\installer_output\enterprise\vm-smoke
```

## Clean Snapshot Workflow

After the guest is fully installed but before ServoERP is installed:

```powershell
VBoxManage snapshot ServoERP-QA-Win11 take Clean-Windows-Baseline
```

Before each installer regression:

```powershell
VBoxManage controlvm ServoERP-QA-Win11 poweroff
VBoxManage snapshot ServoERP-QA-Win11 restore Clean-Windows-Baseline
```

This keeps installer tests clean and repeatable.
