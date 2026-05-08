using System;

namespace HVAC_Pro_Desktop.Models
{
    public class ClientAsset
    {
        public int AssetId { get; set; }
        public int ClientId { get; set; }
        public int? SiteId { get; set; }
        public int? ContractId { get; set; }
        public string ClientName { get; set; }
        public string SiteName { get; set; }
        public string AssetTag { get; set; }
        public string EquipmentType { get; set; }
        public string Brand { get; set; }
        public string ModelNumber { get; set; }
        public string SerialNumber { get; set; }
        public string Capacity { get; set; }
        public string LocationDetail { get; set; }
        public DateTime? InstallDate { get; set; }
        public DateTime? WarrantyExpiry { get; set; }
        public bool IsAmcCovered { get; set; }
        public string MaintenanceFrequency { get; set; }
        public string Notes { get; set; }
        public bool IsActive { get; set; }
        public DateTime CreatedDate { get; set; }
        public DateTime ModifiedDate { get; set; }
    }

    public class ClientDocument
    {
        public int DocumentId { get; set; }
        public int? ClientId { get; set; }
        public int? SiteId { get; set; }
        public int? AssetId { get; set; }
        public int? ContractId { get; set; }
        public string ClientName { get; set; }
        public string SiteName { get; set; }
        public string DocumentType { get; set; }
        public string Title { get; set; }
        public string FilePath { get; set; }
        public string OriginalFileName { get; set; }
        public DateTime? ExpiryDate { get; set; }
        public string Notes { get; set; }
        public DateTime UploadedDate { get; set; }
        public string UploadedBy { get; set; }
    }

    public class ServiceRateCard
    {
        public int RateId { get; set; }
        public int? ClientId { get; set; }
        public string ClientName { get; set; }
        public string Category { get; set; }
        public string ServiceName { get; set; }
        public string Unit { get; set; }
        public decimal Rate { get; set; }
        public decimal GstPercent { get; set; }
        public bool IsEmergencyRate { get; set; }
        public DateTime EffectiveFrom { get; set; }
        public string Notes { get; set; }
        public bool IsActive { get; set; }
    }

    public class PrivateServerConnection
    {
        public int ConnectionId { get; set; }
        public string ConnectionName { get; set; }
        public string ServerType { get; set; }
        public string Host { get; set; }
        public int? Port { get; set; }
        public string DatabaseName { get; set; }
        public string ApiBaseUrl { get; set; }
        public string Username { get; set; }
        public string EncryptedSecret { get; set; }
        public string SyncDirection { get; set; }
        public string LastSyncStatus { get; set; }
        public DateTime? LastSyncDate { get; set; }
        public bool IsActive { get; set; }
        public DateTime CreatedDate { get; set; }
        public DateTime ModifiedDate { get; set; }
    }

    public class DataImportBatch
    {
        public int BatchId { get; set; }
        public string ImportType { get; set; }
        public string SourceFile { get; set; }
        public string Status { get; set; }
        public int TotalRows { get; set; }
        public int SuccessRows { get; set; }
        public int ErrorRows { get; set; }
        public DateTime StartedAt { get; set; }
        public DateTime? CompletedAt { get; set; }
        public string ImportedBy { get; set; }
        public string Notes { get; set; }
    }

    public class DataImportError
    {
        public int ErrorId { get; set; }
        public int BatchId { get; set; }
        public int RowNumber { get; set; }
        public string ColumnName { get; set; }
        public string ErrorMessage { get; set; }
        public string RawValue { get; set; }
    }

    public class MasterDataStatus
    {
        public string Category { get; set; }
        public int Count { get; set; }
        public bool IsComplete { get; set; }
        public string NextAction { get; set; }
    }
}
