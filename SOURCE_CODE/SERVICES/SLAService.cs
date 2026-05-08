using System;
using System.Collections.Generic;
using HVAC_Pro_Desktop.DAL;
using HVAC_Pro_Desktop.Models;

namespace HVAC_Pro_Desktop.Services
{
    public class SLAService
    {
        private SLARepository _slaRepo;

        public SLAService()
        {
            _slaRepo = new SLARepository();
        }

        public void LogSLAEvent(int contractId, string metricType, string target, string actual, bool isCompliant, string notes = "")
        {
            SLALog log = new SLALog
            {
                ContractID = contractId,
                MetricType = metricType,
                Target = target,
                Actual = actual,
                LogDate = DateTime.Now,
                Compliant = isCompliant,
                Notes = notes
            };

            _slaRepo.LogSLAEvent(log);
        }

        public decimal CalculateSLACompliance(int contractId, DateTime month)
        {
            return _slaRepo.CalculateSLACompliance(contractId, month);
        }

        public List<SLALog> GetSLABreaches(int contractId)
        {
            return _slaRepo.GetSLABreaches(contractId);
        }

        public List<SLALog> GetAllLogsForContract(int contractId)
        {
            return _slaRepo.GetAllLogsForContract(contractId);
        }

        public List<SLALog> GetAll()
        {
            return _slaRepo.GetAll();
        }

        public string GetComplianceRating(decimal compliancePercent)
        {
            if (compliancePercent >= 99) return "Excellent";
            if (compliancePercent >= 95) return "Good";
            if (compliancePercent >= 90) return "Fair";
            return "Poor";
        }
    }
}
