using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Data.SqlClient;
using System.Threading.Tasks;
using System.Windows.Forms;
using HVAC_Pro_Desktop.DAL;
using HVAC_Pro_Desktop.Models;
using HVAC_Pro_Desktop.Services;
using ServoERP.Infrastructure;

// Tests only the explicitly named restored database. Never runs against a customer database.
class ActionAuditHarness
{
    static readonly string Source = @"C:\HVAC_PRO_MSE\SOURCE_CODE\bin\Release";
    static readonly string Output = @"C:\HVAC_PRO_MSE\TEST_RESULTS\action-audit-1.1.486";
    static readonly List<string> Results = new List<string>();
    static Assembly App;
    [STAThread] static void Main(string[] args)
    {
        AppDomain.CurrentDomain.AssemblyResolve += (s,e) => { string p=Path.Combine(Source,new AssemblyName(e.Name).Name+".dll"); return File.Exists(p)?Assembly.LoadFrom(p):null; };
        Run(args);
    }
    static void Run(string[] args)
    {
        string name=args.Length==0?"shared":args[0];
        try
        {
            Application.EnableVisualStyles();
            if(new SqlConnectionStringBuilder(DatabaseManager.RequireConfiguredConnectionString()).InitialCatalog != "HVAC_PRO_ActionAudit_20261005")
                throw new Exception("Refusing to run: isolated audit database is not configured.");
            App=typeof(HVAC_Pro_Desktop.UI.AddAMCForm).Assembly;
            var user=new AppUserDto { UserId=1,Username="action-audit",DisplayName="Action audit",RoleId=1,RoleName="Administrator",IsActive=true };
            foreach(string m in new[]{"Clients","Contracts","AMC","Jobs","Inventory","Payments","Purchases","Suppliers","Vendors","Employees","Attendance","Payroll","Reports","MasterData","Quotations","Invoices","SiteMonitor"})
                user.Permissions[m]=new RolePermissionDto {ModuleKey=m,CanView=true,CanCreate=true,CanEdit=true,CanDelete=true};
            SessionManager.SetSession(user,null,null);
            if(name=="shared") { SharedChecks(); File.WriteAllLines(Path.Combine(Output,name+".log"),Results); return; }
            Type type=App.GetType(name=="ConfirmSummary"?"ServoERP.Infrastructure.ServoConfirmDialog":"HVAC_Pro_Desktop.UI."+(name=="AMCEdit"?"AddAMCForm":name),true);
            Control control;
            if(name=="ConfirmSummary") control=(Control)Activator.CreateInstance(type,BindingFlags.Instance|BindingFlags.NonPublic,null,new object[]{"Delete AMC contract?","Record: AMC-AUDIT-001\r\n\r\nLinked invoices, jobs and purchase orders will be kept but unlinked. Contract equipment, visits, reminder drafts and SLA logs will be removed.\r\n\r\nThis cannot be undone."},null);
            else if(name=="AMCEdit") control=new HVAC_Pro_Desktop.UI.AddAMCForm(new ContractRepository().GetAll().First().ContractID);
            else control=(Control)Activator.CreateInstance(type);
            var form=control as Form;
            if(form==null) { form=new Form {Text="Action audit: "+name,ClientSize=new Size(1440,960)}; control.Dock=DockStyle.Fill; form.Controls.Add(control); }
            form.StartPosition=FormStartPosition.Manual;form.Location=new Point(0,0);
            form.Show(); form.Hide(); form.Show(); Pump(8000);
            var deferred=control as HVAC_Pro_Desktop.UI.DeferredPageControl;
            if(deferred!=null && deferred.HasDeferredLoad)
            {
                var loadWatch=System.Diagnostics.Stopwatch.StartNew();
                while(!deferred.DeferredLoadCompleted && loadWatch.ElapsedMilliseconds<16000) Pump(100);
                Results.Add("Deferred load completed: "+deferred.DeferredLoadCompleted);
            }
            if(args.Length>1 && args[1]=="hold") {form.Activate();Pump(45000);}
            if(name=="ClientImportRepairDialog")
            {
                var fixtureGrid=All(control).OfType<DataGridView>().First();
                fixtureGrid.Rows.Add(-1,"1.1","Q-AUDIT-01 / INR 540000");
                fixtureGrid.Rows.Add(-2,"Quotation Is Valid Upto 07 Days","No linked quotations");
                form.Text += " - synthetic visual fixture";
                var filter=All(control).OfType<TextBox>().First(b=>b.Name=="ImportedClientRecordFilter");
                filter.Text="1.1";
                All(control).OfType<Button>().First(b=>b.Name=="SelectAllShownButton").PerformClick();
                Check(fixtureGrid.SelectedRows.Count==1 && fixtureGrid.SelectedRows[0].Cells[0].Value.Equals(-1),"repair filter and select all shown exclude hidden fixture record");
                filter.Clear();
                Check(fixtureGrid.SelectedRows.Count==0,"changing repair filter clears previous selection");
                Pump(100);
            }
            Capture(form,name+"-1440.png"); Inventory(control,name);
            foreach(Button clear in All(control).OfType<Button>().Where(b=>b.Name=="ClearWorkspaceFiltersButton" && b.Visible && b.Enabled).ToList())
            { clear.PerformClick(); Pump(2000); Results.Add("PASS Clear Filters handler completed: "+name); }
            if(name=="DashboardForm")
            {
                Button customize=All(control).OfType<Button>().First(b=>b.Text=="Customize");
                customize.PerformClick();Pump(100);
                Check(customize.ContextMenuStrip!=null && customize.ContextMenuStrip.Items.Count==3,"Dashboard Customize opens lock, unlock and reset actions");
                customize.ContextMenuStrip.Close();
            }
            Capture(form,name+"-cleared.png");
            form.ClientSize=new Size(1024,768);Pump(2000);Capture(form,name+"-1024.png");
            Inventory(control,name+"-1024");
            File.WriteAllLines(Path.Combine(Output,name+".log"),Results);
            form.Dispose();
        }
        catch(Exception ex) { File.WriteAllText(Path.Combine(Output,name+".log"),"FAIL "+ex); Environment.ExitCode=1; }
    }
    static void SharedChecks()
    {
        Results.AddRange(HVAC_Pro_Desktop.Tests.DataQualitySmokeTests.RunAll());
        Results.AddRange(HVAC_Pro_Desktop.Tests.SmartImportDuplicateDetectorSmokeTests.RunAll());
        Results.AddRange(HVAC_Pro_Desktop.Tests.ImportPreflightSmokeTests.RunAll());
        var form=new Form {ClientSize=new Size(900,500)};
        var grid=new DataGridView {MultiSelect=true,SelectionMode=DataGridViewSelectionMode.FullRowSelect,AllowUserToAddRows=false,Dock=DockStyle.Fill};
        grid.Columns.Add("Name","Name");grid.Rows.Add("One");grid.Rows.Add("Hidden");grid.Rows.Add("Three");grid.Rows[1].Visible=false;
        form.Controls.Add(grid);form.Show();Pump(100);
        Type actions=App.GetType("HVAC_Pro_Desktop.UI.WorkspaceActionUi",true);
        var panel=(Control)actions.GetMethod("CreateSelectionActions",BindingFlags.Static|BindingFlags.NonPublic).Invoke(null,new object[]{grid});form.Controls.Add(panel);panel.BringToFront();
        HVAC_Pro_Desktop.UI.GridTheme.Apply(grid);
        Check(grid.MultiSelect,"theme preserves multiple selection");
        All(panel).OfType<Button>().First(b=>b.Name=="SelectAllShownButton").PerformClick();
        Check(grid.SelectedRows.Count==2 && !grid.Rows[1].Selected,"select all includes only shown rows");
        All(panel).OfType<Button>().First(b=>b.Name=="ClearRowSelectionButton").PerformClick();Check(grid.SelectedRows.Count==0,"clear selection clears every selected row");
        var save=new Button {Text="Save Changes"};var related=new Button {Text="Unavailable",Enabled=false};form.Controls.Add(save);form.Controls.Add(related);
        var pending=new TaskCompletionSource<bool>();int calls=0;
        Task run=SaveOperationRunner.RunAsync(save,"Saving...","Save Changes",()=>{calls++;return pending.Task;},null,related);
        Task duplicate=SaveOperationRunner.RunAsync(save,"Saving...","Save Changes",()=>{calls++;return Task.FromResult(true);},null,related);
        Check(calls==1 && !save.Enabled && !related.Enabled,"busy save blocks duplicate invocation");
        pending.SetResult(true);while(!run.IsCompleted) Pump(10);run.GetAwaiter().GetResult();
        Check(save.Enabled && !related.Enabled && save.Text=="Save Changes","save restores original enabled states");
        save.Visible=true;Pump(100);
        Check(actions.GetMethod("FindSaveButton",BindingFlags.Static|BindingFlags.NonPublic).Invoke(null,new object[]{form})==save,"Ctrl+S chooses the enabled editor save");
        form.Dispose();
        using(var cleanup=new HVAC_Pro_Desktop.UI.SmartImportDuplicateCleanupDialog())
        {
            Type cleanupType=cleanup.GetType();
            var visibleGroup=new SmartImportDuplicateGroup();var hiddenGroup=new SmartImportDuplicateGroup();
            cleanupType.GetField("_visibleItems",BindingFlags.Instance|BindingFlags.NonPublic).SetValue(cleanup,new List<SmartImportDuplicateGroup>{visibleGroup});
            var selected=(HashSet<SmartImportDuplicateGroup>)cleanupType.GetField("_selectedGroups",BindingFlags.Instance|BindingFlags.NonPublic).GetValue(cleanup);
            selected.Add(hiddenGroup);
            cleanupType.GetMethod("SetAllGroupsChecked",BindingFlags.Instance|BindingFlags.NonPublic).Invoke(cleanup,new object[]{true});
            Check(selected.Count==1 && selected.Contains(visibleGroup) && !selected.Contains(hiddenGroup),"select shown groups replaces hidden group selection");
            selected.Add(hiddenGroup);
            cleanupType.GetMethod("SetAllGroupsChecked",BindingFlags.Instance|BindingFlags.NonPublic).Invoke(cleanup,new object[]{false});
            Check(selected.Count==0,"clear all duplicate groups clears hidden group selection");
        }
        var inventory=new InventoryService();string unique="ACTION-AUDIT-"+Guid.NewGuid().ToString("N");
        var item=new StockItem {ItemName=unique,Unit="Nos",Category="QA",CurrentStock=2,LastPurchaseRate=100,ReorderLevel=1};
        int id=inventory.Create(item);item.ItemID=id;
        Check(inventory.GetById(id).ItemName==unique,"inventory Add persists and reloads from isolated SQL");
        item.CurrentStock=3;inventory.Update(item);Check(inventory.GetById(id).CurrentStock==3,"inventory Save persists and reloads");
        item.CurrentStock=-1;bool blocked=false;try {inventory.Update(item);}catch(Exception){blocked=true;}
        Check(blocked && inventory.GetById(id).CurrentStock==3,"invalid inventory Save leaves persisted data unchanged");
        inventory.Delete(id);Check(!inventory.GetAll().Any(i=>i.ItemID==id),"inventory Delete removes item from active list");
        var clients=new ClientService();var client=new B2BClient {CompanyName=unique,IndustryType="Other",PaymentTermsDays=30};
        int clientId=clients.CreateClient(client);client.ClientID=clientId;
        Check(new ClientRepository().GetById(clientId).CompanyName==unique,"client Add persists and reloads");
        client.CompanyName=unique+" edited";clients.UpdateClient(client);
        Check(new ClientRepository().GetById(clientId).CompanyName==client.CompanyName,"client Save persists and reloads");
        var sites=new SiteService();var site=new ClientSite {ClientID=clientId,SiteName="Audit site",Address="Test address",City="Pune"};
        int siteId=sites.Create(site);site.SiteID=siteId;
        site.SiteName="Audit site renamed";sites.Update(site);
        Check(sites.GetById(siteId).SiteName==site.SiteName,"site rename preserves identity and reloads");
        blocked=false;try { sites.Create(new ClientSite {ClientID=clientId,SiteName="  Audit SITE renamed  ",Address="Another address"}); }catch(InvalidOperationException){blocked=true;}
        Check(blocked,"duplicate site name under the same client is blocked");
        blocked=false;try { clients.CreateSite(new ClientSite {ClientID=clientId,SiteName="  Audit SITE renamed  ",Address="Another address"}); }catch(InvalidOperationException){blocked=true;}
        Check(blocked,"client-service site creation also blocks normalized duplicates");
        sites.Delete(siteId);Check(sites.GetById(siteId)==null,"site Delete removes isolated test record");
        clients.DeleteClient(clientId);Check(!clients.GetAllClients().Any(c=>c.ClientID==clientId),"client Delete removes isolated test record from active list");
        Results.AddRange(HVAC_Pro_Desktop.Tests.UiQaStateCatalogTests.RunAll());
        Results.AddRange(HVAC_Pro_Desktop.Tests.UiPolicyTests.RunAll());
        Results.Add("All SQL writes above used HVAC_PRO_ActionAudit_20261005; settings and external send actions were not invoked.");
    }
    static void Inventory(Control control,string name)
    {
        var rows=new List<string>{"Screen,Type,Name,Label,Visible,Enabled,HandlerPresent"};
        object clickKey=typeof(Control).GetField("EventClick",BindingFlags.NonPublic|BindingFlags.Static).GetValue(null);
        PropertyInfo events=typeof(System.ComponentModel.Component).GetProperty("Events",BindingFlags.Instance|BindingFlags.NonPublic);
        foreach(Control c in All(control))
        {
            var handlers=(System.ComponentModel.EventHandlerList)events.GetValue(c,null);
            bool hooked=handlers[clickKey]!=null;
            if(c is Button || c is ComboBox || c is TextBox || c is TabPage || (c is Label && hooked))
                rows.Add(string.Join(",",new[]{Csv(name),Csv(c.GetType().Name),Csv(c.Name),Csv(c.Text),c.Visible.ToString(),c.Enabled.ToString(),hooked.ToString()}));
            var grid=c as DataGridView;if(grid!=null)foreach(DataGridViewColumn column in grid.Columns)
                if(column is DataGridViewButtonColumn)rows.Add(string.Join(",",new[]{Csv(name),"GridButton",Csv(column.Name),Csv(column.HeaderText),column.Visible.ToString(),grid.Enabled.ToString(),"Cell handler needs source review"}));
        }
        foreach(Control h in All(control).Where(c=>c.Name.EndsWith("Header")))
        {
            var table=h.Parent as TableLayoutPanel;
            Results.Add("Header geometry "+h.Name+": "+h.Bounds+" parent "+h.Parent.Bounds+(table==null?"":" row="+table.GetPositionFromControl(h).Row+" styles="+string.Join("/",table.RowStyles.Cast<RowStyle>().Select(r=>r.Height.ToString()))));
        }
        File.WriteAllLines(Path.Combine(Output,name+"-controls.csv"),rows);
        Results.Add("Rendered controls inventoried; presence and handler wiring alone do not prove business action success.");
    }
    static string Csv(string s){return "\""+(s??"").Replace("\"","\"\"").Replace("\r"," ").Replace("\n"," ")+"\"";}
    static IEnumerable<Control> All(Control root){foreach(Control c in root.Controls){yield return c;foreach(Control d in All(c))yield return d;}}
    static void Pump(int ms){var watch=System.Diagnostics.Stopwatch.StartNew();while(watch.ElapsedMilliseconds<ms){Application.DoEvents();System.Threading.Thread.Sleep(10);}}
    static void Capture(Control c,string name){c.PerformLayout();c.Refresh();Pump(150);using(var b=new Bitmap(c.Width,c.Height)){c.DrawToBitmap(b,new Rectangle(Point.Empty,c.Size));Pump(150);c.DrawToBitmap(b,new Rectangle(Point.Empty,c.Size));b.Save(Path.Combine(Output,name));}}
    static void Check(bool value,string label){if(!value)throw new Exception(label);Results.Add("PASS "+label);}
}
