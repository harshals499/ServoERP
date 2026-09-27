using System;
using System.Collections.Generic;
using System.Linq;
using HVAC_Pro_Desktop.DAL;
using HVAC_Pro_Desktop.Models;

namespace HVAC_Pro_Desktop.Services
{
    public sealed class FinancialReportingService
    {
        private readonly FinancialReportingRepository _repository;
        private static readonly object SchemaSync = new object();
        private static bool _schemaReady;

        public FinancialReportingService()
        {
            _repository = new FinancialReportingRepository();
        }

        public List<JobProfitabilityRow> GetJobProfitability(DateTime from, DateTime to)
        {
            EnsureSchema();
            List<JobProfitabilityRow> rows = _repository.GetJobProfitability(from, to);
            foreach (JobProfitabilityRow row in rows)
                ApplyCalculations(row);
            return rows;
        }

        public List<MonthlyProfitLossRow> GetMonthlyProfitLoss(DateTime firstMonth, int monthCount)
        {
            EnsureSchema();
            List<MonthlyProfitLossRow> rows = _repository.GetMonthlyProfitLoss(
                new DateTime(firstMonth.Year, firstMonth.Month, 1),
                monthCount);
            foreach (MonthlyProfitLossRow row in rows)
                ApplyCalculations(row);
            return rows;
        }

        public FinancialDashboardSnapshot GetDashboardSnapshot(DateTime month)
        {
            DateTime currentMonth = new DateTime(month.Year, month.Month, 1);
            List<MonthlyProfitLossRow> trend = GetMonthlyProfitLoss(currentMonth.AddMonths(-11), 12);
            MonthlyProfitLossRow current = trend.LastOrDefault(r => r.Month.Year == currentMonth.Year && r.Month.Month == currentMonth.Month)
                ?? new MonthlyProfitLossRow { Month = currentMonth };
            MonthlyProfitLossRow previous = trend.LastOrDefault(r => r.Month.Year == currentMonth.AddMonths(-1).Year && r.Month.Month == currentMonth.AddMonths(-1).Month)
                ?? new MonthlyProfitLossRow { Month = currentMonth.AddMonths(-1) };

            return new FinancialDashboardSnapshot
            {
                Revenue = current.Revenue,
                DirectCosts = current.DirectCosts,
                GrossProfit = current.GrossProfit,
                GrossMarginPercent = current.GrossMarginPercent,
                PayrollExpense = current.PayrollExpense,
                OperatingExpenses = current.OperatingExpenses,
                NetProfit = current.NetProfit,
                NetMarginPercent = current.NetMarginPercent,
                RevenueChangePercent = Change(current.Revenue, previous.Revenue),
                GrossProfitChangePercent = Change(current.GrossProfit, previous.GrossProfit),
                ExpenseChangePercent = Change(current.DirectCosts + current.PayrollExpense + current.OperatingExpenses, previous.DirectCosts + previous.PayrollExpense + previous.OperatingExpenses),
                NetProfitChangePercent = Change(current.NetProfit, previous.NetProfit),
                MonthlyTrend = trend
            };
        }

        public List<ExpenseCategory> GetExpenseCategories()
        {
            EnsureSchema();
            return _repository.GetExpenseCategories();
        }

        public int AddExpense(ExpenseEntry entry)
        {
            EnsureSchema();
            if (entry == null)
                throw new ArgumentNullException(nameof(entry));
            if (entry.ExpenseCategoryId <= 0)
                throw new InvalidOperationException("Select an expense category.");
            if (entry.Amount <= 0m)
                throw new InvalidOperationException("Expense amount must be greater than zero.");
            if (entry.ExpenseDate == DateTime.MinValue)
                throw new InvalidOperationException("Enter a valid expense date.");
            if (string.IsNullOrWhiteSpace(entry.Description))
                throw new InvalidOperationException("Enter a short expense description.");

            entry.Description = entry.Description.Trim();
            entry.ReferenceNumber = string.IsNullOrWhiteSpace(entry.ReferenceNumber) ? null : entry.ReferenceNumber.Trim();
            entry.CreatedByName = SessionManager.CurrentUser == null
                ? Environment.UserName
                : (SessionManager.CurrentUser.DisplayName ?? SessionManager.CurrentUser.Username ?? Environment.UserName);
            int? userId = SessionManager.CurrentUser == null ? (int?)null : SessionManager.CurrentUser.UserId;
            return _repository.AddExpense(entry, userId);
        }

        public int StageImport(ProfitabilityImportPreview preview)
        {
            EnsureSchema();
            if (preview == null || preview.Rows.Count == 0)
                throw new InvalidOperationException("No profitability rows were found in the selected workbook.");
            int? userId = SessionManager.CurrentUser == null ? (int?)null : SessionManager.CurrentUser.UserId;
            string userName = SessionManager.CurrentUser == null
                ? Environment.UserName
                : (SessionManager.CurrentUser.DisplayName ?? SessionManager.CurrentUser.Username ?? Environment.UserName);
            return _repository.StageImport(preview, userId, userName);
        }

        public List<ProfitabilityImportRow> GetLatestImportRows()
        {
            EnsureSchema();
            return _repository.GetLatestImportRows();
        }

        public static void ApplyCalculations(JobProfitabilityRow row)
        {
            if (row == null) throw new ArgumentNullException(nameof(row));
            row.GrossProfit = row.BilledRevenue - row.ActualDirectCost;
            row.GrossMarginPercent = row.BilledRevenue == 0m ? 0m : Math.Round(row.GrossProfit / row.BilledRevenue * 100m, 2);
            row.CostStatus = GetCostStatus(row);
        }

        public static void ApplyCalculations(MonthlyProfitLossRow row)
        {
            if (row == null) throw new ArgumentNullException(nameof(row));
            row.GrossProfit = row.Revenue - row.DirectCosts;
            row.GrossMarginPercent = Percent(row.GrossProfit, row.Revenue);
            row.NetProfit = row.GrossProfit - row.PayrollExpense - row.OperatingExpenses;
            row.NetMarginPercent = Percent(row.NetProfit, row.Revenue);
        }

        private void EnsureSchema()
        {
            if (_schemaReady) return;
            lock (SchemaSync)
            {
                if (_schemaReady) return;
                _repository.EnsureSchema();
                _schemaReady = true;
            }
        }

        private static string GetCostStatus(JobProfitabilityRow row)
        {
            if (row.BilledRevenue <= 0m)
                return row.QuotedRevenue > 0m ? "Not invoiced" : "Revenue missing";
            if (row.ActualDirectCost <= 0m)
                return "Cost incomplete";
            return "Complete";
        }

        private static decimal Percent(decimal numerator, decimal denominator)
        {
            return denominator == 0m ? 0m : Math.Round(numerator / denominator * 100m, 2);
        }

        private static decimal Change(decimal current, decimal previous)
        {
            if (previous == 0m)
                return current == 0m ? 0m : 100m;
            return Math.Round((current - previous) / Math.Abs(previous) * 100m, 1);
        }
    }
}
