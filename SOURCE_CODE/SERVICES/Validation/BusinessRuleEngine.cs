using System;
using System.Linq;
using HVAC_Pro_Desktop.Models;
using HVAC_Pro_Desktop.Models.Validation;

namespace HVAC_Pro_Desktop.Services.Validation
{
    public sealed class BusinessRuleEngine
    {
        public ValidationResult ValidateClient(B2BClient c)
        {
            var r = new ValidationResult();
            if (c == null) return r.Add(ValidationSeverity.Critical, "Clients", "Client", "Client payload is missing.");
            c.CompanyName = GlobalValidationEngine.CleanText(c.CompanyName, 255);
            if (string.IsNullOrWhiteSpace(c.CompanyName)) r.Add(ValidationSeverity.Error, "Clients", "CompanyName", "Company name is required.");
            if (!GlobalValidationEngine.IsValidEmail(c.Email)) r.Add(ValidationSeverity.Error, "Clients", "Email", "Client email format is invalid.");
            if (!GlobalValidationEngine.IsValidPhone(c.Phone)) r.Add(ValidationSeverity.Warning, "Clients", "Phone", "Client phone format looks invalid.", "Use digits, +, spaces, or dashes.", true);
            if (!GlobalValidationEngine.IsValidGSTIN(c.GSTNumber)) r.Add(ValidationSeverity.Error, "Clients", "GSTIN", "GSTIN format is invalid.");
            if (!GlobalValidationEngine.IsValidPAN(c.PANNumber)) r.Add(ValidationSeverity.Error, "Clients", "PAN", "PAN format is invalid.");
            if (c.PaymentTermsDays < 0 || c.PaymentTermsDays > 365) r.Add(ValidationSeverity.Error, "Clients", "PaymentTermsDays", "Payment terms must be between 0 and 365 days.");
            if (c.CreditLimit < 0) r.Add(ValidationSeverity.Error, "Clients", "CreditLimit", "Credit limit cannot be negative.");
            return r;
        }

        public ValidationResult ValidateVendor(Vendor v)
        {
            var r = new ValidationResult();
            if (v == null) return r.Add(ValidationSeverity.Critical, "Vendors", "Vendor", "Vendor payload is missing.");
            v.VendorName = GlobalValidationEngine.CleanText(v.VendorName, 255);
            if (string.IsNullOrWhiteSpace(v.VendorName)) r.Add(ValidationSeverity.Error, "Vendors", "VendorName", "Vendor name is required.");
            if (!GlobalValidationEngine.IsValidEmail(v.Email)) r.Add(ValidationSeverity.Error, "Vendors", "Email", "Vendor email format is invalid.");
            if (!GlobalValidationEngine.IsValidPhone(v.Phone)) r.Add(ValidationSeverity.Warning, "Vendors", "Phone", "Vendor phone format looks invalid.", "Use digits, +, spaces, or dashes.", true);
            if (!GlobalValidationEngine.IsValidGSTIN(v.GSTNumber)) r.Add(ValidationSeverity.Error, "Vendors", "GSTIN", "GSTIN format is invalid.");
            if (!GlobalValidationEngine.IsValidPAN(v.PANNumber)) r.Add(ValidationSeverity.Error, "Vendors", "PAN", "PAN format is invalid.");
            if (v.DefaultCreditDays < 0 || v.DefaultCreditDays > 365) r.Add(ValidationSeverity.Error, "Vendors", "CreditDays", "Vendor credit days must be between 0 and 365.");
            if (v.TDSRate < 0 || v.TDSRate > 100) r.Add(ValidationSeverity.Error, "Vendors", "TDSRate", "TDS rate must be 0-100%.");
            return r;
        }

        public ValidationResult ValidateSite(ClientSite s)
        {
            var r = new ValidationResult();
            if (s == null) return r.Add(ValidationSeverity.Critical, "Sites", "Site", "Site payload is missing.");
            if (s.ClientID <= 0) r.Add(ValidationSeverity.Error, "Sites", "ClientID", "Site must be linked to a client.");
            if (string.IsNullOrWhiteSpace(s.SiteName)) r.Add(ValidationSeverity.Error, "Sites", "SiteName", "Site name is required.");
            return r;
        }

