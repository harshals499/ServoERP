using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Runtime.InteropServices;
using System.Threading.Tasks;
using System.Threading;
using System.Globalization;
using System.Windows.Forms;
using HVAC_Pro_Desktop.Models;
using HVAC_Pro_Desktop.Services;

namespace HVAC_Pro_Desktop.UI
{
    public sealed class GlobalSearchDialog : ServoERP.Infrastructure.ServoFormBase
    {
        protected override bool SuppressAutomaticChildPolish => true;
        private readonly TextBox _searchBox;
        private readonly ListBox _results;
        private readonly Panel _preview, _body, _details;
        private readonly Label _count, _state;
        private readonly FlowLayoutPanel _filters;
        private readonly Button _open;
        private readonly Action<string> _navigate;
        private readonly Func<string, List<GlobalSearchResult>> _search;
        private readonly Action<GlobalSearchResult> _openRecord;
        private readonly System.Windows.Forms.Timer _debounce = new System.Windows.Forms.Timer { Interval = 280 };
        private readonly SemaphoreSlim _searchGate = new SemaphoreSlim(1, 1);
        private List<GlobalSearchResult> _all = new List<GlobalSearchResult>();
        private List<GlobalSearchResult> _current = new List<GlobalSearchResult>();
        private string _filter = "All";
        internal bool RecordOpened { get; private set; }
        private int _revision;
        private static readonly Color Ink = Color.FromArgb(42,55,66), Blue = Color.FromArgb(69,110,147), Line = Color.FromArgb(227,231,228);

