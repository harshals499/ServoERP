using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Threading;
using System.Threading.Tasks;
using HVAC_Pro_Desktop.Models;
using Newtonsoft.Json;

namespace HVAC_Pro_Desktop.Services
{
    internal sealed class PdfRenderResult
    {
        public int Pages { get; set; }
        public string Image { get; set; }
        public string Error { get; set; }
    }

    internal sealed class PdfReviewService : IDisposable
    {
        private readonly string _workspace = Path.Combine(Path.GetTempPath(), "ServoERP-PdfReview-" + Guid.NewGuid().ToString("N"));
        internal const long MaximumBytes = 200L * 1024 * 1024;
        internal const int MaximumFiles = 10000;
        internal static PdfReviewBatch Scan(string root, IProgress<string> progress, CancellationToken token, out List<string> warnings)
        {
            root = Path.GetFullPath(root);
            if (!Directory.Exists(root)) throw new InvalidOperationException("Choose an accessible folder.");
            var batch = new PdfReviewBatch();
            warnings = new List<string>();
            var pending = new Stack<string>(); pending.Push(root);
            while (pending.Count > 0)
            {
                token.ThrowIfCancellationRequested();
                string dir = pending.Pop();
                try
                {
                    // Reparse points are deliberately not followed: avoid cycles or scanning outside the selected tree.
                    foreach (string file in Directory.EnumerateFiles(dir, "*", SearchOption.TopDirectoryOnly))
                    {
                        token.ThrowIfCancellationRequested();
                        if (!string.Equals(Path.GetExtension(file), ".pdf", StringComparison.OrdinalIgnoreCase)) continue;
                        if ((File.GetAttributes(file) & FileAttributes.ReparsePoint) != 0) continue;
                        batch.Files.Add(new PdfReviewFile { SourcePath = file, Folder = dir == root ? "." : dir.Substring(root.TrimEnd(Path.DirectorySeparatorChar).Length + 1) });
                        if (batch.Files.Count >= MaximumFiles) { warnings.Add("The 10,000 PDF batch limit was reached. Review this batch, then choose a smaller folder for remaining files."); return batch; }
                    }
                    foreach (string child in Directory.EnumerateDirectories(dir))
                        if ((File.GetAttributes(child) & FileAttributes.ReparsePoint) == 0) pending.Push(child);
                    progress?.Report(batch.Files.Count + " PDFs found…");
                }
                catch (Exception ex) when (ex is UnauthorizedAccessException || ex is IOException || ex is System.Security.SecurityException)
                {
                    warnings.Add("Could not completely scan: " + dir);
                    Serilog.Log.Warning("PDF folder scan encountered {ErrorType}", ex.GetType().Name);
                }
            }
            return batch;
        }

        internal async Task PrepareAsync(PdfReviewFile file, CancellationToken token)
        {
            if (file.SnapshotPath != null && File.Exists(file.SnapshotPath)) return;
            Directory.CreateDirectory(_workspace);
            string path = Path.Combine(_workspace, Guid.NewGuid().ToString("N") + ".pdf");
            try
            {
                using (var input = new FileStream(file.SourcePath, FileMode.Open, FileAccess.Read, FileShare.Read, 81920, true))
                {
                    if (input.Length == 0 || input.Length > MaximumBytes) throw new InvalidOperationException("Choose a PDF between 1 byte and 200 MB.");
                    using (var output = new FileStream(path, FileMode.CreateNew, FileAccess.Write, FileShare.None, 81920, true))
                        await input.CopyToAsync(output, 81920, token).ConfigureAwait(false);
                }
                file.Hash = await Task.Run(() => Hash(path, token), token).ConfigureAwait(false);
                file.SnapshotPath = path;
            }
            catch { if (File.Exists(path)) File.Delete(path); throw; }
        }

        internal static string Hash(string path, CancellationToken token)
        {
            using (var hash = SHA256.Create())
            using (var stream = File.OpenRead(path))
            {
                var buffer = new byte[81920]; int count;
                while ((count = stream.Read(buffer, 0, buffer.Length)) > 0)
                { token.ThrowIfCancellationRequested(); hash.TransformBlock(buffer, 0, count, buffer, 0); }
                hash.TransformFinalBlock(new byte[0], 0, 0);
                return BitConverter.ToString(hash.Hash).Replace("-", "").ToLowerInvariant();
            }
        }

        internal async Task<PdfRenderResult> RenderAsync(PdfReviewFile file, int page, CancellationToken token)
        {
            await PrepareAsync(file, token).ConfigureAwait(false);
            string output = Path.Combine(_workspace, "preview-" + Guid.NewGuid().ToString("N") + ".png");
            string script = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "SERVICES", "PdfReview.Render.ps1");
            var info = new ProcessStartInfo(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.System), "WindowsPowerShell", "v1.0", "powershell.exe"),
                "-NoProfile -NonInteractive -ExecutionPolicy Bypass -File \"" + script + "\"")
            { UseShellExecute = false, CreateNoWindow = true, RedirectStandardInput = true, RedirectStandardOutput = true, RedirectStandardError = true };
            using (var timeout = CancellationTokenSource.CreateLinkedTokenSource(token))
            {
                timeout.CancelAfter(TimeSpan.FromSeconds(45));
                using (var process = Process.Start(info))
                using (timeout.Token.Register(() => { try { if (!process.HasExited) process.Kill(); } catch (InvalidOperationException) { } }))
                {
                    var stdout = process.StandardOutput.ReadToEndAsync(); var stderr = process.StandardError.ReadToEndAsync();
                    await process.StandardInput.WriteAsync(JsonConvert.SerializeObject(new { Path = file.SnapshotPath, Password = file.Password ?? "", Page = page, Output = output },new JsonSerializerSettings {StringEscapeHandling=StringEscapeHandling.EscapeNonAscii})).ConfigureAwait(false);
                    process.StandardInput.Close();
                    await Task.WhenAll(stdout, stderr).ConfigureAwait(false);
                    token.ThrowIfCancellationRequested();
                    if (timeout.IsCancellationRequested) throw new InvalidOperationException("Opening this PDF timed out. Retry or skip it; other files can still be reviewed.");
                    if (process.ExitCode != 0) throw new InvalidOperationException("The local PDF viewer could not run. Retry or skip this PDF.");
                    return JsonConvert.DeserializeObject<PdfRenderResult>(stdout.Result) ?? new PdfRenderResult { Error = "No PDF preview was returned. Retry or skip." };
                }
            }
        }
        internal static void DeletePreview(string path)
        {
            try { if (!string.IsNullOrEmpty(path) && File.Exists(path)) File.Delete(path); }
            catch (IOException) { }
        }
        public void Dispose()
        {
            try { if (Directory.Exists(_workspace)) Directory.Delete(_workspace, true); }
            catch (Exception ex) when (ex is IOException || ex is UnauthorizedAccessException) { Serilog.Log.Warning("PDF review temporary cleanup failed with {ErrorType}", ex.GetType().Name); }
        }
    }
}
