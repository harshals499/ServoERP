using System;
using System.Drawing;
using System.Linq;
using System.Windows.Forms;
using HVAC_Pro_Desktop.Services;
using HVAC_Pro_Desktop.UI.Helpers;
using ServoERP.Infrastructure;

namespace HVAC_Pro_Desktop.UI
{
    public class BaseUserControl : ServoPageBase
    {
        protected const int DESIGN_WIDTH = 1920;
        protected const int DESIGN_HEIGHT = 1080;

        private bool _ideaPadLayoutApplied;
        private readonly AutoSaveService _autoSave = new AutoSaveService();
        private readonly PersistentLayoutMemoryService _layoutMemory = new PersistentLayoutMemoryService();
        private bool _autoSaveAttached;
        private bool _persistentLayoutAttached;

        protected BaseUserControl()
        {
            AutoScaleMode = AutoScaleMode.Dpi;
            LanguageManager.LanguageChanged += OnLanguageChanged;
            Disposed += (s, e) => LanguageManager.LanguageChanged -= OnLanguageChanged;
        }

        protected bool IsSmallScreen
        {
            get { return Screen.FromControl(this).Bounds.Width < 1400 || DpiScale >= 1.25f; }
        }

        protected float DpiScale
        {
            get
            {
                using (Graphics g = CreateGraphics())
                    return g.DpiX / 96f;
            }
        }

        protected override void OnLoad(EventArgs e)
        {
            base.OnLoad(e);
            if (SuppressBaseAutomaticChildPolish)
                return;

            LayoutScaler.ApplyGlobalScale(this);
            ApplyIdeaPadLayout();
            AttachPersistentLayoutMemory();
            AttachAutoSave();
            ApplyLanguage();
            DS.ApplyTheme(this);
            UIHelper.ApplyInputStyles(Controls);
            InputOutlineService.ApplyToTree(this);
            UIHelper.ApplyButtonAlignment(this);
            SharedUiPrimitives.ApplyToTree(this);
            CrashProtectionService.AttachToTree(this);
            GlobalCardContextMenu.ApplyToTree(this);
            GlobalDashboardLayoutService.ApplyToTree(this);
            LayoutAuditService.AuditAndFix(this);
        }

        protected override void OnControlAdded(ControlEventArgs e)
        {
            base.OnControlAdded(e);
            if (SuppressBaseAutomaticChildPolish)
                return;

            if (e.Control != null)
            {
                LanguageManager.ApplyControlTree(e.Control);
                DS.ApplyTheme(e.Control);
                UIHelper.ApplyInputStyle(e.Control);
                UIHelper.ApplyInputStyles(e.Control.Controls);
                InputOutlineService.ApplyToTree(e.Control);
                UIHelper.ApplyButtonAlignment(e.Control);
                SharedUiPrimitives.ApplyToTree(e.Control);
                CrashProtectionService.AttachToTree(e.Control);
                GlobalCardContextMenu.ApplyToTree(e.Control);
                GlobalDashboardLayoutService.ApplyToTree(e.Control);
                LayoutAuditService.AuditAndFix(e.Control);
            }
        }

        protected virtual bool SuppressBaseAutomaticChildPolish => false;

        /// <summary>Refreshes translated labels and language-specific fonts.</summary>
        protected virtual void ApplyLanguage()
        {
            LanguageManager.ApplyFont(this);
        }

        private void OnLanguageChanged(object sender, EventArgs e)
        {
            if (IsDisposed)
                return;

            if (InvokeRequired)
            {
                BeginInvoke((Action)ApplyLanguage);
                return;
            }

            ApplyLanguage();
        }

        protected virtual bool EnableAutoSaveRecovery => true;
        protected virtual bool EnablePersistentLayoutMemory => true;

        protected override void OnResize(EventArgs e)
        {
            base.OnResize(e);
            ReflowGridColumns(this);
        }

        protected void ApplyIdeaPadLayout()
        {
            if (!IsSmallScreen)
                return;

            if (!_ideaPadLayoutApplied)
            {
                ScaleFontsRecursive(this, DpiScale >= 1.45f ? -1.5f : -1f);
                AnchorPanelsRecursive(this);
                ConfigureToolbarFlowLayouts(this);
                _ideaPadLayoutApplied = true;
            }

            ReflowGridColumns(this);
        }

        protected void ScaleFontsRecursive(Control parent, float delta)
        {
            foreach (Control c in parent.Controls)
            {
                float newSize = Math.Max(7f, c.Font.Size + delta);
                if (Math.Abs(c.Font.Size - newSize) > 0.01f)
                    c.Font = new Font(c.Font.FontFamily, newSize, c.Font.Style);
                ScaleFontsRecursive(c, delta);
            }
        }

        protected void ReflowGridColumns(Control parent)
        {
            foreach (Control c in parent.Controls)
            {
                DataGridView dgv = c as DataGridView;
                if (dgv != null && dgv.Columns.Count > 0)
                {
                    DS.StyleGrid(dgv);
                    if (dgv.Dock == DockStyle.None)
                        dgv.Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right | AnchorStyles.Bottom;
                    ApplyReadableColumnMinimums(dgv);
                    int available = dgv.ClientSize.Width - SystemInformation.VerticalScrollBarWidth - 4;
                    if (available > 0)
                        DistributeColumnWidths(dgv, available);
                }

                ReflowGridColumns(c);
            }
        }

