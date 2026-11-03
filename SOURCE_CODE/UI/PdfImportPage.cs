using System;
using System.Collections.Generic;
using System.Collections.Concurrent;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;
using HVAC_Pro_Desktop.DAL;
using HVAC_Pro_Desktop.Models;
using HVAC_Pro_Desktop.Services;

namespace HVAC_Pro_Desktop.UI
{
    internal sealed class PdfImportPage : ServoERP.Infrastructure.ServoPageBase
    {
        private readonly PdfReviewService _service = new PdfReviewService();
        private readonly PdfReviewRepository _repository = new PdfReviewRepository();
        private readonly BindingSource _source = new BindingSource();
        private readonly DataGridView _files = new DataGridView { Dock=DockStyle.Fill, AutoGenerateColumns=false, AllowUserToAddRows=false, AllowUserToDeleteRows=false, RowHeadersVisible=false, MultiSelect=false, SelectionMode=DataGridViewSelectionMode.FullRowSelect, BackgroundColor=Color.White };
        private readonly Panel _review = new Panel { Dock=DockStyle.Fill };
        private readonly Panel _summary = new Panel { Dock=DockStyle.Fill, Visible=false };
        private readonly Label _sourceLabel = new Label { Dock=DockStyle.Fill, AutoEllipsis=true, Padding=new Padding(8), Text="Choose a folder to find PDFs in all its subfolders. All found files are included by default." };
        private readonly Label _progress = new Label { Dock=DockStyle.Fill, AutoEllipsis=true, Padding=new Padding(8) };
        private readonly Label _status = new Label { Dock=DockStyle.Fill, AutoEllipsis=true, Padding=new Padding(8), Text="Select categories manually. Nothing is imported until Confirm & Import." };
        private readonly TextBox _search = new TextBox { Dock=DockStyle.Fill, AccessibleName="Search filenames or folders" };
        private readonly ComboBox _filter = new ComboBox { Dock=DockStyle.Fill, DropDownStyle=ComboBoxStyle.DropDownList };
        private readonly FlowLayoutPanel _categories = new FlowLayoutPanel { Dock=DockStyle.Fill, FlowDirection=FlowDirection.TopDown, WrapContents=false, AutoScroll=true, Padding=new Padding(8), BackColor=Color.White };
        private readonly Label _destination = new Label { Width=210, Height=102, Padding=new Padding(4), Text="Choose a category after reading the PDF.\r\n\r\nDocument attachments only. No business transaction will be created." };
        private readonly FlowLayoutPanel _actions = new FlowLayoutPanel { Dock=DockStyle.Fill, WrapContents=false, AutoScroll=true };
        private readonly Button _cancel;
        private readonly Button _save;
        private readonly Button _confirm;
        private Button _cancelImport;
        private readonly CheckBox _acknowledge = new CheckBox { Dock=DockStyle.Fill, AutoSize=false, Text="Leave unresolved files out of this import", Margin=new Padding(8,2,8,2) };
        private readonly Label _summaryCounts = new Label { Dock=DockStyle.Fill, Padding=new Padding(12), Font=DS.BodyBold };
        private readonly TextBox _unresolved = new TextBox { Dock=DockStyle.Fill, Multiline=true, ReadOnly=true, ScrollBars=ScrollBars.Vertical, BackColor=DS.Amber50 };
        private readonly DataGridView _destinations = new DataGridView { Dock=DockStyle.Fill, ReadOnly=true, AutoGenerateColumns=true, AllowUserToAddRows=false, AllowUserToDeleteRows=false, RowHeadersVisible=false, BackgroundColor=Color.White, AutoSizeColumnsMode=DataGridViewAutoSizeColumnsMode.Fill };
        private readonly PdfReviewViewer _viewer;
        private PdfReviewBatch _batch = new PdfReviewBatch();
        private PdfCategory? _pendingCategory;
        private PdfReviewFile _shown;
        private CancellationTokenSource _operation;
        private bool _binding;
        private bool _closing;
        private readonly ConcurrentDictionary<string,bool> _checkedHashes = new ConcurrentDictionary<string,bool>();
        private PdfReviewFile Selected => _files.CurrentRow?.DataBoundItem as PdfReviewFile;
        private bool Busy => _operation != null;
        internal bool HasPendingReview => Busy || _batch.Files.Any(file=>!file.Imported);
        public override void RefreshData()
        {
            RefreshReview();
            if(!Busy)_status.Text="Current review preserved. Choose another folder to start a new batch, or continue categorising these PDFs.";
        }

