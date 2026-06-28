namespace HVAC_Pro_Desktop.UI
{
	internal enum SharedPageHeaderMode
	{
		Editor,
		Dashboard
	}

	internal sealed class SharedPageHeaderModel
	{
		public string Name { get; set; }

		public System.Windows.Forms.DockStyle Dock { get; set; } = System.Windows.Forms.DockStyle.Top;

		public System.Drawing.Color BackColor { get; set; } = HVAC_Pro_Desktop.UI.DS.BgPage;

		public System.Windows.Forms.Padding Padding { get; set; } = new System.Windows.Forms.Padding(24, 14, 24, 10);

		public string Title { get; set; }

		public string Subtitle { get; set; }

		public string StatusText { get; set; }

		public System.Drawing.Color StatusColor { get; set; } = HVAC_Pro_Desktop.UI.DS.Slate500;

		public HVAC_Pro_Desktop.UI.SharedPageHeaderMode Mode { get; set; }

		public int DefaultHeight { get; set; } = 86;

		public int CompactHeight { get; set; } = 110;

		public int CompactBreakpoint { get; set; } = 980;

		public bool AllowCompactWrap { get; set; }

		public bool DrawBottomBorder { get; set; } = true;

		public int TitleWidth { get; set; } = 420;

		public int SubtitleWidth { get; set; } = 620;

		public int MinTextWidth { get; set; } = 220;

		public int TitleTop { get; set; }

		public int SubtitleTop { get; set; } = 34;

		public int StatusTop { get; set; } = 56;

		public int ActionTop { get; set; } = 20;

		public int CompactActionTop { get; set; } = 68;

		public int ActionGap { get; set; } = 10;

		public int SectionGap { get; set; } = 12;

		public int CenterTop { get; set; } = 20;

		public int CompactCenterTop { get; set; } = 68;

		public System.Windows.Forms.Control CenterContent { get; set; }

		public System.Windows.Forms.Control RightMetaControl { get; set; }

		public System.Collections.Generic.IList<System.Windows.Forms.Control> RightActions { get; set; } = new System.Collections.Generic.List<System.Windows.Forms.Control>();
	}

	internal sealed class SharedPageHeaderResult
	{
		public System.Windows.Forms.Panel Header { get; set; }

		public System.Windows.Forms.Label TitleLabel { get; set; }

		public System.Windows.Forms.Label SubtitleLabel { get; set; }

		public System.Windows.Forms.Label StatusLabel { get; set; }

		public System.Windows.Forms.Panel ActionHost { get; set; }
	}

	internal static class SharedPageHeader
	{
		private const int WorkspaceTitleWidth = 400;

		private const int WorkspaceSubtitleWidth = 620;

		private const int WorkspaceMinTextWidth = 260;

		private const int WorkspaceSearchWidth = 320;

		private const int WorkspaceSearchHeight = 36;

		public static HVAC_Pro_Desktop.UI.SharedPageHeaderModel CreateWorkspaceDashboard(string name, string title, string subtitle, System.Collections.Generic.IList<System.Windows.Forms.Control> rightActions, System.Windows.Forms.Control centerContent = null, System.Windows.Forms.Control rightMetaControl = null, System.Drawing.Color? backColor = null, System.Windows.Forms.Padding? padding = null)
		{
			return new HVAC_Pro_Desktop.UI.SharedPageHeaderModel
			{
				Name = name,
				Mode = HVAC_Pro_Desktop.UI.SharedPageHeaderMode.Dashboard,
				Dock = System.Windows.Forms.DockStyle.Fill,
				BackColor = (backColor ?? HVAC_Pro_Desktop.UI.DS.BgPage),
				Padding = (padding ?? new System.Windows.Forms.Padding(0, 8, 0, 12)),
				Title = title,
				Subtitle = subtitle,
				TitleWidth = 400,
				SubtitleWidth = 620,
				MinTextWidth = 260,
				ActionGap = 8,
				SectionGap = 16,
				AllowCompactWrap = true,
				CompactBreakpoint = 1280,
				CompactHeight = 126,
				CenterContent = centerContent,
				RightMetaControl = rightMetaControl,
				RightActions = (rightActions ?? new System.Collections.Generic.List<System.Windows.Forms.Control>())
			};
		}

		public static HVAC_Pro_Desktop.UI.SharedPageHeaderModel CreateWorkspaceEditor(string name, string title, string subtitle, System.Collections.Generic.IList<System.Windows.Forms.Control> rightActions, System.Windows.Forms.Control centerContent = null, string statusText = null, System.Drawing.Color? statusColor = null, System.Windows.Forms.Control rightMetaControl = null)
		{
			return new HVAC_Pro_Desktop.UI.SharedPageHeaderModel
			{
				Name = name,
				Mode = HVAC_Pro_Desktop.UI.SharedPageHeaderMode.Editor,
				BackColor = HVAC_Pro_Desktop.UI.DS.BgPage,
				Padding = new System.Windows.Forms.Padding(28, 16, 28, 10),
				Title = title,
				Subtitle = subtitle,
				StatusText = statusText,
				StatusColor = (statusColor ?? HVAC_Pro_Desktop.UI.DS.Slate500),
				TitleWidth = 400,
				SubtitleWidth = 620,
				MinTextWidth = 260,
				ActionGap = 8,
				SectionGap = 16,
				AllowCompactWrap = true,
				CompactBreakpoint = 1220,
				CenterContent = centerContent,
				RightMetaControl = rightMetaControl,
				RightActions = (rightActions ?? new System.Collections.Generic.List<System.Windows.Forms.Control>())
			};
		}

		public static HVAC_Pro_Desktop.UI.SharedPageHeaderModel CreateSalesEditor(string name, string title, string subtitle, System.Collections.Generic.IList<System.Windows.Forms.Control> rightActions, string statusText = null, System.Drawing.Color? statusColor = null, System.Windows.Forms.Control rightMetaControl = null, System.Windows.Forms.Control centerContent = null)
		{
			return CreateWorkspaceEditor(name, title, subtitle, rightActions, centerContent, statusText, statusColor, rightMetaControl);
		}

		public static HVAC_Pro_Desktop.UI.SharedPageHeaderModel CreateSalesDashboard(string name, string title, string subtitle, System.Collections.Generic.IList<System.Windows.Forms.Control> rightActions, System.Windows.Forms.Control centerContent = null, System.Windows.Forms.Control rightMetaControl = null, System.Drawing.Color? backColor = null, System.Windows.Forms.Padding? padding = null)
		{
			return CreateWorkspaceDashboard(name, title, subtitle, rightActions, centerContent, rightMetaControl, backColor, padding ?? new System.Windows.Forms.Padding(0, 0, 0, 8));
		}

		public static System.Windows.Forms.Panel CreateSearchCommand(string name, int width = 320, string placeholder = "Search", string shortcutText = "Ctrl + K", System.Action onClick = null)
		{
			System.Windows.Forms.Panel host = new System.Windows.Forms.Panel
			{
				Name = name,
				Size = new System.Drawing.Size(width, 36),
				BackColor = System.Drawing.Color.White,
				Cursor = ((onClick == null) ? System.Windows.Forms.Cursors.Default : System.Windows.Forms.Cursors.Hand)
			};
			host.Paint += delegate(object s, System.Windows.Forms.PaintEventArgs e)
			{
				e.Graphics.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;
				using (System.Drawing.Drawing2D.GraphicsPath path = HVAC_Pro_Desktop.UI.DS.RoundedRect(new System.Drawing.Rectangle(0, 0, host.Width - 1, host.Height - 1), 9))
				{
					using (System.Drawing.Pen pen = new System.Drawing.Pen(HVAC_Pro_Desktop.UI.DS.BorderStrong))
					{
						e.Graphics.DrawPath(pen, path);
					}
				}
			};
			System.Windows.Forms.Label label = HVAC_Pro_Desktop.UI.ModernIconSystem.Icon(HVAC_Pro_Desktop.UI.ModernIconKind.Search, 18, HVAC_Pro_Desktop.UI.DS.Slate700);
			label.Location = new System.Drawing.Point(12, 8);
			label.Size = new System.Drawing.Size(20, 20);
			System.Windows.Forms.Label value = new System.Windows.Forms.Label
			{
				Text = string.Equals(placeholder, "Search", System.StringComparison.OrdinalIgnoreCase) ? string.Empty : placeholder,
				Location = new System.Drawing.Point(40, 9),
				AutoSize = true,
				Font = new System.Drawing.Font("Segoe UI", 8.7f),
				ForeColor = HVAC_Pro_Desktop.UI.DS.Slate700,
				BackColor = System.Drawing.Color.White
			};
			System.Windows.Forms.Label shortcut = null;
			if (!string.IsNullOrWhiteSpace(shortcutText))
			{
				shortcut = new System.Windows.Forms.Label
				{
					Text = shortcutText,
					Size = new System.Drawing.Size(62, 22),
					BackColor = System.Drawing.Color.FromArgb(248, 250, 252),
					Font = new System.Drawing.Font("Segoe UI", 7.5f, System.Drawing.FontStyle.Bold),
					ForeColor = HVAC_Pro_Desktop.UI.DS.Slate700,
					TextAlign = System.Drawing.ContentAlignment.MiddleCenter,
					Anchor = (System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Right)
				};
				shortcut.Paint += delegate(object s, System.Windows.Forms.PaintEventArgs e)
				{
					e.Graphics.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;
					using (System.Drawing.Drawing2D.GraphicsPath path = HVAC_Pro_Desktop.UI.DS.RoundedRect(new System.Drawing.Rectangle(0, 0, shortcut.Width - 1, shortcut.Height - 1), 4))
					{
						using (System.Drawing.Pen pen = new System.Drawing.Pen(HVAC_Pro_Desktop.UI.DS.BorderStrong))
						{
							e.Graphics.DrawPath(pen, path);
						}
					}
				};
				host.Resize += delegate
				{
					shortcut.Location = new System.Drawing.Point(System.Math.Max(8, host.Width - shortcut.Width - 10), 7);
				};
			}
			host.Controls.Add(label);
			host.Controls.Add(value);
			if (shortcut != null)
			{
				host.Controls.Add(shortcut);
				shortcut.BringToFront();
				shortcut.Location = new System.Drawing.Point(System.Math.Max(8, host.Width - shortcut.Width - 10), 7);
			}
			if (onClick != null)
			{
				WireClick(host, onClick);
			}
			return host;
		}

		public static System.Windows.Forms.Panel CreateSearchInputShell(string name, System.Windows.Forms.Control input, int width = 320)
		{
			if (input == null)
			{
				throw new System.ArgumentNullException("input");
			}
			System.Windows.Forms.Panel host = new System.Windows.Forms.Panel
			{
				Name = name,
				Size = new System.Drawing.Size(width, 36),
				MinimumSize = new System.Drawing.Size(System.Math.Min(width, 180), 36),
				BackColor = System.Drawing.Color.White,
				Padding = new System.Windows.Forms.Padding(12, 6, 12, 6)
			};
			host.Paint += delegate(object s, System.Windows.Forms.PaintEventArgs e)
			{
				e.Graphics.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;
				using (System.Drawing.Drawing2D.GraphicsPath path = HVAC_Pro_Desktop.UI.DS.RoundedRect(new System.Drawing.Rectangle(0, 0, host.Width - 1, host.Height - 1), 9))
				{
					using (System.Drawing.Pen pen = new System.Drawing.Pen(HVAC_Pro_Desktop.UI.DS.BorderStrong))
					{
						e.Graphics.DrawPath(pen, path);
					}
				}
			};
			System.Windows.Forms.Label label = HVAC_Pro_Desktop.UI.ModernIconSystem.Icon(HVAC_Pro_Desktop.UI.ModernIconKind.Search, 18, HVAC_Pro_Desktop.UI.DS.Slate700);
			label.Location = new System.Drawing.Point(12, 8);
			label.Size = new System.Drawing.Size(20, 20);
			input.Dock = System.Windows.Forms.DockStyle.Fill;
			input.Margin = System.Windows.Forms.Padding.Empty;
			if (input is System.Windows.Forms.TextBox textBox)
			{
				textBox.BorderStyle = System.Windows.Forms.BorderStyle.None;
				textBox.BackColor = System.Drawing.Color.White;
			}
			System.Windows.Forms.Panel panel = new System.Windows.Forms.Panel
			{
				Location = new System.Drawing.Point(54, 6),
				Size = new System.Drawing.Size(System.Math.Max(96, width - 70), 24),
				Anchor = (System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Left | System.Windows.Forms.AnchorStyles.Right),
				BackColor = System.Drawing.Color.White
			};
			host.Resize += delegate
			{
				panel.Width = System.Math.Max(96, host.ClientSize.Width - panel.Left - 12);
			};
			input.HandleCreated += delegate
			{
				input.BeginInvoke((System.Action)delegate
				{
					System.Windows.Forms.TextBoxBase textInput = input as System.Windows.Forms.TextBoxBase;
					if (textInput != null && !textInput.IsDisposed && !string.IsNullOrEmpty(textInput.Text))
					{
						textInput.SelectionStart = 0;
						textInput.SelectionLength = 0;
					}
				});
			};
			panel.Controls.Add(input);
			host.Controls.Add(label);
			host.Controls.Add(panel);
			host.Click += delegate
			{
				input.Focus();
			};
			foreach (System.Windows.Forms.Control control in host.Controls)
			{
				control.Click += delegate
				{
					input.Focus();
				};
			}
			return host;
		}

		public static HVAC_Pro_Desktop.UI.SharedPageHeaderResult Build(HVAC_Pro_Desktop.UI.SharedPageHeaderModel model)
		{
			if (model == null)
			{
				throw new System.ArgumentNullException("model");
			}
			NormalizeModel(model);
			System.Windows.Forms.Panel header = new System.Windows.Forms.Panel
			{
				Name = (model.Name ?? string.Empty),
				Dock = model.Dock,
				Height = model.DefaultHeight,
				BackColor = model.BackColor,
				Padding = model.Padding,
				Tag = "custom-header-actions no-global-actions"
			};
			if (model.DrawBottomBorder)
			{
				header.Paint += delegate(object s, System.Windows.Forms.PaintEventArgs e)
				{
					using (System.Drawing.Pen pen = new System.Drawing.Pen(HVAC_Pro_Desktop.UI.DS.Slate200))
					{
						e.Graphics.DrawLine(pen, 0, header.Height - 1, header.Width, header.Height - 1);
					}
				};
			}
			System.Windows.Forms.Label title = new System.Windows.Forms.Label
			{
				Text = (model.Title ?? string.Empty),
				Location = new System.Drawing.Point(model.Padding.Left, model.Padding.Top + model.TitleTop),
				Size = new System.Drawing.Size(model.TitleWidth, 30),
				Font = new System.Drawing.Font("Segoe UI", 18f, System.Drawing.FontStyle.Bold),
				ForeColor = HVAC_Pro_Desktop.UI.DS.Slate900,
				TextAlign = System.Drawing.ContentAlignment.MiddleLeft,
				AutoEllipsis = true
			};
			System.Windows.Forms.Label subtitle = new System.Windows.Forms.Label
			{
				Text = (model.Subtitle ?? string.Empty),
				Location = new System.Drawing.Point(model.Padding.Left + 1, model.Padding.Top + model.SubtitleTop),
				Size = new System.Drawing.Size(model.SubtitleWidth, 22),
				Font = HVAC_Pro_Desktop.UI.DS.Body,
				ForeColor = HVAC_Pro_Desktop.UI.DS.Slate600,
				TextAlign = System.Drawing.ContentAlignment.MiddleLeft,
				AutoEllipsis = true
			};
			System.Windows.Forms.Label status = null;
			if (!string.IsNullOrWhiteSpace(model.StatusText))
			{
				status = new System.Windows.Forms.Label
				{
					Text = model.StatusText,
					Location = new System.Drawing.Point(model.Padding.Left + 1, model.Padding.Top + model.StatusTop),
					Size = new System.Drawing.Size(model.SubtitleWidth, 18),
					Font = HVAC_Pro_Desktop.UI.DS.Small,
					ForeColor = model.StatusColor,
					TextAlign = System.Drawing.ContentAlignment.MiddleLeft,
					AutoEllipsis = true
				};
			}
			System.Windows.Forms.Panel actionHost = new System.Windows.Forms.Panel
			{
				Name = (model.Name ?? "SharedPageHeader") + "Actions",
				BackColor = System.Drawing.Color.Transparent,
				Height = 38,
				Width = 0
			};
			foreach (System.Windows.Forms.Control item in System.Linq.Enumerable.Where(model.RightActions, (System.Windows.Forms.Control action) => action != null))
			{
				item.Margin = System.Windows.Forms.Padding.Empty;
				item.Anchor = System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Left;
				actionHost.Controls.Add(item);
			}
			header.Controls.Add(title);
			header.Controls.Add(subtitle);
			if (status != null)
			{
				header.Controls.Add(status);
			}
			if (model.CenterContent != null)
			{
				model.CenterContent.Anchor = System.Windows.Forms.AnchorStyles.Top;
				header.Controls.Add(model.CenterContent);
			}
			if (model.RightMetaControl != null)
			{
				model.RightMetaControl.Anchor = System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Right;
				if (model.RightMetaControl is System.Windows.Forms.Label label && string.IsNullOrWhiteSpace(label.Text))
				{
					model.RightMetaControl.Visible = false;
				}
				header.Controls.Add(model.RightMetaControl);
			}
			header.Controls.Add(actionHost);
			System.Action layout = delegate
			{
				bool flag = model.AllowCompactWrap && header.ClientSize.Width < model.CompactBreakpoint;
				header.Height = (flag ? model.CompactHeight : model.DefaultHeight);
				int num = (flag ? model.CompactActionTop : model.ActionTop);
				int y = (flag ? model.CompactCenterTop : model.CenterTop);
				System.Collections.Generic.List<System.Windows.Forms.Control> list = System.Linq.Enumerable.ToList(System.Linq.Enumerable.Where(model.RightActions, (System.Windows.Forms.Control action) => action != null && !action.IsDisposed && action.Visible));
				int width = HVAC_Pro_Desktop.UI.SharedUiPrimitives.MeasureVisibleControlSpan(list, model.ActionGap);
				int height = System.Math.Max(38, (list.Count != 0) ? (System.Linq.Enumerable.Max(list, (System.Windows.Forms.Control action) => action.Height) + 4) : 0);
				actionHost.SuspendLayout();
				actionHost.Size = new System.Drawing.Size(width, height);
				actionHost.Location = new System.Drawing.Point(System.Math.Max(model.Padding.Left, header.ClientSize.Width - model.Padding.Right - actionHost.Width), num);
				HVAC_Pro_Desktop.UI.SharedUiPrimitives.LayoutVisibleControlsLeftToRightCentered(list, 0, actionHost.Height, model.ActionGap);
				actionHost.ResumeLayout(false);
				actionHost.Invalidate(true);
				int num2 = actionHost.Left - model.SectionGap;
				if (model.RightMetaControl != null && model.RightMetaControl.Visible)
				{
					int y2 = num + System.Math.Max(0, (actionHost.Height - model.RightMetaControl.Height) / 2);
					model.RightMetaControl.Location = new System.Drawing.Point(System.Math.Max(model.Padding.Left, num2 - model.RightMetaControl.Width), y2);
					num2 = model.RightMetaControl.Left - model.SectionGap;
				}
				if (model.CenterContent != null && model.CenterContent.Visible)
				{
					int num3 = (flag ? model.Padding.Left : (model.Padding.Left + 220));
					int num4 = (flag ? num2 : num2);
					int val = System.Math.Max(140, num4 - num3);
					int val2 = (flag ? System.Math.Min(model.MinTextWidth, val) : model.MinTextWidth);
					int num5 = System.Math.Min(model.CenterContent.Width, System.Math.Max(val2, val));
					model.CenterContent.Width = num5;
					model.CenterContent.Location = new System.Drawing.Point(HVAC_Pro_Desktop.UI.SharedUiPrimitives.CenterControlX(header.ClientSize.Width, num5, num3, num4), y);
					num2 = System.Math.Min(num2, model.CenterContent.Left - model.SectionGap);
				}
				int num6 = (flag ? (header.ClientSize.Width - model.Padding.Right) : num2);
				int width2 = System.Math.Max(model.MinTextWidth, num6 - title.Left);
				title.Width = width2;
				subtitle.Width = System.Math.Max(model.MinTextWidth, num6 - subtitle.Left);
				if (status != null)
				{
					status.Width = System.Math.Max(model.MinTextWidth, num6 - status.Left);
				}
			};
			header.Resize += delegate
			{
				layout();
			};
			foreach (System.Windows.Forms.Control item2 in System.Linq.Enumerable.Where(model.RightActions, (System.Windows.Forms.Control action) => action != null))
			{
				item2.VisibleChanged += delegate
				{
					layout();
				};
				item2.SizeChanged += delegate
				{
					layout();
				};
				item2.TextChanged += delegate
				{
					layout();
				};
			}
			if (model.RightMetaControl != null)
			{
				model.RightMetaControl.VisibleChanged += delegate
				{
					layout();
				};
				model.RightMetaControl.SizeChanged += delegate
				{
					layout();
				};
				model.RightMetaControl.TextChanged += delegate
				{
					if (model.RightMetaControl is System.Windows.Forms.Label label2)
					{
						model.RightMetaControl.Visible = !string.IsNullOrWhiteSpace(label2.Text);
					}
					layout();
				};
			}
			if (model.CenterContent != null)
			{
				model.CenterContent.VisibleChanged += delegate
				{
					layout();
				};
				model.CenterContent.SizeChanged += delegate
				{
					layout();
				};
				model.CenterContent.TextChanged += delegate
				{
					layout();
				};
			}
			layout();
			return new HVAC_Pro_Desktop.UI.SharedPageHeaderResult
			{
				Header = header,
				TitleLabel = title,
				SubtitleLabel = subtitle,
				StatusLabel = status,
				ActionHost = actionHost
			};
		}

		private static void NormalizeModel(HVAC_Pro_Desktop.UI.SharedPageHeaderModel model)
		{
			if (model.ActionGap == 10)
			{
				model.ActionGap = 12;
			}
			if (model.SectionGap == 12)
			{
				model.SectionGap = 16;
			}
			switch (model.Mode)
			{
			case HVAC_Pro_Desktop.UI.SharedPageHeaderMode.Dashboard:
				if (model.DefaultHeight == 86)
				{
					model.DefaultHeight = 88;
				}
				if (model.CompactHeight == 110)
				{
					model.CompactHeight = 122;
				}
				if (model.CompactBreakpoint == 980)
				{
					model.CompactBreakpoint = 1180;
				}
				if (model.ActionTop == 20)
				{
					model.ActionTop = 18;
				}
				if (model.CompactActionTop == 68)
				{
					model.CompactActionTop = 72;
				}
				break;
			case HVAC_Pro_Desktop.UI.SharedPageHeaderMode.Editor:
				if (model.Padding == new System.Windows.Forms.Padding(24, 14, 24, 10))
				{
					model.Padding = new System.Windows.Forms.Padding(28, 16, 28, 10);
				}
				if (model.DefaultHeight == 86)
				{
					model.DefaultHeight = 94;
				}
				if (model.CompactHeight == 110)
				{
					model.CompactHeight = 132;
				}
				if (model.CompactBreakpoint == 980)
				{
					model.CompactBreakpoint = 1180;
				}
				if (model.ActionTop == 20)
				{
					model.ActionTop = 18;
				}
				if (model.CompactActionTop == 68)
				{
					model.CompactActionTop = 74;
				}
				break;
			}
		}

		private static void WireClick(System.Windows.Forms.Control root, System.Action onClick)
		{
			root.Click += delegate
			{
				onClick();
			};
			foreach (System.Windows.Forms.Control control in root.Controls)
			{
				WireClick(control, onClick);
			}
		}
	}
}
