using System.Drawing;

namespace HVAC_Pro_Desktop.UI
{
	public class AttendanceForm : HVAC_Pro_Desktop.UI.DeferredPageControl
	{
		private readonly HVAC_Pro_Desktop.Services.AttendanceService _attendanceService = new HVAC_Pro_Desktop.Services.AttendanceService();

		private readonly HVAC_Pro_Desktop.Services.EmployeeService _employeeService = new HVAC_Pro_Desktop.Services.EmployeeService();

		private readonly HVAC_Pro_Desktop.Services.PayrollDataImportService _importService = new HVAC_Pro_Desktop.Services.PayrollDataImportService();

		private System.Windows.Forms.ComboBox _cmbMonth;

		private System.Windows.Forms.ComboBox _cmbYear;

		private System.Windows.Forms.ComboBox _cmbSiteFilter;

		private System.Windows.Forms.Label _lblStatus;

		private System.Windows.Forms.Label _lblSummary;

		private System.Windows.Forms.Label _lblCompleteCount;

		private System.Windows.Forms.Label _lblReviewCount;

		private System.Windows.Forms.Label _lblCoverage;

		private System.Windows.Forms.Label _lblActiveEmployees;

		private System.Windows.Forms.Label _lblCoverageInsight;

		private System.Windows.Forms.Label _lblReviewInsight;

		private System.Windows.Forms.Label _lblMonthChip;

		private System.Windows.Forms.Label _lblPeriodStatus;

		private System.Windows.Forms.Label _lblRightHeader;

		private System.Windows.Forms.Label _lblMonthSignalSummary;

		private System.Windows.Forms.Label _lblMonthSignalDetail;

		private System.Windows.Forms.Label _lblPayrollTitle;

		private System.Windows.Forms.Label _lblPayrollBody;

		private System.Windows.Forms.Button _btnImport;

		private System.Windows.Forms.DataGridView _gridAttendance;

		private System.Windows.Forms.FlowLayoutPanel _reviewQueueBody;

		private System.Windows.Forms.Label _lblGridLegend;

		private System.Windows.Forms.Panel _dailyGridShell;

		private System.Windows.Forms.Panel _dailyGridFrame;

		private System.Windows.Forms.Panel _dailyGridTopGrip;

		private System.Windows.Forms.Panel _dailyGridBottomGrip;

		private System.Windows.Forms.Panel _dailyGridLeftGrip;

		private System.Windows.Forms.Panel _dailyGridRightGrip;

		private System.Windows.Forms.Panel _dailyGridTopLeftGrip;

		private System.Windows.Forms.Panel _dailyGridTopRightGrip;

		private System.Windows.Forms.Panel _dailyGridBottomLeftGrip;

		private System.Windows.Forms.Panel _dailyGridBottomRightGrip;

		private bool _dailyGridResizing;

		private bool _dailyGridUserResized;

		private Point _dailyGridResizeStartMouse;

		private Size _dailyGridResizeStartSize;

		private Point _dailyGridResizeStartLocation;

		private System.Collections.Generic.List<HVAC_Pro_Desktop.Models.Employee> _employees = new System.Collections.Generic.List<HVAC_Pro_Desktop.Models.Employee>();

		private bool _isInitializing;

		protected override bool EnableAutomaticLayoutScaling => false;

		protected override bool EnableMainScrollCanvas => false;

		protected override bool SuppressAutomaticChildPolish => true;

		private int CurrentMonth
		{
			get
			{
				if (_cmbMonth != null)
				{
					return _cmbMonth.SelectedIndex + 1;
				}
				return System.DateTime.Today.Month;
			}
		}

		private int CurrentYear
		{
			get
			{
				if (_cmbYear == null || !int.TryParse(System.Convert.ToString(_cmbYear.SelectedItem), out var result))
				{
					return System.DateTime.Today.Year;
				}
				return result;
			}
		}

		private string CurrentMonthName => new System.DateTime(CurrentYear, CurrentMonth, 1).ToString("MMMM");

		public AttendanceForm()
		{
			Dock = System.Windows.Forms.DockStyle.Fill;
			BackColor = HVAC_Pro_Desktop.UI.DS.BgPage;
			BuildLayout();
			HVAC_Pro_Desktop.UI.UIHelper.ApplyInputStyles(base.Controls);
			EnableDeferredLoad(delegate
			{
				LoadEmployees();
				RefreshAttendanceWorkspace();
			}, delegate(System.Exception ex)
			{
				SetStatus("Attendance load error: " + ex.Message, System.Drawing.Color.Firebrick);
			});
		}

		private void BuildLayout()
		{
			_isInitializing = true;
			base.Controls.Clear();
			System.Windows.Forms.Button button = NewButton("Mark All Present", System.Drawing.Point.Empty, 132, HVAC_Pro_Desktop.UI.DS.Primary600);
			_btnImport = NewButton("Import Excel / CSV", System.Drawing.Point.Empty, 154, System.Drawing.Color.White);
			_btnImport.ForeColor = HVAC_Pro_Desktop.UI.DS.Slate700;
			_btnImport.FlatAppearance.BorderSize = 1;
			_btnImport.FlatAppearance.BorderColor = HVAC_Pro_Desktop.UI.DS.BorderStrong;
			System.Windows.Forms.Button button2 = NewButton("Save Attendance", System.Drawing.Point.Empty, 136, System.Drawing.Color.FromArgb(39, 174, 96));
			System.Windows.Forms.Button button3 = NewButton("Open Payroll", System.Drawing.Point.Empty, 118, System.Drawing.Color.White);
			button3.ForeColor = HVAC_Pro_Desktop.UI.DS.Slate700;
			button3.FlatAppearance.BorderSize = 1;
			button3.FlatAppearance.BorderColor = HVAC_Pro_Desktop.UI.DS.BorderStrong;
			button.Click += delegate
			{
				MarkAllAttendancePresent();
			};
			_btnImport.Click += delegate
			{
				ImportAttendanceFiles();
			};
			button2.Click += delegate
			{
				SaveAttendanceGrid();
			};
			button3.Click += delegate
			{
				(FindForm() as HVAC_Pro_Desktop.UI.MainForm)?.NavigateTo("Payroll");
			};
			System.Windows.Forms.Button[] array = new System.Windows.Forms.Button[4] { button, _btnImport, button2, button3 };
			foreach (System.Windows.Forms.Button button4 in array)
			{
				button4.Margin = System.Windows.Forms.Padding.Empty;
				button4.Anchor = System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Right;
				button4.Tag = (((button4.Tag == null) ? string.Empty : (button4.Tag?.ToString() + " ")) + "FIXED_WIDTH").Trim();
			}
			System.Windows.Forms.Panel header = HVAC_Pro_Desktop.UI.SharedPageHeader.Build(new HVAC_Pro_Desktop.UI.SharedPageHeaderModel
			{
				Name = "AttendancePageHeader",
				Mode = HVAC_Pro_Desktop.UI.SharedPageHeaderMode.Editor,
				Dock = System.Windows.Forms.DockStyle.Top,
				BackColor = HVAC_Pro_Desktop.UI.DS.BgPage,
				Title = "Time & Attendance",
				Subtitle = "Workforce presence, month coverage, and payroll-ready review.",
				TitleWidth = 360,
				SubtitleWidth = 520,
				RightActions = System.Linq.Enumerable.ToList(System.Linq.Enumerable.Cast<System.Windows.Forms.Control>(array))
			}).Header;
			System.Windows.Forms.Panel panel = new System.Windows.Forms.Panel
			{
				Dock = System.Windows.Forms.DockStyle.Fill,
				BackColor = HVAC_Pro_Desktop.UI.DS.BgPage,
				Padding = new System.Windows.Forms.Padding(14, 0, 14, 14),
				AutoScroll = true
			};
			System.Windows.Forms.Panel panel2 = MakeCard();
			panel2.Dock = System.Windows.Forms.DockStyle.Fill;
			panel2.Padding = new System.Windows.Forms.Padding(14);
			System.Windows.Forms.Panel periodStrip = MakeCard();
			periodStrip.Dock = System.Windows.Forms.DockStyle.Top;
			periodStrip.Height = 62;
			periodStrip.Padding = new System.Windows.Forms.Padding(16, 8, 16, 8);
			periodStrip.Margin = new System.Windows.Forms.Padding(0, 0, 0, 12);
			System.Windows.Forms.Label label = new System.Windows.Forms.Label
			{
				Text = "PERIOD",
				Location = new System.Drawing.Point(0, 2),
				Size = new System.Drawing.Size(120, 16),
				Font = new System.Drawing.Font("Segoe UI", 8f, System.Drawing.FontStyle.Bold),
				ForeColor = HVAC_Pro_Desktop.UI.DS.Slate500
			};
			_cmbMonth = NewCombo(new System.Drawing.Point(0, 20), 78, System.Linq.Enumerable.ToArray(System.Linq.Enumerable.Select(System.Linq.Enumerable.Range(1, 12), (int i) => new System.DateTime(2000, i, 1).ToString("MMMM"))));
			_cmbMonth.SelectedIndex = System.DateTime.Today.Month - 1;
			_cmbYear = NewCombo(new System.Drawing.Point(88, 20), 72, System.Linq.Enumerable.ToArray(System.Linq.Enumerable.Select(System.Linq.Enumerable.Range(System.DateTime.Today.Year - 3, 7), (int y) => y.ToString())));
			_cmbYear.SelectedItem = System.DateTime.Today.Year.ToString();
			_lblMonthChip = HVAC_Pro_Desktop.UI.WorkforceModuleVisuals.CreateChip("Month open", System.Drawing.Color.FromArgb(227, 247, 236), HVAC_Pro_Desktop.UI.DS.Green600, 96);
			_lblMonthChip.Location = new System.Drawing.Point(168, 18);
			System.Windows.Forms.Label siteLabel = new System.Windows.Forms.Label
			{
				Text = "SITE",
				Location = new System.Drawing.Point(280, 2),
				Size = new System.Drawing.Size(120, 16),
				Font = new System.Drawing.Font("Segoe UI", 8f, System.Drawing.FontStyle.Bold),
				ForeColor = HVAC_Pro_Desktop.UI.DS.Slate500
			};
			_cmbSiteFilter = NewCombo(new System.Drawing.Point(280, 20), 190, new string[] { "All sites" });
			_cmbSiteFilter.SelectedIndex = 0;
			_lblStatus = new System.Windows.Forms.Label
			{
				Text = "Workspace ready for " + CurrentMonthName + " " + CurrentYear,
				Anchor = (System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Left | System.Windows.Forms.AnchorStyles.Right),
				Location = new System.Drawing.Point(490, 18),
				Size = new System.Drawing.Size(System.Math.Max(320, periodStrip.Width - 706), 24),
				Font = new System.Drawing.Font("Segoe UI", 9f, System.Drawing.FontStyle.Regular),
				ForeColor = HVAC_Pro_Desktop.UI.DS.Slate700,
				BackColor = System.Drawing.Color.Transparent,
				AutoEllipsis = true
			};
			_lblPeriodStatus = _lblStatus;
			periodStrip.Resize += delegate
			{
				_lblStatus.Width = System.Math.Max(260, periodStrip.ClientSize.Width - 706);
			};
			periodStrip.Controls.AddRange(new System.Windows.Forms.Control[7] { label, _cmbMonth, _cmbYear, _lblMonthChip, siteLabel, _cmbSiteFilter, _lblStatus });
			System.Windows.Forms.Panel panel3 = new System.Windows.Forms.Panel
			{
				BackColor = System.Drawing.Color.White,
				Padding = new System.Windows.Forms.Padding(0)
			};
			_gridAttendance = NewGrid();
			_gridAttendance.Tag = "CUSTOM_INPUT_SHELL";
			_gridAttendance.Dock = System.Windows.Forms.DockStyle.Fill;
			_gridAttendance.DataError += delegate(object s, System.Windows.Forms.DataGridViewDataErrorEventArgs e)
			{
				e.ThrowException = false;
			};
			panel3.Controls.Add(_gridAttendance);
			System.Windows.Forms.Panel control3 = HVAC_Pro_Desktop.UI.WorkforceModuleVisuals.CreateSectionCard("Daily coverage grid", "Each active employee across June. Click any cell to cycle its status.", panel3, BuildStatusLegend(), 560);
			System.Windows.Forms.Panel dailyGridShell = CreateResizableDailyCoverageShell(control3);
			panel2.Controls.Add(dailyGridShell);
			panel2.Controls.Add(periodStrip);
			panel.Controls.Add(panel2);
			base.Controls.Add(panel);
			base.Controls.Add(header);
			_isInitializing = false;
		}

