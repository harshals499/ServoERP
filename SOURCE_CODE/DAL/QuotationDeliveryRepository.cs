using System;
using System.Data;
using System.Data.SqlClient;
using System.Linq;
using Dapper;
using HVAC_Pro_Desktop.Models;
using HVAC_Pro_Desktop.Services;

namespace HVAC_Pro_Desktop.DAL
{
    /// <summary>Persists the reviewed outputs of an accepted-quotation delivery handoff.</summary>
    public sealed class QuotationDeliveryRepository
    {
        private readonly DatabaseManager _db = new DatabaseManager();

        public QuotationDeliveryResult FindCompleted(int quotationId)
        {
            using (SqlConnection connection = _db.GetConnection())
            {
                connection.Open();
                EnsureSchema(connection);
                return connection.QuerySingleOrDefault<QuotationDeliveryResult>(@"
SELECT TOP 1
       b.DeliveryBatchId,
       b.JobId,
       j.JobNumber,
       CAST(1 AS bit) AS WasAlreadyCreated,
       (SELECT COUNT(*) FROM JobChecklistItems c WHERE c.JobId=b.JobId) AS ChecklistItemCount,
       (SELECT COUNT(*) FROM JobMaterialReservations r WHERE r.DeliveryBatchId=b.DeliveryBatchId) AS ReservationCount,
       (SELECT COUNT(*) FROM PurchaseRequirements p WHERE p.DeliveryBatchId=b.DeliveryBatchId) AS PurchaseRequirementCount,
       (SELECT COUNT(*) FROM JobBillingSchedules s WHERE s.DeliveryBatchId=b.DeliveryBatchId) AS BillingMilestoneCount
FROM QuotationDeliveryBatches b
LEFT JOIN Jobs j ON j.JobID=b.JobId
WHERE b.QuotationBidId=@quotationId AND b.Status='Completed';", new { quotationId });
            }
        }

        public int GetOrCreatePendingBatch(int quotationId)
        {
            using (SqlConnection connection = _db.GetConnection())
            {
                connection.Open();
                EnsureSchema(connection);
                // Serializable transaction is intentional: the uniqueness check and pending-batch
                // insert are dependent and must remain idempotent across office PCs.
                using (SqlTransaction transaction = connection.BeginTransaction(IsolationLevel.Serializable))
                {
                    int? existing = connection.QuerySingleOrDefault<int?>(
                        "SELECT DeliveryBatchId FROM QuotationDeliveryBatches WITH (UPDLOCK, HOLDLOCK) WHERE QuotationBidId=@quotationId;",
                        new { quotationId }, transaction);
                    int deliveryBatchId = existing ?? connection.QuerySingle<int>(@"
INSERT INTO QuotationDeliveryBatches (QuotationBidId, Status, CreatedByName)
OUTPUT INSERTED.DeliveryBatchId
VALUES (@quotationId, 'Pending', @createdByName);",
                        new { quotationId, createdByName = CurrentUserName() }, transaction);
                    transaction.Commit();
                    return deliveryBatchId;
                }
            }
        }

        public int? RecoverJobId(int deliveryBatchId)
        {
            using (SqlConnection connection = _db.GetConnection())
            {
                connection.Open();
                EnsureSchema(connection);
                int? linked = connection.QuerySingleOrDefault<int?>(
                    "SELECT JobId FROM QuotationDeliveryBatches WHERE DeliveryBatchId=@deliveryBatchId;",
                    new { deliveryBatchId });
                if (linked.HasValue)
                    return linked;

                string marker = "[DeliveryBatch:" + deliveryBatchId + "]";
                return connection.QuerySingleOrDefault<int?>(
                    "SELECT TOP 1 JobID FROM Jobs WHERE Notes LIKE @marker ORDER BY JobID DESC;",
                    new { marker = "%" + marker + "%" });
            }
        }

        public QuotationDeliveryResult Complete(int deliveryBatchId, int jobId, QuotationDeliveryPlan plan)
        {
            if (plan == null) throw new ArgumentNullException(nameof(plan));

            using (SqlConnection connection = _db.GetConnection())
            {
                connection.Open();
                EnsureSchema(connection);

                // Raw SqlTransaction is intentional: checklist, reservations, purchase requirements,
                // billing milestones, and the handoff status are one dependent business operation.
                using (SqlTransaction transaction = connection.BeginTransaction(IsolationLevel.Serializable))
                {
                    string status = connection.QuerySingle<string>(
                        "SELECT Status FROM QuotationDeliveryBatches WITH (UPDLOCK, HOLDLOCK) WHERE DeliveryBatchId=@deliveryBatchId;",
                        new { deliveryBatchId }, transaction);

                    if (!string.Equals(status, "Completed", StringComparison.OrdinalIgnoreCase))
                    {
                        int sortOrder = connection.QuerySingle<int>(
                            "SELECT ISNULL(MAX(SortOrder), 0) FROM JobChecklistItems WHERE JobId=@jobId;",
                            new { jobId }, transaction);
                        foreach (string item in plan.ChecklistItems.Where(value => !string.IsNullOrWhiteSpace(value)))
                        {
                            sortOrder++;
                            connection.Execute(@"
IF NOT EXISTS (SELECT 1 FROM JobChecklistItems WHERE JobId=@jobId AND ItemText=@itemText)
    INSERT INTO JobChecklistItems (JobId, ItemText, IsCompleted, SortOrder)
    VALUES (@jobId, @itemText, 0, @sortOrder);",
                                new { jobId, itemText = item.Trim(), sortOrder }, transaction);
                        }

                        foreach (QuotationDeliveryMaterialLine line in plan.Materials.Where(value => value.QuantityRequired > 0m))
                        {
                            connection.Execute(@"
INSERT INTO JobMaterialReservations
    (DeliveryBatchId, JobId, QuotationLineItemId, InventoryItemId, ItemDescription,
     QuantityRequired, QuantityReserved, Unit, ReservationStatus, PreferredVendorId, PreferredVendorName)
VALUES
    (@deliveryBatchId, @jobId, @QuotationLineItemId, @InventoryItemId, @ItemDescription,
     @QuantityRequired, @QuantityReserved, @Unit, @ReservationStatus, @PreferredVendorId, @PreferredVendorName);",
                                new
                                {
                                    deliveryBatchId,
                                    jobId,
                                    line.QuotationLineItemId,
                                    line.InventoryItemId,
                                    line.ItemDescription,
                                    line.QuantityRequired,
                                    line.QuantityReserved,
                                    line.Unit,
                                    line.ReservationStatus,
                                    line.PreferredVendorId,
                                    line.PreferredVendorName
                                }, transaction);
                        }

                        foreach (QuotationPurchaseRequirement requirement in plan.PurchaseRequirements.Where(value => value.QuantityRequired > 0m))
                        {
                            connection.Execute(@"
INSERT INTO PurchaseRequirements
    (DeliveryBatchId, JobId, QuotationLineItemId, InventoryItemId, ItemDescription,
     QuantityRequired, Unit, PreferredVendorId, PreferredVendorName, RequiredByDate, Status)
VALUES
    (@deliveryBatchId, @jobId, @QuotationLineItemId, @InventoryItemId, @ItemDescription,
     @QuantityRequired, @Unit, @PreferredVendorId, @PreferredVendorName, @RequiredByDate, 'Draft');",
                                new
                                {
                                    deliveryBatchId,
                                    jobId,
                                    requirement.QuotationLineItemId,
                                    requirement.InventoryItemId,
                                    requirement.ItemDescription,
                                    requirement.QuantityRequired,
                                    requirement.Unit,
                                    requirement.PreferredVendorId,
                                    requirement.PreferredVendorName,
                                    requirement.RequiredByDate
                                }, transaction);
                        }

                        foreach (QuotationBillingMilestone milestone in plan.BillingSchedule.Where(value => value.Amount >= 0m))
                        {
                            connection.Execute(@"
INSERT INTO JobBillingSchedules
    (DeliveryBatchId, JobId, QuotationBidId, MilestoneName, Amount, DueDate, Status)
VALUES
    (@deliveryBatchId, @jobId, @QuotationId, @MilestoneName, @Amount, @DueDate, 'Planned');",
                                new
                                {
                                    deliveryBatchId,
                                    jobId,
                                    plan.QuotationId,
                                    milestone.MilestoneName,
                                    milestone.Amount,
                                    milestone.DueDate
                                }, transaction);
                        }

                        connection.Execute(@"
UPDATE QuotationDeliveryBatches
SET JobId=@jobId, Status='Completed', CompletedDate=GETDATE()
WHERE DeliveryBatchId=@deliveryBatchId;

UPDATE Quotations
SET CustomerDocumentStatus='Job Created',
    FlowNotes=CASE WHEN NULLIF(LTRIM(RTRIM(ISNULL(FlowNotes,''))), '') IS NULL
                   THEN @flowNote ELSE FlowNotes + CHAR(13) + CHAR(10) + @flowNote END
WHERE BidID=@QuotationId;",
                            new
                            {
                                jobId,
                                deliveryBatchId,
                                plan.QuotationId,
                                flowNote = "Delivery wizard completed for job #" + jobId + "."
                            }, transaction);
                    }

                    transaction.Commit();
                }

                return FindCompleted(plan.QuotationId);
            }
        }

        internal static void EnsureSchema(SqlConnection connection)
        {
            connection.Execute(@"
IF OBJECT_ID('dbo.QuotationDeliveryBatches', 'U') IS NULL
BEGIN
    CREATE TABLE dbo.QuotationDeliveryBatches (
        DeliveryBatchId INT IDENTITY(1,1) PRIMARY KEY,
        QuotationBidId INT NOT NULL,
        JobId INT NULL,
        Status NVARCHAR(30) NOT NULL CONSTRAINT DF_QuotationDeliveryBatches_Status DEFAULT 'Pending',
        CreatedByName NVARCHAR(150) NULL,
        CreatedDate DATETIME NOT NULL CONSTRAINT DF_QuotationDeliveryBatches_CreatedDate DEFAULT GETDATE(),
        CompletedDate DATETIME NULL,
        CONSTRAINT UQ_QuotationDeliveryBatches_Quotation UNIQUE (QuotationBidId)
    );
END;

IF OBJECT_ID('dbo.JobMaterialReservations', 'U') IS NULL
BEGIN
    CREATE TABLE dbo.JobMaterialReservations (
        ReservationId INT IDENTITY(1,1) PRIMARY KEY,
        DeliveryBatchId INT NOT NULL,
        JobId INT NOT NULL,
        QuotationLineItemId INT NULL,
        InventoryItemId INT NULL,
        ItemDescription NVARCHAR(1000) NOT NULL,
        QuantityRequired DECIMAL(10,3) NOT NULL,
        QuantityReserved DECIMAL(10,3) NOT NULL,
        Unit NVARCHAR(30) NOT NULL,
        ReservationStatus NVARCHAR(30) NOT NULL,
        PreferredVendorId INT NULL,
        PreferredVendorName NVARCHAR(255) NULL,
        CreatedDate DATETIME NOT NULL CONSTRAINT DF_JobMaterialReservations_CreatedDate DEFAULT GETDATE()
    );
END;

IF OBJECT_ID('dbo.PurchaseRequirements', 'U') IS NULL
BEGIN
    CREATE TABLE dbo.PurchaseRequirements (
        RequirementId INT IDENTITY(1,1) PRIMARY KEY,
        DeliveryBatchId INT NOT NULL,
        JobId INT NOT NULL,
        QuotationLineItemId INT NULL,
        InventoryItemId INT NULL,
        ItemDescription NVARCHAR(1000) NOT NULL,
        QuantityRequired DECIMAL(10,3) NOT NULL,
        Unit NVARCHAR(30) NOT NULL,
        PreferredVendorId INT NULL,
        PreferredVendorName NVARCHAR(255) NULL,
        RequiredByDate DATETIME NOT NULL,
        Status NVARCHAR(30) NOT NULL CONSTRAINT DF_PurchaseRequirements_Status DEFAULT 'Draft',
        CreatedDate DATETIME NOT NULL CONSTRAINT DF_PurchaseRequirements_CreatedDate DEFAULT GETDATE()
    );
END;

IF OBJECT_ID('dbo.JobBillingSchedules', 'U') IS NULL
BEGIN
    CREATE TABLE dbo.JobBillingSchedules (
        BillingScheduleId INT IDENTITY(1,1) PRIMARY KEY,
        DeliveryBatchId INT NOT NULL,
        JobId INT NOT NULL,
        QuotationBidId INT NOT NULL,
        MilestoneName NVARCHAR(150) NOT NULL,
        Amount DECIMAL(18,2) NOT NULL,
        DueDate DATETIME NOT NULL,
        Status NVARCHAR(30) NOT NULL CONSTRAINT DF_JobBillingSchedules_Status DEFAULT 'Planned',
        InvoiceId INT NULL,
        CreatedDate DATETIME NOT NULL CONSTRAINT DF_JobBillingSchedules_CreatedDate DEFAULT GETDATE()
    );
END;

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name='IX_JobMaterialReservations_JobId' AND object_id=OBJECT_ID('dbo.JobMaterialReservations'))
    CREATE INDEX IX_JobMaterialReservations_JobId ON dbo.JobMaterialReservations(JobId, ReservationStatus);
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name='IX_PurchaseRequirements_JobId' AND object_id=OBJECT_ID('dbo.PurchaseRequirements'))
    CREATE INDEX IX_PurchaseRequirements_JobId ON dbo.PurchaseRequirements(JobId, Status);
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name='IX_JobBillingSchedules_JobId' AND object_id=OBJECT_ID('dbo.JobBillingSchedules'))
    CREATE INDEX IX_JobBillingSchedules_JobId ON dbo.JobBillingSchedules(JobId, Status);");
        }

        private static string CurrentUserName()
        {
            return SessionManager.IsLoggedIn && SessionManager.CurrentUser != null
                ? SessionManager.CurrentUser.DisplayName
                : Environment.UserName;
        }
    }
}
