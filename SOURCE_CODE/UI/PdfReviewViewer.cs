using System;
using System.Drawing;
using System.IO;
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
        private readonly FlowLayoutPanel _toolbar = new FlowLayoutPanel { Dock = DockStyle.Fill, WrapContents = false, AutoScroll = true };
        private readonly Panel _canvas = new Panel { Dock = DockStyle.Fill, AutoScroll = true, BackColor = DS.Slate100 };
        private readonly PictureBox _picture = new PictureBox { SizeMode = PictureBoxSizeMode.StretchImage, Location = new Point(12,12), BackColor = Color.White };
        private readonly Label _pageLabel = new Label { AutoSize = true, Padding = new Padding(4,10,4,0), Text = "Page —" };
        private readonly Label _message = new Label { Dock = DockStyle.Fill, Text = "Choose a folder, then select a PDF.", Padding = new Padding(8), AutoEllipsis = true };
        private readonly Label _path = new Label { Dock = DockStyle.Fill, AutoEllipsis = true, Padding = new Padding(8) };
        private readonly FlowLayoutPanel _recovery = new FlowLayoutPanel { Dock = DockStyle.Fill, WrapContents = false, AutoScroll = true, Visible=false };
        private readonly TextBox _password = new TextBox { Width = 130, UseSystemPasswordChar = true, AccessibleName = "PDF password (kept only in memory)", Margin = new Padding(4,8,4,4) };
        private CancellationTokenSource _loading;
        private PdfReviewFile _file;
        private int _page = 1, _pages;
        private double _zoom = 1;
        private bool _fit = true;
        private bool _closing;
        internal event Action Changed;
        internal bool Loading => _loading != null;
        internal PdfReviewFile CurrentFile => _file;

        internal PdfReviewViewer(PdfReviewService service)
        {
            _service = service; Dock = DockStyle.Fill; BackColor = DS.White;
            var root = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 1, RowCount = 5, Margin = Padding.Empty };
            foreach (int height in new[] { 44, 48 }) root.RowStyles.Add(new RowStyle(SizeType.Absolute, height));
            root.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
            root.RowStyles.Add(new RowStyle(SizeType.Absolute, 42)); root.RowStyles.Add(new RowStyle(SizeType.Absolute, 42));
            root.Controls.Add(_toolbar,0,0); root.Controls.Add(_message,0,1); root.Controls.Add(_canvas,0,2); root.Controls.Add(_path,0,3); root.Controls.Add(_recovery,0,4); Controls.Add(root);
            _canvas.Controls.Add(_picture);
            Add(_toolbar,"‹",async()=> { if (_page > 1) { _page--; await LoadPage(); } });
            _toolbar.Controls.Add(_pageLabel);
            Add(_toolbar,"›",async()=> { if (_page < _pages) { _page++; await LoadPage(); } });
            Add(_toolbar,"−",()=> { _fit=false; _zoom=Math.Max(.25,_zoom-.25); ResizeImage(); return Task.CompletedTask; });
            Add(_toolbar,"+",()=> { _fit=false; _zoom=Math.Min(3,_zoom+.25); ResizeImage(); return Task.CompletedTask; });
            Add(_toolbar,"Fit to width",()=> { _fit=true; ResizeImage(); return Task.CompletedTask; });
            _recovery.Controls.Add(_password);
            Add(_recovery,"Unlock / Retry",async()=> { if(_file!=null) { _file.Password=_password.Text; _password.Clear(); await LoadPage(); } });
            _canvas.Resize += (s,e)=>ResizeImage();
        }
        private void Add(FlowLayoutPanel panel, string text, Func<Task> action)
        {
            var button = new Button { Text=text, AutoSize=true, Height=32, MinimumSize=new Size(32,32), Margin=new Padding(3,4,3,3), FlatStyle=FlatStyle.Flat, BackColor=Color.White };
            button.FlatAppearance.BorderColor=DS.Border;
            button.Click+=async(s,e)=> { if (!Loading) await action(); }; panel.Controls.Add(button);
        }
        internal async Task ShowAsync(PdfReviewFile file)
        {
            if(_closing||IsDisposed||Disposing)return;
            _loading?.Cancel();
            _file=file; _page=1; _pages=0; _password.Clear(); _fit=true;
            ClearImage(); _path.Text=file == null ? "" : file.SourcePath;
            if(file==null) { _message.Text="Choose a folder, then select a PDF."; return; }
            await LoadPage();
        }
        private async Task LoadPage()
        {
            var source = new CancellationTokenSource(); var previous=_loading; _loading=source; previous?.Cancel();
            var file=_file;
            ClearImage(); _message.Text="Opening PDF… You can select another file or skip this one."; _pageLabel.Text="Page " + _page + " / …";
            _recovery.Enabled=false; Changed?.Invoke();
            try
            {
                var result=await _service.RenderAsync(file,_page,source.Token).ConfigureAwait(false);
                if (source.IsCancellationRequested || _closing || IsDisposed || Disposing || _file!=file) { PdfReviewService.DeletePreview(result.Image); return; }
                OnUi(()=>
                {
                    if(source.IsCancellationRequested||_file!=file)return;
                    if (!string.IsNullOrEmpty(result.Error)) { file.Error=result.Error; _message.Text=result.Error; _recovery.Visible=true; }
                    else
                    {
                        using(var image=Image.FromFile(result.Image)) _picture.Image=new Bitmap(image);
                        file.Error=null; file.Previewed=true; _pages=result.Pages;
                        _message.Text="PDF preview • " + (_fit ? "Fit to width" : (_zoom*100).ToString("0")+"%") + " • No category detected automatically";
                        _pageLabel.Text="Page " + _page + " of " + _pages; _recovery.Visible=false; ResizeImage();
                    }
                });
                PdfReviewService.DeletePreview(result.Image);
            }
            catch(OperationCanceledException) { }
            catch(Exception ex)
            {
                Serilog.Log.Warning("PDF preview failed with {ErrorType}",ex.GetType().Name);
                OnUi(()=> {if(_file==file&&!source.IsCancellationRequested) { file.Error=ex is InvalidOperationException ? ex.Message : "PDF unavailable or unreadable. Retry or skip this file."; _message.Text=file.Error; _recovery.Visible=true; }});
            }
            finally
            {
                OnUi(()=> {if(_loading==source) { _loading=null; _recovery.Enabled=true; Changed?.Invoke(); }});
                Interlocked.CompareExchange(ref _loading,null,source);
                source.Dispose();
            }
        }
        private void OnUi(Action action)
        {
            if(_closing||IsDisposed||Disposing||!IsHandleCreated)return;
            ServoERP.Infrastructure.UIThread.Run(this,()=> {if(!_closing&&!IsDisposed&&!Disposing)action();});
        }
        private void ResizeImage()
        {
            var image=_picture.Image; if(image==null)return;
            int width=_fit ? Math.Max(80,_canvas.ClientSize.Width-44) : (int)(Math.Min(900,image.Width)*_zoom);
            _picture.Size=new Size(width,Math.Max(1,(int)(width*(double)image.Height/image.Width)));
            _canvas.AutoScrollMinSize=new Size(width+24,_picture.Height+24);
            _message.Text="PDF preview • " + (_fit ? "Fit to width" : (_zoom*100).ToString("0")+"%") + " • No automatic classification";
        }
        private void ClearImage() { var image=_picture.Image; _picture.Image=null; image?.Dispose(); _picture.Size=Size.Empty; _canvas.AutoScrollMinSize=Size.Empty; _canvas.AutoScrollPosition=Point.Empty; }
        protected override void Dispose(bool disposing) { if(disposing) { _closing=true;_loading?.Cancel(); ClearImage(); } base.Dispose(disposing); }
    }
}
