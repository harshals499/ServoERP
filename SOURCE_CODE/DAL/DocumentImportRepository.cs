using System;
using System.Collections.Generic;
using System.Data;
using System.Data.SqlClient;
using System.Linq;
using Dapper;
using HVAC_Pro_Desktop.Models;
using HVAC_Pro_Desktop.Services;

namespace HVAC_Pro_Desktop.DAL
{
    internal sealed class DocumentImportRepository
    {
        // Guarded additive metadata only; existing business schemas and protected financial views stay intact.
        internal static void EnsureSchema(SqlConnection connection)
        {
            connection.Execute(@"IF OBJECT_ID(N'dbo.DocumentImports',N'U') IS NULL
CREATE TABLE dbo.DocumentImports(
 ImportId int IDENTITY PRIMARY KEY, FileHash char(64) NOT NULL, FirstPage int NOT NULL, LastPage int NOT NULL,
 Module nvarchar(40) NOT NULL, RecordId int NOT NULL, DocumentNumber nvarchar(50) NOT NULL,
 PartyId int NOT NULL, DocumentDate datetime NOT NULL, Total decimal(18,2) NOT NULL,
 FilePath nvarchar(500) NOT NULL, OriginalFileName nvarchar(255) NOT NULL,
 ExtractionStatus nvarchar(40) NOT NULL, ReviewStatus nvarchar(40) NOT NULL,
 Source nvarchar(40) NOT NULL, ReviewedBy nvarchar(100) NULL, ReviewedUtc datetime2 NOT NULL DEFAULT SYSUTCDATETIME(),
 CONSTRAINT UQ_DocumentImports_Source UNIQUE(FileHash,FirstPage,LastPage));
IF OBJECT_ID(N'dbo.DocumentImportAttempts',N'U') IS NULL
CREATE TABLE dbo.DocumentImportAttempts(AttemptId uniqueidentifier PRIMARY KEY, Stage nvarchar(40) NOT NULL,
 ErrorCode nvarchar(80) NULL, CreatedUtc datetime2 NOT NULL DEFAULT SYSUTCDATETIME());");
        }
        internal static string Module(ImportDocumentType type) => type == ImportDocumentType.Quotation ? "Quotations" : type == ImportDocumentType.SalesInvoice ? "Invoices" : "Purchases";
        internal static List<ImportReference> Parties(SqlConnection c,bool purchase)
            => c.Query<ImportReference>(purchase ? "SELECT VendorID Id,VendorName Name,GSTNumber GSTIN FROM Vendors WHERE IsSupplier=1 AND IsActive=1" : "SELECT ClientID Id,CompanyName Name,GSTNumber GSTIN FROM B2BClients WHERE IsActive=1").ToList();
        internal static List<ImportReference> Products(SqlConnection c) => c.Query<ImportReference>("SELECT ItemID Id,ItemName Name,HSNCode GSTIN FROM StockItems WHERE IsActive=1").ToList();
        internal static List<ImportSavedRecord> History(SqlConnection c) => c.Query<ImportSavedRecord>("SELECT TOP(500) Module,RecordId,DocumentNumber,FilePath,ReviewedUtc FROM dbo.DocumentImports ORDER BY ImportId DESC").ToList();
        internal static int Save(SqlConnection c, SqlTransaction tx, DocumentImportDraft d,string attachment)
        {
            string module=Module(d.DocumentType);
            // One database-scoped lock serializes duplicate checks with all importer instances, including other PCs.
            int lockResult=c.ExecuteScalar<int>("DECLARE @r int; EXEC @r=sys.sp_getapplock @Resource=N'ServoERP.DocumentImport.Save',@LockMode='Exclusive',@LockOwner='Transaction',@LockTimeout=15000; SELECT @r;",transaction:tx);
            if(lockResult<0) throw new InvalidOperationException("Another import is saving. Please retry.");
            var previous=c.QuerySingleOrDefault<ImportSavedRecord>("SELECT Module,RecordId,DocumentNumber,FilePath,ReviewedUtc FROM dbo.DocumentImports WHERE FileHash=@FileHash AND FirstPage=@FirstPage AND LastPage=@LastPage",d,tx);
            if(previous!=null)
            {
                if(previous.Module!=module) throw new InvalidOperationException("This source range was already saved to "+previous.Module+". Open its history entry.");
                return previous.RecordId;
            }
            if(c.ExecuteScalar<int>("SELECT COUNT(*) FROM dbo.DocumentImports WHERE FileHash=@FileHash AND FirstPage<=@LastPage AND LastPage>=@FirstPage",d,tx)>0)
                throw new InvalidOperationException("These pages overlap an already saved import. Review import history.");
            string duplicate = module=="Invoices"
                ? "SELECT COUNT(*) FROM dbo.Invoices WITH(UPDLOCK,HOLDLOCK) WHERE InvoiceNumber=@DocumentNumber OR (ClientID=@PartyId AND CONVERT(date,InvoiceDate)=CONVERT(date,@DocumentDate) AND ABS(TotalAmount-@Total)<=0.01)"
                : module=="Purchases"
                ? "SELECT COUNT(*) FROM dbo.PurchaseOrders WITH(UPDLOCK,HOLDLOCK) WHERE VendorID=@PartyId AND (VendorInvoiceNumber=@DocumentNumber OR PONumber=@DocumentNumber OR (CONVERT(date,PODate)=CONVERT(date,@DocumentDate) AND ABS(TotalAmount-@Total)<=0.01))"
                : "SELECT COUNT(*) FROM dbo.Quotations WITH(UPDLOCK,HOLDLOCK) WHERE (ClientID=@PartyId AND QuotationNumber=@DocumentNumber) OR (ClientID=@PartyId AND CONVERT(date,SubmittedDate)=CONVERT(date,@DocumentDate) AND ABS(TotalWithGST-@Total)<=0.01)";
            if(c.ExecuteScalar<int>(duplicate,d,tx)>0) throw new InvalidOperationException("A likely duplicate exists in "+module+" (number or party/date/amount). Open the existing record before importing again.");
            string partySql=module=="Purchases"?"SELECT COUNT(*) FROM Vendors WHERE VendorID=@PartyId AND IsSupplier=1 AND IsActive=1":"SELECT COUNT(*) FROM B2BClients WHERE ClientID=@PartyId AND IsActive=1";
            if(c.ExecuteScalar<int>(partySql,d,tx)!=1) throw new InvalidOperationException("The selected party no longer exists. Match the party again.");
            foreach(var line in d.Lines)
                if(line.ProductId.HasValue && c.ExecuteScalar<int>("SELECT COUNT(*) FROM StockItems WHERE ItemID=@ProductId AND IsActive=1",line,tx)!=1)
                    throw new InvalidOperationException("A selected product no longer exists. Match it again.");
            int id;
            if(module=="Invoices")
            {
                id=c.ExecuteScalar<int>(@"INSERT dbo.Invoices(ClientID,InvoiceNumber,InvoiceDate,DueDate,SubTotal,GSTPercent,TaxAmount,TotalAmount,PaidAmount,BalanceDue,PaymentStatus,InvoiceTitle,GSTMode,RoundOff,CGSTAmount,SGSTAmount,IGSTAmount,InventoryReservationStatus,IsPosted,Subject,PONumber,Notes)
VALUES(@PartyId,@DocumentNumber,@DocumentDate,@DueDate,@TaxableAmount,@Gst,@Tax,@Total,0,@Total,'Draft','TAX INVOICE',@Mode,@RoundOff,@CGST,@SGST,@IGST,'Pending',0,@Subject,@Reference,@Notes); SELECT CAST(SCOPE_IDENTITY() AS int);",
                    new {d.PartyId,d.DocumentNumber,d.DocumentDate,d.DueDate,d.TaxableAmount,Gst=d.Lines[0].GSTPercent,Tax=d.CGST+d.SGST+d.IGST,d.Total,Mode=d.IGST>0?"IGST":"CGST+SGST",d.RoundOff,d.CGST,d.SGST,d.IGST,d.Subject,d.Reference,Notes=Notes(d)},tx);
                foreach(var l in d.Lines) c.Execute(@"INSERT dbo.InvoiceLineItems(InvoiceID,StockItemID,Description,HSNCode,Category,Unit,Quantity,Rate,DiscountPercent,GSTPercent,TaxType,TaxAmount,IsStockItem,IsBillable,Amount)
VALUES(@Id,@ProductId,@Description,@HSN,CASE WHEN @ProductId IS NULL THEN 'Service' ELSE 'Material' END,@Unit,@Quantity,@Rate,@DiscountPercent,@GSTPercent,'Taxable',@Tax,CASE WHEN @ProductId IS NULL THEN 0 ELSE 1 END,1,@Taxable)",LineArgs(l,id),tx);
            }
            else if(module=="Purchases")
            {
                id=c.ExecuteScalar<int>(@"INSERT dbo.PurchaseOrders(VendorID,PONumber,PODate,PayByDate,VendorInvoiceNumber,TotalAmount,PaidAmount,Status,ReceiptImagePath,Notes)
VALUES(@PartyId,@DocumentNumber,@DocumentDate,@DueDate,@DocumentNumber,@Total,0,'Pending',@Attachment,@Notes); SELECT CAST(SCOPE_IDENTITY() AS int);",new {d.PartyId,d.DocumentNumber,d.DocumentDate,d.DueDate,d.Total,Attachment=attachment,Notes=Notes(d)},tx);
                foreach(var l in d.Lines) c.Execute(@"INSERT dbo.PurchaseLineItems(POID,InventoryItemId,VendorID,Description,ItemName,HsnSacCode,Quantity,UOM,Rate,UnitPrice,GSTRate,CGSTRate,SGSTRate,IGSTRate,Amount)
VALUES(@Id,@ProductId,@VendorId,@Description,@Description,@HSN,@Quantity,@Unit,@Rate,@Rate,@GSTPercent,@C,@S,@I,@Gross)",new {Id=id,l.ProductId,VendorId=d.PartyId,l.Description,l.HSN,l.Quantity,l.Unit,l.Rate,l.GSTPercent,C=d.IGST>0?0:l.GSTPercent/2,S=d.IGST>0?0:l.GSTPercent/2,I=d.IGST>0?l.GSTPercent:0,Gross=l.Taxable+l.Tax},tx);
            }
            else
            {
                id=c.ExecuteScalar<int>(@"INSERT dbo.Quotations(QuotationNumber,TenderName,ClientID,ClientName,SystemCount,BidValue,DueDate,SubmittedDate,Status,IsMultiLine,TotalTaxableValue,TotalGSTAmount,TotalWithGST,CommercialFlow,CustomerDocumentStatus,SupplierDocumentStatus,Notes)
VALUES(@DocumentNumber,@Title,@PartyId,@PartyName,1,@TaxableAmount,@DueDate,@DocumentDate,'Draft',1,@TaxableAmount,@Tax,@Total,'Revenue','Quote Draft','Not Required',@Notes); SELECT CAST(SCOPE_IDENTITY() AS int);",new {d.DocumentNumber,Title=string.IsNullOrWhiteSpace(d.Subject)?"Imported quotation "+d.DocumentNumber:d.Subject,d.PartyId,d.PartyName,d.TaxableAmount,d.DueDate,d.DocumentDate,Tax=d.CGST+d.SGST+d.IGST,d.Total,Notes=Notes(d)},tx);
                int sort=0;
                foreach(var l in d.Lines) c.Execute(@"INSERT dbo.QuotationLineItems(TenderBidId,SortOrder,Category,InventoryItemId,ItemDescription,Quantity,Unit,HsnSacCode,GSTRatePct,SellPricePerUnit,TaxableLineTotal,GSTAmount,IsSellPriceManual)
VALUES(@Id,@Sort,'Material',@ProductId,@Description,@Quantity,@Unit,@HSN,@GSTPercent,@Rate,@Taxable,@Tax,1)",new {Id=id,Sort=++sort,l.ProductId,l.Description,l.Quantity,l.Unit,l.HSN,l.GSTPercent,l.Rate,l.Taxable,l.Tax},tx);
            }
            c.Execute(@"INSERT dbo.ClientDocuments(ClientId,DocumentType,Title,FilePath,OriginalFileName,Notes,UploadedBy)
VALUES(@ClientId,@Module,@Title,@Path,@OriginalFileName,@Link,@Actor);",new {ClientId=module=="Purchases"?(int?)null:d.PartyId,Module=module,Title="Imported "+d.DocumentNumber,Path=attachment,d.OriginalFileName,Link=module+" #"+id,Actor=SessionManager.CurrentUser?.DisplayName},tx);
            c.Execute(@"INSERT dbo.DocumentImports(FileHash,FirstPage,LastPage,Module,RecordId,DocumentNumber,PartyId,DocumentDate,Total,FilePath,OriginalFileName,ExtractionStatus,ReviewStatus,Source,ReviewedBy)
VALUES(@FileHash,@FirstPage,@LastPage,@Module,@Id,@DocumentNumber,@PartyId,@DocumentDate,@Total,@Path,@OriginalFileName,@ExtractionStatus,'ApprovedDraft','LocalDocumentImport',@Actor)",new {d.FileHash,d.FirstPage,d.LastPage,Module=module,Id=id,d.DocumentNumber,d.PartyId,d.DocumentDate,d.Total,Path=attachment,d.OriginalFileName,d.ExtractionStatus,Actor=SessionManager.CurrentUser?.DisplayName},tx);
            return id;
        }
        private static object LineArgs(DocumentImportLine l,int id) => new {Id=id,l.ProductId,l.Description,l.HSN,l.Unit,l.Quantity,l.Rate,l.DiscountPercent,l.GSTPercent,l.Tax,l.Taxable};
        private static string Notes(DocumentImportDraft d) => "Reviewed local document import; original available in Document Import history. Source: "+d.SourceLocation+"\r\nReference: "+d.Reference+"\r\nPayment details (unposted): "+d.PaymentDetails;
    }
}
