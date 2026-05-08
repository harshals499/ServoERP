-- Purchase Order schema for the HVAC PRO backend module.
-- This mirrors the live SQL Server tables used by the desktop app.
-- If you want PostgreSQL, keep the same column names and types with the
-- obvious dialect changes for identity, defaults, and filtered indexes.

SET ANSI_NULLS ON;
SET QUOTED_IDENTIFIER ON;

IF OBJECT_ID('Vendors', 'U') IS NULL
BEGIN
  CREATE TABLE Vendors (
    VendorID INT IDENTITY(1,1) PRIMARY KEY,
    VendorName NVARCHAR(255) NOT NULL,
    GSTNumber NVARCHAR(20) NULL,
    DefaultCreditDays INT NOT NULL CONSTRAINT DF_Vendors_DefaultCreditDays DEFAULT 30,
    PANNumber NVARCHAR(20) NULL,
    Phone NVARCHAR(20) NULL,
    Email NVARCHAR(255) NULL,
    Address NVARCHAR(MAX) NULL,
    City NVARCHAR(100) NULL,
    Category NVARCHAR(100) NULL,
    IsActive BIT NOT NULL CONSTRAINT DF_Vendors_IsActive DEFAULT 1,
    CreatedDate DATETIME NOT NULL CONSTRAINT DF_Vendors_CreatedDate DEFAULT GETDATE()
  );
END

IF OBJECT_ID('PurchaseOrders', 'U') IS NULL
BEGIN
  CREATE TABLE PurchaseOrders (
    POID INT IDENTITY(1,1) PRIMARY KEY,
    VendorID INT NOT NULL,
    ClientID INT NULL,
    SiteID INT NULL,
    RelatedContractID INT NULL,
    RecommendedByBidID INT NULL,
    PONumber NVARCHAR(100) NOT NULL,
    PODate DATETIME NOT NULL,
    PayByDate DATETIME NOT NULL,
    VendorInvoiceNumber NVARCHAR(100) NULL,
    LinkedToType NVARCHAR(30) NULL,
    LinkedToId INT NULL,
    DeliveryMode NVARCHAR(30) NOT NULL CONSTRAINT DF_PurchaseOrders_DeliveryMode DEFAULT('TechPickup'),
    AssignedTechnicianId INT NULL,
    AssignedTechnicianName NVARCHAR(100) NULL,
    DeliveryAddress NVARCHAR(500) NULL,
    AddToClientInvoice BIT NOT NULL CONSTRAINT DF_PurchaseOrders_AddToClientInvoice DEFAULT(0),
    PendingChargeCreated BIT NOT NULL CONSTRAINT DF_PurchaseOrders_PendingChargeCreated DEFAULT(0),
    ReceiptImagePath NVARCHAR(500) NULL,
    PriceVarianceFlag BIT NOT NULL CONSTRAINT DF_PurchaseOrders_PriceVarianceFlag DEFAULT(0),
    CreatedByUserId INT NULL,
    CreatedByName NVARCHAR(100) NULL,
    CreatedByDate DATETIME NULL CONSTRAINT DF_PurchaseOrders_CreatedByDate DEFAULT(GETDATE()),
    TotalAmount DECIMAL(18,2) NOT NULL CONSTRAINT DF_PurchaseOrders_TotalAmount DEFAULT(0),
    PaidAmount DECIMAL(18,2) NOT NULL CONSTRAINT DF_PurchaseOrders_PaidAmount DEFAULT(0),
    Status NVARCHAR(50) NOT NULL CONSTRAINT DF_PurchaseOrders_Status DEFAULT('Pending'),
    PaymentReference NVARCHAR(100) NULL,
    ComparisonNotes NVARCHAR(MAX) NULL,
    Notes NVARCHAR(MAX) NULL,
    CreatedDate DATETIME NOT NULL CONSTRAINT DF_PurchaseOrders_CreatedDate DEFAULT GETDATE()
  );
END

