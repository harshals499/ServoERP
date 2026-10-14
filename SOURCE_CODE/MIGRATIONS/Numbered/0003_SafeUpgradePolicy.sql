-- 0003: additive metadata only; never edit a shipped numbered migration.
IF OBJECT_ID('dbo.ServoDatabaseUpgradePolicy','U') IS NULL
CREATE TABLE dbo.ServoDatabaseUpgradePolicy (
 PolicyId int NOT NULL PRIMARY KEY,
 PolicyVersion int NOT NULL,
 EnabledUtc datetime2 NOT NULL);
IF NOT EXISTS (SELECT 1 FROM dbo.ServoDatabaseUpgradePolicy WHERE PolicyId=1)
INSERT dbo.ServoDatabaseUpgradePolicy(PolicyId,PolicyVersion,EnabledUtc)
VALUES(1,1,SYSUTCDATETIME());
