using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Forms;
using HVAC_Pro_Desktop.Models;
using HVAC_Pro_Desktop.Services;

namespace HVAC_Pro_Desktop.UI
{
    public class SLADashboardForm : DeferredPageControl
    {
        // â”€â”€ Services â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€
        private readonly SLAService _svc = new SLAService();

        // â”€â”€ Controls â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€
        private DataGridView _grid;
        private Label        _lblStatus;
        private Label        _lblNoRecords;
        private bool _loading;

        // â”€â”€ Colours â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€
        private static readonly Color HeaderBg = DS.White;
        private static readonly Color AccentBlue = DS.Primary600;

        public SLADashboardForm()
        {
            this.Dock      = DockStyle.Fill;
            this.BackColor = DS.BgPage;
            BuildLayout();
            UIHelper.ApplyInputStyles(Controls);
            EnableDeferredLoad(
                () => LoadDataAsync(),
                ex => { _lblStatus.Text = "Load error: " + ex.Message; _lblStatus.ForeColor = Color.Red; });
        }

        protected override bool EnableAutomaticLayoutScaling => false;

        // â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•
        //  LAYOUT
        // â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•
        private void BuildLayout()
        {
            // â”€â”€ Page header â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€
            Panel header = new Panel
            {
                Dock = DockStyle.Top, Height = 56, BackColor = HeaderBg,
                Padding = new Padding(16, 0, 0, 0)
            };
            header.Controls.Add(new Label
            {
                Text = "SLA Dashboard", Font = new Font("Segoe UI", 14, FontStyle.Bold),
                ForeColor = DS.Slate900, Dock = DockStyle.Fill,
                TextAlign = ContentAlignment.MiddleLeft
            });

            // â”€â”€ Toolbar â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€
            Panel toolbar = new Panel
            {
                Dock = DockStyle.Top, Height = 44, BackColor = Color.White,
                Padding = new Padding(8, 6, 8, 6)
            };

            Button btnLog = MakeBtn("Log SLA Event", Color.FromArgb(39, 174, 96), 130);
            Button btnRefresh = MakeBtn("Refresh",   AccentBlue,                   90);

            btnLog.Location     = new Point(8,   8);
            btnRefresh.Location = new Point(148, 8);

            btnLog.Click     += BtnLog_Click;
            btnRefresh.Click += async (s, e) => await LoadDataAsync();

            _lblStatus = new Label
            {
                AutoSize  = true, Font = new Font("Segoe UI", 9),
                ForeColor = Color.Gray, Location = new Point(260, 14)
            };

            toolbar.Controls.AddRange(new Control[] { btnLog, btnRefresh, _lblStatus });

            // â”€â”€ Grid â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€
            _grid = new DataGridView
            {
                Dock              = DockStyle.Fill,
                AutoGenerateColumns = false,
                AllowUserToAddRows  = false,
                ReadOnly            = true,
                SelectionMode       = DataGridViewSelectionMode.FullRowSelect,
                BorderStyle         = BorderStyle.None,
                BackgroundColor     = Color.White,
                GridColor           = Color.FromArgb(220, 220, 220),
                RowHeadersVisible   = false,
                Font                = new Font("Segoe UI", 9),
                RowTemplate         = { Height = 28 }
            };

            _grid.Columns.Add(new DataGridViewTextBoxColumn { Name = "ContractID",    HeaderText = "Contract ID",    Width = 110 });
            _grid.Columns.Add(new DataGridViewTextBoxColumn { Name = "Compliance",    HeaderText = "Compliance %",   Width = 130 });
            _grid.Columns.Add(new DataGridViewTextBoxColumn { Name = "Rating",        HeaderText = "Rating",         Width = 110 });
            _grid.Columns.Add(new DataGridViewTextBoxColumn { Name = "TotalEvents",   HeaderText = "Total Events",   Width = 110 });
            _grid.Columns.Add(new DataGridViewTextBoxColumn { Name = "Breaches",      HeaderText = "Breaches",       Width = 90  });

            GridTheme.Apply(_grid);

            _grid.RowPrePaint += Grid_RowPrePaint;

            _lblNoRecords = new Label
            {
                Dock = DockStyle.Fill,
                Text = "No SLA records found.",
                Visible = false,
                BackColor = Color.White,
                ForeColor = Color.FromArgb(100, 100, 100),
                Font = new Font("Segoe UI", 10f),
                TextAlign = ContentAlignment.MiddleCenter
            };

            // â”€â”€ Add controls in reverse visual order â”€â”€â”€â”€â”€â”€â”€â”€â”€
            this.Controls.Add(_lblNoRecords);
            this.Controls.Add(_grid);      // Fill (must be added first)
            this.Controls.Add(toolbar);
            this.Controls.Add(header);
        }

