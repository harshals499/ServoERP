# ServoERP Release D Recovery Runbook

Use an elevated PowerShell session on the Office Server. Do not run recovery commands from a worker PC.

## Verify a backup before any recovery

```powershell
Set-Location C:\HVAC_PRO_MSE
.\scripts\api\Test-ServoERPBackupRestore.ps1 `
  -SqlServer 'WIN-S0OB0IQUCPR\SQLEXPRESS' `
  -BackupPath 'C:\HVAC_PRO_MSE\DATABASE\Backups\<backup>.bak'
```

This restores to a temporary `ServoERP_RestoreValidation_*` database, runs integrity and core-table checks, then drops only that temporary database.

## API deployment rollback

1. Stop `ServoERP.Api`.
2. Preserve the failed API directory and `appsettings.Production.json` for diagnosis.
3. Restore the previous known-good API artifact into the configured service directory.
4. Preserve the protected production configuration and certificate thumbprint.
5. Start the service and run `Test-ServoERPDeployment.ps1` with authorized test credentials supplied securely.

Never place database passwords, API keys, or user tokens in a command history or support ticket.

## Database recovery

1. Identify the newest backup that passed restore validation.
2. Stop the API service to prevent writes.
3. Take a final safety backup if SQL Server is still operational.
4. Restore only during an approved recovery window using the existing server-side recovery process.
5. Start the API service and verify readiness before allowing clients to reconnect.

Routine validation must not restore over `HVAC_PRO`; use the isolated validation script above.

## Server replacement

Preserve or securely transfer:

- verified SQL backups;
- ServoERP API artifact and protected configuration;
- certificate including its private key, transferred only through an approved secure administrative channel;
- service configuration and Task Scheduler backup task;
- company attachment/storage folders;
- the current desktop release package.

Reinstall the API service, register the verified backup schedule, validate HTTPS, then move clients only after API readiness succeeds.

## Post-recovery checks

```powershell
.\scripts\api\Get-ServoERPOperationsHealth.ps1 `
  -ApiUrl 'https://WIN-S0OB0IQUCPR:7443' `
  -BackupDirectory 'C:\HVAC_PRO_MSE\DATABASE\Backups' `
  -CertificateThumbprint 'DB0BC5317002FFD8F912B70BDAA055B3C7114BC3'
```

Confirm API service status, HTTPS reachability, backup freshness, certificate expiry, and available backup-disk capacity before ending the incident.
