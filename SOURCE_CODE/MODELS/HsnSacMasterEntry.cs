namespace HVAC_Pro_Desktop.Models
{
    public class HsnSacMasterEntry
    {
        public int MasterID { get; set; }
        public string CodeType { get; set; } = "HSN";
        public string Code { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
        public string BusinessCategory { get; set; } = string.Empty;
        public decimal TaxRate { get; set; } = 18m;
        public decimal CGSTRate { get; set; } = 9m;
        public decimal SGSTRate { get; set; } = 9m;
        public decimal IGSTRate { get; set; } = 18m;
        public string Notes { get; set; } = string.Empty;
        public bool IsDefault { get; set; }
        public bool IsActive { get; set; } = true;
    }
}
