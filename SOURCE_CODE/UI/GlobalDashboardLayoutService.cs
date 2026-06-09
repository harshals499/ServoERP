using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Windows.Forms;
using HVAC_Pro_Desktop.Services;

namespace HVAC_Pro_Desktop.UI
{
    internal static class GlobalDashboardLayoutService
    {
        private const int Grid = 10;
        private static readonly object Sync = new object();
        private static readonly Dictionary<Control, CardState> States = new Dictionary<Control, CardState>();
        private static readonly Dictionary<Control, Button> ResetButtons = new Dictionary<Control, Button>();
        private static readonly HashSet<FlowLayoutPanel> MasonryFlows = new HashSet<FlowLayoutPanel>();
        private static readonly HashSet<FlowLayoutPanel> PackingFlows = new HashSet<FlowLayoutPanel>();

        /// <summary>Attaches global drag, resize, persistence, reflow, and reset behavior to card-like controls.</summary>
        public static void ApplyToTree(Control root)
        {
            if (root == null || root.IsDisposed)
                return;

            Control pageRoot = ResolvePageRoot(root);

            Dictionary<string, DashboardCardLayout> saved = LayoutManager.LoadPage(GetPageKey(pageRoot));
            DetachCardsThatNoLongerQualify(root);
            foreach (Control control in EnumerateControls(root).ToList())
            {
                if (IsCardCandidate(control) && !HasAttachedCardAncestor(control))
                    AttachCard(control, pageRoot, saved);
            }

            RestoreManagedOrder(root);
            ApplyMasonryPacking(root);
        }

        private static void DetachCardsThatNoLongerQualify(Control root)
        {
            if (root == null || root.IsDisposed)
                return;

            List<CardState> stale;
            lock (Sync)
            {
                stale = States.Values
                    .Where(state => state != null && state.Card != null && !state.Card.IsDisposed && IsDescendantOrSelf(root, state.Card) && !IsCardCandidate(state.Card))
                    .ToList();
            }

            foreach (CardState state in stale)
            {
                CardResizeGripService.Detach(state.Card);
                lock (Sync)
                    States.Remove(state.Card);
            }
        }

        private static bool IsDescendantOrSelf(Control root, Control candidate)
        {
            Control current = candidate;
            while (current != null)
            {
                if (current == root)
                    return true;
                current = current.Parent;
            }

            return false;
        }

        /// <summary>Deletes the saved layout for a page and restores default bounds for currently loaded cards.</summary>
        public static void ResetPage(Control pageRoot)
        {
            if (pageRoot == null || pageRoot.IsDisposed)
                return;

            string pageKey = GetPageKey(pageRoot);
            LayoutManager.ResetPage(pageKey);

            foreach (CardState state in States.Values.Where(state => state.PageRoot == pageRoot).ToList())
                state.RestoreDefault();

            RestoreManagedOrder(pageRoot, true);
            pageRoot.PerformLayout();
        }

        /// <summary>Returns true when a dashboard card is locked against drag and resize.</summary>
        public static bool IsCardLocked(Control card)
        {
            if (card == null)
                return false;

            lock (Sync)
            {
                CardState state;
                return States.TryGetValue(card, out state) && state.Locked;
            }
        }

        /// <summary>Toggles a dashboard card lock and persists it with the saved card layout.</summary>
        public static bool ToggleCardLock(Control card)
        {
            if (card == null)
                return false;

            CardState state;
            lock (Sync)
            {
                if (!States.TryGetValue(card, out state))
                    return false;
            }

            state.SetLocked(!state.Locked);
            return state.Locked;
        }

        /// <summary>Persists a dashboard card lock state.</summary>
        public static void SetCardLocked(Control card, bool locked)
        {
            if (card == null)
                return;

            CardState state;
            lock (Sync)
            {
                if (!States.TryGetValue(card, out state))
                    return;
            }

            state.SetLocked(locked);
        }