        public GlobalSearchDialog(Action<string> navigate) : this(navigate, q => new GlobalSearchService().Search(q, 120), null) { }
        internal GlobalSearchDialog(Action<string> navigate, Func<string,List<GlobalSearchResult>> search, Action<GlobalSearchResult> open)
        {
            _navigate = navigate; _search = search; _openRecord = open;
            Text = BrandingService.WindowTitle("Global Search"); StartPosition = FormStartPosition.CenterParent;
            Size = new Size(1160, 760); MinimumSize = new Size(820, 560);
            BackColor = Color.White; ForeColor = Ink; Font = new Font("Segoe UI",9f);
            var header = new Panel { Dock = DockStyle.Top, Height = 118, BackColor = Color.White };
            var input = new Panel { Name="SearchInput", Tag="CUSTOM_INPUT_SHELL", Location=new Point(18,18), Size=new Size(690,46), BackColor=Color.FromArgb(248,250,251) };
            DS.Rounded(input,6);
            input.Paint += (s,e) => {using(var path=DS.RoundedRect(new Rectangle(0,0,input.Width-1,input.Height-1),6))using(var pen=new Pen(Blue))e.Graphics.DrawPath(pen,path);};
            var icon=ModernIconSystem.Icon(ModernIconKind.Search,18,Blue);icon.SetBounds(12,8,30,30);icon.AccessibleName="Search";input.Controls.Add(icon);
            _searchBox = new TextBox { Name="SearchQuery", Tag="CUSTOM_INPUT_SHELL", BorderStyle=BorderStyle.None, Location=new Point(49,12), Width=590, Font=new Font("Segoe UI",12f), BackColor=input.BackColor, AccessibleName="Global search query" };
            input.Controls.Add(_searchBox);
            var clear = ButtonAt(input,"×",642,6,38,34,()=>{_searchBox.Clear();_searchBox.Focus();}); clear.AccessibleName="Clear search";
            header.Controls.Add(input);
            var hints=TextLabel(header,"Ctrl + K  Focus search\n↑ ↓  Navigate    Enter  Open    Esc  Close",726,22,370,44,8.5f);
            _filters = new FlowLayoutPanel { Name="SearchFilters", Location=new Point(18,77), Size=new Size(1080,37), WrapContents=false, AutoScroll=true };
            header.Controls.Add(_filters);
            var footer = new Panel { Dock=DockStyle.Bottom,Height=35, BackColor=Color.FromArgb(248,249,247) };
            _count=TextLabel(footer,"Type at least 2 characters to search",18,9,650,22,8.5f);
            _body=new Panel { Dock=DockStyle.Fill,Padding=new Padding(18,10,18,12) };
            _preview=new Panel { Name="RecordPreview",Dock=DockStyle.Right,Width=410,Padding=new Padding(22,0,0,0),BackColor=Color.White };
            _preview.Paint+=(s,e)=>{using(var pen=new Pen(Line))e.Graphics.DrawLine(pen,0,0,0,_preview.Height);};
            _results=new ListBox { Name="SearchResults", Dock=DockStyle.Fill, BorderStyle=BorderStyle.None, IntegralHeight=false, DrawMode=DrawMode.OwnerDrawFixed,ItemHeight=88,Font=Font,AccessibleName="Search results" };
            _state = new Label { Name="SearchState", Dock=DockStyle.Fill, Text="Find a client, job, invoice or other record.\nSearch by name, number or description.",TextAlign=ContentAlignment.MiddleCenter,ForeColor=Color.FromArgb(100,116,128),BackColor=Color.White };
            _body.Controls.Add(_results); _body.Controls.Add(_state); _body.Controls.Add(_preview);
            _details = new Panel { Dock=DockStyle.Fill, AutoScroll=true, Name="PreviewDetails" };
            var actionBar=new Panel {Dock=DockStyle.Bottom,Height=54};
            _preview.Controls.Add(_details);_preview.Controls.Add(actionBar);
            _open=ButtonAt(actionBar,"Open record",0,6,316,40,OpenSelected,true); _open.Name="OpenSearchRecord";
            Controls.Add(_body);Controls.Add(footer);Controls.Add(header);
            _body.AccessibleName="Search workspace";_preview.AccessibleName="Record preview";_details.AccessibleName="Record details";
            header.AccessibleName="Search tools";footer.AccessibleName="Result count";input.AccessibleName="Global search input";
            _filters.AccessibleName="Search categories";actionBar.AccessibleName="Open selected record";
            _searchBox.TextChanged+=(s,e)=>{_revision++;_debounce.Stop();_all.Clear();ApplyFilter();if(_searchBox.Text.Trim().Length<2){SetState("Type at least 2 characters to search.");return;}SetState("Searching…");_debounce.Start();};
            _debounce.Tick+=async(s,e)=>{_debounce.Stop();await RunSearchAsync();};
            _results.DrawItem+=DrawResult; _results.SelectedIndexChanged+=(s,e)=>RenderPreview();
            _results.DoubleClick+=(s,e)=>OpenSelected();
            Resize+=(s,e)=>{input.Width=Math.Max(400,ClientSize.Width-440);_searchBox.Width=input.Width-100;clear.Left=input.Width-44;hints.Left=input.Right+18;hints.Width=ClientSize.Width-hints.Left-18;_filters.Width=ClientSize.Width-36;LayoutPreviewButton();};
            Shown+=(s,e)=>{SendMessage(_searchBox.Handle,0x1501,new IntPtr(1),"Search names, record numbers or descriptions…");_searchBox.Focus();};
            BuildFilters();RenderPreview();CrashProtectionService.AttachToTree(this);
        }
        private static Label TextLabel(Control parent,string text,int x,int y,int w,int h,float size=9,bool bold=false)
        { var label=new Label {Text=text,Location=new Point(x,y),Size=new Size(w,h),Font=new Font("Segoe UI",size,bold?FontStyle.Bold:FontStyle.Regular),ForeColor=Ink,BackColor=Color.Transparent,AutoEllipsis=true,UseMnemonic=false};parent.Controls.Add(label);return label; }
        private static Button ButtonAt(Control parent,string text,int x,int y,int w,int h,Action click,bool primary=false)
        { var b=new Button {Text=text,Location=new Point(x,y),Size=new Size(w,h),FlatStyle=FlatStyle.Flat,BackColor=primary?Blue:Color.White,ForeColor=primary?Color.White:Blue,UseVisualStyleBackColor=false};b.FlatAppearance.BorderColor=Line;b.Click+=(s,e)=>click();parent.Controls.Add(b);DS.Rounded(b,6);b.BringToFront();return b; }
        private void BuildFilters()
        {
            foreach(Control c in _filters.Controls.Cast<Control>().ToArray()) c.Dispose();
            var modules=new[]{"All","Jobs","Clients","Invoices","Service Desk","Sites"}.Concat(_all.Select(r=>r.Module)).Distinct();
            foreach(string module in modules)
            {int count=module=="All"?_all.Count:_all.Count(r=>r.Module==module); var button=ButtonAt(_filters,module+(_all.Count>0?" ("+count+")":""),0,0,Math.Max(78,TextRenderer.MeasureText(module,Font).Width+46),29,()=>{_filter=module;ApplyFilter();BuildFilters();});button.Margin=new Padding(0,0,7,0);if(module==_filter){button.BackColor=Blue;button.ForeColor=Color.White;}}
            var ordered = _filters.Controls.Cast<Control>().Reverse().ToArray();
            for (int i=0;i<ordered.Length;i++) _filters.Controls.SetChildIndex(ordered[i],i);
        }
        private async Task RunSearchAsync()
        {
            int revision=_revision;string query=_searchBox.Text.Trim();var user=SessionManager.CurrentUser;
            await _searchGate.WaitAsync();
            try {if(IsDisposed||revision!=_revision)return;var found=await Task.Run(()=>_search(query));if(IsDisposed||revision!=_revision||user!=SessionManager.CurrentUser)return;_all=found??new List<GlobalSearchResult>();ApplyFilter();BuildFilters();}
            catch(Exception ex){AppLogger.LogError("GlobalSearchDialog.Search",ex);if(!IsDisposed&&revision==_revision)SetState("Search could not be completed. Please try again.");}
            finally {_searchGate.Release();}
        }
        internal void SetResultsForTest(IEnumerable<GlobalSearchResult> rows){_all=rows.ToList();ApplyFilter();BuildFilters();}
        private void ApplyFilter()
        {
            _current=_all.Where(r=>_filter=="All"||r.Module==_filter).ToList();_results.BeginUpdate();_results.Items.Clear();foreach(var r in _current)_results.Items.Add(r);_results.EndUpdate();
            _state.Visible=_current.Count==0;_state.Text=_all.Count==0?"No matching records. Try a name, number or description.":"No results in this category. Choose All or another category.";
            _count.Text=_current.Count+" results"+(_all.Count>=120?" · Showing the first 120 matches; refine your search.":"");if(_current.Count>0)_results.SelectedIndex=0;RenderPreview();
        }
        private void SetState(string text){_state.Text=text;_state.Visible=true;_count.Text=text;RenderPreview();}
        private void DrawResult(object sender,DrawItemEventArgs e)
        {
            if(e.Index<0||e.Index>=_current.Count)return;var r=_current[e.Index];bool selected=(e.State&DrawItemState.Selected)!=0;
            using(var bg=new SolidBrush(selected?Color.FromArgb(232,241,248):Color.White))e.Graphics.FillRectangle(bg,e.Bounds);
            var rect=e.Bounds;var kind=r.Module=="Jobs"?ModernIconKind.Job:r.Module=="Clients"?ModernIconKind.Client:r.Module=="Invoices"?ModernIconKind.Invoice:r.Module=="Sites"?ModernIconKind.Location:r.Module=="Service Desk"?ModernIconKind.Service:ModernIconKind.Document;
            using(var icon=ModernIconSystem.Icon(kind,18,Blue)) { TextRenderer.DrawText(e.Graphics,icon.Text,icon.Font,new Rectangle(rect.X+18,rect.Y+26,30,30),Blue,TextFormatFlags.HorizontalCenter);icon.Font.Dispose(); }
            TextRenderer.DrawText(e.Graphics,r.Module,Font,new Rectangle(rect.X+60,rect.Y+10,rect.Width-80,18),Blue,TextFormatFlags.EndEllipsis);
            using(var bold=new Font("Segoe UI",10.5f,FontStyle.Bold))TextRenderer.DrawText(e.Graphics,r.Title??"Record",bold,new Rectangle(rect.X+60,rect.Y+30,rect.Width-206,24),Ink,TextFormatFlags.EndEllipsis);
            string right=r.Amount.HasValue?"₹"+r.Amount.Value.ToString("N2",CultureInfo.GetCultureInfo("en-IN")):r.RecordDate?.ToString("dd/MM/yyyy");
            TextRenderer.DrawText(e.Graphics,right??"",Font,new Rectangle(rect.Right-148,rect.Y+11,130,20),Ink,TextFormatFlags.Right|TextFormatFlags.EndEllipsis);
            TextRenderer.DrawText(e.Graphics,r.Status??"",Font,new Rectangle(rect.Right-148,rect.Y+32,130,20),Blue,TextFormatFlags.Right|TextFormatFlags.EndEllipsis);
            string summary=string.Join(" · ",new[]{r.ClientName,r.SiteName,r.Detail}.Where(v=>!string.IsNullOrWhiteSpace(v)));
            TextRenderer.DrawText(e.Graphics,summary,Font,new Rectangle(rect.X+60,rect.Y+58,rect.Width-78,20),Color.FromArgb(100,116,128),TextFormatFlags.EndEllipsis);
            using(var pen=new Pen(Line))e.Graphics.DrawLine(pen,rect.Left+12,rect.Bottom-1,rect.Right-12,rect.Bottom-1);e.DrawFocusRectangle();
        }
        private void LayoutPreviewButton(){_open.SetBounds(0,6,Math.Max(100,_preview.Width-22),40);}
        private void RenderPreview()
        {
            foreach(Control c in _details.Controls.Cast<Control>().ToArray())c.Dispose();
            var r=_results.SelectedIndex>=0&&_results.SelectedIndex<_current.Count?_current[_results.SelectedIndex]:null;_open.Enabled=r!=null;LayoutPreviewButton();
            if(r==null){TextLabel(_details,"Record preview",0,12,310,28,12,true);TextLabel(_details,"Select a result to see its details.",0,58,310,70);return;}
            TextLabel(_details,r.Module,0,10,310,22,9);TextLabel(_details,r.Title,0,38,310,64,16,true);
            int y=112;var fields=new[]{Tuple.Create("Client",r.ClientName),Tuple.Create("Site",r.SiteName),Tuple.Create("Job type",r.JobType),Tuple.Create("Technician",r.Technician),Tuple.Create("Status",r.Status),Tuple.Create(r.Module=="Jobs"?"Scheduled":"Record date",r.RecordDate?.ToString("dd/MM/yyyy")),Tuple.Create("Due date",r.DueDate?.ToString("dd/MM/yyyy")),Tuple.Create("Total",r.Amount.HasValue?"₹"+r.Amount.Value.ToString("N2",CultureInfo.GetCultureInfo("en-IN")):null),Tuple.Create("Balance due",r.BalanceDue.HasValue?"₹"+r.BalanceDue.Value.ToString("N2",CultureInfo.GetCultureInfo("en-IN")):null),Tuple.Create("Record ID",r.RecordId.ToString())};
            foreach(var f in fields.Where(f=>!string.IsNullOrWhiteSpace(f.Item2))){TextLabel(_details,f.Item1,0,y,100,22,8.5f);TextLabel(_details,f.Item2,108,y,202,35,9);y+=38;}
            TextLabel(_details,"Details",0,y,310,24,9,true);var detail=TextLabel(_details,string.IsNullOrWhiteSpace(r.Detail)?"No additional details recorded.":r.Detail,0,y+28,310,40,9);detail.Height=Math.Max(40,TextRenderer.MeasureText(detail.Text,detail.Font,new Size(310,0),TextFormatFlags.WordBreak).Height+12);
            _open.Text="Open "+(r.Module=="Jobs"?"job":r.Module=="Clients"?"client":r.Module=="Invoices"?"invoice":"record");_open.BringToFront();
        }
        protected override bool ProcessCmdKey(ref Message msg,Keys keyData)
        {
            if(keyData==(Keys.Control|Keys.K)){_searchBox.Focus();_searchBox.SelectAll();return true;}
            if(keyData==Keys.Down||keyData==Keys.Up){if(_current.Count>0)_results.SelectedIndex=Math.Max(0,Math.Min(_current.Count-1,_results.SelectedIndex+(keyData==Keys.Down?1:-1)));return true;}
            if(keyData==Keys.Enter){OpenSelected();return true;}return base.ProcessCmdKey(ref msg,keyData);
        }
        private void OpenSelected()
        {
            int index=_results.SelectedIndex;if(index<0||index>=_current.Count)return;var selected=_current[index];
            if(_openRecord!=null){_openRecord(selected);return;}
            if(!GlobalSearchService.CanView(selected.PageKey)){MessageBox.Show(this,"This record is no longer available with your current permissions.",Text,MessageBoxButtons.OK,MessageBoxIcon.Information);return;}
            Hide();NavigationHelper.OpenSearchResult(this,selected,_navigate);RecordOpened=true;Close();
        }
        protected override void Dispose(bool disposing){if(disposing){_revision++;_debounce.Dispose();}base.Dispose(disposing);}
        [DllImport("user32.dll",CharSet=CharSet.Unicode)] private static extern IntPtr SendMessage(IntPtr hWnd,int msg,IntPtr wParam,string lParam);
    }
}