		private void LoadEmployees()
		{
			_employees = System.Linq.Enumerable.ToList(System.Linq.Enumerable.OrderBy(System.Linq.Enumerable.Where(_employeeService.GetAll() ?? new System.Collections.Generic.List<HVAC_Pro_Desktop.Models.Employee>(), IsVisibleAttendanceEmployee), (HVAC_Pro_Desktop.Models.Employee e) => e.Name));
			PopulateSiteFilter();
		}

		private void PopulateSiteFilter()
		{
			if (_cmbSiteFilter == null)
			{
				return;
			}
			string selectedSite = System.Convert.ToString(_cmbSiteFilter.SelectedItem);
			_cmbSiteFilter.SelectedIndexChanged -= SiteFilterSelectedIndexChanged;
			try
			{
				_cmbSiteFilter.Items.Clear();
				_cmbSiteFilter.Items.Add("All sites");
				foreach (string site in System.Linq.Enumerable.OrderBy(System.Linq.Enumerable.Distinct(System.Linq.Enumerable.Where(System.Linq.Enumerable.Select(_employees, (HVAC_Pro_Desktop.Models.Employee employee) => (employee.ClientSite ?? string.Empty).Trim()), (string site) => !string.IsNullOrWhiteSpace(site))), (string site) => site))
				{
					_cmbSiteFilter.Items.Add(site);
				}
				int selectedIndex = 0;
				if (!string.IsNullOrWhiteSpace(selectedSite))
				{
					int foundIndex = _cmbSiteFilter.FindStringExact(selectedSite);
					if (foundIndex >= 0)
					{
						selectedIndex = foundIndex;
					}
				}
				_cmbSiteFilter.SelectedIndex = selectedIndex;
			}
			finally
			{
				_cmbSiteFilter.SelectedIndexChanged += SiteFilterSelectedIndexChanged;
			}
		}

		private void SiteFilterSelectedIndexChanged(object sender, System.EventArgs e)
		{
			if (!_isInitializing)
			{
				RefreshAttendanceWorkspace();
			}
		}

		private System.Windows.Forms.FlowLayoutPanel BuildStatusLegend()
		{
			return new System.Windows.Forms.FlowLayoutPanel
			{
				AutoSize = true,
				WrapContents = false,
				FlowDirection = System.Windows.Forms.FlowDirection.LeftToRight,
				BackColor = System.Drawing.Color.Transparent,
				Margin = System.Windows.Forms.Padding.Empty,
				Padding = System.Windows.Forms.Padding.Empty,
				Controls = 
				{
					(System.Windows.Forms.Control)CreateLegendPill("Present", System.Drawing.Color.FromArgb(225, 245, 235), HVAC_Pro_Desktop.UI.DS.Green600),
					(System.Windows.Forms.Control)CreateLegendPill("Absent", System.Drawing.Color.FromArgb(255, 235, 232), HVAC_Pro_Desktop.UI.DS.Red500),
					(System.Windows.Forms.Control)CreateLegendPill("Leave", System.Drawing.Color.FromArgb(255, 244, 221), HVAC_Pro_Desktop.UI.DS.Amber500),
					(System.Windows.Forms.Control)CreateLegendPill("Half-day", System.Drawing.Color.FromArgb(238, 232, 255), HVAC_Pro_Desktop.UI.DS.Primary600),
					(System.Windows.Forms.Control)CreateLegendPill("Blank", System.Drawing.Color.FromArgb(243, 243, 243), HVAC_Pro_Desktop.UI.DS.Slate500)
				}
			};
		}

		private System.Windows.Forms.Panel CreateReviewQueueCard()
		{
			_reviewQueueBody = new System.Windows.Forms.FlowLayoutPanel
			{
				Dock = System.Windows.Forms.DockStyle.Fill,
				FlowDirection = System.Windows.Forms.FlowDirection.TopDown,
				WrapContents = false,
				AutoScroll = false,
				Margin = System.Windows.Forms.Padding.Empty,
				Padding = System.Windows.Forms.Padding.Empty
			};
			return HVAC_Pro_Desktop.UI.WorkforceModuleVisuals.CreateSectionCard("Review queue", "Employees with exceptions to resolve before payroll.", _reviewQueueBody, HVAC_Pro_Desktop.UI.WorkforceModuleVisuals.CreateChip("22 open", System.Drawing.Color.FromArgb(255, 244, 221), HVAC_Pro_Desktop.UI.DS.Amber600, 72), 340);
		}

		private System.Windows.Forms.Panel CreatePayrollHandoffCard()
		{
			System.Windows.Forms.Panel obj = new System.Windows.Forms.Panel
			{
				Dock = System.Windows.Forms.DockStyle.Fill,
				BackColor = HVAC_Pro_Desktop.UI.DS.Primary700,
				Margin = System.Windows.Forms.Padding.Empty,
				Padding = new System.Windows.Forms.Padding(18, 16, 18, 16)
			};
			HVAC_Pro_Desktop.UI.DS.Rounded(obj, 10);
			_lblPayrollTitle = new System.Windows.Forms.Label
			{
				Dock = System.Windows.Forms.DockStyle.Top,
				Height = 20,
				Text = "PAYROLL HANDOFF",
				Font = new System.Drawing.Font("Segoe UI", 8.5f, System.Drawing.FontStyle.Bold),
				ForeColor = System.Drawing.Color.FromArgb(214, 225, 255)
			};
			_lblPayrollBody = new System.Windows.Forms.Label
			{
				Dock = System.Windows.Forms.DockStyle.Top,
				Height = 56,
				Text = "22 employees still need review before you can close June 2026.",
				Font = new System.Drawing.Font("Segoe UI Semibold", 12f, System.Drawing.FontStyle.Bold),
				ForeColor = System.Drawing.Color.White,
				Padding = new System.Windows.Forms.Padding(0, 8, 0, 0)
			};
			System.Windows.Forms.Button button = new System.Windows.Forms.Button
			{
				Dock = System.Windows.Forms.DockStyle.Bottom,
				Height = 40,
				Text = "Close month & send to payroll",
				BackColor = System.Drawing.Color.White,
				ForeColor = HVAC_Pro_Desktop.UI.DS.Primary700,
				FlatStyle = System.Windows.Forms.FlatStyle.Flat,
				Font = new System.Drawing.Font("Segoe UI Semibold", 9.5f, System.Drawing.FontStyle.Bold),
				Cursor = System.Windows.Forms.Cursors.Hand
			};
			button.FlatAppearance.BorderSize = 0;
			button.Click += delegate
			{
				(FindForm() as HVAC_Pro_Desktop.UI.MainForm)?.NavigateTo("Payroll");
			};
			obj.Controls.Add(button);
			obj.Controls.Add(_lblPayrollBody);
			obj.Controls.Add(_lblPayrollTitle);
			return obj;
		}

		private static System.Windows.Forms.Label CreateLegendPill(string text, System.Drawing.Color backColor, System.Drawing.Color foreColor)
		{
			return HVAC_Pro_Desktop.UI.WorkforceModuleVisuals.CreateChip(text, backColor, foreColor);
		}

		private System.Windows.Forms.Panel CreateResizableDailyCoverageShell(System.Windows.Forms.Control card)
		{
			_dailyGridShell = new System.Windows.Forms.Panel
			{
				Dock = System.Windows.Forms.DockStyle.Fill,
				BackColor = System.Drawing.Color.Transparent,
				Margin = System.Windows.Forms.Padding.Empty,
				Padding = System.Windows.Forms.Padding.Empty
			};

			_dailyGridFrame = new System.Windows.Forms.Panel
			{
				Location = new System.Drawing.Point(0, 0),
				Size = new System.Drawing.Size(1660, 780),
				MinimumSize = new System.Drawing.Size(980, 520),
				BackColor = System.Drawing.Color.White,
				Anchor = System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Left,
				Margin = System.Windows.Forms.Padding.Empty,
				Padding = System.Windows.Forms.Padding.Empty
			};
			HVAC_Pro_Desktop.UI.DS.Rounded(_dailyGridFrame, HVAC_Pro_Desktop.UI.DS.RadiusLg);

			card.Dock = System.Windows.Forms.DockStyle.Fill;
			card.Margin = System.Windows.Forms.Padding.Empty;
			_dailyGridFrame.Controls.Add(card);
			_dailyGridShell.Controls.Add(_dailyGridFrame);
			_dailyGridShell.Resize += delegate
			{
				ClampDailyGridFrameToShell();
			};

			_dailyGridTopGrip = CreateDailyGridGrip(System.Windows.Forms.DockStyle.Top, System.Windows.Forms.Cursors.SizeNS, DailyGridResizeDirection.Top);
			_dailyGridBottomGrip = CreateDailyGridGrip(System.Windows.Forms.DockStyle.Bottom, System.Windows.Forms.Cursors.SizeNS, DailyGridResizeDirection.Bottom);
			_dailyGridLeftGrip = CreateDailyGridGrip(System.Windows.Forms.DockStyle.Left, System.Windows.Forms.Cursors.SizeWE, DailyGridResizeDirection.Left);
			_dailyGridRightGrip = CreateDailyGridGrip(System.Windows.Forms.DockStyle.Right, System.Windows.Forms.Cursors.SizeWE, DailyGridResizeDirection.Right);
			_dailyGridTopLeftGrip = CreateDailyGridCornerGrip(System.Windows.Forms.Cursors.SizeNWSE, DailyGridResizeDirection.TopLeft);
			_dailyGridTopRightGrip = CreateDailyGridCornerGrip(System.Windows.Forms.Cursors.SizeNESW, DailyGridResizeDirection.TopRight);
			_dailyGridBottomLeftGrip = CreateDailyGridCornerGrip(System.Windows.Forms.Cursors.SizeNESW, DailyGridResizeDirection.BottomLeft);
			_dailyGridBottomRightGrip = CreateDailyGridCornerGrip(System.Windows.Forms.Cursors.SizeNWSE, DailyGridResizeDirection.BottomRight);

			_dailyGridFrame.Controls.AddRange(new System.Windows.Forms.Control[]
			{
				_dailyGridTopGrip,
				_dailyGridBottomGrip,
				_dailyGridLeftGrip,
				_dailyGridRightGrip,
				_dailyGridTopLeftGrip,
				_dailyGridTopRightGrip,
				_dailyGridBottomLeftGrip,
				_dailyGridBottomRightGrip
			});

			PositionDailyGridGrips();
			return _dailyGridShell;
		}

