namespace HVAC_Pro_Desktop.Models
{
    public class Technician
    {
        public int TechnicianID { get; set; }
        public string Name { get; set; }
        public string Phone { get; set; }
        public decimal HourlyRate { get; set; }
        public string Designation { get; set; }  // Senior, Junior
        public int YearsExperience { get; set; }
    }
}
