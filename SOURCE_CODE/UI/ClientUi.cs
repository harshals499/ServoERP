using System;
using System.Drawing;
using System.Windows.Forms;
using HVAC_Pro_Desktop.Models;
using HVAC_Pro_Desktop.Services;

namespace HVAC_Pro_Desktop.UI
{
    internal static class ClientUi
    {
        public static void ShowActivityModal(IWin32Window owner, int clientId, Action afterSave)
        {
            if (clientId <= 0)
                return;

            using (Form form = new Form
            {
                Text = BrandingService.WindowTitle("Log Activity"),
                StartPosition = FormStartPosition.CenterParent,
                Width = 430,
                Height = 310,
                FormBorderStyle = FormBorderStyle.FixedDialog,
                MaximizeBox = false,
                MinimizeBox = false,
                BackColor = Color.White,
                Font = new Font("Segoe UI", 9f)
            })
            {
                ComboBox type = new ComboBox
                {
                    Location = new Point(24, 38),
                    Size = new Size(360, 24),
                    DropDownStyle = ComboBoxStyle.DropDownList
                };
                type.Items.AddRange(new object[] { "Call", "Email", "Visit", "Job", "Invoice", "Note" });
                type.SelectedIndex = 0;

                TextBox title = new TextBox { Location = new Point(24, 94), Size = new Size(360, 24), Text = "Client follow-up" };
                TextBox detail = new TextBox { Location = new Point(24, 150), Size = new Size(360, 54), Multiline = true, Text = "Spoke with facility manager about HVAC service schedule." };

                form.Controls.Add(new Label { Text = "Activity type", Location = new Point(24, 18), Size = new Size(160, 18), ForeColor = DS.Slate600, Font = DS.SmallBold });
                form.Controls.Add(type);
                form.Controls.Add(new Label { Text = "Title", Location = new Point(24, 74), Size = new Size(160, 18), ForeColor = DS.Slate600, Font = DS.SmallBold });
                form.Controls.Add(title);
                form.Controls.Add(new Label { Text = "Details", Location = new Point(24, 130), Size = new Size(160, 18), ForeColor = DS.Slate600, Font = DS.SmallBold });
                form.Controls.Add(detail);

                Button save = new Button
                {
                    Text = "Log Activity",
                    Location = new Point(270, 222),
                    Size = new Size(114, 34),
                    BackColor = DS.Primary600,
                    ForeColor = Color.White,
                    FlatStyle = FlatStyle.Flat,
                    Font = DS.BodyBold
                };
                save.FlatAppearance.BorderSize = 0;
                save.Click += (s, e) =>
                {
                    try
                    {
                        new ClientService().LogActivity(new ClientActivity
                        {
                            ClientId = clientId,
                            ActivityType = Convert.ToString(type.SelectedItem),
                            Title = title.Text.Trim(),
                            Detail = detail.Text.Trim(),
                            CreatedAt = DateTime.Now
                        });
                        form.DialogResult = DialogResult.OK;
                        form.Close();
                    }
                    catch (Exception ex)
                    {
                        AppRuntime.ShowRecoverableError(BrandingService.WindowTitle("Clients"), "Logging activity", ex);
                    }
                };
                form.Controls.Add(save);

                if (form.ShowDialog(owner) == DialogResult.OK)
                    afterSave?.Invoke();
            }
        }
    }
}