IF OBJECT_ID('PurchaseLineItems', 'U') IS NULL
BEGIN
  CREATE TABLE PurchaseLineItems (
    LineItemID INT IDENTITY(1,1) PRIMARY KEY,
    POID INT NOT NULL,
    InventoryItemId INT NULL,
    Description NVARCHAR(500) NOT NULL,
    HsnSacCode NVARCHAR(50) NULL,
    Quantity DECIMAL(18,2) NOT NULL CONSTRAINT DF_PurchaseLineItems_Quantity DEFAULT(1),
    UOM NVARCHAR(30) NOT NULL CONSTRAINT DF_PurchaseLineItems_UOM DEFAULT('Nos'),
    Rate DECIMAL(18,2) NOT NULL CONSTRAINT DF_PurchaseLineItems_Rate DEFAULT(0),
    GSTRate DECIMAL(5,2) NOT NULL CONSTRAINT DF_PurchaseLineItems_GSTRate DEFAULT(0),
    CGSTRate DECIMAL(5,2) NOT NULL CONSTRAINT DF_PurchaseLineItems_CGSTRate DEFAULT(0),
    SGSTRate DECIMAL(5,2) NOT NULL CONSTRAINT DF_PurchaseLineItems_SGSTRate DEFAULT(0),
    IGSTRate DECIMAL(5,2) NOT NULL CONSTRAINT DF_PurchaseLineItems_IGSTRate DEFAULT(0),
    JobLink NVARCHAR(50) NOT NULL CONSTRAINT DF_PurchaseLineItems_JobLink DEFAULT('General'),
    LinkedWorkOrderId INT NULL,
    LinkedWorkOrderName NVARCHAR(200) NULL,
    PriceVariance DECIMAL(5,2) NOT NULL CONSTRAINT DF_PurchaseLineItems_PriceVariance DEFAULT(0),
    HistoricalRate DECIMAL(18,2) NOT NULL CONSTRAINT DF_PurchaseLineItems_HistoricalRate DEFAULT(0),
    Amount DECIMAL(18,2) NOT NULL CONSTRAINT DF_PurchaseLineItems_Amount DEFAULT(0)
  );
END

IF OBJECT_ID('PurchaseOrderImportLog', 'U') IS NULL
BEGIN
  CREATE TABLE PurchaseOrderImportLog (
    ImportID INT IDENTITY(1,1) PRIMARY KEY,
    SourceFileName NVARCHAR(260) NOT NULL,
    SourceFilePath NVARCHAR(500) NOT NULL,
    PONumber NVARCHAR(100) NULL,
    Status NVARCHAR(50) NOT NULL,
    Message NVARCHAR(MAX) NULL,
    RawText NVARCHAR(MAX) NULL,
    CreatedDate DATETIME NOT NULL CONSTRAINT DF_PurchaseOrderImportLog_CreatedDate DEFAULT(GETDATE())
  );
END

IF COL_LENGTH('Vendors', 'DefaultCreditDays') IS NULL
  ALTER TABLE Vendors ADD DefaultCreditDays INT NOT NULL CONSTRAINT DF_Vendors_DefaultCreditDays DEFAULT 30;

IF COL_LENGTH('PurchaseOrders', 'VendorInvoiceNumber') IS NULL
  ALTER TABLE PurchaseOrders ADD VendorInvoiceNumber NVARCHAR(100) NULL;

IF COL_LENGTH('PurchaseOrders', 'LinkedToType') IS NULL
  ALTER TABLE PurchaseOrders ADD LinkedToType NVARCHAR(30) NULL;

IF COL_LENGTH('PurchaseOrders', 'LinkedToId') IS NULL
  ALTER TABLE PurchaseOrders ADD LinkedToId INT NULL;

IF COL_LENGTH('PurchaseOrders', 'DeliveryMode') IS NULL
  ALTER TABLE PurchaseOrders ADD DeliveryMode NVARCHAR(30) NOT NULL CONSTRAINT DF_PurchaseOrders_DeliveryMode DEFAULT('TechPickup');

IF COL_LENGTH('PurchaseOrders', 'AssignedTechnicianId') IS NULL
  ALTER TABLE PurchaseOrders ADD AssignedTechnicianId INT NULL;

IF COL_LENGTH('PurchaseOrders', 'AssignedTechnicianName') IS NULL
  ALTER TABLE PurchaseOrders ADD AssignedTechnicianName NVARCHAR(100) NULL;

IF COL_LENGTH('PurchaseOrders', 'DeliveryAddress') IS NULL
  ALTER TABLE PurchaseOrders ADD DeliveryAddress NVARCHAR(500) NULL;

IF COL_LENGTH('PurchaseOrders', 'AddToClientInvoice') IS NULL
  ALTER TABLE PurchaseOrders ADD AddToClientInvoice BIT NOT NULL CONSTRAINT DF_PurchaseOrders_AddToClientInvoice DEFAULT(0);

IF COL_LENGTH('PurchaseOrders', 'PendingChargeCreated') IS NULL
  ALTER TABLE PurchaseOrders ADD PendingChargeCreated BIT NOT NULL CONSTRAINT DF_PurchaseOrders_PendingChargeCreated DEFAULT(0);

IF COL_LENGTH('PurchaseOrders', 'ReceiptImagePath') IS NULL
  ALTER TABLE PurchaseOrders ADD ReceiptImagePath NVARCHAR(500) NULL;

IF COL_LENGTH('PurchaseOrders', 'PriceVarianceFlag') IS NULL
  ALTER TABLE PurchaseOrders ADD PriceVarianceFlag BIT NOT NULL CONSTRAINT DF_PurchaseOrders_PriceVarianceFlag DEFAULT(0);

IF COL_LENGTH('PurchaseOrders', 'CreatedByUserId') IS NULL
  ALTER TABLE PurchaseOrders ADD CreatedByUserId INT NULL;

