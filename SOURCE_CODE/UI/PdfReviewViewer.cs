using System;
using System.Drawing;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;
using HVAC_Pro_Desktop.Models;
using HVAC_Pro_Desktop.Services;

namespace HVAC_Pro_Desktop.UI
{
    internal sealed class PdfReviewViewer : UserControl
    {
        private readonly PdfReviewService _service;
        private readonly TableLayoutPanel _root = new TableLayoutPanel { Dock=DockStyle.Fill, ColumnCount=1, RowCount=5, Margin=Padding.Empty, Padding=new Padding(1) };
        private readonly ToolStrip _toolbar = new ToolStrip { Dock=DockStyle.Fill, GripStyle=ToolStripGripStyle.Hidden, AutoSize=false, Padding=new Padding(4,2,4,2), BackColor=DS.White, RenderMode=ToolStripRenderMode.System };
        private readonly Panel _canvas = new Panel { Name="PdfPreviewCanvas", Tag="NO_CARD_SURFACE", Dock=DockStyle.Fill, AutoScroll=true, BackColor=DS.Slate100, Margin=Padding.Empty };
        private readonly PictureBox _picture = new PictureBox { SizeMode=PictureBoxSizeMode.StretchImage, BackColor=Color.White, Visible=false };
        private readonly Label _title = global::HVAC_Pro_Desktop.Services.LanguageManager.Caption(new Label { Dock=DockStyle.Fill, AutoEllipsis=true, Text="Document preview", Font=DS.BodyBold, TextAlign=ContentAlignment.MiddleLeft, Padding=new Padding(8,0,0,0) });
        private readonly Label _folder = global::HVAC_Pro_Desktop.Services.LanguageManager.Caption(new Label { Dock=DockStyle.Fill, AutoEllipsis=true, Text="Choose a folder to begin", ForeColor=DS.Slate500, Padding=new Padding(8,0,0,0) });
        private readonly Label _message = global::HVAC_Pro_Desktop.Services.LanguageManager.Caption(new Label { Dock=DockStyle.Fill, Text="Preview your PDFs here\r\n\r\nChoose a folder, then select a document from the file list.", TextAlign=ContentAlignment.MiddleCenter, Padding=new Padding(24), Font=DS.Body });
        private readonly Label _viewStatus = global::HVAC_Pro_Desktop.Services.LanguageManager.Caption(new Label { Dock=DockStyle.Fill, AutoEllipsis=true, Padding=new Padding(8,4,8,0), ForeColor=DS.Slate500, Text="Review the PDF, then choose its destination category." });
        private readonly ToolStripLabel _pageLabel = new ToolStripLabel("Page —") { AutoSize=false, Width=98 };
        private readonly ToolStripLabel _zoomLabel = new ToolStripLabel("—") { AutoSize=false, Width=46 };
        private readonly FlowLayoutPanel _recovery = new FlowLayoutPanel { Dock=DockStyle.Fill, WrapContents=true, AutoScroll=true, Visible=false, Margin=Padding.Empty, Padding=new Padding(4) };
        private readonly TextBox _password = new TextBox { Width=124, UseSystemPasswordChar=true, AccessibleName=global::HVAC_Pro_Desktop.Services.LanguageManager.Get("PDF password (kept only in memory)"), Margin=new Padding(4,8,4,4) };
        private readonly ToolTip _tips = new ToolTip();
        private readonly Button _expand;
        private readonly ToolStripButton _previous, _next, _minus, _plus, _fitWidth, _fitPage;
        private CancellationTokenSource _loading;
        private PdfReviewFile _file;
        private int _page=1, _pages;
        private double _zoom=1;
        private ViewFit _fit=ViewFit.Width;
        private bool _closing, _resizing;
        private enum ViewFit { Width, Page, Manual }
        internal event Action Changed;
        internal event Action<bool> ExpandedChanged;
        internal bool Loading => _loading!=null;
        internal PdfReviewFile CurrentFile => _file;
        internal bool Expanded { get; private set; }

