-- Disposable QA fixture only. This is the existing desktop Payments definition;
-- it supplies an early dependency before running the full, unchanged initializer.
IF DB_NAME() <> N'ServoERP_MarathiQA_20261107_PreviewOnly'
    THROW 51000, 'Disposable Marathi preview database required', 1;
IF OBJECT_ID(N'dbo.Invoices', N'U') IS NULL OR OBJECT_ID(N'dbo.B2BClients', N'U') IS NULL
    THROW 51000, 'Run the desktop base schema in the QA database first', 1;
IF OBJECT_ID(N'dbo.Payments', N'U') IS NULL
CREATE TABLE dbo.Payments (
    PaymentID INT PRIMARY KEY IDENTITY(1,1),
    PaymentNumber NVARCHAR(50) NOT NULL,
    InvoiceID INT NOT NULL FOREIGN KEY REFERENCES dbo.Invoices(InvoiceID),
    ClientID INT NOT NULL FOREIGN KEY REFERENCES dbo.B2BClients(ClientID),
    AmountPaid DECIMAL(12,2) NOT NULL,
    PaymentDate DATETIME NOT NULL,
    PaymentMode NVARCHAR(50) NOT NULL DEFAULT 'Bank Transfer',
    ReferenceNumber NVARCHAR(100) NULL,
    Notes NVARCHAR(MAX) NULL,
    CreatedDate DATETIME NOT NULL DEFAULT GETDATE()
);
