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
