using System;
using System.Linq;
using HVAC_Pro_Desktop.DAL;
using HVAC_Pro_Desktop.Models;
using HVAC_Pro_Desktop.Models.Validation;

namespace HVAC_Pro_Desktop.Services.Validation
{
    public sealed class RelationshipIntegrityService
    {
        private readonly RelationshipIntegrityRepository _repository = new RelationshipIntegrityRepository();

        public RelationshipHealthSnapshot GetHealthSnapshot() => _repository.GetHealthSnapshot();

        public RelationshipHealthSnapshot ReconcileAndGetHealthSnapshot()
        {
            _repository.EnsureSafeRelationships();
            return _repository.GetHealthSnapshot();
        }

        public static void EnsureValid(ValidationResult result, string context)
        {
            if (result == null || !result.HasErrors)
                return;

            string detail = string.Join(Environment.NewLine, result.Issues
                .Where(issue => issue.Severity == ValidationSeverity.Error || issue.Severity == ValidationSeverity.Critical)
                .Take(8)
                .Select(issue => "- " + issue.Message + (string.IsNullOrWhiteSpace(issue.SuggestedFix) ? string.Empty : " " + issue.SuggestedFix)));
            throw new InvalidOperationException((string.IsNullOrWhiteSpace(context) ? "This record cannot be saved because its relationships are invalid." : context) + Environment.NewLine + detail);
        }

        public ValidationResult CheckClientSite(int clientId, int? siteId, string module)
        {
            RelationshipReferenceContext site = siteId.HasValue && siteId.Value > 0 ? _repository.GetSite(siteId.Value) : null;
            return EvaluateClientSite(clientId, siteId, site == null ? (int?)null : site.ClientId, module);
        }

        public ValidationResult CheckContractContext(int clientId, int? siteId, int? contractId, string module)
        {
            if (!contractId.HasValue || contractId.Value <= 0) return new ValidationResult();
            return EvaluateParentContext(clientId, siteId, _repository.GetContract(contractId.Value), module, "Contract", "contract");
        }

        public ValidationResult CheckInvoiceContext(int clientId, int? siteId, int? invoiceId, string module)
        {
            if (!invoiceId.HasValue || invoiceId.Value <= 0) return new ValidationResult();
            return EvaluateParentContext(clientId, siteId, _repository.GetInvoice(invoiceId.Value), module, "Invoice", "invoice");
        }

        public ValidationResult CheckQuotationContext(int clientId, int? siteId, int? quotationId, string module)
        {
            if (!quotationId.HasValue || quotationId.Value <= 0) return new ValidationResult();
            return EvaluateParentContext(clientId, siteId, _repository.GetQuotation(quotationId.Value), module, "Quotation", "quotation");
        }

        public ValidationResult CheckJobContext(int? clientId, int? siteId, int? jobId, string module)
        {
            if (!jobId.HasValue || jobId.Value <= 0) return new ValidationResult();
            return EvaluateParentContext(clientId.GetValueOrDefault(), siteId, _repository.GetJob(jobId.Value), module, "Job", "work order");
        }

        public ValidationResult CheckEmployee(int? employeeId, string module, string field)
        {
            var result = new ValidationResult();
            if (employeeId.HasValue && employeeId.Value > 0 && !_repository.EmployeeExists(employeeId.Value))
                result.Add(ValidationSeverity.Error, module, field, "Selected technician does not exist.", "Choose a saved active employee.");
            return result;
        }

        public ValidationResult CheckVendor(int? vendorId, string module, string field)
        {
            var result = new ValidationResult();
            if (vendorId.HasValue && vendorId.Value > 0 && !_repository.VendorExists(vendorId.Value))
                result.Add(ValidationSeverity.Error, module, field, "Selected supplier/vendor does not exist.", "Choose a saved supplier/vendor.");
            return result;
        }

        public ValidationResult CheckStockItem(int? itemId, string module, string field)
        {
            var result = new ValidationResult();
            if (itemId.HasValue && itemId.Value > 0 && !_repository.StockItemExists(itemId.Value))
                result.Add(ValidationSeverity.Error, module, field, "Selected inventory item does not exist.", "Choose a saved stock item or clear the stock link.");
            return result;
        }

        public static ValidationResult EvaluateClientSite(int clientId, int? siteId, int? siteOwnerClientId, string module)
        {
            var result = new ValidationResult();
            if (!siteId.HasValue || siteId.Value <= 0) return result;
            if (!siteOwnerClientId.HasValue)
                return result.Add(ValidationSeverity.Error, module, "SiteID", "Selected site does not exist.", "Choose a saved site under the selected client.");
            if (clientId <= 0 || siteOwnerClientId.Value != clientId)
                result.Add(ValidationSeverity.Error, module, "SiteID", "Selected site belongs to a different client.", "Choose a site under the selected client.");
            return result;
        }

        private static ValidationResult EvaluateParentContext(int clientId, int? siteId, RelationshipReferenceContext parent, string module, string field, string label)
        {
            var result = new ValidationResult();
            if (parent == null)
                return result.Add(ValidationSeverity.Error, module, field, "Selected " + label + " does not exist.");
            if (clientId > 0 && parent.ClientId.HasValue && parent.ClientId.Value != clientId)
                result.Add(ValidationSeverity.Error, module, field, "Selected " + label + " belongs to a different client.");
            if (siteId.HasValue && siteId.Value > 0 && parent.SiteId.HasValue && parent.SiteId.Value > 0 && parent.SiteId.Value != siteId.Value)
                result.Add(ValidationSeverity.Error, module, field, "Selected " + label + " belongs to a different site.");
            return result;
        }
    }
}
