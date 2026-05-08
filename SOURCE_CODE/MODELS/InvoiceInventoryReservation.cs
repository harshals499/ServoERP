using System;

namespace HVAC_Pro_Desktop.Models
{
    public class InvoiceInventoryReservation
    {
        public int ReservationID { get; set; }
        public int InvoiceID { get; set; }
        public int StockItemID { get; set; }
        public decimal QuantityReserved { get; set; }
        public decimal QuantityIssued { get; set; }
        public string Status { get; set; }
        public DateTime UpdatedDate { get; set; }
        public string ItemName { get; set; }
    }
}
