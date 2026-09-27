using System;
using System.Collections.Generic;
using System.Data.SqlClient;
using System.Linq;
using Dapper;
using HVAC_Pro_Desktop.Models;

namespace HVAC_Pro_Desktop.DAL
{
    public sealed class FinancialReportingRepository
    {
        public void EnsureSchema()
        {
            // Raw ADO.NET/Dapper DDL is intentional: these are additive, guarded schema migrations.
            const string sql = @"
IF OBJECT_ID('dbo.ExpenseCategories', 'U') IS NULL
BEGIN
    CREATE TABLE dbo.ExpenseCategories (
        ExpenseCategoryId INT IDENTITY(1,1) PRIMARY KEY,
        CategoryName NVARCHAR(100) NOT NULL,
        ProfitLossGroup NVARCHAR(30) NOT NULL,
        IsActive BIT NOT NULL CONSTRAINT DF_ExpenseCategories_IsActive DEFAULT(1),
        CONSTRAINT UQ_ExpenseCategories_CategoryName UNIQUE(CategoryName)
    );
END;

IF OBJECT_ID('dbo.ExpenseEntries', 'U') IS NULL
BEGIN
    CREATE TABLE dbo.ExpenseEntries (
        ExpenseEntryId INT IDENTITY(1,1) PRIMARY KEY,
        ExpenseCategoryId INT NOT NULL REFERENCES dbo.ExpenseCategories(ExpenseCategoryId),
        JobId INT NULL REFERENCES dbo.Jobs(JobID),
        ClientId INT NULL,
        SiteId INT NULL,
        ExpenseDate DATETIME NOT NULL,
        Amount DECIMAL(18,2) NOT NULL,
        ReferenceNumber NVARCHAR(100) NULL,
        Description NVARCHAR(500) NOT NULL,
        CreatedByUserId INT NULL,
        CreatedByName NVARCHAR(100) NULL,
        CreatedDate DATETIME NOT NULL CONSTRAINT DF_ExpenseEntries_CreatedDate DEFAULT(GETDATE())
    );
    CREATE INDEX IX_ExpenseEntries_DateCategory ON dbo.ExpenseEntries(ExpenseDate, ExpenseCategoryId);
    CREATE INDEX IX_ExpenseEntries_JobId ON dbo.ExpenseEntries(JobId) WHERE JobId IS NOT NULL;
END;

IF OBJECT_ID('dbo.ProfitabilityImportBatches', 'U') IS NULL
BEGIN
    CREATE TABLE dbo.ProfitabilityImportBatches (
        ImportBatchId INT IDENTITY(1,1) PRIMARY KEY,
        SourceFileName NVARCHAR(260) NOT NULL,
        WorksheetName NVARCHAR(128) NOT NULL,
        ImportedByUserId INT NULL,
        ImportedByName NVARCHAR(100) NULL,
        ImportedDate DATETIME NOT NULL CONSTRAINT DF_ProfitabilityImportBatches_ImportedDate DEFAULT(GETDATE()),
        SourceRowCount INT NOT NULL DEFAULT(0),
        MatchedRowCount INT NOT NULL DEFAULT(0),
        ReviewRowCount INT NOT NULL DEFAULT(0)
    );
END;

IF OBJECT_ID('dbo.ProfitabilityImportRows', 'U') IS NULL
BEGIN
    CREATE TABLE dbo.ProfitabilityImportRows (
        ImportRowId INT IDENTITY(1,1) PRIMARY KEY,
        ImportBatchId INT NOT NULL REFERENCES dbo.ProfitabilityImportBatches(ImportBatchId),
        SourceRowNumber INT NOT NULL,
        CustomerName NVARCHAR(255) NULL,
        Description NVARCHAR(1000) NULL,
        QuotationNumber NVARCHAR(100) NULL,
        QuotationAmount DECIMAL(18,2) NULL,
        CustomerPoNumber NVARCHAR(100) NULL,
        CustomerPoAmount DECIMAL(18,2) NULL,
        InvoiceNumber NVARCHAR(100) NULL,
        InvoiceDate DATETIME NULL,
        TaxableRevenue DECIMAL(18,2) NULL,
        VendorName NVARCHAR(255) NULL,
        VendorCost DECIMAL(18,2) NULL,
        WorkStatus NVARCHAR(50) NULL,
        PaymentStatus NVARCHAR(50) NULL,
        MatchedInvoiceId INT NULL,
        MatchedJobId INT NULL,
        ReviewStatus NVARCHAR(30) NOT NULL,
        ReviewMessage NVARCHAR(1000) NULL
    );
    CREATE INDEX IX_ProfitabilityImportRows_BatchStatus ON dbo.ProfitabilityImportRows(ImportBatchId, ReviewStatus);
END;

IF COL_LENGTH('dbo.InvoiceLineItems', 'JobID') IS NULL
    ALTER TABLE dbo.InvoiceLineItems ADD JobID INT NULL;

IF COL_LENGTH('dbo.PurchaseOrders', 'FinancialCategory') IS NULL
    ALTER TABLE dbo.PurchaseOrders ADD FinancialCategory NVARCHAR(30) NOT NULL
        CONSTRAINT DF_PurchaseOrders_FinancialCategory DEFAULT('Direct Cost') WITH VALUES;

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_InvoiceLineItems_JobID' AND object_id = OBJECT_ID('dbo.InvoiceLineItems'))
    EXEC(N'CREATE INDEX IX_InvoiceLineItems_JobID ON dbo.InvoiceLineItems(JobID) WHERE JobID IS NOT NULL');

MERGE dbo.ExpenseCategories AS target
USING (VALUES
    (N'Direct labour', N'Direct Cost'),
    (N'Job travel', N'Direct Cost'),
    (N'Other direct cost', N'Direct Cost'),
    (N'Rent', N'Operating Expense'),
    (N'Utilities', N'Operating Expense'),
    (N'Administration', N'Operating Expense'),
    (N'Marketing', N'Operating Expense'),
    (N'Insurance', N'Operating Expense'),
    (N'Depreciation', N'Operating Expense'),
    (N'Finance cost', N'Operating Expense'),
    (N'Other operating expense', N'Operating Expense')
) AS source(CategoryName, ProfitLossGroup)
ON target.CategoryName = source.CategoryName
WHEN NOT MATCHED THEN INSERT(CategoryName, ProfitLossGroup) VALUES(source.CategoryName, source.ProfitLossGroup);";

            using (SqlConnection connection = DapperDatabase.CreateConnection())
            {
                DatabaseConnectionFactory.Open(connection, "FinancialReportingRepository.EnsureSchema");
                connection.Execute(sql, commandTimeout: 60);
            }
        }

