# ServoERP Local VM Update Test Plan

## Goal

Verify that an old ServoERP installation inside a VM can detect, download, install, and relaunch into the latest ServoERP build served from the main PC, while preserving settings, data, and license state.

This plan does not require UI or business-logic changes. Only fix update-check, update-download, or update-install code if a test failure proves the updater is broken.

## Current Updater Behavior

ServoERP currently has two update-check paths:

- Settings page notification check: uses `HVACPro.config` value `App/VersionCheckUrl`. This can point directly to a local HTTP URL such as `http://MAIN_PC_IP:8080/version.txt`, but it only reports availability and tells the user to contact the administrator.
- Real startup update/install flow: `SOURCE_CODE/SERVICES/UpdateService.cs` currently uses hardcoded production URLs:
  - `https://servoerp.in/version.txt`
  - `https://servoerp.in/changelog.json`
  - default update package base `https://servoerp.in/updates/`

For an exact local customer-style update test from the main PC, use the HTTPS hosts-file method in this plan. The simple HTTP method is still useful for quick VM connectivity and Settings-page notification testing.

## Test Artifacts

Main PC workspace:

```text
C:\HVAC_PRO_MSE
```

Recommended local update server folder:

```text
C:\ServoERP_UpdateServer
```

Required files:

```text
C:\ServoERP_UpdateServer\version.txt
C:\ServoERP_UpdateServer\changelog.json
C:\ServoERP_UpdateServer\updates\ServoERP_Update_1.0.22.0.zip
C:\ServoERP_UpdateServer\download\index.html
C:\ServoERP_UpdateServer\download\ServoERP.Setup.1.0.22.0.exe
C:\ServoERP_UpdateServer\checksum.txt
```

Use a lower old VM version than `version.txt`.

Example:

```text
Old VM app: 1.0.20.0
Latest version.txt: 1.0.22.0
```

## Main PC Preparation

1. Close ServoERP on the main PC.

2. Build Release:

```powershell
cd C:\HVAC_PRO_MSE\SOURCE_CODE
dotnet build .\HVAC_Pro_Desktop.sln -c Release
```

3. Generate update ZIP:

```powershell
cd C:\HVAC_PRO_MSE
powershell -ExecutionPolicy Bypass -File .\SOURCE_CODE\Installer\Build-ServoERPUpdatePackage.ps1 -ForceCloseRunningApp
```

Expected output:

```text
C:\HVAC_PRO_MSE\update_output\ServoERP_Update_1.0.22.0.zip
```

4. Confirm installer exists:

```powershell
Test-Path C:\HVAC_PRO_MSE\installer_output\enterprise\ServoERP.Setup.1.0.22.0.exe
```

5. Create the local update server folder:

```powershell
$server = 'C:\ServoERP_UpdateServer'
New-Item -ItemType Directory -Force -Path $server, "$server\updates", "$server\download" | Out-Null
Copy-Item C:\HVAC_PRO_MSE\update_output\ServoERP_Update_1.0.22.0.zip "$server\updates\" -Force
Copy-Item C:\HVAC_PRO_MSE\installer_output\enterprise\ServoERP.Setup.1.0.22.0.exe "$server\download\" -Force
'1.0.22.0' | Set-Content "$server\version.txt" -Encoding ASCII
```

6. Create `changelog.json` for the real updater:

```powershell
@'
{
  "product": "ServoERP",
  "latestVersion": "1.0.22.0",
  "updatedAt": "2026-05-10",
  "download": {
    "url": "/download/",
    "installer": "ServoERP.Setup.1.0.22.0.exe",
    "packageUrl": "/updates/ServoERP_Update_1.0.22.0.zip"
  },
  "versions": [
    {
      "version": "1.0.22.0",
      "date": "2026-05-10",
      "title": "Local VM update test build",
      "changes": [
        {
          "type": "release",
          "items": [
            "Local update server validation package.",
            "Used to verify detection, download, apply, and relaunch behavior."
          ]
        }
      ]
    }
  ]
}
'@ | Set-Content C:\ServoERP_UpdateServer\changelog.json -Encoding UTF8
```

7. Create a download page that resolves to the installer:

```powershell
@'
<!doctype html>
<html>
  <head>
    <meta http-equiv="refresh" content="0; url=/download/ServoERP.Setup.1.0.22.0.exe">
  </head>
  <body>
    <a href="/download/ServoERP.Setup.1.0.22.0.exe">Download ServoERP</a>
  </body>
</html>
'@ | Set-Content C:\ServoERP_UpdateServer\download\index.html -Encoding UTF8
```

8. Create checksums:

```powershell
Get-FileHash C:\ServoERP_UpdateServer\updates\ServoERP_Update_1.0.22.0.zip -Algorithm SHA256 |
  ForEach-Object { "ServoERP_Update_1.0.22.0.zip SHA256 $($_.Hash)" } |
  Set-Content C:\ServoERP_UpdateServer\checksum.txt -Encoding ASCII

Get-FileHash C:\ServoERP_UpdateServer\download\ServoERP.Setup.1.0.22.0.exe -Algorithm SHA256 |
  ForEach-Object { "ServoERP.Setup.1.0.22.0.exe SHA256 $($_.Hash)" } |
  Add-Content C:\ServoERP_UpdateServer\checksum.txt
```

