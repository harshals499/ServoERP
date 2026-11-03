using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;
using FluentValidation;
using HVAC_Pro_Desktop.Models;
using HVAC_Pro_Desktop.Services;

namespace HVAC_Pro_Desktop.UI
{
    public sealed class DocumentImportDialog : ServoERP.Infrastructure.ServoFormBase
    {
        private readonly DocumentImportExtractionService _extractor = new DocumentImportExtractionService();
        private readonly DocumentImportService _service = new DocumentImportService();
        private readonly BindingList<DocumentImportDraft> _drafts = new BindingList<DocumentImportDraft>();
        private readonly ListBox _documents = new ListBox { Dock=DockStyle.Fill,IntegralHeight=false,HorizontalScrollbar=true };
        private readonly PropertyGrid _fields = new PropertyGrid { Dock=DockStyle.Fill,ToolbarVisible=false,PropertySort=PropertySort.Categorized };
        private readonly DataGridView _lines = Grid();
        private readonly DataGridView _evidence = Grid();
        private readonly PictureBox _preview = new PictureBox { Dock=DockStyle.Fill,SizeMode=PictureBoxSizeMode.Zoom,BackColor=DS.BgSubtle };
        private readonly TextBox _text = new TextBox { Dock=DockStyle.Fill,Multiline=true,ReadOnly=true,ScrollBars=ScrollBars.Both,WordWrap=false };
        private readonly TextBox _workbookPreview = new TextBox {Dock=DockStyle.Fill,Multiline=true,ReadOnly=true,ScrollBars=ScrollBars.Both,WordWrap=false,Visible=false};
        private readonly ComboBox _previewPage = new ComboBox {Dock=DockStyle.Top,DropDownStyle=ComboBoxStyle.DropDownList};
        private readonly Label _status = new Label { Dock=DockStyle.Fill,AutoEllipsis=true,Text="Choose PDF, JPG, PNG or an original MSE XLSX. Extraction stays on this PC; every proposed document requires review.",Padding=new Padding(8) };
        private readonly FlowLayoutPanel _actions = new FlowLayoutPanel { Dock=DockStyle.Fill,WrapContents=true,AutoScroll=true };
        private readonly Button _cancel = new Button { Text="Cancel operation",AutoSize=true,Enabled=false };
        private readonly ProgressBar _progress = new ProgressBar { Dock=DockStyle.Fill,Style=ProgressBarStyle.Marquee,Visible=false };
        private CancellationTokenSource _operation;
        private bool _busy;
        private LocalDocumentScanResult _lastDriveScan;
        private DocumentImportDraft Selected => _documents.SelectedItem as DocumentImportDraft;
        protected override bool EnableWorkspaceHeader => false;

