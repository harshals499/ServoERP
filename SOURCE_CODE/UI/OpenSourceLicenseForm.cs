using System;
using System.Diagnostics;
using System.Drawing;
using System.Linq;
using System.Windows.Forms;
using HVAC_Pro_Desktop.Services;

namespace HVAC_Pro_Desktop.UI
{
    public partial class OpenSourceLicenseForm : ServoERP.Infrastructure.ServoFormBase
    {
        private readonly OpenSourceLicenseService _service = new OpenSourceLicenseService();

        /// <summary>Initializes the open-source component disclosure screen.</summary>
        public OpenSourceLicenseForm()
        {
            InitializeComponent();
            LoadComponents();
        }

        /// <summary>Loads component metadata into the grid.</summary>
        private void LoadComponents()
        {
            _gridComponents.DataSource = _service.GetComponents()
                .Select(c => new
                {
                    Component = c.Name,
                    c.Version,
                    c.License,
                    c.Usage,
                    Source = c.SourceUrl,
                    c.Notes
                })
                .ToList();

            _txtDisclosure.Text = _service.BuildDisclosureReport();
        }

        /// <summary>Exports the disclosure report and opens it in Notepad.</summary>
        private void ExportDisclosure(object sender, EventArgs e)
        {
            try
            {
                string path = _service.ExportDisclosureReport();
                _lblStatus.Text = "Disclosure exported: " + path;
                _lblStatus.ForeColor = DS.Green600;
                Process.Start("notepad.exe", path);
            }
            catch (Exception ex)
            {
                _lblStatus.Text = "Export failed: " + ex.Message;
                _lblStatus.ForeColor = DS.Red600;
            }
        }

        /// <summary>Copies the disclosure report to the clipboard.</summary>
        private void CopyDisclosure(object sender, EventArgs e)
        {
            try
            {
                Clipboard.SetText(_txtDisclosure.Text);
                _lblStatus.Text = "Disclosure copied to clipboard.";
                _lblStatus.ForeColor = DS.Green600;
            }
            catch (Exception ex)
            {
                _lblStatus.Text = "Copy failed: " + ex.Message;
                _lblStatus.ForeColor = DS.Red600;
            }
        }

        /// <summary>Opens the selected component source URL.</summary>
        private void OpenSelectedSource(object sender, EventArgs e)
        {
            if (_gridComponents.CurrentRow == null)
                return;

            object source = _gridComponents.CurrentRow.Cells["Source"].Value;
            if (source == null || string.IsNullOrWhiteSpace(source.ToString()))
                return;

            Process.Start(new ProcessStartInfo
            {
                FileName = source.ToString(),
                UseShellExecute = true
            });
        }

        /// <summary>Closes the disclosure screen.</summary>
        private void CloseClicked(object sender, EventArgs e)
        {
            Close();
        }
    }
}

