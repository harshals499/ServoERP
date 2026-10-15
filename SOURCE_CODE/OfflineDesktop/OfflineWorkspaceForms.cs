using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Forms;
using HVAC_Pro_Desktop.Services;
using HVAC_Pro_Desktop.UI;
using Newtonsoft.Json.Linq;
using ServoERP.Infrastructure;

namespace HVAC_Pro_Desktop.OfflineDesktop
{
    internal class OfflineWindow : ServoFormBase
    {
        protected override bool SuppressAutomaticChildPolish => true;
        protected override bool EnableWorkspaceHeader => false;
        protected OfflineWindow()
        {
            BackColor=DS.BgPage;Font=DS.Body;StartPosition=FormStartPosition.CenterParent;
            MinimumSize=new Size(460,360);Text="ServoERP - Offline workspace";
        }
        protected static Button ActionButton(string text,EventHandler action)
        {
            var button=new Button {Text=text,AutoSize=true,Height=36,MinimumSize=new Size(110,36),FlatStyle=FlatStyle.Flat,BackColor=DS.White,ForeColor=DS.Primary700,Margin=new Padding(6)};
            button.FlatAppearance.BorderColor=DS.Border;button.Click+=action;return button;
        }
        protected void Report(Exception ex)
        {
            AppLogger.LogError("OfflineDesktop.UI",ex);
            MessageBox.Show(this,SensitiveDataRedactor.Redact(ex.Message),"ServoERP",MessageBoxButtons.OK,MessageBoxIcon.Warning);
        }
    }
    internal sealed class OfflineSignInForm : OfflineWindow
    {
        private readonly TextBox _username=new TextBox {Dock=DockStyle.Fill,MaxLength=200};
        private readonly TextBox _password=new TextBox {Dock=DockStyle.Fill,UseSystemPasswordChar=true,MaxLength=200};
        private readonly Label _message=new Label {Dock=DockStyle.Fill,AutoSize=true};
        public OfflineSignInForm()
        {
            Text="ServoERP - Offline sign-in";ClientSize=new Size(540,380);FormBorderStyle=FormBorderStyle.FixedDialog;MaximizeBox=false;
            var layout=new TableLayoutPanel {Dock=DockStyle.Fill,Padding=new Padding(28),ColumnCount=1,RowCount=8};
            layout.Controls.Add(new Label {Text="Work while the office server is off",AutoSize=true,Font=DS.H2,Margin=new Padding(0,0,0,18)});
            layout.Controls.Add(new Label {Text="Use an account previously signed in online on this PC.",AutoSize=true,Margin=new Padding(0,0,0,16)});
            layout.Controls.Add(new Label {Text="Username",AutoSize=true});layout.Controls.Add(_username);
            layout.Controls.Add(new Label {Text="Password",AutoSize=true,Margin=new Padding(0,16,0,0)});layout.Controls.Add(_password);
            var signIn=ActionButton("Sign in offline",async(s,e)=>
            {
                var button=(Button)s;button.Enabled=false;
                try { var user=_username.Text;var secret=_password.Text;await Task.Run(()=>OfflineDesktopCoordinator.OfflineLogin(user,secret));_password.Clear();OfflineDesktopCoordinator.Start(true);DialogResult=DialogResult.OK; }
                catch(Exception ex) { _message.Text=ex.Message; }
                finally { button.Enabled=true; }
            });
            AcceptButton=signIn;layout.Controls.Add(signIn);layout.Controls.Add(_message);Controls.Add(layout);
        }
    }
    internal sealed class OfflineRecordEditor : OfflineWindow
    {
        private readonly OfflineRecord _record;
        private readonly TableLayoutPanel _fields=new TableLayoutPanel {Dock=DockStyle.Top,AutoSize=true,ColumnCount=2,Padding=new Padding(12)};
        private readonly List<Action> _capture=new List<Action>();
        private DataGridView _lines;
        public OfflineRecord Result => _record;
        public OfflineRecordEditor(OfflineRecord record)
        {
            _record=new OfflineRecord {EntityId=record.EntityId,Module=record.Module,ServerId=record.ServerId,Revision=record.Revision,Status=record.Status,Data=(JObject)record.Data.DeepClone()};
            Text="ServoERP - "+record.Module.Replace("Drafts"," draft");ClientSize=new Size(760,650);
            var scroll=new Panel {Dock=DockStyle.Fill,AutoScroll=true};scroll.Controls.Add(_fields);Controls.Add(scroll);
            _fields.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute,155));_fields.ColumnStyles.Add(new ColumnStyle(SizeType.Percent,100));
            var data=_record.Data;
            if(record.Module=="Clients")
            {
                TextField("Company name","CompanyName");TextField("Contact name","PrimaryContact");TextField("Phone","Phone");TextField("Email","Email");TextField("Billing address","BillingAddress");TextField("City","City");TextField("Notes","Notes");
            }
            else if(record.Module=="Sites") { ClientField();TextField("Site name","SiteName");TextField("Address","Address");TextField("City","City"); }
            else if(record.Module=="Jobs")
            {
                ClientField();SiteField();TextField("Job title","Title");TextField("Description","Description");DateField("Scheduled","ScheduledDate");Choice("Priority","Priority",new[]{"Low","Medium","High","Urgent"});TextField("Notes","Notes");
            }
            else if(record.Module=="InvoiceDrafts")
            {
                ClientField();DateField("Invoice date","InvoiceDate");DateField("Due date","DueDate");TextField("Place of supply","PlaceOfSupply");Choice("GST mode","GSTMode",new[]{"IGST","CGST+SGST"});TextField("Notes","Notes");
                Add("Line items",new Label {Text="Draft inputs only. Tax, numbering and balances are verified online.",AutoSize=true});
                _lines=new DataGridView {Height=240,Dock=DockStyle.Fill,AllowUserToAddRows=true,AutoSizeColumnsMode=DataGridViewAutoSizeColumnsMode.Fill,RowHeadersVisible=false,BackgroundColor=DS.White};
                foreach(string name in new[]{"Description","Quantity","Rate","GSTPercent"})_lines.Columns.Add(name,name=="GSTPercent"?"GST %":name);
                foreach(var line in (data["LineItems"] as JArray ?? new JArray()))
                { int i=_lines.Rows.Add((string)line["Description"],(decimal?)line["Quantity"]??1,(decimal?)line["Rate"]??0,(decimal?)line["GSTPercent"]??18);_lines.Rows[i].Tag=line.DeepClone(); }
                Add("",_lines);
                _capture.Add(()=>
                {
                    _lines.EndEdit();var lines=new JArray();
                    foreach(DataGridViewRow row in _lines.Rows)
                    {
                        if(row.IsNewRow)continue;var line=row.Tag is JObject old?(JObject)old.DeepClone():new JObject();
                        line["Description"]=Convert.ToString(row.Cells[0].Value);line["Quantity"]=DecimalValue(row.Cells[1].Value);line["Rate"]=DecimalValue(row.Cells[2].Value);line["GSTPercent"]=DecimalValue(row.Cells[3].Value);line["IsBillable"]=true;line["Category"]=line["Category"]??"Service";line["Unit"]=line["Unit"]??"Nos";line["TaxType"]=line["TaxType"]??"Taxable";lines.Add(line);
                    }
                    data["LineItems"]=lines;data["PaymentStatus"]="Draft";data["InvoiceNumber"]=null;data["Description"]=lines.FirstOrDefault()?["Description"];
                });
            }
            else
            {
                var invoices=OfflineDesktopCoordinator.Store.Records("Invoices").Where(r=>(decimal?)r.Data["BalanceDue"]>0 && (bool?)r.Data["IsDeleted"]!=true).ToList();
                var combo=RecordCombo(invoices,(int?)data["InvoiceID"]??0,null);Add("Invoice",combo);
                _capture.Add(()=> { if(!(combo.SelectedItem is OfflineRecord invoice))throw new InvalidOperationException("Select a downloaded outstanding invoice.");data["InvoiceID"]=invoice.ServerId;data["ClientID"]=invoice.Data["ClientID"]; });
                TextField("Amount (INR)","AmountPaid");DateField("Payment date","PaymentDate");Choice("Payment mode","PaymentMode",new[]{"Bank Transfer","NEFT/RTGS","UPI","Cash","Cheque","DD"});TextField("Reference / UTR","ReferenceNumber");TextField("Notes","Notes");
            }
            var footer=new FlowLayoutPanel {Dock=DockStyle.Bottom,Height=54,FlowDirection=FlowDirection.RightToLeft,Padding=new Padding(8,0,8,0)};
            footer.Controls.Add(ActionButton("Save locally",(s,e)=> {try {foreach(var capture in _capture)capture();if(record.Module=="Jobs")data["JobTitle"]=data["Title"];if(record.Module=="PaymentDrafts")data["AmountPaid"]=DecimalValue(data["AmountPaid"]);DesktopSyncServer.Validate(new OfflineOperation {Module=record.Module,Data=data});DialogResult=DialogResult.OK;}catch(Exception ex){Report(ex);} }));
            footer.Controls.Add(ActionButton("Cancel",(s,e)=>DialogResult=DialogResult.Cancel));Controls.Add(footer);
        }
        private static decimal DecimalValue(object value) { if(!decimal.TryParse(Convert.ToString(value),System.Globalization.NumberStyles.Number,System.Globalization.CultureInfo.GetCultureInfo("en-IN"),out var result))throw new InvalidOperationException("Enter a valid number.");return result; }
        private void Add(string label,Control input) { int row=_fields.RowCount++;_fields.RowStyles.Add(new RowStyle(SizeType.AutoSize));_fields.Controls.Add(new Label {Text=label,AutoSize=true,Margin=new Padding(4,9,4,7)},0,row);input.Margin=new Padding(4,6,4,6);input.Dock=DockStyle.Fill;_fields.Controls.Add(input,1,row); }
        private void TextField(string label,string key) { var box=new TextBox {Text=(string)_record.Data[key]??"",MaxLength=key=="Notes"?1000:200};Add(label,box);_capture.Add(()=>_record.Data[key]=box.Text.Trim()); }
        private void DateField(string label,string key) { DateTime value;DateTime.TryParse((string)_record.Data[key],out value);var date=new DateTimePicker {Format=DateTimePickerFormat.Custom,CustomFormat="dd/MM/yyyy",Value=value.Year<1900?DateTime.Today:value};Add(label,date);_capture.Add(()=>_record.Data[key]=date.Value.Date); }
        private void Choice(string label,string key,string[] values) { var combo=new ComboBox {DropDownStyle=ComboBoxStyle.DropDownList};combo.Items.AddRange(values);combo.SelectedItem=(string)_record.Data[key];if(combo.SelectedIndex<0)combo.SelectedIndex=0;Add(label,combo);_capture.Add(()=>_record.Data[key]=Convert.ToString(combo.SelectedItem)); }
        private static ComboBox RecordCombo(List<OfflineRecord> rows,int id,string entity)
        {
            var combo=new ComboBox {DropDownStyle=ComboBoxStyle.DropDownList,DisplayMember="DisplayName"};foreach(var row in rows)combo.Items.Add(row);combo.SelectedItem=rows.FirstOrDefault(r=>(id>0&&r.ServerId==id)||r.EntityId.ToString("D")==entity);return combo;
        }
        private void ClientField()
        {
            var rows=OfflineDesktopCoordinator.Store.Records("Clients").Where(record=>record.Status!="Archived").ToList();int selectedId=(int?)_record.Data["ClientID"]??0;
            if(selectedId>0 && !rows.Any(r=>r.ServerId==selectedId))rows.Add(new OfflineRecord {EntityId=Guid.Empty,ServerId=selectedId,Module="Clients",Data=new JObject {["CompanyName"]="Current client"}});
            var combo=RecordCombo(rows,selectedId,(string)_record.Data["ClientEntityId"]);Add("Client",combo);
            _capture.Add(()=> {if(!(combo.SelectedItem is OfflineRecord client))throw new InvalidOperationException("Select a downloaded or locally created client.");_record.Data["ClientID"]=client.ServerId;_record.Data["ClientEntityId"]=client.EntityId.ToString("D");});
        }
        private void SiteField()
        {
            var combo=RecordCombo(OfflineDesktopCoordinator.Store.Records("Sites"),(int?)_record.Data["SiteID"]??0,(string)_record.Data["SiteEntityId"]);Add("Site (optional)",combo);
            _capture.Add(()=> {if(combo.SelectedItem is OfflineRecord site){_record.Data["SiteID"]=site.ServerId;_record.Data["SiteEntityId"]=site.EntityId.ToString("D");}});
        }
    }
    internal sealed class OfflineWorkspaceForm : OfflineWindow
    {
        private readonly ComboBox _module=new ComboBox {DropDownStyle=ComboBoxStyle.DropDownList,Width=180};
        private readonly DataGridView _records=new DataGridView {Dock=DockStyle.Fill,ReadOnly=true,AllowUserToAddRows=false,AutoGenerateColumns=false,SelectionMode=DataGridViewSelectionMode.FullRowSelect,MultiSelect=false,RowHeadersVisible=false,BackgroundColor=DS.White};
        private readonly TextBox _details=new TextBox {Dock=DockStyle.Fill,Multiline=true,ReadOnly=true,ScrollBars=ScrollBars.Both};
        private readonly Label _status=new Label {Dock=DockStyle.Bottom,Height=64,Padding=new Padding(12),BackColor=DS.Primary50};
        public OfflineWorkspaceForm()
        {
            Text="ServoERP - Offline workspace";ClientSize=new Size(1120,760);MinimumSize=new Size(850,560);
            if(!OfflineDesktopCoordinator.CanUseCurrentWorkspace())throw new InvalidOperationException(OfflineDesktopCoordinator.Status.StartsWith("Offline cache")?OfflineDesktopCoordinator.Status:"Sign in online once to prepare offline access on this PC.");
            var toolbar=new FlowLayoutPanel {Dock=DockStyle.Top,AutoSize=true,AutoSizeMode=AutoSizeMode.GrowAndShrink,Padding=new Padding(8),WrapContents=true};
            foreach(string module in OfflineModules.All)if(OfflineModules.HasPermission(OfflineDesktopCoordinator.Store.Profile()?.User,module,false) && new HVAC_Pro_Desktop.Services.Licensing.LicenseService().CanPerform(OfflineModules.PermissionKey(module),"View"))_module.Items.Add(module);
            _module.FormattingEnabled=true;_module.Format+=(s,e)=>e.Value=Convert.ToString(e.ListItem).Replace("InvoiceDrafts","Invoice drafts").Replace("PaymentDrafts","Payment drafts");
            toolbar.Controls.Add(_module);toolbar.Controls.Add(ActionButton("New",(s,e)=>Edit(true)));toolbar.Controls.Add(ActionButton("Edit draft / record",(s,e)=>Edit(false)));
            toolbar.Controls.Add(ActionButton("Sync now",async(s,e)=>await OfflineDesktopCoordinator.SyncOnceAsync()));toolbar.Controls.Add(ActionButton("Office review",async(s,e)=>await Review()));toolbar.Controls.Add(ActionButton("Finalize online",async(s,e)=>await FinalizeSelected()));
            _records.Columns.Add(new DataGridViewTextBoxColumn {DataPropertyName="DisplayName",HeaderText="Record / draft",AutoSizeMode=DataGridViewAutoSizeColumnMode.Fill});
            _records.Columns.Add(new DataGridViewTextBoxColumn {DataPropertyName="Status",HeaderText="Sync status",Width=150});
            var split=new SplitContainer {Dock=DockStyle.Fill,Width=1100,SplitterDistance=620};split.Panel1.Controls.Add(_records);split.Panel2.Controls.Add(_details);Controls.Add(split);Controls.Add(toolbar);Controls.Add(_status);
            _module.SelectedIndexChanged+=(s,e)=>Reload();_records.SelectionChanged+=(s,e)=>_details.Text=OfflinePresentation.Describe(Selected,OfflineDesktopCoordinator.Store);
            OfflineDesktopCoordinator.Changed+=StateChanged;FormClosed+=(s,e)=>OfflineDesktopCoordinator.Changed-=StateChanged;
            if(_module.Items.Count>0)_module.SelectedIndex=0;UpdateStatus();
        }
        private OfflineRecord Selected => _records.CurrentRow?.DataBoundItem as OfflineRecord;
        private void StateChanged(object sender,EventArgs e) { if(IsDisposed||!IsHandleCreated)return;BeginInvoke((Action)(()=>{UpdateStatus();Reload();})); }
        private void UpdateStatus() { var profile=OfflineDesktopCoordinator.Store.Profile();_status.Text=OfflineDesktopCoordinator.Status+"  Last download: "+(profile?.LastSnapshotUtc?.ToLocalTime().ToString("dd/MM/yyyy HH:mm")??"not ready")+"  Review: "+Math.Max(OfflineDesktopCoordinator.Store.CountStatus("Conflict"),OfflineDesktopCoordinator.Store.OfficeConflicts().Count)+"  Blocked: "+OfflineDesktopCoordinator.Store.CountStatus("Blocked"); }
        private void Reload() { string module=Convert.ToString(_module.SelectedItem);if(!string.IsNullOrWhiteSpace(module))
            {
                if(OfflineModules.HasPermission(OfflineDesktopCoordinator.Store.Profile()?.User,module,false) && new HVAC_Pro_Desktop.Services.Licensing.LicenseService().CanPerform(OfflineModules.PermissionKey(module),"View"))_records.DataSource=OfflineDesktopCoordinator.Store.Records(module);
                else {_records.DataSource=new List<OfflineRecord>();_details.Clear();}
            } }
        private void Edit(bool create)
        {
            try
            {
                var record=create?new OfflineRecord {EntityId=Guid.NewGuid(),Module=Convert.ToString(_module.SelectedItem)}:Selected;
                if(record==null)return;if(!OfflineModules.CanWrite(record.Module))throw new InvalidOperationException("Posted invoices/payments are cached for viewing. Choose InvoiceDrafts or PaymentDrafts to create a provisional entry.");
                if(record.Status=="Archived")throw new InvalidOperationException("This record was archived at the office. It is available offline for viewing only.");
                if(record.Status=="Finalized")throw new InvalidOperationException("This draft was finalized online. Use the normal office workflow for corrections.");
                bool reviewed=record.Status=="Blocked";
                if(reviewed && (OfflineDesktopCoordinator.IsDisconnected || !ServoConfirmDialog.Show(this,"Revise blocked save","The prior attempt remains in history. Submit a new proposal; current office changes will still be checked.")))return;
                using(var editor=new OfflineRecordEditor(record))if(editor.ShowDialog(this)==DialogResult.OK) {OfflineDesktopCoordinator.Save(editor.Result,reviewed);Reload();UpdateStatus();}
            }
            catch(Exception ex){Report(ex);}
        }
        private DesktopSyncServer Server() => new DesktopSyncServer(OfflineDesktopCoordinator.TestConnectionString??HVAC_Pro_Desktop.DAL.DatabaseManager.RequireConfiguredConnectionString(),OfflineDesktopCoordinator.Store,OfflineDesktopCoordinator.Store.Profile());
        private async Task FinalizeSelected()
        {
            try
            {
                var record=Selected;if(record==null||!OfflineModules.IsDraft(record.Module))throw new InvalidOperationException("Select an invoice/payment draft first.");
                if(OfflineDesktopCoordinator.IsDisconnected)throw new InvalidOperationException("Finalization requires the office server. The local draft stays saved.");
                if(!ServoConfirmDialog.Show(this,"Finalize on office server","This will create an official "+(record.Module=="InvoiceDrafts"?"invoice":"payment")+" using current office validation. Review the draft details before confirming."))return;
                int id=await Task.Run(()=>Server().FinalizeDraft(record.EntityId,record.Revision));await OfflineDesktopCoordinator.SyncOnceAsync();MessageBox.Show(this,"Finalized on the office server. Record #"+id,"ServoERP",MessageBoxButtons.OK,MessageBoxIcon.Information);
            }
            catch(Exception ex){Report(ex);}
        }
        private async Task Review()
        {
            try
            {
                if(OfflineDesktopCoordinator.IsDisconnected)throw new InvalidOperationException("Office review requires the server. Both copies remain saved.");
                int legacy=OfflineDesktopCoordinator.LegacyPendingCount();
                if(legacy>0)MessageBox.Show(this,legacy+" older offline save(s) are preserved in the previous queue. Their source office/account was not recorded, so automatic replay is disabled. Have the office review their origin before importing them.","ServoERP - Previous offline work",MessageBoxButtons.OK,MessageBoxIcon.Information);
                var conflicts=await Task.Run(()=>Server().OpenConflicts());if(conflicts.Count==0){MessageBox.Show(this,"No conflicts need office review.","ServoERP");return;}
                using(var dialog=new OfflineConflictForm(conflicts,Server()))dialog.ShowDialog(this);
                await OfflineDesktopCoordinator.SyncOnceAsync();
            }
            catch(Exception ex){Report(ex);}
        }
    }
    internal sealed class OfflineConflictForm : OfflineWindow
    {
        public OfflineConflictForm(List<OfflineOperation> conflicts,DesktopSyncServer server)
        {
            Text="ServoERP - Office conflict review";ClientSize=new Size(1000,700);MinimumSize=new Size(850,560);
            var choices=new ComboBox {Dock=DockStyle.Top,DropDownStyle=ComboBoxStyle.DropDownList,DisplayMember="Error"};choices.DataSource=conflicts;
            var local=new TextBox {Dock=DockStyle.Fill,Multiline=true,ReadOnly=true,ScrollBars=ScrollBars.Both};var office=new TextBox {Dock=DockStyle.Fill,Multiline=true,ReadOnly=true,ScrollBars=ScrollBars.Both};
            var split=new SplitContainer {Dock=DockStyle.Fill,Width=1000,SplitterDistance=490};split.Panel1.Controls.Add(local);split.Panel2.Controls.Add(office);
            var footer=new FlowLayoutPanel {Dock=DockStyle.Bottom,Height=62,Padding=new Padding(8)};
            footer.Controls.Add(ActionButton("Keep office copy",async(s,e)=>
            {
                var op=choices.SelectedItem as OfflineOperation;if(op==null)return;
                if(!ServoConfirmDialog.Show(this,"Keep office copy","Keep the current office record and resolve this local proposal. The proposal remains in review history."))return;
                try {await Task.Run(()=>server.KeepOfficeCopy(op.OperationId));if(op.ServerCopy!=null)OfflineDesktopCoordinator.Store.KeepServer(op);conflicts.Remove(op);choices.DataSource=null;choices.DataSource=conflicts;choices.DisplayMember="Error";}catch(Exception ex){Report(ex);}
            }));
            footer.Controls.Add(ActionButton("Revise local proposal",async(s,e)=>
            {
                var op=choices.SelectedItem as OfflineOperation;if(op==null)return;if(op.ServerCopy==null){MessageBox.Show(this,"The office record was deleted. Keep the office decision or create a new record through the normal workflow.","ServoERP");return;}
                using(var editor=new OfflineRecordEditor(new OfflineRecord {EntityId=op.EntityId,Module=op.Module,ServerId=op.ServerId,Revision=OfflineWorkspaceStore.Revision(op.ServerCopy),Data=op.Data}))
                {
                    if(editor.ShowDialog(this)!=DialogResult.OK)return;
                    if(!ServoConfirmDialog.Show(this,"Apply reviewed changes","Submit the revised proposal against the displayed office version. A newer office edit will remain a conflict."))return;
                    try {var result=await Task.Run(()=>server.ApplyReviewed(op,editor.Result.Data));MessageBox.Show(this,result.Status=="Conflict"?"The office changed again. Both versions remain in review.":"Reviewed changes synchronized.","ServoERP");Close();}catch(Exception ex){Report(ex);}
                }
            }));
            choices.SelectedIndexChanged+=(s,e)=> {var op=choices.SelectedItem as OfflineOperation;local.Text="LOCAL PROPOSAL\r\n\r\n"+(op==null?"":OfflinePresentation.Describe(new OfflineRecord {EntityId=op.EntityId,Module=op.Module,Data=op.Data,Status="Needs review"},OfflineDesktopCoordinator.Store));office.Text="OFFICE COPY\r\n\r\n"+(op?.ServerCopy==null?"Deleted / unavailable":OfflinePresentation.Describe(new OfflineRecord {EntityId=op.EntityId,Module=op.Module,Data=op.ServerCopy,Status="Current office copy"},OfflineDesktopCoordinator.Store));};
            Controls.Add(split);Controls.Add(choices);Controls.Add(footer);if(conflicts.Count>0){choices.SelectedIndex=-1;choices.SelectedIndex=0;}
        }
    }
}