## Network Preparation

1. Find main PC IP:

```powershell
ipconfig
```

Use the IPv4 address reachable from the VM, for example:

```text
192.168.1.50
```

2. Confirm VM network mode:

- Bridged Adapter is easiest.
- NAT can work if the VM can reach the host IP.
- Host-only requires the host-only adapter IP.

3. Open firewall on the main PC for the test port:

```powershell
New-NetFirewallRule -DisplayName "ServoERP Local Update Test 8080" -Direction Inbound -Action Allow -Protocol TCP -LocalPort 8080
```

Remove it after testing:

```powershell
Remove-NetFirewallRule -DisplayName "ServoERP Local Update Test 8080"
```

## Lane A: Simple HTTP Connectivity And Settings Check

Use this lane first. It proves the VM can reach the main PC and confirms the configurable Settings-page check can read a local version file.

1. Start HTTP server on main PC:

```powershell
cd C:\ServoERP_UpdateServer
python -m http.server 8080
```

2. From VM, test network:

```powershell
ping MAIN_PC_IP
curl http://MAIN_PC_IP:8080/version.txt
curl http://MAIN_PC_IP:8080/changelog.json
curl -I http://MAIN_PC_IP:8080/updates/ServoERP_Update_1.0.22.0.zip
```

3. In old ServoERP inside VM:

- Open Settings.
- Set Version File URL to:

```text
http://MAIN_PC_IP:8080/version.txt
```

- Enable automatic update checks.
- Click Check Update.

Expected result:

- App reports that version `1.0.22.0` is available.
- This does not test automatic ZIP download/apply because the Settings check is notification-only.

## Lane B: Full Customer-Style Local Update Test

Use this lane to test the real startup update dialog, update ZIP download, PowerShell apply script, app restart, and final version change.

Because the production updater currently calls `https://servoerp.in/...`, this lane maps `servoerp.in` to the main PC and serves the update files over trusted HTTPS.

### B1. Create a local trusted certificate

Use one of these options:

- Preferred: `mkcert` certificate for `servoerp.in`, then import the mkcert root CA into the VM Trusted Root Certification Authorities store.
- Alternative: IIS Express/IIS with a certificate trusted by the VM.
- Avoid untrusted self-signed certificates because `HttpClient` will reject them.

Certificate must be valid for:

```text
servoerp.in
```

### B2. Start a local HTTPS server on main PC

Serve `C:\ServoERP_UpdateServer` on port `443` for host `servoerp.in`.

Validation from main PC:

```powershell
curl https://servoerp.in/version.txt
curl https://servoerp.in/changelog.json
curl -I https://servoerp.in/updates/ServoERP_Update_1.0.22.0.zip
```

If port `443` is not convenient, do not change the app yet. First prefer using a reverse proxy from `443` to another local port so the hardcoded updater URL remains unchanged.

### B3. Map VM traffic to the main PC

On the VM, edit Notepad as Administrator:

```text
C:\Windows\System32\drivers\etc\hosts
```

Add:

```text
MAIN_PC_IP servoerp.in
```

Flush DNS:

```powershell
ipconfig /flushdns
```

Confirm from VM:

```powershell
ping servoerp.in
curl https://servoerp.in/version.txt
curl https://servoerp.in/changelog.json
curl -I https://servoerp.in/updates/ServoERP_Update_1.0.22.0.zip
```

Expected:

- `version.txt` returns `1.0.22.0`.
- `changelog.json` returns JSON.
- ZIP URL returns `200 OK`, content type not HTML, and a nonzero content length.

### B4. Run the old app update flow

1. Snapshot the VM before testing.

2. Confirm old install version:

- Open old ServoERP.
- Note visible version in Settings or About if present.
- Confirm installed file version if needed:

```powershell
(Get-Item "C:\Program Files\ServoERP\HVAC_Pro_Desktop.exe").VersionInfo.FileVersion
```

3. Confirm old app config/data/license:

Record:

```text
Install folder:
Config path:
Database name:
License state:
Current user:
Known test records:
```

4. Launch old ServoERP normally.

5. Log in.

6. Wait for the startup update check.

Expected:

- Update dialog says ServoERP `1.0.22.0` is available.
- Changelog text is shown.
- Install/Later buttons are visible.

7. Click Install.

Expected:

- Progress dialog downloads `ServoERP_Update_1.0.22.0.zip`.
- ZIP appears in:

```text
C:\HVAC_PRO_MSE\UPDATES\ServoERP_Update_1.0.22.0.zip
```

- App exits.
- `Apply-ServoERPUpdate.ps1` runs.
- App relaunches.

8. Confirm final version:

```powershell
(Get-Item "C:\Program Files\ServoERP\HVAC_Pro_Desktop.exe").VersionInfo.FileVersion
```

or the actual install folder used by the VM.

Expected:

