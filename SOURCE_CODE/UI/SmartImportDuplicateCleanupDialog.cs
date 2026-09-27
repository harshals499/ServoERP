using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Windows.Forms;
using HVAC_Pro_Desktop.Services;
using ServoERP.Infrastructure;

namespace HVAC_Pro_Desktop.UI
{
    public sealed class SmartImportDuplicateCleanupDialog : ServoFormBase
    {
        private readonly ExcelImportModule _module;
        private readonly SmartImportDuplicateCleanupService _service = new SmartImportDuplicateCleanupService();
        private readonly ListBox _groups = new ListBox();
        private readonly ComboBox _survivor = new ComboBox();
        private readonly CheckedListBox _duplicates = new CheckedListBox();
        private readonly Label _status = new Label();
        private List<SmartImportDuplicateGroup> _items = new List<SmartImportDuplicateGroup>();

        public SmartImportDuplicateCleanupDialog(ExcelImportModule module)
        {
            _module = module;
            Text = "Resolve Existing Duplicates - " + ExcelImportService.GetDisplayName(module);
            StartPosition = FormStartPosition.CenterParent;
            Size = new Size(820, 590);
            MinimumSize = new Size(720, 520);
            BackColor = Color.White;
            Font = new Font("Segoe UI", 9f);
            BuildUi();
            LoadGroups();
        }

        private void BuildUi()
        {
            var title = new Label { Dock = DockStyle.Top, Height = 58, Padding = new Padding(18, 14, 18, 0), Font = new Font("Segoe UI", 13f, FontStyle.Bold), Text = "Review, merge and archive duplicate records" };
            var help = new Label { Dock = DockStyle.Top, Height = 46, Padding = new Padding(18, 0, 18, 8), ForeColor = Color.FromArgb(71, 85, 105), Text = "Choose a group and the record to keep. Linked records move in one transaction; failures roll back safely." };
            var split = new SplitContainer { Dock = DockStyle.Fill, SplitterDistance = 275, Padding = new Padding(18, 4, 18, 4) };
            _groups.Dock = DockStyle.Fill;
            _groups.SelectedIndexChanged += (s, e) => BindSelectedGroup();
            split.Panel1.Controls.Add(_groups);

            var right = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 1, RowCount = 5, Padding = new Padding(12, 0, 0, 0) };
            right.RowStyles.Add(new RowStyle(SizeType.Absolute, 28));
            right.RowStyles.Add(new RowStyle(SizeType.Absolute, 38));
            right.RowStyles.Add(new RowStyle(SizeType.Absolute, 34));
            right.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
            right.RowStyles.Add(new RowStyle(SizeType.Absolute, 46));
            right.Controls.Add(new Label { Text = "Record to keep", Dock = DockStyle.Fill, Font = new Font("Segoe UI", 9f, FontStyle.Bold) }, 0, 0);
            _survivor.Dock = DockStyle.Fill;
            _survivor.DropDownStyle = ComboBoxStyle.DropDownList;
            _survivor.SelectedIndexChanged += (s, e) => BindDuplicateChecks();
            right.Controls.Add(_survivor, 0, 1);
            right.Controls.Add(new Label { Text = "Records to archive", Dock = DockStyle.Fill, Padding = new Padding(0, 8, 0, 0), Font = new Font("Segoe UI", 9f, FontStyle.Bold) }, 0, 2);
            _duplicates.Dock = DockStyle.Fill;
            _duplicates.CheckOnClick = true;
            right.Controls.Add(_duplicates, 0, 3);
            var merge = new Button { Text = "Merge selected and archive", Dock = DockStyle.Right, Width = 205, Height = 34, BackColor = Color.FromArgb(180, 30, 30), ForeColor = Color.White, FlatStyle = FlatStyle.Flat };
            merge.FlatAppearance.BorderSize = 0;
            merge.Click += (s, e) => MergeSelected();
            var actionPanel = new Panel { Dock = DockStyle.Fill };
            actionPanel.Controls.Add(merge);
            right.Controls.Add(actionPanel, 0, 4);
            split.Panel2.Controls.Add(right);

