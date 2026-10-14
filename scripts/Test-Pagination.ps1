param([string]$OutputDir = 'C:\HVAC_PRO_MSE\TEST_RESULTS\pagination-1.1.505')
$ErrorActionPreference='Stop'
$release=Join-Path (Split-Path $PSScriptRoot -Parent) 'SOURCE_CODE\bin\Release'
$source=@'
using System;
using System.IO;
using System.Linq;
using System.Drawing;
using System.Reflection;
using System.Collections.Generic;
using System.Windows.Forms;
using HVAC_Pro_Desktop.UI;
using HVAC_Pro_Desktop.UI.Controls;
class PaginationVerification
{
 static List<string> report = new List<string>();
 static void Assert(bool ok, string message) { if(!ok) throw new Exception(message); }
 static IEnumerable<Control> Tree(Control c) { yield return c; foreach(Control x in c.Controls) foreach(Control d in Tree(x)) yield return d; }
 static Control Find(Control c,string n) { return Tree(c).First(x=>x.Name==n); }
 static void Capture(Form f,string path) { Application.DoEvents(); using(var b=new Bitmap(f.Width,f.Height)) { f.DrawToBitmap(b,new Rectangle(Point.Empty,b.Size)); b.Save(path); } }
 static void Bounds(Control root)
 {
  foreach(Control c in root.Controls)
  {
   if(!c.Visible) continue;
   Assert(c.Left>=0 && c.Top>=0 && c.Right<=root.ClientSize.Width && c.Bottom<=root.ClientSize.Height,"Out of bounds: "+c.Name+" "+c.Bounds+" in "+root.Name+" "+root.ClientSize);
   Bounds(c);
  }
  var controls=root.Controls.Cast<Control>().Where(c=>c.Visible).ToArray();
  for(int i=0;i<controls.Length;i++) for(int j=i+1;j<controls.Length;j++)
   Assert(!controls[i].Bounds.IntersectsWith(controls[j].Bounds),"Overlap: "+controls[i].Name+" and "+controls[j].Name+" in "+root.Name);
 }
 [STAThread] static int Main(string[] args)
 {
  string output=args[0]; Directory.CreateDirectory(output);
  try { Run(output); File.WriteAllLines(Path.Combine(output,"verification.txt"),report); return 0; }
  catch(Exception ex) { File.WriteAllText(Path.Combine(output,"failure.txt"),ex.ToString()); Console.WriteLine(ex); return 1; }
 }
 static void Run(string output)
 {
  Application.EnableVisualStyles();
  foreach(int width in new[]{1200,900,760,680,600,560,420,340,300,280})
  using(var host=new Form { Text="ServoERP pagination verification",ClientSize=new Size(width,180),BackColor=Color.White })
  using(var pager=new GlobalPaginationControl { Dock=DockStyle.Bottom })
  {
   host.Controls.Add(pager); host.Show();
   int pageEvents=0,sizeEvents=0;
   pager.PageChanged+=(s,e)=>pageEvents++;
   pager.PageSizeChanged+=(s,e)=>sizeEvents++;
   foreach(int count in new[]{0,1,10,11,164,397,1000000})
   foreach(int page in new[]{1,2,3,9,int.MaxValue})
   {
    pager.SetState(page,count,10); Application.DoEvents();
    UIHelper.ApplyButtonAlignment(host);
    typeof(UIHelper).Assembly.GetType("HVAC_Pro_Desktop.UI.DS").GetMethod("ApplyTheme").Invoke(null,new object[]{host});
    Application.DoEvents(); Bounds(pager);
    Assert(Tree(pager).OfType<Button>().Any(b=>b.Name=="PaginationPage_"+pager.CurrentPage),"Current page absent");
    Assert(Find(pager,"PaginationPrevious").Enabled==(count>0 && pager.CurrentPage>1),"Previous boundary wrong");
    Assert(Find(pager,"PaginationNext").Enabled==(count>0 && pager.CurrentPage<pager.TotalPages),"Next boundary wrong");
   }
   Assert(pageEvents==0 && sizeEvents==0,"SetState must be silent");
   pager.SetState(3,164,10);
   Capture(host,Path.Combine(output,"pagination-"+width+".png"));
   var current=(Button)Find(pager,"PaginationPage_3");
   Assert(current.BackColor.ToArgb()==Color.FromArgb(15,108,189).ToArgb() && current.ForeColor==Color.White,"Current page colour lost after styling");
   ((Button)Find(pager,"PaginationNext")).PerformClick();
   Assert(pager.CurrentPage==4 && pageEvents==1,"Next button did not navigate once");
   ((Button)Find(pager,"PaginationPrevious")).PerformClick();
   Assert(pager.CurrentPage==3 && pageEvents==2,"Previous button did not navigate once");
   var pageButton=Tree(pager).OfType<Button>().First(b=>b.Name.StartsWith("PaginationPage_") && b.Name!="PaginationPage_3");
   int target=int.Parse(pageButton.Text); pageButton.PerformClick();
   Assert(pager.CurrentPage==target && pageEvents==3,"Numbered navigation failed");
   var combo=(ComboBox)Find(pager,"PaginationPageSize"); combo.SelectedItem="25";
   Assert(pager.CurrentPage==1 && pager.PageSize==25 && sizeEvents==1 && pageEvents==3,"Page size did not reset exactly once");
   pager.SetState(99,164,10); Assert(pager.CurrentPage==17 && pager.DisplayTo==164,"Overflow not clamped");
   pager.SetState(-4,164,10); Assert(pager.CurrentPage==1,"Negative page not clamped");
   pager.SetState(3,164,37); Assert(combo.Items.Contains("37"),"Custom page size lost");
   pager.SetState(17,11,10); Assert(pager.CurrentPage==2,"Filtered total not clamped");
   report.Add("PASS numbered navigation, boundaries, custom sizes, silent state and styled bounds at "+width+"px");
  }
  using(var host=new Form { ClientSize=new Size(900,440) })
  using(var payroll=new PayrollForm())
  using(var grid=new DataGridView { Dock=DockStyle.Fill,AllowUserToAddRows=false })
  {
   grid.Columns.Add("Reference","Reference");
   var footer=(Control)typeof(PayrollForm).GetMethod("BuildPagerFooter",BindingFlags.Instance|BindingFlags.NonPublic).Invoke(payroll,new object[]{grid});
   host.Controls.Add(grid); host.Controls.Add(footer); host.Show();
   for(int i=0;i<26;i++) grid.Rows.Add("Entry "+(i+1));
   Application.DoEvents(); var pager=Tree(footer).OfType<GlobalPaginationControl>().Single();
   Assert(pager.TotalRecords==26 && grid.Rows.Cast<DataGridViewRow>().Count(r=>r.Visible)==10,"Payroll first page invalid");
   pager.GoToPage(3); Application.DoEvents(); Assert(grid.Rows.Cast<DataGridViewRow>().Count(r=>r.Visible)==6 && grid.Rows[20].Visible,"Payroll last page invalid");
   ((ComboBox)Find(pager,"PaginationPageSize")).SelectedItem="25"; Application.DoEvents();
   Assert(grid.Rows.Cast<DataGridViewRow>().Count(r=>r.Visible)==25 && pager.CurrentPage==1,"Payroll page size invalid");
   grid.Rows.Clear(); Assert(pager.TotalRecords==0,"Payroll empty state stale");
   for(int i=0;i<164;i++) grid.Rows.Add("Salary history "+(i+1));
   pager.SetState(3,164,10); pager.GoToPage(4); pager.GoToPage(3); Application.DoEvents();
   Capture(host,Path.Combine(output,"payroll-integration.png"));
   host.ClientSize=new Size(420,440); Application.DoEvents(); Bounds(pager); Assert(pager.Bottom<=footer.Height,"Payroll footer clipped");
   Capture(host,Path.Combine(output,"payroll-narrow.png"));
   Assert(footer.Bottom<=host.ClientSize.Height,"Payroll host clipped: "+footer.Bounds+" in "+host.ClientSize);
   report.Add("PASS actual payroll footer: real row slicing, last page, size changes, clear/reload and narrow host");
  }
  foreach(string method in new[]{"EnsureGlobalPaginationControlNavigatesSafely","EnsureGlobalPaginationControlKeepsControlsInsideBounds"})
   typeof(UIHelper).Assembly.GetType("HVAC_Pro_Desktop.Tests.UiPolicyTests").GetMethod(method,BindingFlags.Static|BindingFlags.NonPublic).Invoke(null,null);
  report.Add("PASS existing pagination policy regressions");
  foreach(var spec in new[]{
   new[]{"ClientManagementForm","BuildAllClientsCard"},
   new[]{"ContractManagementForm","BuildRecentContractsCard"},
   new[]{"PaymentForm","BuildTransactionsCard"},
   new[]{"PurchaseForm","BuildPoTableCard"},
   new[]{"JobManagementForm","BuildAllJobsCard"},
   new[]{"InventoryForm","BuildInventoryFooter"}})
  {
   var type=typeof(UIHelper).Assembly.GetType("HVAC_Pro_Desktop.UI."+spec[0]);
   using(var page=(Control)Activator.CreateInstance(type))
   using(var host=new Form { Text="ServoERP "+spec[0],ClientSize=new Size(1000,600),BackColor=Color.White })
   {
    var card=(Control)type.GetMethod(spec[1],BindingFlags.Instance|BindingFlags.NonPublic).Invoke(page,null);
    card.Dock=DockStyle.Fill; host.Controls.Add(card); host.Show(); Application.DoEvents();
    var pager=Tree(card).OfType<GlobalPaginationControl>().Single();
    foreach(int width in new[]{1000,800,680,560,420})
    {
     host.ClientSize=new Size(width,600); Application.DoEvents(); pager.SetState(3,164,10);
     UIHelper.ApplyButtonAlignment(card); Application.DoEvents(); Bounds(pager);
     Assert(pager.Left>=0 && pager.Right<=pager.Parent.ClientSize.Width && pager.Top>=0 && pager.Bottom<=pager.Parent.ClientSize.Height,"Module footer clipped: "+spec[0]+" at "+width+" "+pager.Bounds+" in "+pager.Parent.ClientSize);
     if(width==1000 || width==560) Capture(host,Path.Combine(output,spec[0]+"-"+width+".png"));
    }
    report.Add("PASS actual "+spec[0]+" footer bounds and rendering at 1000,800,680,560,420px");
   }
  }
  using(var vendor=new VendorForm())
  using(var host=new Form { ClientSize=new Size(900,400) })
  {
   var content=(Control)typeof(VendorForm).GetMethod("BuildRecentVendorsResultsContent",BindingFlags.Instance|BindingFlags.NonPublic).Invoke(vendor,new object[]{host.ClientSize});
   host.Controls.Add(content); host.Show(); Application.DoEvents();
   var pager=Tree(content).OfType<GlobalPaginationControl>().Single();
   foreach(int width in new[]{900,560,420})
   {
    host.ClientSize=new Size(width,400); pager.SetState(3,164,10); Application.DoEvents(); Bounds(pager);
    Assert(pager.Right<=content.Width && pager.Bottom<=content.Height,"Supplier footer clipped");
    Capture(host,Path.Combine(output,"suppliers-"+width+".png"));
   }
   report.Add("PASS actual supplier footer at 900,560,420px");
  }
  using(var notifications=new NotificationCenterDialog(null))
  using(var host=new Form { ClientSize=new Size(900,500) })
  {
   var list=(System.Collections.IList)typeof(NotificationCenterDialog).GetField("_notifications",BindingFlags.Instance|BindingFlags.NonPublic).GetValue(notifications);
   var element=list.GetType().GetGenericArguments()[0];
   for(int i=0;i<26;i++) list.Add(Activator.CreateInstance(element));
   var pager=Tree(notifications).OfType<GlobalPaginationControl>().Single();
   var bottom=notifications.Controls.Cast<Control>().Single(c=>c.Dock==DockStyle.Bottom);
   notifications.Controls.Remove(bottom); host.Controls.Add(bottom); host.Show();
   typeof(NotificationCenterDialog).GetMethod("RenderPage",BindingFlags.Instance|BindingFlags.NonPublic).Invoke(notifications,new object[]{true});
   Assert(pager.TotalRecords==26 && pager.TotalPages==3,"Notification totals invalid");
   pager.GoToPage(3);
   var grid=(DataGridView)typeof(NotificationCenterDialog).GetField("_grid",BindingFlags.Instance|BindingFlags.NonPublic).GetValue(notifications);
   Assert(grid.Rows.Count==6,"Notification last page invalid");
   ((ComboBox)Find(pager,"PaginationPageSize")).SelectedItem="25";
   Assert(grid.Rows.Count==25 && pager.CurrentPage==1,"Notification page size invalid");
   host.ClientSize=new Size(420,500); Application.DoEvents(); Bounds(pager);
   Assert(pager.Bottom<=pager.Parent.Height,"Notification footer clipped: "+pager.Bounds+" parent "+pager.Parent.Bounds+" top "+pager.Parent.Parent.Bounds+" bottom "+bottom.Bounds);
   Capture(host,Path.Combine(output,"notifications-narrow.png"));
   Assert(bottom.Bottom<=host.ClientSize.Height,"Notification host clipped: "+bottom.Bounds+" in "+host.ClientSize);
   report.Add("PASS notification slicing, page size, scrollbar sync and responsive footer");
  }
 }
}
'@
New-Item -ItemType Directory -Force -Path $OutputDir | Out-Null
$src=Join-Path $OutputDir 'PaginationVerification.cs'
[IO.File]::WriteAllText($src,$source)
$exe=Join-Path $release 'PaginationVerification.exe'
& "$env:WINDIR\Microsoft.NET\Framework64\v4.0.30319\csc.exe" /nologo /target:exe "/out:$exe" "/reference:$(Join-Path $release 'HVAC_Pro_Desktop.exe')" /reference:System.Windows.Forms.dll /reference:System.Drawing.dll /reference:System.Core.dll $src
if($LASTEXITCODE -ne 0) { throw 'Verification compile failed' }
Copy-Item -LiteralPath (Join-Path $release 'HVAC_Pro_Desktop.exe.config') -Destination ($exe+'.config') -Force
try { & $exe $OutputDir; if($LASTEXITCODE -ne 0) { throw 'Pagination verification failed' } }
finally {
 Move-Item -LiteralPath $exe -Destination (Join-Path $OutputDir 'PaginationVerification.exe') -Force -ErrorAction SilentlyContinue
 Move-Item -LiteralPath ($exe+'.config') -Destination (Join-Path $OutputDir 'PaginationVerification.exe.config') -Force -ErrorAction SilentlyContinue
}
Get-Content (Join-Path $OutputDir 'verification.txt')