        public ValidationResult ValidateInventoryItem(StockItem item)
        {
            var r = new ValidationResult();
            if (item == null) return r.Add(ValidationSeverity.Critical, "Inventory", "Item", "Inventory item payload is missing.");
            item.ItemName = GlobalValidationEngine.CleanText(item.ItemName, 255);
            if (string.IsNullOrWhiteSpace(item.ItemName)) r.Add(ValidationSeverity.Error, "Inventory", "ItemName", "Item name is required.");
            if (item.CurrentStock < 0) r.Add(ValidationSeverity.Error, "Inventory", "CurrentStock", "Stock cannot be negative.");
            if (item.ReservedStock < 0) r.Add(ValidationSeverity.Error, "Inventory", "ReservedStock", "Reserved stock cannot be negative.");
            if (item.ReservedStock > item.CurrentStock) r.Add(ValidationSeverity.Warning, "Inventory", "ReservedStock", "Reserved stock is greater than current stock.", "Review reservations.", true);
            if (item.LastPurchaseRate < 0) r.Add(ValidationSeverity.Error, "Inventory", "Rate", "Purchase rate cannot be negative.");
            if (item.ReorderLevel < 0) r.Add(ValidationSeverity.Error, "Inventory", "ReorderLevel", "Reorder level cannot be negative.");
            return r;
        }

        public ValidationResult ValidateInvoice(Invoice inv)
        {
            var r = new ValidationResult();
            if (inv == null) return r.Add(ValidationSeverity.Critical, "Invoices", "Invoice", "Invoice payload is missing.");
            if (inv.ClientID <= 0) r.Add(ValidationSeverity.Error, "Invoices", "ClientID", "Invoice must be linked to a client.");
            if (inv.SiteID <= 0) r.Add(ValidationSeverity.Error, "Invoices", "SiteID", "Invoice must be linked to a site.");
            if (!GlobalValidationEngine.IsReasonableDate(inv.InvoiceDate)) r.Add(ValidationSeverity.Error, "Invoices", "InvoiceDate", "Invoice date is impossible.");
            if (!GlobalValidationEngine.IsReasonableDate(inv.DueDate)) r.Add(ValidationSeverity.Error, "Invoices", "DueDate", "Due date is impossible.");
            if (inv.DueDate < inv.InvoiceDate) r.Add(ValidationSeverity.Error, "Invoices", "DueDate", "Due date cannot be before invoice date.");
            if (inv.LineItems == null || inv.LineItems.Count == 0) r.Add(ValidationSeverity.Error, "Invoices", "LineItems", "Invoice needs at least one line item.");
            foreach (InvoiceLineItem line in inv.LineItems ?? Enumerable.Empty<InvoiceLineItem>())
            {
                if (string.IsNullOrWhiteSpace(line.Description)) r.Add(ValidationSeverity.Error, "Invoices", "Description", "Every invoice line needs a description.");
                if (line.Quantity <= 0) r.Add(ValidationSeverity.Error, "Invoices", "Quantity", "Invoice quantity must be greater than zero.");
                if (line.Rate < 0) r.Add(ValidationSeverity.Error, "Invoices", "Rate", "Invoice rate cannot be negative.");
                if (line.GSTPercent < 0 || line.GSTPercent > 100) r.Add(ValidationSeverity.Error, "Invoices", "GST", "GST percent must be 0-100.");
            }
            return r;
        }

        public ValidationResult ValidatePurchaseOrder(PurchaseOrder po)
        {
            var r = new ValidationResult();
            if (po == null) return r.Add(ValidationSeverity.Critical, "Purchases", "PO", "Purchase order payload is missing.");
            if (po.VendorID <= 0) r.Add(ValidationSeverity.Error, "Purchases", "VendorID", "PO must be linked to a vendor.");
            if (string.IsNullOrWhiteSpace(po.PONumber)) r.Add(ValidationSeverity.Error, "Purchases", "PONumber", "PO number is required.");
            if (!GlobalValidationEngine.IsReasonableDate(po.PODate)) r.Add(ValidationSeverity.Error, "Purchases", "PODate", "PO date is impossible.");
            if (po.PayByDate != default(DateTime) && po.PayByDate < po.PODate) r.Add(ValidationSeverity.Warning, "Purchases", "PayByDate", "Pay-by date is before PO date.", "Confirm payment terms.", true);
            if (po.TotalAmount < 0) r.Add(ValidationSeverity.Error, "Purchases", "TotalAmount", "PO total cannot be negative.");
            if (po.LineItems == null || po.LineItems.Count == 0) r.Add(ValidationSeverity.Error, "Purchases", "LineItems", "PO needs at least one line item.");
            foreach (PurchaseLineItem line in po.LineItems ?? Enumerable.Empty<PurchaseLineItem>())
            {
                if (string.IsNullOrWhiteSpace(line.Description)) r.Add(ValidationSeverity.Error, "Purchases", "Description", "Every PO line needs a description.");
                if (line.Quantity <= 0) r.Add(ValidationSeverity.Error, "Purchases", "Quantity", "PO line quantity must be greater than zero.");
                if (line.Rate < 0) r.Add(ValidationSeverity.Error, "Purchases", "Rate", "PO line rate cannot be negative.");
                if (line.GSTRate < 0 || line.GSTRate > 100 || line.CGSTRate < 0 || line.CGSTRate > 100 || line.SGSTRate < 0 || line.SGSTRate > 100 || line.IGSTRate < 0 || line.IGSTRate > 100)
                    r.Add(ValidationSeverity.Error, "Purchases", "GST", "PO GST rates must be 0-100%.");
            }
            return r;
        }