		private System.Windows.Forms.Panel CreateDailyGridGrip(System.Windows.Forms.DockStyle dock, System.Windows.Forms.Cursor cursor, DailyGridResizeDirection direction)
		{
			System.Windows.Forms.Panel grip = new System.Windows.Forms.Panel
			{
				BackColor = System.Drawing.Color.FromArgb(226, 236, 249),
				Cursor = cursor
			};
			grip.Dock = dock;
			if (dock == System.Windows.Forms.DockStyle.Top || dock == System.Windows.Forms.DockStyle.Bottom)
			{
				grip.Height = 8;
			}
			else
			{
				grip.Width = 8;
			}
			HookDailyGridGrip(grip, direction);
			return grip;
		}

		private System.Windows.Forms.Panel CreateDailyGridCornerGrip(System.Windows.Forms.Cursor cursor, DailyGridResizeDirection direction)
		{
			System.Windows.Forms.Panel grip = new System.Windows.Forms.Panel
			{
				Size = new System.Drawing.Size(14, 14),
				BackColor = System.Drawing.Color.FromArgb(213, 226, 247),
				Cursor = cursor
			};
			HookDailyGridGrip(grip, direction);
			return grip;
		}

		private enum DailyGridResizeDirection
		{
			None,
			Left,
			Top,
			Right,
			Bottom,
			TopLeft,
			TopRight,
			BottomLeft,
			BottomRight
		}

		private void HookDailyGridGrip(System.Windows.Forms.Control grip, DailyGridResizeDirection direction)
		{
			if (grip == null)
			{
				return;
			}
			grip.Tag = direction;
			grip.MouseDown += DailyGridGripMouseDown;
			grip.MouseMove += DailyGridGripMouseMove;
			grip.MouseUp += DailyGridGripMouseUp;
		}

		private void DailyGridGripMouseDown(object sender, System.Windows.Forms.MouseEventArgs e)
		{
			if (e.Button != System.Windows.Forms.MouseButtons.Left || _dailyGridFrame == null || _dailyGridShell == null)
			{
				return;
			}
			_dailyGridResizing = true;
			_dailyGridUserResized = true;
			_dailyGridResizeStartMouse = System.Windows.Forms.Control.MousePosition;
			_dailyGridResizeStartSize = _dailyGridFrame.Size;
			_dailyGridResizeStartLocation = _dailyGridFrame.Location;
			((System.Windows.Forms.Control)sender).Capture = true;
		}

		private void DailyGridGripMouseMove(object sender, System.Windows.Forms.MouseEventArgs e)
		{
			System.Windows.Forms.Control grip = sender as System.Windows.Forms.Control;
			if (grip == null)
			{
				return;
			}
			if (!_dailyGridResizing)
			{
				return;
			}
			ResizeDailyGridFrame((DailyGridResizeDirection)grip.Tag, System.Windows.Forms.Control.MousePosition);
		}

		private void DailyGridGripMouseUp(object sender, System.Windows.Forms.MouseEventArgs e)
		{
			System.Windows.Forms.Control grip = sender as System.Windows.Forms.Control;
			if (grip != null)
			{
				grip.Capture = false;
			}
			_dailyGridResizing = false;
		}

		private void ResizeDailyGridFrame(DailyGridResizeDirection direction, System.Drawing.Point mousePosition)
		{
			if (_dailyGridFrame == null || _dailyGridShell == null)
			{
				return;
			}

			System.Drawing.Point delta = new System.Drawing.Point(mousePosition.X - _dailyGridResizeStartMouse.X, mousePosition.Y - _dailyGridResizeStartMouse.Y);
			System.Drawing.Size shellSize = _dailyGridShell.ClientSize;
			int minWidth = _dailyGridFrame.MinimumSize.Width;
			int minHeight = _dailyGridFrame.MinimumSize.Height;
			int left = _dailyGridResizeStartLocation.X;
			int top = _dailyGridResizeStartLocation.Y;
			int width = _dailyGridResizeStartSize.Width;
			int height = _dailyGridResizeStartSize.Height;

			if (direction == DailyGridResizeDirection.Left || direction == DailyGridResizeDirection.TopLeft || direction == DailyGridResizeDirection.BottomLeft)
			{
				left = _dailyGridResizeStartLocation.X + delta.X;
				width = _dailyGridResizeStartSize.Width - delta.X;
			}
			if (direction == DailyGridResizeDirection.Right || direction == DailyGridResizeDirection.TopRight || direction == DailyGridResizeDirection.BottomRight)
			{
				width = _dailyGridResizeStartSize.Width + delta.X;
			}
			if (direction == DailyGridResizeDirection.Top || direction == DailyGridResizeDirection.TopLeft || direction == DailyGridResizeDirection.TopRight)
			{
				top = _dailyGridResizeStartLocation.Y + delta.Y;
				height = _dailyGridResizeStartSize.Height - delta.Y;
			}
			if (direction == DailyGridResizeDirection.Bottom || direction == DailyGridResizeDirection.BottomLeft || direction == DailyGridResizeDirection.BottomRight)
			{
				height = _dailyGridResizeStartSize.Height + delta.Y;
			}

			width = System.Math.Max(minWidth, width);
			height = System.Math.Max(minHeight, height);
			left = System.Math.Max(0, left);
			top = System.Math.Max(0, top);

			if (left + width > shellSize.Width)
			{
				if (direction == DailyGridResizeDirection.Left || direction == DailyGridResizeDirection.TopLeft || direction == DailyGridResizeDirection.BottomLeft)
				{
					left = System.Math.Max(0, shellSize.Width - width);
				}
				else
				{
					width = System.Math.Max(minWidth, shellSize.Width - left);
				}
			}
			if (top + height > shellSize.Height)
			{
				if (direction == DailyGridResizeDirection.Top || direction == DailyGridResizeDirection.TopLeft || direction == DailyGridResizeDirection.TopRight)
				{
					top = System.Math.Max(0, shellSize.Height - height);
				}
				else
				{
					height = System.Math.Max(minHeight, shellSize.Height - top);
				}
			}

			_dailyGridFrame.Location = new System.Drawing.Point(left, top);
			_dailyGridFrame.Size = new System.Drawing.Size(width, height);
			PositionDailyGridGrips();
			_dailyGridShell.PerformLayout();
		}

		private void PositionDailyGridGrips()
		{
			if (_dailyGridFrame == null || _dailyGridFrame.IsDisposed)
			{
				return;
			}

			if (_dailyGridTopLeftGrip != null)
			{
				_dailyGridTopLeftGrip.Location = new System.Drawing.Point(0, 0);
				_dailyGridTopLeftGrip.BringToFront();
			}
			if (_dailyGridTopRightGrip != null)
			{
				_dailyGridTopRightGrip.Location = new System.Drawing.Point(System.Math.Max(0, _dailyGridFrame.ClientSize.Width - _dailyGridTopRightGrip.Width), 0);
				_dailyGridTopRightGrip.BringToFront();
			}
			if (_dailyGridBottomLeftGrip != null)
			{
				_dailyGridBottomLeftGrip.Location = new System.Drawing.Point(0, System.Math.Max(0, _dailyGridFrame.ClientSize.Height - _dailyGridBottomLeftGrip.Height));
				_dailyGridBottomLeftGrip.BringToFront();
			}
			if (_dailyGridBottomRightGrip != null)
			{
				_dailyGridBottomRightGrip.Location = new System.Drawing.Point(System.Math.Max(0, _dailyGridFrame.ClientSize.Width - _dailyGridBottomRightGrip.Width), System.Math.Max(0, _dailyGridFrame.ClientSize.Height - _dailyGridBottomRightGrip.Height));
				_dailyGridBottomRightGrip.BringToFront();
			}
		}

		private void ClampDailyGridFrameToShell()
		{
			if (_dailyGridFrame == null || _dailyGridShell == null)
			{
				return;
			}
			System.Drawing.Size shellSize = _dailyGridShell.ClientSize;
			int width = _dailyGridUserResized ? System.Math.Min(_dailyGridFrame.Width, shellSize.Width) : shellSize.Width;
			int height = _dailyGridUserResized ? System.Math.Min(_dailyGridFrame.Height, shellSize.Height) : shellSize.Height;
			_dailyGridFrame.Location = new System.Drawing.Point(System.Math.Max(0, _dailyGridFrame.Left), System.Math.Max(0, _dailyGridFrame.Top));
			_dailyGridFrame.Size = new System.Drawing.Size(System.Math.Max(_dailyGridFrame.MinimumSize.Width, width), System.Math.Max(_dailyGridFrame.MinimumSize.Height, height));
			if (_dailyGridFrame.Right > shellSize.Width)
			{
				_dailyGridFrame.Left = System.Math.Max(0, shellSize.Width - _dailyGridFrame.Width);
			}
			if (_dailyGridFrame.Bottom > shellSize.Height)
			{
				_dailyGridFrame.Top = System.Math.Max(0, shellSize.Height - _dailyGridFrame.Height);
			}
			PositionDailyGridGrips();
		}

		private void RefreshAttendanceWorkspace()
		{
			if (!_isInitializing && _gridAttendance != null)
			{
				LoadAttendanceGrid();
				_btnImport.Visible = HVAC_Pro_Desktop.Services.SessionManager.IsLoggedIn;
			}
		}

