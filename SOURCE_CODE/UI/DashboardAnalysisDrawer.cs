using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Linq;
using System.Windows.Forms;
using System.Windows.Forms.DataVisualization.Charting;
using HVAC_Pro_Desktop;
using HVAC_Pro_Desktop.Services;

namespace HVAC_Pro_Desktop.UI
{
    public enum DashboardAnalysisEntityType { Dashboard, Purchase, Invoice, Quotation, Inventory, Client, Vendor, Contract, Job }
    public enum DashboardAnalysisBadgeVariant { Teal, Red, Amber, Blue, Green }
    public enum DashboardAnalysisAlertVariant { None, Red, Amber, Green, Blue }
    public enum DashboardAnalysisActionStyle { Default, Primary, Secondary, Destructive, Amber, Teal }
    public enum DashboardAnalysisDateRange { ThisMonth, Last30Days, QuarterToDate, FinancialYear, AllTime }
    public enum DashboardAnalysisValueFormat { Number, Currency, Percent, Count }

    public sealed class DashboardAnalysisMetric
    {
        public string Label { get; set; }
        public string Value { get; set; }
        public string Subtitle { get; set; }
        public Color Accent { get; set; } = Color.FromArgb(24, 95, 165);
    }

    public sealed class DashboardAnalysisFact
    {
        public string Label { get; set; }
        public string Value { get; set; }
    }

    public sealed class DashboardAnalysisInsight
    {
        public string Title { get; set; }
        public string Detail { get; set; }
        public Color Accent { get; set; } = Color.FromArgb(29, 158, 117);
    }

    public sealed class DashboardAnalysisTable
    {
        public string Title { get; set; }
        public string Subtitle { get; set; }
        public string StyleKey { get; set; }
        public List<string> Columns { get; set; } = new List<string>();
        public List<string[]> Rows { get; set; } = new List<string[]>();
    }

    public sealed class DashboardAnalysisChartPoint
    {
        public string Label { get; set; }
        public decimal Value { get; set; }
        public Color Color { get; set; } = Color.FromArgb(29, 158, 117);
    }

    public sealed class DashboardAnalysisChart
    {
        public string Title { get; set; }
        public string Subtitle { get; set; }
        public SeriesChartType ChartType { get; set; } = SeriesChartType.Column;
        public DashboardAnalysisValueFormat ValueFormat { get; set; } = DashboardAnalysisValueFormat.Number;
        public string CenterText { get; set; }
        public string CenterSubtext { get; set; }
        public bool ShowLegend { get; set; }
        public List<DashboardAnalysisChartPoint> Points { get; set; } = new List<DashboardAnalysisChartPoint>();
    }

    public sealed class DashboardAnalysisTimelineItem
    {
        public string Title { get; set; }
        public string Detail { get; set; }
        public string TimeText { get; set; }
        public Color Accent { get; set; } = Color.FromArgb(29, 158, 117);
    }

    public sealed class DashboardAnalysisAction
    {
        public string Label { get; set; }
        public Action Handler { get; set; }
        public DashboardAnalysisActionStyle Style { get; set; } = DashboardAnalysisActionStyle.Default;
        public Color BackColor { get; set; } = Color.White;
        public Color ForeColor { get; set; } = Color.FromArgb(26, 26, 26);
    }

    public sealed class DashboardAnalysisAlgorithmFactor
    {
        public string Label { get; set; }
        public string Value { get; set; }
        public string Detail { get; set; }
        public int Score { get; set; }
        public Color Accent { get; set; } = Color.FromArgb(29, 158, 117);
    }

    public sealed class DashboardAnalysisAlert
    {
        public DashboardAnalysisAlertVariant Variant { get; set; }
        public string Title { get; set; }
        public string Body { get; set; }
    }

    public sealed class DashboardAnalysisModel
    {
        public string PageKey { get; set; }
        public DashboardAnalysisEntityType EntityType { get; set; }
        public string Title { get; set; }
        public string Subtitle { get; set; }
        public string StatusText { get; set; }
        public Color StatusColor { get; set; } = Color.FromArgb(29, 158, 117);
        public Color AccentColor { get; set; } = Color.FromArgb(29, 158, 117);
        public string SummaryText { get; set; }
        public string BadgeText { get; set; }
        public DashboardAnalysisBadgeVariant BadgeVariant { get; set; } = DashboardAnalysisBadgeVariant.Teal;
        public DashboardAnalysisAlert Alert { get; set; }
        public DashboardAnalysisDateRange SelectedRange { get; set; } = DashboardAnalysisDateRange.ThisMonth;
        public Action<DashboardAnalysisDateRange> RangeChanged { get; set; }
        public bool ShowDateFilter { get; set; } = true;
        public string AlgorithmTitle { get; set; }
        public string AlgorithmSummary { get; set; }
        public int AlgorithmScore { get; set; }
        public string AlgorithmStatusText { get; set; }
        public Color AlgorithmStatusColor { get; set; } = Color.FromArgb(29, 158, 117);
        public string ComparisonTitle { get; set; }
        public List<DashboardAnalysisMetric> Metrics { get; set; } = new List<DashboardAnalysisMetric>();
        public List<DashboardAnalysisFact> Facts { get; set; } = new List<DashboardAnalysisFact>();
        public List<string> Insights { get; set; } = new List<string>();
        public List<DashboardAnalysisInsight> InsightCards { get; set; } = new List<DashboardAnalysisInsight>();
        public List<DashboardAnalysisFact> ComparisonFacts { get; set; } = new List<DashboardAnalysisFact>();
        public List<DashboardAnalysisChart> Charts { get; set; } = new List<DashboardAnalysisChart>();
        public List<DashboardAnalysisAlgorithmFactor> AlgorithmFactors { get; set; } = new List<DashboardAnalysisAlgorithmFactor>();
        public List<DashboardAnalysisTable> Tables { get; set; } = new List<DashboardAnalysisTable>();
        public List<DashboardAnalysisTimelineItem> Timeline { get; set; } = new List<DashboardAnalysisTimelineItem>();
        public List<DashboardAnalysisAction> Actions { get; set; } = new List<DashboardAnalysisAction>();
    }

    public class DashboardAnalysisDrawer : UserControl
    {
        private const int PagePadding = 20;
        private const int SectionGap = 18;
        private const int CardRadius = 10;
        private const int MetricRadius = 8;
        private const int MaxContentWidth = 1360;