        public ValidationResult ValidateQuotation(TenderBid bid)
        {
            var r = new ValidationResult();
            if (bid == null) return r.Add(ValidationSeverity.Critical, "Quotations", "Quotation", "Quotation payload is missing.");
            if (string.IsNullOrWhiteSpace(bid.QuotationNumber)) r.Add(ValidationSeverity.Error, "Quotations", "QuotationNumber", "Quotation number is required.");
            if (bid.ClientID <= 0) r.Add(ValidationSeverity.Error, "Quotations", "ClientID", "Quotation must be linked to a client.");
            if (bid.SiteID <= 0) r.Add(ValidationSeverity.Warning, "Quotations", "SiteID", "Quotation has no site selected.", "Select site before final approval.", true);
            if (!GlobalValidationEngine.IsReasonableDate(bid.DueDate)) r.Add(ValidationSeverity.Error, "Quotations", "DueDate", "Quotation due date is impossible.");
            if (bid.RequiredByDate.HasValue && !GlobalValidationEngine.IsReasonableDate(bid.RequiredByDate.Value)) r.Add(ValidationSeverity.Error, "Quotations", "RequiredByDate", "Required-by date is impossible.");
            if (bid.SubmittedDate.HasValue && !GlobalValidationEngine.IsReasonableDate(bid.SubmittedDate.Value)) r.Add(ValidationSeverity.Error, "Quotations", "SubmittedDate", "Submitted date is impossible.");
            if (bid.BidValue < 0 || bid.RequiredQuantity < 0 || bid.InventoryAvailable < 0 || bid.ShortfallQuantity < 0 || bid.EstimatedInternalRate < 0 || bid.EstimatedSupplierRate < 0 || bid.EstimatedInternalCost < 0 || bid.EstimatedExternalCost < 0)
                r.Add(ValidationSeverity.Error, "Quotations", "Amounts", "Quotation quantity, inventory, rates, and costs cannot be negative.");
            foreach (TenderBidLineItem line in bid.LineItems ?? Enumerable.Empty<TenderBidLineItem>())
            {
                if (string.IsNullOrWhiteSpace(line.ItemDescription)) r.Add(ValidationSeverity.Error, "Quotations", "ItemDescription", "Every quotation line needs a description.");
                if (line.Quantity <= 0) r.Add(ValidationSeverity.Error, "Quotations", "Quantity", "Quotation line quantity must be greater than zero.");
                if (line.SellPricePerUnit < 0 || line.CostPerUnit < 0 || line.StockAvailable < 0 || line.Shortfall < 0) r.Add(ValidationSeverity.Error, "Quotations", "Rate", "Quotation rates and stock values cannot be negative.");
                if (line.GSTRatePct < 0 || line.GSTRatePct > 100) r.Add(ValidationSeverity.Error, "Quotations", "GST", "Quotation GST must be 0-100%.");
            }
            return r;
        }

        public ValidationResult ValidateContract(AMCContract c)
        {
            var r = new ValidationResult();
            if (c == null) return r.Add(ValidationSeverity.Critical, "Contracts", "Contract", "Contract payload is missing.");
            if (c.ClientID <= 0) r.Add(ValidationSeverity.Error, "Contracts", "ClientID", "Contract must be linked to a client.");
            if (c.SiteID <= 0) r.Add(ValidationSeverity.Warning, "Contracts", "SiteID", "Contract has no service site.", "Select the covered site.", true);
            if (!GlobalValidationEngine.IsReasonableDate(c.StartDate)) r.Add(ValidationSeverity.Error, "Contracts", "StartDate", "Contract start date is impossible.");
            if (!GlobalValidationEngine.IsReasonableDate(c.EndDate)) r.Add(ValidationSeverity.Error, "Contracts", "EndDate", "Contract end date is impossible.");
            if (c.EndDate < c.StartDate) r.Add(ValidationSeverity.Error, "Contracts", "EndDate", "Contract end date cannot be before start date.");
            if (c.MonthlyValue < 0 || c.AnnualValue < 0) r.Add(ValidationSeverity.Error, "Contracts", "Value", "Contract value cannot be negative.");
            return r;
        }