        internal PdfReviewViewer(PdfReviewService service)
        {
            _service=service; Dock=DockStyle.Fill; BackColor=DS.White; Font=DS.Body;
            _root.RowStyles.Add(new RowStyle(SizeType.Absolute,54));
            _root.RowStyles.Add(new RowStyle(SizeType.Absolute,38));
            _root.RowStyles.Add(new RowStyle(SizeType.Percent,100));
            _root.RowStyles.Add(new RowStyle(SizeType.Absolute,28));
            _root.RowStyles.Add(new RowStyle(SizeType.Absolute,0));
            var heading=new TableLayoutPanel {Dock=DockStyle.Fill,ColumnCount=2,RowCount=2,Margin=Padding.Empty};
            heading.ColumnStyles.Add(new ColumnStyle(SizeType.Percent,100));heading.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute,132));
            heading.RowStyles.Add(new RowStyle(SizeType.Absolute,30));heading.RowStyles.Add(new RowStyle(SizeType.Percent,100));
            _expand=global::HVAC_Pro_Desktop.Services.LanguageManager.Caption(new Button {Text="Expand",AccessibleName=global::HVAC_Pro_Desktop.Services.LanguageManager.Get("Expand PDF preview"),Dock=DockStyle.Fill,Margin=new Padding(4,8,4,8),FlatStyle=FlatStyle.Flat,BackColor=DS.White});
            _expand.FlatAppearance.BorderColor=DS.Border;
            _tips.SetToolTip(_expand,global::HVAC_Pro_Desktop.Services.LanguageManager.Get("Expand the PDF preview; keep your current document and review choices."));
            _expand.Click+=(s,e)=> {Expanded=!Expanded;global::HVAC_Pro_Desktop.Services.LanguageManager.SetCaption(_expand, Expanded?"Show files":"Expand");_expand.AccessibleName=(Expanded ? global::HVAC_Pro_Desktop.Services.LanguageManager.Get("Return to file list and categories") : global::HVAC_Pro_Desktop.Services.LanguageManager.Get("Expand PDF preview"));ExpandedChanged?.Invoke(Expanded);};
            heading.Controls.Add(_title,0,0);heading.Controls.Add(_folder,0,1);heading.Controls.Add(_expand,1,0);heading.SetRowSpan(_expand,2);
            var viewport=new Panel {Name="PdfPreviewHost",Tag="NO_CARD_SURFACE",Dock=DockStyle.Fill,Margin=Padding.Empty};
            viewport.Controls.Add(_canvas);viewport.Controls.Add(_message);
            _root.Controls.Add(heading,0,0);_root.Controls.Add(_toolbar,0,1);_root.Controls.Add(viewport,0,2);_root.Controls.Add(_viewStatus,0,3);_root.Controls.Add(_recovery,0,4);Controls.Add(_root);
            // Keep the fill-docked state label outside the scrollable paper surface.
            // Otherwise its bounds can retain a horizontal scrollbar after zoom/resize.
            _canvas.Controls.Add(_picture);
            // Native ToolStrip keeps PDF navigation compact without changing shared business action buttons.
            _toolbar.Font=DS.Body;
            _previous=global::HVAC_Pro_Desktop.Services.LanguageManager.CaptionItem(Add("‹","Previous PDF page",async()=> {if(_page>1){_page--;await LoadPage();}}));
            _toolbar.Items.Add(_pageLabel);
            _next=global::HVAC_Pro_Desktop.Services.LanguageManager.CaptionItem(Add("›","Next PDF page",async()=> {if(_page<_pages){_page++;await LoadPage();}}));
            _toolbar.Items.Add(new ToolStripSeparator());
            _minus=global::HVAC_Pro_Desktop.Services.LanguageManager.CaptionItem(Add("−","Zoom out",()=>Zoom(-.25)));_toolbar.Items.Add(_zoomLabel);_plus=global::HVAC_Pro_Desktop.Services.LanguageManager.CaptionItem(Add("+","Zoom in",()=>Zoom(.25)));
            _toolbar.Items.Add(new ToolStripSeparator());
            _fitWidth=global::HVAC_Pro_Desktop.Services.LanguageManager.CaptionItem(Add("Fit to width","Fill the preview width",()=>SetFit(ViewFit.Width)));
            _fitPage=global::HVAC_Pro_Desktop.Services.LanguageManager.CaptionItem(Add("Fit page","Show the complete page",()=>SetFit(ViewFit.Page)));
            _recovery.Controls.Add(global::HVAC_Pro_Desktop.Services.LanguageManager.Caption(new Label {Text="PDF password",AutoSize=true,Margin=new Padding(4,11,0,0)}));
            _recovery.Controls.Add(_password);
            var retry=global::HVAC_Pro_Desktop.Services.LanguageManager.Caption(new Button {Text="Unlock / Retry",AutoSize=true,Height=32,Margin=new Padding(4),FlatStyle=FlatStyle.Flat,BackColor=DS.White});
            retry.FlatAppearance.BorderColor=DS.Border;
            retry.Click+=async(s,e)=> {if(!Loading&&_file!=null){_file.Password=_password.Text;_password.Clear();await LoadPage();}};
            _recovery.Controls.Add(retry);
            _canvas.Resize+=(s,e)=>ResizeImage();
            _recovery.SizeChanged+=(s,e)=> {if(_recovery.Visible)_root.RowStyles[4].Height=_recovery.PreferredSize.Height;};
            UpdateToolbar();
        }
        private ToolStripButton Add(string text,string description,Func<Task> action)
        {
            var button=new ToolStripButton(text) {DisplayStyle=ToolStripItemDisplayStyle.Text,ToolTipText=description,AccessibleName=description,AutoSize=true,Padding=new Padding(6,2,6,2)};
            button.Click+=async(s,e)=> {if(!Loading)await action();};_toolbar.Items.Add(button);return button;
        }
        private Task SetFit(ViewFit fit) {_fit=fit;_canvas.AutoScrollPosition=Point.Empty;ResizeImage();return Task.CompletedTask;}
        private Task Zoom(double amount) {_fit=ViewFit.Manual;_zoom=Math.Max(.25,Math.Min(3,_zoom+amount));ResizeImage();return Task.CompletedTask;}
        internal async Task ShowAsync(PdfReviewFile file)
        {
            if(_closing||IsDisposed||Disposing)return;
            _loading?.Cancel();_file=file;_page=1;_pages=0;_password.Clear();_fit=ViewFit.Width;
            ClearImage();ShowRecovery(false);
            global::HVAC_Pro_Desktop.Services.LanguageManager.SetDataText(_title, (file==null ? global::HVAC_Pro_Desktop.Services.LanguageManager.Get("Document preview") : file.FileName));
            global::HVAC_Pro_Desktop.Services.LanguageManager.SetDataText(_folder, (file==null ? global::HVAC_Pro_Desktop.Services.LanguageManager.Get("Choose a folder to begin") : global::HVAC_Pro_Desktop.Services.LanguageManager.Format("Folder: {0}", file.Folder)));
            _tips.SetToolTip(_title,file?.SourcePath);_tips.SetToolTip(_folder,file?.SourcePath);
            UpdateToolbar();
            if(file==null){ShowMessage("Preview your PDFs here\r\n\r\nChoose a folder, then select a document from the file list.");global::HVAC_Pro_Desktop.Services.LanguageManager.SetCaption(_viewStatus, "Review the PDF, then choose its destination category.");return;}
            await LoadPage();
        }
        private async Task LoadPage()
        {
            var source=new CancellationTokenSource();var previous=_loading;_loading=source;previous?.Cancel();var file=_file;
            ClearImage();ShowRecovery(false);ShowMessage("Opening PDF…\r\n\r\nYou can select another document or skip this file.");
            global::HVAC_Pro_Desktop.Services.LanguageManager.SetDataText(_viewStatus, global::HVAC_Pro_Desktop.Services.LanguageManager.Format("Loading page {0}…", _page));UpdateToolbar();Changed?.Invoke();
            try
            {
                var result=await _service.RenderAsync(file,_page,source.Token).ConfigureAwait(false);
                try
                {
                    if(source.IsCancellationRequested||_closing||IsDisposed||Disposing||_file!=file)return;
                    OnUi(()=>
                    {
                        if(source.IsCancellationRequested||_file!=file)return;
                        if(!string.IsNullOrEmpty(result.Error)){ShowError(file,result.Error);return;}
                        using(var image=Image.FromFile(result.Image))_picture.Image=new Bitmap(image);
                        file.Error=null;file.Previewed=true;_pages=result.Pages;_message.Visible=false;_canvas.Visible=true;_picture.Visible=true;
                        ShowRecovery(false);ResizeImage();
                    });
                }
                finally {PdfReviewService.DeletePreview(result.Image);}
            }
            catch(OperationCanceledException) { }
            catch(Exception ex)
            {
                Serilog.Log.Warning("PDF preview failed with {ErrorType}",ex.GetType().Name);
                OnUi(()=> {if(_file==file&&!source.IsCancellationRequested)ShowError(file,ex is InvalidOperationException?ex.Message:"PDF unavailable or unreadable. Retry or skip this file.");});
            }
            finally
            {
                OnUi(()=> {if(_loading==source){_loading=null;_recovery.Enabled=true;UpdateToolbar();Changed?.Invoke();}});
                Interlocked.CompareExchange(ref _loading,null,source);source.Dispose();
            }
        }
        private void ShowError(PdfReviewFile file,string error)
        {
            file.Error=error;ShowMessage("This PDF needs attention\r\n\r\n"+error+"\r\n\r\nRetry below, or use Skip to continue reviewing other files.");
            global::HVAC_Pro_Desktop.Services.LanguageManager.SetCaption(_viewStatus, "Preview unavailable • Other documents can still be reviewed.");ShowRecovery(true);
        }
        private void ShowMessage(string text) {global::HVAC_Pro_Desktop.Services.LanguageManager.SetDataText(_message, text);_canvas.Visible=false;_message.Visible=true;_message.BringToFront();}
        private void ShowRecovery(bool visible) {_recovery.Visible=visible;_recovery.Enabled=!Loading;_root.RowStyles[4].Height=visible?Math.Max(48,_recovery.PreferredSize.Height):0;}
        private void UpdateToolbar()
        {
            bool ready=_picture.Image!=null&&!Loading;
            _previous.Enabled=ready&&_page>1;_next.Enabled=ready&&_page<_pages;
            _minus.Enabled=ready&&_zoom>.25;_plus.Enabled=ready&&_zoom<3;_fitWidth.Enabled=ready;_fitPage.Enabled=ready;
            _fitWidth.Checked=ready&&_fit==ViewFit.Width;_fitPage.Checked=ready&&_fit==ViewFit.Page;
            global::HVAC_Pro_Desktop.Services.LanguageManager.SetDataText(_pageLabel, (_file==null ? global::HVAC_Pro_Desktop.Services.LanguageManager.Get("Page —") : (Loading ? global::HVAC_Pro_Desktop.Services.LanguageManager.Format("Page {0} / …", _page) : (_pages>0 ? global::HVAC_Pro_Desktop.Services.LanguageManager.Format("Page {0} of {1}", _page, _pages) : global::HVAC_Pro_Desktop.Services.LanguageManager.Get("Page —")))));
            global::HVAC_Pro_Desktop.Services.LanguageManager.SetDataText(_zoomLabel, (ready ? (_zoom*100).ToString("0")+"%" : global::HVAC_Pro_Desktop.Services.LanguageManager.Get("—")));
        }
        private void OnUi(Action action)
        {
            if(_closing||IsDisposed||Disposing||!IsHandleCreated)return;
            ServoERP.Infrastructure.UIThread.Run(this,()=> {if(!_closing&&!IsDisposed&&!Disposing)action();});
        }
        private void ResizeImage()
        {
            if(_resizing||_picture.Image==null)return;
            _resizing=true;
            try
            {
                var image=_picture.Image;
                // Reserve native scrollbar space up front. ClientSize changes while AutoScroll
                // recalculates, and the shared theme adds a bottom scroll margin.
                // Using that changing width can alternate vertical and horizontal scrollbars.
                int availableWidth=Math.Max(1,_canvas.Width-SystemInformation.VerticalScrollBarWidth-40-_canvas.AutoScrollMargin.Width);
                int availableHeight=Math.Max(1,_canvas.Height-SystemInformation.HorizontalScrollBarHeight-32-_canvas.AutoScrollMargin.Height);
                double baseWidth=Math.Min(900,image.Width),ratio=(double)image.Height/image.Width;
                if(_fit==ViewFit.Width)_zoom=availableWidth/baseWidth;
                else if(_fit==ViewFit.Page)_zoom=Math.Min(availableWidth/baseWidth,availableHeight/(baseWidth*ratio));
                int width=Math.Max(1,(int)Math.Round(baseWidth*_zoom)),height=Math.Max(1,(int)Math.Round(width*ratio));
                _picture.Size=new Size(width,height);
                _canvas.AutoScrollMinSize=new Size(width+32,height+32);
                // Page-level scroll helpers suppress horizontal scrolling; a zoomed PDF needs it.
                _canvas.HorizontalScroll.Enabled=_fit==ViewFit.Manual&&width+32>_canvas.ClientSize.Width;
                var scroll=_canvas.AutoScrollPosition;
                _picture.Location=new Point(Math.Max(16,(_canvas.ClientSize.Width-width)/2)+scroll.X,16+scroll.Y);
                global::HVAC_Pro_Desktop.Services.LanguageManager.SetDataText(_viewStatus, (_fit==ViewFit.Width?"Fit to width":_fit==ViewFit.Page?"Fit page":"Custom zoom")+" • "+(_zoom*100).ToString("0")+"% • "+(_pages==1?"1 page":_pages+" pages"));
                UpdateToolbar();
            }
            finally {_resizing=false;}
        }
        private void ClearImage() {var image=_picture.Image;_picture.Image=null;_picture.Visible=false;image?.Dispose();_picture.Size=Size.Empty;_canvas.AutoScrollMinSize=Size.Empty;_canvas.AutoScrollPosition=Point.Empty;}
        protected override void Dispose(bool disposing) {if(disposing){_closing=true;_loading?.Cancel();ClearImage();_tips.Dispose();}base.Dispose(disposing);}
    }
}
