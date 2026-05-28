param(
    [string]$Server = "localhost\SQLEXPRESS",
    [string]$Database = "HVAC_PRO",
    [switch]$SkipBackup,
    [switch]$WhatIfOnly
)

$ErrorActionPreference = "Stop"

$root = "C:\HVAC_PRO_MSE"
$backupDir = Join-Path $root "DATABASE\Backups"
$logDir = Join-Path $root "LOGS"
New-Item -ItemType Directory -Force -Path $backupDir, $logDir | Out-Null

$timestamp = Get-Date -Format "yyyyMMdd_HHmmss"
$backupPath = Join-Path $backupDir "HVAC_PRO_before_test_demo_cleanup_$timestamp.bak"
$logPath = Join-Path $logDir "test-demo-cleanup-$timestamp.log"
$connectionString = "Server=$Server;Database=$Database;Integrated Security=True;TrustServerCertificate=True;Connection Timeout=15;"

function Write-Log {
    param([string]$Message)
    $line = "$(Get-Date -Format s) $Message"
    Add-Content -Path $logPath -Value $line
    Write-Host $line
}

function Invoke-Sql {
    param(
        [string]$Sql,
        [string]$ConnectionString = $script:connectionString,
        [int]$Timeout = 300
    )

    $connection = New-Object System.Data.SqlClient.SqlConnection $ConnectionString
    $command = $connection.CreateCommand()
    $command.CommandText = $Sql
    $command.CommandTimeout = $Timeout
    $adapter = New-Object System.Data.SqlClient.SqlDataAdapter $command
    $table = New-Object System.Data.DataTable
    try {
        $connection.Open()
        [void]$adapter.Fill($table)
        return $table
    }
    finally {
        $connection.Close()
        $connection.Dispose()
    }
}