        // â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•
        //  DATA
        // â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•
        private async Task LoadDataAsync()
        {
            if (_loading)
                return;

            _loading = true;
            try
            {
                ShowStatus("Loading SLA dashboard...", Color.Gray);
                List<SlaGridRow> rows = await Task.Run(() =>
                {
                    List<SLALog> all = _svc.GetAll();
                    Dictionary<int, List<SLALog>> byContract = new Dictionary<int, List<SLALog>>();
                    foreach (SLALog log in all)
                    {
                        if (!byContract.ContainsKey(log.ContractID))
                            byContract[log.ContractID] = new List<SLALog>();
                        byContract[log.ContractID].Add(log);
                    }

                    return byContract.Select(kvp =>
                    {
                        int total = kvp.Value.Count;
                        int compliant = kvp.Value.Count(l => l.Compliant);
                        int breaches = total - compliant;
                        decimal pct = total > 0 ? Math.Round((decimal)compliant / total * 100m, 1) : 100m;
                        return new SlaGridRow
                        {
                            ContractId = kvp.Key,
                            Compliance = pct,
                            Rating = _svc.GetComplianceRating(pct),
                            TotalEvents = total,
                            Breaches = breaches
                        };
                    }).ToList();
                });

                if (IsDisposed)
                    return;

                _grid.Rows.Clear();
                foreach (SlaGridRow row in rows)
                    _grid.Rows.Add(row.ContractId, row.Compliance.ToString("F1") + "%", row.Rating, row.TotalEvents, row.Breaches);
                _lblNoRecords.Visible = _grid.RowCount == 0;
                _grid.Visible = !_lblNoRecords.Visible;

                ShowStatus(rows.Count + " contract(s) loaded.", Color.Gray);
            }
            catch (Exception ex)
            {
                ShowStatus("Error: " + ex.Message, Color.Red);
            }
            finally
            {
                _loading = false;
            }
        }

        // â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•
        //  ROW COLOUR CODING
        // â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•
        private void Grid_RowPrePaint(object sender, DataGridViewRowPrePaintEventArgs e)
        {
            if (e.RowIndex < 0 || e.RowIndex >= _grid.Rows.Count) return;
            DataGridViewRow row = _grid.Rows[e.RowIndex];
            string rating = row.Cells["Rating"].Value as string ?? "";
            Color bg;
            switch (rating)
            {
                case "Excellent": bg = Color.FromArgb(200, 240, 210); break;
                case "Good":      bg = Color.FromArgb(220, 248, 225); break;
                case "Fair":      bg = Color.FromArgb(255, 229, 204); break;
                case "Poor":      bg = Color.FromArgb(255, 204, 204); break;
                default:          bg = Color.White;                   break;
            }
            row.DefaultCellStyle.BackColor  = bg;
            row.DefaultCellStyle.ForeColor  = Color.FromArgb(40, 40, 40);
            row.DefaultCellStyle.SelectionBackColor = ControlPaint.Dark(bg, 0.05f);
            row.DefaultCellStyle.SelectionForeColor = Color.Black;
        }

        // â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•
        //  LOG SLA EVENT DIALOG
        // â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•
        private async void BtnLog_Click(object sender, EventArgs e)
        {
            using (Form dlg = BuildLogDialog())
            {
                if (dlg.ShowDialog(this) == DialogResult.OK)
                    await LoadDataAsync();
            }
        }

