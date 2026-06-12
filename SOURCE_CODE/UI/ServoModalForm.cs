using System.Drawing;
using ServoERP.Infrastructure;
using HVAC_Pro_Desktop.Services;

namespace HVAC_Pro_Desktop.UI
{
    /// <summary>Shared ServoERP modal shell for small view, edit, setup, and action dialogs.</summary>
    public sealed class ServoModalForm : ServoFormBase
    {
        public ServoModalForm(string title, int clientWidth, int clientHeight)
        {
            Text = BrandingService.WindowTitle(title);
            StartPosition = System.Windows.Forms.FormStartPosition.CenterParent;
            FormBorderStyle = System.Windows.Forms.FormBorderStyle.FixedDialog;
            MaximizeBox = false;
            MinimizeBox = false;
            BackColor = DS.BgPage;
            Font = DS.Body;
            ClientSize = new Size(clientWidth, clientHeight);
        }

        public static ServoModalForm Create(string title, int clientWidth, int clientHeight)
        {
            return new ServoModalForm(title, clientWidth, clientHeight);
        }
    }
}