IF COL_LENGTH('PurchaseOrders', 'CreatedByName') IS NULL
  ALTER TABLE PurchaseOrders ADD CreatedByName NVARCHAR(100) NULL;

IF COL_LENGTH('PurchaseOrders', 'CreatedByDate') IS NULL
  ALTER TABLE PurchaseOrders ADD CreatedByDate DATETIME NULL CONSTRAINT DF_PurchaseOrders_CreatedByDate DEFAULT(GETDATE());

IF COL_LENGTH('PurchaseOrders', 'PaymentReference') IS NULL
  ALTER TABLE PurchaseOrders ADD PaymentReference NVARCHAR(100) NULL;

IF COL_LENGTH('PurchaseOrders', 'ComparisonNotes') IS NULL
  ALTER TABLE PurchaseOrders ADD ComparisonNotes NVARCHAR(MAX) NULL;

IF COL_LENGTH('PurchaseOrders', 'Notes') IS NULL
  ALTER TABLE PurchaseOrders ADD Notes NVARCHAR(MAX) NULL;

IF COL_LENGTH('PurchaseOrders', 'Status') IS NULL
  ALTER TABLE PurchaseOrders ADD Status NVARCHAR(50) NOT NULL CONSTRAINT DF_PurchaseOrders_Status DEFAULT('Pending');

IF COL_LENGTH('PurchaseOrders', 'PayByDate') IS NULL
  ALTER TABLE PurchaseOrders ADD PayByDate DATETIME NOT NULL CONSTRAINT DF_PurchaseOrders_PayByDate DEFAULT(GETDATE());

IF COL_LENGTH('PurchaseLineItems', 'InventoryItemId') IS NULL
  ALTER TABLE PurchaseLineItems ADD InventoryItemId INT NULL;

IF COL_LENGTH('PurchaseLineItems', 'Description') IS NULL
  ALTER TABLE PurchaseLineItems ADD Description NVARCHAR(500) NULL;

IF COL_LENGTH('PurchaseLineItems', 'HsnSacCode') IS NULL
  ALTER TABLE PurchaseLineItems ADD HsnSacCode NVARCHAR(50) NULL;

IF COL_LENGTH('PurchaseLineItems', 'UOM') IS NULL
  ALTER TABLE PurchaseLineItems ADD UOM NVARCHAR(30) NOT NULL CONSTRAINT DF_PurchaseLineItems_UOM DEFAULT('Nos');

IF COL_LENGTH('PurchaseLineItems', 'GSTRate') IS NULL
  ALTER TABLE PurchaseLineItems ADD GSTRate DECIMAL(5,2) NOT NULL CONSTRAINT DF_PurchaseLineItems_GSTRate DEFAULT(0);

IF COL_LENGTH('PurchaseLineItems', 'CGSTRate') IS NULL
  ALTER TABLE PurchaseLineItems ADD CGSTRate DECIMAL(5,2) NOT NULL CONSTRAINT DF_PurchaseLineItems_CGSTRate DEFAULT(0);

IF COL_LENGTH('PurchaseLineItems', 'SGSTRate') IS NULL
  ALTER TABLE PurchaseLineItems ADD SGSTRate DECIMAL(5,2) NOT NULL CONSTRAINT DF_PurchaseLineItems_SGSTRate DEFAULT(0);

IF COL_LENGTH('PurchaseLineItems', 'IGSTRate') IS NULL
  ALTER TABLE PurchaseLineItems ADD IGSTRate DECIMAL(5,2) NOT NULL CONSTRAINT DF_PurchaseLineItems_IGSTRate DEFAULT(0);

IF COL_LENGTH('PurchaseLineItems', 'JobLink') IS NULL
  ALTER TABLE PurchaseLineItems ADD JobLink NVARCHAR(50) NOT NULL CONSTRAINT DF_PurchaseLineItems_JobLink DEFAULT('General');

IF COL_LENGTH('PurchaseLineItems', 'LinkedWorkOrderId') IS NULL
  ALTER TABLE PurchaseLineItems ADD LinkedWorkOrderId INT NULL;

IF COL_LENGTH('PurchaseLineItems', 'LinkedWorkOrderName') IS NULL
  ALTER TABLE PurchaseLineItems ADD LinkedWorkOrderName NVARCHAR(200) NULL;

IF COL_LENGTH('PurchaseLineItems', 'PriceVariance') IS NULL
  ALTER TABLE PurchaseLineItems ADD PriceVariance DECIMAL(5,2) NOT NULL CONSTRAINT DF_PurchaseLineItems_PriceVariance DEFAULT(0);

IF COL_LENGTH('PurchaseLineItems', 'HistoricalRate') IS NULL
  ALTER TABLE PurchaseLineItems ADD HistoricalRate DECIMAL(18,2) NOT NULL CONSTRAINT DF_PurchaseLineItems_HistoricalRate DEFAULT(0);
