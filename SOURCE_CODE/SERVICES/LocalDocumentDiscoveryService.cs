using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security;
using System.Threading;
using System.Threading.Tasks;
using HVAC_Pro_Desktop.Models;

namespace HVAC_Pro_Desktop.Services
{
    /// <summary>Metadata-only discovery on a user-selected local drive. Never opens document contents,
    /// follows links, downloads cloud placeholders, modifies files or writes business data.</summary>
    public sealed class LocalDocumentDiscoveryService
    {
        public const int MaximumCandidates = 20000;
        public const int MaximumFolders = 250000;
        public const long MaximumDocumentBytes = 20L * 1024 * 1024;
        private static readonly HashSet<string> SupportedExtensions = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
            { ".pdf", ".jpg", ".jpeg", ".png", ".xlsx" };
        private static readonly HashSet<string> SystemDirectoryNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
            { "Windows", "Program Files", "Program Files (x86)", "ProgramData", "AppData", "$Recycle.Bin", "System Volume Information", ".git", ".nuget", ".cache", "node_modules", "packages" };

        public static List<DriveInfo> GetLocalDrives()
        {
            return DriveInfo.GetDrives().Where(d => d.DriveType == DriveType.Fixed || d.DriveType == DriveType.Removable).ToList();
        }

        public Task<LocalDocumentScanResult> ScanDriveAsync(string driveRoot, string nameFilter, bool skipSystemFolders,
            IProgress<LocalDocumentScanProgress> progress, CancellationToken cancellationToken)
        {
            string root = ValidateDriveRoot(driveRoot);
            return Task.Run(() => ScanDirectory(root, nameFilter, skipSystemFolders, progress, cancellationToken, MaximumCandidates), cancellationToken);
        }

        internal static string ValidateDriveRoot(string path)
        {
            if (string.IsNullOrWhiteSpace(path) || path.StartsWith(@"\\", StringComparison.Ordinal))
                throw new InvalidOperationException("Select a ready local fixed or removable drive.");
            string fullPath = Path.GetFullPath(path);
            string root = Path.GetPathRoot(fullPath);
            if (!string.Equals(fullPath.TrimEnd(Path.DirectorySeparatorChar), root.TrimEnd(Path.DirectorySeparatorChar), StringComparison.OrdinalIgnoreCase))
                throw new InvalidOperationException("Select a drive root, such as C:\\ or D:\\.");
            var drive = new DriveInfo(root);
            if ((drive.DriveType != DriveType.Fixed && drive.DriveType != DriveType.Removable) || !drive.IsReady)
                throw new InvalidOperationException("The selected drive is unavailable or is not a local fixed/removable drive.");
            return drive.RootDirectory.FullName;
        }

        // The bounded directory entry point allows filesystem regression fixtures without scanning a real drive.
        internal static LocalDocumentScanResult ScanDirectory(string root, string nameFilter, bool skipSystemFolders,
            IProgress<LocalDocumentScanProgress> progress, CancellationToken token, int maximumCandidates)
        {
            token.ThrowIfCancellationRequested();
            var result = new LocalDocumentScanResult { RootPath = Path.GetFullPath(root), NameFilter = nameFilter ?? "", SystemFoldersSkipped = skipSystemFolders };
            var pending = new Stack<string>();
            pending.Push(result.RootPath);
            var lastReport = System.Diagnostics.Stopwatch.StartNew();
            while (pending.Count > 0)
            {
                token.ThrowIfCancellationRequested();
                if (result.LimitReached || result.FoldersVisited >= MaximumFolders) { result.LimitReached = true; break; }
                string directory = pending.Pop();
                try
                {
                    if ((File.GetAttributes(directory) & FileAttributes.ReparsePoint) != 0) { result.LinksSkipped++; continue; }
                    result.FoldersVisited++;
                    foreach (string path in Directory.EnumerateFiles(directory))
                    {
                        token.ThrowIfCancellationRequested();
                        result.FilesVisited++;
                        ReportProgress(result,progress,lastReport);
                        try
                        {
                            if (!SupportedExtensions.Contains(Path.GetExtension(path))) continue;
                            string fileName = Path.GetFileName(path);
                            if (fileName.StartsWith("~$", StringComparison.Ordinal)) continue;
                            if (!string.IsNullOrWhiteSpace(nameFilter) && fileName.IndexOf(nameFilter, StringComparison.OrdinalIgnoreCase) < 0) continue;
                            var file = new FileInfo(path);
                            if ((file.Attributes & FileAttributes.ReparsePoint) != 0) { result.LinksSkipped++; continue; }
                            bool eligible = file.Length > 0 && file.Length <= MaximumDocumentBytes;
                            result.Files.Add(new LocalDocumentCandidate
                            {
                                FileName = fileName, FullPath = file.FullName, SizeBytes = file.Length,
                                ModifiedUtc = file.LastWriteTimeUtc, Format = file.Extension.TrimStart('.').ToUpperInvariant(),
                                CanReview = eligible, Status = file.Length == 0 ? "Empty file" : !eligible ? "Over 20 MB limit" : "Not analysed — select for review"
                            });
                            if (result.Files.Count >= maximumCandidates) { result.LimitReached = true; break; }
                        }
                        catch (Exception ex) when (IsUnavailable(ex)) { result.UnavailableLocations++; }
                    }
                    if (result.LimitReached) break;
                    foreach (string child in Directory.EnumerateDirectories(directory))
                    {
                        token.ThrowIfCancellationRequested();
                        if (skipSystemFolders && SystemDirectoryNames.Contains(Path.GetFileName(child))) { result.SystemFoldersSkippedCount++; continue; }
                        if (pending.Count + result.FoldersVisited >= MaximumFolders) { result.LimitReached = true; break; }
                        pending.Push(child);
                    }
                }
                catch (Exception ex) when (IsUnavailable(ex)) { result.UnavailableLocations++; }
                ReportProgress(result,progress,lastReport);
            }
            result.Files.Sort((a, b) => StringComparer.OrdinalIgnoreCase.Compare(a.FullPath, b.FullPath));
            Serilog.Log.Information("Local drive discovery completed: {Folders} folders, {Files} files, {Candidates} candidates, {Partial} partial",
                result.FoldersVisited, result.FilesVisited, result.Files.Count, result.LimitReached);
            return result;
        }

        private static void ReportProgress(LocalDocumentScanResult result,IProgress<LocalDocumentScanProgress> progress,System.Diagnostics.Stopwatch elapsed)
        {
            if(elapsed.ElapsedMilliseconds<150)return;
            progress?.Report(new LocalDocumentScanProgress {FoldersVisited=result.FoldersVisited,FilesVisited=result.FilesVisited,
                CandidatesFound=result.Files.Count,SkippedLocations=result.UnavailableLocations+result.LinksSkipped+result.SystemFoldersSkippedCount});
            elapsed.Restart();
        }

        private static bool IsUnavailable(Exception ex) => ex is UnauthorizedAccessException || ex is IOException || ex is SecurityException;

        internal static bool StillMatchesDiscovery(LocalDocumentCandidate candidate)
        {
            try
            {
                var file = new FileInfo(candidate.FullPath);
                for(var directory=file.Directory;directory!=null;directory=directory.Parent)
                    if((directory.Attributes & FileAttributes.ReparsePoint)!=0)return false;
                return file.Exists && (file.Attributes & FileAttributes.ReparsePoint) == 0
                    && file.Length == candidate.SizeBytes && file.LastWriteTimeUtc == candidate.ModifiedUtc;
            }
            catch (Exception ex) when (IsUnavailable(ex)) { return false; }
        }
    }
}
