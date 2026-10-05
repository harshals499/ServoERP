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
        private readonly TextBox _filter = new TextBox();
        private readonly Dictionary<SmartImportDuplicateGroup, GroupSelectionState> _selectionStates = new Dictionary<SmartImportDuplicateGroup, GroupSelectionState>();
        private readonly HashSet<SmartImportDuplicateGroup> _selectedGroups = new HashSet<SmartImportDuplicateGroup>();
        private SplitContainer _workspaceSplit;
        private List<SmartImportDuplicateGroup> _items = new List<SmartImportDuplicateGroup>();
        private SmartImportDuplicateGroup _boundGroup;
        private bool _suppressBinding;
        private bool _applyingWorkspaceSplit;
        private bool _suppressGroupChecks;
        private List<SmartImportDuplicateGroup> _visibleItems = new List<SmartImportDuplicateGroup>();

        public SmartImportDuplicateCleanupDialog() : this(ExcelImportModule.Employees, false)
        {
        }

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
            var title = new Label { Dock = DockStyle.Top, Height = 58, Padding = new Padding(18, 14, 18, 0), Font = new Font("Segoe UI", 13f, FontStyle.Bold), Text = "Review, merge or delete duplicate records" };
            var help = new Label { Dock = DockStyle.Top, Height = 46, Padding = new Padding(18, 0, 18, 8), ForeColor = Color.FromArgb(71, 85, 105), Text = "Choose records to merge/archive or permanently delete. Linked records move in one transaction; failures roll back safely." };
            _workspaceSplit = new SplitContainer { Dock = DockStyle.Fill, SplitterDistance = 275, FixedPanel = FixedPanel.Panel1, Padding = new Padding(18, 4, 18, 4) };
            var groupPanel = new Panel { Dock = DockStyle.Fill };
            var groupActions = new TableLayoutPanel { Dock = DockStyle.Top, Height = 40, ColumnCount = 2, RowCount = 1, Padding = new Padding(0, 0, 0, 6) };
            groupActions.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50));
            groupActions.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50));
            var selectAll = new Button { Name = "SelectAllDuplicateGroupsButton", Text = "Select shown groups", Dock = DockStyle.Fill, Margin = new Padding(0, 0, 4, 0), FlatStyle = FlatStyle.Flat };
            var clearAll = new Button { Name = "ClearDuplicateGroupsButton", Text = "Clear all selection", Dock = DockStyle.Fill, Margin = new Padding(4, 0, 0, 0), FlatStyle = FlatStyle.Flat };
            selectAll.Click += (s, e) => SetAllGroupsChecked(true);
            clearAll.Click += (s, e) => SetAllGroupsChecked(false);
            groupActions.Controls.Add(selectAll, 0, 0);
            groupActions.Controls.Add(clearAll, 1, 0);
            var filterPanel = new Panel { Dock = DockStyle.Top, Height = 44, Padding = new Padding(0, 16, 0, 6) };
            _filter.Name = "DuplicateGroupFilter";
            _filter.Dock = DockStyle.Fill;
            _filter.Font = new Font("Segoe UI", 9f);
            _filter.TextChanged += (s, e) => ApplyGroupFilter();
            filterPanel.Controls.Add(_filter);
            filterPanel.Controls.Add(new Label { Text = "Filter groups", Dock = DockStyle.Top, Height = 16, Font = new Font("Segoe UI", 7.5f), ForeColor = Color.FromArgb(100, 116, 139) });
            _groups.Dock = DockStyle.Fill;
            _groups.CheckOnClick = true;
            _groups.SelectedIndexChanged += (s, e) => BindSelectedGroup();
            _groups.ItemCheck += (s, e) =>
            {
                if (!_suppressGroupChecks && e.Index >= 0 && e.Index < _visibleItems.Count)
                {
                    SmartImportDuplicateGroup group = _visibleItems[e.Index];
                    if (e.NewValue == CheckState.Checked) _selectedGroups.Add(group); else _selectedGroups.Remove(group);
                }
                if (IsHandleCreated)
                    BeginInvoke((Action)UpdateSelectionStatus);
            };
            groupPanel.Controls.Add(_groups);
            groupPanel.Controls.Add(filterPanel);
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
            right.Controls.Add(new Label { Text = "Duplicate records selected", Dock = DockStyle.Fill, Padding = new Padding(0, 8, 0, 0), Font = new Font("Segoe UI", 9f, FontStyle.Bold) }, 0, 2);
            _duplicates.Dock = DockStyle.Fill;
            _duplicates.CheckOnClick = true;
            right.Controls.Add(_duplicates, 0, 3);
            var merge = new Button { Name = "MergeSelectedDuplicateGroupsButton", Text = "Merge selected groups", Dock = DockStyle.Fill, Margin = new Padding(0, 0, 6, 0), BackColor = Color.FromArgb(37, 99, 235), ForeColor = Color.White, FlatStyle = FlatStyle.Flat };
            merge.FlatAppearance.BorderSize = 0;
            merge.Click += (s, e) => MergeSelectedGroups();
            var delete = new Button { Name = "DeleteSelectedDuplicateRecordsButton", Text = "Delete duplicates", Dock = DockStyle.Fill, Margin = new Padding(6, 0, 0, 0), BackColor = Color.FromArgb(180, 30, 30), ForeColor = Color.White, FlatStyle = FlatStyle.Flat };
            delete.FlatAppearance.BorderSize = 0;
            delete.Click += (s, e) => DeleteSelectedDuplicates();
            var actionPanel = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 2, RowCount = 1 };
            actionPanel.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 56));
            actionPanel.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 44));
            actionPanel.Controls.Add(merge, 0, 0);
            actionPanel.Controls.Add(delete, 1, 0);
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
                _selectedGroups.Clear();
                foreach (SmartImportDuplicateGroup group in _items)
                {
                    SmartImportDuplicateRecord survivor = group.Records.FirstOrDefault();
                    _selectionStates[group] = new GroupSelectionState
                    {
                        SurvivorId = survivor == null ? null : survivor.RecordId,
                        DuplicateIds = new HashSet<string>(group.Records.Skip(1).Select(record => record.RecordId), StringComparer.OrdinalIgnoreCase)
                    };
                }
                if (_items.Count > 0) _selectedGroups.Add(_items[0]);
                ApplyGroupFilter();
                if (_items.Count == 0) { _survivor.DataSource = null; _duplicates.Items.Clear(); }
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
            _selectedGroups.Clear();
            foreach (SmartImportDuplicateGroup group in _visibleItems)
                if (isChecked) _selectedGroups.Add(group); else _selectedGroups.Remove(group);
            _suppressGroupChecks = true;
            try
            {
                for (int index = 0; index < _groups.Items.Count; index++)
                    _groups.SetItemChecked(index, isChecked);
            }
            finally { _suppressGroupChecks = false; }
            UpdateSelectionStatus();
        }

        private void ApplyGroupFilter()
        {
            string needle = (_filter.Text ?? string.Empty).Trim();
            _visibleItems = _items.Where(group => string.IsNullOrWhiteSpace(needle) ||
                ((group.MatchReason ?? string.Empty) + " " + string.Join(" ", group.Records.Select(record => record.DisplayName + " " + record.RecordId)))
                    .IndexOf(needle, StringComparison.OrdinalIgnoreCase) >= 0).ToList();
            _suppressGroupChecks = true;
            try
            {
                _groups.Items.Clear();
                foreach (SmartImportDuplicateGroup group in _visibleItems)
                    _groups.Items.Add(group.MatchReason + " - " + group.Records.Count + " records", _selectedGroups.Contains(group));
            }
            finally { _suppressGroupChecks = false; }
            if (_groups.Items.Count > 0) _groups.SelectedIndex = 0;
            else { _boundGroup = null; _survivor.DataSource = null; _duplicates.Items.Clear(); }
            UpdateSelectionStatus();
        }

        private void UpdateSelectionStatus()
        {
            _status.Text = _items.Count == 0
                ? "No existing duplicate groups remain."
                : _selectedGroups.Count + " of " + _items.Count + " group(s) selected; " + _visibleItems.Count + " visible. Overlaps are consolidated automatically.";
        }

        private void MergeSelectedGroups()
        {
            DuplicateCleanupPlanningResult planning;
            if (!TryBuildSelectedPlan(out planning)) return;
            List<DuplicateCleanupPlan> plans = planning.Plans;
            int duplicateCount = plans.Sum(plan => (plan.DuplicateIds ?? Enumerable.Empty<string>()).Count());
            if (plans.Count == 0 || duplicateCount == 0)
            {
                MessageBox.Show(this, "Select at least one duplicate group with records to archive.", "Nothing Selected", MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }
            string overlapNote = planning.ConsolidatedOverlapCount > 0 ? " Smart planning consolidated " + planning.ConsolidatedOverlapCount + " overlapping group(s) to prevent conflicts." : string.Empty;
            if (!ServoConfirmDialog.Show(this, "Merge " + plans.Count + " safe group(s) and archive " + duplicateCount + " duplicate record(s)?", "Linked records will be reassigned, the entire batch will run in one transaction, and the operation will be audited." + overlapNote)) return;
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

        private void DeleteSelectedDuplicates()
        {
            DuplicateCleanupPlanningResult planning;
            if (!TryBuildSelectedPlan(out planning)) return;
            List<DuplicateCleanupPlan> plans = planning.Plans;
            int duplicateCount = plans.Sum(plan => (plan.DuplicateIds ?? Enumerable.Empty<string>()).Count());
            string overlapNote = planning.ConsolidatedOverlapCount > 0 ? " Smart planning consolidated " + planning.ConsolidatedOverlapCount + " overlapping group(s)." : string.Empty;
            if (!ServoConfirmDialog.Show(this, "Permanently delete " + duplicateCount + " duplicate record(s)?", "The selected survivor in each safe group will remain. Linked records will be moved first, conflicting child rows will retain the survivor's version, and duplicate master records will then be permanently deleted in one transaction." + overlapNote)) return;
            try
            {
                DuplicateCleanupResult result = _service.MergeAndDeleteGroups(_module, plans);
                MessageBox.Show(this, result.Message, "Duplicate Records Deleted", MessageBoxButtons.OK, MessageBoxIcon.Information);
                LoadGroups();
            }
            catch (Exception ex)
            {
                AppLogger.LogError("SmartImportDuplicateCleanupDialog.DeleteSelectedDuplicates", ex);
                MessageBox.Show(this, "No records were changed. " + ex.Message, "Duplicate Delete Rolled Back", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            }
        }

        private bool TryBuildSelectedPlan(out DuplicateCleanupPlanningResult planning)
        {
            SaveBoundGroupSelection();
            var plans = new List<DuplicateCleanupPlan>();
            foreach (SmartImportDuplicateGroup group in _selectedGroups)
            {
                GroupSelectionState state = _selectionStates[group];
                plans.Add(new DuplicateCleanupPlan { SurvivorId = state.SurvivorId, DuplicateIds = state.DuplicateIds.ToList() });
            }
            try
            {
                planning = SmartImportDuplicateCleanupService.BuildSmartBulkPlan(plans);
                return true;
            }
            catch (InvalidOperationException ex)
            {
                planning = null;
                MessageBox.Show(this, ex.Message, "Nothing Selected", MessageBoxButtons.OK, MessageBoxIcon.Information);
                return false;
            }
        }

        private SmartImportDuplicateGroup SelectedGroup() { return _groups.SelectedIndex >= 0 && _groups.SelectedIndex < _visibleItems.Count ? _visibleItems[_groups.SelectedIndex] : null; }

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
