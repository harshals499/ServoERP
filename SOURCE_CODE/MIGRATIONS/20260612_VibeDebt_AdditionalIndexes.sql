IF OBJECT_ID('dbo.Payments', 'U') IS NOT NULL
BEGIN
    IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_Payments_InvoiceID_PaymentDate' AND object_id = OBJECT_ID('dbo.Payments'))
        CREATE INDEX IX_Payments_InvoiceID_PaymentDate ON dbo.Payments (InvoiceID, PaymentDate DESC);

    IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_Payments_ClientID_PaymentDate' AND object_id = OBJECT_ID('dbo.Payments'))
        CREATE INDEX IX_Payments_ClientID_PaymentDate ON dbo.Payments (ClientID, PaymentDate DESC);

    IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_Payments_ReferenceNumber' AND object_id = OBJECT_ID('dbo.Payments'))
        CREATE INDEX IX_Payments_ReferenceNumber ON dbo.Payments (ReferenceNumber);
END;

IF OBJECT_ID('dbo.Quotations', 'U') IS NOT NULL
BEGIN
    IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_Quotations_QuotationNumber' AND object_id = OBJECT_ID('dbo.Quotations'))
        CREATE INDEX IX_Quotations_QuotationNumber ON dbo.Quotations (QuotationNumber);

    IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_Quotations_Status_RequiredByDate' AND object_id = OBJECT_ID('dbo.Quotations'))
        CREATE INDEX IX_Quotations_Status_RequiredByDate ON dbo.Quotations (Status, RequiredByDate);
END;

IF OBJECT_ID('dbo.ClientSites', 'U') IS NOT NULL
BEGIN
    IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_ClientSites_ClientID_SiteName' AND object_id = OBJECT_ID('dbo.ClientSites'))
        CREATE INDEX IX_ClientSites_ClientID_SiteName ON dbo.ClientSites (ClientID, SiteName);
END;

IF OBJECT_ID('dbo.Jobs', 'U') IS NOT NULL
BEGIN
    IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_Jobs_AssignedEmployeeID_ScheduledDate' AND object_id = OBJECT_ID('dbo.Jobs'))
        CREATE INDEX IX_Jobs_AssignedEmployeeID_ScheduledDate ON dbo.Jobs (AssignedEmployeeID, ScheduledDate);
END;
