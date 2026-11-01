using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using HVAC_Pro_Desktop.Models;
using OfficeOpenXml;
using UglyToad.PdfPig.Content;

namespace HVAC_Pro_Desktop.Services
{
    /// <summary>Read-only mappings of Madhusuman's actual printable quotations and Sales/Purchases registers.
    /// Uses cached/displayed source cells; never recalculates formulas, invents item splits or executes workbook content.</summary>
    internal static class MseHistoricalDocumentProfile
    {
        private static readonly CultureInfo India=CultureInfo.GetCultureInfo("en-IN");
        internal static void ApplyPrintablePdfQuotation(DocumentImportDraft draft,Page page)
        {
            var rows=new List<List<Word>>();
            foreach(var word in page.GetWords().OrderByDescending(w=>w.BoundingBox.Bottom))
            {
                var row=rows.LastOrDefault();
                if(row==null||Math.Abs(row[0].BoundingBox.Bottom-word.BoundingBox.Bottom)>3){row=new List<Word>();rows.Add(row);}row.Add(word);
            }
            var physical=new StringBuilder();
            foreach(var row in rows)
            {
                double right=0;
                foreach(var word in row.OrderBy(w=>w.BoundingBox.Left)){physical.Append(word.BoundingBox.Left-right>8?"  ":" ").Append(word.Text);right=word.BoundingBox.Right;}
                physical.AppendLine();
            }
            string text=physical.ToString();
            if(!Regex.IsMatch(text,@"Quotation No\s*:",RegexOptions.IgnoreCase)||!text.Contains("Madhusuman Enterprises"))return;
            draft.DocumentType=ImportDocumentType.Quotation;draft.Direction=ImportDirection.Sales;draft.ExtractionStatus="MsePrintablePdfReviewRequired";
            var number=Regex.Match(text,@"Quotation No\s*:\s*([^\r\n]+)",RegexOptions.IgnoreCase);if(number.Success)draft.DocumentNumber=number.Groups[1].Value.Trim();
            var date=Regex.Match(text,@"Quotation Date\s*:\s*(\d{1,2}-\d{1,2}-\d{4})",RegexOptions.IgnoreCase);if(date.Success)draft.DocumentDate=Date(date.Groups[1].Value);
            var buyer=Regex.Match(text,@"^\s*(\S[^\r\n]+?)\s{2,}From:\s*$",RegexOptions.Multiline);
            if(buyer.Success)draft.PartyName=buyer.Groups[1].Value.Trim();
            // This original PDF uses Total Rs. for taxable and Total Amount for grand total,
            // the reverse of the printable workbook. Keep distinct verified profiles.
            draft.TaxableAmount=LayoutAmount(text,@"^\s*Total Rs\.\s{2,}([\d, ]+)\s*$");
            draft.Total=LayoutAmount(text,@"^\s*Total Amount\s{2,}([\d, ]+)\s*$");
            draft.CGST=LayoutAmount(text,@"^\s*Add:\s*CGST\s*@\s*\d+(?:\.\d+)?%\s{2,}([\d, ]+)\s*$");
            draft.SGST=LayoutAmount(text,@"^\s*Add:\s*SGST\s*@\s*\d+(?:\.\d+)?%\s{2,}([\d, ]+)\s*$");
            draft.GSTIN=null; // The visible GST NUMBER belongs to the seller, not the customer.
            var subject=Regex.Match(text,@"^\s*Sub:\s*([^\r\n]+)",RegexOptions.Multiline|RegexOptions.IgnoreCase);if(subject.Success)draft.Subject=subject.Groups[1].Value.Trim();
            draft.Lines.Clear();bool table=false;string pending="";
            foreach(string line in text.Split('\n'))
            {
                if(line.Contains("Description")&&line.Contains("Qty")){table=true;continue;}
                if(!table)continue;if(Regex.IsMatch(line,@"^\s*Total Rs\."))break;
                if(line.Trim()=="Code"||string.IsNullOrWhiteSpace(line))continue;
                var item=Regex.Match(line,@"^\s*\d+\s{2,}(.+?)\s{2,}(\d{4,8})\s{2,}([A-Za-z]+)\s{2,}([\d,.]+)\s{2,}([\d,.]+)\s{2,}([\d,.]+)\s*$");
                if(item.Success)
                {
                    decimal qty,rate,amount;if(!decimal.TryParse(item.Groups[4].Value,NumberStyles.Number,India,out qty)||!decimal.TryParse(item.Groups[5].Value,NumberStyles.Number,India,out rate)||!decimal.TryParse(item.Groups[6].Value,NumberStyles.Number,India,out amount))continue;
                    draft.Lines.Add(new DocumentImportLine{Description=pending+item.Groups[1].Value.Trim(),HSN=item.Groups[2].Value,Unit=item.Groups[3].Value,Quantity=qty,Rate=rate,Taxable=amount});pending="";
                    draft.Evidence.Add(new ImportFieldEvidence{Field="MSE PDF item",Page=draft.FirstPage,Confidence="Positioned source row — verify",Source=line.Trim()});
                }
                else if(draft.Lines.Count==0)pending+=line.Trim()+" ";
                else draft.Lines.Last().Description+=" "+line.Trim();
            }
            draft.Evidence.Add(new ImportFieldEvidence{Field="MSE printable PDF",Page=draft.FirstPage,Confidence="Matched — review required",Source="Verified historical layout: Total Rs. is taxable, Total Amount is grand total. Source whole-rupee GST is retained; discrepancies with line calculations must be reconciled explicitly. Seller GST is not mapped to buyer."});
        }
        private static decimal? LayoutAmount(string text,string pattern){var match=Regex.Match(text,pattern,RegexOptions.Multiline|RegexOptions.IgnoreCase);decimal value;return match.Success&&decimal.TryParse(match.Groups[1].Value.Replace(" ",""),NumberStyles.Number,India,out value)?value:(decimal?)null;}
        internal static bool ApplyTallyInvoice(DocumentImportDraft d)
        {
            string text=d.SourceText??"";
            int consignee=text.IndexOf("Consignee (Ship to)",StringComparison.OrdinalIgnoreCase);
            int buyer=text.IndexOf("Buyer (Bill to)",StringComparison.OrdinalIgnoreCase);
            if(consignee<0||buyer<0||!text.Substring(0,consignee).Contains("Madhusuman Enterprises")||!text.Contains("Invoice No."))return false;
            d.DocumentType=ImportDocumentType.SalesInvoice;d.Direction=ImportDirection.Sales;d.ExtractionStatus="MseTallyReviewRequired";
            d.Evidence.RemoveAll(e=>e.Field=="Document type"||e.Field=="GSTIN (role unconfirmed)"||e.Field=="Party (role unconfirmed)");
            d.Evidence.Add(new ImportFieldEvidence{Field="Historical profile",Confidence="Matched — verify",Page=d.FirstPage,Source="Madhusuman seller block, Tally Tax Invoice, Consignee/Buyer blocks and Invoice No. layout. All fields still require approval."});
            var number=Regex.Match(text,@"Invoice No\.\s*\r?\n([^\r\n]+)",RegexOptions.IgnoreCase);
            if(number.Success)d.DocumentNumber=number.Groups[1].Value.Trim();
            var date=Regex.Match(text,@"^Dated\s*\r?\n(\d{1,2}-[A-Za-z]{3}-\d{2,4})",RegexOptions.Multiline);
            if(date.Success)d.DocumentDate=Date(date.Groups[1].Value);
            int end=text.IndexOf("Invoice No.",buyer,StringComparison.OrdinalIgnoreCase);
            string block=text.Substring(buyer,end>buyer?end-buyer:text.Length-buyer);
            var lines=block.Split(new[]{'\r','\n'},StringSplitOptions.RemoveEmptyEntries).Skip(1).ToArray();
            if(lines.Length>0)d.PartyName=lines[0].Trim();
            d.Address=string.Join(Environment.NewLine,lines.Skip(1).TakeWhile(l=>!l.StartsWith("GSTIN",StringComparison.OrdinalIgnoreCase)));
            var gstin=Regex.Match(block,@"GSTIN/UIN\s*:\s*([A-Z0-9]{15})");d.GSTIN=gstin.Success?gstin.Groups[1].Value:null;
            var reference=Regex.Match(text,@"Buyer's Order No\.\s*\r?\n([^\r\n]+)",RegexOptions.IgnoreCase);if(reference.Success)d.Reference=reference.Groups[1].Value.Trim();
            d.Total=TextAmount(text,@"^Total\s*[₹]\s*([\d,]+\.\d{2})(?:\s*\d+\.\d{3}\s+[A-Za-z]+)?\s*$");
            d.IGST=TextAmount(text,@"^IGST\s+([\d,]+\.\d{2})\s*$");
            d.CGST=TextAmount(text,@"^CGST\s+([\d,]+\.\d{2})\s*$");
            d.SGST=TextAmount(text,@"^SGST\s+([\d,]+\.\d{2})\s*$");
            var round=Regex.Match(text,@"Round Off\s*(\(-\)|-)?\s*([\d,]+\.\d{2})",RegexOptions.IgnoreCase);
            decimal parsed;if(round.Success&&decimal.TryParse(round.Groups[2].Value,NumberStyles.Number,India,out parsed))d.RoundOff=round.Groups[1].Success?-parsed:parsed;
            // PdfPig's content order for this verified Tally layout is Amount, per, Rate, Quantity, UOM, HSN.
            // Do not use the same pattern for unrelated supplier layouts or OCR text.
            var items=Regex.Matches(text,@"^\s*\d+\s+(.+?)\s+([\d,]+\.\d{2})([A-Za-z]+)([\d,]+\.\d{2})([\d,]+\.\d{3})\s+([A-Za-z]+)(\d{4,8})\s*$",RegexOptions.Multiline);
            var rates=Regex.Matches(text,@"\b(\d{1,2}(?:\.\d+)?)%").Cast<Match>().Select(m=>m.Groups[1].Value).Distinct().ToList();
            if(items.Count>0)
            {
                d.Lines.Clear();
                foreach(Match item in items)
                {
                    decimal amount,rate,quantity; if(!decimal.TryParse(item.Groups[2].Value,NumberStyles.Number,India,out amount)||!decimal.TryParse(item.Groups[4].Value,NumberStyles.Number,India,out rate)||!decimal.TryParse(item.Groups[5].Value,NumberStyles.Number,India,out quantity))continue;
                    decimal gst;decimal? gstRate=rates.Count==1&&decimal.TryParse(rates[0],NumberStyles.Number,India,out gst)?gst:(decimal?)null;
                    d.Lines.Add(new DocumentImportLine{Description=item.Groups[1].Value,HSN=item.Groups[7].Value,Quantity=quantity,Unit=item.Groups[6].Value,Rate=rate,Taxable=amount,GSTPercent=gstRate});
                    d.Evidence.Add(new ImportFieldEvidence{Field="Tally item",Confidence="Source text — verify",Page=d.FirstPage,Source=item.Value.Trim()});
                }
                var beforeTax=Regex.Match(text,@"^([\d,]+\.\d{2})\s*\r?\n(?:IGST|CGST|SGST)\s",RegexOptions.Multiline);
                if(beforeTax.Success&&decimal.TryParse(beforeTax.Groups[1].Value,NumberStyles.Number,India,out parsed))d.TaxableAmount=parsed;
                else if(d.Lines.Count==1)d.TaxableAmount=d.Lines[0].Taxable;
            }
            d.Evidence.Add(new ImportFieldEvidence{Field="Unspecified fields",Confidence="Review required",Page=d.FirstPage,Source="Source discount cells, missing tax jurisdictions, due date and per-line GST amounts are not inferred. Review missing fields; the original Tally PDF is unchanged."});
            return true;
        }
        private static decimal? TextAmount(string text,string pattern){var m=Regex.Match(text,pattern,RegexOptions.Multiline);decimal value;return m.Success&&decimal.TryParse(m.Groups[1].Value,NumberStyles.Number,India,out value)?value:(decimal?)null;}
        internal static List<DocumentImportDraft> ReadWorkbook(string snapshot,CancellationToken token)
        {
            var result=new List<DocumentImportDraft>();
            using(var package=new ExcelPackage(new FileInfo(snapshot)))
            {
                foreach(var sheet in package.Workbook.Worksheets)
                {
                    token.ThrowIfCancellationRequested();
                    if(sheet.Dimension==null)continue;
                    // Explicitly exclude archived, wrong-data, master, proforma and tracking tabs.
                    string name=sheet.Name.Trim();
                    if(name.Equals("Sales",StringComparison.OrdinalIgnoreCase)||name.Equals("Purchases",StringComparison.OrdinalIgnoreCase))
                        ReadRegister(sheet,result,token);
                    else
                    {
                        var quote=ReadQuotation(sheet);
                        if(quote!=null)result.Add(quote);
                    }
                }
            }
            if(result.Count==0)throw new InvalidOperationException("This workbook does not match the verified Madhusuman quotation or Sales/Purchases register layouts. Choose an original supported sheet; no rows were imported.");
            return result;
        }
        private static DocumentImportDraft ReadQuotation(ExcelWorksheet sheet)
        {
            ExcelRangeBase number=null,date=null,to=null;
            for(int r=1;r<=Math.Min(80,sheet.Dimension.End.Row);r++)for(int c=1;c<=Math.Min(12,sheet.Dimension.End.Column);c++)
            {
                var cell=sheet.Cells[r,c];string value=cell.Text;
                if(Regex.IsMatch(value,@"^\s*Quotation\s*No\s*:",RegexOptions.IgnoreCase))number=cell;
                if(Regex.IsMatch(value,@"^\s*Quotation\s*Date\s*:",RegexOptions.IgnoreCase))date=cell;
                if(Key(value)=="TO")to=cell;
            }
            if(number==null||date==null||to==null)return null;
            int header=0,desc=0,unit=0,qty=0;var rates=new List<int>();var amounts=new List<int>();
            for(int row=to.Start.Row;row<=Math.Min(80,sheet.Dimension.End.Row);row++)
            {
                desc=Column(sheet,row,"DESCRIPTION");unit=Column(sheet,row,"UOM");if(unit==0)unit=Column(sheet,row,"UNIT");qty=Column(sheet,row,"QUANTITY");if(qty==0)qty=Column(sheet,row,"QTY");
                if(desc==0||unit==0||qty==0)continue;
                int rateRow=row;
                if(ColumnPrefix(sheet,row,"RATE")==0)rateRow++;
                for(int col=qty+1;col<=Math.Min(12,sheet.Dimension.End.Column);col++)
                {
                    if(Key(sheet.Cells[rateRow,col].Text).StartsWith("RATE"))rates.Add(col);
                    if(Key(sheet.Cells[rateRow,col].Text).StartsWith("AMOUNT"))amounts.Add(col);
                }
                if(rates.Count==0||rates.Count!=amounts.Count)return null;
                header=rateRow;break;
            }
            if(header==0)return null;
            var d=NewDraft(sheet,header,ImportDocumentType.Quotation);
            d.DocumentNumber=AfterColon(number.Text);d.DocumentDate=Date(AfterColon(date.Text));
            for(int row=1;row<header;row++)for(int col=1;col<=Math.Min(4,sheet.Dimension.End.Column);col++)if(Regex.IsMatch(sheet.Cells[row,col].Text,@"^\s*Sub:",RegexOptions.IgnoreCase)){d.Subject=AfterColon(sheet.Cells[row,col].Text);Cite(d,"Subject",sheet,sheet.Cells[row,col]);}
            Cite(d,"Quotation number",sheet,number);Cite(d,"Quotation date",sheet,date);
            for(int row=to.Start.Row+1;row<header;row++)
            {
                string text=sheet.Cells[row,to.Start.Column].Text.Trim();
                if(string.IsNullOrEmpty(text)||Key(text).StartsWith("SUB"))continue;
                var lines=text.Split(new[]{'\r','\n'},StringSplitOptions.RemoveEmptyEntries);
                if(lines.Length>0){d.PartyName=lines[0].Trim();d.Address=string.Join(Environment.NewLine,lines.Skip(1));Cite(d,"Customer/address",sheet,sheet.Cells[row,to.Start.Column]);break;}
            }
            decimal? cgstRate=null,sgstRate=null;bool ended=false;string section=null;
            for(int row=header+1;row<=Math.Min(sheet.Dimension.End.Row,1000);row++)
            {
                string label=sheet.Cells[row,1].Text.Trim();string key=Key(label);
                if(key=="TOTAL"||key=="TOTALAMOUNT") {d.TaxableAmount=RightmostAmount(sheet,row,amounts);ended=true;Cite(d,"Taxable total",sheet,sheet.Cells[row,amounts.Last()]);continue;}
                if(key.StartsWith("CGST")){d.CGST=RightmostAmount(sheet,row,amounts);cgstRate=Percent(label);Cite(d,"CGST",sheet,sheet.Cells[row,amounts.Last()]);continue;}
                if(key.StartsWith("SGST")){d.SGST=RightmostAmount(sheet,row,amounts);sgstRate=Percent(label);Cite(d,"SGST",sheet,sheet.Cells[row,amounts.Last()]);continue;}
                if(key.StartsWith("TOTALRS")){d.Total=RightmostAmount(sheet,row,amounts);Cite(d,"Total Rs.",sheet,sheet.Cells[row,amounts.Last()]);continue;}
                if(ended)continue;
                string description=sheet.Cells[row,desc].Text.Trim();decimal? quantity=Number(sheet.Cells[row,qty]);
                if(string.IsNullOrWhiteSpace(description))continue;
                if(!quantity.HasValue){section=description;continue;}
                for(int group=0;group<rates.Count;group++)
                {
                    decimal? rate=Number(sheet.Cells[row,rates[group]]),amount=Number(sheet.Cells[row,amounts[group]]);
                    if(!rate.HasValue&&!amount.HasValue)continue;
                    string suffix=rates.Count==2?(group==0?" — Supply":" — Installation"):"";
                    d.Lines.Add(new DocumentImportLine{Description=(string.IsNullOrWhiteSpace(section)?"":section+" · ")+description+suffix,Quantity=quantity,Unit=sheet.Cells[row,unit].Text.Trim(),Rate=rate,Taxable=amount});
                    Cite(d,"Item "+d.Lines.Count+suffix,sheet,sheet.Cells[row,desc]);Cite(d,"Rate",sheet,sheet.Cells[row,rates[group]]);Cite(d,"Line amount",sheet,sheet.Cells[row,amounts[group]]);
                }
            }
            if(cgstRate.HasValue&&sgstRate.HasValue)foreach(var line in d.Lines)line.GSTPercent=cgstRate+sgstRate;
            d.Evidence.Add(new ImportFieldEvidence{Field="Missing line taxes/discounts",Confidence="Review required",Source="Historical quotation stores GST at document level. Individual line taxes, missing discount/IGST/round-off and due date are not fabricated. Confirm or enter them against the source."});
            d.SourceText=Source(sheet,1,Math.Min(sheet.Dimension.End.Row,100),Math.Min(sheet.Dimension.End.Column,12));
            return d;
        }
        private static void ReadRegister(ExcelWorksheet sheet,List<DocumentImportDraft> result,CancellationToken token)
        {
            bool purchase=sheet.Name.Trim().Equals("Purchases",StringComparison.OrdinalIgnoreCase);
            int header=0;Dictionary<string,int> columns=null;
            for(int row=1;row<=Math.Min(8,sheet.Dimension.End.Row);row++)
            {
                var candidate=new Dictionary<string,int>();
                for(int col=1;col<=Math.Min(40,sheet.Dimension.End.Column);col++){string key=Key(sheet.Cells[row,col].Text);if(key.Length>0&&!candidate.ContainsKey(key))candidate.Add(key,col);}
                if(candidate.ContainsKey("INVOICENO")&&candidate.ContainsKey(purchase?"NAMEOFTHESUPPLIER":"CUSTOMER")&&candidate.ContainsKey(purchase?"VALUEWITHOUTGST":"SUBTOTAL")){header=row;columns=candidate;break;}
            }
            if(columns==null)return;
            int number=columns["INVOICENO"],party=columns[purchase?"NAMEOFTHESUPPLIER":"CUSTOMER"];
            for(int row=header+1;row<=Math.Min(sheet.Dimension.End.Row,20000);row++)
            {
                token.ThrowIfCancellationRequested();
                if(string.IsNullOrWhiteSpace(sheet.Cells[row,number].Text)||string.IsNullOrWhiteSpace(sheet.Cells[row,party].Text))continue;
                var d=NewDraft(sheet,row,purchase?ImportDocumentType.PurchaseInvoice:ImportDocumentType.SalesInvoice);
                d.DocumentNumber=sheet.Cells[row,number].Text.Trim();d.PartyName=sheet.Cells[row,party].Text.Trim();
                d.DocumentDate=CellDate(Cell(sheet,row,columns,"INVDATE","DATE"));d.DueDate=CellDate(Cell(sheet,row,columns,"DUEDATE"));
                d.TaxableAmount=Number(Cell(sheet,row,columns,purchase?"VALUEWITHOUTGST":"SUBTOTAL"));
                d.Total=Number(Cell(sheet,row,columns,purchase?"TOTALWITHGST":"TOTAL"));
                d.GSTIN=Cell(sheet,row,columns,"GSTINNUMBER")?.Text.Trim();d.Reference=Cell(sheet,row,columns,"PONUMBER","REFNO")?.Text.Trim();
                d.CGST=TaxBucket(sheet,row,columns,"CGST");d.SGST=TaxBucket(sheet,row,columns,"SGST");d.IGST=TaxBucket(sheet,row,columns,"IGST");
                var payment=new StringBuilder();
                foreach(var col in columns.Where(c=>c.Key=="PMTDATE"||c.Key=="PAIDAMT"||c.Key=="CRAMOUNT"||c.Key=="TDSIFANY"||c.Key=="DEDUCTIONS"||c.Key=="ADVANCEADJ"||c.Key=="REMARKS"))
                    if(!string.IsNullOrWhiteSpace(sheet.Cells[row,col.Value].Text))payment.Append(sheet.Cells[header,col.Value].Text.Trim()).Append(": ").Append(sheet.Cells[row,col.Value].Text).Append("; ");
                d.PaymentDetails=payment.ToString();
                // Registers are header/summary records, not item invoices: do not turn description into a made-up quantity-one item.
                foreach(var col in columns)if(!string.IsNullOrWhiteSpace(sheet.Cells[row,col.Value].Text))Cite(d,sheet.Cells[header,col.Value].Text.Trim(),sheet,sheet.Cells[row,col.Value]);
                d.Evidence.Add(new ImportFieldEvidence{Field="Item details",Confidence="Missing — review required",Source="MSE register row is a summary. Upload the matching detailed invoice or enter its actual line items. Payment, TDS, deductions and received status are evidence only and never posted by import."});
                d.SourceText=Source(sheet,header,header,Math.Min(40,sheet.Dimension.End.Column))+Environment.NewLine+Source(sheet,row,row,Math.Min(40,sheet.Dimension.End.Column));
                result.Add(d);
                if(result.Count>=5000)throw new InvalidOperationException("This workbook contains more than 5,000 documents. Split a copy into smaller Sales/Purchases batches; originals have not been modified or imported.");
            }
        }
        private static DocumentImportDraft NewDraft(ExcelWorksheet sheet,int row,ImportDocumentType type)=>new DocumentImportDraft{DocumentType=type,Direction=type==ImportDocumentType.PurchaseInvoice?ImportDirection.Purchase:ImportDirection.Sales,FirstPage=sheet.Index*100000+row,LastPage=sheet.Index*100000+row,SourceLocation=sheet.Name+" · row "+row,ExtractionStatus="MseHistoricalReviewRequired"};
        private static decimal? TaxBucket(ExcelWorksheet s,int row,Dictionary<string,int> columns,string prefix)
        {
            var values=columns.Where(x=>x.Key.StartsWith(prefix,StringComparison.Ordinal)).Select(x=>Number(s.Cells[row,x.Value])).Where(x=>x.HasValue).ToList();
            return values.Count==0?(decimal?)null:values.Sum(x=>x.Value);
        }
        private static ExcelRangeBase Cell(ExcelWorksheet s,int row,Dictionary<string,int> columns,params string[] keys){foreach(string key in keys)if(columns.ContainsKey(key))return s.Cells[row,columns[key]];return null;}
        private static DateTime? CellDate(ExcelRangeBase cell)
        {
            if(cell==null||cell.Value==null||cell.Value is ExcelErrorValue)return null;
            if(cell.Value is DateTime)return ((DateTime)cell.Value).Date;
            if(cell.Value is double){double serial=(double)cell.Value;if(serial>1&&serial<100000)return DateTime.FromOADate(serial).Date;return null;}
            return Date(cell.Text);
        }
        private static DateTime? Date(string value){DateTime date;return DateTime.TryParseExact(value?.Trim(),new[]{"dd-MM-yyyy","d-M-yyyy","dd/MM/yyyy","d/M/yyyy","d-MMM-yy","dd-MMM-yyyy"},India,DateTimeStyles.None,out date)?date:(DateTime?)null;}
        private static decimal? Number(ExcelRangeBase cell)
        {
            if(cell==null||cell.Value==null||cell.Value is ExcelErrorValue)return null;
            // Cached numeric values are authoritative. Reading Text would silently round MSE's
            // zero-decimal display formats (e.g. 69606.05 displayed as 69606).
            if(cell.Value is double||cell.Value is float||cell.Value is decimal||cell.Value is int||cell.Value is long)
                return Convert.ToDecimal(cell.Value,CultureInfo.InvariantCulture);
            decimal number;string text=Regex.Replace(cell.Text,@"(?i)(?:Rs\.?|INR|₹)|,","").Trim();
            return decimal.TryParse(text,NumberStyles.Number,India,out number)?number:(decimal?)null;
        }
        private static decimal? Percent(string label){var match=Regex.Match(label,@"([\d.]+)\s*%");decimal value;return match.Success&&decimal.TryParse(match.Groups[1].Value,NumberStyles.Number,India,out value)?value:(decimal?)null;}
        private static decimal? RightmostAmount(ExcelWorksheet s,int row,List<int> columns){for(int i=columns.Count-1;i>=0;i--){var value=Number(s.Cells[row,columns[i]]);if(value.HasValue)return value;}return null;}
        private static string AfterColon(string text)=>text.Substring(text.IndexOf(':')+1).Trim();
        private static string Key(string text)=>Regex.Replace(text??"",@"[^A-Za-z0-9]","").ToUpperInvariant();
        private static int Column(ExcelWorksheet s,int row,string key){for(int col=1;col<=Math.Min(s.Dimension.End.Column,12);col++)if(Key(s.Cells[row,col].Text)==key)return col;return 0;}
        private static int ColumnPrefix(ExcelWorksheet s,int row,string key){for(int col=1;col<=Math.Min(s.Dimension.End.Column,12);col++)if(Key(s.Cells[row,col].Text).StartsWith(key))return col;return 0;}
        private static void Cite(DocumentImportDraft d,string field,ExcelWorksheet sheet,ExcelRangeBase cell)=>d.Evidence.Add(new ImportFieldEvidence{Field=field,Confidence="Source cell — verify",Source=sheet.Name+"!"+cell.Address+" = "+cell.Text+" [cached: "+Convert.ToString(cell.Value,CultureInfo.InvariantCulture)+"]"});
        private static string Source(ExcelWorksheet sheet,int first,int last,int columns)
        {
            var text=new StringBuilder();text.AppendLine("Original workbook: "+sheet.Name+" (cached/displayed values; formulas not recalculated)");
            for(int row=first;row<=last;row++)for(int col=1;col<=columns;col++)if(!string.IsNullOrWhiteSpace(sheet.Cells[row,col].Text))
            {
                var cell=sheet.Cells[row,col];text.Append(cell.Address).Append("  ").Append(cell.Text);
                if(cell.Value is double||cell.Value is decimal)text.Append(" [cached: ").Append(Convert.ToString(cell.Value,CultureInfo.InvariantCulture)).Append(']');
                text.AppendLine();
            }
            return text.ToString();
        }
    }
}
