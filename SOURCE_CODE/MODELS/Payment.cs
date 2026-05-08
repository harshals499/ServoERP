using System;

namespace HVAC_Pro_Desktop.Models
{
    public class Payment
    {
        public int    PaymentID       { get; set; }
        public string PaymentNumber   { get; set; }   // PAY-2025-04-00001
        public int    InvoiceID       { get; set; }
        public int    ClientID        { get; set; }
        public decimal AmountPaid     { get; set; }
        public DateTime PaymentDate   { get; set; }
        // Bank Transfer | NEFT/RTGS | UPI | Cash | Cheque | DD
        public string PaymentMode     { get; set; } = "Bank Transfer";
        public string ReferenceNumber { get; set; }   // UTR / Cheque no.
        public string Notes           { get; set; }
        public DateTime CreatedDate   { get; set; }
        public int? CreatedByUserId { get; set; }
        public string CreatedByName { get; set; }
        public int? ModifiedByUserId { get; set; }
        public string ModifiedByName { get; set; }
        public DateTime? ModifiedDate { get; set; }

        // Joined display fields (not stored)
        public string InvoiceNumber { get; set; }
        public string ClientName    { get; set; }
    }
}