		private void LoadAttendanceGrid()
		{
			_gridAttendance.SuspendLayout();
			_gridAttendance.CellFormatting -= AttendanceGridCellFormatting;
			_gridAttendance.CellPainting -= AttendanceGridCellPainting;
			_gridAttendance.CellClick -= AttendanceGridCellClick;
			_gridAttendance.AutoSizeColumnsMode = System.Windows.Forms.DataGridViewAutoSizeColumnsMode.None;
			_gridAttendance.ScrollBars = System.Windows.Forms.ScrollBars.Both;
			_gridAttendance.BackgroundColor = System.Drawing.Color.White;
			_gridAttendance.BorderStyle = System.Windows.Forms.BorderStyle.None;
			_gridAttendance.RowHeadersVisible = false;
			_gridAttendance.AllowUserToAddRows = false;
			_gridAttendance.AllowUserToDeleteRows = false;
			_gridAttendance.AllowUserToResizeRows = false;
			_gridAttendance.SelectionMode = System.Windows.Forms.DataGridViewSelectionMode.CellSelect;
			_gridAttendance.MultiSelect = false;
			_gridAttendance.EditMode = System.Windows.Forms.DataGridViewEditMode.EditProgrammatically;
			_gridAttendance.ColumnHeadersHeightSizeMode = System.Windows.Forms.DataGridViewColumnHeadersHeightSizeMode.DisableResizing;
			_gridAttendance.ColumnHeadersHeight = 34;
			_gridAttendance.DefaultCellStyle.Font = new System.Drawing.Font("Segoe UI", 9f);
			_gridAttendance.DefaultCellStyle.ForeColor = HVAC_Pro_Desktop.UI.DS.Slate800;
			_gridAttendance.DefaultCellStyle.SelectionBackColor = System.Drawing.Color.Transparent;
			_gridAttendance.DefaultCellStyle.SelectionForeColor = HVAC_Pro_Desktop.UI.DS.Slate800;
			_gridAttendance.RowTemplate.Height = 44;
			_gridAttendance.Columns.Clear();
			_gridAttendance.Rows.Clear();
			_gridAttendance.Columns.Add("Avatar", "");
			_gridAttendance.Columns["Avatar"].Width = 48;
			_gridAttendance.Columns["Avatar"].Frozen = true;
			_gridAttendance.Columns["Avatar"].SortMode = System.Windows.Forms.DataGridViewColumnSortMode.NotSortable;
			_gridAttendance.Columns["Avatar"].ReadOnly = true;
			_gridAttendance.Columns["Avatar"].Resizable = System.Windows.Forms.DataGridViewTriState.False;
			_gridAttendance.Columns.Add("Employee", "Employee");
			_gridAttendance.Columns["Employee"].Width = 190;
			_gridAttendance.Columns["Employee"].Frozen = true;
			_gridAttendance.Columns["Employee"].ReadOnly = true;
			_gridAttendance.Columns["Employee"].SortMode = System.Windows.Forms.DataGridViewColumnSortMode.NotSortable;
			int daysInMonth = System.DateTime.DaysInMonth(CurrentYear, CurrentMonth);
			for (int i = 1; i <= daysInMonth; i++)
			{
				System.Windows.Forms.DataGridViewTextBoxColumn dayColumn = new System.Windows.Forms.DataGridViewTextBoxColumn
				{
					Name = "D" + i,
					HeaderText = i.ToString(),
					Width = 30,
					MinimumWidth = 30,
					SortMode = System.Windows.Forms.DataGridViewColumnSortMode.NotSortable,
					ReadOnly = true
				};
				dayColumn.HeaderCell.Style.Alignment = System.Windows.Forms.DataGridViewContentAlignment.MiddleCenter;
				dayColumn.HeaderCell.Style.BackColor = System.Drawing.Color.White;
				dayColumn.HeaderCell.Style.ForeColor = HVAC_Pro_Desktop.UI.DS.Slate500;
				dayColumn.HeaderCell.Style.Font = new System.Drawing.Font("Segoe UI", 8.5f, System.Drawing.FontStyle.Bold);
				_gridAttendance.Columns.Add(dayColumn);
			}
			_gridAttendance.CellFormatting += AttendanceGridCellFormatting;
			_gridAttendance.CellPainting += AttendanceGridCellPainting;
			_gridAttendance.CellClick += AttendanceGridCellClick;
			System.Collections.Generic.Dictionary<string, HVAC_Pro_Desktop.Models.AttendanceRecord> dictionary = System.Linq.Enumerable.ToDictionary(_attendanceService.GetMonthlyAttendanceRecords(CurrentMonth, CurrentYear) ?? new System.Collections.Generic.List<HVAC_Pro_Desktop.Models.AttendanceRecord>(), (HVAC_Pro_Desktop.Models.AttendanceRecord a) => a.EmployeeId + "|" + a.AttendanceDate.Day, (HVAC_Pro_Desktop.Models.AttendanceRecord a) => a);
			System.Collections.Generic.List<HVAC_Pro_Desktop.Models.Employee> visibleEmployees = GetFilteredAttendanceEmployees();
			int reviewCount = 0;
			int completeCount = 0;
			System.Collections.Generic.List<System.Tuple<HVAC_Pro_Desktop.Models.Employee, int, string>> reviewRows = new System.Collections.Generic.List<System.Tuple<HVAC_Pro_Desktop.Models.Employee, int, string>>();
			foreach (HVAC_Pro_Desktop.Models.Employee employee in visibleEmployees)
			{
				int index = _gridAttendance.Rows.Add(GetEmployeeInitials(employee), BuildEmployeeDisplay(employee));
				System.Windows.Forms.DataGridViewRow row = _gridAttendance.Rows[index];
				row.Tag = employee;
				row.DefaultCellStyle.WrapMode = System.Windows.Forms.DataGridViewTriState.True;
				row.Height = 44;
				int employeeReviewCount = 0;
				bool hasAnyRecord = false;
				for (int day = 1; day <= daysInMonth; day++)
				{
					string key = employee.EmployeeID + "|" + day;
					HVAC_Pro_Desktop.Models.AttendanceRecord value;
					string status = dictionary.TryGetValue(key, out value) ? NormalizeAttendanceStatus(value.Status) : string.Empty;
					if (!string.IsNullOrWhiteSpace(status))
					{
						hasAnyRecord = true;
					}
					if (IsAttendanceExceptionStatus(status))
					{
						employeeReviewCount++;
					}
					row.Cells["D" + day].Value = status;
				}
				if (employeeReviewCount > 0 || !hasAnyRecord)
				{
					reviewCount++;
					reviewRows.Add(System.Tuple.Create(employee, (employeeReviewCount == 0) ? 1 : employeeReviewCount, BuildReviewDetail(row, daysInMonth)));
				}
				else
				{
					completeCount++;
				}
			}
			foreach (System.Windows.Forms.DataGridViewRow item in (System.Collections.IEnumerable)_gridAttendance.Rows)
			{
				if (!item.IsNewRow)
				{
					ApplyAttendanceRowStyle(item);
				}
			}
			StyleAttendanceGrid(daysInMonth);
			_gridAttendance.ResumeLayout();
			_gridAttendance.ClearSelection();
			int count = visibleEmployees.Count;
			decimal coverage = count == 0 ? 0m : ((decimal)completeCount * 100m / (decimal)count);
			if (_lblActiveEmployees != null) _lblActiveEmployees.Text = count.ToString();
			if (_lblCompleteCount != null) _lblCompleteCount.Text = completeCount.ToString();
			if (_lblReviewCount != null) _lblReviewCount.Text = reviewCount.ToString();
			if (_lblCoverage != null) _lblCoverage.Text = coverage.ToString("0.#") + "%";
			if (_lblMonthChip != null) _lblMonthChip.Text = reviewCount == 0 ? "Month ready" : "Month open";
			string reconciliationBanner = GetAttendanceReconciliationBanner();
			if (_lblPeriodStatus != null) _lblPeriodStatus.Text = string.IsNullOrWhiteSpace(reconciliationBanner) ? $"Workspace ready for {CurrentMonthName} {CurrentYear}" : reconciliationBanner;
			if (_lblSummary != null) _lblSummary.Text = $"{reviewCount} employees need review.";
			if (_lblReviewInsight != null) _lblReviewInsight.Text = "Focus on blanks, absences, leave, half-day.";
			if (_lblCoverageInsight != null) _lblCoverageInsight.Text = $"Coverage: {coverage:0.#}% across {count} employee(s)" + GetSelectedSiteSuffix() + ".";
			if (_lblMonthSignalSummary != null) _lblMonthSignalSummary.Text = $"{reviewCount} employees still need attendance review for {CurrentMonthName} {CurrentYear}.";
			if (_lblMonthSignalDetail != null) _lblMonthSignalDetail.Text = "Review queue, coverage distribution, and exception mix.";
			if (_reviewQueueBody != null)
			{
				_reviewQueueBody.Controls.Clear();
				foreach (System.Tuple<HVAC_Pro_Desktop.Models.Employee, int, string> review in System.Linq.Enumerable.Take(System.Linq.Enumerable.OrderByDescending(reviewRows, (System.Tuple<HVAC_Pro_Desktop.Models.Employee, int, string> tuple) => tuple.Item2), 5))
				{
					_reviewQueueBody.Controls.Add(CreateReviewQueueItem(review.Item1, review.Item2, review.Item3));
				}
			}
			if (_lblPayrollBody != null) _lblPayrollBody.Text = $"{reviewCount} employee(s)" + GetSelectedSiteSuffix() + $" still need review before you can close {CurrentMonthName} {CurrentYear}.";
			SetStatus(string.IsNullOrWhiteSpace(reconciliationBanner) ? "Attendance workspace ready for " + CurrentMonthName + " " + CurrentYear + "." : reconciliationBanner, (reviewCount == 0 && string.IsNullOrWhiteSpace(reconciliationBanner)) ? HVAC_Pro_Desktop.UI.DS.Green600 : HVAC_Pro_Desktop.UI.DS.Amber600);
		}

		private System.Collections.Generic.List<HVAC_Pro_Desktop.Models.Employee> GetFilteredAttendanceEmployees()
		{
			string selectedSite = GetSelectedSiteFilter();
			System.Collections.Generic.IEnumerable<HVAC_Pro_Desktop.Models.Employee> query = _employees ?? new System.Collections.Generic.List<HVAC_Pro_Desktop.Models.Employee>();
			if (!string.IsNullOrWhiteSpace(selectedSite))
			{
				query = System.Linq.Enumerable.Where(query, (HVAC_Pro_Desktop.Models.Employee employee) => string.Equals((employee.ClientSite ?? string.Empty).Trim(), selectedSite, System.StringComparison.OrdinalIgnoreCase));
			}
			return System.Linq.Enumerable.ToList(query);
		}

		private string GetSelectedSiteFilter()
		{
			string selectedSite = _cmbSiteFilter == null ? string.Empty : System.Convert.ToString(_cmbSiteFilter.SelectedItem);
			if (string.IsNullOrWhiteSpace(selectedSite) || string.Equals(selectedSite, "All sites", System.StringComparison.OrdinalIgnoreCase))
			{
				return string.Empty;
			}
			return selectedSite.Trim();
		}

		private string GetSelectedSiteSuffix()
		{
			string selectedSite = GetSelectedSiteFilter();
			return string.IsNullOrWhiteSpace(selectedSite) ? string.Empty : " at " + selectedSite;
		}

		private string GetAttendanceReconciliationBanner()
		{
			try
			{
				return _attendanceService.GetSourceReconciliationBanner(CurrentMonth, CurrentYear);
			}
			catch (System.Exception ex)
			{
				HVAC_Pro_Desktop.Services.AppLogger.LogError("AttendanceForm.GetAttendanceReconciliationBanner", ex);
				return string.Empty;
			}
		}

		private void RefreshAttendanceSummary(int daysInMonth)
		{
			int num = 0;
			int num2 = 0;
			int num3 = 0;
			System.Collections.Generic.List<System.Tuple<HVAC_Pro_Desktop.Models.Employee, int, string>> list = new System.Collections.Generic.List<System.Tuple<HVAC_Pro_Desktop.Models.Employee, int, string>>();
			foreach (System.Windows.Forms.DataGridViewRow item2 in (System.Collections.IEnumerable)_gridAttendance.Rows)
			{
				if (item2.IsNewRow)
				{
					continue;
				}
				num++;
				HVAC_Pro_Desktop.Models.Employee item = item2.Tag as HVAC_Pro_Desktop.Models.Employee;
				int num4 = 0;
				bool flag = false;
				for (int i = 1; i <= daysInMonth; i++)
				{
					string text = NormalizeAttendanceStatus(System.Convert.ToString(item2.Cells["D" + i].Value));
					if (!string.IsNullOrWhiteSpace(text))
					{
						flag = true;
					}
					if (IsAttendanceExceptionStatus(text))
					{
						num4++;
					}
				}
				if (num4 > 0 || !flag)
				{
					num2++;
					list.Add(System.Tuple.Create(item, (num4 == 0) ? 1 : num4, BuildReviewDetail(item2, daysInMonth)));
				}
				else
				{
					num3++;
				}
			}
			decimal num5 = ((num == 0) ? 0m : ((decimal)num3 * 100m / (decimal)num));
			if (_lblActiveEmployees != null)
			{
				_lblActiveEmployees.Text = num.ToString();
			}
			if (_lblCompleteCount != null)
			{
				_lblCompleteCount.Text = num3.ToString();
			}
			if (_lblReviewCount != null)
			{
				_lblReviewCount.Text = num2.ToString();
			}
			if (_lblCoverage != null)
			{
				_lblCoverage.Text = num5.ToString("0.#") + "%";
			}
			if (_lblMonthChip != null)
			{
				_lblMonthChip.Text = ((num2 == 0) ? "Month ready" : "Month open");
			}
			if (_lblPeriodStatus != null)
			{
				string reconciliationBanner = GetAttendanceReconciliationBanner();
				_lblPeriodStatus.Text = string.IsNullOrWhiteSpace(reconciliationBanner) ? $"Workspace ready for {CurrentMonthName} {CurrentYear}" : reconciliationBanner;
			}
			if (_lblMonthSignalSummary != null)
			{
				_lblMonthSignalSummary.Text = $"{num2} employees still need attendance review for {CurrentMonthName} {CurrentYear}.";
			}
			if (_lblMonthSignalDetail != null)
			{
				_lblMonthSignalDetail.Text = "Distribution across all recorded working days.";
			}
			if (_lblCoverageInsight != null)
			{
				_lblCoverageInsight.Text = $"Coverage is {num5:0.#}% across {num} active employees.";
			}
			if (_reviewQueueBody != null)
			{
				_reviewQueueBody.Controls.Clear();
				foreach (System.Tuple<HVAC_Pro_Desktop.Models.Employee, int, string> item3 in System.Linq.Enumerable.Take(System.Linq.Enumerable.OrderByDescending(list, (System.Tuple<HVAC_Pro_Desktop.Models.Employee, int, string> tuple) => tuple.Item2), 5))
				{
					_reviewQueueBody.Controls.Add(CreateReviewQueueItem(item3.Item1, item3.Item2, item3.Item3));
				}
			}
			if (_lblPayrollBody != null)
			{
				_lblPayrollBody.Text = $"{num2} employees still need review before you can close {CurrentMonthName} {CurrentYear}.";
			}
		}