        public List<JobProfitabilityRow> GetJobProfitability(DateTime from, DateTime to)
        {
            const string sql = @"
SELECT
    j.JobID AS JobId,
    j.JobNumber,
    COALESCE(NULLIF(j.JobTitle, ''), NULLIF(j.Title, ''), j.Description) AS JobTitle,
    c.CompanyName AS ClientName,
    s.SiteName,
    COALESCE(NULLIF(j.PipelineStatus, ''), j.Status) AS JobStatus,
    COALESCE(i.InvoiceDate, j.CompletedDate, j.ScheduledDate) AS ReportingDate,
    COALESCE(NULLIF(j.QuotedRevenue, 0), j.Revenue, 0) AS QuotedRevenue,
    COALESCE(lineRevenue.Amount, NULLIF(i.SubTotal, 0), NULLIF(j.ActualRevenue, 0), 0) AS BilledRevenue,
    COALESCE(vendorCost.Amount, 0) AS CommittedVendorCost,
    COALESCE(partsCost.Amount, 0) AS MaterialCost,
    COALESCE(directCosts.LabourCost, 0) AS LabourCost,
    COALESCE(directCosts.TravelCost, 0) AS TravelCost,
    COALESCE(directCosts.OtherDirectCost, 0) AS OtherDirectCost,
    COALESCE(vendorCost.Amount, 0) + COALESCE(partsCost.Amount, 0) + COALESCE(directCosts.TotalDirectCost, 0) AS ActualDirectCost,
    COALESCE(i.PaidAmount, 0) AS AmountCollected,
    COALESCE(i.BalanceDue, 0) AS OutstandingAmount,
    i.InvoiceNumber
FROM dbo.Jobs j
LEFT JOIN dbo.B2BClients c ON c.ClientID = j.ClientID
LEFT JOIN dbo.ClientSites s ON s.SiteID = j.SiteID
LEFT JOIN dbo.Invoices i ON i.InvoiceID = j.InvoiceId
OUTER APPLY (
    SELECT SUM(li.Amount) AS Amount
    FROM dbo.InvoiceLineItems li
    INNER JOIN dbo.Invoices linkedInvoice ON linkedInvoice.InvoiceID = li.InvoiceID
    WHERE li.JobID = j.JobID
      AND linkedInvoice.PaymentStatus <> 'Cancelled'
) lineRevenue
OUTER APPLY (
    SELECT SUM(pli.Amount) AS Amount
    FROM dbo.PurchaseLineItems pli
    INNER JOIN dbo.PurchaseOrders po ON po.POID = pli.POID
    WHERE pli.LinkedWorkOrderId = j.JobID
      AND po.Status <> 'Cancelled'
) vendorCost
OUTER APPLY (
    SELECT SUM(jpu.TotalCost) AS Amount
    FROM dbo.JobPartsUsed jpu
    WHERE jpu.JobId = j.JobID AND jpu.LinkedPoId IS NULL
) partsCost
OUTER APPLY (
    SELECT
        SUM(CASE WHEN ec.CategoryName = 'Direct labour' THEN ee.Amount ELSE 0 END) AS LabourCost,
        SUM(CASE WHEN ec.CategoryName = 'Job travel' THEN ee.Amount ELSE 0 END) AS TravelCost,
        SUM(CASE WHEN ec.CategoryName NOT IN ('Direct labour', 'Job travel') THEN ee.Amount ELSE 0 END) AS OtherDirectCost,
        SUM(ee.Amount) AS TotalDirectCost
    FROM dbo.ExpenseEntries ee
    INNER JOIN dbo.ExpenseCategories ec ON ec.ExpenseCategoryId = ee.ExpenseCategoryId
    WHERE ee.JobId = j.JobID AND ec.ProfitLossGroup = 'Direct Cost'
) directCosts
WHERE COALESCE(i.InvoiceDate, j.CompletedDate, j.ScheduledDate) >= @From
  AND COALESCE(i.InvoiceDate, j.CompletedDate, j.ScheduledDate) < @ToExclusive
ORDER BY ReportingDate DESC, j.JobID DESC;";

            using (SqlConnection connection = DapperDatabase.CreateConnection())
            {
                DatabaseConnectionFactory.Open(connection, "FinancialReportingRepository.GetJobProfitability");
                return connection.Query<JobProfitabilityRow>(sql, new { From = from.Date, ToExclusive = to.Date.AddDays(1) }).ToList();
            }
        }

