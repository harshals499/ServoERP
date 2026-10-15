-- Approved offline synchronization: additive schema only. No LicenseInfo changes.
IF OBJECT_ID('dbo.DesktopOfflineReceipts','U') IS NULL
CREATE TABLE dbo.DesktopOfflineReceipts(OperationId uniqueidentifier NOT NULL PRIMARY KEY,DeviceId uniqueidentifier NULL,Module nvarchar(30) NULL,BaseRevision varchar(64) NULL,RequestedServerId int NULL,OfficeId uniqueidentifier NOT NULL,ActorUserId int NOT NULL,EntityId uniqueidentifier NOT NULL,PayloadHash varchar(64) NOT NULL,ResultJson nvarchar(max) NOT NULL,CommittedUtc datetime2 NOT NULL DEFAULT SYSUTCDATETIME());
IF OBJECT_ID('dbo.DesktopOfflineDrafts','U') IS NULL
CREATE TABLE dbo.DesktopOfflineDrafts(EntityId uniqueidentifier NOT NULL PRIMARY KEY,OfficeId uniqueidentifier NOT NULL,ActorUserId int NOT NULL,Module nvarchar(30) NOT NULL,PayloadJson nvarchar(max) NOT NULL,Revision varchar(64) NOT NULL,Status nvarchar(30) NOT NULL DEFAULT 'Draft',ResultId int NULL,UpdatedUtc datetime2 NOT NULL DEFAULT SYSUTCDATETIME());
IF OBJECT_ID('dbo.DesktopOfflineConflicts','U') IS NULL
CREATE TABLE dbo.DesktopOfflineConflicts(OperationId uniqueidentifier NOT NULL PRIMARY KEY,OfficeId uniqueidentifier NOT NULL,ActorUserId int NOT NULL,EntityId uniqueidentifier NOT NULL,Module nvarchar(30) NOT NULL,ServerId int NOT NULL,BaseRevision varchar(64) NULL,LocalJson nvarchar(max) NOT NULL,ServerJson nvarchar(max) NULL,Reason nvarchar(1000) NOT NULL,CreatedUtc datetime2 NOT NULL DEFAULT SYSUTCDATETIME(),ResolvedUtc datetime2 NULL,ResolvedByUserId int NULL,Resolution nvarchar(100) NULL);
IF COL_LENGTH('dbo.Invoices','OfflineDraftId') IS NULL ALTER TABLE dbo.Invoices ADD OfflineDraftId uniqueidentifier NULL;
IF COL_LENGTH('dbo.Payments','OfflineDraftId') IS NULL ALTER TABLE dbo.Payments ADD OfflineDraftId uniqueidentifier NULL;
IF NOT EXISTS(SELECT 1 FROM sys.indexes WHERE object_id=OBJECT_ID('dbo.Invoices') AND name='UX_Invoices_OfflineDraftId')
EXEC(N'CREATE UNIQUE INDEX UX_Invoices_OfflineDraftId ON dbo.Invoices(OfflineDraftId) WHERE OfflineDraftId IS NOT NULL');
IF NOT EXISTS(SELECT 1 FROM sys.indexes WHERE object_id=OBJECT_ID('dbo.Payments') AND name='UX_Payments_OfflineDraftId')
EXEC(N'CREATE UNIQUE INDEX UX_Payments_OfflineDraftId ON dbo.Payments(OfflineDraftId) WHERE OfflineDraftId IS NOT NULL');

IF COL_LENGTH('dbo.DesktopOfflineReceipts','DeviceId') IS NULL ALTER TABLE dbo.DesktopOfflineReceipts ADD DeviceId uniqueidentifier NULL;

IF COL_LENGTH('dbo.DesktopOfflineReceipts','Module') IS NULL ALTER TABLE dbo.DesktopOfflineReceipts ADD Module nvarchar(30) NULL;
IF COL_LENGTH('dbo.DesktopOfflineReceipts','BaseRevision') IS NULL ALTER TABLE dbo.DesktopOfflineReceipts ADD BaseRevision varchar(64) NULL;

IF COL_LENGTH('dbo.DesktopOfflineReceipts','RequestedServerId') IS NULL ALTER TABLE dbo.DesktopOfflineReceipts ADD RequestedServerId int NULL;
