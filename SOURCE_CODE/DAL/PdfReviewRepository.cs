using System;
using System.Collections.Generic;
using System.Data;
using System.Data.SqlClient;
using System.IO;
using System.Linq;
using System.Threading;
using Dapper;
using FluentValidation;
using HVAC_Pro_Desktop.Models;
using HVAC_Pro_Desktop.Services;

namespace HVAC_Pro_Desktop.DAL
{
    internal sealed class PdfReviewRepository
    {
        internal static void EnsureSchema(SqlConnection connection)
        {
            // Additive schema guard, deliberately not run by scan, preview or categorisation.
            connection.Execute(@"
IF OBJECT_ID(N'dbo.PdfReviewDocuments',N'U') IS NULL
CREATE TABLE dbo.PdfReviewDocuments(
 DocumentId int NOT NULL PRIMARY KEY REFERENCES dbo.ClientDocuments(DocumentId) ON DELETE CASCADE, FileHash char(64) NOT NULL UNIQUE,
 Category nvarchar(32) NOT NULL, SourceFolder nvarchar(2000) NOT NULL,
 ImportedUtc datetime2 NOT NULL DEFAULT SYSUTCDATETIME());");
        }
        internal bool Exists(string hash)
        {
            using (var c = DatabaseConnectionFactory.CreateConnection())
            {
                c.Open();
                if (c.ExecuteScalar<int>("SELECT CASE WHEN OBJECT_ID(N'dbo.PdfReviewDocuments',N'U') IS NULL THEN 0 ELSE 1 END") == 0) return false;
                return c.ExecuteScalar<int>("SELECT COUNT(*) FROM dbo.PdfReviewDocuments WHERE FileHash=@hash", new { hash }) > 0;
            }
        }
        internal List<PdfLibraryRecord> List(PdfCategory? category)
        {
            if (category.HasValue) SessionManager.DemandPermission(PdfCategories.Module(category.Value), "View");
            else SessionManager.DemandPermission("MasterData", "View");
            using (var c = DatabaseConnectionFactory.CreateConnection())
            {
                c.Open();
                if (c.ExecuteScalar<int>("SELECT CASE WHEN OBJECT_ID(N'dbo.PdfReviewDocuments',N'U') IS NULL THEN 0 ELSE 1 END") == 0) return new List<PdfLibraryRecord>();
                return c.Query<PdfLibraryRecord>(@"SELECT TOP(1000) p.DocumentId,d.OriginalFileName FileName,p.Category,p.SourceFolder,p.ImportedUtc,d.FilePath,p.FileHash
FROM dbo.PdfReviewDocuments p INNER JOIN dbo.ClientDocuments d ON d.DocumentId=p.DocumentId
WHERE @category IS NULL OR p.Category=@category ORDER BY p.ImportedUtc DESC,p.DocumentId DESC", new { category = category?.ToString() }).ToList();
            }
        }
        internal int Import(PdfReviewFile file, CancellationToken token, string testVault = null)
        {
            new ServoERP.Validators.PdfReviewFileValidator().ValidateAndThrow(file);
            var category = file.Category.Value;
            SessionManager.DemandPermission("MasterData", "Create");
            SessionManager.DemandPermission(PdfCategories.Module(category), "Create");
            if (testVault != null && !new SqlConnectionStringBuilder(DatabaseConnectionFactory.DiagnosticConnectionString ?? "").InitialCatalog.StartsWith("ServoERP_PdfReviewQA_", StringComparison.Ordinal))
                throw new InvalidOperationException("A test vault requires a disposable PDF review QA database.");
            token.ThrowIfCancellationRequested();
            if (file.Hash == null || file.Hash.Length != 64 || PdfReviewService.Hash(file.SnapshotPath, token) != file.Hash)
                throw new InvalidOperationException("The reviewed PDF changed. Reopen it before importing.");
            string vault = testVault ?? Path.Combine(@"C:\HVAC_PRO_MSE", "CLIENT_DATA_UPLOADS", "ReviewedPDFs");
            Directory.CreateDirectory(vault);
            string path = Path.Combine(vault, file.Hash + ".pdf");
            if (!File.Exists(path))
            {
                string staging = Path.Combine(vault, Guid.NewGuid().ToString("N") + ".pending");
                try
                {
                    File.Copy(file.SnapshotPath, staging, false);
                    try { File.Move(staging, path); } catch (IOException) { if (!File.Exists(path)) throw; }
                }
                finally { if (File.Exists(staging)) File.Delete(staging); }
            }
            if (PdfReviewService.Hash(path, token) != file.Hash) throw new InvalidOperationException("Stored PDF integrity check failed.");
            using (var c = DatabaseConnectionFactory.CreateConnection())
            {
                c.Open(); EnsureSchema(c);
                using (var tx = c.BeginTransaction(IsolationLevel.Serializable))
                {
                    int lockResult = c.ExecuteScalar<int>("DECLARE @r int; EXEC @r=sys.sp_getapplock @Resource=N'ServoERP.PdfReview.Import',@LockMode='Exclusive',@LockOwner='Transaction',@LockTimeout=15000; SELECT @r;", transaction: tx);
                    if (lockResult < 0) throw new InvalidOperationException("Another PDF import is saving. Retry shortly.");
                    if (c.ExecuteScalar<int>("SELECT COUNT(*) FROM dbo.PdfReviewDocuments WHERE FileHash=@Hash", new { file.Hash }, tx) > 0)
                        throw new InvalidOperationException("This PDF is already imported. Open Imported PDFs to find it.");
                    int id = c.ExecuteScalar<int>(@"INSERT dbo.ClientDocuments(DocumentType,Title,FilePath,OriginalFileName,Notes,UploadedBy)
VALUES(@Type,@Title,@Path,@FileName,@Notes,@Actor); SELECT CAST(SCOPE_IDENTITY() AS int);",
                        new { Type = PdfCategories.DocumentType(category), Title = file.FileName.Length > 180 ? file.FileName.Substring(0, 180) : file.FileName, Path = path, file.FileName,
                            Notes = "Reviewed PDF attachment only. No transaction created. Destination: " + PdfCategories.Destination(category), Actor = SessionManager.CurrentUser?.DisplayName }, tx);
                    c.Execute("INSERT dbo.PdfReviewDocuments(DocumentId,FileHash,Category,SourceFolder) VALUES(@id,@Hash,@Category,@Folder)",
                        new { id, file.Hash, Category = category.ToString(), Folder = file.Folder.Length > 2000 ? file.Folder.Substring(0, 2000) : file.Folder }, tx);
                    token.ThrowIfCancellationRequested(); tx.Commit();
                    AppDataCache.RemovePrefix("masterdata:");
                    Serilog.Log.Information("Reviewed PDF attachment {DocumentId} imported to {Module}", id, PdfCategories.Module(category));
                    return id;
                }
            }
        }
    }
}