        private static readonly Color White = DS.White;
        private static readonly Color MetricBg = DS.Slate50;
        private static readonly Color Border = DS.Border;
        private static readonly Color TextPrimary = DS.Slate900;
        private static readonly Color TextSecondary = DS.Slate600;
        private static readonly Color TextHint = DS.Slate400;
        private static readonly Color Teal = DS.Teal600;
        private static readonly Color Amber = DS.Amber500;
        private static readonly Color Red = DS.Red500;
        private static readonly Color RedDark = DS.Red600;
        private static readonly Color Blue = DS.Primary600;
        private static readonly Color Green = DS.Green600;
        private static readonly Color RedLight = DS.Red50;
        private static readonly Color AmberLight = DS.Amber50;
        private static readonly Color GreenLight = DS.Green50;
        private static readonly Color TealBadgeBg = DS.Teal50;
        private static readonly Color TealBadgeText = Color.FromArgb(15, 118, 110);
        private static readonly Color RedBadgeBg = DS.Red50;
        private static readonly Color AmberBadgeBg = DS.Amber50;
        private static readonly Color AmberBadgeText = Color.FromArgb(146, 64, 14);
        private static readonly Color BlueBadgeBg = DS.Primary50;
        private static readonly Color BlueBadgeText = DS.Primary700;
        private static readonly Color ScoreTrack = DS.Slate100;
        private static readonly Color TableAlt = DS.Slate50;

        private readonly Panel _topBar;
        private readonly Label _titleLabel;
        private readonly Label _subtitleLabel;
        private readonly Label _badgeLabel;
        private readonly Button _backButton;
        private readonly ComboBox _rangePicker;
        private readonly Panel _scrollHost;
        private readonly FlowLayoutPanel _contentFlow;
        private DashboardAnalysisModel _model;
        private bool _suppressRangeChange;
        public event EventHandler BackRequested;

        public DashboardAnalysisDrawer()
        {
            Dock = DockStyle.Fill;
            BackColor = DS.BgPage;

            _topBar = new Panel
            {
                Dock = DockStyle.Top,
                Height = 82,
                BackColor = White
            };
            _topBar.Paint += (s, e) =>
            {
                using (Pen pen = new Pen(Border, 0.5f))
                    e.Graphics.DrawLine(pen, 0, _topBar.Height - 1, _topBar.Width, _topBar.Height - 1);
            };

            _titleLabel = new Label
            {
                AutoSize = false,
                ForeColor = TextPrimary,
                Font = new Font("Segoe UI", 18f, FontStyle.Bold),
                Location = new Point(PagePadding, 8),
                Height = 34
            };

            _subtitleLabel = new Label
            {
                AutoSize = false,
                ForeColor = TextSecondary,
                Font = new Font("Segoe UI", 9f, FontStyle.Regular),
                Location = new Point(PagePadding, 42),
                Height = 22
            };

            _badgeLabel = new Label
            {
                AutoSize = true,
                Font = new Font("Segoe UI", 8.5f, FontStyle.Bold),
                ForeColor = TealBadgeText,
                BackColor = TealBadgeBg,
                Padding = new Padding(10, 3, 10, 3)
            };

            _rangePicker = new ComboBox
            {
                DropDownStyle = ComboBoxStyle.DropDownList,
                Width = 132,
                Height = 28,
                Font = new Font("Segoe UI", 9f, FontStyle.Regular),
                BackColor = White,
                ForeColor = TextSecondary,
                FlatStyle = FlatStyle.Flat
            };
            _rangePicker.Items.AddRange(new object[] { "This month", "Last 30 days", "Quarter to date", "Financial year", "All time" });
            _rangePicker.SelectedIndexChanged += (s, e) =>
            {
                if (_suppressRangeChange || _model == null || _model.RangeChanged == null || _rangePicker.SelectedIndex < 0)
                    return;

                _model.RangeChanged(MapRange(_rangePicker.SelectedIndex));
            };

            _backButton = new Button
            {
                Text = "<- Back to dashboard",
                Width = 178,
                Height = 30,
                FlatStyle = FlatStyle.Flat,
                BackColor = White,
                ForeColor = TextSecondary,
                Font = new Font("Segoe UI", 9f, FontStyle.Bold),
                Padding = new Padding(5, 0, 5, 0)
            };
            _backButton.FlatAppearance.BorderSize = 1;
            _backButton.FlatAppearance.BorderColor = Border;
            _backButton.FlatAppearance.MouseDownBackColor = White;
            _backButton.FlatAppearance.MouseOverBackColor = White;
            ApplyRoundedControl(_backButton, 6);
            _backButton.Click += (s, e) => BackRequested?.Invoke(this, EventArgs.Empty);

            _topBar.Controls.AddRange(new Control[] { _titleLabel, _subtitleLabel, _badgeLabel, _rangePicker, _backButton });
            _topBar.Resize += (s, e) => LayoutTopBar();

            _scrollHost = new Panel
            {
                Dock = DockStyle.Fill,
                BackColor = DS.BgPage,
                AutoScroll = true
            };
            _scrollHost.Resize += (s, e) => LayoutContent();

            _contentFlow = new FlowLayoutPanel
            {
                FlowDirection = FlowDirection.TopDown,
                WrapContents = false,
                AutoSize = true,
                BackColor = DS.BgPage,
                Margin = new Padding(0),
                Padding = new Padding(PagePadding, PagePadding, PagePadding, PagePadding)
            };
            _scrollHost.Controls.Add(_contentFlow);

            Controls.Add(_scrollHost);
            Controls.Add(_topBar);
        }

        public void ShowAnalysis(DashboardAnalysisModel model)
        {
            _model = model ?? new DashboardAnalysisModel();
            Render();
        }

