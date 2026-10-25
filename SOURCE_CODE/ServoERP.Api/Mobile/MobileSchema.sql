-- Explicit deployment only. Never executed automatically by API startup.
-- Existing Jobs/AppUsers/Companies/StockItems remain authoritative.
IF OBJECT_ID('dbo.MobileEmployeeCompanies','U') IS NULL
CREATE TABLE dbo.MobileEmployeeCompanies (
 CompanyId int NOT NULL REFERENCES dbo.Companies(CompanyId),
 EmployeeId int NOT NULL REFERENCES dbo.Employees(EmployeeID),
 CONSTRAINT PK_MobileEmployeeCompanies PRIMARY KEY(CompanyId,EmployeeId)
);
IF OBJECT_ID('dbo.MobileWorkspaceReceipts','U') IS NULL
CREATE TABLE dbo.MobileWorkspaceReceipts (
 CompanyId int NOT NULL,UserId int NOT NULL,OperationId uniqueidentifier NOT NULL,
 RequestHash varchar(64) NOT NULL,RecordJson nvarchar(max) NOT NULL,
 CreatedUtc datetime2 NOT NULL DEFAULT SYSUTCDATETIME(),
 CONSTRAINT PK_MobileWorkspaceReceipts PRIMARY KEY(CompanyId,UserId,OperationId)
);
IF OBJECT_ID('dbo.Companies','U') IS NULL OR OBJECT_ID('dbo.UserCompanies','U') IS NULL
 OR COL_LENGTH('dbo.B2BClients','CompanyId') IS NULL OR COL_LENGTH('dbo.Jobs','SyncPublicId') IS NULL
 OR COL_LENGTH('dbo.Jobs','SyncVersion') IS NULL OR COL_LENGTH('dbo.StockItems','CompanyId') IS NULL
 THROW 51000, 'Install and verify the existing office API company-isolation and sync schema before enabling engineer mobile.', 1;
IF OBJECT_ID('dbo.EngineerSessions','U') IS NULL
CREATE TABLE dbo.EngineerSessions (
 SessionId uniqueidentifier NOT NULL PRIMARY KEY, FamilyId uniqueidentifier NOT NULL,
 UserId int NOT NULL REFERENCES dbo.AppUsers(UserId), CompanyId int NOT NULL REFERENCES dbo.Companies(CompanyId),
 AccessHash varchar(64) NOT NULL UNIQUE, RefreshHash varchar(64) NOT NULL UNIQUE,
 AccessExpiresUtc datetime2 NOT NULL, RefreshExpiresUtc datetime2 NOT NULL,
 Revoked bit NOT NULL DEFAULT 0, DeviceId nvarchar(128) NOT NULL
);
IF OBJECT_ID('dbo.EngineerReports','U') IS NULL
CREATE TABLE dbo.EngineerReports (
 JobId int NOT NULL PRIMARY KEY REFERENCES dbo.Jobs(JobID), CompanyId int NOT NULL REFERENCES dbo.Companies(CompanyId),
 Revision bigint NOT NULL, ReportJson nvarchar(max) NOT NULL, UpdatedUtc datetime2 NOT NULL
);
IF OBJECT_ID('dbo.EngineerReceipts','U') IS NULL
CREATE TABLE dbo.EngineerReceipts (
 OperationId uniqueidentifier NOT NULL, CompanyId int NOT NULL, UserId int NOT NULL,
 JobId int NOT NULL REFERENCES dbo.Jobs(JobID), RequestHash varchar(64) NOT NULL,
 ReceiptJson nvarchar(max) NOT NULL, CommittedUtc datetime2 NOT NULL,
 CONSTRAINT PK_EngineerReceipts PRIMARY KEY(CompanyId,UserId,OperationId)
);
IF OBJECT_ID('dbo.EngineerVanStock','U') IS NULL
CREATE TABLE dbo.EngineerVanStock (
 CompanyId int NOT NULL REFERENCES dbo.Companies(CompanyId), EmployeeId int NOT NULL REFERENCES dbo.Employees(EmployeeID),
 ItemId int NOT NULL REFERENCES dbo.StockItems(ItemID), Quantity decimal(10,2) NOT NULL CHECK(Quantity>=0),
 CONSTRAINT PK_EngineerVanStock PRIMARY KEY(CompanyId,EmployeeId,ItemId)
);
IF OBJECT_ID('dbo.EngineerAttachments','U') IS NULL
CREATE TABLE dbo.EngineerAttachments (
 AttachmentId uniqueidentifier NOT NULL PRIMARY KEY, OperationId uniqueidentifier NOT NULL,
 CompanyId int NOT NULL, UserId int NOT NULL, JobId int NOT NULL REFERENCES dbo.Jobs(JobID),
 SourceHash varchar(64) NOT NULL, StorageName varchar(80) NOT NULL, CreatedUtc datetime2 NOT NULL, Linked bit NOT NULL DEFAULT 0,
 CONSTRAINT UQ_EngineerAttachments_Operation UNIQUE(CompanyId,UserId,OperationId)
);
IF OBJECT_ID('dbo.EngineerAudit','U') IS NULL
CREATE TABLE dbo.EngineerAudit (
 EventId bigint IDENTITY PRIMARY KEY, CompanyId int NOT NULL, UserId int NOT NULL,
 JobId int NOT NULL REFERENCES dbo.Jobs(JobID), OperationId uniqueidentifier NOT NULL,
 DeviceId nvarchar(128) NOT NULL, Action nvarchar(40) NOT NULL, CapturedUtc datetime2 NOT NULL, ReceivedUtc datetime2 NOT NULL,
 BeforeState nvarchar(40) NOT NULL, AfterState nvarchar(40) NOT NULL
);
IF COL_LENGTH('dbo.JobChecklistItems','EngineerRequired') IS NULL
ALTER TABLE dbo.JobChecklistItems ADD EngineerRequired bit NOT NULL CONSTRAINT DF_JobChecklist_EngineerRequired DEFAULT 1;
IF COL_LENGTH('dbo.JobChecklistItems','EngineerOutcome') IS NULL
ALTER TABLE dbo.JobChecklistItems ADD EngineerOutcome nvarchar(10) NULL;
IF COL_LENGTH('dbo.JobChecklistItems','EngineerReading') IS NULL
ALTER TABLE dbo.JobChecklistItems ADD EngineerReading nvarchar(200) NULL;
IF COL_LENGTH('dbo.JobChecklistItems','EngineerNote') IS NULL
ALTER TABLE dbo.JobChecklistItems ADD EngineerNote nvarchar(1000) NULL;
IF COL_LENGTH('dbo.Jobs','EngineerRequiredPhotos') IS NULL
ALTER TABLE dbo.Jobs ADD EngineerRequiredPhotos nvarchar(500) NULL;

