using System;
using System.Diagnostics;
using System.Drawing;
using System.Windows.Forms;
using HVAC_Pro_Desktop.Services;

namespace HVAC_Pro_Desktop.UI
{
    public sealed class CompliancePackForm : ServoERP.Infrastructure.ServoFormBase
    {
        private Label _status;
        private TextBox _details;

        /// <summary>Initializes the compliance pack exporter screen.</summary>
        public CompliancePackForm()
        {
            BuildLayout();
        }

        private void BuildLayout()
        {
            AutoScaleMode = AutoScaleMode.Dpi;
            BackColor = DS.BgPage;
            ClientSize = new Size(760, 460);
            Font = new Font("Segoe UI", 9f);
            FormBorderStyle = FormBorderStyle.FixedDialog;
            MaximizeBox = false;
            MinimizeBox = false;
            StartPosition = FormStartPosition.CenterParent;
            Text = "ServoERP - Compliance Export Pack";

            Controls.Add(new Label { Text = "Compliance Export Pack", Location = new Point(24, 18), Size = new Size(460, 34), Font = DS.H1, ForeColor = DS.Slate900 });
            Controls.Add(new Label { Text = "Generate a local ZIP for client IT, procurement, onboarding, and audit review.", Location = new Point(26, 52), Size = new Size(680, 28), Font = DS.Body, ForeColor = DS.Slate600 });

            _details = new TextBox
            {
                Location = new Point(24, 96),
                Size = new Size(712, 236),
                Multiline = true,
                ReadOnly = true,
                ScrollBars = ScrollBars.Vertical,
                Font = DS.Mono,
                BorderStyle = BorderStyle.FixedSingle,
                BackColor = Color.White,
                Text = "Pack includes:\r\n- Open-source disclosure\r\n- Module catalog\r\n- System readiness report\r\n- EULA\r\n- Privacy Policy\r\n- Data Processing Policy\r\n- Disclaimer\r\n\r\nGenerated locally at C:\\HVAC_PRO_MSE\\COMPLIANCE. No business data is uploaded."
            };
            Controls.Add(_details);

            Button export = Button("Generate Pack", DS.Green600, Color.White, 140);
            export.Location = new Point(24, 356);
            export.Click += (s, e) => ExportPack();
            Controls.Add(export);

            Button close = Button("Close", Color.White, DS.Slate700, 96);
            close.FlatAppearance.BorderColor = DS.Border;
            close.FlatAppearance.BorderSize = 1;
            close.Location = new Point(640, 356);
            close.Click += (s, e) => Close();
            Controls.Add(close);

            _status = new Label { Text = "Ready.", Location = new Point(184, 362), Size = new Size(430, 24), Font = DS.Small, ForeColor = DS.Slate600 };
            Controls.Add(_status);
        }

        private void ExportPack()
        {
            try
            {
                string path = new CompliancePackService().ExportPack();
                _status.Text = "Pack created: " + path;
                _status.ForeColor = DS.Green600;
                Process.Start("explorer.exe", "/select,\"" + path + "\"");
            }
            catch (Exception ex)
            {
                _status.Text = "Export failed: " + ex.Message;
                _status.ForeColor = DS.Red600;
            }
        }

        private static Button Button(string text, Color backColor, Color foreColor, int width)
        {
            Button button = new Button { Text = text, Size = new Size(width, 34), BackColor = backColor, ForeColor = foreColor, FlatStyle = FlatStyle.Flat, Font = DS.BodyBold, UseVisualStyleBackColor = false };
            button.FlatAppearance.BorderSize = backColor == Color.White ? 1 : 0;
            return button;
        }
    }
}

