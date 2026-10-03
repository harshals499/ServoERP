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
        private readonly CheckedListBox _groups = new CheckedListBox();
        private readonly ComboBox _survivor = new ComboBox();
        private readonly CheckedListBox _duplicates = new CheckedListBox();
        private readonly Label _status = new Label();
        private readonly Dictionary<SmartImportDuplicateGroup, GroupSelectionState> _selectionStates = new Dictionary<SmartImportDuplicateGroup, GroupSelectionState>();
        private SplitContainer _workspaceSplit;
        private List<SmartImportDuplicateGroup> _items = new List<SmartImportDuplicateGroup>();
        private SmartImportDuplicateGroup _boundGroup;
        private bool _suppressBinding;
        private bool _applyingWorkspaceSplit;

        public SmartImportDuplicateCleanupDialog(ExcelImportModule module) : this(module, true)
        {
        }

        internal SmartImportDuplicateCleanupDialog(ExcelImportModule module, bool loadGroups)
        {
            _module = module;
            Text = "Resolve Existing Duplicates - " + ExcelImportService.GetDisplayName(module);
            StartPosition = FormStartPosition.CenterParent;
            Size = new Size(820, 590);
            MinimumSize = new Size(720, 520);
            AutoScaleMode = AutoScaleMode.None;
            BackColor = Color.White;
            Font = new Font("Segoe UI", 9f);
            BuildUi();
            if (loadGroups)
                LoadGroups();
        }

        private void BuildUi()
        {
            var title = new Label { Dock = DockStyle.Top, Height = 58, Padding = new Padding(18, 14, 18, 0), Font = new Font("Segoe UI", 13f, FontStyle.Bold), Text = "Review, merge and archive duplicate records" };
            var help = new Label { Dock = DockStyle.Top, Height = 46, Padding = new Padding(18, 0, 18, 8), ForeColor = Color.FromArgb(71, 85, 105), Text = "Choose a group and the record to keep. Linked records move in one transaction; failures roll back safely." };
            _workspaceSplit = new SplitContainer { Dock = DockStyle.Fill, SplitterDistance = 275, FixedPanel = FixedPanel.Panel1, Padding = new Padding(18, 4, 18, 4) };
            var groupPanel = new Panel { Dock = DockStyle.Fill };
            var groupActions = new FlowLayoutPanel { Dock = DockStyle.Top, Height = 38, FlowDirection = FlowDirection.LeftToRight, WrapContents = false, Padding = new Padding(0, 0, 0, 6) };
            var selectAll = new Button { Name = "SelectAllDuplicateGroupsButton", Text = "Select all groups", Width = 126, Height = 30, FlatStyle = FlatStyle.Flat };
            var clearAll = new Button { Name = "ClearDuplicateGroupsButton", Text = "Clear selection", Width = 116, Height = 30, FlatStyle = FlatStyle.Flat };
            selectAll.Click += (s, e) => SetAllGroupsChecked(true);
            clearAll.Click += (s, e) => SetAllGroupsChecked(false);
            groupActions.Controls.Add(selectAll);
            groupActions.Controls.Add(clearAll);
            _groups.Dock = DockStyle.Fill;
            _groups.CheckOnClick = true;
            _groups.SelectedIndexChanged += (s, e) => BindSelectedGroup();
            _groups.ItemCheck += (s, e) =>
            {
                if (IsHandleCreated)
                    BeginInvoke((Action)UpdateSelectionStatus);
            };
            groupPanel.Controls.Add(_groups);
            groupPanel.Controls.Add(groupActions);
            _workspaceSplit.Panel1.Controls.Add(groupPanel);

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
            var merge = new Button { Name = "MergeSelectedDuplicateGroupsButton", Text = "Merge selected groups", Dock = DockStyle.Right, Width = 205, Height = 34, BackColor = Color.FromArgb(180, 30, 30), ForeColor = Color.White, FlatStyle = FlatStyle.Flat };
            merge.FlatAppearance.BorderSize = 0;
            merge.Click += (s, e) => MergeSelectedGroups();
            var actionPanel = new Panel { Dock = DockStyle.Fill };
            actionPanel.Controls.Add(merge);
            right.Controls.Add(actionPanel, 0, 4);
            _workspaceSplit.Panel2.Controls.Add(right);

            var footer = new Panel { Dock = DockStyle.Bottom, Height = 58, Padding = new Padding(18, 10, 18, 10), BackColor = Color.FromArgb(248, 250, 252) };
            _status.Dock = DockStyle.Fill;
            _status.ForeColor = Color.FromArgb(71, 85, 105);
            _status.TextAlign = ContentAlignment.MiddleLeft;
            var close = new Button { Text = "Close", DialogResult = DialogResult.OK, Dock = DockStyle.Right, Width = 96 };
            footer.Controls.Add(_status);
            footer.Controls.Add(close);
            Controls.Add(_workspaceSplit);
            Controls.Add(help);
            Controls.Add(title);
            Controls.Add(footer);
            AcceptButton = close;
        }

        protected override void OnLoad(EventArgs e)
        {
            base.OnLoad(e);
            ApplyWorkspaceSplit();
        }

        protected override void OnShown(EventArgs e)
        {
            base.OnShown(e);
            ApplyWorkspaceSplit();
        }

        protected override void OnResize(EventArgs e)
        {
            base.OnResize(e);
            ApplyWorkspaceSplit();
        }

        private void ApplyWorkspaceSplit()
        {
            if (_workspaceSplit == null || _workspaceSplit.Width <= 0 || _applyingWorkspaceSplit)
                return;

            _applyingWorkspaceSplit = true;
            try
            {
                const int groupPaneWidth = 275;
                _workspaceSplit.Panel1MinSize = 260;
                _workspaceSplit.Panel2MinSize = 360;
                int maximum = _workspaceSplit.Width - _workspaceSplit.Panel2MinSize - _workspaceSplit.SplitterWidth;
                if (maximum >= _workspaceSplit.Panel1MinSize)
                    _workspaceSplit.SplitterDistance = Math.Max(_workspaceSplit.Panel1MinSize, Math.Min(groupPaneWidth, maximum));
            }
            finally
            {
                _applyingWorkspaceSplit = false;
            }
        }

        private void LoadGroups()
        {
            try
            {
                _items = _service.GetGroups(_module).Groups;
                _selectionStates.Clear();
                _groups.Items.Clear();
                foreach (SmartImportDuplicateGroup group in _items)
                {
                    SmartImportDuplicateRecord survivor = group.Records.FirstOrDefault();
                    _selectionStates[group] = new GroupSelectionState
                    {
                        SurvivorId = survivor == null ? null : survivor.RecordId,
                        DuplicateIds = new HashSet<string>(group.Records.Skip(1).Select(record => record.RecordId), StringComparer.OrdinalIgnoreCase)
                    };
                    _groups.Items.Add(group.MatchReason + " - " + group.Records.Count + " records");
                }
                if (_groups.Items.Count > 0)
                {
                    _groups.SelectedIndex = 0;
                    _groups.SetItemChecked(0, true);
                }
                else { _survivor.DataSource = null; _duplicates.Items.Clear(); }
                UpdateSelectionStatus();
            }
            catch (Exception ex)
            {
                AppLogger.LogError("SmartImportDuplicateCleanupDialog.LoadGroups", ex);
                MessageBox.Show(this, ex.Message, "Duplicate Review Unavailable", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            }
        }

        private void BindSelectedGroup()
        {
            SaveBoundGroupSelection();
            SmartImportDuplicateGroup group = SelectedGroup();
            _boundGroup = group;
            _suppressBinding = true;
            try
            {
                _survivor.DataSource = group == null ? null : group.Records.Select(record => new RecordChoice(record)).ToList();
                _survivor.DisplayMember = "Text";
                GroupSelectionState state;
                if (group != null && _selectionStates.TryGetValue(group, out state))
                {
                    int selectedIndex = group.Records.FindIndex(record => string.Equals(record.RecordId, state.SurvivorId, StringComparison.OrdinalIgnoreCase));
                    _survivor.SelectedIndex = selectedIndex < 0 ? 0 : selectedIndex;
                }
                BindDuplicateChecksCore();
            }
            finally
            {
                _suppressBinding = false;
            }
        }

        private void BindDuplicateChecks()
        {
            if (_suppressBinding)
                return;
            SmartImportDuplicateGroup group = _boundGroup;
            RecordChoice survivor = _survivor.SelectedItem as RecordChoice;
            if (group == null || survivor == null)
                return;
            GroupSelectionState state = _selectionStates[group];
            state.SurvivorId = survivor.Id;
            state.DuplicateIds = new HashSet<string>(group.Records.Where(record => !string.Equals(record.RecordId, survivor.Id, StringComparison.OrdinalIgnoreCase)).Select(record => record.RecordId), StringComparer.OrdinalIgnoreCase);
            BindDuplicateChecksCore();
        }

        private void BindDuplicateChecksCore()
        {
            SmartImportDuplicateGroup group = _boundGroup;
            RecordChoice survivor = _survivor.SelectedItem as RecordChoice;
            _duplicates.Items.Clear();
            if (group == null || survivor == null) return;
            GroupSelectionState state = _selectionStates[group];
            foreach (SmartImportDuplicateRecord record in group.Records.Where(record => record.RecordId != survivor.Id))
                _duplicates.Items.Add(new RecordChoice(record), state.DuplicateIds.Contains(record.RecordId));
        }

        private void SaveBoundGroupSelection()
        {
            if (_suppressBinding || _boundGroup == null)
                return;
            RecordChoice survivor = _survivor.SelectedItem as RecordChoice;
            if (survivor == null)
                return;
            GroupSelectionState state = _selectionStates[_boundGroup];
            state.SurvivorId = survivor.Id;
            state.DuplicateIds = new HashSet<string>(_duplicates.CheckedItems.Cast<RecordChoice>().Select(item => item.Id), StringComparer.OrdinalIgnoreCase);
        }

        private void SetAllGroupsChecked(bool isChecked)
        {
            for (int index = 0; index < _groups.Items.Count; index++)
                _groups.SetItemChecked(index, isChecked);
            UpdateSelectionStatus();
        }

        private void UpdateSelectionStatus()
        {
            _status.Text = _items.Count == 0
                ? "No existing duplicate groups remain."
                : _groups.CheckedIndices.Count + " of " + _items.Count + " duplicate group(s) selected.";
        }

        private void MergeSelectedGroups()
        {
            SaveBoundGroupSelection();
            List<int> selectedIndexes = _groups.CheckedIndices.Cast<int>().ToList();
            var plans = new List<DuplicateCleanupPlan>();
            foreach (int index in selectedIndexes)
            {
                SmartImportDuplicateGroup group = _items[index];
                GroupSelectionState state = _selectionStates[group];
                plans.Add(new DuplicateCleanupPlan { SurvivorId = state.SurvivorId, DuplicateIds = state.DuplicateIds.ToList() });
            }
            int duplicateCount = plans.Sum(plan => (plan.DuplicateIds ?? Enumerable.Empty<string>()).Count());
            if (plans.Count == 0 || duplicateCount == 0)
            {
                MessageBox.Show(this, "Select at least one duplicate group with records to archive.", "Nothing Selected", MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }
            if (!ServoConfirmDialog.Show(this, "Merge " + plans.Count + " selected group(s) and archive " + duplicateCount + " duplicate record(s)?", "Each group's selected survivor will be kept. Linked records will be reassigned, the entire batch will run in one transaction, and the operation will be audited.")) return;
            try
            {
                DuplicateCleanupResult result = _service.MergeAndArchiveGroups(_module, plans);
                MessageBox.Show(this, result.Message, "Duplicate Cleanup Complete", MessageBoxButtons.OK, MessageBoxIcon.Information);
                LoadGroups();
            }
            catch (Exception ex)
            {
                AppLogger.LogError("SmartImportDuplicateCleanupDialog.MergeSelectedGroups", ex);
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

        private sealed class GroupSelectionState
        {
            public string SurvivorId { get; set; }
            public HashSet<string> DuplicateIds { get; set; }
        }
    }
}
