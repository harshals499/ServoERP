using System;
using System.IO;
using System.Text;
using System.ComponentModel;
using System.Runtime.InteropServices;
using Serilog;

namespace HVAC_Pro_Desktop.Services
{
    // Per-Windows-user UI preference, independent of business data and available before login.
    internal static class LanguagePreferenceService
    {
        [DllImport("kernel32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
        private static extern bool MoveFileEx(string existingFile, string newFile, int flags);

        private static string PreferencePath => Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "ServoERP", "Preferences", "language.txt");

        internal static string Load(string fallback = LanguageManager.English)
        {
            return LoadFrom(PreferencePath, fallback);
        }

        internal static string LoadFrom(string path, string fallback)
        {
            try
            {
                if (!File.Exists(path)) return fallback;
                string value = File.ReadAllText(path, Encoding.UTF8).Trim();
                if (value == "mr-IN") return LanguageManager.Marathi;
                if (value == "en-IN") return LanguageManager.English;
                if (value == "hi-IN") return LanguageManager.Hindi;
            }
            catch (Exception ex) { Log.Warning(ex, "Unable to read UI language preference"); }
            return fallback;
        }

        internal static void Save(string language)
        {
            SaveTo(PreferencePath, language);
        }

        internal static void SaveTo(string path, string language)
        {
            if (language != LanguageManager.English && language != LanguageManager.Marathi && language != LanguageManager.Hindi)
                throw new ArgumentException("Unsupported UI language", nameof(language));
            path = Path.GetFullPath(path);
            Directory.CreateDirectory(Path.GetDirectoryName(path));
            string temporary = path + "." + Guid.NewGuid().ToString("N") + ".tmp";
            try
            {
                File.WriteAllText(temporary, language == LanguageManager.Marathi ? "mr-IN" : language == LanguageManager.Hindi ? "hi-IN" : "en-IN", Encoding.UTF8);
                // Atomic replacement also works on Windows volumes that do not support ReplaceFile.
                if (!MoveFileEx(temporary, path, 0x1 | 0x8))
                    throw new Win32Exception(Marshal.GetLastWin32Error(), "Unable to persist UI language preference");
            }
            finally { if (File.Exists(temporary)) File.Delete(temporary); }
        }
    }
}