        public DocumentImportDialog()
        {
            Text="ServoERP · Review document import"; Size=new Size(1260,860); MinimumSize=new Size(1000,720); StartPosition=FormStartPosition.CenterParent;
            BackColor=DS.BgPage; Font=new Font("Segoe UI",9);
            var root=new TableLayoutPanel { Dock=DockStyle.Fill,ColumnCount=1,RowCount=4,Padding=new Padding(12) };
            root.RowStyles.Add(new RowStyle(SizeType.Absolute,104)); root.RowStyles.Add(new RowStyle(SizeType.Percent,100)); root.RowStyles.Add(new RowStyle(SizeType.Absolute,48)); root.RowStyles.Add(new RowStyle(SizeType.Absolute,8));
            Controls.Add(root); root.Controls.Add(_actions,0,0); root.Controls.Add(_status,0,2); root.Controls.Add(_progress,0,3);
            AddAction("Upload document",async()=>await Upload());
            AddAction("Scan local drive",async()=>await DiscoverDrive());
            AddAction("Match party",async()=>await MatchParty());
            AddAction("Match selected item",async()=>await MatchItem());
            AddAction("Merge next page",()=> { Merge(); return Task.CompletedTask; });
            AddAction("Save reviewed draft",async()=>await Save());
            AddAction("Saved imports / original",async()=>await History());
            AddAction("Open selected original",()=> {if(Selected!=null&&File.Exists(Selected.SnapshotPath))Process.Start(new ProcessStartInfo(Selected.SnapshotPath){UseShellExecute=true});return Task.CompletedTask;});
            _actions.Controls.Add(_cancel); _cancel.Click+=(s,e)=>_operation?.Cancel();
            var body=new TableLayoutPanel { Dock=DockStyle.Fill,ColumnCount=3,RowCount=1 };
            body.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute,175)); body.ColumnStyles.Add(new ColumnStyle(SizeType.Percent,45)); body.ColumnStyles.Add(new ColumnStyle(SizeType.Percent,55));
            root.Controls.Add(body,0,1); body.Controls.Add(_documents,0,0);
            var previewTabs=new TabControl { Dock=DockStyle.Fill }; var previewPanel=new Panel {Dock=DockStyle.Fill}; previewPanel.Controls.Add(_preview); previewPanel.Controls.Add(_workbookPreview); previewPanel.Controls.Add(_previewPage); AddTab(previewTabs,"Original preview",previewPanel); AddTab(previewTabs,"Source text",_text); body.Controls.Add(previewTabs,1,0);
            _previewPage.SelectedIndexChanged+=(s,e)=>ShowPreview();
            var edit=new TableLayoutPanel { Dock=DockStyle.Fill,ColumnCount=1,RowCount=2 }; edit.RowStyles.Add(new RowStyle(SizeType.Percent,55)); edit.RowStyles.Add(new RowStyle(SizeType.Percent,45));
            edit.Controls.Add(_fields,0,0); var tabs=new TabControl { Dock=DockStyle.Fill }; AddTab(tabs,"Editable items (INR)",_lines); AddTab(tabs,"Confidence / source evidence",_evidence); edit.Controls.Add(tabs,0,1); body.Controls.Add(edit,2,0);
            _documents.DataSource=_drafts; _documents.SelectedIndexChanged+=(s,e)=>ShowSelected();
            _fields.PropertyValueChanged+=(s,e)=> { if(Selected==null)return; if(e.ChangedItem.PropertyDescriptor.Name!="ReviewConfirmed") Selected.ReviewConfirmed=false; if(e.ChangedItem.PropertyDescriptor.Name=="Direction"||e.ChangedItem.PropertyDescriptor.Name=="DocumentType") Selected.PartyId=0; _fields.Refresh(); };
            _lines.DataError+=(s,e)=> { e.ThrowException=false; _status.Text="Enter a valid numeric value. Use DD/MM/YYYY for document dates."; };
            _lines.CellValueChanged+=(s,e)=> { if(Selected!=null) { Selected.ReviewConfirmed=false; _fields.Refresh(); } };
            _evidence.ReadOnly=true; _evidence.AllowUserToAddRows=false; _evidence.AllowUserToDeleteRows=false;
            _lines.DataBindingComplete+=(s,e)=>ApplyReviewColumns();
            _evidence.DataBindingComplete+=(s,e)=> { foreach(DataGridViewColumn column in _evidence.Columns) {column.AutoSizeMode=DataGridViewAutoSizeColumnMode.None;column.Width=column.Name=="Source"?360:column.Name=="Page"?55:150;} };
            FormClosing+=(s,e)=> { if(_busy) { e.Cancel=true; _operation?.Cancel(); _status.Text="Waiting for the operation to finish safely…"; } };
        }
        private static DataGridView Grid() => new DataGridView {Dock=DockStyle.Fill,AutoGenerateColumns=true,AutoSizeColumnsMode=DataGridViewAutoSizeColumnsMode.DisplayedCells,BackgroundColor=DS.White,RowHeadersVisible=false};
        protected override void OnShown(EventArgs e)
        {
            base.OnShown(e);BeginInvoke((Action)ApplyReviewColumns);
        }
        private void ApplyReviewColumns()
        {
            _lines.AutoSizeColumnsMode=DataGridViewAutoSizeColumnsMode.None;
            foreach(DataGridViewColumn column in _lines.Columns){column.AutoSizeMode=DataGridViewAutoSizeColumnMode.None;column.MinimumWidth=column.Name=="Description"?200:100;column.Width=column.MinimumWidth;}
        }
        private static void AddTab(TabControl tabs,string title,Control content) { var tab=new TabPage(title); tab.Controls.Add(content); tabs.TabPages.Add(tab); }
        private void AddAction(string label,Func<Task> action)
        {
            var button=new Button {Text=label,AutoSize=true,MinimumSize=new Size(100,30),Margin=new Padding(3)};
            button.Click+=async(s,e)=>await Run(action); _actions.Controls.Add(button);
        }
        private async Task Run(Func<Task> action)
        {
            if(_busy)return; _busy=true; _operation=new CancellationTokenSource(); _progress.Visible=true;
            foreach(Control c in _actions.Controls)c.Enabled=c==_cancel; _fields.Enabled=false; _lines.Enabled=false; _documents.Enabled=false;
            try { await action(); }
            catch(OperationCanceledException) { _status.Text="Operation cancelled. Any completed save remains visible in history; retrying the same source cannot create another record."; }
            catch(FluentValidation.ValidationException ex) { _status.Text="Review required."; MessageBox.Show(this,string.Join(Environment.NewLine,ex.Errors.Select(x=>x.ErrorMessage)),"ServoERP · Review required",MessageBoxButtons.OK,MessageBoxIcon.Warning); }
            catch(Exception ex)
            {
                Serilog.Log.Warning("Document import UI operation failed with {ErrorType}",ex.GetType().Name);
                string message=ex is InvalidOperationException ? ex.Message : "The operation could not be completed. No partial business record was saved. Review the source and retry; check saved imports if the result is uncertain.";
                _status.Text=message; MessageBox.Show(this,message,"ServoERP · Document import",MessageBoxButtons.OK,MessageBoxIcon.Warning);
            }
            finally { _busy=false; _operation.Dispose(); _operation=null; _progress.Visible=false; foreach(Control c in _actions.Controls)c.Enabled=c!=_cancel; _fields.Enabled=true; _lines.Enabled=true; _documents.Enabled=true; }
        }
        private async Task Upload()
        {
            using(var picker=new OpenFileDialog {Filter="Business documents / MSE workbooks|*.pdf;*.jpg;*.jpeg;*.png;*.xlsx",Multiselect=false})
            {
                if(picker.ShowDialog(this)!=DialogResult.OK)return;
                var drafts=await _extractor.ExtractAsync(picker.FileName,new Progress<string>(s=>_status.Text=s),_operation.Token);
                int count=StageDrafts(drafts);
                _status.Text=count==0?"This source is already in the review queue. Review the existing entry; no records were saved.":"Review every source field, item, tax and document boundary. Workbook display rounding and cached numeric values can differ; no business records have been saved.";
            }
        }
        private int StageDrafts(List<DocumentImportDraft> drafts)
        {
            if(drafts.Count==0)return 0;
            string hash=drafts[0].FileHash;
            if(!string.IsNullOrEmpty(hash)&&_drafts.Any(d=>d.FileHash==hash))return 0;
            if(_drafts.Count+drafts.Count>5000)throw new InvalidOperationException("The review queue is limited to 5,000 documents. Finish this batch before adding another source.");
            int start=_drafts.Count;foreach(var draft in drafts)_drafts.Add(draft);_documents.SelectedIndex=start;
            return drafts.Count;
        }
        private async Task DiscoverDrive()
        {
            List<LocalDocumentCandidate> selected;
            using(var discovery=new LocalDocumentDiscoveryDialog(_lastDriveScan))
            {
                var choice=discovery.ShowDialog(this);_lastDriveScan=discovery.Result;
                if(choice!=DialogResult.OK)return;
                selected=discovery.SelectedFiles;
            }
            LocalDocumentBatchResult batch;bool loading=true;
            try{batch=await LocalDocumentBatchLoader.LoadAsync(selected,_extractor.ExtractAsync,StageDrafts,new Progress<string>(s=>{if(loading)_status.Text=s;}),_operation.Token);}
            finally{loading=false;}
            _status.Text=batch.ReadyDocuments+" documents ready for review; "+batch.DuplicateSources+" queued-source duplicates skipped; "+batch.Errors.Count+" files need attention. No business records saved. Reopen Scan local drive for remaining results.";
            if(batch.Errors.Count>0)MessageBox.Show(this,string.Join(Environment.NewLine,batch.Errors.Take(8))+(batch.Errors.Count>8?Environment.NewLine+"See the retained detection list for other files.":""),"ServoERP · Some files need attention",MessageBoxButtons.OK,MessageBoxIcon.Warning);
        }
        private void ShowSelected()
        {
            _fields.SelectedObject=Selected; _lines.DataSource=Selected?.Lines; _evidence.DataSource=Selected?.Evidence; _text.Text=Selected?.SourceText??"";
            bool workbook=Selected!=null&&Path.GetExtension(Selected.SnapshotPath)==".xlsx";
            _workbookPreview.Visible=workbook;_preview.Visible=!workbook;_previewPage.Visible=!workbook;_workbookPreview.Text=workbook?Selected.SourceText:"";
            _previewPage.Items.Clear();
            if(Selected!=null) for(int page=Selected.FirstPage;page<=Selected.LastPage;page++)_previewPage.Items.Add("Page "+page);
            if(_previewPage.Items.Count>0)_previewPage.SelectedIndex=0; else ShowPreview();
        }
        private void ShowPreview()
        {
            var old=_preview.Image; _preview.Image=null; old?.Dispose();
            string path=Selected?.PreviewPath; int index=_previewPage.SelectedIndex;
            if(Selected!=null && index>=0 && index<Selected.PreviewPaths.Count)path=Selected.PreviewPaths[index];
            if(File.Exists(path))using(var image=Image.FromFile(path))_preview.Image=new Bitmap(image);
        }
        private async Task MatchParty()
        {
            var draft=Selected; if(draft==null)return;
            if(draft.Direction==ImportDirection.Unknown)throw new InvalidOperationException("Choose sales or purchase direction before matching the party.");
            SessionManager.DemandPermission(draft.Direction==ImportDirection.Purchase?"Purchases":draft.DocumentType==ImportDocumentType.Quotation?"Quotations":"Invoices","Create");
            var parties=await Task.Run(()=>_service.GetParties(draft.Direction==ImportDirection.Purchase),_operation.Token);
            var party=Choose("Select existing party — review name and GSTIN",parties,draft.PartyName,draft.GSTIN);
            if(party==null)return; draft.PartyId=party.Id; draft.PartyName=party.Name; draft.ReviewConfirmed=false; _fields.Refresh();
        }
        private async Task MatchItem()
        {
            var line=_lines.CurrentRow?.DataBoundItem as DocumentImportLine; if(line==null)return;
            var items=await Task.Run(()=>_service.GetProducts(),_operation.Token); var item=Choose("Select existing product (optional)",items,line.Description,null);
            if(item!=null){line.ProductId=item.Id; Selected.ReviewConfirmed=false; _lines.Refresh(); _fields.Refresh();}
        }
        private ImportReference Choose(string title,List<ImportReference> items,string name,string gstin)
        {
            using(var dialog=new ImportChildDialog())
            {
                dialog.Text=title; dialog.Size=new Size(660,460); dialog.StartPosition=FormStartPosition.CenterParent;
                var search=new TextBox {Dock=DockStyle.Top,Text=name??""}; var list=new ListBox {Dock=DockStyle.Fill}; var select=new Button {Text="Use selected record",Dock=DockStyle.Bottom,Height=38,DialogResult=DialogResult.OK};
                Action filter=()=> { list.DataSource=items.Where(x=>(x.Name??"").IndexOf(search.Text,StringComparison.OrdinalIgnoreCase)>=0 || (!string.IsNullOrEmpty(gstin)&&string.Equals(x.GSTIN,gstin,StringComparison.OrdinalIgnoreCase))).ToList(); };
                search.TextChanged+=(s,e)=>filter(); dialog.Controls.Add(list); dialog.Controls.Add(search); dialog.Controls.Add(select); filter();
                return dialog.ShowDialog(this)==DialogResult.OK?list.SelectedItem as ImportReference:null;
            }
        }
        private void Merge()
        {
            var d=Selected; int index=_documents.SelectedIndex;
            if(d!=null&&Path.GetExtension(d.SnapshotPath)==".xlsx")throw new InvalidOperationException("Workbook rows are separate documents. They cannot be merged as continuation pages.");
            if(d==null||index+1>=_drafts.Count)return; var next=_drafts[index+1];
            if(d.FileHash!=next.FileHash||d.LastPage+1!=next.FirstPage)throw new InvalidOperationException("Only consecutive pages of the same file can be merged.");
            d.SourceText+="\r\n--- Page "+next.FirstPage+" ---\r\n"+next.SourceText; d.LastPage=next.LastPage; d.PreviewPaths.AddRange(next.PreviewPaths); d.Evidence.AddRange(next.Evidence); foreach(var line in next.Lines)d.Lines.Add(line); d.ReviewConfirmed=false;
            _drafts.Remove(next); _drafts.ResetItem(index); ShowSelected(); _status.Text="Continuation page merged. Review combined fields and totals. Select each original page above the preview.";
        }
        private async Task Save()
        {
            var draft=Selected;if(draft==null)return; _fields.Refresh(); if(!_lines.EndEdit())throw new InvalidOperationException("Correct the current item before saving."); BindingContext[_lines.DataSource]?.EndCurrentEdit();
            int id=await _service.SaveAsync(draft,_operation.Token);
            _status.Text="Saved "+draft.DocumentType+" #"+id+". Open the module list to continue its normal workflow. Original attachment is available in Saved imports. No stock, posting or payment action was taken.";
            _drafts.Remove(draft); ShowSelected();
        }
        private async Task History()
        {
            var records=await Task.Run(()=>_service.GetHistory(),_operation.Token);
            using(var dialog=new ImportChildDialog())
            {
                dialog.Text="Saved document imports · originals"; dialog.Size=new Size(960,500); dialog.StartPosition=FormStartPosition.CenterParent;
                var grid=Grid(); grid.ReadOnly=true; grid.AllowUserToAddRows=false; grid.DataSource=records;
                var open=new Button {Text="Open selected original",Dock=DockStyle.Bottom,Height=38}; open.Click+=(s,e)=> {var r=grid.CurrentRow?.DataBoundItem as ImportSavedRecord;if(r!=null&&SessionManager.HasPermission(r.Module,"View")&&File.Exists(r.FilePath))Process.Start(new ProcessStartInfo(r.FilePath){UseShellExecute=true});};
                dialog.Controls.Add(grid); dialog.Controls.Add(open); dialog.ShowDialog(this);
            }
        }
        protected override void Dispose(bool disposing) { if(disposing){_preview.Image?.Dispose();_extractor.Dispose();} base.Dispose(disposing); }
        private sealed class ImportChildDialog : ServoERP.Infrastructure.ServoFormBase
        {
            protected override bool EnableWorkspaceHeader => false;
        }
    }
}
