# ServoERP 1.1.459 Release Notes

Release date: 03/10/2026

## User-facing summary

ServoERP now converts a customer-accepted quotation into a reviewed delivery plan through a guided wizard. The operator sees the proposed job, checklist, material reservation plan, draft purchase requirements, and billing schedule before confirming.

## Features and fixes

- Added **Accepted Quote → Delivery Wizard** to quotation actions.
- Requires an Accepted/Won quotation or an explicit Quote Accepted/PO Received customer-document state.
- Creates one revenue job per quotation and safely returns the existing job on a repeated confirmation.
- Creates a standard field checklist for scope review, site readiness, material readiness, customer approval, and billing handoff.
- Plans available material reservations without reducing physical stock.
- Creates draft purchase requirements only for material shortfalls and retains the preferred supplier when available.
- Creates one planned billing milestone using the accepted quotation total; it does not create an invoice.
- Clearly confirms that supplier POs, messages, and invoices are not sent automatically.

## Affected modules

- Quotations
- Jobs
- Inventory planning
- Purchases planning
- Billing planning

## Database or migration impact

Additive SQL Server schema guards create `QuotationDeliveryBatches`, `JobMaterialReservations`, `PurchaseRequirements`, and `JobBillingSchedules`, plus supporting indexes. No table or column is renamed or dropped. The `LicenseInfo` structure is unchanged.

## Configuration and dependency changes

None. The implementation uses the existing Dapper dependency and SQL Server connection.

## Compatibility or breaking changes

None. The existing public `CreateDispatchJobFromQuotation(int)` method remains compatible; an overload adds the delivery batch recovery marker.

## Security impact

No new outbound service, credential, or license-validation behavior. Existing role permissions for quotation and work-order actions remain in force.

## Deployment/update steps

1. Close ServoERP on the workstation.
2. Deploy the 1.1.459 application files using the normal ServoERP update process.
3. Start ServoERP and allow the additive schema guards to run.
4. Open an accepted quotation and review **Actions → Accepted Quote → Delivery Wizard**.

## Validation performed

- Release compilation with `dotnet build SOURCE_CODE\\HVAC_Pro_Desktop.sln -c Release --no-restore`.
- CI smoke suite, including material reservation, draft purchase requirement, billing milestone, acceptance gate, and no-auto-send assertions.
- Standalone visual regression capture at 1180 × 780, followed by layout iteration to remove irrelevant global header actions and keep the four outcome cards aligned.
- Normal `/firstrun` migration completed and SQL Server verification confirmed all four new tables and all three supporting indexes.
- Confirmed `SOURCE_CODE/bin/Release/HVAC_Pro_Desktop.exe` exists.

## Known limitations

- Material reservations are planning records and intentionally do not consume or decrement physical stock.
- Purchase requirements remain drafts until procurement creates a supplier PO.
- Billing schedules remain planned until accounts creates an invoice.
- Technician mobile/offline workflows remain deferred until the approved API and sync-conflict architecture exists.

## Rollback guidance

Roll back the application binaries to version 1.1.458. The additive 1.1.459 tables can remain safely in the database; do not drop them during rollback because that could remove delivery-planning records.
