using System;
using System.Drawing;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Forms;
using HVAC_Pro_Desktop.DAL;
using HVAC_Pro_Desktop.Models;

namespace HVAC_Pro_Desktop.UI
{
    internal sealed class PdfLibraryDialog : ServoERP.Infrastructure.ServoFormBase
    {
        protected override bool EnableWorkspaceHeader => false;
        internal PdfLibraryDialog(PdfCategory? category)
        {
            Text="ServoERP · Imported PDFs"; Size=new Size(1000,650); MinimumSize=new Size(700,450); StartPosition=FormStartPosition.CenterParent; BackColor=DS.BgPage;
            var grid=new DataGridView { Dock=DockStyle.Fill, ReadOnly=true, AutoGenerateColumns=false, AllowUserToAddRows=false, AllowUserToDeleteRows=false, RowHeadersVisible=false, SelectionMode=DataGridViewSelectionMode.FullRowSelect, MultiSelect=false, BackgroundColor=Color.White };
            grid.Columns.Add(new DataGridViewTextBoxColumn { DataPropertyName="FileName",HeaderText="Filename",Width=280 });
            grid.Columns.Add(new DataGridViewTextBoxColumn { DataPropertyName="Category",HeaderText="Category",Width=130 });
            grid.Columns.Add(new DataGridViewTextBoxColumn { DataPropertyName="SourceFolder",HeaderText="Source folder",Width=240 });
            grid.Columns.Add(new DataGridViewTextBoxColumn { DataPropertyName="ImportedUtc",HeaderText="Imported (local)",Width=155 });
            grid.CellFormatting+=(s,e)=> { if(grid.Columns[e.ColumnIndex].DataPropertyName=="ImportedUtc" && e.Value is DateTime time) {e.Value=DateTime.SpecifyKind(time,DateTimeKind.Utc).ToLocalTime().ToString("dd/MM/yyyy HH:mm");e.FormattingApplied=true;} };
            var status=new Label { Dock=DockStyle.Top,Height=50,Padding=new Padding(12),Text="Loading imported PDFs…" };
            var open=new Button {Text="Open selected PDF",Dock=DockStyle.Bottom,Height=40};
            open.Click+=(s,e)=> { var row=grid.CurrentRow?.DataBoundItem as PdfLibraryRecord; if(row!=null) RecentDocumentOpenService.OpenStoredFile(this,row.FilePath,"ServoERP · Imported PDF"); };
            Controls.Add(grid);Controls.Add(open);Controls.Add(status);
            Shown+=async(s,e)=>
            {
                try
                {
                    var rows=await Task.Run(()=>new PdfReviewRepository().List(category));
                    if(IsDisposed)return;grid.DataSource=rows;
                    status.Text=(category.HasValue ? PdfCategories.Destination(category.Value) : "All imported PDFs")+" • "+rows.Count+" most recent attachments. No business transactions created.";
                }
                catch(Exception ex) {Serilog.Log.Warning("PDF library failed with {ErrorType}",ex.GetType().Name);if(!IsDisposed)status.Text="Imported PDFs could not be loaded. Check the database connection and your access, then reopen.";}
            };
        }
        internal static void AddModuleAction(SharedPageHeaderModel model)
        {
            string title=(model.Title??"").Trim(); PdfCategory category;
            if(title=="Invoices"||title=="Invoice Dashboard")category=PdfCategory.Invoice;
            else if(title=="Purchases"||title=="Purchase Orders")category=PdfCategory.PurchaseOrder;
            else if(title=="Quotations"||title=="Quotation Dashboard")category=PdfCategory.Quotation;
            else return;
            if(model.RightActions.Any(c=>c.Name=="ImportedPdfLibrary"))return;
            var button=new Button {Name="ImportedPdfLibrary",Text="Imported PDFs",Width=120,Height=36,FlatStyle=FlatStyle.Flat};
            button.Click+=(s,e)=> {using(var dialog=new PdfLibraryDialog(category))dialog.ShowDialog(button.FindForm());};
            model.RightActions.Add(button);
        }
    }
}