        public List<MonthlyProfitLossRow> GetMonthlyProfitLoss(DateTime firstMonth, int monthCount)
        {
            const string sql = @"
;WITH Months AS (
    SELECT 0 AS OffsetNo, CAST(@FirstMonth AS DATE) AS MonthStart
    UNION ALL
    SELECT OffsetNo + 1, DATEADD(MONTH, 1, MonthStart)
    FROM Months WHERE OffsetNo + 1 < @MonthCount
)
SELECT
    m.MonthStart AS Month,
    COALESCE(revenue.Amount, 0) AS Revenue,
    COALESCE(directPurchases.Amount, 0) + COALESCE(inventoryCost.Amount, 0) + COALESCE(manualDirect.Amount, 0) AS DirectCosts,
    COALESCE(payroll.Amount, 0) AS PayrollExpense,
    COALESCE(generalPurchases.Amount, 0) + COALESCE(manualOpex.Amount, 0) AS OperatingExpenses
FROM Months m
OUTER APPLY (
    SELECT SUM(i.SubTotal) AS Amount FROM dbo.Invoices i
    WHERE i.InvoiceDate >= m.MonthStart AND i.InvoiceDate < DATEADD(MONTH, 1, m.MonthStart)
      AND i.PaymentStatus <> 'Cancelled'
) revenue
OUTER APPLY (
    SELECT SUM(pli.Amount) AS Amount
    FROM dbo.PurchaseLineItems pli INNER JOIN dbo.PurchaseOrders po ON po.POID = pli.POID
    WHERE pli.LinkedWorkOrderId IS NOT NULL AND po.Status <> 'Cancelled'
      AND po.PODate >= m.MonthStart AND po.PODate < DATEADD(MONTH, 1, m.MonthStart)
) directPurchases
OUTER APPLY (
    SELECT SUM(jpu.TotalCost) AS Amount
    FROM dbo.JobPartsUsed jpu INNER JOIN dbo.Jobs j ON j.JobID = jpu.JobId
    WHERE jpu.LinkedPoId IS NULL
      AND COALESCE(j.CompletedDate, j.ScheduledDate) >= m.MonthStart
      AND COALESCE(j.CompletedDate, j.ScheduledDate) < DATEADD(MONTH, 1, m.MonthStart)
) inventoryCost
OUTER APPLY (
    SELECT SUM(ee.Amount) AS Amount
    FROM dbo.ExpenseEntries ee INNER JOIN dbo.ExpenseCategories ec ON ec.ExpenseCategoryId = ee.ExpenseCategoryId
    WHERE ec.ProfitLossGroup = 'Direct Cost'
      AND ee.ExpenseDate >= m.MonthStart AND ee.ExpenseDate < DATEADD(MONTH, 1, m.MonthStart)
) manualDirect
OUTER APPLY (
    SELECT SUM(pr.TotalGross + pr.TotalEPFEmployer + pr.TotalESIEmployer) AS Amount
    FROM dbo.PayrollRuns pr WHERE pr.PayrollMonth = MONTH(m.MonthStart) AND pr.PayrollYear = YEAR(m.MonthStart)
      AND pr.Status IN ('Completed', 'Locked')
) payroll
OUTER APPLY (
    SELECT SUM(pli.Amount) AS Amount
    FROM dbo.PurchaseLineItems pli INNER JOIN dbo.PurchaseOrders po ON po.POID = pli.POID
    WHERE pli.LinkedWorkOrderId IS NULL AND po.Status <> 'Cancelled'
      AND po.PODate >= m.MonthStart AND po.PODate < DATEADD(MONTH, 1, m.MonthStart)
) generalPurchases
OUTER APPLY (
    SELECT SUM(ee.Amount) AS Amount
    FROM dbo.ExpenseEntries ee INNER JOIN dbo.ExpenseCategories ec ON ec.ExpenseCategoryId = ee.ExpenseCategoryId
    WHERE ec.ProfitLossGroup = 'Operating Expense'
      AND ee.ExpenseDate >= m.MonthStart AND ee.ExpenseDate < DATEADD(MONTH, 1, m.MonthStart)
) manualOpex
ORDER BY m.MonthStart OPTION (MAXRECURSION 120);";

            using (SqlConnection connection = DapperDatabase.CreateConnection())
            {
                DatabaseConnectionFactory.Open(connection, "FinancialReportingRepository.GetMonthlyProfitLoss");
                return connection.Query<MonthlyProfitLossRow>(sql, new { FirstMonth = firstMonth.Date, MonthCount = Math.Max(1, monthCount) }).ToList();
            }
        }