        private void Render()
        {
            DashboardAnalysisModel model = _model ?? new DashboardAnalysisModel();
            string pageKey = ResolvePageKey(model);
            _titleLabel.Text = string.IsNullOrWhiteSpace(model.Title) ? "Analysis" : model.Title;
            _subtitleLabel.Text = model.Subtitle ?? string.Empty;

            string badgeText = string.IsNullOrWhiteSpace(model.BadgeText) ? model.StatusText : model.BadgeText;
            _badgeLabel.Text = string.IsNullOrWhiteSpace(badgeText) ? "Live" : badgeText;
            ApplyBadgeStyle(_badgeLabel, model);

            _rangePicker.Visible = model.ShowDateFilter;
            if (_rangePicker.Visible)
            {
                _suppressRangeChange = true;
                _rangePicker.SelectedIndex = MapRange(model.SelectedRange);
                _suppressRangeChange = false;
            }

            _contentFlow.SuspendLayout();
            _contentFlow.Controls.Clear();

            if (model.Alert != null && (!string.IsNullOrWhiteSpace(model.Alert.Title) || !string.IsNullOrWhiteSpace(model.Alert.Body)))
                _contentFlow.Controls.Add(CreateAlertStrip(model.Alert));
            if (model.Metrics.Any())
                _contentFlow.Controls.Add(CreateMetricSection(pageKey, model.Metrics));
            if (model.AlgorithmFactors.Any() || !string.IsNullOrWhiteSpace(model.AlgorithmTitle))
                _contentFlow.Controls.Add(CreateAlgorithmSection(pageKey, model));

            List<DashboardAnalysisChart> charts = model.Charts.Where(c => c != null && c.Points.Any()).ToList();
            for (int i = 0; i < charts.Count; i += 2)
            {
                if (i + 1 < charts.Count)
                    _contentFlow.Controls.Add(CreateTwoColumnRow(CreateChartCard(pageKey, charts[i], i), CreateChartCard(pageKey, charts[i + 1], i + 1)));
                else
                    _contentFlow.Controls.Add(CreateChartCard(pageKey, charts[i], i));
            }

            if (model.Facts.Any())
                _contentFlow.Controls.Add(CreateFactSection(pageKey, "Snapshot", "Key details from the current dataset", model.Facts, 2, "facts_snapshot"));
            if (model.ComparisonFacts.Any())
                _contentFlow.Controls.Add(CreateFactSection(pageKey, string.IsNullOrWhiteSpace(model.ComparisonTitle) ? "Comparison" : model.ComparisonTitle, string.Empty, model.ComparisonFacts, 2, "facts_comparison"));
            if (model.InsightCards.Any())
                _contentFlow.Controls.Add(CreateInsightCardSection(pageKey, "Insights", "Context-aware signals generated from the data", model.InsightCards));
            if (model.Insights.Any())
                _contentFlow.Controls.Add(CreateLegacyInsightSection(pageKey, model.Insights));
            foreach (DashboardAnalysisTable table in model.Tables.Where(t => t != null))
                _contentFlow.Controls.Add(CreateTableSection(pageKey, table));
            if (model.Timeline.Any())
                _contentFlow.Controls.Add(CreateTimelineSection(pageKey, model.Timeline));
            if (model.Actions.Any())
                _contentFlow.Controls.Add(CreateActionsSection(model.Actions));

            try
            {
                new CardLayoutService().ApplyLayoutToPage(this, pageKey, CardLayoutService.ResolveCurrentUserId());
            }
            catch (Exception ex)
            {
                AppRuntime.LogException("DashboardAnalysisDrawer.ApplyLayout(" + pageKey + ")", ex);
            }

            _contentFlow.ResumeLayout();
            LayoutTopBar();
            LayoutContent();
        }

        private void LayoutTopBar()
        {
            _backButton.Location = new Point(_topBar.Width - _backButton.Width - PagePadding, 13);
            _rangePicker.Location = new Point(_backButton.Left - _rangePicker.Width - 12, 14);

            _titleLabel.Location = new Point(PagePadding, 8);
            _titleLabel.Width = Math.Max(360, _rangePicker.Left - PagePadding - 240);
            _subtitleLabel.Location = new Point(PagePadding, 42);
            _subtitleLabel.Width = Math.Max(420, _rangePicker.Left - PagePadding - 40);

            int badgeLeft = _titleLabel.Right + 14;
            _badgeLabel.Location = new Point(badgeLeft, 13);

            if (_badgeLabel.Right > _rangePicker.Left - 12)
                _badgeLabel.Left = Math.Max(PagePadding + 240, _rangePicker.Left - _badgeLabel.Width - 12);
        }

        private void LayoutContent()
        {
            int availableWidth = Math.Max(960, _scrollHost.ClientSize.Width - SystemInformation.VerticalScrollBarWidth - 8);
            _contentFlow.Width = Math.Min(MaxContentWidth, availableWidth);
            _contentFlow.Left = Math.Max(0, (_scrollHost.ClientSize.Width - _contentFlow.Width) / 2);
        }

        private Control CreateAlertStrip(DashboardAnalysisAlert alert)
        {
            Panel alertPanel = new Panel
            {
                Width = MaxContentWidth - (PagePadding * 2),
                Height = 72,
                BackColor = ResolveAlertBackColor(alert.Variant),
                Margin = new Padding(0, 0, 0, SectionGap)
            };
            alertPanel.Paint += (s, e) =>
            {
                using (SolidBrush brush = new SolidBrush(ResolveAlertAccent(alert.Variant)))
                    e.Graphics.FillRectangle(brush, 0, 0, 3, alertPanel.Height);
            };

            Label title = new Label
            {
                Text = alert.Title ?? string.Empty,
                Font = new Font("Segoe UI", 13f, FontStyle.Bold),
                ForeColor = alert.Variant == DashboardAnalysisAlertVariant.Red ? Color.FromArgb(121, 31, 31) : TextPrimary,
                Location = new Point(18, 10),
                Width = alertPanel.Width - 28,
                Height = 20
            };
            Label body = new Label
            {
                Text = alert.Body ?? string.Empty,
                Font = new Font("Segoe UI", 12f, FontStyle.Regular),
                ForeColor = TextSecondary,
                Location = new Point(18, 33),
                Width = alertPanel.Width - 28,
                Height = 30
            };
            alertPanel.Controls.Add(title);
            alertPanel.Controls.Add(body);
            return alertPanel;
        }

