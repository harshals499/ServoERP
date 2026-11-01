using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using HVAC_Pro_Desktop.Services;
using HVAC_Pro_Desktop.Models;
using ServoERP.Validators;

namespace HVAC_Pro_Desktop.Tests
{
    public static class MseHistoricalImportTests
    {
        // Read-only original-file regressions. No business connection, imports or master creation.
        public static string Run(string downloads,string pdfDirectory)
        {
            var result=new List<string>();
            using(var extractor=new DocumentImportExtractionService())
            {
                var quotes=Directory.GetFiles(downloads,"Quotation for MSE-Zydus-*.xlsx");
                Require(quotes.Length>=10,"Historical quotation fixtures unavailable.");
                foreach(string path in quotes)
                {
                    string hash=DocumentImportExtractionService.Hash(path);
                    var docs=extractor.ExtractAsync(path,null,CancellationToken.None).GetAwaiter().GetResult();
                    Require(docs.Count==1&&docs[0].DocumentType==ImportDocumentType.Quotation&&docs[0].Direction==ImportDirection.Sales,"MSE quotation type mapping failed.");
                    var q=docs[0];Require(q.DocumentNumber.StartsWith("MSE/")&&q.DocumentDate.HasValue&&q.Lines.Count>0&&!string.IsNullOrWhiteSpace(q.Subject),"Historical quotation fields missing.");
                    Require(q.Total.HasValue&&q.TaxableAmount.HasValue&&q.CGST.HasValue&&q.SGST.HasValue,"Historical quotation totals missing.");
                    if(Path.GetFileName(path).Contains("01 Supply of Compressor"))Require(q.TaxableAmount==73751.8m&&q.Lines[0].Rate==69606.05m,"Displayed whole-rupee formatting silently changed cached source money.");
                    Require(q.Evidence.Any(e=>e.Source.Contains("!"))&&!q.ReviewConfirmed&&q.PartyId==0,"Source-cell evidence or review guard missing.");
                    Require(DocumentImportExtractionService.Hash(path)==hash,"Historical original changed.");
                }
                result.Add("PASS ten original MSE quotations: header/customer/location/item/cell evidence and totals, source files unchanged");
                string bdb=Path.Combine(Path.GetDirectoryName(downloads),"Documents","Quotation for MSE-BDB-05-2026-27-Supply, Installation & Commissioning of Split unit work.xlsx");
                var bdbQuote=extractor.ExtractAsync(bdb,null,CancellationToken.None).GetAwaiter().GetResult().Single();
                Require(bdbQuote.Lines.Count>=3&&bdbQuote.DocumentNumber!=null&&bdbQuote.Lines.All(l=>!string.IsNullOrWhiteSpace(l.Unit)),"Original Unit/Qty quotation variant failed.");
                result.Add("PASS original BDB quotation: Unit/Qty headers, sectioned items and source price precision retained");
                var printable=extractor.ExtractAsync(Path.Combine(downloads,"mse template quotation.pdf"),null,CancellationToken.None).GetAwaiter().GetResult().Single();
                Require(printable.DocumentType==ImportDocumentType.Quotation&&printable.DocumentNumber!=null&&printable.DocumentDate.HasValue&&printable.Lines.Count==2,"Original printable PDF quotation mapping failed.");
                Require(printable.TaxableAmount==96460&&printable.Total==113822&&printable.CGST==8681&&printable.SGST==8681&&printable.GSTIN==null,"Original PDF total labels or seller GST were mis-mapped.");
                result.Add("PASS original MSE PDF quotation: positioned multiline items and reversed taxable/grand-total labels; seller GST not assigned to customer");
                foreach(string file in new[]{"Sales Tax Invoice from April 2025 to March 2026.xlsx","Purchase Details April 2025 to March 2026.xlsx"})
                {
                    string path=Path.Combine(downloads,file),hash=DocumentImportExtractionService.Hash(path);
                    var docs=extractor.ExtractAsync(path,null,CancellationToken.None).GetAwaiter().GetResult();
                    Require(docs.Count>100,"Historical register rows were not found.");
                    Require(docs.All(d=>d.SourceLocation.StartsWith(file.StartsWith("Sales")?"Sales ·":"Purchases ·")&&d.Lines.Count==0&&d.PartyId==0&&!d.ReviewConfirmed),"Register copied wrong/master tabs or invented lines/master records.");
                    Require(docs.Any(d=>!string.IsNullOrWhiteSpace(d.PaymentDetails)),"Historical payment evidence was lost.");
                    Require(docs.Select(d=>d.FirstPage).Distinct().Count()==docs.Count,"Register retry identity collisions.");
                    Require(DocumentImportExtractionService.Hash(path)==hash,"Historical register modified.");
                    result.Add("PASS original "+(file.StartsWith("Sales")?"Sales":"Purchases")+" register: "+docs.Count+" rows, tax buckets, payment evidence, no invented items, wrong/master/proforma tabs excluded");
                }
                var pdfs=Directory.GetFiles(pdfDirectory,"*.pdf");Require(pdfs.Length==2,"Original Tally fixture pair unavailable.");
                foreach(string pdf in pdfs)
                {
                    string hash=DocumentImportExtractionService.Hash(pdf);
                    var docs=extractor.ExtractAsync(pdf,null,CancellationToken.None).GetAwaiter().GetResult();
                    var invoice=docs.Single();Require(invoice.DocumentType==ImportDocumentType.SalesInvoice&&invoice.Direction==ImportDirection.Sales,"Tally seller direction failed.");
                    Require(invoice.DocumentNumber!=null&&invoice.DocumentDate.HasValue&&invoice.PartyName!=null&&invoice.GSTIN!=null&&invoice.Total.HasValue&&invoice.TaxableAmount.HasValue&&invoice.IGST.HasValue&&invoice.RoundOff.HasValue&&invoice.Lines.Count>0,"Tally field extraction incomplete.");
                    Require(invoice.Lines.All(l=>l.Quantity.HasValue&&l.Rate.HasValue&&l.Taxable.HasValue&&l.HSN!=null),"Tally item columns were lost.");
                    Require(!new DocumentImportValidator().Validate(invoice).IsValid,"Unreviewed historic invoice accepted.");
                    Require(DocumentImportExtractionService.Hash(pdf)==hash,"Original Tally PDF changed.");
                }
                result.Add("PASS two original Tally e-invoices: seller/buyer distinction, dates, HSN/qty/rate/item amount, GST and signed round-off; originals unchanged");
            }
            return string.Join(Environment.NewLine,result);
        }
        private static void Require(bool value,string message){if(!value)throw new InvalidOperationException(message);}
    }
}
