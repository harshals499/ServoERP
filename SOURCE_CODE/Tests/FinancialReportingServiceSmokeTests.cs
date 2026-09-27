using System;
using System.Collections.Generic;
using HVAC_Pro_Desktop.Models;
using HVAC_Pro_Desktop.Services;

namespace HVAC_Pro_Desktop.Tests
{
    public static class FinancialReportingServiceSmokeTests
    {
        public static List<string> RunAll()
        {
            var passed = new List<string>();

            var job = new JobProfitabilityRow { BilledRevenue = 100000m, ActualDirectCost = 70000m };
            FinancialReportingService.ApplyCalculations(job);
            if (job.GrossProfit != 30000m || job.GrossMarginPercent != 30m || job.CostStatus != "Complete")
                throw new InvalidOperationException("Job gross profit and margin calculation is incorrect.");
            passed.Add("job profitability uses taxable revenue less actual direct cost");

            var incomplete = new JobProfitabilityRow { BilledRevenue = 50000m, ActualDirectCost = 0m };
            FinancialReportingService.ApplyCalculations(incomplete);
            if (incomplete.CostStatus != "Cost incomplete" || incomplete.GrossProfit != 50000m)
                throw new InvalidOperationException("Missing job costs must remain visibly incomplete.");
            passed.Add("missing costs remain flagged instead of appearing complete");

            var month = new MonthlyProfitLossRow
            {
                Revenue = 250000m,
                DirectCosts = 125000m,
                PayrollExpense = 50000m,
                OperatingExpenses = 25000m
            };
            FinancialReportingService.ApplyCalculations(month);
            if (month.GrossProfit != 125000m || month.NetProfit != 50000m || month.GrossMarginPercent != 50m || month.NetMarginPercent != 20m)
                throw new InvalidOperationException("Monthly P&L calculation is incorrect.");
            passed.Add("company P&L separates gross profit, payroll, operating expense, and net profit");

            return passed;
        }
    }
}
