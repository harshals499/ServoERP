using System;
using System.Collections.Generic;

namespace HVAC_Pro_Desktop.Models
{
    public sealed class TallyConnectionSettings
    {
        public bool Enabled { get; set; }
        public string EndpointUrl { get; set; }
        public string ExportFolder { get; set; }
        public string CompanyName { get; set; }
        public string DefaultGodownName { get; set; }
    }

    public sealed class TallyLedgerMapping
    {
        public int MappingID { get; set; }
        public string MappingType { get; set; }
        public string ServoKey { get; set; }
        public string TallyLedgerName { get; set; }
        public int? TallyMasterId { get; set; }
        public bool IsDefault { get; set; }
        public DateTime UpdatedAt { get; set; }
    }

    public sealed class TallyExportCandidate
    {
        public int EntityID { get; set; }
        public string EntityType { get; set; }
        public string Number { get; set; }
        public string PartyName { get; set; }
        public string TallyPartyName { get; set; }
        public DateTime VoucherDate { get; set; }
        public decimal Amount { get; set; }
        public string Status { get; set; }
        public string TallyExportStatus { get; set; }
        public string MissingFields { get; set; }
    }

    public sealed class TallyMasterRecord
    {
        public string MasterType { get; set; }
        public string Name { get; set; }
        public string Parent { get; set; }
        public string Guid { get; set; }
        public int? MasterId { get; set; }
        public string Gstin { get; set; }
        public string Unit { get; set; }
        public string HsnCode { get; set; }
        public decimal ClosingBalance { get; set; }
        public decimal ClosingQuantity { get; set; }
        public decimal Rate { get; set; }
    }

    public sealed class TallyImportSummary
    {
        public int ImportBatchID { get; set; }
        public int CreatedCount { get; set; }
        public int UpdatedCount { get; set; }
        public int SkippedCount { get; set; }
        public int ErrorCount { get; set; }
        public List<string> Messages { get; set; } = new List<string>();
    }

    public sealed class TallySyncLogEntry
    {
        public int SyncLogID { get; set; }
        public string Direction { get; set; }
        public string EntityType { get; set; }
        public int? EntityID { get; set; }
        public string Operation { get; set; }
        public string Status { get; set; }
        public string Message { get; set; }
        public string LocalXmlPath { get; set; }
        public string RawResponse { get; set; }
        public DateTime CreatedAt { get; set; }
    }

    public sealed class TallyBatchResult
    {
        public int SuccessCount { get; set; }
        public int FailedCount { get; set; }
        public int SkippedCount { get; set; }
        public string OutputFolder { get; set; }
        public List<string> Messages { get; set; } = new List<string>();
    }

    public sealed class TallyInventoryConflict
    {
        public int? ServoItemID { get; set; }
        public string ServoItemName { get; set; }
        public string TallyItemName { get; set; }
        public string ServoUnit { get; set; }
        public string TallyUnit { get; set; }
        public decimal ServoStock { get; set; }
        public decimal TallyStock { get; set; }
        public string SuggestedAction { get; set; }
    }
}