```text
1.0.22.0
```

9. Confirm data and settings:

- Login still works.
- License remains active.
- Database connection still works.
- Existing clients/vendors/invoices/jobs are still present.
- Company settings remain intact.
- No missing DLL/config startup errors.
- New UI/features from the latest build are visible.

10. Check update logs:

```powershell
Get-Content C:\HVAC_PRO_MSE\LOGS\update-apply.log -Tail 80
Get-Content C:\HVAC_PRO_MSE\LOGS\app.log -Tail 120
```

Use the actual VM log path if different.

## Failure Triage

### VM cannot reach main PC

Check:

```powershell
ping MAIN_PC_IP
Test-NetConnection MAIN_PC_IP -Port 8080
Test-NetConnection MAIN_PC_IP -Port 443
```

Fix:

- Use bridged networking or host-only adapter.
- Open Windows Firewall.
- Confirm server is listening.
- Confirm main PC and VM are on same network.

### Settings check works but startup update does not

Likely cause:

- Settings uses configurable `VersionCheckUrl`.
- Startup updater uses hardcoded `https://servoerp.in`.

Fix options:

1. Use Lane B hosts-file plus trusted HTTPS.
2. If local testing must use plain HTTP, make a small updater fix to read update URLs from config, then rebuild. Only do this if the test requires it.

### Update dialog appears but download fails

Check:

- `changelog.json` has `download.packageUrl`.
- ZIP exists at `/updates/ServoERP_Update_1.0.22.0.zip`.
- Response is not HTML.
- Content-Length is nonzero.
- VM can download the ZIP manually.

Manual test:

```powershell
Invoke-WebRequest https://servoerp.in/updates/ServoERP_Update_1.0.22.0.zip -OutFile $env:TEMP\ServoERP_Update_1.0.22.0.zip
Get-FileHash $env:TEMP\ServoERP_Update_1.0.22.0.zip -Algorithm SHA256
```

### Download succeeds but install/apply fails

Check:

- ServoERP process fully exits.
- User has write permission to install folder.
- Antivirus did not block PowerShell.
- `update-apply.log`.
- Package contains `HVAC_Pro_Desktop.exe`, DLLs, config, resources, and runtimes.

### App updates but loses config

Expected updater behavior skips copying `HVACPro.config` from the ZIP, so local config should be preserved. If config is lost, inspect `BuildPackageUpdaterScript()` in `SOURCE_CODE/SERVICES/UpdateService.cs` and the generated ZIP contents.

## Validation Matrix

| Test | Expected | Pass/Fail | Notes |
| --- | --- | --- | --- |
| Main PC Release build | Build succeeds |  |  |
| Update ZIP generated | ZIP exists in `update_output` |  |  |
| Local server version URL | VM reads latest version |  |  |
| Local server changelog URL | VM reads JSON |  |  |
| Local server package URL | VM receives ZIP headers |  |  |
| Old version lower than latest | Old `<` latest |  |  |
| Update detected | Dialog or message appears |  |  |
| Changelog displayed | Version notes visible |  |  |
| Package downloaded | ZIP in update folder |  |  |
| Package applied | Files copied |  |  |
| App relaunches | ServoERP opens again |  |  |
| Final version updated | Shows latest version |  |  |
| Data preserved | Existing records present |  |  |
| License preserved | License active |  |  |
| Config preserved | DB/update settings intact |  |  |
| Logs clean | No update errors |  |  |

## Test Report Template

```text
ServoERP Local VM Update Test Report

Date:
Tester:

Main PC:
- IP:
- Update server path:
- Server mode: HTTP / HTTPS hosts spoof
- Version URL:
- Changelog URL:
- Package URL:
- Installer URL:

Artifacts:
- version.txt content:
- update ZIP:
- update ZIP SHA256:
- installer:
- installer SHA256:

VM:
- Hypervisor:
- Network mode:
- Old app version:
- Old install path:
- Database:
- License state before:

Results:
- VM can ping main PC: Pass / Fail
- VM can open version.txt: Pass / Fail
- VM can open changelog.json: Pass / Fail
- VM can reach update ZIP: Pass / Fail
- New version detected: Pass / Fail
- Download success: Pass / Fail
- Install/apply success: Pass / Fail
- App relaunched: Pass / Fail
- Final app version:
- Data preserved: Pass / Fail
- License preserved: Pass / Fail
- Settings preserved: Pass / Fail

Errors:
- Error message:
- Log file:
- Root cause:
- Fix applied:

Final decision:
- Ready for customer update test: Yes / No
```

## Cleanup

1. Stop the local HTTP/HTTPS server.

2. Remove firewall rule if created:

```powershell
Remove-NetFirewallRule -DisplayName "ServoERP Local Update Test 8080"
```

3. Remove VM hosts entry:

```text
MAIN_PC_IP servoerp.in
```

4. Flush DNS in VM:

```powershell
ipconfig /flushdns
```

5. Keep or archive:

```text
C:\ServoERP_UpdateServer
C:\HVAC_PRO_MSE\update_output
C:\HVAC_PRO_MSE\release
```