        private static void AttachCard(Control card, Control pageRoot, Dictionary<string, DashboardCardLayout> saved)
        {
            if (card == null || card.IsDisposed || pageRoot == null)
                return;

            lock (Sync)
            {
                if (States.ContainsKey(card))
                    return;
            }

            string pageKey = GetPageKey(pageRoot);
            string cardKey = BuildCardKey(card, pageRoot);
            string legacyCardKey = BuildLegacyCardKey(card, pageRoot);
            var state = new CardState(card, pageRoot, pageKey, cardKey);
            lock (Sync)
                States[card] = state;

            DashboardCardLayout layout;
            if (saved != null && saved.TryGetValue(cardKey, out layout))
                state.Apply(layout);
            else if (saved != null && !string.Equals(legacyCardKey, cardKey, StringComparison.OrdinalIgnoreCase) && saved.TryGetValue(legacyCardKey, out layout))
                state.Apply(layout);

            if (card is ResizableCard resizable)
            {
                resizable.CardDragRequested += (s, e) => state.BeginDrag(Control.MousePosition);
                resizable.CardResized += (s, e) => state.ResizeChanging();
                resizable.CardResizeComplete += (s, e) => state.Save();
            }
            else
            {
                CardResizeGripService.Attach(card, (c, size) => state.Save(), (c, size) => state.ResizeChanging());
                if (state.Locked)
                    CardResizeGripService.SetLocked(card, true);
            }

            state.HookControlTree(card);
            card.Disposed += (s, e) =>
            {
                lock (Sync)
                    States.Remove(card);
            };
        }

        private static void AddResetButton(Control pageRoot)
        {
            return;
        }

        private static void PositionResetButton(Control pageRoot, Button button)
        {
            if (pageRoot == null || button == null || button.IsDisposed)
                return;

            button.Location = new Point(Math.Max(8, pageRoot.ClientSize.Width - button.Width - 18), 8);
            button.BringToFront();
        }

        private static void RestoreManagedOrder(Control root, bool defaultsOnly = false)
        {
            foreach (FlowLayoutPanel flow in EnumerateControls(root).OfType<FlowLayoutPanel>())
            {
                List<CardState> cards = flow.Controls.Cast<Control>()
                    .Where(control => States.ContainsKey(control))
                    .Select(control => States[control])
                    .OrderBy(state => defaultsOnly ? state.DefaultChildIndex : state.Order)
                    .ToList();

                for (int i = 0; i < cards.Count; i++)
                    flow.Controls.SetChildIndex(cards[i].Card, i);
            }

            foreach (TableLayoutPanel table in EnumerateControls(root).OfType<TableLayoutPanel>())
            {
                foreach (CardState state in table.Controls.Cast<Control>().Where(control => States.ContainsKey(control)).Select(control => States[control]))
                {
                    if (defaultsOnly)
                    {
                        table.SetRow(state.Card, state.DefaultRow);
                        table.SetColumn(state.Card, state.DefaultColumn);
                    }
                }
            }
        }

        private static void ApplyMasonryPacking(Control root)
        {
            foreach (FlowLayoutPanel flow in EnumerateControls(root).OfType<FlowLayoutPanel>())
            {
                if (!ShouldPackFlow(flow))
                    continue;

                if (MasonryFlows.Add(flow))
                {
                    flow.WrapContents = true;
                    flow.AutoScroll = true;
                    flow.Resize += (s, e) => PackFlowLikeMasonry(flow);
                    flow.ControlAdded += (s, e) => PackFlowLikeMasonry(flow);
                    flow.ControlRemoved += (s, e) => PackFlowLikeMasonry(flow);
                }

                PackFlowLikeMasonry(flow);
            }
        }

        private static bool ShouldPackFlow(FlowLayoutPanel flow)
        {
            if (flow == null || flow.IsDisposed || flow.Controls.Count < 2)
                return false;

            List<Control> cards = flow.Controls.Cast<Control>()
                .Where(control => States.ContainsKey(control) || CardSurfacePolicy.IsDashboardLayoutCard(control))
                .ToList();

            return cards.Count >= 2 && cards.Count == flow.Controls.Count;
        }

        private static void PackFlowLikeMasonry(FlowLayoutPanel flow)
        {
            if (flow == null || flow.IsDisposed || PackingFlows.Contains(flow) || !ShouldPackFlow(flow))
                return;

            try
            {
                PackingFlows.Add(flow);
                int usable = flow.ClientSize.Width
                    - flow.Padding.Left
                    - flow.Padding.Right
                    - SystemInformation.VerticalScrollBarWidth
                    - Grid;
                if (usable < 360)
                    return;

                List<Control> cards = flow.Controls.Cast<Control>()
                    .Where(control => States.ContainsKey(control) || CardSurfacePolicy.IsDashboardLayoutCard(control))
                    .ToList();
                List<Control> row = new List<Control>();
                int rowWidth = 0;

                foreach (Control card in cards)
                {
                    NormalizeFlowCard(card);
                    int cardOuterWidth = GetOuterWidth(card);
                    if (row.Count > 0 && rowWidth + cardOuterWidth > usable)
                    {
                        FillFlowRow(row, usable);
                        row.Clear();
                        rowWidth = 0;
                    }

                    row.Add(card);
                    rowWidth += GetOuterWidth(card);
                }

                FillFlowRow(row, usable);
                flow.PerformLayout();

                int bottom = 0;
                foreach (Control card in cards)
                    bottom = Math.Max(bottom, card.Bottom + card.Margin.Bottom + flow.Padding.Bottom + 12);
                flow.AutoScrollMinSize = new Size(0, Math.Max(flow.ClientSize.Height, bottom));
            }
            finally
            {
                PackingFlows.Remove(flow);
            }
        }