$markerPredicate = @"
(
    LOWER(CONCAT_WS(N'|',
        ISNULL(CAST(CompanyName AS NVARCHAR(MAX)), N''),
        ISNULL(CAST(ClientName AS NVARCHAR(MAX)), N''),
        ISNULL(CAST(SiteName AS NVARCHAR(MAX)), N''),
        ISNULL(CAST(VendorName AS NVARCHAR(MAX)), N''),
        ISNULL(CAST(Name AS NVARCHAR(MAX)), N''),
        ISNULL(CAST(EmployeeCode AS NVARCHAR(MAX)), N''),
        ISNULL(CAST(InvoiceNumber AS NVARCHAR(MAX)), N''),
        ISNULL(CAST(PaymentNumber AS NVARCHAR(MAX)), N''),
        ISNULL(CAST(JobNumber AS NVARCHAR(MAX)), N''),
        ISNULL(CAST(QuotationNumber AS NVARCHAR(MAX)), N''),
        ISNULL(CAST(PONumber AS NVARCHAR(MAX)), N''),
        ISNULL(CAST(Notes AS NVARCHAR(MAX)), N''),
        ISNULL(CAST(Description AS NVARCHAR(MAX)), N''),
        ISNULL(CAST(Subject AS NVARCHAR(MAX)), N''),
        ISNULL(CAST(Email AS NVARCHAR(MAX)), N''),
        ISNULL(CAST(Tags AS NVARCHAR(MAX)), N''),
        ISNULL(CAST(LeadSource AS NVARCHAR(MAX)), N'')
    )) LIKE N'%servoerp demo%'
    OR LOWER(CONCAT_WS(N'|',
        ISNULL(CAST(CompanyName AS NVARCHAR(MAX)), N''),
        ISNULL(CAST(ClientName AS NVARCHAR(MAX)), N''),
        ISNULL(CAST(SiteName AS NVARCHAR(MAX)), N''),
        ISNULL(CAST(VendorName AS NVARCHAR(MAX)), N''),
        ISNULL(CAST(Name AS NVARCHAR(MAX)), N''),
        ISNULL(CAST(EmployeeCode AS NVARCHAR(MAX)), N''),
        ISNULL(CAST(InvoiceNumber AS NVARCHAR(MAX)), N''),
        ISNULL(CAST(PaymentNumber AS NVARCHAR(MAX)), N''),
        ISNULL(CAST(JobNumber AS NVARCHAR(MAX)), N''),
        ISNULL(CAST(QuotationNumber AS NVARCHAR(MAX)), N''),
        ISNULL(CAST(PONumber AS NVARCHAR(MAX)), N''),
        ISNULL(CAST(Notes AS NVARCHAR(MAX)), N''),
        ISNULL(CAST(Description AS NVARCHAR(MAX)), N''),
        ISNULL(CAST(Subject AS NVARCHAR(MAX)), N''),
        ISNULL(CAST(Email AS NVARCHAR(MAX)), N''),
        ISNULL(CAST(Tags AS NVARCHAR(MAX)), N''),
        ISNULL(CAST(LeadSource AS NVARCHAR(MAX)), N'')
    )) LIKE N'%demo%'
    OR LOWER(CONCAT_WS(N'|',
        ISNULL(CAST(CompanyName AS NVARCHAR(MAX)), N''),
        ISNULL(CAST(ClientName AS NVARCHAR(MAX)), N''),
        ISNULL(CAST(SiteName AS NVARCHAR(MAX)), N''),
        ISNULL(CAST(VendorName AS NVARCHAR(MAX)), N''),
        ISNULL(CAST(Name AS NVARCHAR(MAX)), N''),
        ISNULL(CAST(EmployeeCode AS NVARCHAR(MAX)), N''),
        ISNULL(CAST(InvoiceNumber AS NVARCHAR(MAX)), N''),
        ISNULL(CAST(PaymentNumber AS NVARCHAR(MAX)), N''),
        ISNULL(CAST(JobNumber AS NVARCHAR(MAX)), N''),
        ISNULL(CAST(QuotationNumber AS NVARCHAR(MAX)), N''),
        ISNULL(CAST(PONumber AS NVARCHAR(MAX)), N''),
        ISNULL(CAST(Notes AS NVARCHAR(MAX)), N''),
        ISNULL(CAST(Description AS NVARCHAR(MAX)), N''),
        ISNULL(CAST(Subject AS NVARCHAR(MAX)), N''),
        ISNULL(CAST(Email AS NVARCHAR(MAX)), N''),
        ISNULL(CAST(Tags AS NVARCHAR(MAX)), N''),
        ISNULL(CAST(LeadSource AS NVARCHAR(MAX)), N'')
    )) LIKE N'%test%'
    OR LOWER(CONCAT_WS(N'|',
        ISNULL(CAST(CompanyName AS NVARCHAR(MAX)), N''),
        ISNULL(CAST(ClientName AS NVARCHAR(MAX)), N''),
        ISNULL(CAST(SiteName AS NVARCHAR(MAX)), N''),
        ISNULL(CAST(VendorName AS NVARCHAR(MAX)), N''),
        ISNULL(CAST(Name AS NVARCHAR(MAX)), N''),
        ISNULL(CAST(EmployeeCode AS NVARCHAR(MAX)), N''),
        ISNULL(CAST(InvoiceNumber AS NVARCHAR(MAX)), N''),
        ISNULL(CAST(PaymentNumber AS NVARCHAR(MAX)), N''),
        ISNULL(CAST(JobNumber AS NVARCHAR(MAX)), N''),
        ISNULL(CAST(QuotationNumber AS NVARCHAR(MAX)), N''),
        ISNULL(CAST(PONumber AS NVARCHAR(MAX)), N''),
        ISNULL(CAST(Notes AS NVARCHAR(MAX)), N''),
        ISNULL(CAST(Description AS NVARCHAR(MAX)), N''),
        ISNULL(CAST(Subject AS NVARCHAR(MAX)), N''),
        ISNULL(CAST(Email AS NVARCHAR(MAX)), N''),
        ISNULL(CAST(Tags AS NVARCHAR(MAX)), N''),
        ISNULL(CAST(LeadSource AS NVARCHAR(MAX)), N'')
    )) LIKE N'%sample%'
)
"@