        private void DistributeColumnWidths(DataGridView dgv, int available)
        {
            int totalCurrent = dgv.Columns
                .Cast<DataGridViewColumn>()
                .Where(col => col.Visible)
                .Sum(col => col.Width);
            if (totalCurrent == 0)
                return;

            foreach (DataGridViewColumn col in dgv.Columns)
            {
                if (!col.Visible)
                    continue;

                int newWidth = (int)((float)col.Width / totalCurrent * available);
                int minimum = col.MinimumWidth > 0 ? col.MinimumWidth : 60;
                col.Width = Math.Max(minimum, newWidth);
            }
        }

        protected void ApplyReadableColumnMinimums(DataGridView dgv)
        {
            foreach (DataGridViewColumn col in dgv.Columns)
            {
                string key = ((col.Name ?? string.Empty) + " " + (col.HeaderText ?? string.Empty)).ToLowerInvariant();
                int minimum = 60;

                if (key.Contains("clientname") || key.Contains("client name") || key.Contains("employee name"))
                    minimum = 130;
                else if (key.Contains("designation"))
                    minimum = 110;
                else if (key.Contains("department"))
                    minimum = 100;
                else if (key.Contains("phone") || key.Contains("mobile"))
                    minimum = 105;
                else if (key.Contains("email"))
                    minimum = 140;
                else if (key.Contains("gstin") || key.Contains("gst"))
                    minimum = 140;
                else if (key.Contains("city"))
                    minimum = 80;
                else if (key.Contains("status"))
                    minimum = 75;
                else if (key.Contains("contract") && key.Contains("no"))
                    minimum = 100;
                else if (key.Contains("invoice") && (key.Contains("no") || key.Contains("#")))
                    minimum = 100;
                else if (key.Contains("date") || key.Contains("start") || key.Contains("end"))
                    minimum = 90;
                else if (key.Contains("cgst") || key.Contains("sgst") || key.Contains("igst"))
                    minimum = 75;
                else if (key.Contains("total") || key.Contains("amount") || key.Contains("balance"))
                    minimum = 90;
                else if (key.Equals(" type") || key.EndsWith(" type"))
                    minimum = 60;

                col.MinimumWidth = Math.Max(col.MinimumWidth, minimum);
            }
        }

        protected void AnchorPanelsRecursive(Control parent)
        {
            foreach (Control c in parent.Controls)
            {
                Panel panel = c as Panel;
                if (panel != null && panel.Dock == DockStyle.None && panel.Top < 100 && panel.Height < 80)
                    panel.Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right;

                Panel bottomPanel = c as Panel;
                if (bottomPanel != null && bottomPanel.Dock == DockStyle.None && bottomPanel.Top > ClientSize.Height - 120)
                    bottomPanel.Anchor = AnchorStyles.Bottom | AnchorStyles.Left | AnchorStyles.Right;

                AnchorPanelsRecursive(c);
            }
        }

        protected void ConfigureToolbarFlowLayouts(Control parent)
        {
            foreach (Control c in parent.Controls)
            {
                FlowLayoutPanel flow = c as FlowLayoutPanel;
                if (flow != null && flow.Height <= 90 && ContainsToolbarControl(flow))
                {
                    flow.FlowDirection = FlowDirection.LeftToRight;
                    flow.WrapContents = true;
                    flow.AutoSize = true;
                    flow.AutoSizeMode = AutoSizeMode.GrowAndShrink;
                    if (flow.Dock == DockStyle.None && flow.Top < 120)
                        flow.Dock = DockStyle.Top;
                }

                ConfigureToolbarFlowLayouts(c);
            }
        }

        private static bool ContainsToolbarControl(Control parent)
        {
            foreach (Control child in parent.Controls)
            {
                if (child is Button || child is TextBox || child is ComboBox || child is DateTimePicker)
                    return true;
            }

            return false;
        }

        private void AttachAutoSave()
        {
            if (_autoSaveAttached || !EnableAutoSaveRecovery || DesignMode)
                return;

            _autoSaveAttached = true;
            string key = GetType().FullName;
            DateTime savedAt;
            if (_autoSave.HasDraft(key, out savedAt))
            {
                DialogResult restore = MessageBox.Show(
                    string.Format(LanguageManager.Get("An autosaved draft exists for this screen from {0}. Restore it?"), savedAt.ToString("dd MMM yyyy hh:mm tt")),
                    BrandingService.WindowTitle(LanguageManager.Get("Autosave")),
                    MessageBoxButtons.YesNo,
                    MessageBoxIcon.Question);
                if (restore == DialogResult.Yes)
                    _autoSave.Restore(this, key);
            }

            _autoSave.Attach(this, key);
        }

        private void AttachPersistentLayoutMemory()
        {
            if (_persistentLayoutAttached || !EnablePersistentLayoutMemory || DesignMode)
                return;

            _persistentLayoutAttached = true;
            string key = GetType().FullName;
            _layoutMemory.ApplyPage(this, key);
            Disposed += (s, e) => _layoutMemory.SavePage(this, key);
        }
    }
}