        private static void NormalizeFlowCard(Control card)
        {
            if (card == null)
                return;

            if (card.Dock != DockStyle.None)
                card.Dock = DockStyle.None;
            card.Anchor = AnchorStyles.Top | AnchorStyles.Left;
            if (card.Margin == Padding.Empty)
                card.Margin = new Padding(0, 0, 12, 12);
            if (card.Width < card.MinimumSize.Width)
                card.Width = card.MinimumSize.Width;
            if (card.Height < card.MinimumSize.Height)
                card.Height = card.MinimumSize.Height;
        }

        private static void FillFlowRow(List<Control> row, int usable)
        {
            if (row == null || row.Count == 0 || usable <= 0)
                return;

            int rowOuterWidth = row.Sum(GetOuterWidth);
            int extra = usable - rowOuterWidth;
            if (extra <= Grid / 2)
                return;

            Control target = row
                .Where(control => !IsCardLocked(control))
                .OrderByDescending(control => control.Width * Math.Max(1, control.Height))
                .FirstOrDefault();
            if (target == null)
                return;

            int maximum = target.MaximumSize.Width > 0 ? target.MaximumSize.Width : 1800;
            target.Width = Math.Max(target.MinimumSize.Width, Math.Min(maximum, target.Width + extra));
        }

        private static int GetOuterWidth(Control control)
        {
            if (control == null)
                return 0;

            return control.Width + control.Margin.Left + control.Margin.Right;
        }

        private static bool IsPageRoot(Control control)
        {
            return control is UserControl || (control is Form && !string.Equals(control.GetType().Name, "MainForm", StringComparison.OrdinalIgnoreCase));
        }

        private static Control ResolvePageRoot(Control control)
        {
            Control current = control;
            while (current != null && !(current is UserControl) && !(current is Form))
                current = current.Parent;
            return current ?? control;
        }

        private static string GetPageKey(Control pageRoot)
        {
            string name = pageRoot == null ? "Screen" : pageRoot.GetType().Name;
            if (name.EndsWith("Form", StringComparison.OrdinalIgnoreCase))
                name = name.Substring(0, name.Length - 4);
            return string.IsNullOrWhiteSpace(name) ? "Screen" : name;
        }

        private static bool HasAttachedCardAncestor(Control control)
        {
            Control parent = control == null ? null : control.Parent;
            while (parent != null)
            {
                if (States.ContainsKey(parent))
                    return true;
                parent = parent.Parent;
            }

            return false;
        }

        private static bool IsCardCandidate(Control control)
        {
            return CardSurfacePolicy.IsDashboardLayoutCard(control);
        }

        private static string BuildCardKey(Control card, Control pageRoot)
        {
            string explicitKey = FirstNonEmpty(
                card is ResizableCard resizable ? resizable.CardKey : null,
                card is DraggableCard draggable ? draggable.CardKey : null,
                card.Name);

            if (!string.IsNullOrWhiteSpace(explicitKey))
                return CleanKey(explicitKey);

            string key = FirstNonEmpty(FindTitle(card), card.GetType().Name);
            return CleanKey(BuildStablePath(card, pageRoot, key));
        }

        private static string BuildLegacyCardKey(Control card, Control pageRoot)
        {
            string key = FirstNonEmpty(
                card.Name,
                card is ResizableCard resizable ? resizable.CardKey : null,
                card is DraggableCard draggable ? draggable.CardKey : null,
                FindTitle(card),
                card.GetType().Name);

            return CleanKey(key) + "_" + BuildSiblingPath(card, pageRoot);
        }