            var footer = new Panel { Dock = DockStyle.Bottom, Height = 58, Padding = new Padding(18, 10, 18, 10), BackColor = Color.FromArgb(248, 250, 252) };
            _status.Dock = DockStyle.Fill;
            _status.ForeColor = Color.FromArgb(71, 85, 105);
            _status.TextAlign = ContentAlignment.MiddleLeft;
            var close = new Button { Text = "Close", DialogResult = DialogResult.OK, Dock = DockStyle.Right, Width = 96 };
            footer.Controls.Add(_status);
            footer.Controls.Add(close);
            Controls.Add(split);
            Controls.Add(help);
            Controls.Add(title);
            Controls.Add(footer);
            AcceptButton = close;
        }

        private void LoadGroups()
        {
            try
            {
                _items = _service.GetGroups(_module).Groups;
                _groups.Items.Clear();
                foreach (SmartImportDuplicateGroup group in _items)
                    _groups.Items.Add(group.MatchReason + " - " + group.Records.Count + " records");
                _status.Text = _items.Count == 0 ? "No existing duplicate groups remain." : _items.Count + " duplicate group(s) need review.";
                if (_groups.Items.Count > 0) _groups.SelectedIndex = 0;
                else { _survivor.DataSource = null; _duplicates.Items.Clear(); }
            }
            catch (Exception ex)
            {
                AppLogger.LogError("SmartImportDuplicateCleanupDialog.LoadGroups", ex);
                MessageBox.Show(this, ex.Message, "Duplicate Review Unavailable", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            }
        }

        private void BindSelectedGroup()
        {
            SmartImportDuplicateGroup group = SelectedGroup();
            _survivor.DataSource = group == null ? null : group.Records.Select(record => new RecordChoice(record)).ToList();
            _survivor.DisplayMember = "Text";
            BindDuplicateChecks();
        }

        private void BindDuplicateChecks()
        {
            SmartImportDuplicateGroup group = SelectedGroup();
            RecordChoice survivor = _survivor.SelectedItem as RecordChoice;
            _duplicates.Items.Clear();
            if (group == null || survivor == null) return;
            foreach (SmartImportDuplicateRecord record in group.Records.Where(record => record.RecordId != survivor.Id))
                _duplicates.Items.Add(new RecordChoice(record), true);
        }

        private void MergeSelected()
        {
            RecordChoice survivor = _survivor.SelectedItem as RecordChoice;
            List<string> duplicateIds = _duplicates.CheckedItems.Cast<RecordChoice>().Select(item => item.Id).ToList();
            if (survivor == null || duplicateIds.Count == 0)
            {
                MessageBox.Show(this, "Select a survivor and at least one duplicate.", "Nothing Selected", MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }
            if (!ServoConfirmDialog.Show(this, "Merge and archive " + duplicateIds.Count + " duplicate record(s)?", "Keep " + survivor.Text + ". Linked records will be reassigned and the operation will be audited.")) return;
            try
            {
                DuplicateCleanupResult result = _service.MergeAndArchive(_module, survivor.Id, duplicateIds);
                MessageBox.Show(this, result.Message, "Duplicate Cleanup Complete", MessageBoxButtons.OK, MessageBoxIcon.Information);
                LoadGroups();
            }
            catch (Exception ex)
            {
                AppLogger.LogError("SmartImportDuplicateCleanupDialog.MergeSelected", ex);
                MessageBox.Show(this, "No records were changed. " + ex.Message, "Duplicate Cleanup Rolled Back", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            }
        }

        private SmartImportDuplicateGroup SelectedGroup() { return _groups.SelectedIndex >= 0 && _groups.SelectedIndex < _items.Count ? _items[_groups.SelectedIndex] : null; }

        private sealed class RecordChoice
        {
            public RecordChoice(SmartImportDuplicateRecord record) { Id = record.RecordId; Text = (string.IsNullOrWhiteSpace(record.DisplayName) ? "Record" : record.DisplayName) + " (#" + record.RecordId + ")"; }
            public string Id { get; private set; }
            public string Text { get; private set; }
            public override string ToString() { return Text; }
        }
    }
}