        private Form BuildLogDialog()
        {
            Form dlg = new Form
            {
                Text            = "Log SLA Event",
                Width           = 420,
                Height          = 380,
                StartPosition   = FormStartPosition.CenterParent,
                FormBorderStyle = FormBorderStyle.FixedDialog,
                MaximizeBox     = false,
                MinimizeBox     = false,
                BackColor       = Color.White,
                Font            = new Font("Segoe UI", 9)
            };

            int lx = 16, tx = 150, tw = 230, cy = 18;

            Label MakeLbl(string text, int y)
            {
                var l = new Label { Text = text, Location = new Point(lx, y + 3), AutoSize = true,
                                    Font = new Font("Segoe UI", 9, FontStyle.Bold), ForeColor = Color.FromArgb(60,60,60) };
                dlg.Controls.Add(l);
                return l;
            }
            TextBox MakeTb(int y, int w = 0)
            {
                var tb = new TextBox { Location = new Point(tx, y), Width = w > 0 ? w : tw,
                                       BorderStyle = BorderStyle.FixedSingle, Font = new Font("Segoe UI", 9) };
                dlg.Controls.Add(tb);
                return tb;
            }

            MakeLbl("Contract ID",  cy);  var tbContractId  = MakeTb(cy, 80);  cy += 34;
            MakeLbl("Metric Type",  cy);  var tbMetricType  = MakeTb(cy);       cy += 34;
            MakeLbl("Target",       cy);  var tbTarget      = MakeTb(cy);       cy += 34;
            MakeLbl("Actual",       cy);  var tbActual      = MakeTb(cy);       cy += 34;
            MakeLbl("Notes",        cy);  var tbNotes       = MakeTb(cy);       cy += 34;

            MakeLbl("Compliant",    cy);
            var chkCompliant = new CheckBox
            {
                Location = new Point(tx, cy), Text = "Yes", Checked = true,
                Font = new Font("Segoe UI", 9)
            };
            dlg.Controls.Add(chkCompliant);
            cy += 38;

            // Hint for MetricType
            var hint = new Label
            {
                Text = "Metric types: ResponseTime, Uptime, RepairTime",
                Location = new Point(lx, cy), AutoSize = true,
                Font = new Font("Segoe UI", 8), ForeColor = Color.Gray
            };
            dlg.Controls.Add(hint);
            cy += 24;

            var lblErr = new Label
            {
                Location = new Point(lx, cy), AutoSize = true,
                Font = new Font("Segoe UI", 8, FontStyle.Bold), ForeColor = Color.Red
            };
            dlg.Controls.Add(lblErr);

            Button btnOk = new Button
            {
                Text         = "Log Event",
                Location     = new Point(lx, dlg.ClientSize.Height - 48),
                Width        = 110, Height = 30,
                BackColor    = Color.FromArgb(39, 174, 96), ForeColor = Color.White,
                FlatStyle    = FlatStyle.Flat, Font = new Font("Segoe UI", 9, FontStyle.Bold),
                DialogResult = DialogResult.None
            };
            btnOk.FlatAppearance.BorderSize = 0;

            Button btnCancel = new Button
            {
                Text         = "Cancel",
                Location     = new Point(lx + 120, dlg.ClientSize.Height - 48),
                Width        = 80, Height = 30,
                FlatStyle    = FlatStyle.Flat, Font = new Font("Segoe UI", 9),
                DialogResult = DialogResult.Cancel
            };

            btnOk.Click += (s, ev) =>
            {
                lblErr.Text = "";
                if (!int.TryParse(tbContractId.Text.Trim(), out int cid) || cid <= 0)
                { lblErr.Text = "Enter a valid Contract ID."; return; }
                if (string.IsNullOrWhiteSpace(tbMetricType.Text))
                { lblErr.Text = "Metric Type is required."; return; }
                try
                {
                    _svc.LogSLAEvent(cid, tbMetricType.Text.Trim(), tbTarget.Text.Trim(),
                                     tbActual.Text.Trim(), chkCompliant.Checked, tbNotes.Text.Trim());
                    ShowStatus("SLA event logged for contract " + cid + ".", Color.FromArgb(39, 174, 96));
                    dlg.DialogResult = DialogResult.OK;
                    dlg.Close();
                }
                catch (Exception ex)
                {
                    lblErr.Text = "Error: " + ex.Message;
                }
            };

            dlg.Controls.Add(btnOk);
            dlg.Controls.Add(btnCancel);
            dlg.AcceptButton = btnOk;
            dlg.CancelButton = btnCancel;
            return dlg;
        }

        // â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•
        //  HELPERS
        // â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•
        private void ShowStatus(string msg, Color color)
        {
            _lblStatus.Text      = msg;
            _lblStatus.ForeColor = color;
        }

        private sealed class SlaGridRow
        {
            public int ContractId { get; set; }
            public decimal Compliance { get; set; }
            public string Rating { get; set; }
            public int TotalEvents { get; set; }
            public int Breaches { get; set; }
        }

        private Button MakeBtn(string text, Color bg, int width)
        {
            Button b = new Button
            {
                Text = text, Width = width, Height = 28,
                BackColor = bg, ForeColor = Color.White,
                Font = new Font("Segoe UI", 9, FontStyle.Bold), FlatStyle = FlatStyle.Flat,
                Cursor = Cursors.Hand
            };
            b.FlatAppearance.BorderSize = 0;
            return b;
        }
    }
}