        private static string FindTitle(Control root)
        {
            GroupBox group = root as GroupBox;
            if (group != null && !string.IsNullOrWhiteSpace(group.Text))
                return group.Text;

            foreach (Label label in EnumerateControls(root).OfType<Label>())
            {
                string text = (label.Text ?? string.Empty).Trim();
                if (text.Length >= 2 && text.Length <= 80)
                    return text;
            }

            return string.Empty;
        }

        private static string BuildSiblingPath(Control card, Control pageRoot)
        {
            var parts = new List<string>();
            Control current = card;
            while (current != null && current != pageRoot)
            {
                Control parent = current.Parent;
                int index = parent == null ? 0 : parent.Controls.GetChildIndex(current);
                parts.Add(index.ToString("D2"));
                current = parent;
            }

            parts.Reverse();
            return string.Join("_", parts.ToArray());
        }

        private static string BuildStablePath(Control card, Control pageRoot, string leafKey)
        {
            var parts = new List<string>();
            Control current = card;
            while (current != null && current != pageRoot)
            {
                string segment = CleanKey(FirstNonEmpty(
                    current.Name,
                    current is ResizableCard resizable ? resizable.CardKey : null,
                    current is DraggableCard draggable ? draggable.CardKey : null,
                    current == card ? leafKey : FindTitle(current),
                    current.GetType().Name));

                if (!string.IsNullOrWhiteSpace(segment))
                    parts.Add(segment);

                current = current.Parent;
            }

            parts.Reverse();
            return string.Join("_", parts.ToArray());
        }

        private static string CleanKey(string value)
        {
            if (string.IsNullOrWhiteSpace(value))
                return "Card";

            char[] chars = value.Trim().Where(ch => char.IsLetterOrDigit(ch) || ch == '_' || ch == '-').ToArray();
            string cleaned = new string(chars);
            return string.IsNullOrWhiteSpace(cleaned) ? "Card" : cleaned;
        }

        private static string FirstNonEmpty(params string[] values)
        {
            foreach (string value in values)
            {
                if (!string.IsNullOrWhiteSpace(value))
                    return value.Trim();
            }

            return string.Empty;
        }

        private static bool ContainsAny(string value, params string[] tokens)
        {
            foreach (string token in tokens)
            {
                if (!string.IsNullOrWhiteSpace(token) && value.IndexOf(token, StringComparison.OrdinalIgnoreCase) >= 0)
                    return true;
            }

            return false;
        }

        private static IEnumerable<Control> EnumerateControls(Control root)
        {
            if (root == null)
                yield break;

            yield return root;
            foreach (Control child in root.Controls)
            {
                foreach (Control descendant in EnumerateControls(child))
                    yield return descendant;
            }
        }

        private sealed class CardState
        {
            public readonly Control Card;
            public readonly Control PageRoot;
            public readonly string PageKey;
            public readonly string CardKey;
            public readonly Rectangle DefaultBounds;
            public readonly DockStyle DefaultDock;
            public readonly AnchorStyles DefaultAnchor;
            public readonly Padding DefaultMargin;
            public readonly int DefaultChildIndex;
            public readonly int DefaultRow;
            public readonly int DefaultColumn;
            private readonly HashSet<Control> _hooked = new HashSet<Control>();
            private bool _dragging;
            private bool _locked;
            private Point _startMouse;
            private Rectangle _startBounds;

            public CardState(Control card, Control pageRoot, string pageKey, string cardKey)
            {
                Card = card;
                PageRoot = pageRoot;
                PageKey = pageKey;
                CardKey = cardKey;
                DefaultBounds = card.Bounds;
                DefaultDock = card.Dock;
                DefaultAnchor = card.Anchor;
                DefaultMargin = card.Margin;
                DefaultChildIndex = card.Parent == null ? 0 : card.Parent.Controls.GetChildIndex(card);
                TableLayoutPanel table = card.Parent as TableLayoutPanel;
                DefaultRow = table == null ? -1 : table.GetRow(card);
                DefaultColumn = table == null ? -1 : table.GetColumn(card);
            }

            public int Order
            {
                get { return Card.Parent == null ? DefaultChildIndex : Card.Parent.Controls.GetChildIndex(Card); }
            }

            public bool Locked { get { return _locked; } }

            public void HookControlTree(Control root)
            {
                if (root == null || root.IsDisposed || ShouldSkipDragSurface(root))
                    return;

                if (_hooked.Add(root))
                {
                    root.MouseDown += MouseDown;
                    root.MouseMove += MouseMove;
                    root.MouseUp += MouseUp;
                    root.Disposed += (s, e) => _hooked.Remove(root);
                    root.ControlAdded += (s, e) => HookControlTree(e.Control);
                }

                foreach (Control child in root.Controls)
                    HookControlTree(child);
            }

