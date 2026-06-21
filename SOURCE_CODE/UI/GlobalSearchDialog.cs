using System;
using System.Collections.Generic;
using System.Drawing;
using System.Runtime.InteropServices;
using System.Windows.Forms;
using HVAC_Pro_Desktop.Models;
using HVAC_Pro_Desktop.Services;

namespace HVAC_Pro_Desktop.UI
{
    public sealed class GlobalSearchDialog : ServoERP.Infrastructure.ServoFormBase
    {
        private const int EM_SETCUEBANNER = 0x1501;

        private readonly GlobalSearchService _searchService = new GlobalSearchService();
        private readonly TextBox _searchBox;
        private readonly ListBox _results;
        private readonly Action<string> _navigate;
        private List<GlobalSearchResult> _current = new List<GlobalSearchResult>();

        public GlobalSearchDialog(Action<string> navigate)
        {
            _navigate = navigate;
            Text = BrandingService.WindowTitle("Global Search");
            StartPosition = FormStartPosition.CenterParent;
            Size = new Size(720, 520);
            MinimumSize = new Size(560, 380);
            BackColor = DS.BgPage;

            _searchBox = new TextBox { Dock = DockStyle.Top, Height = 34, Font = new Font("Segoe UI", 11f), Margin = new Padding(12) };
            _searchBox.TextChanged += (s, e) => RunSearch();
            _searchBox.HandleCreated += (s, e) => SetCueBanner(_searchBox, "Search");
            _searchBox.KeyDown += (s, e) =>
            {
                if (e.KeyCode == Keys.Enter)
                {
                    OpenSelected();
                    e.SuppressKeyPress = true;
                }
            };

            _results = new ListBox { Dock = DockStyle.Fill, Font = new Font("Segoe UI", 9.5f), IntegralHeight = false, Cursor = Cursors.Hand };
            _results.DoubleClick += (s, e) => OpenSelected();

            var header = new Label
            {
                Text = "Search clients, sites, jobs, invoices, purchase orders, stock, vendors, and tickets",
                Dock = DockStyle.Top,
                Height = 34,
                Padding = new Padding(12, 8, 12, 0),
                ForeColor = DS.Slate600,
                BackColor = DS.BgPage
            };

            Controls.Add(_results);
            Controls.Add(_searchBox);
            Controls.Add(header);
            Shown += (s, e) =>
            {
                SetCueBanner(_searchBox, "Search");
                _searchBox.Focus();
            };
        }

        private void RunSearch()
        {
            try
            {
                _results.Items.Clear();
                _current = _searchService.Search(_searchBox.Text, 60);
                foreach (GlobalSearchResult result in _current)
                    _results.Items.Add(result.Module + " | " + result.Title + (string.IsNullOrWhiteSpace(result.Detail) ? string.Empty : " | " + result.Detail));
            }
            catch (Exception ex)
            {
                AppLogger.LogError("GlobalSearchDialog.RunSearch", ex);
            }
        }

        private void OpenSelected()
        {
            int index = _results.SelectedIndex;
            if (index < 0 || index >= _current.Count)
                return;

            GlobalSearchResult selected = _current[index];
            Hide();
            NavigationHelper.OpenSearchResult(this, selected, _navigate);
            Close();
        }

        private static void SetCueBanner(TextBox box, string placeholder)
        {
            if (box == null || box.IsDisposed || !box.IsHandleCreated)
                return;

            SendMessage(box.Handle, EM_SETCUEBANNER, new IntPtr(1), placeholder ?? string.Empty);
        }

        [DllImport("user32.dll", CharSet = CharSet.Unicode)]
        private static extern IntPtr SendMessage(IntPtr hWnd, int msg, IntPtr wParam, string lParam);
    }
}

