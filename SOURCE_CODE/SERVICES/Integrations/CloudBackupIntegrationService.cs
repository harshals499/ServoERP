using System;
using System.IO;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Threading;
using System.Threading.Tasks;
using HVAC_Pro_Desktop.Models;

namespace HVAC_Pro_Desktop.Services.Integrations
{
    public sealed class CloudBackupIntegrationService
    {
        private const string Provider = "Cloud Backup";
        private readonly BackupService _backupService = new BackupService();

        public bool IsEnabled => IntegrationConfig.GetBool("CloudBackup", "Enabled", false);

        public async Task<IntegrationOperationResult> CreateAndUploadBackupAsync(CancellationToken cancellationToken)
        {
            BackupResult backup = _backupService.CreateDatabaseBackup("Cloud backup integration");
            if (backup == null || !backup.Success)
                return IntegrationOperationResult.Fail(Provider, "CreateAndUploadBackup", backup == null ? "Backup failed." : backup.Message);

            IntegrationOperationResult upload = await UploadBackupAsync(backup.BackupPath, cancellationToken).ConfigureAwait(false);
            if (upload != null && string.IsNullOrWhiteSpace(upload.LocalPath))
                upload.LocalPath = backup.BackupPath;
            return upload;
        }

        public async Task<IntegrationOperationResult> UploadBackupAsync(string backupPath, CancellationToken cancellationToken)
        {
            if (string.IsNullOrWhiteSpace(backupPath) || !File.Exists(backupPath))
                return IntegrationOperationResult.Fail(Provider, "UploadBackup", "Backup file was not found.");

            if (!IsEnabled)
                return IntegrationOperationResult.Fail(Provider, "UploadBackup", "Cloud backup is disabled.");

            string targetType = IntegrationConfig.Get("CloudBackup", "TargetType", "LocalFolder");
            if (string.Equals(targetType, "HttpPut", StringComparison.OrdinalIgnoreCase))
                return await UploadWithHttpPutAsync(backupPath, cancellationToken).ConfigureAwait(false);

            return CopyToFolder(backupPath);
        }

        private IntegrationOperationResult CopyToFolder(string backupPath)
        {
            try
            {
                string targetFolder = IntegrationConfig.Get(
                    "CloudBackup",
                    "TargetPath",
                    Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData), "ServoERP", "CloudBackups"));

                Directory.CreateDirectory(targetFolder);
                string targetPath = Path.Combine(targetFolder, Path.GetFileName(backupPath));
                File.Copy(backupPath, targetPath, true);

                var result = IntegrationOperationResult.Ok(Provider, "UploadBackup", "Backup copied to configured cloud/local folder.");
                result.LocalPath = targetPath;
                result.ReferenceId = Path.GetFileName(targetPath);
                return result;
            }
            catch (Exception ex)
            {
                AppLogger.LogError("CloudBackupIntegrationService.CopyToFolder", ex);
                return IntegrationOperationResult.Fail(Provider, "UploadBackup", ex.Message);
            }
        }

        private async Task<IntegrationOperationResult> UploadWithHttpPutAsync(string backupPath, CancellationToken cancellationToken)
        {
            string uploadUrl = IntegrationConfig.Get("CloudBackup", "UploadUrl", string.Empty);
            if (string.IsNullOrWhiteSpace(uploadUrl))
                return IntegrationOperationResult.Fail(Provider, "UploadBackup", "Cloud backup UploadUrl is not configured.");

            try
            {
                using (var client = new HttpClient { Timeout = TimeSpan.FromMinutes(30) })
                using (var stream = File.OpenRead(backupPath))
                using (var content = new StreamContent(stream))
                {
                    content.Headers.ContentType = new MediaTypeHeaderValue("application/octet-stream");
                    string bearer = IntegrationConfig.Get("CloudBackup", "BearerToken", string.Empty);
                    if (!string.IsNullOrWhiteSpace(bearer))
                        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", bearer.Trim());

                    using (HttpResponseMessage response = await client.PutAsync(uploadUrl, content, cancellationToken).ConfigureAwait(false))
                    {
                        string raw = await response.Content.ReadAsStringAsync().ConfigureAwait(false);
                        var result = response.IsSuccessStatusCode
                            ? IntegrationOperationResult.Ok(Provider, "UploadBackup", "Backup uploaded.")
                            : IntegrationOperationResult.Fail(Provider, "UploadBackup", "Backup upload returned HTTP " + (int)response.StatusCode + ".");
                        result.LocalPath = backupPath;
                        result.ReferenceId = Path.GetFileName(backupPath);
                        result.RawResponse = raw;
                        return result;
                    }
                }
            }
            catch (Exception ex)
            {
                AppLogger.LogError("CloudBackupIntegrationService.UploadWithHttpPutAsync", ex);
                return IntegrationOperationResult.Fail(Provider, "UploadBackup", ex.Message);
            }
        }
    }
}