Write-Log "Starting selective test/demo cleanup for $Server / $Database"

if (-not $SkipBackup -and -not $WhatIfOnly) {
    $masterConnection = "Server=$Server;Database=master;Integrated Security=True;TrustServerCertificate=True;Connection Timeout=15;"
    $escapedBackupPath = $backupPath.Replace("'", "''")
    Invoke-Sql -ConnectionString $masterConnection -Timeout 900 -Sql "BACKUP DATABASE [$Database] TO DISK = N'$escapedBackupPath' WITH INIT, COPY_ONLY;"
    Write-Log "Backup created: $backupPath"
}

$sql = @"
SET NOCOUNT ON;

DECLARE @WhatIf BIT = $(if ($WhatIfOnly) { "1" } else { "0" });
DECLARE @Deleted TABLE (TableName SYSNAME, RowsDeleted INT);

CREATE TABLE #DemoClients (ClientID INT PRIMARY KEY);
CREATE TABLE #DemoSites (SiteID INT PRIMARY KEY);
CREATE TABLE #DemoVendors (VendorID INT PRIMARY KEY);
CREATE TABLE #DemoEmployees (EmployeeID INT PRIMARY KEY);
CREATE TABLE #DemoContracts (ContractID INT PRIMARY KEY);
CREATE TABLE #DemoInvoices (InvoiceID INT PRIMARY KEY);
CREATE TABLE #DemoPayments (PaymentID INT PRIMARY KEY);
CREATE TABLE #DemoQuotes (BidID INT PRIMARY KEY);
CREATE TABLE #DemoJobs (JobID INT PRIMARY KEY);
CREATE TABLE #DemoPOs (POID INT PRIMARY KEY);
CREATE TABLE #DemoStock (ItemID INT PRIMARY KEY);

IF OBJECT_ID('B2BClients','U') IS NOT NULL
INSERT INTO #DemoClients SELECT ClientID FROM B2BClients
WHERE LOWER(CONCAT_WS(N'|', ISNULL(CompanyName,N''), ISNULL(PrimaryContact,N''), ISNULL(Email,N''), ISNULL(Tags,N''), ISNULL(LeadSource,N''), ISNULL(Notes,N''))) LIKE N'%demo%'
   OR LOWER(CONCAT_WS(N'|', ISNULL(CompanyName,N''), ISNULL(PrimaryContact,N''), ISNULL(Email,N''), ISNULL(Tags,N''), ISNULL(LeadSource,N''), ISNULL(Notes,N''))) LIKE N'%test%'
   OR LOWER(CONCAT_WS(N'|', ISNULL(CompanyName,N''), ISNULL(PrimaryContact,N''), ISNULL(Email,N''), ISNULL(Tags,N''), ISNULL(LeadSource,N''), ISNULL(Notes,N''))) LIKE N'%sample%';