            public void Apply(DashboardCardLayout layout)
            {
                if (layout == null)
                    return;

                if (layout.Width > 0 && layout.Height > 0)
                    Card.Size = new Size(Math.Max(Card.MinimumSize.Width, layout.Width), Math.Max(Card.MinimumSize.Height, layout.Height));

                SetLocked(layout.Locked, false);

                FlowLayoutPanel flow = Card.Parent as FlowLayoutPanel;
                TableLayoutPanel table = Card.Parent as TableLayoutPanel;
                if (flow != null)
                    return;
                if (table != null)
                {
                    if (layout.Row >= 0 && layout.Row < table.RowCount)
                        table.SetRow(Card, layout.Row);
                    if (layout.Column >= 0 && layout.Column < table.ColumnCount)
                        table.SetColumn(Card, layout.Column);
                    return;
                }

                if (layout.Width > 0 && layout.Height > 0)
                {
                    PrepareAbsoluteMovement();
                    Card.Location = ClampLocation(new Point(Snap(layout.X), Snap(layout.Y)));
                }
            }

            public void RestoreDefault()
            {
                Card.Dock = DefaultDock;
                Card.Anchor = DefaultAnchor;
                Card.Margin = DefaultMargin;
                Card.Bounds = DefaultBounds;
                if (Card.Parent != null)
                    Card.Parent.Controls.SetChildIndex(Card, DefaultChildIndex);
            }

            public void ResizeChanging()
            {
                if (_locked)
                    return;

                Save();
                FlowLayoutPanel flow = Card.Parent as FlowLayoutPanel;
                if (flow != null)
                    PackFlowLikeMasonry(flow);
                else
                    AutoReflow(Card);
            }

            public void Save()
            {
                TableLayoutPanel table = Card.Parent as TableLayoutPanel;
                LayoutManager.SaveCard(PageKey, new DashboardCardLayout
                {
                    CardKey = CardKey,
                    X = Card.Left,
                    Y = Card.Top,
                    Width = Card.Width,
                    Height = Card.Height,
                    Order = Card.Parent == null ? DefaultChildIndex : Card.Parent.Controls.GetChildIndex(Card),
                    Row = table == null ? -1 : table.GetRow(Card),
                    Column = table == null ? -1 : table.GetColumn(Card),
                    ParentKey = Card.Parent == null ? string.Empty : Card.Parent.Name,
                    Locked = _locked
                });
            }

            public void SetLocked(bool locked)
            {
                SetLocked(locked, true);
            }

            private void SetLocked(bool locked, bool save)
            {
                _locked = locked;
                _dragging = false;
                if (Card != null && !Card.IsDisposed)
                {
                    Card.Capture = false;
                    CardResizeGripService.SetLocked(Card, locked);
                    ResizableCard resizable = Card as ResizableCard;
                    if (resizable != null)
                        resizable.SetLayoutLocked(locked);
                }

                if (save)
                    Save();
            }

            public void BeginDrag(Point mousePosition)
            {
                if (_locked || Card.Parent == null)
                    return;

                _dragging = true;
                _startMouse = mousePosition;
                _startBounds = Card.Bounds;
                Card.Capture = true;
                Card.BringToFront();
            }

            private void MouseDown(object sender, MouseEventArgs e)
            {
                if (e.Button != MouseButtons.Left)
                    return;

                Control source = sender as Control;
                if (_locked || ShouldSkipDragSurface(source))
                    return;

                BeginDrag(Control.MousePosition);
            }

            private void MouseMove(object sender, MouseEventArgs e)
            {
                if (!_dragging || Card.Parent == null)
                    return;

                if (Card.Parent is FlowLayoutPanel || Card.Parent is TableLayoutPanel)
                    return;

                PrepareAbsoluteMovement();
                Point mouse = Control.MousePosition;
                Point target = new Point(_startBounds.Left + mouse.X - _startMouse.X, _startBounds.Top + mouse.Y - _startMouse.Y);
                Card.Location = ClampLocation(new Point(Snap(target.X), Snap(target.Y)));
                AutoReflow(Card);
            }

            private void MouseUp(object sender, MouseEventArgs e)
            {
                if (!_dragging)
                    return;

                _dragging = false;
                Card.Capture = false;

                if (Card.Parent is FlowLayoutPanel)
                    ReorderFlowCard(Control.MousePosition);
                else if (Card.Parent is TableLayoutPanel)
                    SwapTableCard(Control.MousePosition);

                Save();
            }

