# ServoERP 1.1.501
Release date: 10/10/2026

## User-facing summary
Deleted clients stay hidden after refresh; linked invoices, jobs, payments, contracts and sites remain preserved.

## Features and fixes
Client deletion atomically marks the client inactive and records a persistent archive marker. Active client selectors also exclude archives. Import validation rejects numeric names and quotation validity or terms-and-conditions rows, while permitting names such as 3M India and 218 Industries.

## Affected modules
Clients Management, client selectors and Excel client creation across document imports.

## Database or migration impact
Uses the existing guarded DuplicateMergeArchive table; creates it if absent. No tables or columns dropped. No linked records deleted or reassigned. Previously inactive records are not automatically archived.

## Configuration and dependency changes
None. No packages added.

## Compatibility or breaking changes
None. Client IDs and historical document relationships remain unchanged.

## Security impact
None. Existing delete permission enforcement retained. No new outbound services.

## Deployment/update steps
Install the 1.1.501 desktop build on office PCs. Delete unwanted clients again to persist their archive marker. Review existing import artifacts before relinking their documents to a correct client.

## Validation performed
Release build succeeded. Smart Upload smoke tests passed, including numeric and quotation-term rejection and valid alphanumeric names. Transactional SQL regression passed archive persistence on refresh, repeated deletion, unchanged invoice amount and invoice/job client links, and ordinary inactive-client visibility; all QA writes rolled back. No interactive customer-machine smoke test performed. Initial prepackaging artifact check correctly rejected missing updater artifacts; public publishing requires the completed Velopack package and feed to pass the same guard.

## Known limitations
Existing invalid names are not automatically repaired. Archived client records remain accessible to historical document joins. Remote customer database not accessed.

## Rollback guidance
Reinstall the previous desktop build. Archive markers and linked business data are retained; do not remove client rows or linked documents.
