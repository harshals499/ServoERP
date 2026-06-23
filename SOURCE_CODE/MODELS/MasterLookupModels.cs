using System;

namespace HVAC_Pro_Desktop.Models
{
    public sealed class MasterLookupCategory
    {
        public int CategoryId { get; set; }
        public string CategoryKey { get; set; }
        public string ModuleKey { get; set; }
        public string DisplayName { get; set; }
        public string Description { get; set; }
        public bool IsSystem { get; set; }
        public bool IsActive { get; set; }
        public int SortOrder { get; set; }
        public DateTime CreatedDate { get; set; }
        public DateTime ModifiedDate { get; set; }
    }

    public sealed class MasterLookupValue
    {
        public int ValueId { get; set; }
        public int CategoryId { get; set; }
        public string CategoryKey { get; set; }
        public string ModuleKey { get; set; }
        public string ValueCode { get; set; }
        public string DisplayText { get; set; }
        public string Description { get; set; }
        public string MetadataJson { get; set; }
        public bool IsDefault { get; set; }
        public bool IsSystem { get; set; }
        public bool IsActive { get; set; }
        public int SortOrder { get; set; }
        public DateTime CreatedDate { get; set; }
        public DateTime ModifiedDate { get; set; }
    }
}
