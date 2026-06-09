using System;
using System.Drawing;
using System.Windows.Forms;
using HVAC_Pro_Desktop.DAL;
using HVAC_Pro_Desktop.Services;

namespace HVAC_Pro_Desktop.UI
{
    public partial class LegalAgreementForm : ServoERP.Infrastructure.ServoFormBase
    {
        private readonly bool _readOnlyMode;

        public LegalAgreementForm()
            : this(false)
        {
        }

        /// <summary>Initializes the legal agreement form in acceptance or read-only mode.</summary>
        public LegalAgreementForm(bool readOnlyMode)
        {
            _readOnlyMode = readOnlyMode;
            InitializeComponent();
            ConfigureMode();
            LoadLegalTexts();
        }

        /// <summary>Shows the legal agreement gate when terms have not been accepted.</summary>
        public static bool EnsureAccepted(IWin32Window owner)
        {
            string accepted = DbSettings.Get("LegalAccepted", "false");
            if (string.Equals(accepted, "true", StringComparison.OrdinalIgnoreCase))
                return true;

            using (var form = new LegalAgreementForm(false))
            {
                DialogResult result = owner == null ? form.ShowDialog() : form.ShowDialog(owner);
                return result == DialogResult.OK;
            }
        }

        /// <summary>Applies read-only viewer mode when the form is opened from Settings.</summary>
        private void ConfigureMode()
        {
            if (!_readOnlyMode)
                return;

            _chkAccept.Visible = false;
            _btnAccept.Visible = false;
            _btnDecline.Visible = false;
            _btnClose.Visible = true;
            _btnClose.Location = _btnDecline.Location;
            AcceptButton = _btnClose;
            CancelButton = _btnClose;
        }

        /// <summary>Loads embedded legal text into each agreement tab.</summary>
        private void LoadLegalTexts()
        {
            _rtbEula.Text = LegalTexts.EULA;
            _rtbPrivacy.Text = LegalTexts.PrivacyPolicy;
            _rtbDataProcessing.Text = LegalTexts.DataProcessingPolicy;
            _rtbDisclaimer.Text = LegalTexts.Disclaimer;
            ApplyLegalTextStyle(_rtbEula);
            ApplyLegalTextStyle(_rtbPrivacy);
            ApplyLegalTextStyle(_rtbDataProcessing);
            ApplyLegalTextStyle(_rtbDisclaimer);
            ResetScroll(_rtbEula);
            ResetScroll(_rtbPrivacy);
            ResetScroll(_rtbDataProcessing);
            ResetScroll(_rtbDisclaimer);
        }

        /// <summary>Applies readable heading, section, and bullet styling to legal text.</summary>
        private void ApplyLegalTextStyle(RichTextBox box)
        {
            if (box == null || string.IsNullOrWhiteSpace(box.Text))
                return;

            box.SuspendLayout();
            box.SelectAll();
            box.SelectionFont = new Font("Segoe UI", 10f, FontStyle.Regular);
            box.SelectionColor = DS.Slate800;
            box.SelectionBackColor = Color.White;

            string[] lines = box.Lines;
            int searchStart = 0;
            for (int i = 0; i < lines.Length; i++)
            {
                string line = lines[i] ?? string.Empty;
                int index = box.Text.IndexOf(line, searchStart, StringComparison.Ordinal);
                if (index < 0)
                    continue;

                searchStart = index + line.Length;
                if (line.Length == 0)
                    continue;

                if (i <= 2)
                    SetLineStyle(box, index, line.Length, i == 1 ? 13f : 10.5f, FontStyle.Bold, i == 1 ? DS.Primary700 : DS.Slate700);
                else if (IsSectionHeading(line))
                    SetLineStyle(box, index, line.Length, 10.5f, FontStyle.Bold, DS.Primary700);
                else if (line.TrimStart().StartsWith("- ", StringComparison.Ordinal))
                    SetLineStyle(box, index, line.Length, 10f, FontStyle.Regular, DS.Slate700);
            }

            box.ResumeLayout();
        }

        /// <summary>Checks whether a legal text line is a numbered section heading.</summary>
        private static bool IsSectionHeading(string line)
        {
            if (string.IsNullOrWhiteSpace(line))
                return false;

            string text = line.TrimStart();
            int dotIndex = text.IndexOf('.');
            return dotIndex > 0
                && dotIndex <= 2
                && text.Length > dotIndex + 1
                && char.IsDigit(text[0]);
        }

        /// <summary>Applies font and colour styling to a single legal text line.</summary>
        private static void SetLineStyle(RichTextBox box, int start, int length, float size, FontStyle style, Color color)
        {
            box.Select(start, length);
            box.SelectionFont = new Font("Segoe UI", size, style);
            box.SelectionColor = color;
        }

        /// <summary>Positions the legal text viewer at the top of its content.</summary>
        private void ResetScroll(RichTextBox box)
        {
            box.SelectionStart = 0;
            box.SelectionLength = 0;
            box.ScrollToCaret();
        }

        /// <summary>Enables the Accept button only after the agreement checkbox is checked.</summary>
        private void OnAcceptCheckedChanged(object sender, EventArgs e)
        {
            _btnAccept.Enabled = _chkAccept.Checked;
            _btnAccept.BackColor = _chkAccept.Checked ? DS.Green600 : DS.Slate300;
            _btnAccept.ForeColor = Color.White;
        }

        /// <summary>Saves legal acceptance and closes the startup gate.</summary>
        private void OnAcceptClick(object sender, EventArgs e)
        {
            DbSettings.Set("LegalAccepted", "true");
            DialogResult = DialogResult.OK;
            Close();
        }

        /// <summary>Closes ServoERP when the legal agreements are declined.</summary>
        private void OnDeclineClick(object sender, EventArgs e)
        {
            MessageBox.Show(
                "You must accept the terms to use ServoERP. The application will now close.",
                BrandingService.WindowTitle("Legal Agreements"),
                MessageBoxButtons.OK,
                MessageBoxIcon.Warning);
            DialogResult = DialogResult.Cancel;
            Application.Exit();
        }

        /// <summary>Closes the read-only legal agreement viewer.</summary>
        private void OnCloseClick(object sender, EventArgs e)
        {
            DialogResult = DialogResult.OK;
            Close();
        }
    }
}

