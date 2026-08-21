using System;
using System.Drawing;
using System.Windows.Forms;
using HVAC_Pro_Desktop.Services;

namespace HVAC_Pro_Desktop.UI
{
    public sealed class OfficeApiSettingsForm : ServoERP.Infrastructure.ServoFormBase
    {
        private readonly TextBox _url = new TextBox();
        private readonly TextBox _key = new TextBox { UseSystemPasswordChar = true };
        private readonly TextBox _userToken = new TextBox { UseSystemPasswordChar = true };
        private readonly ComboBox _company = new ComboBox { DropDownStyle = ComboBoxStyle.DropDownList };
        private readonly CheckBox _enabled = new CheckBox { Text = "Use Office API for payments, stock movements, and purchase receiving" };
        private readonly Label _status = new Label();

        public OfficeApiSettingsForm()
        {
            Text = "Office API connection"; StartPosition = FormStartPosition.CenterParent; ClientSize = new Size(610, 430); MaximizeBox = false; MinimizeBox = false;
            Controls.Add(new Label { Text = "Private office API", Font = new Font("Segoe UI", 16F, FontStyle.Bold), Location = new Point(24, 20), Size = new Size(450, 32) });
            Controls.Add(new Label { Text = "API address (HTTPS)", Location = new Point(24, 78), Size = new Size(250, 20) });
            _url.Location = new Point(24, 101); _url.Size = new Size(560, 28); _url.Text = OfficeApiClient.BaseUrl;
            Controls.Add(_url);
            Controls.Add(new Label { Text = "API key", Location = new Point(24, 142), Size = new Size(250, 20) });
            _key.Location = new Point(24, 165); _key.Size = new Size(560, 28); _key.Tag = "Enter the key supplied by the office server administrator";
            Controls.Add(_key);
            Controls.Add(new Label { Text = "Signed-in user API token (issued by IT)", Location = new Point(24, 204), Size = new Size(300, 20) });
            _userToken.Location = new Point(24, 227); _userToken.Size = new Size(560, 28); Controls.Add(_userToken);
            Controls.Add(new Label { Text = "Authorized company", Location = new Point(24, 266), Size = new Size(250, 20) });
            _company.Location = new Point(24, 289); _company.Size = new Size(560, 28); Controls.Add(_company);
            _enabled.Location = new Point(24, 324); _enabled.Size = new Size(560, 26); _enabled.Checked = OfficeApiClient.IsEnabled; Controls.Add(_enabled);
            var test = new Button { Text = "Test", Location = new Point(332, 372), Size = new Size(80, 34) }; test.Click += (s, e) => Test(); Controls.Add(test);
            var save = new Button { Text = "Save", Location = new Point(420, 372), Size = new Size(80, 34) }; save.Click += (s, e) => Save(); Controls.Add(save);
            var cancel = new Button { Text = "Close", Location = new Point(504, 372), Size = new Size(80, 34) }; cancel.Click += (s, e) => Close(); Controls.Add(cancel);
            _status.Location = new Point(24, 357); _status.Size = new Size(295, 50); Controls.Add(_status);
            UIHelper.ApplyInputStyles(Controls);
        }

        private void Test()
        {
            try { OfficeApiClient.SaveSettings(_url.Text, _key.Text, true, _userToken.Text); ApiHealthResult r = OfficeApiClient.CheckHealth(); var companies = OfficeApiClient.GetAuthorizedCompanies(); _company.DataSource = companies; _status.ForeColor = Color.ForestGreen; _status.Text = r.Message + " " + r.Server + " / " + r.Database + ". Select the authorized company."; }
            catch (Exception ex) { _status.ForeColor = Color.Firebrick; _status.Text = ex.Message; }
        }

        private void Save()
        {
            try { var selected = _company.SelectedItem as OfficeApiCompany; if (_enabled.Checked && selected == null) throw new InvalidOperationException("Test the connection and select an authorized company before enabling Office API mode."); OfficeApiClient.SaveSettings(_url.Text, _key.Text, _enabled.Checked, _userToken.Text, selected == null ? (int?)null : selected.CompanyId); if (selected != null) ConfigService.Set("OfficeApi", "ActiveCompanyName", selected.CompanyName); DialogResult = DialogResult.OK; Close(); }
            catch (Exception ex) { _status.ForeColor = Color.Firebrick; _status.Text = ex.Message; }
        }
    }
}