		private void AttendanceGridCellFormatting(object sender, System.Windows.Forms.DataGridViewCellFormattingEventArgs e)
		{
			if (e.RowIndex < 0 || e.ColumnIndex < 0 || !(sender is System.Windows.Forms.DataGridView dataGridView))
			{
				return;
			}
			System.Windows.Forms.DataGridViewColumn dataGridViewColumn = dataGridView.Columns[e.ColumnIndex];
			if (dataGridViewColumn != null)
			{
				if (dataGridViewColumn.Name == "Avatar")
				{
					e.Value = System.Convert.ToString(e.Value);
					e.FormattingApplied = true;
				}
				else if (dataGridViewColumn.Name == "Employee")
				{
					e.FormattingApplied = true;
				}
				else
				{
					string status = NormalizeAttendanceStatus(System.Convert.ToString(e.Value));
					e.Value = GetStatusLetter(status);
					e.FormattingApplied = true;
				}
			}
		}

		private void AttendanceGridCellPainting(object sender, System.Windows.Forms.DataGridViewCellPaintingEventArgs e)
		{
			if (e.RowIndex < 0 || e.ColumnIndex < 0 || !(sender is System.Windows.Forms.DataGridView dataGridView))
			{
				return;
			}
			System.Windows.Forms.DataGridViewColumn dataGridViewColumn = dataGridView.Columns[e.ColumnIndex];
			if (dataGridViewColumn == null)
			{
				return;
			}
			if (dataGridViewColumn.Name == "Avatar")
			{
				e.PaintBackground(e.CellBounds, cellsPaintSelectionBackground: true);
				HVAC_Pro_Desktop.Models.Employee employee = dataGridView.Rows[e.RowIndex].Tag as HVAC_Pro_Desktop.Models.Employee;
				string employeeInitials = GetEmployeeInitials(employee);
				System.Drawing.Color employeeAccent = GetEmployeeAccent(employee);
				System.Drawing.Rectangle rectangle = new System.Drawing.Rectangle(e.CellBounds.X + 7, e.CellBounds.Y + 8, 28, 28);
				using (System.Drawing.SolidBrush brush = new System.Drawing.SolidBrush(employeeAccent))
				{
					e.Graphics.FillEllipse(brush, rectangle);
				}
				using (System.Drawing.Pen pen = new System.Drawing.Pen(System.Drawing.Color.FromArgb(40, employeeAccent)))
				{
					e.Graphics.DrawEllipse(pen, rectangle);
				}
				using (System.Drawing.StringFormat format = new System.Drawing.StringFormat
				{
					Alignment = System.Drawing.StringAlignment.Center,
					LineAlignment = System.Drawing.StringAlignment.Center
				})
				{
					using (System.Drawing.Brush brush2 = new System.Drawing.SolidBrush(System.Drawing.Color.White))
					{
						e.Graphics.DrawString(employeeInitials, new System.Drawing.Font("Segoe UI Semibold", 8.5f, System.Drawing.FontStyle.Bold), brush2, rectangle, format);
					}
				}
				e.Handled = true;
			}
			else
			{
				if (!dataGridViewColumn.Name.StartsWith("D", System.StringComparison.OrdinalIgnoreCase))
				{
					return;
				}
				string text = NormalizeAttendanceStatus(System.Convert.ToString(dataGridView.Rows[e.RowIndex].Cells[e.ColumnIndex].Value));
				System.Drawing.Rectangle rectangle2 = System.Drawing.Rectangle.Inflate(e.CellBounds, -3, -4);
				System.Drawing.Color color;
				System.Drawing.Color color2;
				if (string.Equals(text, "PRESENT", System.StringComparison.OrdinalIgnoreCase))
				{
					color = System.Drawing.Color.FromArgb(225, 243, 233);
					color2 = HVAC_Pro_Desktop.UI.DS.Green600;
				}
				else if (string.Equals(text, "ABSENT", System.StringComparison.OrdinalIgnoreCase))
				{
					color = System.Drawing.Color.FromArgb(255, 236, 232);
					color2 = HVAC_Pro_Desktop.UI.DS.Red600;
				}
				else if (string.Equals(text, "LEAVE", System.StringComparison.OrdinalIgnoreCase))
				{
					color = System.Drawing.Color.FromArgb(255, 243, 221);
					color2 = System.Drawing.Color.FromArgb(184, 121, 0);
				}
				else if (string.Equals(text, "HALFDAY", System.StringComparison.OrdinalIgnoreCase))
				{
					color = System.Drawing.Color.FromArgb(240, 233, 255);
					color2 = System.Drawing.Color.FromArgb(111, 72, 194);
				}
				else
				{
					color = System.Drawing.Color.White;
					color2 = HVAC_Pro_Desktop.UI.DS.Slate400;
				}
				e.PaintBackground(e.CellBounds, cellsPaintSelectionBackground: true);
				using (System.Drawing.Drawing2D.GraphicsPath path = HVAC_Pro_Desktop.UI.DS.RoundedRect(rectangle2, 6))
				{
					using (System.Drawing.SolidBrush brush3 = new System.Drawing.SolidBrush(color))
					{
						using (System.Drawing.Pen pen2 = new System.Drawing.Pen(System.Drawing.Color.FromArgb(220, 226, 235)))
						{
							e.Graphics.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;
							e.Graphics.FillPath(brush3, path);
							e.Graphics.DrawPath(pen2, path);
						}
					}
				}
				string statusLetter = GetStatusLetter(text);
				if (!string.IsNullOrWhiteSpace(statusLetter))
				{
					using (System.Drawing.StringFormat format2 = new System.Drawing.StringFormat
					{
						Alignment = System.Drawing.StringAlignment.Center,
						LineAlignment = System.Drawing.StringAlignment.Center
					})
					{
						using (System.Drawing.Brush brush4 = new System.Drawing.SolidBrush(color2))
						{
							e.Graphics.DrawString(statusLetter, new System.Drawing.Font("Segoe UI Semibold", 8.5f, System.Drawing.FontStyle.Bold), brush4, rectangle2, format2);
						}
					}
				}
				e.Handled = true;
			}
		}

		private void AttendanceGridCellClick(object sender, System.Windows.Forms.DataGridViewCellEventArgs e)
		{
			if (e.RowIndex >= 0 && e.ColumnIndex >= 0 && sender is System.Windows.Forms.DataGridView dataGridView)
			{
				System.Windows.Forms.DataGridViewColumn dataGridViewColumn = dataGridView.Columns[e.ColumnIndex];
				if (dataGridViewColumn != null && dataGridViewColumn.Name.StartsWith("D", System.StringComparison.OrdinalIgnoreCase))
				{
					System.Windows.Forms.DataGridViewCell dataGridViewCell = dataGridView.Rows[e.RowIndex].Cells[e.ColumnIndex];
					string status = (string)(dataGridViewCell.Value = CycleAttendanceStatus(System.Convert.ToString(dataGridViewCell.Value)));
					ApplyAttendanceCellStyle(dataGridViewCell, status);
					RefreshAttendanceSummary(System.DateTime.DaysInMonth(CurrentYear, CurrentMonth));
					dataGridView.InvalidateCell(dataGridViewCell);
					dataGridView.InvalidateRow(e.RowIndex);
				}
			}
		}

		private static string CycleAttendanceStatus(string current)
		{
			string text = NormalizeAttendanceStatus(current);
			if (string.IsNullOrEmpty(text))
			{
				return "Present";
			}
			if (string.Equals(text, "PRESENT", System.StringComparison.OrdinalIgnoreCase))
			{
				return "Absent";
			}
			if (string.Equals(text, "ABSENT", System.StringComparison.OrdinalIgnoreCase))
			{
				return "Leave";
			}
			if (string.Equals(text, "LEAVE", System.StringComparison.OrdinalIgnoreCase))
			{
				return "HalfDay";
			}
			if (string.Equals(text, "HALFDAY", System.StringComparison.OrdinalIgnoreCase))
			{
				return string.Empty;
			}
			return "Present";
		}

		private void ApplyAttendanceCellStyle(System.Windows.Forms.DataGridViewCell cell, string status)
		{
			if (cell != null)
			{
				string a = NormalizeAttendanceStatus(status);
				cell.Style.Alignment = System.Windows.Forms.DataGridViewContentAlignment.MiddleCenter;
				cell.Style.Font = new System.Drawing.Font("Segoe UI Semibold", 8.5f, System.Drawing.FontStyle.Bold);
				cell.Style.SelectionForeColor = cell.Style.ForeColor;
				cell.Style.SelectionBackColor = cell.Style.BackColor;
				cell.Style.Padding = new System.Windows.Forms.Padding(0);
				if (string.Equals(a, "PRESENT", System.StringComparison.OrdinalIgnoreCase))
				{
					cell.Style.BackColor = System.Drawing.Color.FromArgb(229, 244, 235);
					cell.Style.ForeColor = HVAC_Pro_Desktop.UI.DS.Green600;
				}
				else if (string.Equals(a, "ABSENT", System.StringComparison.OrdinalIgnoreCase))
				{
					cell.Style.BackColor = System.Drawing.Color.FromArgb(255, 236, 232);
					cell.Style.ForeColor = HVAC_Pro_Desktop.UI.DS.Red600;
				}
				else if (string.Equals(a, "LEAVE", System.StringComparison.OrdinalIgnoreCase))
				{
					cell.Style.BackColor = System.Drawing.Color.FromArgb(255, 243, 221);
					cell.Style.ForeColor = System.Drawing.Color.FromArgb(184, 121, 0);
				}
				else if (string.Equals(a, "HALFDAY", System.StringComparison.OrdinalIgnoreCase))
				{
					cell.Style.BackColor = System.Drawing.Color.FromArgb(241, 233, 255);
					cell.Style.ForeColor = System.Drawing.Color.FromArgb(111, 72, 194);
				}
				else if (string.Equals(a, "WEEKOFF", System.StringComparison.OrdinalIgnoreCase) || string.Equals(a, "HOLIDAY", System.StringComparison.OrdinalIgnoreCase))
				{
					cell.Style.BackColor = System.Drawing.Color.FromArgb(243, 243, 243);
					cell.Style.ForeColor = HVAC_Pro_Desktop.UI.DS.Slate500;
				}
				else
				{
					cell.Style.BackColor = System.Drawing.Color.White;
					cell.Style.ForeColor = HVAC_Pro_Desktop.UI.DS.Slate400;
				}
			}
		}

