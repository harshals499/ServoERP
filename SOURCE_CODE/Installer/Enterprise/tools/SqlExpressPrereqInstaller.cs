using System;
using System.Diagnostics;
using System.IO;

internal static class SqlExpressPrereqInstaller
{
    private const string InstanceName = "SQLEXPRESS";

    private static int Main()
    {
        string logDir = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData),
            "ServoERP",
            "Logs");
        Directory.CreateDirectory(logDir);

        string logPath = Path.Combine(logDir, "SqlExpressPrereqInstaller.log");
        using (var log = new StreamWriter(logPath, append: true))
        {
            log.AutoFlush = true;
            try
            {
                Log(log, "Starting SQL Server Express prerequisite installer.");

                string baseDir = AppDomain.CurrentDomain.BaseDirectory;
                string sqlPackage = Path.Combine(baseDir, "SQLEXPR_x64_ENU.exe");
                if (!File.Exists(sqlPackage))
                {
                    Log(log, "SQL package not found next to helper: " + sqlPackage);
                    return unchecked((int)0x80070002);
                }

                string extractRoot = Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData),
                    "ServoERP",
                    "InstallCache",
                    "SQLExpress2022");
                Directory.CreateDirectory(extractRoot);

                string setupExe = Path.Combine(extractRoot, "SETUP.EXE");
                string mediaInfo = Path.Combine(extractRoot, "MediaInfo.xml");

                if (!File.Exists(setupExe) || !File.Exists(mediaInfo))
                {
                    Log(log, "Extracting SQL media to: " + extractRoot);
                    int extractExit = Run(log, sqlPackage, "/Q /X:\"" + extractRoot + "\"", baseDir);
                    if (extractExit != 0)
                    {
                        Log(log, "SQL media extraction failed with exit code: " + extractExit);
                        return extractExit;
                    }
                }

                if (!File.Exists(setupExe))
                {
                    Log(log, "SQL setup.exe missing after extraction: " + setupExe);
                    return unchecked((int)0x80070002);
                }

                if (!File.Exists(mediaInfo))
                {
                    Log(log, "SQL MediaInfo.xml missing after extraction: " + mediaInfo);
                    return unchecked((int)0x80070002);
                }

                string sqlArgs =
                    "/Q " +
                    "/IACCEPTSQLSERVERLICENSETERMS " +
                    "/ACTION=Install " +
                    "/FEATURES=SQLEngine " +
                    "/INSTANCENAME=" + InstanceName + " " +
                    "/SQLSVCACCOUNT=\"NT AUTHORITY\\NETWORK SERVICE\" " +
                    "/SQLSYSADMINACCOUNTS=\"Builtin\\Administrators\" " +
                    "/TCPENABLED=1 " +
                    "/NPENABLED=1 " +
                    "/BROWSERSVCSTARTUPTYPE=Automatic " +
                    "/UpdateEnabled=False " +
                    "/SUPPRESSPRIVACYSTATEMENTNOTICE=True " +
                    "/MEDIASOURCE=\"" + extractRoot + "\"";

                Log(log, "Running SQL setup from extracted media.");
                int setupExit = Run(log, setupExe, sqlArgs, extractRoot);
                Log(log, "SQL setup finished with exit code: " + setupExit);
                return setupExit;
            }
            catch (Exception ex)
            {
                Log(log, ex.ToString());
                return -1;
            }
        }
    }

    private static int Run(StreamWriter log, string fileName, string arguments, string workingDirectory)
    {
        Log(log, "Run: " + fileName + " " + arguments);
        var startInfo = new ProcessStartInfo
        {
            FileName = fileName,
            Arguments = arguments,
            WorkingDirectory = workingDirectory,
            UseShellExecute = false,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            CreateNoWindow = true
        };

        using (var process = Process.Start(startInfo))
        {
            process.OutputDataReceived += (_, e) => { if (e.Data != null) Log(log, e.Data); };
            process.ErrorDataReceived += (_, e) => { if (e.Data != null) Log(log, "ERR: " + e.Data); };
            process.BeginOutputReadLine();
            process.BeginErrorReadLine();
            process.WaitForExit();
            return process.ExitCode;
        }
    }

    private static void Log(StreamWriter log, string message)
    {
        log.WriteLine(DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss") + " " + message);
    }
}