        public List<ExpenseCategory> GetExpenseCategories()
        {
            using (SqlConnection connection = DapperDatabase.CreateConnection())
            {
                DatabaseConnectionFactory.Open(connection, "FinancialReportingRepository.GetExpenseCategories");
                return connection.Query<ExpenseCategory>("SELECT ExpenseCategoryId, CategoryName, ProfitLossGroup, IsActive FROM dbo.ExpenseCategories WHERE IsActive = 1 ORDER BY ProfitLossGroup, CategoryName").ToList();
            }
        }

        public int AddExpense(ExpenseEntry entry, int? userId)
        {
            const string sql = @"
INSERT dbo.ExpenseEntries(ExpenseCategoryId, JobId, ClientId, SiteId, ExpenseDate, Amount, ReferenceNumber, Description, CreatedByUserId, CreatedByName)
VALUES(@ExpenseCategoryId, @JobId, @ClientId, @SiteId, @ExpenseDate, @Amount, @ReferenceNumber, @Description, @UserId, @CreatedByName);
SELECT CAST(SCOPE_IDENTITY() AS INT);";
            using (SqlConnection connection = DapperDatabase.CreateConnection())
            {
                DatabaseConnectionFactory.Open(connection, "FinancialReportingRepository.AddExpense");
                return connection.QuerySingle<int>(sql, new
                {
                    entry.ExpenseCategoryId,
                    entry.JobId,
                    entry.ClientId,
                    entry.SiteId,
                    entry.ExpenseDate,
                    entry.Amount,
                    entry.ReferenceNumber,
                    entry.Description,
                    UserId = userId,
                    entry.CreatedByName
                });
            }
        }

