using System;
using System.Collections.Generic;

namespace HVAC_Pro_Desktop.Models
{
    public class Vendor
    {
        public int     VendorID    { get; set; }
        public string  VendorName  { get; set; }
        public string  GSTNumber   { get; set; }
        public int     DefaultCreditDays { get; set; } = 30;
        public string  PANNumber   { get; set; }
        public string  Phone       { get; set; }
        public string  Email       { get; set; }
        public string  Address     { get; set; }
        public string  City        { get; set; }
        public string  Category    { get; set; }
        public string  WhatsAppNumber { get; set; }
        public string  VendorType  { get; set; } = "Supplier";
        public string  MSMERegistered { get; set; } = "No";
        public string  MSMENumber { get; set; }
        public string  GSTRegistrationType { get; set; } = "Regular";
        public bool    TDSApplicable { get; set; }
        public string  TDSSection { get; set; }
        public decimal TDSRate { get; set; }
        public bool    RCMApplicable { get; set; }
        public bool    IsSupplier { get; set; } = true;
        public bool    IsServiceVendor { get; set; }
        public string  BankAccountNumber { get; set; }
        public string  BankIFSC { get; set; }
        public string  BankAccountName { get; set; }
        public string  BankName { get; set; }
        public string  PreferredPaymentMode { get; set; }
        public string  StateCode { get; set; }
        public string  Notes { get; set; }
        public bool    IsArchived { get; set; }
        public string  SpecialisationTags { get; set; }
        public decimal TotalPurchased { get; set; }
        public bool    IsActive    { get; set; }
        public double? GeoLatitude { get; set; }
        public double? GeoLongitude { get; set; }
        public string  GeocodeAddress { get; set; }
        public string  GeocodeStatus { get; set; }
        public DateTime? GeocodeUpdatedOn { get; set; }
        public DateTime CreatedDate { get; set; }

        /// <summary>Returns true when this party can be selected for material, inventory, and purchase workflows.</summary>
        public bool CanSupplyMaterials => IsSupplier;

        /// <summary>Returns true when this party can be selected for service, subcontracting, and labour workflows.</summary>
        public bool CanProvideServices => IsServiceVendor;

        public override string ToString() => VendorName;
    }

    public class VendorSummaryDto
    {
        public int VendorId { get; set; }
        public string VendorName { get; set; }
        public string Category { get; set; }
        public string VendorType { get; set; }
        public string City { get; set; }
        public string State { get; set; }
        public string Phone { get; set; }
        public bool IsSupplier { get; set; }
        public bool IsServiceVendor { get; set; }
        public bool IsActive { get; set; }
        public bool IsArchived { get; set; }
        public decimal OutstandingBalance { get; set; }
        public int OpenPOCount { get; set; }
        public bool HasOverdue { get; set; }
        public bool IsDuplicate { get; set; }
        public decimal TotalPurchased { get; set; }
        public string MSMERegistered { get; set; }
    }

    public class DuplicateVendorItemDto
    {
        public int VendorId { get; set; }
        public string VendorName { get; set; }
        public int OpenPOCount { get; set; }
        public decimal OutstandingBalance { get; set; }
    }

    public class DuplicateGroupDto
    {
        public string NormalisedName { get; set; }
        public List<DuplicateVendorItemDto> Vendors { get; set; } = new List<DuplicateVendorItemDto>();
        public decimal CombinedOutstanding { get; set; }
    }

    public class VendorDetailDto : Vendor
    {
        public decimal OutstandingBalance { get; set; }
        public int OpenPOCount { get; set; }
        public List<PurchaseOrder> RecentPOs { get; set; } = new List<PurchaseOrder>();
    }
}
