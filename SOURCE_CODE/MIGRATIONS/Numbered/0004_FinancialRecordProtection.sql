-- Financial protection: additive columns, transactional audit and immutable posted documents.
IF OBJECT_ID('dbo.FinancialRecordAudit','U') IS NULL
CREATE TABLE dbo.FinancialRecordAudit (
 AuditId bigint IDENTITY PRIMARY KEY, ChangedUtc datetime2 NOT NULL DEFAULT SYSUTCDATETIME(),
 Module nvarchar(30) NOT NULL, RecordId int NOT NULL, Action nvarchar(30) NOT NULL,
 UserId int NULL, UserName nvarchar(160) NOT NULL, SqlLogin nvarchar(160) NOT NULL,
 MachineName nvarchar(160) NULL, OldValues nvarchar(max) NULL, NewValues nvarchar(max) NULL);
IF NOT EXISTS(SELECT 1 FROM sys.indexes WHERE object_id=OBJECT_ID('dbo.FinancialRecordAudit') AND name='IX_FinancialRecordAudit_Record')
CREATE INDEX IX_FinancialRecordAudit_Record ON dbo.FinancialRecordAudit(Module,RecordId,AuditId DESC);
IF COL_LENGTH('dbo.Invoices','IsDeleted') IS NULL ALTER TABLE dbo.Invoices ADD IsDeleted bit NOT NULL DEFAULT(0) WITH VALUES;
IF COL_LENGTH('dbo.Invoices','DeletedUtc') IS NULL ALTER TABLE dbo.Invoices ADD DeletedUtc datetime2 NULL;
IF COL_LENGTH('dbo.Invoices','DeletedByUserId') IS NULL ALTER TABLE dbo.Invoices ADD DeletedByUserId int NULL;
IF COL_LENGTH('dbo.Invoices','DeletedByName') IS NULL ALTER TABLE dbo.Invoices ADD DeletedByName nvarchar(160) NULL;
IF COL_LENGTH('dbo.Jobs','IsDeleted') IS NULL ALTER TABLE dbo.Jobs ADD IsDeleted bit NOT NULL DEFAULT(0) WITH VALUES;
IF COL_LENGTH('dbo.Jobs','DeletedUtc') IS NULL ALTER TABLE dbo.Jobs ADD DeletedUtc datetime2 NULL;
IF COL_LENGTH('dbo.Jobs','DeletedByUserId') IS NULL ALTER TABLE dbo.Jobs ADD DeletedByUserId int NULL;
IF COL_LENGTH('dbo.Jobs','DeletedByName') IS NULL ALTER TABLE dbo.Jobs ADD DeletedByName nvarchar(160) NULL;
IF COL_LENGTH('dbo.Invoices','IsPosted') IS NULL ALTER TABLE dbo.Invoices ADD IsPosted bit NOT NULL DEFAULT(0) WITH VALUES;
IF COL_LENGTH('dbo.Invoices','PostedUtc') IS NULL ALTER TABLE dbo.Invoices ADD PostedUtc datetime2 NULL;
EXEC(N'UPDATE dbo.Invoices SET IsPosted=1 WHERE IsPosted=0 AND ISNULL(PaymentStatus,''Draft'')<>''Draft'';');
EXEC(N'UPDATE dbo.Jobs SET IsDeleted=1 WHERE IsDeleted=0 AND DeletedUtc IS NOT NULL;');
EXEC(N'CREATE OR ALTER VIEW dbo.ActiveInvoices AS SELECT * FROM dbo.Invoices WHERE IsDeleted=0;');
EXEC(N'CREATE OR ALTER VIEW dbo.FinancialInvoices AS SELECT * FROM dbo.Invoices WHERE IsDeleted=0 OR IsPosted=1;');
EXEC(N'CREATE OR ALTER VIEW dbo.ActiveJobs AS SELECT * FROM dbo.Jobs WHERE IsDeleted=0;');
EXEC(N'CREATE OR ALTER TRIGGER dbo.TR_Invoices_FinancialProtection ON dbo.Invoices AFTER INSERT,UPDATE,DELETE AS
BEGIN
 SET NOCOUNT ON;
 IF ISNULL(TRY_CONVERT(int,SESSION_CONTEXT(N''ServoERP.SkipAudit'')),0)=1 RETURN;
 IF ISNULL(TRY_CONVERT(int,SESSION_CONTEXT(N''ServoERP.Maintenance'')),0)<>1
 BEGIN
 IF EXISTS(SELECT 1 FROM deleted d LEFT JOIN inserted i ON i.InvoiceID=d.InvoiceID WHERE i.InvoiceID IS NULL) THROW 51024,''Archive invoices instead of permanently deleting them.'',1;
 IF EXISTS(SELECT 1 FROM inserted i JOIN deleted d ON i.InvoiceID=d.InvoiceID WHERE d.IsPosted=1 AND
 (i.IsPosted=0 OR i.PaymentStatus IN (''Draft'',''Cancelled'') AND ISNULL(i.PaymentStatus,'''')<>ISNULL(d.PaymentStatus,'''') OR
 EXISTS(SELECT i.ContractID,i.ClientID,i.SiteID,i.QuotationBidID,ISNULL(i.InvoiceNumber,N''''),i.InvoiceDate,i.DueDate,i.SubTotal,i.GSTPercent,i.TaxAmount,i.TotalAmount,ISNULL(i.Notes,N''''),ISNULL(i.InvoiceTitle,N''''),ISNULL(i.Subject,N''''),ISNULL(i.PONumber,N''''),i.PODate,ISNULL(i.SendInvoiceTo,N''''),ISNULL(i.CertificationNote,N''''),ISNULL(i.TemplateCode,N''''),ISNULL(i.WorkflowType,N''''),ISNULL(i.GSTMode,N''''),ISNULL(i.PaymentTerms,N''''),ISNULL(i.PlaceOfSupply,N''''),i.RoundOff,i.CGSTAmount,i.SGSTAmount,i.IGSTAmount,ISNULL(i.ContractCoverageType,N''''),ISNULL(i.ServiceChecklist,N''''),ISNULL(i.AssetDetails,N''''),ISNULL(i.WarrantyStatus,N''''),i.WarrantyExpiry EXCEPT SELECT d.ContractID,d.ClientID,d.SiteID,d.QuotationBidID,ISNULL(d.InvoiceNumber,N''''),d.InvoiceDate,d.DueDate,d.SubTotal,d.GSTPercent,d.TaxAmount,d.TotalAmount,ISNULL(d.Notes,N''''),ISNULL(d.InvoiceTitle,N''''),ISNULL(d.Subject,N''''),ISNULL(d.PONumber,N''''),d.PODate,ISNULL(d.SendInvoiceTo,N''''),ISNULL(d.CertificationNote,N''''),ISNULL(d.TemplateCode,N''''),ISNULL(d.WorkflowType,N''''),ISNULL(d.GSTMode,N''''),ISNULL(d.PaymentTerms,N''''),ISNULL(d.PlaceOfSupply,N''''),d.RoundOff,d.CGSTAmount,d.SGSTAmount,d.IGSTAmount,ISNULL(d.ContractCoverageType,N''''),ISNULL(d.ServiceChecklist,N''''),ISNULL(d.AssetDetails,N''''),ISNULL(d.WarrantyStatus,N''''),d.WarrantyExpiry)))
 THROW 51020,''Posted invoices are locked. Use a credit note for financial corrections.'',1;
IF EXISTS(SELECT 1 FROM inserted i JOIN deleted d ON i.InvoiceID=d.InvoiceID WHERE d.IsDeleted=1 AND i.IsDeleted=1 AND
 EXISTS(SELECT i.ContractID,i.ClientID,i.SiteID,i.QuotationBidID,ISNULL(i.InvoiceNumber,N''''),i.InvoiceDate,i.DueDate,i.SubTotal,i.GSTPercent,i.TaxAmount,i.TotalAmount,ISNULL(i.Notes,N''''),ISNULL(i.InvoiceTitle,N''''),ISNULL(i.Subject,N''''),ISNULL(i.PONumber,N''''),i.PODate,ISNULL(i.SendInvoiceTo,N''''),ISNULL(i.CertificationNote,N''''),ISNULL(i.TemplateCode,N''''),ISNULL(i.WorkflowType,N''''),ISNULL(i.GSTMode,N''''),ISNULL(i.PaymentTerms,N''''),ISNULL(i.PlaceOfSupply,N''''),i.RoundOff,i.CGSTAmount,i.SGSTAmount,i.IGSTAmount,ISNULL(i.ContractCoverageType,N''''),ISNULL(i.ServiceChecklist,N''''),ISNULL(i.AssetDetails,N''''),ISNULL(i.WarrantyStatus,N''''),i.WarrantyExpiry EXCEPT SELECT d.ContractID,d.ClientID,d.SiteID,d.QuotationBidID,ISNULL(d.InvoiceNumber,N''''),d.InvoiceDate,d.DueDate,d.SubTotal,d.GSTPercent,d.TaxAmount,d.TotalAmount,ISNULL(d.Notes,N''''),ISNULL(d.InvoiceTitle,N''''),ISNULL(d.Subject,N''''),ISNULL(d.PONumber,N''''),d.PODate,ISNULL(d.SendInvoiceTo,N''''),ISNULL(d.CertificationNote,N''''),ISNULL(d.TemplateCode,N''''),ISNULL(d.WorkflowType,N''''),ISNULL(d.GSTMode,N''''),ISNULL(d.PaymentTerms,N''''),ISNULL(d.PlaceOfSupply,N''''),d.RoundOff,d.CGSTAmount,d.SGSTAmount,d.IGSTAmount,ISNULL(d.ContractCoverageType,N''''),ISNULL(d.ServiceChecklist,N''''),ISNULL(d.AssetDetails,N''''),ISNULL(d.WarrantyStatus,N''''),d.WarrantyExpiry))
 THROW 51021,''Archived invoice details cannot be edited.'',1;

 END;
 INSERT dbo.FinancialRecordAudit(Module,RecordId,Action,UserId,UserName,SqlLogin,MachineName,OldValues,NewValues)
 SELECT N''Invoices'',COALESCE(i.InvoiceID,d.InvoiceID),CASE WHEN d.InvoiceID IS NULL THEN ''CREATE'' WHEN i.InvoiceID IS NULL THEN ''MAINTENANCE_DELETE'' WHEN d.IsDeleted=0 AND i.IsDeleted=1 THEN ''ARCHIVE'' WHEN d.IsDeleted=1 AND i.IsDeleted=0 THEN ''RESTORE'' WHEN d.IsPosted=0 AND i.IsPosted=1 THEN ''POST'' ELSE ''EDIT'' END,TRY_CONVERT(int,SESSION_CONTEXT(N''ServoERP.UserId'')),
 COALESCE(CONVERT(nvarchar(160),SESSION_CONTEXT(N''ServoERP.UserName'')),ORIGINAL_LOGIN()),ORIGINAL_LOGIN(),HOST_NAME(),
 CASE WHEN d.InvoiceID IS NULL THEN NULL ELSE (SELECT d.* FOR JSON PATH,INCLUDE_NULL_VALUES,WITHOUT_ARRAY_WRAPPER) END,
 CASE WHEN i.InvoiceID IS NULL THEN NULL ELSE (SELECT i.* FOR JSON PATH,INCLUDE_NULL_VALUES,WITHOUT_ARRAY_WRAPPER) END
 FROM inserted i FULL OUTER JOIN deleted d ON i.InvoiceID=d.InvoiceID;
END;');
EXEC(N'CREATE OR ALTER TRIGGER dbo.TR_Jobs_FinancialProtection ON dbo.Jobs AFTER INSERT,UPDATE,DELETE AS
BEGIN
 SET NOCOUNT ON;
 IF ISNULL(TRY_CONVERT(int,SESSION_CONTEXT(N''ServoERP.SkipAudit'')),0)=1 RETURN;
 IF ISNULL(TRY_CONVERT(int,SESSION_CONTEXT(N''ServoERP.Maintenance'')),0)<>1
 BEGIN
 IF EXISTS(SELECT 1 FROM deleted d LEFT JOIN inserted i ON i.JobID=d.JobID WHERE i.JobID IS NULL) THROW 51025,''Archive jobs instead of permanently deleting them.'',1;
 IF EXISTS(SELECT 1 FROM inserted i JOIN deleted d ON i.JobID=d.JobID WHERE d.IsDeleted=1 AND i.IsDeleted=1)
 THROW 51023,''Archived jobs cannot be edited. Restore the job first.'',1;

 END;
 INSERT dbo.FinancialRecordAudit(Module,RecordId,Action,UserId,UserName,SqlLogin,MachineName,OldValues,NewValues)
 SELECT N''Jobs'',COALESCE(i.JobID,d.JobID),CASE WHEN d.JobID IS NULL THEN ''CREATE'' WHEN i.JobID IS NULL THEN ''MAINTENANCE_DELETE'' WHEN d.IsDeleted=0 AND i.IsDeleted=1 THEN ''ARCHIVE'' WHEN d.IsDeleted=1 AND i.IsDeleted=0 THEN ''RESTORE'' ELSE ''EDIT'' END,TRY_CONVERT(int,SESSION_CONTEXT(N''ServoERP.UserId'')),
 COALESCE(CONVERT(nvarchar(160),SESSION_CONTEXT(N''ServoERP.UserName'')),ORIGINAL_LOGIN()),ORIGINAL_LOGIN(),HOST_NAME(),
 CASE WHEN d.JobID IS NULL THEN NULL ELSE (SELECT d.* FOR JSON PATH,INCLUDE_NULL_VALUES,WITHOUT_ARRAY_WRAPPER) END,
 CASE WHEN i.JobID IS NULL THEN NULL ELSE (SELECT i.* FOR JSON PATH,INCLUDE_NULL_VALUES,WITHOUT_ARRAY_WRAPPER) END
 FROM inserted i FULL OUTER JOIN deleted d ON i.JobID=d.JobID;
END;');
EXEC(N'CREATE OR ALTER TRIGGER dbo.TR_InvoiceLineItems_FinancialProtection ON dbo.InvoiceLineItems AFTER INSERT,UPDATE,DELETE AS
BEGIN
 SET NOCOUNT ON;
 IF ISNULL(TRY_CONVERT(int,SESSION_CONTEXT(N''ServoERP.SkipAudit'')),0)=1 RETURN;
 IF ISNULL(TRY_CONVERT(int,SESSION_CONTEXT(N''ServoERP.Maintenance'')),0)<>1
 BEGIN
 IF EXISTS(SELECT 1 FROM inserted x JOIN dbo.Invoices p WITH(UPDLOCK,HOLDLOCK) ON p.InvoiceID=x.InvoiceID WHERE p.IsPosted=1 OR p.IsDeleted=1)
 OR EXISTS(SELECT 1 FROM deleted x JOIN dbo.Invoices p WITH(UPDLOCK,HOLDLOCK) ON p.InvoiceID=x.InvoiceID WHERE p.IsPosted=1 OR p.IsDeleted=1)
 THROW 51022,''Posted or archived invoice line items are locked. Use a credit note.'',1;

 END;
 INSERT dbo.FinancialRecordAudit(Module,RecordId,Action,UserId,UserName,SqlLogin,MachineName,OldValues,NewValues)
 SELECT N''Invoice lines'',COALESCE(i.LineItemID,d.LineItemID),CASE WHEN d.LineItemID IS NULL THEN ''CREATE'' WHEN i.LineItemID IS NULL THEN ''DELETE'' ELSE ''EDIT'' END,TRY_CONVERT(int,SESSION_CONTEXT(N''ServoERP.UserId'')),
 COALESCE(CONVERT(nvarchar(160),SESSION_CONTEXT(N''ServoERP.UserName'')),ORIGINAL_LOGIN()),ORIGINAL_LOGIN(),HOST_NAME(),
 CASE WHEN d.LineItemID IS NULL THEN NULL ELSE (SELECT d.* FOR JSON PATH,INCLUDE_NULL_VALUES,WITHOUT_ARRAY_WRAPPER) END,
 CASE WHEN i.LineItemID IS NULL THEN NULL ELSE (SELECT i.* FOR JSON PATH,INCLUDE_NULL_VALUES,WITHOUT_ARRAY_WRAPPER) END
 FROM inserted i FULL OUTER JOIN deleted d ON i.LineItemID=d.LineItemID;
END;');
