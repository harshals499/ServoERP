using System;
using System.Data.SqlClient;
using System.Windows.Forms;
using HVAC_Pro_Desktop.DAL;
using HVAC_Pro_Desktop.Services;
using HVAC_Pro_Desktop.Services.Licensing;

namespace HVAC_Pro_Desktop.OfflineDesktop
{
    internal static class OfflineDesktopStartup
    {
        public static bool TryRunBeforeSqlStartup(Func<HVAC_Pro_Desktop.Models.LicenseValidationResult> validateLicense=null)
        {
            if(!OfflineDesktopCoordinator.HasPreparedWorkspace())return false;
            try
            {
                var builder=new SqlConnectionStringBuilder(OfflineDesktopCoordinator.ConnectionString){ConnectTimeout=3};
                using(var connection=new SqlConnection(builder.ConnectionString))connection.Open();
                return false;
            }
            catch(Exception ex) { return TryRunAfterFailure(ex,validateLicense); }
        }
        public static bool TryRunAfterFailure(Exception error,Func<HVAC_Pro_Desktop.Models.LicenseValidationResult> validateLicense=null)
        {
            if(!OfflineDesktopCoordinator.IsConnectivityFailure(error) || !OfflineDesktopCoordinator.HasPreparedWorkspace())return false;
            OfflineDesktopCoordinator.BeginOfflineStartup();
            var validation=validateLicense!=null?validateLicense():new LicenseService().ValidateCurrentLicense();
            if(validation.RequiresActivation)
            {
                MessageBox.Show("Offline work requires an existing activated ServoERP license on this PC. Activate through the normal online setup first.","ServoERP",MessageBoxButtons.OK,MessageBoxIcon.Information);return true;
            }
            // A completed online enrollment/cache was created only after normal legal acceptance.
            using(var login=new OfflineSignInForm())
            {
                if(login.ShowDialog()!=DialogResult.OK)return true;
                try { Application.Run(new OfflineWorkspaceForm()); }
                finally { OfflineDesktopCoordinator.Stop();SessionManager.ClearSession(); }
            }
            return true;
        }
    }
}