		private void ApplyAttendanceRowStyle(System.Windows.Forms.DataGridViewRow row)
		{
			if (row == null)
			{
				return;
			}
			if (row.Tag is HVAC_Pro_Desktop.Models.Employee)
			{
				row.Cells["Employee"].Style.Font = new System.Drawing.Font("Segoe UI Semibold", 9f, System.Drawing.FontStyle.Bold);
				row.Cells["Employee"].Style.ForeColor = HVAC_Pro_Desktop.UI.DS.Slate900;
				row.Cells["Employee"].Style.Alignment = System.Windows.Forms.DataGridViewContentAlignment.MiddleLeft;
				row.Cells["Employee"].Style.WrapMode = System.Windows.Forms.DataGridViewTriState.True;
				row.Cells["Avatar"].Style.BackColor = System.Drawing.Color.White;
			}
			foreach (System.Windows.Forms.DataGridViewCell cell in row.Cells)
			{
				if (cell.OwningColumn != null && cell.OwningColumn.Name.StartsWith("D", System.StringComparison.OrdinalIgnoreCase))
				{
					ApplyAttendanceCellStyle(cell, System.Convert.ToString(cell.Value));
				}
			}
		}

		private static string GetStatusLetter(string status)
		{
			string a = NormalizeAttendanceStatus(status);
			if (string.Equals(a, "PRESENT", System.StringComparison.OrdinalIgnoreCase))
			{
				return "P";
			}
			if (string.Equals(a, "ABSENT", System.StringComparison.OrdinalIgnoreCase))
			{
				return "A";
			}
			if (string.Equals(a, "LEAVE", System.StringComparison.OrdinalIgnoreCase))
			{
				return "L";
			}
			if (string.Equals(a, "HALFDAY", System.StringComparison.OrdinalIgnoreCase))
			{
				return "H";
			}
			if (string.Equals(a, "WEEKOFF", System.StringComparison.OrdinalIgnoreCase))
			{
				return "W";
			}
			if (string.Equals(a, "HOLIDAY", System.StringComparison.OrdinalIgnoreCase))
			{
				return "H";
			}
			return string.Empty;
		}

		private static string BuildEmployeeDisplay(HVAC_Pro_Desktop.Models.Employee employee)
		{
			if (employee == null)
			{
				return string.Empty;
			}
			string text = employee.Name ?? string.Empty;
			string text2 = (string.IsNullOrWhiteSpace(employee.Designation) ? (employee.Department ?? string.Empty) : employee.Designation);
			if (!string.IsNullOrWhiteSpace(text2))
			{
				return text + System.Environment.NewLine + text2;
			}
			return text;
		}

		private static string BuildReviewDetail(System.Windows.Forms.DataGridViewRow row, int daysInMonth)
		{
			int num = 0;
			int num2 = 0;
			int num3 = 0;
			int num4 = 0;
			for (int i = 1; i <= daysInMonth; i++)
			{
				string text = NormalizeAttendanceStatus(System.Convert.ToString(row.Cells["D" + i].Value));
				if (string.IsNullOrWhiteSpace(text))
				{
					num4++;
				}
				else if (string.Equals(text, "ABSENT", System.StringComparison.OrdinalIgnoreCase))
				{
					num++;
				}
				else if (string.Equals(text, "LEAVE", System.StringComparison.OrdinalIgnoreCase))
				{
					num2++;
				}
				else if (string.Equals(text, "HALFDAY", System.StringComparison.OrdinalIgnoreCase))
				{
					num3++;
				}
			}
			System.Collections.Generic.List<string> list = new System.Collections.Generic.List<string>();
			if (num > 0)
			{
				list.Add(num + " absent");
			}
			if (num2 > 0)
			{
				list.Add(num2 + " leave");
			}
			if (num3 > 0)
			{
				list.Add(num3 + " half-day");
			}
			if (num4 > 0 && list.Count == 0)
			{
				list.Add(num4 + " blank");
			}
			if (list.Count != 0)
			{
				return string.Join(" · ", list);
			}
			return "Ready for payroll";
		}

		private static string GetEmployeeInitials(HVAC_Pro_Desktop.Models.Employee employee)
		{
			if (employee == null || string.IsNullOrWhiteSpace(employee.Name))
			{
				return string.Empty;
			}
			string[] array = employee.Name.Trim().Split(new char[1] { ' ' }, System.StringSplitOptions.RemoveEmptyEntries);
			if (array.Length == 0)
			{
				return string.Empty;
			}
			if (array.Length == 1)
			{
				return array[0].Substring(0, System.Math.Min(2, array[0].Length)).ToUpperInvariant();
			}
			return (array[0][0].ToString() + array[array.Length - 1][0]).ToUpperInvariant();
		}

		private static System.Drawing.Color GetEmployeeAccent(HVAC_Pro_Desktop.Models.Employee employee)
		{
			string text = ((employee == null) ? string.Empty : (employee.Name ?? string.Empty)).Trim();
			if (string.IsNullOrWhiteSpace(text))
			{
				return HVAC_Pro_Desktop.UI.DS.Primary600;
			}
			int num = System.Math.Abs(text.GetHashCode());
			System.Drawing.Color[] array = new System.Drawing.Color[6]
			{
				System.Drawing.Color.FromArgb(98, 118, 255),
				System.Drawing.Color.FromArgb(58, 162, 139),
				System.Drawing.Color.FromArgb(232, 161, 52),
				System.Drawing.Color.FromArgb(145, 112, 230),
				System.Drawing.Color.FromArgb(207, 100, 86),
				System.Drawing.Color.FromArgb(81, 156, 204)
			};
			return array[num % array.Length];
		}

		private System.Windows.Forms.Panel CreateReviewQueueItem(HVAC_Pro_Desktop.Models.Employee employee, int issueCount, string detail)
		{
			System.Windows.Forms.Panel item = new System.Windows.Forms.Panel
			{
				Height = 72,
				Width = 250,
				BackColor = System.Drawing.Color.White,
				Margin = new System.Windows.Forms.Padding(0, 0, 0, 10),
				Padding = new System.Windows.Forms.Padding(10, 9, 10, 9)
			};
			HVAC_Pro_Desktop.UI.DS.Rounded(item);
			item.Paint += delegate(object s, System.Windows.Forms.PaintEventArgs e)
			{
				using (System.Drawing.Pen pen = new System.Drawing.Pen(System.Drawing.Color.FromArgb(225, 229, 236)))
				{
					e.Graphics.DrawRectangle(pen, 0, 0, item.Width - 1, item.Height - 1);
				}
			};
			System.Windows.Forms.Label label = new System.Windows.Forms.Label
			{
				Text = GetEmployeeInitials(employee),
				Location = new System.Drawing.Point(10, 14),
				Size = new System.Drawing.Size(28, 28),
				TextAlign = System.Drawing.ContentAlignment.MiddleCenter,
				Font = new System.Drawing.Font("Segoe UI Semibold", 8.5f, System.Drawing.FontStyle.Bold),
				ForeColor = GetEmployeeAccent(employee),
				BackColor = System.Drawing.Color.FromArgb(245, 247, 255)
			};
			HVAC_Pro_Desktop.UI.DS.Rounded(label, 14);
			System.Windows.Forms.Label label2 = new System.Windows.Forms.Label
			{
				Text = ((employee == null) ? "Employee" : employee.Name),
				Location = new System.Drawing.Point(48, 10),
				Size = new System.Drawing.Size(146, 20),
				Font = new System.Drawing.Font("Segoe UI Semibold", 9.5f, System.Drawing.FontStyle.Bold),
				ForeColor = HVAC_Pro_Desktop.UI.DS.Slate900
			};
			System.Windows.Forms.Label label3 = new System.Windows.Forms.Label
			{
				Text = detail,
				Location = new System.Drawing.Point(48, 31),
				Size = new System.Drawing.Size(164, 20),
				Font = HVAC_Pro_Desktop.UI.DS.Small,
				ForeColor = HVAC_Pro_Desktop.UI.DS.Slate500,
				AutoEllipsis = true
			};
			System.Windows.Forms.Label label4 = new System.Windows.Forms.Label
			{
				Text = issueCount.ToString(),
				Location = new System.Drawing.Point(210, 19),
				Size = new System.Drawing.Size(26, 26),
				TextAlign = System.Drawing.ContentAlignment.MiddleCenter,
				Font = new System.Drawing.Font("Segoe UI Semibold", 8.5f, System.Drawing.FontStyle.Bold),
				ForeColor = HVAC_Pro_Desktop.UI.DS.Red600,
				BackColor = System.Drawing.Color.FromArgb(255, 238, 235)
			};
			HVAC_Pro_Desktop.UI.DS.Rounded(label4, 13);
			item.Controls.AddRange(new System.Windows.Forms.Control[4] { label4, label3, label2, label });
			return item;
		}

		private void MarkAllAttendancePresent()
		{
			if (_gridAttendance == null || !ServoERP.Infrastructure.ServoConfirmDialog.Show(this, "Mark all visible attendance as present?", "This will overwrite the visible month's attendance grid for every active employee. Use it only when the imported attendance has been verified."))
			{
				return;
			}
			int num = System.DateTime.DaysInMonth(CurrentYear, CurrentMonth);
			foreach (System.Windows.Forms.DataGridViewRow item in (System.Collections.IEnumerable)_gridAttendance.Rows)
			{
				if (!item.IsNewRow)
				{
					if (_gridAttendance.Columns.Contains("Status"))
					{
						item.Cells["Status"].Value = "Present";
						ApplyAttendanceTableRowStyle(item);
					}
					else
					{
						for (int i = 1; i <= num; i++)
						{
							item.Cells["D" + i].Value = "Present";
						}
					}
				}
			}
			StyleAttendanceGrid(num);
			SetStatus("Marked visible attendance as present. Review before saving.", HVAC_Pro_Desktop.UI.DS.Primary600);
		}

		private void SaveAttendanceGrid()
		{
			if (_gridAttendance == null)
			{
				return;
			}
			try
			{
				int num = 0;
				foreach (System.Windows.Forms.DataGridViewRow item in (System.Collections.IEnumerable)_gridAttendance.Rows)
				{
					if (item.IsNewRow)
					{
						continue;
					}
					HVAC_Pro_Desktop.Models.Employee employee = item.Tag as HVAC_Pro_Desktop.Models.Employee;
					if (employee == null || employee.EmployeeID <= 0)
					{
						continue;
					}
					if (_gridAttendance.Columns.Contains("Status"))
					{
						string text = NormalizeAttendanceStatus(System.Convert.ToString(item.Cells["Status"].Value));
						if (!string.IsNullOrWhiteSpace(text))
						{
							System.DateTime attendanceDate = new System.DateTime(CurrentYear, CurrentMonth, System.Math.Min(System.DateTime.Today.Day, System.DateTime.DaysInMonth(CurrentYear, CurrentMonth)));
							if (item.Cells["Date"].Tag is System.DateTime)
							{
								attendanceDate = (System.DateTime)item.Cells["Date"].Tag;
							}
							_attendanceService.SaveAttendanceRecord(new HVAC_Pro_Desktop.Models.AttendanceRecord
							{
								EmployeeId = employee.EmployeeID,
								AttendanceDate = attendanceDate,
								Status = text,
								OvertimeHours = 0m
							});
							num++;
						}
					}
					else
					{
						int num2 = System.DateTime.DaysInMonth(CurrentYear, CurrentMonth);
						for (int i = 1; i <= num2; i++)
						{
							string text = NormalizeAttendanceStatus(System.Convert.ToString(item.Cells["D" + i].Value));
							if (!string.IsNullOrWhiteSpace(text))
							{
								_attendanceService.SaveAttendanceRecord(new HVAC_Pro_Desktop.Models.AttendanceRecord
								{
									EmployeeId = employee.EmployeeID,
									AttendanceDate = new System.DateTime(CurrentYear, CurrentMonth, i),
									Status = text,
									OvertimeHours = 0m
								});
								num++;
							}
						}
					}
				}
				if (num == 0)
				{
					SetStatus("No attendance changes were found to save for the visible month.", HVAC_Pro_Desktop.UI.DS.Amber600);
					return;
				}
				SetStatus($"Attendance saved for {CurrentMonthName} {CurrentYear}. Records touched: {num}.", HVAC_Pro_Desktop.UI.DS.Green600);
				LoadAttendanceGrid();
			}
			catch (System.Exception ex)
			{
				HVAC_Pro_Desktop.Services.AppLogger.LogError("AttendanceForm.SaveAttendanceGrid", ex);
				SetStatus("Attendance could not be saved. Please review the month and try again.", System.Drawing.Color.Firebrick);
				System.Windows.Forms.MessageBox.Show(this, "Attendance could not be saved. Please review the visible month and try again.", "Attendance Save Failed", System.Windows.Forms.MessageBoxButtons.OK, System.Windows.Forms.MessageBoxIcon.Warning);
			}
		}

