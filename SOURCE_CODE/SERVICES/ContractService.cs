using System;
using System.Collections.Generic;
using HVAC_Pro_Desktop.DAL;
using HVAC_Pro_Desktop.Models;
using HVAC_Pro_Desktop.Models.Validation;
using HVAC_Pro_Desktop.Services.Audit;
using HVAC_Pro_Desktop.Services.Validation;

namespace HVAC_Pro_Desktop.Services
{
    public class ContractService
    {
        private static readonly TimeSpan CacheTtl = TimeSpan.FromSeconds(45);
        private ContractRepository _contractRepo;
        private ClientRepository _clientRepo;
        private readonly BusinessRuleEngine _businessRules = new BusinessRuleEngine();
        private readonly GlobalValidationEngine _validation = new GlobalValidationEngine();
        private readonly AuditTrailService _audit = new AuditTrailService();

        public ContractService()
        {
            _contractRepo = new ContractRepository();
            _clientRepo = new ClientRepository();
        }

        public decimal GetMonthlyRecurringRevenue()
        {
            decimal total = 0;
            foreach (var contract in GetAllContracts())
            {
                if (string.Equals(contract.ContractStatus, "Active", StringComparison.OrdinalIgnoreCase))
                    total += contract.MonthlyValue;
            }
            return total;
        }

        public int GetActiveContractCount()
        {
            return GetAllContracts().FindAll(c => string.Equals(c.ContractStatus, "Active", StringComparison.OrdinalIgnoreCase)).Count;
        }

        public List<AMCContract> GetExpiringContractsInNextDays(int days)
        {
            DateTime today = DateTime.Today;
            DateTime cutoff = today.AddDays(days);
            return GetAllContracts().FindAll(c => c.EndDate.Date >= today && c.EndDate.Date <= cutoff);
        }

        public int GetExpiringContractCount(int days)
        {
            return GetExpiringContractsInNextDays(days).Count;
        }

        public List<AMCContract> GetAllContracts()
        {
            return AppDataCache.GetOrCreate("contracts:all", CacheTtl, _contractRepo.GetAll);
        }

        public AMCContract GetContractDetails(int contractId)
        {
            foreach (var contract in GetAllContracts())
                if (contract.ContractID == contractId)
                    return contract;
            return _contractRepo.GetById(contractId);
        }

        public List<AMCContract> GetContractsByClient(int clientId)
        {
            return GetAllContracts().FindAll(c => c.ClientID == clientId);
        }

        public int CreateContract(AMCContract contract)
        {
            SessionManager.DemandPermission("Contracts", "Create");
            if (contract == null)
                throw new Exception("Contract details are missing.");
            // Auto-calculate annual value from monthly if not set
            if (contract.AnnualValue == 0 && contract.MonthlyValue > 0)
                contract.AnnualValue = contract.MonthlyValue * 12;
            if (string.IsNullOrEmpty(contract.ContractStatus))
                contract.ContractStatus = "Active";
            ValidateContractForSave(contract);
            if (SessionManager.IsLoggedIn)
            {
                contract.CreatedByUserId = SessionManager.CurrentUser.UserId;
                contract.CreatedByName = SessionManager.CurrentUser.DisplayName;
            }
            int id = _contractRepo.Create(contract);
            AppDataCache.RemovePrefix("contracts:");
            SessionManager.LogAction("CREATE", "Contracts", id, "Contract saved");
            _audit.Record("CREATE", "Contracts", id, "Contract saved with data-quality validation");
            return id;
        }

        public void UpdateContract(AMCContract contract)
        {
            SessionManager.DemandPermission("Contracts", "Edit");
            if (contract == null)
                throw new Exception("Contract details are missing.");
            // Recalculate annual value
            if (contract.MonthlyValue > 0)
                contract.AnnualValue = contract.MonthlyValue * 12;
            ValidateContractForSave(contract);
            if (SessionManager.IsLoggedIn)
            {
                contract.ModifiedByUserId = SessionManager.CurrentUser.UserId;
                contract.ModifiedByName = SessionManager.CurrentUser.DisplayName;
                contract.ModifiedDate = DateTime.Now;
            }

            _contractRepo.Update(contract);
            AppDataCache.RemovePrefix("contracts:");
            SessionManager.LogAction("EDIT", "Contracts", contract.ContractID, "Contract saved");
            _audit.Record("EDIT", "Contracts", contract.ContractID, "Contract saved with data-quality validation");
        }

        public void DeleteContract(int contractId)
        {
            SessionManager.DemandPermission("Contracts", "Delete");
            if (contractId <= 0)
                throw new Exception("Select a saved contract to delete.");

            _contractRepo.Delete(contractId);
            AppDataCache.RemovePrefix("contracts:");
            AppDataCache.RemovePrefix("invoices:");
            AppDataCache.RemovePrefix("jobs:");
            AppDataCache.RemovePrefix("purchases:");
            SessionManager.LogAction("DELETE", "Contracts", contractId, "Contract deleted");
            _audit.Record("DELETE", "Contracts", contractId, "Contract deleted");
        }

        public string GetContractStatusLabel(AMCContract contract)
        {
            int daysUntilExpiry = (contract.EndDate - DateTime.Now).Days;

            if (daysUntilExpiry < 0)
                return "Expired";
            else if (daysUntilExpiry <= 90)
                return "Expiring Soon";
            else
                return "Active";
        }

        private void ValidateContractForSave(AMCContract contract)
        {
            ValidationResult result = _businessRules.ValidateContract(contract);
            _validation.EnsureValid(result, "Contract validation failed");
        }
    }
}
