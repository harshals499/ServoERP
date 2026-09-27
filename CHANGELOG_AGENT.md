# ServoERP Autonomous Agent Changelog

## 2026-08-18 — 1.1.437

- Change: expanded LAN Control Center with private IPv4 discovery, WinRM readiness, enrolled-node visibility, Enterprise installer selection, selected-PC deployment, SQL configuration, and public desktop shortcut creation.
- Security: Windows administrator and SQL credentials are prompted only in the elevated deployment process; no passwords are embedded in generated packages. Workgroup TrustedHosts is limited to explicitly selected targets.
- Files touched by this iteration: `SOURCE_CODE/MODELS/OfficeLanNodeStatus.cs`, `SOURCE_CODE/SERVICES/OfficeLanControlService.cs`, `SOURCE_CODE/UI/OfficeLanControlForm.cs`, `SOURCE_CODE/Tests/UiPolicyTests.cs`, `VERSION`, `SOURCE_CODE/Properties/AssemblyInfo.cs`, `SOURCE_CODE/HVACPro.config`, `HVACPro.config`, `SOURCE_CODE/Installer/ServoERP.version.iss`, `CHANGELOG.md`, `CHANGELOG_AGENT.md`.

## 2026-08-16 23:23:27 +02:00 — 1.1.436

- Change: separated authenticated-user role-policy evaluation from live license/database enforcement so `/cismoketest` remains CI-safe. Production `SessionManager.HasPermission` still applies the existing `LicenseService.CanPerform` entitlement gate.
- Reasoning: the Release build succeeded, but the CI-safe suite failed with an SSPI SQL error because a pure UI role-policy assertion invoked the production licensing persistence path. The test was environment-dependent and contradicted the suite's no-SQL contract.
- Files touched by this iteration: `SOURCE_CODE/SERVICES/SessionManager.cs`, `SOURCE_CODE/Tests/UiPolicyTests.cs`, `VERSION`, `SOURCE_CODE/Properties/AssemblyInfo.cs`, `SOURCE_CODE/HVACPro.config`, `SOURCE_CODE/Installer/ServoERP.version.iss`, `CHANGELOG.md`, `CHANGELOG_AGENT.md`, `NEEDS_HUMAN_INPUT.md`.
- Verification: `scripts/Invoke-ServoSmokeTests.ps1 -Suite CiSafe` passed; full Release rebuild passed with 0 warnings and 0 errors; `SOURCE_CODE/bin/Release/HVAC_Pro_Desktop.exe` exists (5,915,136 bytes).
- Release result: held locally. Public promotion gate is closed because the worktree contains pre-existing uncommitted protected `MainForm.cs` changes, and no distinct beta/staging deployment channel is configured. No paid action was taken.
- Self-assessment: correctness confidence 9/10; known-issue coverage 7/10; stability 8/10. Overall 8/10.