		private void ImportAttendanceFiles()
		{
			using (System.Windows.Forms.OpenFileDialog openFileDialog = new System.Windows.Forms.OpenFileDialog
			{
				Title = "Import Attendance Excel or CSV",
				Filter = "Attendance Excel or CSV|*.xlsx;*.xls;*.csv|Excel Files|*.xlsx;*.xls|CSV Files|*.csv|All Files|*.*",
				Multiselect = true
			})
			{
				if (openFileDialog.ShowDialog(this) == System.Windows.Forms.DialogResult.OK && openFileDialog.FileNames.Length != 0)
				{
					HVAC_Pro_Desktop.Models.ServiceResult<HVAC_Pro_Desktop.Models.PayrollImportReport> serviceResult = _importService.ImportFiles(openFileDialog.FileNames, CurrentMonth, CurrentYear);
					SetStatus(serviceResult.Message, serviceResult.Success ? HVAC_Pro_Desktop.UI.DS.Green600 : System.Drawing.Color.Firebrick);
					System.Windows.Forms.MessageBox.Show(BuildImportSummary(serviceResult), serviceResult.Success ? "Attendance Import Complete" : "Attendance Import Failed", System.Windows.Forms.MessageBoxButtons.OK, serviceResult.Success ? System.Windows.Forms.MessageBoxIcon.Asterisk : System.Windows.Forms.MessageBoxIcon.Exclamation);
					if (serviceResult.Success)
					{
						LoadEmployees();
						LoadAttendanceGrid();
					}
				}
			}
		}

		private System.Windows.Forms.Button NewButton(string text, System.Drawing.Point location, int width, System.Drawing.Color backColor)
		{
			bool flag = backColor == System.Drawing.Color.White || backColor.GetBrightness() > 0.92f;
			System.Windows.Forms.Button button = new System.Windows.Forms.Button();
			button.Text = text;
			button.Location = location;
			button.Width = width;
			button.Height = 34;
			button.BackColor = backColor;
			button.ForeColor = (flag ? HVAC_Pro_Desktop.UI.DS.Slate700 : System.Drawing.Color.White);
			button.FlatStyle = System.Windows.Forms.FlatStyle.Flat;
			button.Font = new System.Drawing.Font("Segoe UI", 8.75f, System.Drawing.FontStyle.Bold);
			button.Cursor = System.Windows.Forms.Cursors.Hand;
			button.UseVisualStyleBackColor = false;
			button.AutoEllipsis = true;
			button.FlatAppearance.BorderSize = (flag ? 1 : 0);
			button.FlatAppearance.BorderColor = (flag ? HVAC_Pro_Desktop.UI.DS.BorderStrong : backColor);
			button.FlatAppearance.MouseOverBackColor = (flag ? HVAC_Pro_Desktop.UI.DS.BgCardHov : HVAC_Pro_Desktop.UI.DS.Lighten(backColor, 0.08f));
			button.FlatAppearance.MouseDownBackColor = (flag ? HVAC_Pro_Desktop.UI.DS.Slate100 : HVAC_Pro_Desktop.UI.DS.Darken(backColor, 0.1f));
			HVAC_Pro_Desktop.UI.DS.Rounded(button, 6);
			return button;
		}

		private System.Windows.Forms.ComboBox NewCombo(System.Drawing.Point location, int width, string[] items)
		{
			System.Windows.Forms.ComboBox obj = new System.Windows.Forms.ComboBox
			{
				Location = location,
				Width = width,
				DropDownStyle = System.Windows.Forms.ComboBoxStyle.DropDownList,
				Font = new System.Drawing.Font("Segoe UI", 9f)
			};
			obj.Items.AddRange(items);
			obj.SelectedIndexChanged += delegate
			{
				if (!_isInitializing)
				{
					RefreshAttendanceWorkspace();
				}
			};
			return obj;
		}

		private System.Windows.Forms.Panel MakeCard()
		{
			System.Windows.Forms.Panel panel = new System.Windows.Forms.Panel
			{
				BackColor = System.Drawing.Color.White
			};
			panel.Paint += delegate(object s, System.Windows.Forms.PaintEventArgs e)
			{
				using (System.Drawing.Pen pen = new System.Drawing.Pen(HVAC_Pro_Desktop.UI.DS.Border))
				{
					e.Graphics.DrawRectangle(pen, 0, 0, panel.Width - 1, panel.Height - 1);
				}
			};
			HVAC_Pro_Desktop.UI.DS.Rounded(panel, 10);
			return panel;
		}

		private System.Windows.Forms.Panel MakeKpiCard(string title, string value, string subtitle, System.Drawing.Color accent, out System.Windows.Forms.Label valueLabel)
		{
			System.Windows.Forms.Panel panel = MakeCard();
			panel.Dock = System.Windows.Forms.DockStyle.Fill;
			panel.Margin = new System.Windows.Forms.Padding(0, 0, 12, 0);
			panel.Padding = new System.Windows.Forms.Padding(18, 16, 18, 16);
			System.Windows.Forms.Panel panel2 = new System.Windows.Forms.Panel
			{
				Location = new System.Drawing.Point(18, 20),
				Size = new System.Drawing.Size(12, 12),
				BackColor = accent
			};
			HVAC_Pro_Desktop.UI.DS.Rounded(panel2, 6);
			System.Windows.Forms.Label value2 = new System.Windows.Forms.Label
			{
				Text = title,
				Location = new System.Drawing.Point(44, 16),
				Size = new System.Drawing.Size(180, 18),
				Font = HVAC_Pro_Desktop.UI.DS.Small,
				ForeColor = HVAC_Pro_Desktop.UI.DS.Slate600,
				AutoEllipsis = true
			};
			valueLabel = new System.Windows.Forms.Label
			{
				Text = value,
				Location = new System.Drawing.Point(44, 38),
				Size = new System.Drawing.Size(180, 28),
				Font = new System.Drawing.Font("Segoe UI", 14f, System.Drawing.FontStyle.Bold),
				ForeColor = HVAC_Pro_Desktop.UI.DS.Slate900
			};
			System.Windows.Forms.Label value3 = new System.Windows.Forms.Label
			{
				Text = subtitle,
				Location = new System.Drawing.Point(44, 70),
				Size = new System.Drawing.Size(180, 18),
				Font = HVAC_Pro_Desktop.UI.DS.Small,
				ForeColor = HVAC_Pro_Desktop.UI.DS.Slate500,
				AutoEllipsis = true
			};
			panel.Controls.Add(panel2);
			panel.Controls.Add(value2);
			panel.Controls.Add(valueLabel);
			panel.Controls.Add(value3);
			return panel;
		}

		private System.Windows.Forms.Label AddSummaryCell(System.Windows.Forms.TableLayoutPanel table, int column, string title)
		{
			System.Windows.Forms.Panel cell = new System.Windows.Forms.Panel
			{
				Dock = System.Windows.Forms.DockStyle.Fill,
				BackColor = System.Drawing.Color.White,
				Padding = new System.Windows.Forms.Padding(16, 10, 12, 8),
				Margin = new System.Windows.Forms.Padding(0)
			};
			System.Windows.Forms.Label titleLabel = new System.Windows.Forms.Label
			{
				Text = title,
				Dock = System.Windows.Forms.DockStyle.Top,
				Height = 20,
				Font = new System.Drawing.Font("Segoe UI", 8.2f, System.Drawing.FontStyle.Bold),
				ForeColor = HVAC_Pro_Desktop.UI.DS.Slate600,
				AutoEllipsis = true
			};
			System.Windows.Forms.Label valueLabel = new System.Windows.Forms.Label
			{
				Text = "0",
				Dock = System.Windows.Forms.DockStyle.Fill,
				Font = new System.Drawing.Font("Segoe UI", 14f, System.Drawing.FontStyle.Bold),
				ForeColor = HVAC_Pro_Desktop.UI.DS.Slate900,
				TextAlign = System.Drawing.ContentAlignment.MiddleLeft
			};
			System.Windows.Forms.Panel separator = new System.Windows.Forms.Panel
			{
				Dock = System.Windows.Forms.DockStyle.Right,
				Width = column == 5 ? 0 : 1,
				BackColor = HVAC_Pro_Desktop.UI.DS.Border
			};
			cell.Controls.Add(valueLabel);
			cell.Controls.Add(titleLabel);
			cell.Controls.Add(separator);
			table.Controls.Add(cell, column, 0);
			return valueLabel;
		}

		private static void SetSummaryValue(System.Windows.Forms.Label label, string value)
		{
			if (label != null)
			{
				label.Text = value;
			}
		}

		private void AddAttendanceTextColumn(string name, string header, int width, bool readOnly)
		{
			System.Windows.Forms.DataGridViewTextBoxColumn column = new System.Windows.Forms.DataGridViewTextBoxColumn
			{
				Name = name,
				HeaderText = header,
				Width = width,
				ReadOnly = readOnly,
				SortMode = System.Windows.Forms.DataGridViewColumnSortMode.NotSortable
			};
			_gridAttendance.Columns.Add(column);
		}

		private void ApplyAttendanceTableRowStyle(System.Windows.Forms.DataGridViewRow row)
		{
			if (row == null || row.IsNewRow || !_gridAttendance.Columns.Contains("Status"))
			{
				return;
			}
			foreach (System.Windows.Forms.DataGridViewCell cell in row.Cells)
			{
				cell.Style.BackColor = System.Drawing.Color.White;
				cell.Style.ForeColor = HVAC_Pro_Desktop.UI.DS.Slate800;
				cell.Style.SelectionBackColor = HVAC_Pro_Desktop.UI.DS.Primary50;
				cell.Style.SelectionForeColor = HVAC_Pro_Desktop.UI.DS.Slate900;
				cell.Style.Alignment = System.Windows.Forms.DataGridViewContentAlignment.MiddleLeft;
				cell.Style.Padding = new System.Windows.Forms.Padding(4, 0, 4, 0);
			}
			System.Windows.Forms.DataGridViewCell statusCell = row.Cells["Status"];
			string status = ToAttendanceDisplayStatus(System.Convert.ToString(statusCell.Value));
			statusCell.Style.Alignment = System.Windows.Forms.DataGridViewContentAlignment.MiddleCenter;
			statusCell.Style.Font = new System.Drawing.Font("Segoe UI", 8.4f, System.Drawing.FontStyle.Bold);
			if (status == "Present")
			{
				statusCell.Style.BackColor = System.Drawing.Color.FromArgb(232, 245, 233);
				statusCell.Style.ForeColor = HVAC_Pro_Desktop.UI.DS.Green600;
			}
			else if (status == "Absent")
			{
				statusCell.Style.BackColor = System.Drawing.Color.FromArgb(254, 242, 242);
				statusCell.Style.ForeColor = HVAC_Pro_Desktop.UI.DS.Red600;
			}
			else if (status == "Late")
			{
				statusCell.Style.BackColor = System.Drawing.Color.FromArgb(255, 247, 237);
				statusCell.Style.ForeColor = HVAC_Pro_Desktop.UI.DS.Amber600;
			}
			else if (status == "Leave")
			{
				statusCell.Style.BackColor = System.Drawing.Color.FromArgb(239, 246, 255);
				statusCell.Style.ForeColor = HVAC_Pro_Desktop.UI.DS.Primary700;
			}
			else
			{
				statusCell.Style.BackColor = HVAC_Pro_Desktop.UI.DS.Slate100;
				statusCell.Style.ForeColor = HVAC_Pro_Desktop.UI.DS.Slate600;
			}
		}

