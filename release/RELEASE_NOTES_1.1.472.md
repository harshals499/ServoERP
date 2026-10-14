# ServoERP 1.1.472 Release Notes

Release date: 03/10/2026

## User-facing summary

Settings is now focused on supported ServoERP administration. The unusable agent, simulation, and AI model controls have been removed so operators no longer encounter experimental tools in the production Settings workspace.

## Features and fixes

- Removed the **Agent Simulation** card and its run/report actions from Settings.
- Removed the **ServoERP Brain - Dev Team** agent dashboard card from Settings.
- Removed the **ServoERP Assistant** provider/model card and assistant status tile from Settings.
- Removed the associated Settings-only load, save, and health-check hooks.
- Preserved all supported company, India defaults, display, HSN/SAC, units, backup, licensing, compliance, update, and diagnostics settings.

## Affected modules

- Settings
- Settings Control Center summary

## Database or migration impact

None. No tables, columns, business records, or stored settings are deleted or renamed.

## Configuration and dependency changes

None. Existing AI configuration values are left untouched for compatibility, and no packages were added or removed.

## Compatibility or breaking changes

The retired Agent Simulation, local agent dashboard, and AI assistant/model controls are no longer available from Settings. Public types, database schema, and supported business workflows remain compatible.

## Security impact

The removed Settings controls no longer initiate the retired local agent or simulation workflows. No authentication, licensing, machine-ID, or outbound-service behavior was changed.

## Deployment and update steps

1. Close ServoERP on the target PC.
2. Deploy the ServoERP 1.1.472 application files using the normal update process.
3. Start ServoERP and open **Settings**.
4. Confirm the Settings workspace contains only supported administration cards.

## Validation performed

- Release rebuild with MSBuild 17.14 for .NET Framework 4.7.2.
- Complete ServoERP CI smoke suite, including UI policies, startup/error handling, offline sync, LAN control, Smart Dispatch, Accepted Quote Delivery, and AMC planner scenarios.
- Rendered Settings verification at 1440 x 900 pixels for both the top and lower workspace views.
- Automated scan of 327 rendered Settings controls confirming the retired agent, simulation, and AI assistant/model labels and actions are absent.
- Verified `SOURCE_CODE/bin/Release/HVAC_Pro_Desktop.exe` exists with file and product version `1.1.472.0`.

## Known limitations

None for this Settings cleanup.

## Rollback guidance

Reinstall the ServoERP 1.1.471 application files if application rollback is required. No database rollback is needed because this release does not change schema or business data.