        private Control CreateMetricSection(string pageKey, List<DashboardAnalysisMetric> metrics)
        {
            ResizableCard wrapper = CreateCardContainer(pageKey, "section_key_metrics", "Key metrics", string.Empty, 192);
            TableLayoutPanel grid = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                BackColor = White,
                ColumnCount = Math.Max(1, metrics.Count),
                RowCount = 1,
                Padding = new Padding(20, 20, 20, 20)
            };
            for (int i = 0; i < metrics.Count; i++)
                grid.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100f / metrics.Count));

            for (int i = 0; i < metrics.Count; i++)
                grid.Controls.Add(CreateMetricCard(metrics[i]), i, 0);

            wrapper.ContentPanel.Controls.Add(grid);
            grid.BringToFront();
            return wrapper;
        }

        private Control CreateAlgorithmSection(string pageKey, DashboardAnalysisModel model)
        {
            int rows = (int)Math.Ceiling(model.AlgorithmFactors.Count / 3d);
            ResizableCard wrapper = CreateCardContainer(
                pageKey,
                "section_algorithm",
                string.IsNullOrWhiteSpace(model.AlgorithmTitle) ? "Algorithm score" : model.AlgorithmTitle,
                string.IsNullOrWhiteSpace(model.AlgorithmSummary) ? "Score built from live dashboard factors" : model.AlgorithmSummary,
                228 + (rows * 110));

            Panel scorePanel = new Panel
            {
                Dock = DockStyle.Top,
                Height = 136,
                BackColor = White,
                Padding = new Padding(20, 18, 20, 8)
            };

            Label scoreLabel = new Label
            {
                Text = model.AlgorithmScore.ToString("N0"),
                Font = new Font("Segoe UI", 32f, FontStyle.Regular),
                ForeColor = ResolveScoreColor(model.AlgorithmScore),
                AutoSize = true,
                Location = new Point(20, 8)
            };
            Label scoreOutOf = new Label
            {
                Text = "/100",
                Font = new Font("Segoe UI", 15f, FontStyle.Regular),
                ForeColor = TextHint,
                AutoSize = true,
                Location = new Point(scoreLabel.Right + 4, 24)
            };
            Label status = new Label
            {
                Text = model.AlgorithmStatusText ?? ResolveScoreStatus(model.AlgorithmScore),
                Font = new Font("Segoe UI", 12f, FontStyle.Bold),
                ForeColor = model.AlgorithmStatusColor == default(Color) ? ResolveScoreStatusColor(model.AlgorithmScore) : model.AlgorithmStatusColor,
                AutoSize = true,
                Location = new Point(20, 58)
            };

            Panel barTrack = new Panel { BackColor = ScoreTrack, Width = Math.Max(260, wrapper.Width - 80), Height = 8, Location = new Point(20, 92) };
            Panel barFill = new Panel { BackColor = ResolveScoreColor(model.AlgorithmScore), Height = 8, Width = (int)Math.Round(barTrack.Width * (Math.Max(0, Math.Min(100, model.AlgorithmScore)) / 100d)) };
            ApplyRoundedControl(barTrack, 4);
            ApplyRoundedControl(barFill, 4);
            barTrack.Controls.Add(barFill);

            scorePanel.Controls.Add(scoreLabel);
            scorePanel.Controls.Add(scoreOutOf);
            scorePanel.Controls.Add(status);
            scorePanel.Controls.Add(barTrack);
            wrapper.ContentPanel.Controls.Add(scorePanel);

            if (model.AlgorithmFactors.Any())
            {
                TableLayoutPanel factorsGrid = new TableLayoutPanel
                {
                    Dock = DockStyle.Fill,
                    BackColor = White,
                    ColumnCount = 3,
                    RowCount = rows,
                    Padding = new Padding(20, 0, 20, 20)
                };
                for (int i = 0; i < 3; i++)
                    factorsGrid.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 33.3333f));
                for (int i = 0; i < rows; i++)
                    factorsGrid.RowStyles.Add(new RowStyle(SizeType.Absolute, 100));

                for (int i = 0; i < model.AlgorithmFactors.Count; i++)
                    factorsGrid.Controls.Add(CreateFactorCard(model.AlgorithmFactors[i]), i % 3, i / 3);

                wrapper.ContentPanel.Controls.Add(factorsGrid);
                factorsGrid.BringToFront();
            }

            scorePanel.BringToFront();
            return wrapper;
        }

        private Control CreateChartCard(string pageKey, DashboardAnalysisChart chartModel, int chartIndex)
        {
            ResizableCard wrapper = CreateCardContainer(pageKey, BuildCardKey("chart", chartIndex, chartModel.Title), chartModel.Title ?? "Chart", chartModel.Subtitle ?? string.Empty, 338);
            Panel content = new Panel
            {
                Dock = DockStyle.Fill,
                BackColor = White,
                Padding = new Padding(12, 6, 28, 18)
            };

            Chart chart = new Chart
            {
                Dock = DockStyle.Fill,
                Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right | AnchorStyles.Bottom,
                BackColor = White,
                Palette = ChartColorPalette.None,
                BorderlineDashStyle = ChartDashStyle.NotSet,
                BorderlineColor = Color.Transparent
            };

            ChartArea area = new ChartArea("Main")
            {
                BackColor = White,
                BorderColor = Color.Transparent
            };
            area.Position = new ElementPosition(4, 6, 88, 82);
            area.InnerPlotPosition = new ElementPosition(10, 6, 80, 82);
            area.AxisX.LineColor = Border;
            area.AxisY.LineColor = Border;
            area.AxisX.MajorGrid.LineColor = Color.FromArgb(240, 240, 240);
            area.AxisY.MajorGrid.LineColor = Color.FromArgb(240, 240, 240);
            area.AxisX.LabelStyle.ForeColor = TextSecondary;
            area.AxisY.LabelStyle.ForeColor = TextSecondary;
            area.AxisX.LabelStyle.Font = new Font(Font.FontFamily, 11f, FontStyle.Regular);
            area.AxisY.LabelStyle.Font = new Font(Font.FontFamily, 11f, FontStyle.Regular);
            area.AxisX.Interval = 1;
            area.AxisX.IsLabelAutoFit = false;

            if (chartModel.ChartType == SeriesChartType.Doughnut)
            {
                area.AxisX.Enabled = AxisEnabled.False;
                area.AxisY.Enabled = AxisEnabled.False;
            }
            else if (chartModel.ChartType == SeriesChartType.Bar)
            {
                area.AxisX.MajorGrid.Enabled = true;
                area.AxisY.MajorGrid.Enabled = false;
            }
            else
            {
                area.AxisX.MajorGrid.Enabled = false;
            }

            chart.ChartAreas.Add(area);
            chart.Legends.Add(new Legend
            {
                Docking = Docking.Bottom,
                BackColor = White,
                Font = new Font(Font.FontFamily, 11f, FontStyle.Regular),
                ForeColor = TextSecondary,
                Enabled = chartModel.ShowLegend || chartModel.ChartType == SeriesChartType.Doughnut
            });

            Series series = new Series("Series")
            {
                ChartType = chartModel.ChartType,
                IsValueShownAsLabel = true,
                Font = new Font(Font.FontFamily, 11f, FontStyle.Bold),
                LabelForeColor = chartModel.ChartType == SeriesChartType.Bar ? White : TextPrimary,
                BorderWidth = chartModel.ChartType == SeriesChartType.Doughnut ? 0 : 1,
                CustomProperties = chartModel.ChartType == SeriesChartType.Doughnut ? "PieLabelStyle=Disabled,DoughnutRadius=60" : string.Empty
            };

            IEnumerable<DashboardAnalysisChartPoint> points = chartModel.Points;
            if (chartModel.ChartType == SeriesChartType.Bar)
                points = chartModel.Points.OrderBy(point => point.Value).ToList();

            foreach (DashboardAnalysisChartPoint point in points)
            {
                int index = series.Points.AddXY(point.Label, point.Value);
                DataPoint dataPoint = series.Points[index];
                dataPoint.Color = point.Color;
                dataPoint.LegendText = point.Label;
                dataPoint.AxisLabel = point.Label;
                dataPoint.Label = FormatChartValue(point.Value, chartModel.ValueFormat);
                if (chartModel.ChartType == SeriesChartType.Bar)
                    dataPoint["BarLabelStyle"] = "Center";
            }

            chart.Series.Add(series);
            content.Controls.Add(chart);

            if (chartModel.ChartType == SeriesChartType.Doughnut)
            {
                Label center = new Label
                {
                    Text = chartModel.CenterText ?? string.Empty,
                    Font = new Font("Segoe UI", 18f, FontStyle.Regular),
                    ForeColor = TextPrimary,
                    AutoSize = false,
                    Width = 140,
                    Height = 32,
                    TextAlign = ContentAlignment.MiddleCenter
                };
                Label sub = new Label
                {
                    Text = chartModel.CenterSubtext ?? string.Empty,
                    Font = new Font("Segoe UI", 11f, FontStyle.Regular),
                    ForeColor = TextHint,
                    AutoSize = false,
                    Width = 140,
                    Height = 18,
                    TextAlign = ContentAlignment.MiddleCenter
                };
                content.Resize += (s, e) =>
                {
                    center.Location = new Point((content.Width - center.Width) / 2, (content.Height - center.Height) / 2 - 14);
                    sub.Location = new Point((content.Width - sub.Width) / 2, center.Bottom - 4);
                };
                content.Controls.Add(center);
                content.Controls.Add(sub);
                center.BringToFront();
                sub.BringToFront();
            }

            wrapper.ContentPanel.Controls.Add(content);
            content.BringToFront();
            return wrapper;
        }

        private Control CreateFactSection(string pageKey, string title, string subtitle, List<DashboardAnalysisFact> facts, int columns, string suffix)
        {
            int rows = (int)Math.Ceiling(facts.Count / (double)Math.Max(1, columns));
            ResizableCard wrapper = CreateCardContainer(pageKey, suffix, title, subtitle, 86 + (rows * 86));
            TableLayoutPanel grid = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                BackColor = White,
                ColumnCount = columns,
                RowCount = rows,
                Padding = new Padding(20, 18, 20, 18)
            };
            for (int i = 0; i < columns; i++)
                grid.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100f / columns));
            for (int i = 0; i < rows; i++)
                grid.RowStyles.Add(new RowStyle(SizeType.Absolute, 74));

            for (int i = 0; i < facts.Count; i++)
                grid.Controls.Add(CreateFactCard(facts[i]), i % columns, i / columns);

            wrapper.ContentPanel.Controls.Add(grid);
            grid.BringToFront();
            return wrapper;
        }

        private Control CreateInsightCardSection(string pageKey, string title, string subtitle, List<DashboardAnalysisInsight> insights)
        {
            int rows = (int)Math.Ceiling(insights.Count / 2d);
            ResizableCard wrapper = CreateCardContainer(pageKey, "section_insights", title, subtitle, 86 + (rows * 90));
            TableLayoutPanel grid = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                BackColor = White,
                ColumnCount = 2,
                RowCount = rows,
                Padding = new Padding(20, 18, 20, 18)
            };
            grid.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50f));
            grid.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50f));
            for (int i = 0; i < rows; i++)
                grid.RowStyles.Add(new RowStyle(SizeType.Absolute, 78));

            for (int i = 0; i < insights.Count; i++)
                grid.Controls.Add(CreateInsightCard(insights[i]), i % 2, i / 2);

            wrapper.ContentPanel.Controls.Add(grid);
            grid.BringToFront();
            return wrapper;
        }

        private Control CreateLegacyInsightSection(string pageKey, List<string> insights)
        {
            List<DashboardAnalysisInsight> cards = insights
                .Where(text => !string.IsNullOrWhiteSpace(text))
                .Select(text => new DashboardAnalysisInsight { Title = "Insight", Detail = text, Accent = Blue })
                .ToList();
            return CreateInsightCardSection(pageKey, "Insights", string.Empty, cards);
        }

        private Control CreateTableSection(string pageKey, DashboardAnalysisTable table)
        {
            ResizableCard wrapper = CreateCardContainer(pageKey, BuildCardKey("table", 0, table.Title), table.Title ?? "Table", table.Subtitle ?? string.Empty, 350);
            DataGridView grid = new DataGridView
            {
                Dock = DockStyle.Fill,
                AllowUserToAddRows = false,
                AllowUserToDeleteRows = false,
                AllowUserToResizeRows = false,
                ReadOnly = true,
                RowHeadersVisible = false,
                BackgroundColor = White,
                BorderStyle = BorderStyle.None,
                GridColor = Color.FromArgb(240, 240, 240),
                SelectionMode = DataGridViewSelectionMode.FullRowSelect,
                EnableHeadersVisualStyles = false,
                CellBorderStyle = DataGridViewCellBorderStyle.SingleHorizontal,
                ColumnHeadersHeight = 34,
                AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill
            };
            DS.StyleGrid(grid);
            grid.DefaultCellStyle.Font = new Font("Segoe UI", 9f, FontStyle.Regular);
            grid.ColumnHeadersDefaultCellStyle.Font = new Font("Segoe UI", 8.5f, FontStyle.Bold);
            grid.DefaultCellStyle.SelectionBackColor = DS.Primary50;
            grid.DefaultCellStyle.SelectionForeColor = TextPrimary;
            grid.AlternatingRowsDefaultCellStyle.BackColor = TableAlt;
            grid.ColumnHeadersBorderStyle = DataGridViewHeaderBorderStyle.Single;
            grid.RowTemplate.Height = 32;

            foreach (string column in table.Columns)
                grid.Columns.Add(new DataGridViewTextBoxColumn { HeaderText = column, SortMode = DataGridViewColumnSortMode.NotSortable });
            foreach (string[] row in table.Rows)
                grid.Rows.Add(row);

            grid.ClearSelection();
            try { grid.CurrentCell = null; } catch { }

            if (string.Equals(table.StyleKey, "JobsTracker", StringComparison.OrdinalIgnoreCase))
                ApplyJobsTrackerStyle(grid);

            wrapper.ContentPanel.Controls.Add(grid);
            grid.BringToFront();
            return wrapper;
        }

        private Control CreateTimelineSection(string pageKey, List<DashboardAnalysisTimelineItem> items)
        {
            ResizableCard wrapper = CreateCardContainer(pageKey, "section_activity", "Activity", "Recent related events", 90 + (items.Count * 48));
            FlowLayoutPanel flow = new FlowLayoutPanel
            {
                Dock = DockStyle.Fill,
                BackColor = White,
                FlowDirection = FlowDirection.TopDown,
                WrapContents = false,
                Padding = new Padding(20, 18, 20, 18)
            };

            foreach (DashboardAnalysisTimelineItem item in items)
            {
                Panel row = new Panel
                {
                    Width = MaxContentWidth - 80,
                    Height = 36,
                    BackColor = White,
                    Margin = new Padding(0, 0, 0, 8)
                };
                row.Paint += (s, e) =>
                {
                    using (Pen pen = new Pen(Border, 0.5f))
                        e.Graphics.DrawRectangle(pen, 0, 0, row.Width - 1, row.Height - 1);
                };

                Panel dot = new Panel { BackColor = item.Accent, Width = 8, Height = 8, Location = new Point(14, 14) };
                dot.Paint += (s, e) =>
                {
                    using (SolidBrush brush = new SolidBrush(item.Accent))
                        e.Graphics.FillEllipse(brush, 0, 0, dot.Width, dot.Height);
                };

                row.Controls.Add(dot);
                row.Controls.Add(new Label { Text = item.Title ?? string.Empty, Font = new Font("Segoe UI", 12f, FontStyle.Bold), ForeColor = TextPrimary, Location = new Point(30, 8), Width = 320, Height = 18 });
                row.Controls.Add(new Label { Text = item.Detail ?? string.Empty, Font = new Font("Segoe UI", 11f, FontStyle.Regular), ForeColor = TextSecondary, Location = new Point(260, 8), Width = 620, Height = 18 });
                row.Controls.Add(new Label { Text = item.TimeText ?? string.Empty, Font = new Font("Segoe UI", 11f, FontStyle.Regular), ForeColor = TextHint, TextAlign = ContentAlignment.MiddleRight, Location = new Point(row.Width - 180, 8), Width = 156, Height = 18, Anchor = AnchorStyles.Top | AnchorStyles.Right });
                flow.Controls.Add(row);
            }

            wrapper.ContentPanel.Controls.Add(flow);
            flow.BringToFront();
            return wrapper;
        }

        private Control CreateActionsSection(List<DashboardAnalysisAction> actions)
        {
            Panel wrapper = new Panel { Width = MaxContentWidth, Height = 60, BackColor = White, Margin = new Padding(0, 0, 0, SectionGap) };
            FlowLayoutPanel flow = new FlowLayoutPanel { Dock = DockStyle.Fill, BackColor = White, FlowDirection = FlowDirection.LeftToRight, WrapContents = true, Padding = new Padding(0) };
            foreach (DashboardAnalysisAction action in actions)
                flow.Controls.Add(CreateActionButton(action));
            wrapper.Controls.Add(flow);
            return wrapper;
        }

        private Control CreateTwoColumnRow(Control left, Control right)
        {
            TableLayoutPanel row = new TableLayoutPanel { Width = MaxContentWidth, Height = Math.Max(left.Height, right.Height), BackColor = White, ColumnCount = 2, RowCount = 1, Margin = new Padding(0, 0, 0, SectionGap) };
            row.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50f));
            row.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50f));
            left.Margin = new Padding(0, 0, 6, 0);
            right.Margin = new Padding(6, 0, 0, 0);
            left.Dock = DockStyle.Fill;
            right.Dock = DockStyle.Fill;
            row.Controls.Add(left, 0, 0);
            row.Controls.Add(right, 1, 0);
            return row;
        }

        private ResizableCard CreateCardContainer(string pageKey, string cardKey, string title, string subtitle, int height)
        {
            var wrapper = new ResizableCard
            {
                Width = MaxContentWidth,
                Height = height,
                BackColor = White,
                Margin = new Padding(0, 0, 0, SectionGap),
                PageKey = pageKey,
                CardKey = pageKey + "_" + cardKey,
                CardTitle = title ?? string.Empty,
                BorderColor = Border,
                BackgroundColor = White,
                ShowHeader = true,
                AllowResize = true,
                SizePreset = "Medium"
            };
            wrapper.ContentPanel.Padding = new Padding(20, 14, 20, 20);

            if (!string.IsNullOrWhiteSpace(subtitle))
            {
                wrapper.ContentPanel.Controls.Add(new Label
                {
                    Text = subtitle,
                    Font = new Font("Segoe UI", 12f, FontStyle.Regular),
                    ForeColor = TextSecondary,
                    AutoSize = false,
                    Dock = DockStyle.Top,
                    Height = 26
                });
            }

            CardLayoutService.RegisterDefaultSize(wrapper.PageKey, wrapper.CardKey, wrapper.Size, wrapper.SizePreset);
            return wrapper;
        }

        private Control CreateMetricCard(DashboardAnalysisMetric metric)
        {
            Panel card = new Panel { Dock = DockStyle.Fill, BackColor = MetricBg, Margin = new Padding(0, 0, 12, 0), Padding = new Padding(16, 14, 16, 14), Height = 132 };
            ApplyRoundedControl(card, MetricRadius);
            card.Controls.Add(new Label { Text = (metric.Label ?? string.Empty).ToUpperInvariant(), Font = new Font("Segoe UI", 11f, FontStyle.Regular), ForeColor = TextSecondary, AutoSize = false, Width = 320, Height = 22, Location = new Point(16, 14) });
            card.Controls.Add(new Label { Text = metric.Value ?? "-", Font = new Font("Segoe UI", 22f, FontStyle.Regular), ForeColor = metric.Accent, AutoSize = false, Width = 320, Height = 40, Location = new Point(16, 44) });
            card.Controls.Add(new Label { Text = metric.Subtitle ?? string.Empty, Font = new Font("Segoe UI", 11f, FontStyle.Regular), ForeColor = TextHint, AutoSize = false, Width = 320, Height = 24, Location = new Point(16, 88) });
            return card;
        }

        private Control CreateFactorCard(DashboardAnalysisAlgorithmFactor factor)
        {
            Panel card = new Panel { Dock = DockStyle.Fill, BackColor = MetricBg, Margin = new Padding(0, 0, 12, 12), Padding = new Padding(14, 12, 14, 12), Height = 92 };
            card.Paint += (s, e) =>
            {
                using (SolidBrush brush = new SolidBrush(factor.Accent))
                    e.Graphics.FillRectangle(brush, 0, 0, 3, card.Height);
            };
            card.Controls.Add(new Label { Text = factor.Label ?? string.Empty, Font = new Font("Segoe UI", 12f, FontStyle.Bold), ForeColor = TextPrimary, Location = new Point(14, 10), Width = 220, Height = 22 });
            card.Controls.Add(new Label { Text = factor.Value ?? factor.Score.ToString("N0"), Font = new Font("Segoe UI", 16f, FontStyle.Regular), ForeColor = factor.Accent, TextAlign = ContentAlignment.TopRight, Location = new Point(180, 10), Width = 150, Height = 24 });
            card.Controls.Add(new Label { Text = factor.Detail ?? string.Empty, Font = new Font("Segoe UI", 11f, FontStyle.Regular), ForeColor = TextSecondary, Location = new Point(14, 38), Width = 320, Height = 42 });
            return card;
        }

        private Control CreateFactCard(DashboardAnalysisFact fact)
        {
            Panel card = new Panel { Dock = DockStyle.Fill, BackColor = White, Margin = new Padding(0, 0, 12, 12) };
            card.Paint += (s, e) =>
            {
                using (Pen pen = new Pen(Border, 0.5f))
                    e.Graphics.DrawRectangle(pen, 0, 0, card.Width - 1, card.Height - 1);
            };
            ApplyRoundedControl(card, CardRadius);
            card.Controls.Add(new Label { Text = fact.Label ?? string.Empty, Font = new Font("Segoe UI", 11f, FontStyle.Regular), ForeColor = TextSecondary, Location = new Point(16, 12), Width = 220, Height = 16 });
            card.Controls.Add(new Label { Text = fact.Value ?? string.Empty, Font = new Font("Segoe UI", 13f, FontStyle.Regular), ForeColor = TextPrimary, Location = new Point(16, 34), Width = 300, Height = 22 });
            return card;
        }

        private Control CreateInsightCard(DashboardAnalysisInsight insight)
        {
            Panel card = new Panel { Dock = DockStyle.Fill, BackColor = White, Margin = new Padding(0, 0, 12, 12) };
            card.Paint += (s, e) =>
            {
                using (SolidBrush brush = new SolidBrush(insight.Accent))
                    e.Graphics.FillRectangle(brush, 0, 0, 3, card.Height);
            };
            card.Controls.Add(new Label { Text = insight.Title ?? string.Empty, Font = new Font("Segoe UI", 12f, FontStyle.Bold), ForeColor = TextPrimary, Location = new Point(14, 10), Width = 250, Height = 18 });
            card.Controls.Add(new Label { Text = insight.Detail ?? string.Empty, Font = new Font("Segoe UI", 11f, FontStyle.Regular), ForeColor = TextSecondary, Location = new Point(14, 32), Width = 330, Height = 34 });
            return card;
        }

        private Button CreateActionButton(DashboardAnalysisAction action)
        {
            Button button = new Button { Text = action.Label ?? "Action", Height = 38, AutoSize = true, AutoSizeMode = AutoSizeMode.GrowAndShrink, FlatStyle = FlatStyle.Flat, Font = new Font("Segoe UI", 12f, FontStyle.Regular), Padding = new Padding(18, 8, 18, 8), Margin = new Padding(0, 0, 10, 0) };
            Color backColor;
            Color foreColor;
            Color borderColor;
            ResolveActionColors(action, out backColor, out foreColor, out borderColor);
            button.BackColor = backColor;
            button.ForeColor = foreColor;
            button.FlatAppearance.BorderSize = borderColor == Color.Transparent ? 0 : 1;
            if (button.FlatAppearance.BorderSize > 0)
                button.FlatAppearance.BorderColor = borderColor;
            button.FlatAppearance.MouseDownBackColor = backColor;
            button.FlatAppearance.MouseOverBackColor = backColor;
            ApplyRoundedControl(button, 8);
            button.Click += (s, e) => action.Handler?.Invoke();
            return button;
        }

        private void ApplyJobsTrackerStyle(DataGridView grid)
        {
            grid.CellFormatting += (s, e) =>
            {
                if (e.RowIndex < 0 || e.ColumnIndex < 0)
                    return;

                string header = grid.Columns[e.ColumnIndex].HeaderText;
                string text = Convert.ToString(e.Value);

                if (string.Equals(header, "Status", StringComparison.OrdinalIgnoreCase))
                {
                    if (string.Equals(text, "In Progress", StringComparison.OrdinalIgnoreCase))
                    {
                        e.CellStyle.BackColor = TealBadgeBg;
                        e.CellStyle.ForeColor = TealBadgeText;
                    }
                    else if (string.Equals(text, "Pending", StringComparison.OrdinalIgnoreCase))
                    {
                        e.CellStyle.BackColor = AmberBadgeBg;
                        e.CellStyle.ForeColor = AmberBadgeText;
                    }
                    else if (string.Equals(text, "Completed", StringComparison.OrdinalIgnoreCase))
                    {
                        e.CellStyle.BackColor = Color.FromArgb(234, 243, 222);
                        e.CellStyle.ForeColor = Color.FromArgb(39, 80, 10);
                    }
                    e.CellStyle.SelectionBackColor = e.CellStyle.BackColor;
                    e.CellStyle.SelectionForeColor = e.CellStyle.ForeColor;
                }

                if (string.Equals(header, "Est margin", StringComparison.OrdinalIgnoreCase))
                {
                    decimal margin;
                    if (decimal.TryParse((text ?? string.Empty).Replace("%", string.Empty), out margin))
                        e.CellStyle.ForeColor = margin >= 25m ? Green : margin >= 15m ? Amber : Red;
                }
            };
        }

        private void ApplyBadgeStyle(Label label, DashboardAnalysisModel model)
        {
            DashboardAnalysisBadgeVariant variant = model.BadgeVariant;
            if (string.IsNullOrWhiteSpace(model.BadgeText) && !string.IsNullOrWhiteSpace(model.StatusText))
            {
                if (model.StatusColor == Red)
                    variant = DashboardAnalysisBadgeVariant.Red;
                else if (model.StatusColor == Amber)
                    variant = DashboardAnalysisBadgeVariant.Amber;
                else if (model.StatusColor == Blue)
                    variant = DashboardAnalysisBadgeVariant.Blue;
                else
                    variant = DashboardAnalysisBadgeVariant.Teal;
            }

            switch (variant)
            {
                case DashboardAnalysisBadgeVariant.Red:
                    label.BackColor = RedBadgeBg;
                    label.ForeColor = RedDark;
                    break;
                case DashboardAnalysisBadgeVariant.Amber:
                    label.BackColor = AmberBadgeBg;
                    label.ForeColor = AmberBadgeText;
                    break;
                case DashboardAnalysisBadgeVariant.Blue:
                    label.BackColor = BlueBadgeBg;
                    label.ForeColor = BlueBadgeText;
                    break;
                case DashboardAnalysisBadgeVariant.Green:
                    label.BackColor = GreenLight;
                    label.ForeColor = Green;
                    break;
                default:
                    label.BackColor = TealBadgeBg;
                    label.ForeColor = TealBadgeText;
                    break;
            }
            ApplyRoundedControl(label, 12);
        }

        private static string ResolvePageKey(DashboardAnalysisModel model)
        {
            if (!string.IsNullOrWhiteSpace(model.PageKey))
                return model.PageKey;

            switch (model.EntityType)
            {
                case DashboardAnalysisEntityType.Quotation: return "QuotationAnalysis";
                case DashboardAnalysisEntityType.Invoice: return "InvoiceAnalysis";
                case DashboardAnalysisEntityType.Job: return "JobAnalysis";
                case DashboardAnalysisEntityType.Inventory: return "InventoryAnalysis";
                case DashboardAnalysisEntityType.Purchase: return "PurchaseAnalysis";
                default: return "DashboardAnalysis";
            }
        }

        private static string BuildCardKey(string prefix, int index, string title)
        {
            string normalized = new string((title ?? string.Empty).ToLowerInvariant().Select(ch => char.IsLetterOrDigit(ch) ? ch : '_').ToArray());
            while (normalized.Contains("__"))
                normalized = normalized.Replace("__", "_");
            normalized = normalized.Trim('_');
            if (string.IsNullOrWhiteSpace(normalized))
                normalized = prefix + "_" + index;
            return prefix + "_" + index + "_" + normalized;
        }

        private static DashboardAnalysisDateRange MapRange(int index)
        {
            switch (index)
            {
                case 1: return DashboardAnalysisDateRange.Last30Days;
                case 2: return DashboardAnalysisDateRange.QuarterToDate;
                case 3: return DashboardAnalysisDateRange.FinancialYear;
                case 4: return DashboardAnalysisDateRange.AllTime;
                default: return DashboardAnalysisDateRange.ThisMonth;
            }
        }

        private static int MapRange(DashboardAnalysisDateRange range)
        {
            switch (range)
            {
                case DashboardAnalysisDateRange.Last30Days: return 1;
                case DashboardAnalysisDateRange.QuarterToDate: return 2;
                case DashboardAnalysisDateRange.FinancialYear: return 3;
                case DashboardAnalysisDateRange.AllTime: return 4;
                default: return 0;
            }
        }

        private static Color ResolveAlertBackColor(DashboardAnalysisAlertVariant variant)
        {
            switch (variant)
            {
                case DashboardAnalysisAlertVariant.Red: return RedLight;
                case DashboardAnalysisAlertVariant.Amber: return AmberLight;
                case DashboardAnalysisAlertVariant.Green: return GreenLight;
                default: return White;
            }
        }

        private static Color ResolveAlertAccent(DashboardAnalysisAlertVariant variant)
        {
            switch (variant)
            {
                case DashboardAnalysisAlertVariant.Red: return Red;
                case DashboardAnalysisAlertVariant.Amber: return Amber;
                case DashboardAnalysisAlertVariant.Green: return Teal;
                default: return Blue;
            }
        }

        private static string ResolveScoreStatus(int score)
        {
            if (score < 40)
                return "Needs attention";
            if (score <= 70)
                return "Moderate";
            return "Healthy";
        }

        private static Color ResolveScoreStatusColor(int score)
        {
            if (score < 40)
                return RedDark;
            if (score <= 70)
                return Color.FromArgb(133, 79, 11);
            return Teal;
        }

        private static Color ResolveScoreColor(int score)
        {
            if (score < 30)
                return Teal;
            if (score <= 80)
                return Amber;
            return Red;
        }

        private static string FormatChartValue(decimal value, DashboardAnalysisValueFormat format)
        {
            switch (format)
            {
                case DashboardAnalysisValueFormat.Currency:
                    return FormatIndianCurrency(value);
                case DashboardAnalysisValueFormat.Percent:
                    return value.ToString("N1") + "%";
                case DashboardAnalysisValueFormat.Count:
                    return value.ToString("N0");
                default:
                    return value.ToString("N0");
            }
        }

        private static void ResolveActionColors(DashboardAnalysisAction action, out Color backColor, out Color foreColor, out Color borderColor)
        {
            switch (action.Style)
            {
                case DashboardAnalysisActionStyle.Primary:
                case DashboardAnalysisActionStyle.Teal:
                    backColor = Teal;
                    foreColor = White;
                    borderColor = Color.Transparent;
                    return;
                case DashboardAnalysisActionStyle.Destructive:
                    backColor = Red;
                    foreColor = White;
                    borderColor = Color.Transparent;
                    return;
                case DashboardAnalysisActionStyle.Amber:
                    backColor = Amber;
                    foreColor = Color.FromArgb(65, 36, 2);
                    borderColor = Color.Transparent;
                    return;
                case DashboardAnalysisActionStyle.Secondary:
                    backColor = White;
                    foreColor = TextPrimary;
                    borderColor = Border;
                    return;
                default:
                    backColor = action.BackColor;
                    foreColor = action.ForeColor;
                    borderColor = action.BackColor == White ? Border : Color.Transparent;
                    return;
            }
        }

        private static string FormatIndianCurrency(decimal amount)
        {
            decimal absolute = Math.Abs(amount);
            long whole = (long)Math.Floor(absolute);
            int decimals = (int)Math.Round((absolute - whole) * 100m, MidpointRounding.AwayFromZero);
            string digits = whole.ToString();
            string formatted;

            if (digits.Length <= 3)
            {
                formatted = digits;
            }
            else
            {
                string lastThree = digits.Substring(digits.Length - 3);
                string prefix = digits.Substring(0, digits.Length - 3);
                List<string> groups = new List<string>();
                while (prefix.Length > 2)
                {
                    groups.Insert(0, prefix.Substring(prefix.Length - 2));
                    prefix = prefix.Substring(0, prefix.Length - 2);
                }
                if (prefix.Length > 0)
                    groups.Insert(0, prefix);
                formatted = string.Join(",", groups) + "," + lastThree;
            }

            string sign = amount < 0 ? "-" : string.Empty;
            return sign + "\u20b9" + formatted + "." + decimals.ToString("00");
        }

        private static void ApplyRoundedControl(Control control, int radius)
        {
            if (control == null)
                return;

            void update(object sender, EventArgs args)
            {
                if (control.Width <= 0 || control.Height <= 0)
                    return;

                using (GraphicsPath path = CreateRoundedRectPath(new Rectangle(0, 0, control.Width, control.Height), Math.Max(2, radius)))
                    control.Region = new Region(path);
            }

            control.Resize += update;
            update(control, EventArgs.Empty);
        }

        private static GraphicsPath CreateRoundedRectPath(Rectangle bounds, int radius)
        {
            int diameter = radius * 2;
            GraphicsPath path = new GraphicsPath();
            Rectangle arc = new Rectangle(bounds.Location, new Size(diameter, diameter));

            path.AddArc(arc, 180, 90);
            arc.X = bounds.Right - diameter;
            path.AddArc(arc, 270, 90);
            arc.Y = bounds.Bottom - diameter;
            path.AddArc(arc, 0, 90);
            arc.X = bounds.Left;
            path.AddArc(arc, 90, 90);
            path.CloseFigure();
            return path;
        }
    }
}