		private void ExportAttendanceGrid()
		{
			if (_gridAttendance == null || _gridAttendance.Rows.Count == 0)
			{
				SetStatus("No attendance rows are available to export.", HVAC_Pro_Desktop.UI.DS.Amber600);
				return;
			}
			using (System.Windows.Forms.SaveFileDialog dialog = new System.Windows.Forms.SaveFileDialog
			{
				Title = "Export Attendance",
				Filter = "CSV files|*.csv|All files|*.*",
				FileName = "attendance-" + CurrentYear + "-" + CurrentMonth.ToString("00") + ".csv"
			})
			{
				if (dialog.ShowDialog(this) != System.Windows.Forms.DialogResult.OK)
				{
					return;
				}
				try
				{
					System.Text.StringBuilder builder = new System.Text.StringBuilder();
					System.Collections.Generic.List<string> headers = new System.Collections.Generic.List<string>();
					foreach (System.Windows.Forms.DataGridViewColumn column in _gridAttendance.Columns)
					{
						headers.Add(EscapeCsv(column.HeaderText));
					}
					builder.AppendLine(string.Join(",", headers.ToArray()));
					foreach (System.Windows.Forms.DataGridViewRow row in _gridAttendance.Rows)
					{
						if (row.IsNewRow)
						{
							continue;
						}
						System.Collections.Generic.List<string> cells = new System.Collections.Generic.List<string>();
						foreach (System.Windows.Forms.DataGridViewCell cell in row.Cells)
						{
							cells.Add(EscapeCsv(System.Convert.ToString(cell.Value)));
						}
						builder.AppendLine(string.Join(",", cells.ToArray()));
					}
					System.IO.File.WriteAllText(dialog.FileName, builder.ToString(), System.Text.Encoding.UTF8);
					SetStatus("Attendance exported successfully.", HVAC_Pro_Desktop.UI.DS.Green600);
				}
				catch (System.Exception ex)
				{
					HVAC_Pro_Desktop.Services.AppLogger.LogError("AttendanceForm.ExportAttendanceGrid", ex);
					SetStatus("Attendance export could not be completed. Please try again.", System.Drawing.Color.Firebrick);
					System.Windows.Forms.MessageBox.Show(this, "Attendance export could not be completed. Please try again.", "Attendance Export Failed", System.Windows.Forms.MessageBoxButtons.OK, System.Windows.Forms.MessageBoxIcon.Warning);
				}
			}
		}

		private static string EscapeCsv(string value)
		{
			value = value ?? string.Empty;
			if (value.Contains(",") || value.Contains("\"") || value.Contains("\r") || value.Contains("\n"))
			{
				return "\"" + value.Replace("\"", "\"\"") + "\"";
			}
			return value;
		}

		private System.Windows.Forms.DataGridView NewGrid()
		{
			return new System.Windows.Forms.DataGridView
			{
				AllowUserToAddRows = false,
				AllowUserToDeleteRows = false,
				AllowUserToResizeRows = false,
				BackgroundColor = System.Drawing.Color.White,
				BorderStyle = System.Windows.Forms.BorderStyle.None,
				SelectionMode = System.Windows.Forms.DataGridViewSelectionMode.CellSelect,
				MultiSelect = false,
				RowHeadersVisible = false,
				EnableHeadersVisualStyles = false,
				AutoSizeRowsMode = System.Windows.Forms.DataGridViewAutoSizeRowsMode.None,
				ClipboardCopyMode = System.Windows.Forms.DataGridViewClipboardCopyMode.EnableWithoutHeaderText,
				GridColor = HVAC_Pro_Desktop.UI.DS.Border,
				ColumnHeadersDefaultCellStyle = 
				{
					BackColor = HVAC_Pro_Desktop.UI.DS.Slate100,
					ForeColor = HVAC_Pro_Desktop.UI.DS.Slate900,
					Font = new System.Drawing.Font("Segoe UI", 8.5f, System.Drawing.FontStyle.Bold)
				},
				DefaultCellStyle = 
				{
					Font = new System.Drawing.Font("Segoe UI", 8.5f),
					SelectionBackColor = HVAC_Pro_Desktop.UI.GridTheme.RowSelected,
					SelectionForeColor = HVAC_Pro_Desktop.UI.GridTheme.RowSelectedFore
				},
				RowTemplate = 
				{
					Height = 32
				}
			};
		}

		private void StyleAttendanceGrid(int daysInMonth)
		{
			if (_gridAttendance == null)
			{
				return;
			}
			_gridAttendance.CellBorderStyle = System.Windows.Forms.DataGridViewCellBorderStyle.Single;
			_gridAttendance.ColumnHeadersDefaultCellStyle.Alignment = System.Windows.Forms.DataGridViewContentAlignment.MiddleCenter;
			_gridAttendance.ColumnHeadersDefaultCellStyle.BackColor = System.Drawing.Color.White;
			_gridAttendance.ColumnHeadersDefaultCellStyle.ForeColor = HVAC_Pro_Desktop.UI.DS.Slate500;
			_gridAttendance.ColumnHeadersDefaultCellStyle.SelectionBackColor = System.Drawing.Color.White;
			_gridAttendance.ColumnHeadersDefaultCellStyle.SelectionForeColor = HVAC_Pro_Desktop.UI.DS.Slate500;
			_gridAttendance.Columns["Avatar"].Width = 42;
			_gridAttendance.Columns["Employee"].Width = 190;
			_gridAttendance.Columns["Avatar"].SortMode = System.Windows.Forms.DataGridViewColumnSortMode.NotSortable;
			_gridAttendance.Columns["Employee"].SortMode = System.Windows.Forms.DataGridViewColumnSortMode.NotSortable;
			for (int i = 1; i <= 31; i++)
			{
				System.Windows.Forms.DataGridViewColumn dataGridViewColumn = _gridAttendance.Columns["D" + i];
				if (dataGridViewColumn != null)
				{
					if (i > daysInMonth)
					{
						dataGridViewColumn.Visible = false;
						continue;
					}
					dataGridViewColumn.Visible = true;
					dataGridViewColumn.HeaderCell.Style.Alignment = System.Windows.Forms.DataGridViewContentAlignment.MiddleCenter;
					dataGridViewColumn.Width = 30;
					dataGridViewColumn.MinimumWidth = 30;
					dataGridViewColumn.HeaderCell.Style.BackColor = System.Drawing.Color.White;
					dataGridViewColumn.HeaderCell.Style.ForeColor = HVAC_Pro_Desktop.UI.DS.Slate500;
					dataGridViewColumn.HeaderCell.Style.SelectionBackColor = System.Drawing.Color.White;
					dataGridViewColumn.HeaderCell.Style.SelectionForeColor = HVAC_Pro_Desktop.UI.DS.Slate500;
				}
			}
			foreach (System.Windows.Forms.DataGridViewRow item in (System.Collections.IEnumerable)_gridAttendance.Rows)
			{
				if (!item.IsNewRow)
				{
					item.DefaultCellStyle.BackColor = System.Drawing.Color.White;
					item.DefaultCellStyle.SelectionBackColor = System.Drawing.Color.White;
					item.DefaultCellStyle.SelectionForeColor = HVAC_Pro_Desktop.UI.DS.Slate800;
					item.Height = 36;
					ApplyAttendanceRowStyle(item);
					for (int j = 1; j <= daysInMonth; j++)
					{
						System.Windows.Forms.DataGridViewCell dataGridViewCell = item.Cells["D" + j];
						ApplyAttendanceCellStyle(dataGridViewCell, System.Convert.ToString(dataGridViewCell.Value));
					}
				}
			}
		}

		private void SetStatus(string message, System.Drawing.Color color)
		{
			if (_lblStatus != null)
			{
				_lblStatus.Text = message;
				_lblStatus.ForeColor = color;
			}
		}

		private static bool IsAttendanceExceptionStatus(string status)
		{
			string text = NormalizeAttendanceStatus(status);
			if (!string.IsNullOrWhiteSpace(text) && !string.Equals(text, "ABSENT", System.StringComparison.OrdinalIgnoreCase) && !string.Equals(text, "LEAVE", System.StringComparison.OrdinalIgnoreCase))
			{
				return string.Equals(text, "HALFDAY", System.StringComparison.OrdinalIgnoreCase);
			}
			return true;
		}

		private static bool IsVisibleAttendanceEmployee(HVAC_Pro_Desktop.Models.Employee employee)
		{
			if (employee == null)
			{
				return false;
			}
			if (!string.Equals(employee.Status ?? string.Empty, "Active", System.StringComparison.OrdinalIgnoreCase))
			{
				return false;
			}
			string text = (employee.Name ?? string.Empty).Trim();
			if (string.IsNullOrWhiteSpace(text))
			{
				return false;
			}
			if (string.Equals(text, "Blank", System.StringComparison.OrdinalIgnoreCase))
			{
				return false;
			}
			return true;
		}

		private static string NormalizeAttendanceStatus(string status)
		{
			return (status ?? string.Empty).Trim().Replace(" ", string.Empty).Replace("-", string.Empty)
				.Replace("_", string.Empty)
				.Replace("/", string.Empty)
				.ToUpperInvariant();
		}

		private static string ToAttendanceDisplayStatus(string status)
		{
			string normalized = NormalizeAttendanceStatus(status);
			if (normalized == "PRESENT" || normalized == "P")
			{
				return "Present";
			}
			if (normalized == "ABSENT" || normalized == "A")
			{
				return "Absent";
			}
			if (normalized == "LATE" || normalized == "L")
			{
				return "Late";
			}
			if (normalized == "LEAVE" || normalized == "LV")
			{
				return "Leave";
			}
			return "Pending";
		}

		private static string BuildImportSummary(HVAC_Pro_Desktop.Models.ServiceResult<HVAC_Pro_Desktop.Models.PayrollImportReport> result)
		{
			if (result == null)
			{
				return "Attendance import could not be completed.";
			}
			if (!result.Success || result.Data == null)
			{
				return result?.Message ?? "Attendance import failed.";
			}
			HVAC_Pro_Desktop.Models.PayrollImportReport data = result.Data;
			return string.Join(System.Environment.NewLine, System.Linq.Enumerable.Where(new string[7]
			{
				result.Message,
				string.Empty,
				"Payroll entries imported: " + data.PayrollEntriesImported,
				"Attendance records imported: " + data.AttendanceRecordsImported,
				"Employees matched: " + data.EmployeesMatched,
				"New employees created: " + data.NewEmployeesCreated,
				"Warnings: " + data.Warnings.Count
			}, (string line) => !string.IsNullOrWhiteSpace(line)));
		}
	}
}
