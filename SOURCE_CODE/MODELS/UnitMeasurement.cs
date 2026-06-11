namespace HVAC_Pro_Desktop.Models
{
    public class UnitMeasurement
    {
        public int UnitMeasurementId { get; set; }
        public string UnitCode { get; set; }
        public string DisplayName { get; set; }
        public bool IsActive { get; set; }
        public bool IsSystem { get; set; }
    }
}
