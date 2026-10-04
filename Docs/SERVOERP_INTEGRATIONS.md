# ServoERP Integrations

This document lists the integration services currently available in the desktop codebase.

## TallyPrime export/sync

Service: `HVAC_Pro_Desktop.Services.Integrations.TallyPrimeIntegrationService`

Capabilities:
- Export ServoERP invoices to Tally voucher XML.
- Export ServoERP purchase orders to Tally voucher XML.
- Push invoice or purchase voucher XML to a configured local TallyPrime HTTP endpoint.

Config section: `TallyPrime`

Keys:
- `Enabled`
- `EndpointUrl`
- `ExportFolder`
- `SalesLedgerName`
- `PurchaseLedgerName`
- `GstLedgerName`

## WhatsApp Cloud API

Service: `HVAC_Pro_Desktop.Services.Integrations.WhatsAppCloudIntegrationService`

Capabilities:
- Send text messages.
- Send approved template messages.
- Normalize Indian 10-digit phone numbers to `91XXXXXXXXXX`.

Config section: `WhatsAppCloud`

Keys:
- `GraphVersion`
- `PhoneNumberId`
- `AccessToken`

## Calendar dispatch

Service: `HVAC_Pro_Desktop.Services.Integrations.CalendarDispatchIntegrationService`

Capabilities:
- Export ServoERP jobs as `.ics` calendar events.
- Include job number, client, site, priority, status, and description.

Config section: `CalendarDispatch`

Keys:
- `ExportFolder`

## Cloud backup

Service: `HVAC_Pro_Desktop.Services.Integrations.CloudBackupIntegrationService`

Capabilities:
- Create a SQL Server backup through the existing `BackupService`.
- Copy backup to a configured folder target.
- Upload backup with HTTP PUT to a pre-signed or protected endpoint.

Config section: `CloudBackup`

Keys:
- `Enabled`
- `TargetType`: `LocalFolder` or `HttpPut`
- `TargetPath`
- `UploadUrl`
- `BearerToken`

## Microsoft OneDrive local sync

Services: `BackupService`, `OneDriveStorageService`

Capabilities:
- Detect the signed-in OneDrive for Business or Personal folder installed on the PC.
- Copy completed SQL Server `.bak` files into the tenant-isolated `OneDrive\ServoERP Backups\<company>` folder.
- Keep the live SQL Server database and the machine-local offline SQLite queue outside OneDrive.

Settings:

Keys:
- `BackupOneDriveEnabled` and `BackupOneDrivePath` in ServoERP user settings control backup copying.
- `OneDriveStorage.RootPath` mirrors the selected local root for the safety guard that keeps the offline SQLite file outside OneDrive.

OneDrive is a backup and ordinary-document destination only. It is not used as a live database or as a shared-file replacement for SQL Server.

## Offline client/site/job queue

Services: `LocalSqliteFallbackStore`, `OfflineSyncService`

Capabilities:
- Queue Clients, Sites, and Jobs create/update operations during SQL connectivity failures.
- Coalesce repeated local edits, remap dependencies created offline, and replay in order.
- Retain stale updates as review conflicts when the SQL Server version changed.

Financial, stock, payroll, and job-material changes remain online-only.

## GST e-invoice

Service: `HVAC_Pro_Desktop.Services.Integrations.GstEinvoiceIntegrationService`

Capabilities:
- Build a GST e-invoice payload from a ServoERP invoice.
- Validate GSTIN, document number, document date, party details, HSN/SAC, quantity, and line amounts.
- Submit payload to a configured GSP/e-invoice API endpoint.

Config section: `GstEinvoice`

Keys:
- `Enabled`
- `EndpointUrl`
- `ClientId`
- `ClientSecret`
- `Gstin`
- `BearerToken`
- `DefaultHsnSac`

## Notes

- These services are intentionally isolated from existing UI and business logic.
- Missing credentials return a controlled failure result instead of throwing into forms.
- The next wiring step is to add explicit action buttons or scheduled jobs where each workflow needs them.