IF OBJECT_ID('ClientSites','U') IS NOT NULL
INSERT INTO #DemoSites SELECT SiteID FROM ClientSites
WHERE ClientID IN (SELECT ClientID FROM #DemoClients)
   OR LOWER(CONCAT_WS(N'|', ISNULL(SiteName,N''), ISNULL(Address,N''), ISNULL(City,N''))) LIKE N'%demo%'
   OR LOWER(CONCAT_WS(N'|', ISNULL(SiteName,N''), ISNULL(Address,N''), ISNULL(City,N''))) LIKE N'%test%'
   OR LOWER(CONCAT_WS(N'|', ISNULL(SiteName,N''), ISNULL(Address,N''), ISNULL(City,N''))) LIKE N'%sample%';

IF OBJECT_ID('Vendors','U') IS NOT NULL
INSERT INTO #DemoVendors SELECT VendorID FROM Vendors
WHERE LOWER(CONCAT_WS(N'|', ISNULL(VendorName,N''), ISNULL(Email,N''), ISNULL(Category,N''), ISNULL(SpecialisationTags,N''), ISNULL(Notes,N''))) LIKE N'%demo%'
   OR LOWER(CONCAT_WS(N'|', ISNULL(VendorName,N''), ISNULL(Email,N''), ISNULL(Category,N''), ISNULL(SpecialisationTags,N''), ISNULL(Notes,N''))) LIKE N'%test%'
   OR LOWER(CONCAT_WS(N'|', ISNULL(VendorName,N''), ISNULL(Email,N''), ISNULL(Category,N''), ISNULL(SpecialisationTags,N''), ISNULL(Notes,N''))) LIKE N'%sample%';

IF OBJECT_ID('Employees','U') IS NOT NULL
INSERT INTO #DemoEmployees SELECT EmployeeID FROM Employees
WHERE LOWER(CONCAT_WS(N'|', ISNULL(EmployeeCode,N''), ISNULL(Name,N''), ISNULL(Designation,N''), ISNULL(Department,N''), ISNULL(ClientSite,N''), ISNULL(PANNumber,N''), ISNULL(PAN,N''))) LIKE N'%demo%'
   OR LOWER(CONCAT_WS(N'|', ISNULL(EmployeeCode,N''), ISNULL(Name,N''), ISNULL(Designation,N''), ISNULL(Department,N''), ISNULL(ClientSite,N''), ISNULL(PANNumber,N''), ISNULL(PAN,N''))) LIKE N'%test%'
   OR LOWER(CONCAT_WS(N'|', ISNULL(EmployeeCode,N''), ISNULL(Name,N''), ISNULL(Designation,N''), ISNULL(Department,N''), ISNULL(ClientSite,N''), ISNULL(PANNumber,N''), ISNULL(PAN,N''))) LIKE N'%sample%';

IF OBJECT_ID('AMCContracts','U') IS NOT NULL
INSERT INTO #DemoContracts SELECT ContractID FROM AMCContracts
WHERE ClientID IN (SELECT ClientID FROM #DemoClients)
   OR SiteID IN (SELECT SiteID FROM #DemoSites)
   OR LOWER(ISNULL(Notes,N'')) LIKE N'%demo%'
   OR LOWER(ISNULL(Notes,N'')) LIKE N'%test%'
   OR LOWER(ISNULL(Notes,N'')) LIKE N'%sample%';

IF OBJECT_ID('Invoices','U') IS NOT NULL
INSERT INTO #DemoInvoices SELECT InvoiceID FROM Invoices
WHERE ClientID IN (SELECT ClientID FROM #DemoClients)
   OR SiteID IN (SELECT SiteID FROM #DemoSites)
   OR ContractID IN (SELECT ContractID FROM #DemoContracts)
   OR LOWER(CONCAT_WS(N'|', ISNULL(InvoiceNumber,N''), ISNULL(Subject,N''), ISNULL(Notes,N''), ISNULL(PONumber,N''), ISNULL(CreatedByName,N''))) LIKE N'%demo%'
   OR LOWER(CONCAT_WS(N'|', ISNULL(InvoiceNumber,N''), ISNULL(Subject,N''), ISNULL(Notes,N''), ISNULL(PONumber,N''), ISNULL(CreatedByName,N''))) LIKE N'%test%'
   OR LOWER(CONCAT_WS(N'|', ISNULL(InvoiceNumber,N''), ISNULL(Subject,N''), ISNULL(Notes,N''), ISNULL(PONumber,N''), ISNULL(CreatedByName,N''))) LIKE N'%sample%';

IF OBJECT_ID('Payments','U') IS NOT NULL
INSERT INTO #DemoPayments SELECT PaymentID FROM Payments
WHERE InvoiceID IN (SELECT InvoiceID FROM #DemoInvoices)
   OR ClientID IN (SELECT ClientID FROM #DemoClients)
   OR LOWER(CONCAT_WS(N'|', ISNULL(PaymentNumber,N''), ISNULL(ReferenceNumber,N''), ISNULL(Notes,N''), ISNULL(CreatedByName,N''))) LIKE N'%demo%'
   OR LOWER(CONCAT_WS(N'|', ISNULL(PaymentNumber,N''), ISNULL(ReferenceNumber,N''), ISNULL(Notes,N''), ISNULL(CreatedByName,N''))) LIKE N'%test%'
   OR LOWER(CONCAT_WS(N'|', ISNULL(PaymentNumber,N''), ISNULL(ReferenceNumber,N''), ISNULL(Notes,N''), ISNULL(CreatedByName,N''))) LIKE N'%sample%';

IF OBJECT_ID('TenderBids','U') IS NOT NULL
INSERT INTO #DemoQuotes SELECT BidID FROM TenderBids
WHERE ClientID IN (SELECT ClientID FROM #DemoClients)
   OR SiteID IN (SELECT SiteID FROM #DemoSites)
   OR RecommendedVendorID IN (SELECT VendorID FROM #DemoVendors)
   OR LOWER(CONCAT_WS(N'|', ISNULL(QuotationNumber,N''), ISNULL(TenderName,N''), ISNULL(ClientName,N''), ISNULL(Notes,N''), ISNULL(ComparisonSummary,N''), ISNULL(CreatedByName,N''))) LIKE N'%demo%'
   OR LOWER(CONCAT_WS(N'|', ISNULL(QuotationNumber,N''), ISNULL(TenderName,N''), ISNULL(ClientName,N''), ISNULL(Notes,N''), ISNULL(ComparisonSummary,N''), ISNULL(CreatedByName,N''))) LIKE N'%test%'
   OR LOWER(CONCAT_WS(N'|', ISNULL(QuotationNumber,N''), ISNULL(TenderName,N''), ISNULL(ClientName,N''), ISNULL(Notes,N''), ISNULL(ComparisonSummary,N''), ISNULL(CreatedByName,N''))) LIKE N'%sample%';

IF OBJECT_ID('Jobs','U') IS NOT NULL
INSERT INTO #DemoJobs SELECT JobID FROM Jobs
WHERE ClientID IN (SELECT ClientID FROM #DemoClients)
   OR SiteID IN (SELECT SiteID FROM #DemoSites)
   OR AssignedEmployeeID IN (SELECT EmployeeID FROM #DemoEmployees)
   OR LOWER(CONCAT_WS(N'|', ISNULL(JobNumber,N''), ISNULL(Title,N''), ISNULL(JobTitle,N''), ISNULL(Description,N''), ISNULL(Notes,N''), ISNULL(CreatedByName,N''))) LIKE N'%demo%'
   OR LOWER(CONCAT_WS(N'|', ISNULL(JobNumber,N''), ISNULL(Title,N''), ISNULL(JobTitle,N''), ISNULL(Description,N''), ISNULL(Notes,N''), ISNULL(CreatedByName,N''))) LIKE N'%test%'
   OR LOWER(CONCAT_WS(N'|', ISNULL(JobNumber,N''), ISNULL(Title,N''), ISNULL(JobTitle,N''), ISNULL(Description,N''), ISNULL(Notes,N''), ISNULL(CreatedByName,N''))) LIKE N'%sample%';

IF OBJECT_ID('PurchaseOrders','U') IS NOT NULL
INSERT INTO #DemoPOs SELECT POID FROM PurchaseOrders
WHERE ClientID IN (SELECT ClientID FROM #DemoClients)
   OR SiteID IN (SELECT SiteID FROM #DemoSites)
   OR VendorID IN (SELECT VendorID FROM #DemoVendors)
   OR AssignedTechnicianId IN (SELECT EmployeeID FROM #DemoEmployees)
   OR LOWER(CONCAT_WS(N'|', ISNULL(PONumber,N''), ISNULL(Notes,N''), ISNULL(ComparisonNotes,N''), ISNULL(VendorInvoiceNumber,N''), ISNULL(AssignedTechnicianName,N''), ISNULL(CreatedByName,N''))) LIKE N'%demo%'
   OR LOWER(CONCAT_WS(N'|', ISNULL(PONumber,N''), ISNULL(Notes,N''), ISNULL(ComparisonNotes,N''), ISNULL(VendorInvoiceNumber,N''), ISNULL(AssignedTechnicianName,N''), ISNULL(CreatedByName,N''))) LIKE N'%test%'
   OR LOWER(CONCAT_WS(N'|', ISNULL(PONumber,N''), ISNULL(Notes,N''), ISNULL(ComparisonNotes,N''), ISNULL(VendorInvoiceNumber,N''), ISNULL(AssignedTechnicianName,N''), ISNULL(CreatedByName,N''))) LIKE N'%sample%';

IF OBJECT_ID('StockItems','U') IS NOT NULL
INSERT INTO #DemoStock SELECT ItemID FROM StockItems
WHERE VendorID IN (SELECT VendorID FROM #DemoVendors)
   OR LOWER(CONCAT_WS(N'|', ISNULL(ItemName,N''), ISNULL(Category,N''), ISNULL(Unit,N''))) LIKE N'%demo%'
   OR LOWER(CONCAT_WS(N'|', ISNULL(ItemName,N''), ISNULL(Category,N''), ISNULL(Unit,N''))) LIKE N'%test%'
   OR LOWER(CONCAT_WS(N'|', ISNULL(ItemName,N''), ISNULL(Category,N''), ISNULL(Unit,N''))) LIKE N'%sample%';

SELECT 'B2BClients' AS TableName, COUNT(*) AS RowsMatched FROM #DemoClients UNION ALL
SELECT 'ClientSites', COUNT(*) FROM #DemoSites UNION ALL
SELECT 'Vendors', COUNT(*) FROM #DemoVendors UNION ALL
SELECT 'Employees', COUNT(*) FROM #DemoEmployees UNION ALL
SELECT 'AMCContracts', COUNT(*) FROM #DemoContracts UNION ALL
SELECT 'Invoices', COUNT(*) FROM #DemoInvoices UNION ALL
SELECT 'Payments', COUNT(*) FROM #DemoPayments UNION ALL
SELECT 'TenderBids', COUNT(*) FROM #DemoQuotes UNION ALL
SELECT 'Jobs', COUNT(*) FROM #DemoJobs UNION ALL
SELECT 'PurchaseOrders', COUNT(*) FROM #DemoPOs UNION ALL
SELECT 'StockItems', COUNT(*) FROM #DemoStock;

IF @WhatIf = 1 RETURN;

BEGIN TRANSACTION;

IF OBJECT_ID('JobActivityLog','U') IS NOT NULL BEGIN DELETE FROM JobActivityLog WHERE JobId IN (SELECT JobID FROM #DemoJobs); INSERT INTO @Deleted VALUES ('JobActivityLog', @@ROWCOUNT); END
IF OBJECT_ID('JobPartsUsed','U') IS NOT NULL BEGIN DELETE FROM JobPartsUsed WHERE JobId IN (SELECT JobID FROM #DemoJobs) OR InventoryItemId IN (SELECT ItemID FROM #DemoStock); INSERT INTO @Deleted VALUES ('JobPartsUsed', @@ROWCOUNT); END
IF OBJECT_ID('JobChecklistItems','U') IS NOT NULL BEGIN DELETE FROM JobChecklistItems WHERE JobId IN (SELECT JobID FROM #DemoJobs); INSERT INTO @Deleted VALUES ('JobChecklistItems', @@ROWCOUNT); END
IF OBJECT_ID('InvoiceLineItems','U') IS NOT NULL BEGIN DELETE FROM InvoiceLineItems WHERE InvoiceID IN (SELECT InvoiceID FROM #DemoInvoices); INSERT INTO @Deleted VALUES ('InvoiceLineItems', @@ROWCOUNT); END
IF OBJECT_ID('InvoiceInventoryReservations','U') IS NOT NULL BEGIN DELETE FROM InvoiceInventoryReservations WHERE InvoiceID IN (SELECT InvoiceID FROM #DemoInvoices) OR ItemID IN (SELECT ItemID FROM #DemoStock); INSERT INTO @Deleted VALUES ('InvoiceInventoryReservations', @@ROWCOUNT); END
IF OBJECT_ID('TenderBidLineItems','U') IS NOT NULL BEGIN DELETE FROM TenderBidLineItems WHERE TenderBidId IN (SELECT BidID FROM #DemoQuotes) OR InventoryItemId IN (SELECT ItemID FROM #DemoStock); INSERT INTO @Deleted VALUES ('TenderBidLineItems', @@ROWCOUNT); END
IF OBJECT_ID('PurchaseLineItems','U') IS NOT NULL BEGIN DELETE FROM PurchaseLineItems WHERE POID IN (SELECT POID FROM #DemoPOs) OR InventoryItemId IN (SELECT ItemID FROM #DemoStock); INSERT INTO @Deleted VALUES ('PurchaseLineItems', @@ROWCOUNT); END
IF OBJECT_ID('StockMovements','U') IS NOT NULL BEGIN DELETE FROM StockMovements WHERE ItemID IN (SELECT ItemID FROM #DemoStock) OR LOWER(ISNULL(Notes,N'')) LIKE N'%demo%' OR LOWER(ISNULL(Notes,N'')) LIKE N'%test%' OR LOWER(ISNULL(Notes,N'')) LIKE N'%sample%'; INSERT INTO @Deleted VALUES ('StockMovements', @@ROWCOUNT); END
IF OBJECT_ID('SupplierItemPrices','U') IS NOT NULL BEGIN DELETE FROM SupplierItemPrices WHERE VendorID IN (SELECT VendorID FROM #DemoVendors) OR LOWER(ISNULL(Source,N'')) LIKE N'%demo%' OR LOWER(ISNULL(ItemName,N'')) LIKE N'%demo%'; INSERT INTO @Deleted VALUES ('SupplierItemPrices', @@ROWCOUNT); END
IF OBJECT_ID('SLALogs','U') IS NOT NULL BEGIN DELETE FROM SLALogs WHERE ContractID IN (SELECT ContractID FROM #DemoContracts) OR LOWER(ISNULL(Notes,N'')) LIKE N'%demo%' OR LOWER(ISNULL(Notes,N'')) LIKE N'%test%' OR LOWER(ISNULL(Notes,N'')) LIKE N'%sample%'; INSERT INTO @Deleted VALUES ('SLALogs', @@ROWCOUNT); END
IF OBJECT_ID('ServiceDeskNotes','U') IS NOT NULL AND OBJECT_ID('ServiceDeskIncidents','U') IS NOT NULL BEGIN DELETE FROM ServiceDeskNotes WHERE IncidentId IN (SELECT IncidentId FROM ServiceDeskIncidents WHERE ClientID IN (SELECT ClientID FROM #DemoClients) OR SiteID IN (SELECT SiteID FROM #DemoSites) OR LOWER(ISNULL(Description,N'')) LIKE N'%demo%'); INSERT INTO @Deleted VALUES ('ServiceDeskNotes', @@ROWCOUNT); END
IF OBJECT_ID('ServiceDeskIncidents','U') IS NOT NULL BEGIN DELETE FROM ServiceDeskIncidents WHERE ClientID IN (SELECT ClientID FROM #DemoClients) OR SiteID IN (SELECT SiteID FROM #DemoSites) OR AssignedEmployeeId IN (SELECT EmployeeID FROM #DemoEmployees) OR LOWER(ISNULL(Description,N'')) LIKE N'%demo%' OR LOWER(ISNULL(ShortDescription,N'')) LIKE N'%demo%'; INSERT INTO @Deleted VALUES ('ServiceDeskIncidents', @@ROWCOUNT); END
IF OBJECT_ID('StatutoryPayments','U') IS NOT NULL BEGIN DELETE FROM StatutoryPayments WHERE PayrollRunId IN (SELECT PayrollRunId FROM PayrollRuns WHERE LOWER(ISNULL(Notes,N'')) LIKE N'%demo%'); INSERT INTO @Deleted VALUES ('StatutoryPayments', @@ROWCOUNT); END
IF OBJECT_ID('PayrollEntries','U') IS NOT NULL BEGIN DELETE FROM PayrollEntries WHERE EmployeeId IN (SELECT EmployeeID FROM #DemoEmployees) OR PayrollRunId IN (SELECT PayrollRunId FROM PayrollRuns WHERE LOWER(ISNULL(Notes,N'')) LIKE N'%demo%'); INSERT INTO @Deleted VALUES ('PayrollEntries', @@ROWCOUNT); END
IF OBJECT_ID('PayrollRuns','U') IS NOT NULL BEGIN DELETE FROM PayrollRuns WHERE LOWER(ISNULL(Notes,N'')) LIKE N'%demo%' OR LOWER(ISNULL(ProcessedBy,N'')) LIKE N'%demo%'; INSERT INTO @Deleted VALUES ('PayrollRuns', @@ROWCOUNT); END
IF OBJECT_ID('AttendanceRecords','U') IS NOT NULL BEGIN DELETE FROM AttendanceRecords WHERE EmployeeId IN (SELECT EmployeeID FROM #DemoEmployees) OR LOWER(ISNULL(Notes,N'')) LIKE N'%demo%'; INSERT INTO @Deleted VALUES ('AttendanceRecords', @@ROWCOUNT); END

DELETE FROM Payments WHERE PaymentID IN (SELECT PaymentID FROM #DemoPayments); INSERT INTO @Deleted VALUES ('Payments', @@ROWCOUNT);
DELETE FROM Invoices WHERE InvoiceID IN (SELECT InvoiceID FROM #DemoInvoices); INSERT INTO @Deleted VALUES ('Invoices', @@ROWCOUNT);
DELETE FROM Jobs WHERE JobID IN (SELECT JobID FROM #DemoJobs); INSERT INTO @Deleted VALUES ('Jobs', @@ROWCOUNT);
DELETE FROM PurchaseOrders WHERE POID IN (SELECT POID FROM #DemoPOs); INSERT INTO @Deleted VALUES ('PurchaseOrders', @@ROWCOUNT);
DELETE FROM TenderBids WHERE BidID IN (SELECT BidID FROM #DemoQuotes); INSERT INTO @Deleted VALUES ('TenderBids', @@ROWCOUNT);
DELETE FROM AMCContracts WHERE ContractID IN (SELECT ContractID FROM #DemoContracts); INSERT INTO @Deleted VALUES ('AMCContracts', @@ROWCOUNT);
DELETE FROM StockItems WHERE ItemID IN (SELECT ItemID FROM #DemoStock); INSERT INTO @Deleted VALUES ('StockItems', @@ROWCOUNT);
DELETE FROM Employees WHERE EmployeeID IN (SELECT EmployeeID FROM #DemoEmployees); INSERT INTO @Deleted VALUES ('Employees', @@ROWCOUNT);
DELETE FROM ClientSites WHERE SiteID IN (SELECT SiteID FROM #DemoSites); INSERT INTO @Deleted VALUES ('ClientSites', @@ROWCOUNT);
DELETE FROM Vendors WHERE VendorID IN (SELECT VendorID FROM #DemoVendors); INSERT INTO @Deleted VALUES ('Vendors', @@ROWCOUNT);
DELETE FROM B2BClients WHERE ClientID IN (SELECT ClientID FROM #DemoClients); INSERT INTO @Deleted VALUES ('B2BClients', @@ROWCOUNT);

COMMIT TRANSACTION;

SELECT TableName, SUM(RowsDeleted) AS RowsDeleted
FROM @Deleted
GROUP BY TableName
HAVING SUM(RowsDeleted) > 0
ORDER BY TableName;
"@

$result = Invoke-Sql $sql -Timeout 900
$csv = Join-Path $logDir "test-demo-cleanup-$timestamp.csv"
$result | Export-Csv -NoTypeInformation -Path $csv
Write-Log "Cleanup result written: $csv"
if ($WhatIfOnly) {
    Write-Log "WhatIfOnly mode: no rows were deleted."
}
else {
    Write-Log "Selective test/demo cleanup complete."
}
$result | Format-Table -AutoSize
