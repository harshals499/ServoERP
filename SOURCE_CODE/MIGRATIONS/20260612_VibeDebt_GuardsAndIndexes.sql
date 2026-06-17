IF OBJECT_ID(N'dbo.AppRoles', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.AppRoles (
        RoleId INT IDENTITY(1,1) PRIMARY KEY,
        RoleName NVARCHAR(50) NOT NULL UNIQUE,
        Description NVARCHAR(200) NULL
    );
END;

IF OBJECT_ID(N'dbo.AppUsers', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.AppUsers (
        UserId INT IDENTITY(1,1) PRIMARY KEY,
        Username NVARCHAR(50) NOT NULL UNIQUE,
        Email NVARCHAR(255) NULL,
        DisplayName NVARCHAR(100) NOT NULL,
        PasswordHash NVARCHAR(256) NOT NULL,
        PasswordSalt NVARCHAR(64) NOT NULL,
        RoleId INT NOT NULL FOREIGN KEY REFERENCES dbo.AppRoles(RoleId),
        IsActive BIT NOT NULL DEFAULT 1,
        LastLoginDate DATETIME NULL,
        CreatedDate DATETIME NOT NULL DEFAULT GETDATE(),
        ForcePasswordChange BIT NOT NULL DEFAULT 0,
        FailedAttempts INT NOT NULL DEFAULT 0,
        LockoutUntil DATETIME NULL
    );
END;

IF OBJECT_ID(N'dbo.UserSessions', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.UserSessions (
        SessionId UNIQUEIDENTIFIER NOT NULL PRIMARY KEY,
        UserId INT NOT NULL FOREIGN KEY REFERENCES dbo.AppUsers(UserId),
        TokenHash NVARCHAR(128) NOT NULL,
        RefreshTokenHash NVARCHAR(128) NULL,
        DeviceName NVARCHAR(128) NULL,
        IPAddress NVARCHAR(50) NULL,
        CreatedAt DATETIME NOT NULL DEFAULT GETDATE(),
        ExpiresAt DATETIME NOT NULL,
        LastSeenAt DATETIME NULL,
        RevokedAt DATETIME NULL
    );
END;

IF OBJECT_ID(N'dbo.LoginAudit', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.LoginAudit (
        AuditId INT IDENTITY(1,1) PRIMARY KEY,
        UserId INT NULL,
        Username NVARCHAR(255) NULL,
        Success BIT NOT NULL,
        FailureReason NVARCHAR(200) NULL,
        IPAddress NVARCHAR(50) NULL,
        DeviceName NVARCHAR(128) NULL,
        CreatedAt DATETIME NOT NULL DEFAULT GETDATE()
    );
END;

IF OBJECT_ID(N'dbo.PasswordResetTokens', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.PasswordResetTokens (
        TokenId UNIQUEIDENTIFIER NOT NULL PRIMARY KEY,
        UserId INT NOT NULL FOREIGN KEY REFERENCES dbo.AppUsers(UserId),
        TokenHash NVARCHAR(128) NOT NULL,
        CreatedAt DATETIME NOT NULL DEFAULT GETDATE(),
        ExpiresAt DATETIME NOT NULL,
        UsedAt DATETIME NULL,
        RequestedByUserId INT NULL
    );
END;

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'IX_Jobs_ClientID_ScheduledDate' AND object_id = OBJECT_ID(N'dbo.Jobs'))
BEGIN
    CREATE INDEX IX_Jobs_ClientID_ScheduledDate
        ON dbo.Jobs (ClientID, ScheduledDate DESC);
END;

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'IX_Invoices_ClientID_InvoiceDate' AND object_id = OBJECT_ID(N'dbo.Invoices'))
BEGIN
    CREATE INDEX IX_Invoices_ClientID_InvoiceDate
        ON dbo.Invoices (ClientID, InvoiceDate DESC);
END;

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'IX_PurchaseOrders_SiteID_PODate' AND object_id = OBJECT_ID(N'dbo.PurchaseOrders'))
BEGIN
    CREATE INDEX IX_PurchaseOrders_SiteID_PODate
        ON dbo.PurchaseOrders (SiteID, PODate DESC);
END;

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'IX_ServiceDeskIncidents_SiteId_OpenedAt' AND object_id = OBJECT_ID(N'dbo.ServiceDeskIncidents'))
BEGIN
    CREATE INDEX IX_ServiceDeskIncidents_SiteId_OpenedAt
        ON dbo.ServiceDeskIncidents (SiteId, OpenedAt DESC);
END;

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'IX_StockItems_CurrentStock_ReorderLevel' AND object_id = OBJECT_ID(N'dbo.StockItems'))
BEGIN
    CREATE INDEX IX_StockItems_CurrentStock_ReorderLevel
        ON dbo.StockItems (CurrentStock, ReorderLevel);
END;

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'IX_B2BClients_CompanyName' AND object_id = OBJECT_ID(N'dbo.B2BClients'))
BEGIN
    CREATE INDEX IX_B2BClients_CompanyName
        ON dbo.B2BClients (CompanyName);
END;