        public int StageImport(ProfitabilityImportPreview preview, int? userId, string userName)
        {
            const string batchSql = @"
INSERT dbo.ProfitabilityImportBatches(SourceFileName, WorksheetName, ImportedByUserId, ImportedByName, SourceRowCount, MatchedRowCount, ReviewRowCount)
VALUES(@SourceFileName, @WorksheetName, @UserId, @UserName, @SourceRowCount, @MatchedRowCount, @ReviewRowCount);
SELECT CAST(SCOPE_IDENTITY() AS INT);";
            const string rowSql = @"
INSERT dbo.ProfitabilityImportRows(ImportBatchId, SourceRowNumber, CustomerName, Description, QuotationNumber, QuotationAmount, CustomerPoNumber, CustomerPoAmount, InvoiceNumber, InvoiceDate, TaxableRevenue, VendorName, VendorCost, WorkStatus, PaymentStatus, MatchedInvoiceId, MatchedJobId, ReviewStatus, ReviewMessage)
VALUES(@ImportBatchId, @SourceRowNumber, @CustomerName, @Description, @QuotationNumber, @QuotationAmount, @CustomerPoNumber, @CustomerPoAmount, @InvoiceNumber, @InvoiceDate, @TaxableRevenue, @VendorName, @VendorCost, @WorkStatus, @PaymentStatus, @MatchedInvoiceId, @MatchedJobId, @ReviewStatus, @ReviewMessage);";

            using (SqlConnection connection = DapperDatabase.CreateConnection())
            {
                DatabaseConnectionFactory.Open(connection, "FinancialReportingRepository.StageImport");
                using (SqlTransaction transaction = connection.BeginTransaction())
                {
                    int batchId = connection.QuerySingle<int>(batchSql, new
                    {
                        preview.SourceFileName,
                        preview.WorksheetName,
                        UserId = userId,
                        UserName = userName,
                        SourceRowCount = preview.Rows.Count,
                        preview.MatchedRowCount,
                        preview.ReviewRowCount
                    }, transaction);
                    foreach (ProfitabilityImportRow row in preview.Rows)
                    {
                        connection.Execute(rowSql, new
                        {
                            ImportBatchId = batchId,
                            row.SourceRowNumber,
                            row.CustomerName,
                            row.Description,
                            row.QuotationNumber,
                            row.QuotationAmount,
                            row.CustomerPoNumber,
                            row.CustomerPoAmount,
                            row.InvoiceNumber,
                            row.InvoiceDate,
                            row.TaxableRevenue,
                            row.VendorName,
                            row.VendorCost,
                            row.WorkStatus,
                            row.PaymentStatus,
                            row.MatchedInvoiceId,
                            row.MatchedJobId,
                            row.ReviewStatus,
                            row.ReviewMessage
                        }, transaction);
                    }
                    transaction.Commit();
                    return batchId;
                }
            }
        }

        public List<ProfitabilityImportRow> GetLatestImportRows()
        {
            const string sql = @"
DECLARE @BatchId INT = (SELECT MAX(ImportBatchId) FROM dbo.ProfitabilityImportBatches);
SELECT SourceRowNumber, CustomerName, Description, QuotationNumber, QuotationAmount,
       CustomerPoNumber, CustomerPoAmount, InvoiceNumber, InvoiceDate, TaxableRevenue,
       VendorName, VendorCost, WorkStatus, PaymentStatus, MatchedInvoiceId, MatchedJobId,
       ReviewStatus, ReviewMessage
FROM dbo.ProfitabilityImportRows
WHERE ImportBatchId = @BatchId
ORDER BY CASE WHEN ReviewStatus = 'Review' THEN 0 ELSE 1 END, SourceRowNumber;";
            using (SqlConnection connection = DapperDatabase.CreateConnection())
            {
                DatabaseConnectionFactory.Open(connection, "FinancialReportingRepository.GetLatestImportRows");
                return connection.Query<ProfitabilityImportRow>(sql).ToList();
            }
        }
    }
}
