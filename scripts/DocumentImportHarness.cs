using System;
using System.Drawing;
using System.IO;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Windows.Forms;
using HVAC_Pro_Desktop.Models;
using HVAC_Pro_Desktop.Services;
using HVAC_Pro_Desktop.Tests;
using HVAC_Pro_Desktop.UI;

internal static class DocumentImportHarness
{
    [STAThread] private static int Main(string[] args)
    {
        string bin=Path.GetFullPath(args[0]);
        AppDomain.CurrentDomain.AssemblyResolve+=(s,e)=> {string p=Path.Combine(bin,new AssemblyName(e.Name).Name+".dll"); if(!File.Exists(p))p=Path.Combine(bin,new AssemblyName(e.Name).Name+".exe");return File.Exists(p)?Assembly.LoadFrom(p):null;};
        // Extraction resolves the packaged OCR worker relative to the host EXE. Test harness is copied
        // to the build directory for execution and removed before packaging.
        try { return Run(args); } catch(Exception ex) { Console.Error.WriteLine(ex); return 1; }
    }
    [MethodImpl(MethodImplOptions.NoInlining)] private static int Run(string[] args)
    {
        Thread.CurrentThread.CurrentCulture=System.Globalization.CultureInfo.GetCultureInfo("en-IN");
        Thread.CurrentThread.CurrentUICulture=Thread.CurrentThread.CurrentCulture;
        if(args[1]=="discovery") {Console.WriteLine(LocalDocumentDiscoveryTests.Run(args[2]));return 0;}
        if(args[1]=="uidiscovery")
        {
            Application.EnableVisualStyles();Application.SetCompatibleTextRenderingDefault(false);
            var result=new LocalDocumentScanResult{RootPath=Path.GetPathRoot(args[2]),FoldersVisited=3,FilesVisited=8,LinksSkipped=1,SystemFoldersSkipped=true,SystemFoldersSkippedCount=1};
            foreach(string file in Directory.GetFiles(args[2],"*.pdf")) {var info=new FileInfo(file);result.Files.Add(new LocalDocumentCandidate{FileName=info.Name,FullPath=info.FullName,SizeBytes=info.Length,ModifiedUtc=info.LastWriteTimeUtc,Format="PDF",CanReview=info.Length>0&&info.Length<=20*1024*1024,Status=info.Length==0?"Empty file":info.Length>20*1024*1024?"Over 20 MB limit":"Not analysed — select for review"});}
            using(var discovery=new LocalDocumentDiscoveryDialog(result))
            {
                discovery.Text+=" · QA fixture results (not a real drive scan)";
                var timer=new System.Windows.Forms.Timer{Interval=1800};timer.Tick+=(s,e)=>{timer.Stop();using(var bitmap=new Bitmap(discovery.Width,discovery.Height)){discovery.DrawToBitmap(bitmap,new Rectangle(Point.Empty,discovery.Size));bitmap.Save(args[3]);}timer.Dispose();};discovery.Shown+=(s,e)=>timer.Start();
                discovery.ShowDialog();
            }
            return 0;
        }
        if(args[1]=="historical") {Console.WriteLine(MseHistoricalImportTests.Run(args[2],args[3]));return 0;}
        if(args[1]=="ci")
        {
            // Mirror the existing app's QuestPDF startup configuration without invoking Program.Main,
            // whose single-instance cleanup would interrupt the user's installed ServoERP session.
            var quest=Assembly.LoadFrom(Path.Combine(args[0],"QuestPDF.dll"));
            var license=quest.GetType("QuestPDF.Settings").GetProperty("License");
            license.SetValue(null,Enum.Parse(Nullable.GetUnderlyingType(license.PropertyType)??license.PropertyType,"Community"));
            string report=(string)typeof(DocumentImportService).Assembly.GetType("HVAC_Pro_Desktop.Program").GetMethod("WriteCiSmokeReport",BindingFlags.NonPublic|BindingFlags.Static).Invoke(null,null);
            string text=File.ReadAllText(report);Console.WriteLine(text);return text.Contains("FAIL ")?1:0;
        }
        if(args[1]=="test") { Console.WriteLine(DocumentImportRegressionTests.Run(args[2],args[3]));return 0; }
        Application.EnableVisualStyles();Application.SetCompatibleTextRenderingDefault(false);
        using(var extractor=new DocumentImportExtractionService())
        using(var dialog=new DocumentImportDialog())
        {
            if(args.Length>2)
            {
                var draft=args[1]=="uiworkbook"?extractor.ExtractAsync(args[2],null,CancellationToken.None).GetAwaiter().GetResult()[0]:DocumentImportExtractionService.Parse("TAX INVOICE\nInvoice No: QA-001\nDate: 01/11/2026\nTaxable Amount: 1000.00\nCGST: 90.00\nSGST: 90.00\nIGST: 0.00\nRound Off: 0.00\nGrand Total: 1180.00",1);
                if(args[1]!="uiworkbook") {draft.PreviewPath=args[2];draft.Lines.Add(new DocumentImportLine{Description="Cooling service",HSN="9987",Quantity=1,Unit="Nos",Rate=1000,DiscountPercent=0,GSTPercent=18,Taxable=1000,Tax=180});}
                var list=(System.ComponentModel.BindingList<DocumentImportDraft>)typeof(DocumentImportDialog).GetField("_drafts",BindingFlags.NonPublic|BindingFlags.Instance).GetValue(dialog);list.Add(draft);
            }
            if(args.Length>3)
            {
                var timer=new System.Windows.Forms.Timer {Interval=1800};timer.Tick+=(s,e)=>{timer.Stop();using(var bitmap=new Bitmap(dialog.Width,dialog.Height)){dialog.DrawToBitmap(bitmap,new Rectangle(Point.Empty,dialog.Size));bitmap.Save(args[3]);}timer.Dispose();};dialog.Shown+=(s,e)=>timer.Start();
            }
            Application.Run(dialog);
        }
        return 0;
    }
}
