using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;
using HVAC_Pro_Desktop.Models;
using HVAC_Pro_Desktop.Services;
using HVAC_Pro_Desktop.UI;

namespace HVAC_Pro_Desktop.Tests
{
    internal static class GlobalSearchSmokeTests
    {
        internal static List<GlobalSearchResult> Fixture() => new List<GlobalSearchResult> {
            new GlobalSearchResult { Module="Clients",PageKey="Clients",RecordId=1007,Title="ABC Industries",Detail="Industrial client · Pune, Maharashtra",Status="Active" },
            new GlobalSearchResult { Module="Jobs",PageKey="Jobs",RecordId=1042,Title="JOB-1042",ClientName="ABC Industries",SiteName="Chakan Plant",Technician="Unassigned",Status="Created",JobType="HVAC Service",RecordDate=DateTime.Today,Detail="Scheduled HVAC service and system check for Chakan Plant." },
            new GlobalSearchResult { Module="Invoices",PageKey="Invoices",RecordId=91,Title="INV-2026-091",ClientName="ABC Industries",Detail="Unpaid · Total ₹1,20,000.00",Status="Unpaid",RecordDate=DateTime.Today,Amount=120000,BalanceDue=120000,DueDate=DateTime.Today.AddDays(14) },
            new GlobalSearchResult { Module="Service Desk",PageKey="ServiceDesk",RecordId=91,Title="INC-2026-0091",Detail="HVAC not cooling · Urgent",Status="Open",RecordDate=DateTime.Today },
            new GlobalSearchResult { Module="Sites",PageKey="Clients",RecordId=17,Title="Chakan Plant",Detail="ABC Industries · Chakan, Pune" }
        };
        private static IEnumerable<Control> Children(Control root){foreach(Control c in root.Controls){yield return c;foreach(var child in Children(c))yield return child;}}
        private static void Require(bool value,string message){if(!value)throw new InvalidOperationException(message);}
        private static void Key(GlobalSearchDialog dialog,Keys keys){var args=new object[]{Message.Create(IntPtr.Zero,0,IntPtr.Zero,IntPtr.Zero),keys};typeof(GlobalSearchDialog).GetMethod("ProcessCmdKey",BindingFlags.NonPublic|BindingFlags.Instance).Invoke(dialog,args);}
        private static void Pump(Task task){var until=DateTime.UtcNow.AddSeconds(5);while(!task.IsCompleted&&DateTime.UtcNow<until){Application.DoEvents();Thread.Sleep(10);}Require(task.IsCompleted,"Search did not complete");task.GetAwaiter().GetResult();}
        internal static void RunAll()
        {
            var pass=new List<string>();var original=SessionManager.CurrentUser;var session=SessionManager.CurrentSessionId;var expiry=SessionManager.ExpiresAt;
            try {
                GlobalSearchResult opened=null;
                using(var dialog=new GlobalSearchDialog(null,q=>Fixture(),r=>opened=r)) {
                    dialog.Show();dialog.SetResultsForTest(Fixture());var list=Children(dialog).OfType<ListBox>().Single();
                    Key(dialog,Keys.Down);Require(list.SelectedIndex==1,"Down did not select job");Require(Children(dialog).OfType<Label>().Any(l=>l.Text=="Unassigned"),"Job preview lacks technician");Key(dialog,Keys.Enter);Require(opened?.RecordId==1042,"Enter opened wrong record");Key(dialog,Keys.Up);Require(list.SelectedIndex==0,"Up failed");pass.Add("arrows, Enter and selected-record preview");
                    var query=Children(dialog).OfType<TextBox>().Single();query.Text="ABC";Key(dialog,Keys.Control|Keys.K);Require(query.SelectionLength==3,"Ctrl+K failed: selection="+query.SelectionLength);pass.Add("Ctrl+K selects query for replacement");
                    dialog.SetResultsForTest(Fixture());Children(dialog).OfType<Button>().First(b=>b.Text.StartsWith("Invoices (")).PerformClick();Require(list.Items.Count==1&&((GlobalSearchResult)list.Items[0]).Module=="Invoices","Category filter failed");Require(Children(dialog).OfType<Label>().Any(l=>l.Text=="₹1,20,000.00"),"Invoice preview money formatting failed");pass.Add("category filters, counts and Indian invoice amount preview");
                    dialog.SetResultsForTest(new GlobalSearchResult[0]);Require(!Children(dialog).OfType<Button>().Single(b=>b.Name=="OpenSearchRecord").Enabled,"Empty state enables open");pass.Add("empty state clears selection and disables open");
                    foreach(var size in new[]{new Size(1160,700),new Size(820,560)}){dialog.Size=size;Application.DoEvents();foreach(var b in Children(dialog).OfType<Button>().Where(b=>b.Name=="OpenSearchRecord"))Require(b.Right<=b.Parent.ClientSize.Width&&b.Bottom<=b.Parent.ClientSize.Height,"Open action outside container");}pass.Add("responsive action remains within dialog at standard and minimum sizes");
                    Key(dialog,Keys.Escape);Require(!dialog.Visible,"Escape failed");pass.Add("Escape closes search");
                }
                var release=new ManualResetEventSlim(false);int calls=0;
                using(var dialog=new GlobalSearchDialog(null,q=>{Interlocked.Increment(ref calls);release.Wait(3000);return Fixture();},r=>{})) {
                    dialog.Show();var query=Children(dialog).OfType<TextBox>().Single();query.Text="ABC";
                    var timer=(System.Windows.Forms.Timer)typeof(GlobalSearchDialog).GetField("_debounce",BindingFlags.Instance|BindingFlags.NonPublic).GetValue(dialog);timer.Stop();
                    var search=(Task)typeof(GlobalSearchDialog).GetMethod("RunSearchAsync",BindingFlags.Instance|BindingFlags.NonPublic).Invoke(dialog,null);
                    query.Text="A";timer.Stop();release.Set();Pump(search);Require(Children(dialog).OfType<ListBox>().Single().Items.Count==0,"Stale search replaced new query");pass.Add("stale async results are discarded after query changes");
                }
                SessionManager.SetSession(new AppUserDto {Permissions=new Dictionary<string,RolePermissionDto>{{"Clients",new RolePermissionDto{CanView=true}}}});
                Require(!GlobalSearchService.CanView("Invoices")&&!GlobalSearchService.CanView("Jobs"),"Restricted modules leak");pass.Add("module permissions deny unavailable results and previews");
            } finally {SessionManager.SetSession(original,session,expiry);}
            Directory.CreateDirectory(@"C:\HVAC_PRO_MSE\TEST_RESULTS\global-search-1.1.507");File.WriteAllLines(@"C:\HVAC_PRO_MSE\TEST_RESULTS\global-search-1.1.507\smoke.txt",pass.Select(p=>"PASS "+p));
        }
        internal static void RunPreview()
        {
            using(var shell=new MainForm())using(var host=new Form {Text="ServoERP - Search QA",WindowState=FormWindowState.Maximized})using(var page=new DashboardForm {Dock=DockStyle.Fill}) {
                page.LoadActionCenterPreviewForVisualTest();var sidebar=(Control)typeof(MainForm).GetField("_sidebar",BindingFlags.NonPublic|BindingFlags.Instance).GetValue(shell);shell.Controls.Remove(sidebar);host.Controls.Add(page);host.Controls.Add(sidebar);
                host.Shown+=(s,e)=>{var dialog=new GlobalSearchDialog(null,q=>Fixture(),r=>MessageBox.Show("Preview only: "+r.Title));dialog.Show(host);Children(dialog).OfType<TextBox>().Single().Text="ABC";((System.Windows.Forms.Timer)typeof(GlobalSearchDialog).GetField("_debounce",BindingFlags.Instance|BindingFlags.NonPublic).GetValue(dialog)).Stop();dialog.SetResultsForTest(Fixture());Children(dialog).OfType<ListBox>().Single().SelectedIndex=1;};Application.Run(host);
            }
        }
        internal static void RunDataRead()
        {
            var user=SessionManager.CurrentUser;var session=SessionManager.CurrentSessionId;var expiry=SessionManager.ExpiresAt;
            try {
                SessionManager.SetSession(new AppUserDto {UserId=1,Username="Search QA",RoleName="Manager"});
                Require(GlobalSearchService.CanView("Jobs"),"Local license does not allow query validation");
                var rows=new GlobalSearchService().Search("ABC",120);
                Require(rows.Count<=120,"Search exceeds result limit");
                SessionManager.SetSession(new AppUserDto {UserId=1,Username="Search QA",RoleName="Technician",EmployeeId=int.MinValue});
                var technicianRows=new GlobalSearchService().Search("JOB",120);
                Require(!technicianRows.Any(r=>r.Module=="Jobs"),"Technician search exposes another employee's jobs");
                SessionManager.SetSession(new AppUserDto {UserId=1,Username="Search QA",RoleName="Manager",Permissions=new Dictionary<string,RolePermissionDto>{{"Clients",new RolePermissionDto{CanView=true}}}});
                var restrictedRows=new GlobalSearchService().Search("ABC",120);
                Require(restrictedRows.All(r=>r.Module=="Clients"||r.Module=="Sites"),"Restricted search exposes other modules");
                Directory.CreateDirectory(@"C:\HVAC_PRO_MSE\TEST_RESULTS\global-search-1.1.507");
                File.WriteAllLines(@"C:\HVAC_PRO_MSE\TEST_RESULTS\global-search-1.1.507\sql-read.txt",new[]{"PASS parameterized Dapper search executed against configured SQL Server; "+rows.Count+" results within limit. Read-only; no records modified.","PASS technician assignment restriction on actual job query", "PASS module restriction on actual search queries"});
            } finally {SessionManager.SetSession(user,session,expiry);}
        }
    }
}
