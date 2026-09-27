/* ServoERP job/site profitability, reviewed workbook import, and company P&L.
   Additive migration only. Existing business tables and columns are preserved. */

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
