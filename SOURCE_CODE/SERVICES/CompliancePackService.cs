using System;
using System.IO;
using System.IO.Compression;
using System.Reflection;
using System.Text;

namespace HVAC_Pro_Desktop.Services
{
    public sealed class CompliancePackService
    {
        /// <summary>Exports a local compliance ZIP with legal, license, module, and system-readiness reports.</summary>
        public string ExportPack()
        {
            string root = Path.Combine(@"C:\HVAC_PRO_MSE", "COMPLIANCE");
            Directory.CreateDirectory(root);
            string stamp = DateTime.Now.ToString("yyyyMMdd_HHmmss");
            string folder = Path.Combine(root, "ServoERP_CompliancePack_" + stamp);
            Directory.CreateDirectory(folder);

            File.WriteAllText(Path.Combine(folder, "00_READ_ME.txt"), BuildReadMe(), Encoding.UTF8);
            File.WriteAllText(Path.Combine(folder, "01_open_source_disclosure.txt"), new OpenSourceLicenseService().BuildDisclosureReport(), Encoding.UTF8);
            File.WriteAllText(Path.Combine(folder, "02_module_catalog.txt"), new ModuleCatalogService().BuildReport(), Encoding.UTF8);
            File.WriteAllText(Path.Combine(folder, "03_system_readiness.txt"), BuildSystemReadiness(), Encoding.UTF8);
            File.WriteAllText(Path.Combine(folder, "04_eula.txt"), LegalTexts.EULA, Encoding.UTF8);
            File.WriteAllText(Path.Combine(folder, "05_privacy_policy.txt"), LegalTexts.PrivacyPolicy, Encoding.UTF8);
            File.WriteAllText(Path.Combine(folder, "06_data_processing_policy.txt"), LegalTexts.DataProcessingPolicy, Encoding.UTF8);
            File.WriteAllText(Path.Combine(folder, "07_disclaimer.txt"), LegalTexts.Disclaimer, Encoding.UTF8);

            string zipPath = folder + ".zip";
            if (File.Exists(zipPath))
                File.Delete(zipPath);
            ZipFile.CreateFromDirectory(folder, zipPath);
            Directory.Delete(folder, true);
            return zipPath;
        }

        private static string BuildReadMe()
        {
            return "ServoERP Compliance Pack" + Environment.NewLine +
                   "Generated: " + DateTime.Now.ToString("dd/MM/yyyy HH:mm") + Environment.NewLine +
                   "Purpose: Client IT, procurement, onboarding, and internal audit review." + Environment.NewLine +
                   Environment.NewLine +
                   "This pack is generated locally on the client machine. It does not upload business data, passwords, license keys, or database records." + Environment.NewLine +
                   "ServoERP is built for Indian SMEs and keeps backup/compliance artefacts on client infrastructure.";
        }

        private static string BuildSystemReadiness()
        {
            Assembly assembly = Assembly.GetExecutingAssembly();
            return "ServoERP System Readiness" + Environment.NewLine +
                   "Generated: " + DateTime.Now.ToString("dd/MM/yyyy HH:mm") + Environment.NewLine +
                   "App Version: " + ConfigService.GetAppVersion() + Environment.NewLine +
                   "Assembly Version: " + assembly.GetName().Version + Environment.NewLine +
                   "Machine Name: " + Environment.MachineName + Environment.NewLine +
                   "OS Version: " + Environment.OSVersion + Environment.NewLine +
                   ".NET Version: " + Environment.Version + Environment.NewLine +
                   "Database: SQL Server Express / HVAC_PRO" + Environment.NewLine +
                   "Default Locale: India (en-IN)" + Environment.NewLine +
                   "Data Handling: Client-side desktop application; backups remain on configured client storage.";
        }
    }
}