        public ValidationResult ValidateJob(Job job)
        {
            var r = new ValidationResult();
            if (job == null) return r.Add(ValidationSeverity.Critical, "Jobs", "Job", "Job payload is missing.");
            if (job.ClientID <= 0) r.Add(ValidationSeverity.Error, "Jobs", "ClientID", "Job must be linked to a client.");
            if (job.SiteID <= 0) r.Add(ValidationSeverity.Error, "Jobs", "SiteID", "Job must be linked to a site.");
            if (string.IsNullOrWhiteSpace(job.JobTitle) && string.IsNullOrWhiteSpace(job.Title)) r.Add(ValidationSeverity.Error, "Jobs", "Title", "Job title is required.");
            if (!GlobalValidationEngine.IsReasonableDate(job.ScheduledDate)) r.Add(ValidationSeverity.Error, "Jobs", "ScheduledDate", "Scheduled date is impossible.");
            if (job.Revenue < 0 || job.QuotedRevenue < 0 || job.EstimatedCost < 0) r.Add(ValidationSeverity.Error, "Jobs", "Amounts", "Job revenue/cost cannot be negative.");
            return r;
        }

        public ValidationResult ValidateAsset(ClientAsset asset)
        {
            var r = new ValidationResult();
            if (asset == null) return r.Add(ValidationSeverity.Critical, "Assets", "Asset", "Asset payload is missing.");
            if (asset.ClientId <= 0) r.Add(ValidationSeverity.Error, "Assets", "ClientId", "Asset must be linked to a client.");
            if (string.IsNullOrWhiteSpace(asset.EquipmentType)) r.Add(ValidationSeverity.Error, "Assets", "EquipmentType", "Equipment type is required.");
            if (asset.WarrantyExpiry.HasValue && asset.InstallDate.HasValue && asset.WarrantyExpiry.Value < asset.InstallDate.Value) r.Add(ValidationSeverity.Warning, "Assets", "WarrantyExpiry", "Warranty expiry is before install date.", "Confirm dates.", true);
            return r;
        }

        public ValidationResult ValidateEmployee(Employee employee)
        {
            var r = new ValidationResult();
            if (employee == null) return r.Add(ValidationSeverity.Critical, "Employees", "Employee", "Employee payload is missing.");
            employee.Name = GlobalValidationEngine.CleanText(employee.Name, 255);
            employee.EmployeeCode = GlobalValidationEngine.CleanText(employee.EmployeeCode, 50);
            if (string.IsNullOrWhiteSpace(employee.Name)) r.Add(ValidationSeverity.Error, "Employees", "Name", "Employee name is required.");
            if (string.IsNullOrWhiteSpace(employee.EmployeeCode)) r.Add(ValidationSeverity.Error, "Employees", "EmployeeCode", "Employee code is required.");
            if (!GlobalValidationEngine.IsValidPhone(employee.Phone)) r.Add(ValidationSeverity.Warning, "Employees", "Phone", "Employee phone format looks invalid.", "Use digits, +, spaces, or dashes.", true);
            string pan = string.IsNullOrWhiteSpace(employee.PANNumber) ? employee.PAN : employee.PANNumber;
            if (!GlobalValidationEngine.IsValidPAN(pan)) r.Add(ValidationSeverity.Error, "Employees", "PAN", "Employee PAN format is invalid.");
            if (employee.JoiningDate.HasValue && !GlobalValidationEngine.IsReasonableDate(employee.JoiningDate.Value)) r.Add(ValidationSeverity.Error, "Employees", "JoiningDate", "Joining date is impossible.");
            if (employee.DateOfJoining.HasValue && !GlobalValidationEngine.IsReasonableDate(employee.DateOfJoining.Value)) r.Add(ValidationSeverity.Error, "Employees", "DateOfJoining", "Date of joining is impossible.");
            if (employee.DateOfBirth.HasValue && (employee.DateOfBirth.Value > DateTime.Today || employee.DateOfBirth.Value < DateTime.Today.AddYears(-100))) r.Add(ValidationSeverity.Error, "Employees", "DateOfBirth", "Date of birth is impossible.");
            if (employee.BasicSalary < 0 || employee.GrossSalary < 0) r.Add(ValidationSeverity.Error, "Employees", "Salary", "Salary values cannot be negative.");
            return r;
        }

        public ValidationResult ValidatePayment(Payment payment)
        {
            var r = new ValidationResult();
            if (payment == null) return r.Add(ValidationSeverity.Critical, "Payments", "Payment", "Payment payload is missing.");
            if (payment.InvoiceID <= 0) r.Add(ValidationSeverity.Error, "Payments", "InvoiceID", "Payment must be linked to an invoice.");
            if (payment.ClientID <= 0) r.Add(ValidationSeverity.Error, "Payments", "ClientID", "Payment must be linked to a client.");
            if (payment.AmountPaid <= 0) r.Add(ValidationSeverity.Error, "Payments", "AmountPaid", "Payment amount must be greater than zero.");
            if (!GlobalValidationEngine.IsReasonableDate(payment.PaymentDate)) r.Add(ValidationSeverity.Error, "Payments", "PaymentDate", "Payment date is impossible.");
            return r;
        }
    }
}
