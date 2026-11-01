using System;
using System.Collections.Generic;
using System.Data;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Dapper;
using FluentValidation;
using HVAC_Pro_Desktop.DAL;
using HVAC_Pro_Desktop.Models;
using Newtonsoft.Json;
using ServoERP.Validators;
using HVAC_Pro_Desktop.Services.Validation;

namespace HVAC_Pro_Desktop.Services
{
    public sealed class DocumentImportService
    {
        public List<ImportReference> GetParties(bool purchase) { using(var c=DatabaseConnectionFactory.CreateConnection()) { c.Open(); return DocumentImportRepository.Parties(c,purchase); } }
        public List<ImportReference> GetProducts() { using(var c=DatabaseConnectionFactory.CreateConnection()) { c.Open(); return DocumentImportRepository.Products(c); } }
        public string GetOriginal(string module,int recordId)
        {
            if(!new[]{"Quotations","Invoices","Purchases"}.Contains(module))throw new ArgumentException("Unsupported document module.");
            SessionManager.DemandPermission(module,"View");
            using(var c=DatabaseConnectionFactory.CreateConnection()){c.Open();DocumentImportRepository.EnsureSchema(c);return c.QuerySingleOrDefault<string>("SELECT FilePath FROM dbo.DocumentImports WHERE Module=@module AND RecordId=@recordId",new{module,recordId});}
        }
        public List<ImportSavedRecord> GetHistory()
        {
            using(var c=DatabaseConnectionFactory.CreateConnection()) { c.Open(); DocumentImportRepository.EnsureSchema(c); return DocumentImportRepository.History(c).Where(r=>SessionManager.HasPermission(r.Module,"View")).ToList(); }
        }
        public Task<int> SaveAsync(DocumentImportDraft draft,CancellationToken token)
        {
            new DocumentImportValidator().ValidateAndThrow(draft);
            ValidateModuleMapping(draft);
            string module=DocumentImportRepository.Module(draft.DocumentType);
            SessionManager.DemandPermission(module,"Create");
            // Snapshot the reviewed values on the UI thread; edits cannot race an in-flight save.
            var copy=JsonConvert.DeserializeObject<DocumentImportDraft>(JsonConvert.SerializeObject(draft));
            return Task.Run(() => Save(copy,token),token);
        }
        internal static void ValidateModuleMapping(DocumentImportDraft d)
        {
            var rules=new BusinessRuleEngine(); var calculations=new CalculationVerificationService();
            HVAC_Pro_Desktop.Models.Validation.ValidationResult result;
            if(d.DocumentType==ImportDocumentType.SalesInvoice)
            {
                var invoice=new Invoice {ClientID=d.PartyId,InvoiceNumber=d.DocumentNumber,InvoiceDate=d.DocumentDate.Value,DueDate=d.DueDate.Value,SubTotal=d.TaxableAmount.Value,TaxAmount=(d.CGST+d.SGST+d.IGST).Value,TotalAmount=d.Total.Value,RoundOff=d.RoundOff.Value,PaidAmount=0,PaymentStatus="Draft",
                    LineItems=d.Lines.Select(l=>new InvoiceLineItem {Description=l.Description,Quantity=l.Quantity.Value,Rate=l.Rate.Value,DiscountPercent=l.DiscountPercent.Value,GSTPercent=l.GSTPercent.Value,Amount=l.Taxable.Value,TaxAmount=l.Tax.Value,IsBillable=true}).ToList()};
                result=rules.ValidateInvoice(invoice); result.Merge(calculations.VerifyInvoice(invoice));
            }
            else if(d.DocumentType==ImportDocumentType.PurchaseInvoice)
            {
                var purchase=new PurchaseOrder {VendorID=d.PartyId,PONumber=d.DocumentNumber,PODate=d.DocumentDate.Value,PayByDate=d.DueDate.Value,TotalAmount=d.Total.Value,PaidAmount=0,Status="Pending",
                    LineItems=d.Lines.Select(l=>new PurchaseLineItem {Description=l.Description,Quantity=l.Quantity.Value,Rate=l.Rate.Value,GSTRate=l.GSTPercent.Value,Amount=(l.Taxable+l.Tax).Value}).ToList()};
                result=rules.ValidatePurchaseOrder(purchase); result.Merge(calculations.VerifyPurchaseOrder(purchase));
            }
            else
            {
                var quote=new TenderBid {ClientID=d.PartyId,QuotationNumber=d.DocumentNumber,TenderName=string.IsNullOrWhiteSpace(d.Subject)?"Imported quotation "+d.DocumentNumber:d.Subject,DueDate=d.DueDate.Value,SubmittedDate=d.DocumentDate,TotalTaxableValue=d.TaxableAmount.Value,TotalGSTAmount=(d.CGST+d.SGST+d.IGST).Value,TotalWithGST=d.Total.Value,Status="Draft",
                    LineItems=d.Lines.Select(l=>new TenderBidLineItem {ItemDescription=l.Description,Quantity=l.Quantity.Value,SellPricePerUnit=l.Rate.Value,GSTRatePct=l.GSTPercent.Value,TaxableLineTotal=l.Taxable.Value,GSTAmount=l.Tax.Value}).ToList()};
                new TenderBidValidator().ValidateAndThrow(quote);
                result=rules.ValidateQuotation(quote); result.Merge(calculations.VerifyQuotation(quote));
            }
            // Existing validators supply messages only, without logging the reviewed payload.
            if(result.HasErrors) throw new InvalidOperationException(string.Join(Environment.NewLine,result.Issues.Where(i=>i.Severity==HVAC_Pro_Desktop.Models.Validation.ValidationSeverity.Error||i.Severity==HVAC_Pro_Desktop.Models.Validation.ValidationSeverity.Critical).Select(i=>i.Message)));
        }
        internal static int Save(DocumentImportDraft d,CancellationToken token,string testVaultRoot = null)
        {
            new DocumentImportValidator().ValidateAndThrow(d);
            ValidateModuleMapping(d);
            if(testVaultRoot!=null && !(new System.Data.SqlClient.SqlConnectionStringBuilder(DatabaseConnectionFactory.DiagnosticConnectionString??"").InitialCatalog).StartsWith("ServoERP_DocumentImportQA_",StringComparison.Ordinal))
                throw new InvalidOperationException("A test vault requires a disposable import QA database.");
            var attempt=Guid.NewGuid();
            try
            {
                token.ThrowIfCancellationRequested();
                if(!new[]{".pdf",".jpg",".jpeg",".png",".xlsx"}.Contains(Path.GetExtension(d.SnapshotPath).ToLowerInvariant()))throw new InvalidOperationException("Unsupported source document format.");
                if(!File.Exists(d.SnapshotPath) || DocumentImportExtractionService.Hash(d.SnapshotPath)!=d.FileHash)
                    throw new InvalidOperationException("The source snapshot changed. Upload the original again.");
                // Content-addressed vault file is durable before SQL commit. A failed transaction can leave
                // an unregistered file, reused on retry; never delete a shared attachment during rollback.
                string vault=Path.Combine(testVaultRoot??Path.Combine(@"C:\HVAC_PRO_MSE","CLIENT_DATA_UPLOADS","Imports"),d.FileHash.Substring(0,2));
                Directory.CreateDirectory(vault);
                string attachment=Path.Combine(vault,d.FileHash+Path.GetExtension(d.SnapshotPath));
                if(!File.Exists(attachment))
                {
                    string staging=Path.Combine(vault,attempt.ToString("N")+".pending");
                    try { File.Copy(d.SnapshotPath,staging,false); try { File.Move(staging,attachment); } catch(IOException) { if(!File.Exists(attachment)) throw; } }
                    finally { if(File.Exists(staging)) File.Delete(staging); }
                }
                if(DocumentImportExtractionService.Hash(attachment)!=d.FileHash) throw new InvalidOperationException("Attachment integrity verification failed.");
                using(var c=DatabaseConnectionFactory.CreateConnection())
                {
                    c.Open(); DocumentImportRepository.EnsureSchema(c);
                    token.ThrowIfCancellationRequested();
                    using(var tx=c.BeginTransaction(IsolationLevel.Serializable))
                    {
                        int id=DocumentImportRepository.Save(c,tx,d,attachment);
                        token.ThrowIfCancellationRequested();
                        tx.Commit(); // Cancellation after this point must report success, never invite a duplicate save.
                        AppDataCache.RemovePrefix("invoices:"); AppDataCache.RemovePrefix("purchases:"); AppDataCache.RemovePrefix("masterdata:");
                        DashboardRefreshService.NotifyChanged(DocumentImportRepository.Module(d.DocumentType));
                        Serilog.Log.Information("Document import {AttemptId} saved to {Module} record {RecordId}",attempt,DocumentImportRepository.Module(d.DocumentType),id);
                        return id;
                    }
                }
            }
            catch(Exception ex)
            {
                // SQL exception text can contain document numbers or other sensitive values.
                Serilog.Log.Warning("Document import {AttemptId} failed with {ErrorType}",attempt,ex.GetType().Name);
                try { using(var c=DatabaseConnectionFactory.CreateConnection()) { c.Open(); DocumentImportRepository.EnsureSchema(c); c.Execute("INSERT dbo.DocumentImportAttempts(AttemptId,Stage,ErrorCode) VALUES(@attempt,'Save',@code)",new {attempt,code=ex.GetType().Name}); } } catch { }
                throw;
            }
        }
    }
}