            private void PrepareAbsoluteMovement()
            {
                if (Card.Dock != DockStyle.None)
                {
                    Rectangle bounds = Card.Bounds;
                    Card.Dock = DockStyle.None;
                    Card.Bounds = bounds;
                }

                Card.Anchor = AnchorStyles.Top | AnchorStyles.Left;
            }

            private Point ClampLocation(Point target)
            {
                Control parent = Card.Parent;
                if (parent == null)
                    return target;

                int maxX = Math.Max(0, parent.ClientSize.Width - Card.Width - parent.Padding.Right);
                int x = Math.Max(parent.Padding.Left, Math.Min(target.X, maxX));
                int y = Math.Max(parent.Padding.Top, target.Y);
                return new Point(x, y);
            }

            private void ReorderFlowCard(Point screenPoint)
            {
                FlowLayoutPanel flow = Card.Parent as FlowLayoutPanel;
                if (flow == null)
                    return;

                Point point = flow.PointToClient(screenPoint);
                Control target = flow.GetChildAtPoint(point);
                if (target != null && target != Card)
                    flow.Controls.SetChildIndex(Card, Math.Max(0, flow.Controls.GetChildIndex(target)));
                flow.PerformLayout();
                PackFlowLikeMasonry(flow);
            }

            private void SwapTableCard(Point screenPoint)
            {
                TableLayoutPanel table = Card.Parent as TableLayoutPanel;
                if (table == null)
                    return;

                Point point = table.PointToClient(screenPoint);
                for (int row = 0; row < table.RowCount; row++)
                {
                    for (int col = 0; col < table.ColumnCount; col++)
                    {
                        Rectangle cell = GetCellBounds(table, col, row);
                        if (!cell.Contains(point))
                            continue;

                        Control other = table.GetControlFromPosition(col, row);
                        int oldRow = table.GetRow(Card);
                        int oldCol = table.GetColumn(Card);
                        if (other != null && other != Card)
                        {
                            table.SetRow(other, oldRow);
                            table.SetColumn(other, oldCol);
                        }

                        table.SetRow(Card, row);
                        table.SetColumn(Card, col);
                        table.PerformLayout();
                        return;
                    }
                }
            }

            private Rectangle GetCellBounds(TableLayoutPanel table, int column, int row)
            {
                int x = 0;
                for (int i = 0; i < column; i++)
                    x += table.GetColumnWidths()[i];
                int y = 0;
                for (int i = 0; i < row; i++)
                    y += table.GetRowHeights()[i];
                return new Rectangle(x, y, table.GetColumnWidths()[column], table.GetRowHeights()[row]);
            }

            private void AutoReflow(Control moved)
            {
                Control parent = moved == null ? null : moved.Parent;
                if (parent == null || parent is FlowLayoutPanel || parent is TableLayoutPanel)
                    return;

                List<Control> siblings = parent.Controls.Cast<Control>()
                    .Where(control => control != moved && States.ContainsKey(control) && control.Dock == DockStyle.None)
                    .OrderBy(control => control.Top)
                    .ThenBy(control => control.Left)
                    .ToList();

                Rectangle occupied = moved.Bounds;
                foreach (Control sibling in siblings)
                {
                    int guard = 0;
                    while (occupied.IntersectsWith(sibling.Bounds) && guard++ < 120)
                        sibling.Top = Snap(sibling.Bottom + Grid);
                }

                ScrollableControl scroll = parent as ScrollableControl;
                if (scroll != null)
                    scroll.AutoScrollMinSize = new Size(scroll.AutoScrollMinSize.Width, Math.Max(scroll.AutoScrollMinSize.Height, parent.Controls.Cast<Control>().Max(control => control.Bottom) + 24));
            }

            private int Snap(int value)
            {
                return (int)Math.Round(value / (double)Grid) * Grid;
            }

            private bool ShouldSkipDragSurface(Control control)
            {
                return CardSurfacePolicy.ShouldSkipRightClickSurface(control)
                    || control is Button
                    || control is TextBoxBase
                    || control is ComboBox
                    || control is DateTimePicker
                    || control is NumericUpDown
                    || control is LinkLabel
                    || control is DataGridView
                    || control is ListView
                    || control is TreeView
                    || control is TabControl
                    || control is ToolStrip;
            }
        }
    }
}
