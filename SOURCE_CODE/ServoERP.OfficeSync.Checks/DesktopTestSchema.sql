-- Disposable integration fixture supplement, copied from the desktop schema guard.
-- Never apply this test fixture to HVAC_PRO or customer databases.
IF DB_NAME() NOT LIKE 'EngineerTests[_]%' THROW 51000, 'Disposable EngineerTests database required', 1;
IF NOT EXISTS (SELECT 1 FROM sys.key_constraints WHERE parent_object_id=OBJECT_ID('B2BClients') AND type='PK') ALTER TABLE B2BClients ADD CONSTRAINT PK_SyncTest_B2BClients PRIMARY KEY (ClientID);
IF NOT EXISTS (SELECT 1 FROM sys.tables WHERE name='ClientContacts')
CREATE TABLE ClientContacts (
 ContactID int PRIMARY KEY IDENTITY(1,1), ClientID int NOT NULL REFERENCES B2BClients(ClientID),
 ContactName nvarchar(255) NOT NULL, Role nvarchar(100) NULL, Phone nvarchar(30) NULL,
 Email nvarchar(255) NULL, IsPrimary bit NOT NULL DEFAULT 0, Notes nvarchar(500) NULL,
 CreatedDate datetime NOT NULL DEFAULT GETDATE()
);

IF DB_NAME() NOT LIKE 'EngineerTests[_]%' THROW 51000, 'Disposable EngineerTests database required', 1;
-- Mirror the existing desktop ActiveJobs view from migration 0004, in this synthetic fixture only.
IF COL_LENGTH('dbo.Jobs','IsDeleted') IS NULL ALTER TABLE dbo.Jobs ADD IsDeleted bit NOT NULL CONSTRAINT DF_SyncTest_Jobs_IsDeleted DEFAULT 0;
IF OBJECT_ID('dbo.ActiveJobs','V') IS NULL EXEC(N'CREATE VIEW dbo.ActiveJobs AS SELECT * FROM dbo.Jobs WHERE IsDeleted=0;');
