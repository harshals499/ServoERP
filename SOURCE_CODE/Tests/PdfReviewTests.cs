using System;
using System.Collections.Generic;
using System.Data.SqlClient;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;
using Dapper;
using HVAC_Pro_Desktop.DAL;
using HVAC_Pro_Desktop.Models;
using HVAC_Pro_Desktop.Services;
using HVAC_Pro_Desktop.Services.Licensing;
using HVAC_Pro_Desktop.UI;
using QuestPDF.Fluent;

namespace HVAC_Pro_Desktop.Tests
{
    internal static class PdfReviewTests
    {
        internal static void Run(string fixtureRoot)
        {
            fixtureRoot=Path.GetFullPath(string.IsNullOrWhiteSpace(fixtureRoot)?Path.Combine(Directory.GetCurrentDirectory(),"TEST_RESULTS","pdf-review"):fixtureRoot);
            Directory.CreateDirectory(fixtureRoot);
            string connection=Environment.GetEnvironmentVariable("SERVOERP_PDF_QA_CONNECTION");
            if(string.IsNullOrEmpty(connection)||!new SqlConnectionStringBuilder(connection).InitialCatalog.StartsWith("ServoERP_PdfReviewQA_",StringComparison.Ordinal))
                throw new InvalidOperationException("Set SERVOERP_PDF_QA_CONNECTION to a disposable ServoERP_PdfReviewQA_* database. Never use HVAC_PRO.");
            string previousConnection=DatabaseConnectionFactory.DiagnosticConnectionString;
            var licenseField=typeof(LicenseService).GetField("_lastValidation",BindingFlags.Static|BindingFlags.NonPublic);var previousLicense=licenseField.GetValue(null);
            var previousUser=SessionManager.CurrentUser;
            var passes=new List<string>();
            try
            {
                DatabaseConnectionFactory.DiagnosticConnectionString=connection;
                // Test process memory only. No license tables, endpoint, cache or machine identity are changed.
                licenseField.SetValue(null,new LicenseValidationResult {Success=true,IsFrozen=false,Snapshot=new LicenseSnapshot {PlanType=LicensePlanType.Enterprise,Status=LicenseStatus.Active,EnabledModules=LicenseFeatureCatalog.GetModulesForPlan(LicensePlanType.Enterprise),ExpiryDateUtc=DateTime.UtcNow.AddYears(1)}});
                SessionManager.SetSession(new AppUserDto {UserId=1,Username="pdf-qa",DisplayName="PDF Review QA",RoleName="Admin",RoleId=1,IsActive=true});
                string scanRoot=Path.Combine(fixtureRoot,"sources");Directory.CreateDirectory(Path.Combine(scanRoot,"Nested"));
                string first=Path.Combine(scanRoot,"PO-2026-104.pdf");WritePdf(first,"PURCHASE ORDER",2);
                string duplicate=Path.Combine(scanRoot,"Nested","Duplicate.PDF");File.Copy(first,duplicate,true);
                File.WriteAllText(Path.Combine(scanRoot,"Nested","unreadable.pdf"),"not a PDF");File.WriteAllText(Path.Combine(scanRoot,"ignore.txt"),"ignored");
                List<string> warnings;var batch=PdfReviewService.Scan(scanRoot,null,CancellationToken.None,out warnings);
                Assert(batch.Files.Count==3&&batch.Files.All(f=>f.Include)&&warnings.Count==0,"Recursive scan/default inclusion failed");
                Assert(batch.Files.Any(f=>f.Folder=="Nested"),"Relative source folders lost");
                using(var cancellation=new CancellationTokenSource()) {cancellation.Cancel();Expect<OperationCanceledException>(()=>PdfReviewService.Scan(scanRoot,null,cancellation.Token,out warnings));}
                passes.Add("PASS recursive scan, case-insensitive PDFs, default inclusion, folder labels, cancellation");
                using(var service=new PdfReviewService())
                {
                    var file=batch.Files.Single(f=>f.SourcePath==first);var copy=batch.Files.Single(f=>f.SourcePath==duplicate);
                    var render=service.RenderAsync(file,1,CancellationToken.None).GetAwaiter().GetResult();Assert(render.Pages==2&&File.Exists(render.Image),"Real multipage preview failed: "+render.Error);PdfReviewService.DeletePreview(render.Image);file.Previewed=true;
                    render=service.RenderAsync(file,2,CancellationToken.None).GetAwaiter().GetResult();Assert(render.Pages==2&&File.Exists(render.Image),"Second page failed");PdfReviewService.DeletePreview(render.Image);
                    service.PrepareAsync(copy,CancellationToken.None).GetAwaiter().GetResult();batch.RefreshDuplicates();Assert(copy.Duplicate,"Content duplicate not detected");
                    Expect<InvalidOperationException>(()=>batch.SaveChoice(copy,PdfCategory.Invoice));
                    batch.SaveChoice(file,PdfCategory.PurchaseOrder);Assert(batch.Ready==1&&batch.Unresolved==2,"Progress counts incorrect");
                    Expect<InvalidOperationException>(()=>batch.ConfirmedFiles(false));Assert(batch.ConfirmedFiles(true).Count==1,"Unresolved acknowledgement failed");
                    file.Include=false;batch.RefreshDuplicates();Assert(!copy.Duplicate,"Excluding first copy did not release duplicate");file.Include=true;batch.RefreshDuplicates();
                    batch.SaveChoice(file,PdfCategory.Quotation);Assert(file.Category==PdfCategory.Quotation,"Earlier category cannot be changed");
                    render=service.RenderAsync(batch.Files.Single(f=>f.FileName=="unreadable.pdf"),1,CancellationToken.None).GetAwaiter().GetResult();Assert(!string.IsNullOrEmpty(render.Error),"Unreadable PDF accepted");
                    passes.Add("PASS actual local rendering (2 pages), duplicate release, manual category edits, unresolved acknowledgement, unreadable isolation");
                    if(File.Exists(Path.Combine(fixtureRoot,"locked.pdf")))
                    {
                        var locked=new PdfReviewFile {SourcePath=Path.Combine(fixtureRoot,"locked.pdf"),Folder="."};
                        var blocked=service.RenderAsync(locked,1,CancellationToken.None).GetAwaiter().GetResult();Assert(blocked.Error!=null,"Protected file opened without password");
                        locked.Password="sample-123";var unlocked=service.RenderAsync(locked,1,CancellationToken.None).GetAwaiter().GetResult();Assert(unlocked.Pages>0&&File.Exists(unlocked.Image),"Password unlock failed: "+unlocked.Error);PdfReviewService.DeletePreview(unlocked.Image);locked.Password=null;
                        passes.Add("PASS password-protected PDF error and local unlock (password only in memory/stdin)");
                    }
                    SqlChecks(connection,fixtureRoot,service,passes);
                }
                UiChecks(fixtureRoot,passes);
                NavigationChecks(fixtureRoot,passes);
                File.WriteAllLines(Path.Combine(fixtureRoot,"results.txt"),passes);
            }
            catch(Exception ex) {File.WriteAllLines(Path.Combine(fixtureRoot,"results.txt"),passes.Concat(new[]{"FAIL "+ex}));Environment.ExitCode=1;}
            finally {DatabaseConnectionFactory.DiagnosticConnectionString=previousConnection;licenseField.SetValue(null,previousLicense);SessionManager.SetSession(previousUser);}
        }
        private static void SqlChecks(string connection,string root,PdfReviewService service,List<string> passes)
        {
            using(var c=new SqlConnection(connection))
            {
                c.Open();
                // Minimal disposable schema, never application startup or migrations against business data.
                c.Execute(@"IF OBJECT_ID(N'dbo.ClientDocuments',N'U') IS NULL
CREATE TABLE dbo.ClientDocuments(DocumentId int IDENTITY PRIMARY KEY,DocumentType nvarchar(80),Title nvarchar(180) NOT NULL,FilePath nvarchar(500) NOT NULL,OriginalFileName nvarchar(255),Notes nvarchar(max),UploadedDate datetime NOT NULL DEFAULT GETDATE(),UploadedBy nvarchar(100));");
                PdfReviewRepository.EnsureSchema(c);PdfReviewRepository.EnsureSchema(c);
                var repo=new PdfReviewRepository();int initial=c.ExecuteScalar<int>("SELECT COUNT(*) FROM ClientDocuments");
                Expect<FluentValidation.ValidationException>(()=>repo.Import(new PdfReviewFile {SourcePath="unreviewed.pdf"},CancellationToken.None,Path.Combine(root,"vault")));
                foreach(var category in PdfCategories.All)
                {
                    string path=Path.Combine(root,category+".pdf");WritePdf(path,category.ToString(),1);
                    var file=new PdfReviewFile {SourcePath=path,Folder="Fixtures",Category=category,Previewed=true};service.PrepareAsync(file,CancellationToken.None).GetAwaiter().GetResult();
                    int id=repo.Import(file,CancellationToken.None,Path.Combine(root,"vault"));Assert(id>0&&repo.Exists(file.Hash),"Missing imported attachment");
                    Assert(repo.List(category).Any(r=>r.DocumentId==id&&File.Exists(r.FilePath)),"Destination library missing attachment");
                    int before=c.ExecuteScalar<int>("SELECT COUNT(*) FROM ClientDocuments");Expect<InvalidOperationException>(()=>repo.Import(file,CancellationToken.None,Path.Combine(root,"vault")));Assert(before==c.ExecuteScalar<int>("SELECT COUNT(*) FROM ClientDocuments"),"Duplicate changed rows");
                    if(category==PdfCategory.Invoice)
                    {
                        c.Execute("DELETE dbo.ClientDocuments WHERE DocumentId=@id",new{id});
                        Assert(!repo.Exists(file.Hash),"Removed registration left stale duplicate metadata");
                        Assert(repo.Import(file,CancellationToken.None,Path.Combine(root,"vault"))>0,"Removed registration cannot be imported again");
                    }
                }
                Assert(c.ExecuteScalar<int>("SELECT COUNT(*) FROM ClientDocuments")==initial+5,"Unexpected attachment count");
                foreach(string table in new[]{"Invoices","PurchaseOrders","Quotations","StockItems","Payments"})
                    Assert(c.ExecuteScalar<int>("SELECT COUNT(*) FROM sys.tables WHERE name=@table",new{table})==0,"Attachment import created a business table");
                passes.Add("PASS additive/idempotent schema, all 5 category libraries, content duplicates blocked, registration-removal metadata cleanup, no business tables/transactions created");
                c.Execute(@"CREATE TRIGGER dbo.PdfReviewQA_ForceRollback ON dbo.PdfReviewDocuments AFTER INSERT AS BEGIN IF EXISTS(SELECT 1 FROM inserted WHERE SourceFolder='force-failure') THROW 51000,'QA forced failure',1; END");
                string rollback=Path.Combine(root,"rollback.pdf");WritePdf(rollback,"ROLLBACK QA",1);
                var failing=new PdfReviewFile {SourcePath=rollback,Folder="force-failure",Category=PdfCategory.Other,Previewed=true};service.PrepareAsync(failing,CancellationToken.None).GetAwaiter().GetResult();
                int count=c.ExecuteScalar<int>("SELECT COUNT(*) FROM ClientDocuments");Expect<SqlException>(()=>repo.Import(failing,CancellationToken.None,Path.Combine(root,"vault")));Assert(count==c.ExecuteScalar<int>("SELECT COUNT(*) FROM ClientDocuments"),"Failed metadata insert left partial registration");
                File.AppendAllText(failing.SnapshotPath,"tampered");Expect<InvalidOperationException>(()=>repo.Import(failing,CancellationToken.None,Path.Combine(root,"vault")));
                using(var cancel=new CancellationTokenSource()){cancel.Cancel();Expect<OperationCanceledException>(()=>repo.Import(failing,cancel.Token,Path.Combine(root,"vault")));}
                var user=SessionManager.CurrentUser;SessionManager.SetSession(null);Expect<UnauthorizedAccessException>(()=>repo.Import(failing,CancellationToken.None,Path.Combine(root,"vault")));SessionManager.SetSession(user);
                passes.Add("PASS per-file SQL rollback, tamper refusal, cancellation, unauthenticated writes blocked");
            }
        }
        private static void UiChecks(string root,List<string> passes)
        {
            string path=Path.Combine(root,"review-window.pdf");WritePdf(path,"DESIGN REVIEW — SAMPLE PDF",2);
            var batch=new PdfReviewBatch();batch.Files.Add(new PdfReviewFile {SourcePath=path,Folder="Site A"});
            batch.Files.Add(new PdfReviewFile {SourcePath=path,Folder="Archive",Include=false});
            batch.Files.Add(new PdfReviewFile {SourcePath=Path.Combine(root,"password-required.pdf"),Folder="Needs attention",Error="Password required",Include=true});
            using(var page=new PdfImportPage())
            using(var host=new Form {Text="ServoERP · PDF Import QA",ClientSize=new Size(1440,940),StartPosition=FormStartPosition.CenterScreen})
            {
                host.Controls.Add(page);host.Show();page.LoadForTest(batch);
                PumpUntil(()=>batch.Files[0].Previewed,TimeSpan.FromSeconds(55));
                FindButton(page,"Purchase Order (PO)").PerformClick();Application.DoEvents();
                Capture(host,Path.Combine(root,"review-wide.png"));
                Assert(FindButton(page,"Save Selection & Next").Enabled,"Save not enabled after preview/category choice");
                FindButton(page,"Save Selection & Next").PerformClick();Application.DoEvents();Assert(batch.Files[0].Category==PdfCategory.PurchaseOrder,"Native Save Selection failed");
                page.SummaryForTest();Application.DoEvents();
                var confirm=Descendants(page).OfType<Button>().Single(b=>b.Text.StartsWith("Confirm & Import"));Assert(!confirm.Enabled,"Summary allowed unresolved files without acknowledgement");
                Descendants(page).OfType<CheckBox>().Single(b=>b.Text.StartsWith("Leave unresolved")).Checked=true;Assert(confirm.Enabled,"Summary acknowledgement did not enable reviewed import");
                Capture(host,Path.Combine(root,"summary-wide.png"));
                host.ClientSize=new Size(1080,760);Application.DoEvents();Capture(host,Path.Combine(root,"summary-narrow.png"));
                FindButton(page,"Back to review").PerformClick();Application.DoEvents();
                var grid=Descendants(page).OfType<DataGridView>().First(g=>g.Columns.Cast<DataGridViewColumn>().Any(c=>c.DataPropertyName=="FileName"));grid.CurrentCell=grid.Rows[0].Cells[1];
                PumpUntil(()=>Descendants(page).OfType<PdfReviewViewer>().Single().CurrentFile==batch.Files[0]&&!Descendants(page).OfType<PdfReviewViewer>().Single().Loading,TimeSpan.FromSeconds(55));
                Capture(host,Path.Combine(root,"review-narrow.png"));
                host.Close();
            }
            foreach(string title in new[]{"Invoices","Purchase Orders","Quotations"})
            {
                var model=SharedPageHeader.CreateSalesEditor("QA",title,"QA",new List<Control>());using(var header=SharedPageHeader.Build(model).Header)
                    Assert(Descendants(header).Any(c=>c.Name=="ImportedPdfLibrary"),"Module library action missing: "+title);
            }
            using(var main=new MainForm())
            using(var host=new Form {ClientSize=new Size(310,900)})
            {
                var sidebar=(Control)typeof(MainForm).GetField("_sidebar",BindingFlags.Instance|BindingFlags.NonPublic).GetValue(main);sidebar.Parent.Controls.Remove(sidebar);host.Controls.Add(sidebar);host.Show();Application.DoEvents();
                Assert(Descendants(sidebar).Any(c=>c.AccessibleName=="PDF Import"),"Sidebar PDF Import missing");Capture(host,Path.Combine(root,"sidebar.png"));
                var content=(Control)typeof(MainForm).GetField("_content",BindingFlags.Instance|BindingFlags.NonPublic).GetValue(main);
                content.Parent.Controls.Remove(content);host.Controls.Add(content);sidebar.Dock=DockStyle.Left;content.Dock=DockStyle.Fill;content.BringToFront();
                host.ClientSize=new Size(1600,960);main.NavigateTo("PDF Import");Application.DoEvents();
                var shellPage=Descendants(content).OfType<PdfImportPage>().Single();
                var shellBatch=new PdfReviewBatch();shellBatch.Files.Add(new PdfReviewFile {SourcePath=path,Folder="Site A"});shellPage.LoadForTest(shellBatch);
                PumpUntil(()=>shellBatch.Files[0].Previewed&&!Descendants(shellPage).OfType<PdfReviewViewer>().Single().Loading,TimeSpan.FromSeconds(55));
                Capture(host,Path.Combine(root,"shell-pdf-import.png"));main.ReloadPageByKey("PDF Import");Assert(!shellPage.IsDisposed,"Refresh discarded the review");
                typeof(MainForm).GetField("_currentIndex",BindingFlags.Instance|BindingFlags.NonPublic).SetValue(main,0);main.ClearCachedPagesExceptCurrent();Assert(!shellPage.IsDisposed,"Cache clearing discarded the review");
                host.Close();
            }
            passes.Add("PASS native built review/category/save-next/summary, wide+narrow screenshots, sidebar PDF Import, module library actions");
        }
        private static void NavigationChecks(string root,List<string> passes)
        {
            string folder=Path.Combine(root,"navigation");Directory.CreateDirectory(folder);
            for(int index=0;index<5;index++)WritePdf(Path.Combine(folder,"invoice-"+index+".pdf"),"NAVIGATION SAMPLE "+index,2);
            List<string> warnings;var batch=PdfReviewService.Scan(folder,null,CancellationToken.None,out warnings);
            Assert(batch.Files.Count==5&&warnings.Count==0,"Navigation fixture scan failed");
            int registrations;using(var c=DatabaseConnectionFactory.CreateConnection()){c.Open();registrations=c.ExecuteScalar<int>("SELECT COUNT(*) FROM dbo.ClientDocuments");}
            using(var page=new PdfImportPage())
            using(var host=new Form {ClientSize=new Size(1440,940),StartPosition=FormStartPosition.CenterScreen})
            {
                host.Controls.Add(page);host.Show();Application.DoEvents();
                // Exercise the actual parent-disable cycle used while scanning, not just a preloaded idle page.
                typeof(PdfImportPage).GetMethod("BeginOperation",BindingFlags.Instance|BindingFlags.NonPublic).Invoke(page,new object[]{"Scanning synthetic QA PDFs…"});
                typeof(PdfImportPage).GetMethod("EndOperation",BindingFlags.Instance|BindingFlags.NonPublic).Invoke(page,null);
                page.LoadForTest(batch);
                var viewer=Descendants(page).OfType<PdfReviewViewer>().Single();
                var save=FindButton(page,"Save Selection & Next");
                for(int index=0;index<5;index++)
                {
                    int expected=index;
                    PumpUntil(()=>viewer.CurrentFile==batch.Files[expected]&&batch.Files[expected].Previewed&&!viewer.Loading,TimeSpan.FromSeconds(55));
                    Assert(FindButton(viewer,"+").Enabled&&FindButton(viewer,"+").ForeColor.ToArgb()!=DS.Slate500.ToArgb(),"Live PDF toolbar still looks disabled after scanning");
                    if(index==0)
                    {
                        FindButton(viewer,"›").PerformClick();PumpUntil(()=>!viewer.Loading,TimeSpan.FromSeconds(55));
                        Assert(Descendants(viewer).OfType<Label>().Any(label=>label.Text=="Page 2 of 2"),"Next page did not advance");
                        FindButton(viewer,"+").PerformClick();Application.DoEvents();
                        Assert(Descendants(viewer).OfType<Label>().Any(label=>label.Text.Contains("125%")),"Zoom did not respond");
                        FindButton(viewer,"Fit to width").PerformClick();
                    }
                    FindButton(page,"Invoice").PerformClick();Application.DoEvents();
                    Assert(save.Enabled&&save.BackColor.ToArgb()==DS.Primary600.ToArgb()&&save.ForeColor.ToArgb()==Color.White.ToArgb(),"Ready Save/Next must be visibly enabled in the established primary palette");
                    if(index==0)Capture(host,Path.Combine(root,"controls-ready.png"));
                    save.PerformClick();Application.DoEvents();
                    Assert(batch.Files[index].Category==PdfCategory.Invoice,"Save/Next did not retain the current category");
                }
                Assert(batch.Ready==5&&Descendants(page).OfType<Button>().Any(button=>button.Text=="Confirm & Import — 5 PDFs"&&button.Enabled),"Last document did not reach a correct import summary");
                Capture(host,Path.Combine(root,"navigation-summary.png"));FindButton(page,"Back to review").PerformClick();
                FindButton(page,"Previous").PerformClick();PumpUntil(()=>viewer.CurrentFile==batch.Files[3]&&!viewer.Loading,TimeSpan.FromSeconds(55));
                FindButton(page,"Quotation").PerformClick();save.PerformClick();Application.DoEvents();Assert(batch.Files[3].Category==PdfCategory.Quotation,"Earlier category could not be changed");
                PumpUntil(()=>viewer.CurrentFile==batch.Files[4]&&!viewer.Loading,TimeSpan.FromSeconds(55));
                var grid=Descendants(page).OfType<DataGridView>().First(control=>control.Columns.Cast<DataGridViewColumn>().Any(column=>column.DataPropertyName=="FileName"));
                grid.CurrentCell=grid.Rows[0].Cells[1];grid.CurrentCell=grid.Rows[4].Cells[1];
                PumpUntil(()=>viewer.CurrentFile==batch.Files[4]&&!viewer.Loading,TimeSpan.FromSeconds(55));
                Assert(save.Enabled,"Rapid selection/cancel left Save stranded");
                host.Close();
            }
            using(var c=DatabaseConnectionFactory.CreateConnection()){c.Open();Assert(c.ExecuteScalar<int>("SELECT COUNT(*) FROM dbo.ClientDocuments")==registrations,"Categorisation/navigation wrote document registrations before import confirmation");}
            passes.Add("PASS scan enable cycle restores button colours; 5-file Save/Next, multipage/zoom, Previous/reclassification, rapid selection/cancel, final summary; categorising writes no records");
        }
        private static IEnumerable<Control> Descendants(Control root) {yield return root;foreach(Control child in root.Controls)foreach(var c in Descendants(child))yield return c;}
        private static Button FindButton(Control root,string text)=>Descendants(root).OfType<Button>().First(b=>b.Text==text);
        private static void PumpUntil(Func<bool> ready,TimeSpan limit) {var end=DateTime.UtcNow+limit;while(!ready()){if(DateTime.UtcNow>end)throw new InvalidOperationException("Native preview timed out");Application.DoEvents();Thread.Sleep(20);}Application.DoEvents();}
        private static void Capture(Form form,string path) {using(var bitmap=new Bitmap(form.Width,form.Height)){form.DrawToBitmap(bitmap,new Rectangle(Point.Empty,bitmap.Size));bitmap.Save(path,ImageFormat.Png);}}
        private static void WritePdf(string path,string title,int pages)
        {
            Document.Create(document=> {for(int i=1;i<=pages;i++){int pageNumber=i;document.Page(page=> {page.Size(595,842);page.Margin(40);page.Content().Column(column=> {column.Item().Text("Sample HVAC Supplies").FontSize(24);column.Item().PaddingTop(20).Text(title).FontSize(20);column.Item().Text("Document QA/2026/104 • 03/11/2026 • Page "+pageNumber);column.Item().PaddingTop(30).Text("Copper pipe   10 × ₹450.00     ₹4,500.00\nAir filters     5 × ₹400.00      ₹2,000.00\n\nSubtotal ₹6,500.00\nGST (18%) ₹1,170.00\nTotal ₹7,670.00").FontSize(15);column.Item().PaddingTop(30).Text("Fictional local QA fixture. No customer or business data.");});});}}).GeneratePdf(path);
        }
        private static void Assert(bool condition,string message){if(!condition)throw new InvalidOperationException(message);}
        private static void Expect<T>(Action action) where T:Exception {try{action();}catch(T){return;}throw new InvalidOperationException("Expected "+typeof(T).Name);}
    }
}
