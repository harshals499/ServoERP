using System;
using System.IO;

namespace HVAC_Pro_Desktop.Services.Audit
{
    public sealed class AuditTrailService
    {
        public void Record(string action, string module, int? recordId, string detail)
        {
            try
            {
                SessionManager.LogAction(action, module, recordId, detail);
                Directory.CreateDirectory(@"C:\HVAC_PRO_MSE\LOGS");
                File.AppendAllText(@"C:\HVAC_PRO_MSE\LOGS\data-quality-audit.log",
                    DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss") + " | " + module + " | " + action + " | " + recordId + " | " + detail + Environment.NewLine);
            }
            catch
            {
            }
        }
    }
}