        internal PdfImportPage()
        {
            Name="PdfImportPage"; Dock=DockStyle.Fill; BackColor=DS.BgPage;
            _viewer=new PdfReviewViewer(_service); _viewer.Changed+=RefreshReview;
            var header=SharedPageHeader.Build(new SharedPageHeaderModel {Name="PdfImportHeader",Title="PDF Import",Subtitle="Select folder  ›  Review & categorise  ›  Confirm import",CompactUtility=true});
            var choose=ActionButton("Choose folder",async()=>await ChooseFolder(),true);
            var library=ActionButton("Imported PDFs",()=> {using(var dialog=new PdfLibraryDialog(null))dialog.ShowDialog(FindForm());return Task.CompletedTask;});
            var scanBar=new TableLayoutPanel {Dock=DockStyle.Fill,ColumnCount=3,RowCount=1};
            scanBar.ColumnStyles.Add(new ColumnStyle(SizeType.Percent,100));scanBar.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute,132));scanBar.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute,132));
            scanBar.Controls.Add(_sourceLabel,0,0);scanBar.Controls.Add(choose,1,0);scanBar.Controls.Add(library,2,0);
            var root=new TableLayoutPanel {Name="PdfImportWorkspace",Dock=DockStyle.Fill,ColumnCount=1,RowCount=5,Padding=new Padding(12),MinimumSize=new Size(1010,600)};
            root.RowStyles.Add(new RowStyle(SizeType.Absolute,56));root.RowStyles.Add(new RowStyle(SizeType.Absolute,40));root.RowStyles.Add(new RowStyle(SizeType.Percent,100));root.RowStyles.Add(new RowStyle(SizeType.Absolute,48));root.RowStyles.Add(new RowStyle(SizeType.Absolute,44));
            var scroll=new Panel {Dock=DockStyle.Fill,AutoScroll=true};scroll.Controls.Add(root);Controls.Add(scroll);Controls.Add(header.Header);
            root.Controls.Add(scanBar,0,0);root.Controls.Add(_progress,0,1);
            var stage=new Panel {Dock=DockStyle.Fill};stage.Controls.Add(_review);stage.Controls.Add(_summary);root.Controls.Add(stage,0,2);
            root.Controls.Add(_actions,0,3);root.Controls.Add(_status,0,4);
            var body=new TableLayoutPanel {Dock=DockStyle.Fill,ColumnCount=3,RowCount=1,Margin=Padding.Empty};
            body.ColumnStyles.Add(new ColumnStyle(SizeType.Percent,32));body.ColumnStyles.Add(new ColumnStyle(SizeType.Percent,68));body.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute,232));
            _review.Controls.Add(body);
            var list=new TableLayoutPanel {Dock=DockStyle.Fill,ColumnCount=1,RowCount=5,Margin=new Padding(0,0,8,0)};
            list.RowStyles.Add(new RowStyle(SizeType.Absolute,22));list.RowStyles.Add(new RowStyle(SizeType.Absolute,30));list.RowStyles.Add(new RowStyle(SizeType.Absolute,34));list.RowStyles.Add(new RowStyle(SizeType.Percent,100));list.RowStyles.Add(new RowStyle(SizeType.Absolute,38));
            list.Controls.Add(new Label {Text="Search filenames or folders",Dock=DockStyle.Fill},0,0);
            list.Controls.Add(_search,0,1);_filter.Items.AddRange(new object[]{"All files","Needs attention","Categorised","Excluded"});_filter.SelectedIndex=0;list.Controls.Add(_filter,0,2);list.Controls.Add(_files,0,3);
            var inclusion=new FlowLayoutPanel {Dock=DockStyle.Fill,WrapContents=false};
            inclusion.Controls.Add(ActionButton("Select all",()=> {SetInclusion(true);return Task.CompletedTask;}));
            inclusion.Controls.Add(ActionButton("Exclude all",()=> {SetInclusion(false);return Task.CompletedTask;}));list.Controls.Add(inclusion,0,4);
            _files.Columns.Add(new DataGridViewCheckBoxColumn {DataPropertyName="Include",HeaderText="✓",Width=32});
            _files.Columns.Add(new DataGridViewTextBoxColumn {DataPropertyName="FileName",HeaderText="Filename",Width=190,ReadOnly=true});
            _files.Columns.Add(new DataGridViewTextBoxColumn {DataPropertyName="Folder",HeaderText="Folder",Width=130,ReadOnly=true});
            _files.Columns.Add(new DataGridViewTextBoxColumn {DataPropertyName="Status",HeaderText="Review status",Width=180,ReadOnly=true});
            _files.DataSource=_source;_files.RowTemplate.Height=32;
            _files.CurrentCellDirtyStateChanged+=(s,e)=> {if(_files.IsCurrentCellDirty)_files.CommitEdit(DataGridViewDataErrorContexts.Commit);};
            _files.CellValueChanged+=(s,e)=> {if(!_binding&&e.ColumnIndex==0)RefreshReview();};
            // CurrentCellChanged runs after CurrentRow updates; SelectionChanged can still expose the previous row.
            _files.CurrentCellChanged+=async(s,e)=> {if(!_binding&&!Busy)await ShowSelected();};
            _files.CellToolTipTextNeeded+=(s,e)=> {if(e.RowIndex>=0&&_files.Rows[e.RowIndex].DataBoundItem is PdfReviewFile file)e.ToolTipText=file.SourcePath+"\r\n"+file.Status;};
            _files.DataError+=(s,e)=> {e.ThrowException=false;_status.Text="The selection could not be changed. Select the file again.";};
            _search.TextChanged+=(s,e)=>ApplyFilter();_filter.SelectedIndexChanged+=(s,e)=>ApplyFilter();
            body.Controls.Add(list,0,0);body.Controls.Add(_viewer,1,0);body.Controls.Add(_categories,2,0);
            _categories.Controls.Add(new Label {Text="Assign category",Font=DS.H3,Width=210,Height=34});
            foreach(var category in PdfCategories.All)
            {
                var value=category;var button=ActionButton(PdfCategories.Label(value),()=> {SelectCategory(value);return Task.CompletedTask;});button.Tag=value;button.Size=new Size(207,42);button.AutoSize=false;_categories.Controls.Add(button);
            }
            _categories.Controls.Add(_destination);
            _categories.Controls.Add(ActionButton("Keep this duplicate copy",()=> {KeepCopy();return Task.CompletedTask;}));
            _actions.Controls.Add(ActionButton("Previous",async()=>await MoveDocument(-1)));
            _actions.Controls.Add(ActionButton("Skip",async()=> {if(Selected!=null&&!Selected.Imported){Selected.Skipped=true;Selected.Category=null;await MoveDocument(1);RefreshReview();}}));
            _save=ActionButton("Save Selection & Next",async()=>await SaveNext(),true);_save.Width=192;_save.Enabled=false;_actions.Controls.Add(_save);
            _actions.Controls.Add(ActionButton("Review import summary",()=> {ShowSummary();return Task.CompletedTask;}));
            _cancel=ActionButton("Cancel operation",()=> {_operation?.Cancel();return Task.CompletedTask;});_cancel.Enabled=false;_actions.Controls.Add(_cancel);
            _confirm=ActionButton("Confirm & Import",async()=>await ConfirmImport(),true);
            BuildSummary();RefreshReview();
        }

        private Button ActionButton(string text,Func<Task> action,bool primary=false)
        {
            var button=new Button {Text=text,UseMnemonic=false,AutoSize=true,MinimumSize=new Size(100,34),Height=36,Margin=new Padding(4),FlatStyle=FlatStyle.Flat,BackColor=primary?DS.Primary600:Color.White,ForeColor=primary?Color.White:DS.Slate900};
            button.FlatAppearance.BorderColor=DS.Border;
            button.Click+=async(s,e)=>
            {
                if(Busy&&button!=_cancel&&button!=_cancelImport)return;
                try {await action();}
                catch(OperationCanceledException) {if(!IsDisposed)_status.Text="Operation cancelled. Completed imports remain saved; the remaining files are still available for review.";}
                catch(Exception ex) {Serilog.Log.Warning("PDF review action failed with {ErrorType}",ex.GetType().Name);if(!IsDisposed){_status.Text=ex is InvalidOperationException?ex.Message:"ServoERP could not complete this action. Check your connection or access and retry.";MessageBox.Show(FindForm(),_status.Text,"ServoERP · PDF Import",MessageBoxButtons.OK,MessageBoxIcon.Warning);}}
            };
            return button;
        }
        private async Task ChooseFolder()
        {
            SessionManager.DemandPermission("MasterData","View");
            using(var picker=new FolderBrowserDialog {Description="Choose a folder — all subfolders will be scanned for PDFs",ShowNewFolderButton=false})
            {
                if(picker.ShowDialog(FindForm())!=DialogResult.OK)return;
                if(_batch.Files.Any(f=>!f.Imported) && !ServoERP.Infrastructure.ServoConfirmDialog.Show(this,"Replace the current PDF review?","Unsaved category choices will be discarded. Already imported documents remain saved."))return;
                var token=BeginOperation("Scanning folders…");
                try
                {
                    List<string> warnings=null; string folder=picker.SelectedPath;
                    var progress=new Progress<string>(text=> {if(!IsDisposed&&Busy)_status.Text=text;});
                    var batch=await Task.Run(()=>PdfReviewService.Scan(folder,progress,token,out warnings),token);
                    if(IsDisposed)return;
                    _batch=batch;_shown=null;await _viewer.ShowAsync(null);_checkedHashes.Clear();_search.Clear();_filter.SelectedIndex=0;
                    _sourceLabel.Text=folder+"\r\nIncluding subfolders • "+batch.Files.Count+" PDFs found";
                    _status.Text=warnings.Count==0?"All PDFs included. Select a file to begin review.":string.Join(" • ",warnings.Take(3));
                    if(warnings.Count>0)MessageBox.Show(FindForm(),string.Join(Environment.NewLine,warnings.Take(20)),"ServoERP · Scan needs attention",MessageBoxButtons.OK,MessageBoxIcon.Information);
                    _summary.Visible=false;_review.Visible=true;_actions.Visible=true;
                }
                finally {EndOperation();}
                ApplyFilter(); await ShowSelected();
            }
        }
        private CancellationToken BeginOperation(string message)
        {
            _operation=new CancellationTokenSource();_status.Text=message;_cancel.Enabled=true;_cancelImport.Enabled=true;_review.Enabled=false;_confirm.Enabled=false;_acknowledge.Enabled=false;_search.Enabled=false;_filter.Enabled=false;RefreshReview();return _operation.Token;
        }
        private void EndOperation()
        {
            _operation?.Dispose();_operation=null;if(IsDisposed)return;_cancel.Enabled=false;_cancelImport.Enabled=false;_review.Enabled=true;_acknowledge.Enabled=true;_search.Enabled=true;_filter.Enabled=true;RefreshReview();
        }
        private void ApplyFilter()
        {
            if(Busy||_closing||IsDisposed||Disposing)return;
            var selected=Selected;string term=_search.Text.Trim();
            var rows=_batch.Files.Where(f=>(term.Length==0||f.FileName.IndexOf(term,StringComparison.OrdinalIgnoreCase)>=0||f.Folder.IndexOf(term,StringComparison.OrdinalIgnoreCase)>=0)
                && (_filter.SelectedIndex==0||_filter.SelectedIndex==1&&(f.Include&&!f.Ready&&!f.Imported)||_filter.SelectedIndex==2&&f.Category.HasValue||_filter.SelectedIndex==3&&!f.Include)).ToList();
            _binding=true;_source.DataSource=rows;if(selected!=null&&rows.Contains(selected))_source.Position=rows.IndexOf(selected);_binding=false;
            var ignored=ShowSelected();RefreshReview();
        }
        private async Task ShowSelected()
        {
            if(_closing||IsDisposed||Disposing||!IsHandleCreated)return;
            if(InvokeRequired) {RunOnUI(async()=>await ShowSelected());return;}
            var file=Selected;if(file==_shown)return;_shown=file;_pendingCategory=file?.Category;PaintCategories();RefreshReview();
            await _viewer.ShowAsync(file);
            if(_closing||IsDisposed||Disposing)return;
            if(file?.Hash!=null && !_checkedHashes.ContainsKey(file.Hash))
            {
                try {bool exists=await Task.Run(()=>_repository.Exists(file.Hash));if(!_closing&&!IsDisposed)RunOnUI(()=>{file.Existing=exists;_checkedHashes.TryAdd(file.Hash,true);});}
                catch(Exception ex) {Serilog.Log.Warning("PDF duplicate lookup deferred with {ErrorType}",ex.GetType().Name);if(!_closing&&!IsDisposed)RunOnUI(()=>_status.Text="Database duplicate check unavailable. It will be checked again during import.");}
            }
            if(!IsDisposed)RefreshReview();
        }
        private void SelectCategory(PdfCategory value)
        {
            _pendingCategory=value;PaintCategories();RefreshReview();
        }
        private void PaintCategories()
        {
            foreach(Control control in _categories.Controls)
                if(control is Button button&&button.Tag is PdfCategory value) {bool active=_pendingCategory==value;button.BackColor=active?DS.Primary600:Color.White;button.ForeColor=active?Color.White:DS.Slate900;}
            _destination.Text=(_pendingCategory.HasValue?"Destination:\r\n"+PdfCategories.Destination(_pendingCategory.Value):"Choose a category after reading the PDF.")+"\r\n\r\nDocument attachment only. No transaction created.";
        }
        private void RefreshReview()
        {
            if(_closing||IsDisposed||Disposing||!IsHandleCreated)return;
            if(InvokeRequired) {RunOnUI(RefreshReview);return;}
            _batch.RefreshDuplicates();
            int index=Selected==null?0:_batch.Files.IndexOf(Selected)+1;
            _progress.Text="Document "+index+" of "+_batch.Files.Count+"  •  "+_batch.Files.Count(f=>f.Include&&f.Category.HasValue&&!f.Skipped&&!f.Imported)+" categorised  •  "+_batch.Files.Count(f=>f.Include&&f.Skipped&&!f.Imported)+" skipped  •  "+_batch.Files.Count(f=>!f.Include)+" excluded  •  "+_batch.Files.Count(f=>f.Imported)+" imported  •  "+_batch.Unresolved+" unresolved";
            _files.Invalidate();
            if(_save!=null)_save.Enabled=!Busy&&!_viewer.Loading&&Selected!=null&&Selected.Include&&!Selected.Imported&&!Selected.Existing&&!Selected.Duplicate&&Selected.Previewed&&string.IsNullOrEmpty(Selected.Error)&&_pendingCategory.HasValue;
        }
        private void SetInclusion(bool include) {foreach(var file in _batch.Files.Where(f=>!f.Imported))file.Include=include;RefreshReview();}
        private void KeepCopy()
        {
            var file=Selected;if(file?.Hash==null||file.Existing||file.Imported)return;
            foreach(var other in _batch.Files.Where(f=>f!=file&&f.Hash==file.Hash&&!f.Imported))other.Include=false;
            file.Include=true;RefreshReview();
        }
        private async Task SaveNext()
        {
            if(_viewer.Loading||Selected==null||!_pendingCategory.HasValue)return;
            _batch.SaveChoice(Selected,_pendingCategory.Value);RefreshReview();await MoveDocument(1);
        }
        private async Task MoveDocument(int direction)
        {
            if(Selected==null)return;
            int index=_batch.Files.IndexOf(Selected);
            for(int i=index+direction;i>=0&&i<_batch.Files.Count;i+=direction)
                if(_batch.Files[i].Include&&!_batch.Files[i].Imported)
                {
                    _search.Text="";_filter.SelectedIndex=0;_source.Position=i;await ShowSelected();return;
                }
            if(direction>0)ShowSummary();
        }
        private void BuildSummary()
        {
            var root=new TableLayoutPanel {Dock=DockStyle.Fill,ColumnCount=1,RowCount=5,Padding=new Padding(8)};
            root.RowStyles.Add(new RowStyle(SizeType.Absolute,44));root.RowStyles.Add(new RowStyle(SizeType.Absolute,220));root.RowStyles.Add(new RowStyle(SizeType.Percent,100));root.RowStyles.Add(new RowStyle(SizeType.Absolute,38));root.RowStyles.Add(new RowStyle(SizeType.Absolute,46));
            root.Controls.Add(_summaryCounts,0,0);root.Controls.Add(_destinations,0,1);root.Controls.Add(_unresolved,0,2);root.Controls.Add(_acknowledge,0,3);
            var buttons=new FlowLayoutPanel {Dock=DockStyle.Fill,WrapContents=false};buttons.Controls.Add(ActionButton("Back to review",()=> {_summary.Visible=false;_review.Visible=true;_actions.Visible=true;return Task.CompletedTask;}));buttons.Controls.Add(_confirm);
            _cancelImport=ActionButton("Cancel import",()=> {_operation?.Cancel();return Task.CompletedTask;});_cancelImport.Enabled=false;buttons.Controls.Add(_cancelImport);root.Controls.Add(buttons,0,4);_summary.Controls.Add(root);
            _acknowledge.CheckedChanged+=(s,e)=>_confirm.Enabled=!Busy&&_batch.Ready>0&&(_batch.Unresolved==0||_acknowledge.Checked);
        }
        private void ShowSummary()
        {
            _files.EndEdit();RefreshReview();_summary.BringToFront();_summary.Visible=true;_review.Visible=false;_actions.Visible=false;
            _summaryCounts.Text="Review import summary • "+_batch.Files.Count+" found • "+_batch.Ready+" ready • "+_batch.Unresolved+" unresolved • "+_batch.Files.Count(f=>!f.Include)+" excluded";
            _destinations.DataSource=PdfCategories.All.Select(c=>new {Category=PdfCategories.Label(c),Destination=PdfCategories.Destination(c),Ready=_batch.Files.Count(f=>f.Ready&&f.Category==c)}).ToList();
            _unresolved.Text=_batch.Unresolved==0?"No unresolved PDFs. All included, unimported files are ready.":"Unresolved PDFs (not imported):"+Environment.NewLine+string.Join(Environment.NewLine,_batch.Files.Where(f=>f.Include&&!f.Imported&&!f.Ready).Select(f=>f.FileName+" — "+f.Status));
            _acknowledge.Checked=false;_acknowledge.Visible=_batch.Unresolved>0;_confirm.Text="Confirm & Import — "+_batch.Ready+" PDFs";_confirm.Enabled=_batch.Ready>0&&_batch.Unresolved==0;
            _status.Text="Imports document attachments only. No purchase orders, invoices, quotations, stock movements or payments will be created.";
        }
        private async Task ConfirmImport()
        {
            var files=_batch.ConfirmedFiles(_acknowledge.Checked);
            // Explicit final confirmation is required for the bulk write, after the detailed summary.
            if(!ServoERP.Infrastructure.ServoConfirmDialog.Show(this,"Confirm & Import "+files.Count+" PDFs?","Save the categorised document attachments to their listed sections. Unresolved and excluded files will be left out. No financial records, stock or payments will be created."))return;
            var token=BeginOperation("Importing reviewed document attachments…");int imported=0,failed=0;
            try
            {
                foreach(var file in files)
                {
                    token.ThrowIfCancellationRequested();
                    try {await Task.Run(()=>_repository.Import(file,token),token);file.Imported=true;file.Password=null;file.Error=null;imported++;}
                    catch(OperationCanceledException){throw;}
                    catch(Exception ex) {failed++;file.Error=ex is InvalidOperationException?ex.Message:"Import failed. Check database access or storage and retry.";Serilog.Log.Warning("Reviewed PDF import failed with {ErrorType}",ex.GetType().Name);}
                    if(IsDisposed)return;_status.Text=imported+" imported • "+failed+" failed • "+(files.Count-imported-failed)+" remaining";RefreshReview();
                }
            }
            finally {EndOperation();if(!IsDisposed){ShowSummary();_status.Text=imported+" PDFs imported; "+failed+" failed. Completed files remain saved. Failed files can be reopened and retried. No business transactions created.";}}
        }
        internal void LoadForTest(PdfReviewBatch batch)
        {
            _batch=batch;_sourceLabel.Text="Test fixture folder — all subfolders";ApplyFilter();
        }
        internal void SummaryForTest()=>ShowSummary();
        protected override void Dispose(bool disposing)
        {
            if(disposing){_closing=true;_viewer.Changed-=RefreshReview;_operation?.Cancel();_viewer.Dispose();_service.Dispose();_source.Dispose();foreach(var file in _batch.Files)file.Password=null;}
            base.Dispose(disposing);
        }
    }
}
