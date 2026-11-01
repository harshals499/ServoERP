using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using HVAC_Pro_Desktop.Models;
using Newtonsoft.Json;
using UglyToad.PdfPig;
using UglyToad.PdfPig.DocumentLayoutAnalysis.TextExtractor;

namespace HVAC_Pro_Desktop.Services
{
    // This service parses data only. No LLM, network, expression evaluation, macros or document instructions.
    public sealed class DocumentImportExtractionService : IDisposable
    {
        private readonly string _workspace = Path.Combine(Path.GetTempPath(), "ServoERP-Import-" + Guid.NewGuid().ToString("N"));
        private sealed class LocalPage { public int Page { get; set; } public string Text { get; set; } public string ImagePath { get; set; } }
        public Task<List<DocumentImportDraft>> ExtractAsync(string source, IProgress<string> progress, CancellationToken token)
            => Task.Run(async () =>
            {
                string extension = Path.GetExtension(source).ToLowerInvariant();
                if (!new[] { ".pdf", ".jpg", ".jpeg", ".png", ".xlsx" }.Contains(extension))
                    throw new InvalidOperationException("Choose PDF, JPG or PNG. Use the existing reviewed workbook import for Excel/CSV.");
                if (!File.Exists(source) || new FileInfo(source).Length > 20 * 1024 * 1024)
                    throw new InvalidOperationException("Choose a readable document no larger than 20 MB.");
                string dir = Path.Combine(_workspace, Guid.NewGuid().ToString("N"));
                Directory.CreateDirectory(dir);
                string snapshot = Path.Combine(dir, "original" + extension);
                using (var input = new FileStream(source, FileMode.Open, FileAccess.Read, FileShare.Read))
                using (var output = File.Create(snapshot)) await input.CopyToAsync(output, 81920, token).ConfigureAwait(false);
                string hash = Hash(snapshot);
                if(extension==".xlsx")
                {
                    progress?.Report("Reading Madhusuman's historical workbook layout locally; formulas and payment actions are not executed…");
                    var historical=MseHistoricalDocumentProfile.ReadWorkbook(snapshot,token);
                    foreach(var draft in historical){draft.SnapshotPath=snapshot;draft.FileHash=hash;draft.OriginalFileName=Path.GetFileName(source);}
                    return historical;
                }
                progress?.Report("Rendering source pages and reading local OCR…");
                string script = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "SERVICES", "DocumentImport.LocalOcr.ps1");
                var start = new ProcessStartInfo(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.System), "WindowsPowerShell", "v1.0", "powershell.exe"),
                    "-NoProfile -NonInteractive -ExecutionPolicy Bypass -File " + Quote(script) + " -InputPath " + Quote(snapshot) + " -OutputDirectory " + Quote(dir))
                { UseShellExecute = false, CreateNoWindow = true, RedirectStandardOutput = true, RedirectStandardError = true };
                using (var process = Process.Start(start))
                using (token.Register(() => { try { if (!process.HasExited) process.Kill(); } catch (InvalidOperationException) { } }))
                {
                    // Drain both pipes; never put process output (which can contain source text) in logs.
                    var stdout = process.StandardOutput.ReadToEndAsync();
                    var stderr = process.StandardError.ReadToEndAsync();
                    var elapsed=Stopwatch.StartNew();
                    while (!process.HasExited)
                    {
                        token.ThrowIfCancellationRequested();
                        if(elapsed.Elapsed>TimeSpan.FromMinutes(5))
                        {
                            try{process.Kill();}catch(InvalidOperationException){}
                            throw new InvalidOperationException("Local OCR exceeded five minutes. Split the document into smaller files or retry after checking Windows OCR support. No records were saved.");
                        }
                        await Task.Delay(100, token).ConfigureAwait(false);
                    }
                    await Task.WhenAll(stdout, stderr).ConfigureAwait(false);
                    token.ThrowIfCancellationRequested();
                    if (process.ExitCode != 0) throw new InvalidOperationException("Local document rendering/OCR failed. Check the file is readable, unencrypted and Windows OCR language support is installed. No records were saved.");
                }
                var pages = JsonConvert.DeserializeObject<List<LocalPage>>(File.ReadAllText(Path.Combine(dir, "pages.json"))) ?? new List<LocalPage>();
                if (extension == ".pdf")
                {
                    using (var pdf = PdfDocument.Open(snapshot))
                        foreach (var page in pages)
                        {
                            token.ThrowIfCancellationRequested();
                            string text = ContentOrderTextExtractor.GetText(pdf.GetPage(page.Page));
                            if (!string.IsNullOrWhiteSpace(text) && text.Count(char.IsLetterOrDigit) > 30) page.Text = text;
                        }
                }
                var drafts = new List<DocumentImportDraft>();
                foreach (var page in pages)
                {
                    token.ThrowIfCancellationRequested();
                    var draft = Parse(page.Text ?? "", page.Page);
                    if(extension==".pdf")MseHistoricalDocumentProfile.ApplyTallyInvoice(draft);
                    draft.SnapshotPath = snapshot; draft.FileHash = hash; draft.OriginalFileName = Path.GetFileName(source); draft.PreviewPath = page.ImagePath;
                    draft.PreviewPaths.Add(page.ImagePath);
                    drafts.Add(draft);
                }
                if(extension==".pdf")using(var pdf=PdfDocument.Open(snapshot))foreach(var draft in drafts)MseHistoricalDocumentProfile.ApplyPrintablePdfQuotation(draft,pdf.GetPage(draft.FirstPage));
                progress?.Report("Review every page boundary, field and line. No business records have been saved.");
                return drafts;
            }, token);

        public static DocumentImportDraft Parse(string text, int page)
        {
            var draft = new DocumentImportDraft { FirstPage = page, LastPage = page, SourceText = text, ExtractionStatus = string.IsNullOrWhiteSpace(text) ? "NoText" : "LocalReviewRequired" };
            string lower = text.ToLowerInvariant();
            // Ordering prevents credit notes and purchase orders from being mistaken for invoices.
            var types = new Dictionary<string, ImportDocumentType> { {"credit note",ImportDocumentType.CreditNote},{"debit note",ImportDocumentType.DebitNote},{"delivery challan",ImportDocumentType.DeliveryChallan},{"purchase order",ImportDocumentType.PurchaseOrder},{"receipt",ImportDocumentType.Receipt},{"quotation",ImportDocumentType.Quotation},{"tax invoice",ImportDocumentType.Unknown},{"invoice",ImportDocumentType.Unknown} };
            foreach (var type in types) if (lower.Contains(type.Key)) { draft.DocumentType = type.Value; Evidence(draft,"Document type","Suggested",page,type.Key); break; }
            if(Regex.IsMatch(text,@"^\s*sales\s+(?:tax\s+)?invoice\s*$",RegexOptions.IgnoreCase|RegexOptions.Multiline)) { draft.DocumentType=ImportDocumentType.SalesInvoice;draft.Direction=ImportDirection.Sales; }
            if(Regex.IsMatch(text,@"^\s*(?:purchase|vendor)\s+invoice\s*$",RegexOptions.IgnoreCase|RegexOptions.Multiline)) { draft.DocumentType=ImportDocumentType.PurchaseInvoice;draft.Direction=ImportDirection.Purchase; }
            draft.DocumentNumber = Capture(draft, "Document number", @"(?:quotation|quote|invoice|document)\s*(?:no\.?|number|#)\s*[:#\-]?\s*([A-Z0-9][A-Z0-9/_.\-]{0,49})", text, page);
            string date = Capture(draft,"Document date",@"^\s*(?:invoice\s*date|quotation\s*date|date)\s*[:\-]?\s*(\d{1,2}[/-]\d{1,2}[/-]\d{4})",text,page);
            DateTime parsed;
            if (DateTime.TryParseExact(date,new[]{"d/M/yyyy","dd/MM/yyyy","d-M-yyyy","dd-MM-yyyy"},CultureInfo.GetCultureInfo("en-IN"),DateTimeStyles.None,out parsed)) draft.DocumentDate = parsed;
            string due=Capture(draft,"Due date",@"(?:due\s*date|pay\s*by)\s*[:\-]?\s*(\d{1,2}[/-]\d{1,2}[/-]\d{4})",text,page);
            if(DateTime.TryParseExact(due,new[]{"d/M/yyyy","dd/MM/yyyy","d-M-yyyy","dd-MM-yyyy"},CultureInfo.GetCultureInfo("en-IN"),DateTimeStyles.None,out parsed))draft.DueDate=parsed;
            draft.GSTIN = Capture(draft,"GSTIN (role unconfirmed)",@"\b(\d{2}[A-Z]{5}\d{4}[A-Z][A-Z0-9]Z[A-Z0-9])\b",text,page);
            draft.PartyName = Capture(draft,"Party (role unconfirmed)",@"(?:bill\s*to|buyer|customer|vendor|supplier)\s*[:\-]\s*([^\r\n]+)",text,page);
            draft.Address=Capture(draft,"Address (role unconfirmed)",@"(?:billing\s*address|address)\s*[:\-]\s*([^\r\n]+)",text,page);
            draft.Reference=Capture(draft,"Reference",@"(?:PO\s*(?:no\.?|number)|reference)\s*[:#\-]\s*([^\r\n]+)",text,page);
            draft.PaymentDetails=Capture(draft,"Payment details (unposted)",@"(?:payment\s*(?:reference|details)|UTR|transaction\s*(?:id|number))\s*[:#\-]\s*([^\r\n]+)",text,page);
            draft.TaxableAmount = Money(draft,"Taxable amount",@"(?:total\s*taxable(?:\s*(?:value|amount))?|taxable\s*(?:value|amount)|sub\s*total)",text,page);
            draft.CGST = Money(draft,"CGST",@"CGST(?:\s*amount)?",text,page);
            draft.SGST = Money(draft,"SGST",@"SGST(?:\s*amount)?",text,page);
            draft.IGST = Money(draft,"IGST",@"IGST(?:\s*amount)?",text,page);
            draft.RoundOff = Money(draft,"Round-off",@"round[ -]*off",text,page);
            draft.Total = Money(draft,"Total",@"(?:grand\s*total|net\s*(?:total|payable)|total\s*amount)",text,page);
            // Only explicit delimited item rows are parsed. Arbitrary OCR tables remain for human correction.
            foreach (string line in text.Split('\n'))
            {
                var cells = line.Trim().Split('|').Select(c=>c.Trim()).ToArray();
                decimal qty,rate,gst,taxable,tax,discount;
                if (cells.Length == 9 && decimal.TryParse(cells[2],NumberStyles.Number,CultureInfo.GetCultureInfo("en-IN"),out qty)
                    && Decimal(cells[4],out rate) && Decimal(cells[5],out discount) && Decimal(cells[6],out gst) && Decimal(cells[7],out taxable) && Decimal(cells[8],out tax))
                {
                    draft.Lines.Add(new DocumentImportLine { Description=cells[0],HSN=cells[1],Quantity=qty,Unit=cells[3],Rate=rate,DiscountPercent=discount,GSTPercent=gst,Taxable=taxable,Tax=tax });
                    Evidence(draft,"Line item","Review required",page,line.Trim());
                }
            }
            if(draft.Lines.Count==0) ParseDelimitedTable(draft,text,page);
            Evidence(draft,"Review","Required",page,"Direction, party role, missing values, items and page boundaries require confirmation. OCR can misread numbers. No confidence score guarantees accuracy.");
            return draft;
        }
        private static void ParseDelimitedTable(DocumentImportDraft draft,string text,int page)
        {
            string[] headers=null;
            var aliases=new Dictionary<string,string>(StringComparer.OrdinalIgnoreCase) {{"description","Description"},{"itemdescription","Description"},{"item","Description"},{"hsn","HSN"},{"hsnsac","HSN"},{"qty","Quantity"},{"quantity","Quantity"},{"unit","Unit"},{"uom","Unit"},{"rate","Rate"},{"unitprice","Rate"},{"discount%","DiscountPercent"},{"discountpercent","DiscountPercent"},{"gst%","GSTPercent"},{"gstpercent","GSTPercent"},{"taxable","Taxable"},{"taxableamount","Taxable"},{"taxablevalue","Taxable"},{"tax","Tax"},{"taxamount","Tax"}};
            foreach(string row in text.Split('\n'))
            {
                string[] cells=Regex.Split(row.Trim(),@"\s*\|\s*|\t+| {2,}");
                var names=cells.Select(c=>{string key=Regex.Replace(c,@"[\s/_.]","");return aliases.ContainsKey(key)?aliases[key]:null;}).ToArray();
                if(names.Contains("Description")&&names.Contains("Quantity")&&names.Contains("Rate")){headers=names;continue;}
                if(headers==null||cells.Length!=headers.Length)continue;
                var line=new DocumentImportLine();bool hasQuantity=false;
                for(int i=0;i<headers.Length;i++)
                {
                    if(headers[i]==null)continue;var property=typeof(DocumentImportLine).GetProperty(headers[i]);
                    if(property.PropertyType==typeof(string))property.SetValue(line,cells[i]);
                    else {decimal number;if(Decimal(cells[i].TrimEnd('%'),out number)){property.SetValue(line,(decimal?)number);if(headers[i]=="Quantity")hasQuantity=true;}}
                }
                if(hasQuantity&&!string.IsNullOrWhiteSpace(line.Description)){draft.Lines.Add(line);Evidence(draft,"Table row","Suggested — verify",page,row.Trim());}
            }
        }
        private static bool Decimal(string value,out decimal number) => decimal.TryParse(value,NumberStyles.Number,CultureInfo.GetCultureInfo("en-IN"),out number);
        private static decimal? Money(DocumentImportDraft d,string field,string label,string text,int page)
        {
            string value=Capture(d,field,@"(?:"+label+@")\s*[:=]?\s*(?:INR|Rs\.?|₹)?\s*([\-]?[\d,]+\.\d{2})\s*(?:\r?$)",text,page);
            decimal number; return Decimal(value,out number)? number:(decimal?)null;
        }
        private static string Capture(DocumentImportDraft d,string field,string pattern,string text,int page)
        {
            var matches=Regex.Matches(text,pattern,RegexOptions.IgnoreCase|RegexOptions.Multiline,TimeSpan.FromSeconds(1));
            if(matches.Count!=1) { if(matches.Count>1) Evidence(d,field,"Conflicting candidates",page,"Multiple source matches; enter the correct value."); return null; }
            Evidence(d,field,"Suggested — verify",page,matches[0].Value); return matches[0].Groups[1].Value.Trim();
        }
        private static void Evidence(DocumentImportDraft d,string field,string confidence,int page,string source) => d.Evidence.Add(new ImportFieldEvidence {Field=field,Confidence=confidence,Page=page,Source=source});
        public static string Hash(string path) { using(var input=File.OpenRead(path)) using(var sha=SHA256.Create()) return BitConverter.ToString(sha.ComputeHash(input)).Replace("-","").ToLowerInvariant(); }
        private static string Quote(string value) { if(value.Contains("\"")) throw new ArgumentException("Invalid path."); return "\""+value+"\""; }
        public void Dispose() { try { if(Directory.Exists(_workspace)) Directory.Delete(_workspace,true); } catch(IOException) { } catch(UnauthorizedAccessException) { } }
    }
}
