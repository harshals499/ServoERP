using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Xml;

namespace HVAC_Pro_Desktop.Services
{
    public sealed class OpenSourceComponent
    {
        public string Name { get; set; }
        public string Version { get; set; }
        public string License { get; set; }
        public string Usage { get; set; }
        public string SourceUrl { get; set; }
        public string Notes { get; set; }
    }

    public sealed class OpenSourceLicenseService
    {
        /// <summary>Returns open-source and redistributable components used by ServoERP.</summary>
        public List<OpenSourceComponent> GetComponents()
        {
            Dictionary<string, string> versions = ReadNuGetPackageVersions();
            var list = new List<OpenSourceComponent>
            {
                Component(
                    "EPPlus",
                    VersionOf(versions, "EPPlus", "4.5.3.3"),
                    "LGPL for EPPlus 4.x",
                    "Excel import/export across ServoERP modules.",
                    "https://github.com/JanKallman/EPPlus",
                    "EPPlus 5+ changed licensing. ServoERP currently references EPPlus 4.5.3.3."),
                Component(
                    "BCrypt.Net-Next",
                    VersionOf(versions, "BCrypt.Net-Next", "4.0.3"),
                    "MIT",
                    "Password hashing for local application users.",
                    "https://github.com/BcryptNet/bcrypt.net",
                    "Used for one-way password hashing only."),
                Component(
                    "Microsoft.Web.WebView2",
                    VersionOf(versions, "Microsoft.Web.WebView2", "1.0.3912.50"),
                    "Microsoft Edge WebView2 Runtime terms",
                    "Embedded browser runtime for map, preview, and web-backed UI surfaces.",
                    "https://learn.microsoft.com/microsoft-edge/webview2/",
                    "Runtime is redistributed with the ServoERP installer."),
                Component(
                    "QuestPDF",
                    VersionOf(versions, "QuestPDF", "2026.5.0"),
                    "MIT / QuestPDF Community License terms",
                    "PDF generation for invoices, job cards, and business documents.",
                    "https://www.nuget.org/packages/QuestPDF",
                    "ServoERP sets the QuestPDF Community license at startup."),
                Component(
                    ".NET Framework",
                    "4.7.2",
                    "Microsoft .NET Framework terms",
                    "Desktop runtime for ServoERP WinForms.",
                    "https://dotnet.microsoft.com/",
                    "Installed or checked by the ServoERP installer.")
            };

            return list.OrderBy(c => c.Name).ToList();
        }

        /// <summary>Builds a plain-text open-source disclosure report.</summary>
        public string BuildDisclosureReport()
        {
            var sb = new StringBuilder();
            sb.AppendLine("ServoERP Open Source & Third-Party Components");
            sb.AppendLine("Generated: " + DateTime.Now.ToString("dd/MM/yyyy HH:mm"));
            sb.AppendLine();
            sb.AppendLine("This disclosure lists key open-source and redistributable components bundled or referenced by ServoERP.");
            sb.AppendLine("Client business data is not included in this report.");
            sb.AppendLine();

            foreach (OpenSourceComponent component in GetComponents())
            {
                sb.AppendLine(component.Name + " " + component.Version);
                sb.AppendLine("License : " + component.License);
                sb.AppendLine("Usage   : " + component.Usage);
                sb.AppendLine("Source  : " + component.SourceUrl);
                sb.AppendLine("Notes   : " + component.Notes);
                sb.AppendLine();
            }

            return sb.ToString();
        }

        /// <summary>Exports the disclosure report to the LOGS folder and returns the file path.</summary>
        public string ExportDisclosureReport()
        {
            string folder = @"C:\HVAC_PRO_MSE\LOGS";
            Directory.CreateDirectory(folder);
            string path = Path.Combine(folder, "servoerp-open-source-disclosure-" + DateTime.Now.ToString("yyyyMMdd-HHmmss") + ".txt");
            File.WriteAllText(path, BuildDisclosureReport(), Encoding.UTF8);
            return path;
        }

        /// <summary>Opens the component source URL in the user's default browser.</summary>
        public void OpenComponentSource(OpenSourceComponent component)
        {
            if (component == null || string.IsNullOrWhiteSpace(component.SourceUrl))
                return;

            Process.Start(new ProcessStartInfo
            {
                FileName = component.SourceUrl,
                UseShellExecute = true
            });
        }

        /// <summary>Creates a component disclosure item.</summary>
        private static OpenSourceComponent Component(string name, string version, string license, string usage, string sourceUrl, string notes)
        {
            return new OpenSourceComponent
            {
                Name = name,
                Version = version,
                License = license,
                Usage = usage,
                SourceUrl = sourceUrl,
                Notes = notes
            };
        }

        /// <summary>Reads package versions from packages.config when available.</summary>
        private static Dictionary<string, string> ReadNuGetPackageVersions()
        {
            var versions = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            try
            {
                string path = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "..", "..", "packages.config");
                path = Path.GetFullPath(path);
                if (!File.Exists(path))
                    path = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "packages.config");

                if (!File.Exists(path))
                    return versions;

                var doc = new XmlDocument();
                doc.Load(path);
                foreach (XmlNode package in doc.SelectNodes("//package"))
                {
                    string id = package.Attributes["id"] == null ? string.Empty : package.Attributes["id"].Value;
                    string version = package.Attributes["version"] == null ? string.Empty : package.Attributes["version"].Value;
                    if (!string.IsNullOrWhiteSpace(id))
                        versions[id] = version ?? string.Empty;
                }
            }
            catch (Exception ex)
            {
                AppLogger.LogError("OpenSourceLicenseService.ReadNuGetPackageVersions", ex);
            }

            return versions;
        }

        /// <summary>Returns a package version with a fallback when discovery is unavailable.</summary>
        private static string VersionOf(Dictionary<string, string> versions, string key, string fallback)
        {
            string value;
            return versions.TryGetValue(key, out value) && !string.IsNullOrWhiteSpace(value) ? value : fallback;
        }

    }
}