-- Every legacy stock writer is protected, including desktop SQL paths.
-- Allocations are a partition of aggregate available stock, not additional stock.
-- DDL uses dynamic SQL only to create guarded triggers, never to interpolate user data.
IF OBJECT_ID('dbo.TR_StockItems_EngineerAllocations','TR') IS NULL
EXEC('CREATE TRIGGER dbo.TR_StockItems_EngineerAllocations ON dbo.StockItems AFTER UPDATE AS
BEGIN
 SET NOCOUNT ON;
 IF EXISTS(SELECT 1 FROM inserted i WHERE i.CurrentStock-ISNULL(i.ReservedStock,0) <
 (SELECT ISNULL(SUM(v.Quantity),0) FROM dbo.EngineerVanStock v WITH(UPDLOCK,HOLDLOCK) WHERE v.ItemId=i.ItemID AND v.CompanyId=i.CompanyId))
 THROW 51001, ''Stock is allocated to engineer vans. Reconcile allocations before reducing availability.'', 1;
END');
IF OBJECT_ID('dbo.TR_EngineerVanStock_Availability','TR') IS NULL
EXEC('CREATE TRIGGER dbo.TR_EngineerVanStock_Availability ON dbo.EngineerVanStock AFTER INSERT,UPDATE AS
BEGIN
 SET NOCOUNT ON;
 IF EXISTS(SELECT 1 FROM inserted i JOIN dbo.StockItems s WITH(UPDLOCK,HOLDLOCK) ON s.ItemID=i.ItemId AND s.CompanyId=i.CompanyId
 WHERE s.CurrentStock-ISNULL(s.ReservedStock,0) < (SELECT SUM(v.Quantity) FROM dbo.EngineerVanStock v WHERE v.ItemId=i.ItemId AND v.CompanyId=i.CompanyId))
 THROW 51002, ''Van allocations exceed available stock.'', 1;
 IF EXISTS(SELECT 1 FROM inserted i WHERE NOT EXISTS(SELECT 1 FROM dbo.StockItems s WHERE s.ItemID=i.ItemId AND s.CompanyId=i.CompanyId))
 THROW 51003, ''Cross-company stock allocation blocked.'', 1;
END');

