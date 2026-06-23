namespace HVAC_Pro_Desktop.UI
{
	internal sealed class WorkforceMetricCardResult
	{
		public System.Windows.Forms.Panel Card { get; set; }

		public System.Windows.Forms.Label ValueLabel { get; set; }

		public System.Windows.Forms.Label CaptionLabel { get; set; }
	}

	internal static class WorkforceModuleVisuals
	{
		public static System.Windows.Forms.Panel CreateSectionCard(string title, string detail, System.Windows.Forms.Control body, System.Windows.Forms.Control action = null, int minHeight = 180)
		{
			System.Windows.Forms.Panel card = new System.Windows.Forms.Panel
			{
				Dock = System.Windows.Forms.DockStyle.Fill,
				BackColor = System.Drawing.Color.White,
				Margin = new System.Windows.Forms.Padding(0),
				Padding = new System.Windows.Forms.Padding(0),
				MinimumSize = new System.Drawing.Size(220, minHeight)
			};
			HVAC_Pro_Desktop.UI.DS.Rounded(card, 10);
			card.Paint += delegate(object s, System.Windows.Forms.PaintEventArgs e)
			{
				e.Graphics.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;
				using (System.Drawing.Drawing2D.GraphicsPath path = HVAC_Pro_Desktop.UI.DS.RoundedRect(new System.Drawing.Rectangle(0, 0, card.Width - 1, card.Height - 1), 10))
				{
					using (System.Drawing.Pen pen = new System.Drawing.Pen(System.Drawing.Color.FromArgb(223, 229, 238)))
					{
						e.Graphics.DrawPath(pen, path);
					}
				}
			};
			System.Windows.Forms.Panel header = new System.Windows.Forms.Panel
			{
				Dock = System.Windows.Forms.DockStyle.Top,
				Height = 72,
				BackColor = System.Drawing.Color.White,
				Padding = new System.Windows.Forms.Padding(12, 12, 12, 8)
			};
			System.Windows.Forms.Label titleLabel = new System.Windows.Forms.Label
			{
				Text = title,
				Location = new System.Drawing.Point(0, 0),
				Size = new System.Drawing.Size(320, 22),
				Font = new System.Drawing.Font("Segoe UI Semibold", 10.5f, System.Drawing.FontStyle.Bold),
				ForeColor = HVAC_Pro_Desktop.UI.DS.Slate900
			};
			System.Windows.Forms.Label detailLabel = new System.Windows.Forms.Label
			{
				Text = detail,
				Location = new System.Drawing.Point(0, 28),
				Size = new System.Drawing.Size(520, 30),
				Font = new System.Drawing.Font("Segoe UI", 8.75f),
				ForeColor = HVAC_Pro_Desktop.UI.DS.Slate500
			};
			header.Resize += delegate
			{
				int num = ((action != null && action.Visible) ? (action.Width + 16) : 0);
				int num2 = System.Math.Max(200, header.ClientSize.Width - num);
				titleLabel.Width = System.Math.Min(num2, 420);
				detailLabel.Width = num2;
				if (action != null)
				{
					action.Location = new System.Drawing.Point(System.Math.Max(0, header.ClientSize.Width - action.Width), 8);
				}
			};
			header.Controls.Add(titleLabel);
			header.Controls.Add(detailLabel);
			if (action != null)
			{
				action.Anchor = System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Right;
				header.Controls.Add(action);
			}
			System.Windows.Forms.Panel panel = new System.Windows.Forms.Panel
			{
				Dock = System.Windows.Forms.DockStyle.Fill,
				BackColor = System.Drawing.Color.White,
				Padding = new System.Windows.Forms.Padding(12, 0, 12, 12)
			};
			if (body != null)
			{
				body.Dock = System.Windows.Forms.DockStyle.Fill;
				panel.Controls.Add(body);
			}
			card.Controls.Add(panel);
			card.Controls.Add(header);
			return card;
		}

		public static HVAC_Pro_Desktop.UI.WorkforceMetricCardResult CreateMetricCard(string title, string value, string caption, System.Drawing.Color accent, bool filled = false)
		{
			System.Windows.Forms.Panel card = new System.Windows.Forms.Panel
			{
				Dock = System.Windows.Forms.DockStyle.Fill,
				Margin = new System.Windows.Forms.Padding(0),
				Padding = new System.Windows.Forms.Padding(12, 12, 12, 12),
				BackColor = (filled ? accent : System.Drawing.Color.White),
				MinimumSize = new System.Drawing.Size(180, 96)
			};
			HVAC_Pro_Desktop.UI.DS.Rounded(card);
			card.Paint += delegate(object s, System.Windows.Forms.PaintEventArgs e)
			{
				e.Graphics.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;
				using (System.Drawing.Drawing2D.GraphicsPath path = HVAC_Pro_Desktop.UI.DS.RoundedRect(new System.Drawing.Rectangle(0, 0, card.Width - 1, card.Height - 1), 8))
				{
					using (System.Drawing.Pen pen = new System.Drawing.Pen(filled ? HVAC_Pro_Desktop.UI.DS.Darken(accent, 0.14f) : System.Drawing.Color.FromArgb(223, 229, 238)))
					{
						e.Graphics.DrawPath(pen, path);
					}
				}
				if (!filled)
				{
					using (System.Drawing.SolidBrush brush = new System.Drawing.SolidBrush(accent))
					{
						e.Graphics.FillRectangle(brush, 0, 0, card.Width, 4);
					}
				}
			};
			System.Windows.Forms.Panel value2 = new System.Windows.Forms.Panel
			{
				Location = new System.Drawing.Point(18, 72),
				Size = new System.Drawing.Size(56, 3),
				BackColor = (filled ? System.Drawing.Color.FromArgb(210, System.Drawing.Color.White) : accent)
			};
			System.Windows.Forms.Label value3 = new System.Windows.Forms.Label
			{
				Text = title,
				Location = new System.Drawing.Point(18, 14),
				Size = new System.Drawing.Size(180, 18),
				Font = new System.Drawing.Font("Segoe UI", 8f, System.Drawing.FontStyle.Bold),
				ForeColor = (filled ? System.Drawing.Color.FromArgb(235, 245, 255) : HVAC_Pro_Desktop.UI.DS.Slate600)
			};
			System.Windows.Forms.Label label = new System.Windows.Forms.Label
			{
				Text = value,
				Location = new System.Drawing.Point(18, 34),
				Size = new System.Drawing.Size(180, 30),
				Font = new System.Drawing.Font("Segoe UI Semibold", 16f, System.Drawing.FontStyle.Bold),
				ForeColor = (filled ? System.Drawing.Color.White : HVAC_Pro_Desktop.UI.DS.Slate900)
			};
			System.Windows.Forms.Label label2 = new System.Windows.Forms.Label
			{
				Text = caption,
				Location = new System.Drawing.Point(18, 82),
				Size = new System.Drawing.Size(220, 30),
				Font = new System.Drawing.Font("Segoe UI", 8.5f),
				ForeColor = (filled ? System.Drawing.Color.FromArgb(229, 237, 246) : HVAC_Pro_Desktop.UI.DS.Slate500)
			};
			card.Controls.Add(value2);
			card.Controls.Add(value3);
			card.Controls.Add(label);
			card.Controls.Add(label2);
			return new HVAC_Pro_Desktop.UI.WorkforceMetricCardResult
			{
				Card = card,
				ValueLabel = label,
				CaptionLabel = label2
			};
		}

		public static System.Windows.Forms.FlowLayoutPanel CreateChipRow()
		{
			return new System.Windows.Forms.FlowLayoutPanel
			{
				AutoSize = true,
				WrapContents = true,
				FlowDirection = System.Windows.Forms.FlowDirection.LeftToRight,
				BackColor = System.Drawing.Color.Transparent,
				Margin = System.Windows.Forms.Padding.Empty,
				Padding = System.Windows.Forms.Padding.Empty
			};
		}

		public static System.Windows.Forms.Label CreateChip(string text, System.Drawing.Color backColor, System.Drawing.Color foreColor, int width = 0)
		{
			System.Windows.Forms.Label obj = new System.Windows.Forms.Label
			{
				Text = text,
				AutoSize = (width <= 0),
				Width = ((width > 0) ? width : 0),
				Height = 28,
				Margin = new System.Windows.Forms.Padding(0, 0, 8, 8),
				Padding = new System.Windows.Forms.Padding(10, 6, 10, 0),
				BackColor = backColor,
				ForeColor = foreColor,
				Font = new System.Drawing.Font("Segoe UI", 8.5f, System.Drawing.FontStyle.Bold),
				TextAlign = System.Drawing.ContentAlignment.MiddleLeft
			};
			HVAC_Pro_Desktop.UI.DS.Rounded(obj, 14);
			return obj;
		}

		public static System.Windows.Forms.Panel CreateHeroCard(string eyebrow, string title, string detail, System.Drawing.Color accent, System.Collections.Generic.IEnumerable<System.Windows.Forms.Control> actions, out System.Windows.Forms.Label titleLabel, out System.Windows.Forms.Label detailLabel, out System.Windows.Forms.FlowLayoutPanel chipRow)
		{
			System.Windows.Forms.Panel card = new System.Windows.Forms.Panel
			{
				BackColor = System.Drawing.Color.White,
				Margin = System.Windows.Forms.Padding.Empty,
				Padding = new System.Windows.Forms.Padding(18, 16, 18, 16),
				Height = 156
			};
			HVAC_Pro_Desktop.UI.DS.Rounded(card, 10);
			card.Paint += delegate(object s, System.Windows.Forms.PaintEventArgs e)
			{
				System.Drawing.Rectangle rectangle = new System.Drawing.Rectangle(0, 0, card.Width - 1, card.Height - 1);
				e.Graphics.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;
				using (System.Drawing.Drawing2D.GraphicsPath path = HVAC_Pro_Desktop.UI.DS.RoundedRect(rectangle, 10))
				{
					using (System.Drawing.Drawing2D.LinearGradientBrush brush = new System.Drawing.Drawing2D.LinearGradientBrush(rectangle, System.Drawing.Color.FromArgb(248, 251, 255), System.Drawing.Color.FromArgb(255, 255, 255), System.Drawing.Drawing2D.LinearGradientMode.ForwardDiagonal))
					{
						using (System.Drawing.Pen pen = new System.Drawing.Pen(System.Drawing.Color.FromArgb(220, 228, 238)))
						{
							e.Graphics.FillPath(brush, path);
							e.Graphics.DrawPath(pen, path);
						}
					}
				}
				using (System.Drawing.SolidBrush brush2 = new System.Drawing.SolidBrush(accent))
				{
					e.Graphics.FillRectangle(brush2, 0, 0, 7, card.Height);
				}
			};
			System.Windows.Forms.Label eyebrowLabel = new System.Windows.Forms.Label
			{
				Text = eyebrow,
				Location = new System.Drawing.Point(0, 0),
				Size = new System.Drawing.Size(280, 18),
				Font = new System.Drawing.Font("Segoe UI", 8f, System.Drawing.FontStyle.Bold),
				ForeColor = accent
			};
			System.Windows.Forms.Label localTitleLabel = new System.Windows.Forms.Label
			{
				Text = title,
				Location = new System.Drawing.Point(0, 24),
				Size = new System.Drawing.Size(520, 42),
				Font = new System.Drawing.Font("Segoe UI Semibold", 18f, System.Drawing.FontStyle.Bold),
				ForeColor = HVAC_Pro_Desktop.UI.DS.Slate900
			};
			System.Windows.Forms.Label localDetailLabel = new System.Windows.Forms.Label
			{
				Text = detail,
				Location = new System.Drawing.Point(0, 72),
				Size = new System.Drawing.Size(560, 38),
				Font = new System.Drawing.Font("Segoe UI", 9f),
				ForeColor = HVAC_Pro_Desktop.UI.DS.Slate600
			};
			System.Windows.Forms.FlowLayoutPanel localChipRow = CreateChipRow();
			localChipRow.Location = new System.Drawing.Point(0, 118);
			System.Windows.Forms.Panel actionHost = new System.Windows.Forms.Panel
			{
				Anchor = (System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Right),
				BackColor = System.Drawing.Color.Transparent,
				Size = new System.Drawing.Size(0, 40),
				Visible = false
			};
			System.Collections.Generic.List<System.Windows.Forms.Control> actionList = System.Linq.Enumerable.ToList(System.Linq.Enumerable.Where(actions ?? System.Linq.Enumerable.Empty<System.Windows.Forms.Control>(), (System.Windows.Forms.Control control) => control != null));
			if (actionList.Count > 0)
			{
				actionHost.Visible = true;
			}
			actionHost.Resize += delegate
			{
				int num = actionHost.ClientSize.Width;
				foreach (System.Windows.Forms.Control item in System.Linq.Enumerable.Reverse(System.Linq.Enumerable.Where(actionList, (System.Windows.Forms.Control control) => control.Visible)))
				{
					num -= item.Width;
					item.Location = new System.Drawing.Point(System.Math.Max(0, num), 0);
					num -= 10;
				}
			};
			foreach (System.Windows.Forms.Control item2 in actionList)
			{
				item2.Height = System.Math.Max(item2.Height, 36);
				actionHost.Controls.Add(item2);
			}
			card.Resize += delegate
			{
				int num = System.Linq.Enumerable.Count(actionList, (System.Windows.Forms.Control control) => control.Visible);
				int num2 = System.Linq.Enumerable.Sum(System.Linq.Enumerable.Where(actionList, (System.Windows.Forms.Control control) => control.Visible), (System.Windows.Forms.Control control) => control.Width) + System.Math.Max(0, num - 1) * 10;
				if (num > 0)
				{
					actionHost.Visible = true;
					actionHost.Width = System.Math.Max(240, System.Math.Min(480, num2 + 4));
					actionHost.Location = new System.Drawing.Point(System.Math.Max(260, card.ClientSize.Width - actionHost.Width - 22), 22);
				}
				else
				{
					actionHost.Visible = false;
					actionHost.Width = 0;
					actionHost.Location = new System.Drawing.Point(card.ClientSize.Width - 22, 22);
				}
				int num3 = System.Math.Max(260, ((num > 0) ? actionHost.Left : (card.ClientSize.Width - 22)) - 34);
				eyebrowLabel.Width = System.Math.Min(num3, 320);
				localTitleLabel.Width = num3;
				localDetailLabel.Width = num3;
				localChipRow.Width = num3;
			};
			titleLabel = localTitleLabel;
			detailLabel = localDetailLabel;
			chipRow = localChipRow;
			card.Controls.Add(actionHost);
			card.Controls.Add(localChipRow);
			card.Controls.Add(localDetailLabel);
			card.Controls.Add(localTitleLabel);
			card.Controls.Add(eyebrowLabel);
			return card;
		}
	}
}
