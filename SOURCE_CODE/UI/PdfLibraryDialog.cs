using System;
using System.Drawing;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Forms;
using HVAC_Pro_Desktop.DAL;
using HVAC_Pro_Desktop.Models;

namespace HVAC_Pro_Desktop.UI
{
    internal sealed class PdfCategoryPicker : ComboBox
    {
        internal PdfCategoryPicker()
        {
            Name = "PdfCategoryPicker";
            AccessibleName = global::HVAC_Pro_Desktop.Services.LanguageManager.Get("Search or select a PDF category");
            DropDownStyle = ComboBoxStyle.DropDown;
            AutoCompleteMode = AutoCompleteMode.Suggest;
            AutoCompleteSource = AutoCompleteSource.ListItems;
            Items.AddRange(PdfCategories.All.Select(value => (object)PdfCategories.Label(value)).ToArray());
        }

        internal PdfCategory? Category
        {
            get => PdfCategories.All.Cast<PdfCategory?>().FirstOrDefault(value =>
                string.Equals(Text.Trim(), PdfCategories.Label(value.Value), StringComparison.OrdinalIgnoreCase));
            set => Text = value.HasValue ? PdfCategories.Label(value.Value) : string.Empty;
        }
    }

    internal sealed class PdfLibraryDialog : ServoERP.Infrastructure.ServoFormBase
    {
        protected override bool EnableWorkspaceHeader => false;
        internal PdfLibraryDialog(PdfCategory? category)
        {
            global::HVAC_Pro_Desktop.Services.LanguageManager.SetCaption(this, "ServoERP · Imported PDFs"); Size=new Size(1000,650); MinimumSize=new Size(700,450); StartPosition=FormStartPosition.CenterParent; BackColor=DS.BgPage;
            var grid=new DataGridView { Dock=DockStyle.Fill, ReadOnly=true, AutoGenerateColumns=false, AllowUserToAddRows=false, AllowUserToDeleteRows=false, RowHeadersVisible=false, SelectionMode=DataGridViewSelectionMode.FullRowSelect, MultiSelect=false, BackgroundColor=Color.White };
            grid.Columns.Add(new DataGridViewTextBoxColumn { DataPropertyName="FileName",HeaderText=global::HVAC_Pro_Desktop.Services.LanguageManager.Get("Filename"),Width=280 });
            grid.Columns.Add(new DataGridViewTextBoxColumn { DataPropertyName="Category",HeaderText=global::HVAC_Pro_Desktop.Services.LanguageManager.Get("Category"),Width=130 });
            grid.Columns.Add(new DataGridViewTextBoxColumn { DataPropertyName="SourceFolder",HeaderText=global::HVAC_Pro_Desktop.Services.LanguageManager.Get("Source folder"),Width=240 });
            grid.Columns.Add(new DataGridViewTextBoxColumn { DataPropertyName="ImportedUtc",HeaderText=global::HVAC_Pro_Desktop.Services.LanguageManager.Get("Imported (local)"),Width=155 });
            grid.CellFormatting+=(s,e)=> { if(grid.Columns[e.ColumnIndex].DataPropertyName=="ImportedUtc" && e.Value is DateTime time) {e.Value=DateTime.SpecifyKind(time,DateTimeKind.Utc).ToLocalTime().ToString("dd/MM/yyyy HH:mm");e.FormattingApplied=true;} };
            var status=global::HVAC_Pro_Desktop.Services.LanguageManager.Caption(new Label { Dock=DockStyle.Top,Height=50,Padding=new Padding(12),Text="Loading imported PDFs…" });
            var open=global::HVAC_Pro_Desktop.Services.LanguageManager.Caption(new Button {Text="Open selected PDF",Dock=DockStyle.Bottom,Height=40});
            open.Click+=(s,e)=> { var row=grid.CurrentRow?.DataBoundItem as PdfLibraryRecord; if(row!=null) RecentDocumentOpenService.OpenStoredFile(this,row.FilePath,"ServoERP · Imported PDF"); };
            var change = global::HVAC_Pro_Desktop.Services.LanguageManager.Caption(new Button { Text = "Change category", Dock = DockStyle.Bottom, Height = 40, Enabled = false });
            var repository = new PdfReviewRepository();
            async Task ReloadAsync()
            {
                var rows = await Task.Run(() => repository.List(category));
                if (IsDisposed) return;
                grid.DataSource = rows;
                change.Enabled = rows.Count > 0;
                global::HVAC_Pro_Desktop.Services.LanguageManager.SetDataText(status, global::HVAC_Pro_Desktop.Services.LanguageManager.Format("{0} • {1} most recent attachments. No business transactions created.", (category.HasValue ? PdfCategories.Destination(category.Value) : "All imported PDFs"), rows.Count));
            }

            change.Click += async (s, e) =>
            {
                var row = grid.CurrentRow?.DataBoundItem as PdfLibraryRecord;
                if (row == null || !Enum.TryParse(row.Category, out PdfCategory current)) return;
                using (var dialog = new CategoryDialog())
                {
                    global::HVAC_Pro_Desktop.Services.LanguageManager.SetCaption(dialog, "ServoERP · Change PDF category");
                    dialog.StartPosition = FormStartPosition.CenterParent;
                    dialog.ClientSize = new Size(420, 180);
                    var picker = new PdfCategoryPicker { Category = current, Dock = DockStyle.Top };
                    var destination = new Label { Dock = DockStyle.Fill, Padding = new Padding(12) };
                    var save = global::HVAC_Pro_Desktop.Services.LanguageManager.Caption(new Button { Text = "Save category", DialogResult = DialogResult.OK, Dock = DockStyle.Bottom, Height = 40 });
                    void RefreshChoice()
                    {
                        var selected = picker.Category;
                        save.Enabled = selected.HasValue && selected != current;
                        global::HVAC_Pro_Desktop.Services.LanguageManager.SetDataText(destination, (selected.HasValue ? PdfCategories.Destination(selected.Value) : global::HVAC_Pro_Desktop.Services.LanguageManager.Get("Choose an available category.")));
                    }
                    picker.TextChanged += (sender, args) => RefreshChoice();
                    dialog.Controls.Add(destination);
                    dialog.Controls.Add(picker);
                    dialog.Controls.Add(save);
                    dialog.AcceptButton = save;
                    RefreshChoice();
                    if (dialog.ShowDialog(this) != DialogResult.OK || !picker.Category.HasValue) return;
                    var target = picker.Category.Value;
                    change.Enabled = false;
                    try
                    {
                        await Task.Run(() => repository.ChangeCategory(row.DocumentId, current, target));
                        await ReloadAsync();
                    }
                    catch (Exception ex)
                    {
                        Serilog.Log.Warning(ex, "Could not change imported PDF category for {DocumentId}", row.DocumentId);
                        if (IsDisposed) return;
                        global::HVAC_Pro_Desktop.Services.LanguageManager.SetCaption(status, "The category could not be updated or refreshed. Check your access and reopen Imported PDFs.");
                        change.Enabled = true;
                    }
                }
            };
            Controls.Add(grid);Controls.Add(change);Controls.Add(open);Controls.Add(status);
            Shown+=async(s,e)=>
            {
                try
                {
                    await ReloadAsync();
                }
                catch(Exception ex) {Serilog.Log.Warning("PDF library failed with {ErrorType}",ex.GetType().Name);if(!IsDisposed)global::HVAC_Pro_Desktop.Services.LanguageManager.SetCaption(status, "Imported PDFs could not be loaded. Check the database connection and your access, then reopen.");}
            };
        }
        private sealed class CategoryDialog : ServoERP.Infrastructure.ServoFormBase
        {
            protected override bool EnableWorkspaceHeader => false;
        }

        internal static void AddModuleAction(SharedPageHeaderModel model)
        {
            string title=(model.Title??"").Trim(); PdfCategory category;
            if(title=="Invoices"||title=="Invoice Dashboard")category=PdfCategory.Invoice;
            else if(title=="Purchases"||title=="Purchase Orders")category=PdfCategory.PurchaseOrder;
            else if(title=="Quotations"||title=="Quotation Dashboard")category=PdfCategory.Quotation;
            else return;
            if(model.RightActions.Any(c=>c.Name=="ImportedPdfLibrary"))return;
            var button=global::HVAC_Pro_Desktop.Services.LanguageManager.Caption(new Button {Name="ImportedPdfLibrary",Text="Imported PDFs",Width=120,Height=36,FlatStyle=FlatStyle.Flat});
            button.Click+=(s,e)=> {using(var dialog=new PdfLibraryDialog(category))dialog.ShowDialog(button.FindForm());};
            model.RightActions.Add(button);
        }
    }
}
