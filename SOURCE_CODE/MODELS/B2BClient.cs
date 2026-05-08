using System;
using System.Collections.Generic;

namespace HVAC_Pro_Desktop.Models
{
    public class B2BClient
    {
        // Core identity
        public int    ClientID      { get; set; }
        public string CompanyName   { get; set; }
        public string IndustryType  { get; set; }   // IT, Banking, Healthcare, Manufacturing

        // Contact
        public string PrimaryContact  { get; set; }
        public string SecondaryContact{ get; set; }
        public string Phone           { get; set; }
        public string Email           { get; set; }

        // Indian-specific financials
        public string  GSTNumber        { get; set; }   // e.g. 27AAAAA0000A1Z5
        public string  PANNumber        { get; set; }   // e.g. AAAAA0000A
        public int     PaymentTermsDays { get; set; }   // 30 / 45 / 60
        public decimal CreditLimit      { get; set; }

        // Address
        public string BillingAddress { get; set; }
        public string City           { get; set; }
        public double? GeoLatitude { get; set; }
        public double? GeoLongitude { get; set; }
        public string GeocodeAddress { get; set; }
        public string GeocodeStatus { get; set; }
        public DateTime? GeocodeUpdatedOn { get; set; }
        public string RelationshipStage { get; set; }
        public string Tags { get; set; }
        public int HealthScore { get; set; }
        public string Notes { get; set; }
        public string AssignedTo { get; set; }
        public string LeadSource { get; set; }

        // Legacy / computed
        public decimal TotalAnnualValue { get; set; }   // Sum of active contracts
        public DateTime CustomerSince   { get; set; }
        public bool     IsActive        { get; set; } = true;
        public List<ClientContact> Contacts { get; set; } = new List<ClientContact>();
    }
}
