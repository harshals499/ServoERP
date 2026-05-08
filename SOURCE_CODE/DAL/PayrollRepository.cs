using System;
using System.Collections.Generic;
using System.Data.SqlClient;
using HVAC_Pro_Desktop.Models;

namespace HVAC_Pro_Desktop.DAL
{
    public class PayrollRepository
    {
        private readonly DatabaseManager _db = new DatabaseManager();

        public List<SalaryStructure> GetSalaryStructures(int employeeId)
        {
            var list = new List<SalaryStructure>();
            using (SqlConnection conn = _db.GetConnection())
            {
                conn.Open();
                using (SqlCommand cmd = new SqlCommand(@"
                    SELECT StructureId, EmployeeId, EffectiveFrom, EffectiveTo, BasicSalary, DA, HRA, SpecialAllowance,
                           ConveyanceAllowance, MedicalAllowance, LTA, OtherAllowances, GrossSalary, IsActive, CreatedDate
                    FROM SalaryStructures
                    WHERE EmployeeId = @employeeId
                    ORDER BY EffectiveFrom DESC, StructureId DESC", conn))
                {
                    cmd.Parameters.AddWithValue("@employeeId", employeeId);
                    using (SqlDataReader r = cmd.ExecuteReader())
                        while (r.Read())
                            list.Add(MapSalaryStructure(r));
                }
            }

            return list;
        }

        public SalaryStructure GetActiveSalaryStructure(int employeeId, DateTime asOfDate)
        {
            return GetActiveSalaryStructure(employeeId, asOfDate, null, null);
        }

        public SalaryStructure GetActiveSalaryStructure(int employeeId, DateTime asOfDate, SqlConnection existingConn, SqlTransaction tx)
        {
            bool ownsConnection = existingConn == null;
            SqlConnection conn = existingConn ?? _db.GetConnection();
            try
            {
                if (ownsConnection)
                    conn.Open();
                using (SqlCommand cmd = new SqlCommand(@"
                    SELECT TOP 1 StructureId, EmployeeId, EffectiveFrom, EffectiveTo, BasicSalary, DA, HRA, SpecialAllowance,
                           ConveyanceAllowance, MedicalAllowance, LTA, OtherAllowances, GrossSalary, IsActive, CreatedDate
                    FROM SalaryStructures
                    WHERE EmployeeId = @employeeId
                      AND EffectiveFrom <= @asOfDate
                      AND (EffectiveTo IS NULL OR EffectiveTo >= @asOfDate)
                      AND IsActive = 1
                    ORDER BY EffectiveFrom DESC, StructureId DESC", conn, tx))
                {
                    cmd.Parameters.AddWithValue("@employeeId", employeeId);
                    cmd.Parameters.AddWithValue("@asOfDate", asOfDate.Date);
                    using (SqlDataReader r = cmd.ExecuteReader())
                        return r.Read() ? MapSalaryStructure(r) : null;
                }
            }
            finally
            {
                if (ownsConnection)
                    conn.Dispose();
            }
        }

        public int SaveSalaryStructure(SalaryStructure structure)
        {
            using (SqlConnection conn = _db.GetConnection())
            {
                conn.Open();
                using (SqlTransaction tx = conn.BeginTransaction())
                {
                    if (structure.StructureId <= 0)
                    {
                        using (SqlCommand closePrevious = new SqlCommand(@"
                            UPDATE SalaryStructures
                            SET EffectiveTo = DATEADD(DAY, -1, @effectiveFrom),
                                IsActive = 0
                            WHERE EmployeeId = @employeeId
                              AND IsActive = 1
                              AND StructureId <> @structureId
                              AND EffectiveFrom < @effectiveFrom
                              AND (EffectiveTo IS NULL OR EffectiveTo >= @effectiveFrom)", conn, tx))
                        {
                            closePrevious.Parameters.AddWithValue("@employeeId", structure.EmployeeId);
                            closePrevious.Parameters.AddWithValue("@effectiveFrom", structure.EffectiveFrom.Date);
                            closePrevious.Parameters.AddWithValue("@structureId", structure.StructureId);
                            closePrevious.ExecuteNonQuery();
                        }

                        using (SqlCommand cmd = new SqlCommand(@"
                            INSERT INTO SalaryStructures
                                (EmployeeId, EffectiveFrom, EffectiveTo, BasicSalary, DA, HRA, SpecialAllowance,
                                 ConveyanceAllowance, MedicalAllowance, LTA, OtherAllowances, IsActive)
                            VALUES
                                (@employeeId, @effectiveFrom, @effectiveTo, @basicSalary, @da, @hra, @specialAllowance,
                                 @conveyanceAllowance, @medicalAllowance, @lta, @otherAllowances, @isActive);
                            SELECT CAST(SCOPE_IDENTITY() AS INT);", conn, tx))
                        {
                            AddSalaryStructureParams(cmd, structure);
                            structure.StructureId = Convert.ToInt32(cmd.ExecuteScalar());
                        }
                    }
                    else
                    {
                        using (SqlCommand cmd = new SqlCommand(@"
                            UPDATE SalaryStructures SET
                                EffectiveFrom = @effectiveFrom,
                                EffectiveTo = @effectiveTo,
                                BasicSalary = @basicSalary,
                                DA = @da,
                                HRA = @hra,
                                SpecialAllowance = @specialAllowance,
                                ConveyanceAllowance = @conveyanceAllowance,
                                MedicalAllowance = @medicalAllowance,
                                LTA = @lta,
                                OtherAllowances = @otherAllowances,
                                IsActive = @isActive
                            WHERE StructureId = @structureId", conn, tx))
                        {
                            AddSalaryStructureParams(cmd, structure);
                            cmd.Parameters.AddWithValue("@structureId", structure.StructureId);
                            cmd.ExecuteNonQuery();
                        }
                    }

                    tx.Commit();
                    return structure.StructureId;
                }
            }
        }

        public PayrollRun GetPayrollRun(int month, int year)
        {
            using (SqlConnection conn = _db.GetConnection())
            {
                conn.Open();
                using (SqlCommand cmd = new SqlCommand(@"
                    SELECT TOP 1 PayrollRunId, PayrollMonth, PayrollYear, RunDate, Status, ProcessedBy,
                           TotalGross, TotalNetPay, TotalEPFEmployee, TotalEPFEmployer, TotalESIEmployee,
                           TotalESIEmployer, TotalTDS, TotalPT, Notes
                    FROM PayrollRuns
                    WHERE PayrollMonth = @month AND PayrollYear = @year", conn))
                {
                    cmd.Parameters.AddWithValue("@month", month);
                    cmd.Parameters.AddWithValue("@year", year);
                    using (SqlDataReader r = cmd.ExecuteReader())
                        return r.Read() ? MapPayrollRun(r) : null;
                }
            }
        }

        public PayrollRun GetPayrollRunById(int payrollRunId)
        {
            using (SqlConnection conn = _db.GetConnection())
            {
                conn.Open();
                using (SqlCommand cmd = new SqlCommand(@"
                    SELECT TOP 1 PayrollRunId, PayrollMonth, PayrollYear, RunDate, Status, ProcessedBy,
                           TotalGross, TotalNetPay, TotalEPFEmployee, TotalEPFEmployer, TotalESIEmployee,
                           TotalESIEmployer, TotalTDS, TotalPT, Notes
                    FROM PayrollRuns
                    WHERE PayrollRunId = @payrollRunId", conn))
                {
                    cmd.Parameters.AddWithValue("@payrollRunId", payrollRunId);
                    using (SqlDataReader r = cmd.ExecuteReader())
                        return r.Read() ? MapPayrollRun(r) : null;
                }
            }
        }

        public PayrollRun GetLatestPayrollRun()
        {
            using (SqlConnection conn = _db.GetConnection())
            {
                conn.Open();
                using (SqlCommand cmd = new SqlCommand(@"
                    SELECT TOP 1 PayrollRunId, PayrollMonth, PayrollYear, RunDate, Status, ProcessedBy,
                           TotalGross, TotalNetPay, TotalEPFEmployee, TotalEPFEmployer, TotalESIEmployee,
                           TotalESIEmployer, TotalTDS, TotalPT, Notes
                    FROM PayrollRuns
                    ORDER BY PayrollYear DESC, PayrollMonth DESC, PayrollRunId DESC", conn))
                using (SqlDataReader r = cmd.ExecuteReader())
                    return r.Read() ? MapPayrollRun(r) : null;
            }
        }

        public int CreatePayrollRun(SqlConnection conn, SqlTransaction tx, PayrollRun run)
        {
            using (SqlCommand cmd = new SqlCommand(@"
                INSERT INTO PayrollRuns
                    (PayrollMonth, PayrollYear, Status, ProcessedBy, TotalGross, TotalNetPay,
                     TotalEPFEmployee, TotalEPFEmployer, TotalESIEmployee, TotalESIEmployer, TotalTDS, TotalPT, Notes)
                VALUES
                    (@month, @year, @status, @processedBy, @totalGross, @totalNetPay,
                     @totalEPFEmployee, @totalEPFEmployer, @totalESIEmployee, @totalESIEmployer, @totalTDS, @totalPT, @notes);
                SELECT CAST(SCOPE_IDENTITY() AS INT);", conn, tx))
            {
                AddPayrollRunParams(cmd, run);
                return Convert.ToInt32(cmd.ExecuteScalar());
            }
        }

        public void UpdatePayrollRun(SqlConnection conn, SqlTransaction tx, PayrollRun run)
        {
            using (SqlCommand cmd = new SqlCommand(@"
                UPDATE PayrollRuns SET
                    Status = @status,
                    ProcessedBy = @processedBy,
                    TotalGross = @totalGross,
                    TotalNetPay = @totalNetPay,
                    TotalEPFEmployee = @totalEPFEmployee,
                    TotalEPFEmployer = @totalEPFEmployer,
                    TotalESIEmployee = @totalESIEmployee,
                    TotalESIEmployer = @totalESIEmployer,
                    TotalTDS = @totalTDS,
                    TotalPT = @totalPT,
                    Notes = @notes
                WHERE PayrollRunId = @payrollRunId", conn, tx))
            {
                cmd.Parameters.AddWithValue("@payrollRunId", run.PayrollRunId);
                AddPayrollRunParams(cmd, run);
                cmd.ExecuteNonQuery();
            }
        }

        public void DeletePayrollDataForRun(SqlConnection conn, SqlTransaction tx, int payrollRunId)
        {
            using (SqlCommand cmd = new SqlCommand("DELETE FROM StatutoryPayments WHERE PayrollRunId = @payrollRunId; DELETE FROM PayrollEntries WHERE PayrollRunId = @payrollRunId;", conn, tx))
            {
                cmd.Parameters.AddWithValue("@payrollRunId", payrollRunId);
                cmd.ExecuteNonQuery();
            }
        }

        public void DeleteStatutoryPaymentsForRun(SqlConnection conn, SqlTransaction tx, int payrollRunId)
        {
            using (SqlCommand cmd = new SqlCommand("DELETE FROM StatutoryPayments WHERE PayrollRunId = @payrollRunId", conn, tx))
            {
                cmd.Parameters.AddWithValue("@payrollRunId", payrollRunId);
                cmd.ExecuteNonQuery();
            }
        }

        public int InsertPayrollEntry(SqlConnection conn, SqlTransaction tx, PayrollEntry entry)
        {
            using (SqlCommand cmd = new SqlCommand(@"
                INSERT INTO PayrollEntries
                    (PayrollRunId, EmployeeId, EmployeeName, Designation, BasicSalary, DA, HRA, SpecialAllowance,
                     ConveyanceAllowance, MedicalAllowance, LTA, OtherAllowances, OvertimePay, Bonus, GrossSalary,
                     WorkingDaysInMonth, DaysPresent, DaysAbsent, LeaveDays, OvertimeHours, EPFEmployee, ESIEmployee,
                     TDSDeducted, ProfessionalTax, LoanDeduction, AdvanceDeduction, OtherDeductions, TotalDeductions,
                     EPFEmployer, EPSEmployer, ESIEmployer, NetSalary, TaxRegime, UAN, ESICNumber, BankAccount,
                     BankIFSC, PayslipGenerated, PayslipPath)
                VALUES
                    (@payrollRunId, @employeeId, @employeeName, @designation, @basicSalary, @da, @hra, @specialAllowance,
                     @conveyanceAllowance, @medicalAllowance, @lta, @otherAllowances, @overtimePay, @bonus, @grossSalary,
                     @workingDaysInMonth, @daysPresent, @daysAbsent, @leaveDays, @overtimeHours, @epfEmployee, @esiEmployee,
                     @tdsDeducted, @professionalTax, @loanDeduction, @advanceDeduction, @otherDeductions, @totalDeductions,
                     @epfEmployer, @epsEmployer, @esiEmployer, @netSalary, @taxRegime, @uan, @esicNumber, @bankAccount,
                     @bankIfsc, @payslipGenerated, @payslipPath);
                SELECT CAST(SCOPE_IDENTITY() AS INT);", conn, tx))
            {
                AddPayrollEntryParams(cmd, entry);
                return Convert.ToInt32(cmd.ExecuteScalar());
            }
        }

        public void UpdatePayrollEntry(SqlConnection conn, SqlTransaction tx, PayrollEntry entry)
        {
            using (SqlCommand cmd = new SqlCommand(@"
                UPDATE PayrollEntries SET
                    EmployeeName = @employeeName,
                    Designation = @designation,
                    BasicSalary = @basicSalary,
                    DA = @da,
                    HRA = @hra,
                    SpecialAllowance = @specialAllowance,
                    ConveyanceAllowance = @conveyanceAllowance,
                    MedicalAllowance = @medicalAllowance,
                    LTA = @lta,
                    OtherAllowances = @otherAllowances,
                    OvertimePay = @overtimePay,
                    Bonus = @bonus,
                    GrossSalary = @grossSalary,
                    WorkingDaysInMonth = @workingDaysInMonth,
                    DaysPresent = @daysPresent,
                    DaysAbsent = @daysAbsent,
                    LeaveDays = @leaveDays,
                    OvertimeHours = @overtimeHours,
                    EPFEmployee = @epfEmployee,
                    ESIEmployee = @esiEmployee,
                    TDSDeducted = @tdsDeducted,
                    ProfessionalTax = @professionalTax,
                    LoanDeduction = @loanDeduction,
                    AdvanceDeduction = @advanceDeduction,
                    OtherDeductions = @otherDeductions,
                    TotalDeductions = @totalDeductions,
                    EPFEmployer = @epfEmployer,
                    EPSEmployer = @epsEmployer,
                    ESIEmployer = @esiEmployer,
                    NetSalary = @netSalary,
                    TaxRegime = @taxRegime,
                    UAN = @uan,
                    ESICNumber = @esicNumber,
                    BankAccount = @bankAccount,
                    BankIFSC = @bankIfsc,
                    PayslipGenerated = @payslipGenerated,
                    PayslipPath = @payslipPath
                WHERE EntryId = @entryId", conn, tx))
            {
                cmd.Parameters.AddWithValue("@entryId", entry.EntryId);
                AddPayrollEntryParams(cmd, entry);
                cmd.ExecuteNonQuery();
            }
        }

        public PayrollEntry GetPayrollEntryById(int entryId)
        {
            using (SqlConnection conn = _db.GetConnection())
            {
                conn.Open();
                using (SqlCommand cmd = new SqlCommand(@"
                    SELECT e.EntryId, e.PayrollRunId, e.EmployeeId, e.EmployeeName, e.Designation,
                           e.BasicSalary, e.DA, e.HRA, e.SpecialAllowance, e.ConveyanceAllowance,
                           e.MedicalAllowance, e.LTA, e.OtherAllowances, e.OvertimePay, e.Bonus,
                           e.GrossSalary, e.WorkingDaysInMonth, e.DaysPresent, e.DaysAbsent, e.LeaveDays,
                           e.OvertimeHours, e.EPFEmployee, e.ESIEmployee, e.TDSDeducted, e.ProfessionalTax,
                           e.LoanDeduction, e.AdvanceDeduction, e.OtherDeductions, e.TotalDeductions,
                           e.EPFEmployer, e.EPSEmployer, e.ESIEmployer, e.NetSalary, e.TaxRegime,
                           e.UAN, e.ESICNumber, e.BankAccount, e.BankIFSC, e.PayslipGenerated, e.PayslipPath,
                           r.PayrollMonth, r.PayrollYear
                    FROM PayrollEntries e
                    INNER JOIN PayrollRuns r ON r.PayrollRunId = e.PayrollRunId
                    WHERE e.EntryId = @entryId", conn))
                {
                    cmd.Parameters.AddWithValue("@entryId", entryId);
                    using (SqlDataReader r = cmd.ExecuteReader())
                        return r.Read() ? MapPayrollEntry(r) : null;
                }
            }
        }

        public List<PayrollEntry> GetPayrollEntriesByRun(int payrollRunId)
        {
            var list = new List<PayrollEntry>();
            using (SqlConnection conn = _db.GetConnection())
            {
                conn.Open();
                using (SqlCommand cmd = new SqlCommand(@"
                    SELECT e.EntryId, e.PayrollRunId, e.EmployeeId, e.EmployeeName, e.Designation,
                           e.BasicSalary, e.DA, e.HRA, e.SpecialAllowance, e.ConveyanceAllowance,
                           e.MedicalAllowance, e.LTA, e.OtherAllowances, e.OvertimePay, e.Bonus,
                           e.GrossSalary, e.WorkingDaysInMonth, e.DaysPresent, e.DaysAbsent, e.LeaveDays,
                           e.OvertimeHours, e.EPFEmployee, e.ESIEmployee, e.TDSDeducted, e.ProfessionalTax,
                           e.LoanDeduction, e.AdvanceDeduction, e.OtherDeductions, e.TotalDeductions,
                           e.EPFEmployer, e.EPSEmployer, e.ESIEmployer, e.NetSalary, e.TaxRegime,
                           e.UAN, e.ESICNumber, e.BankAccount, e.BankIFSC, e.PayslipGenerated, e.PayslipPath,
                           r.PayrollMonth, r.PayrollYear
                    FROM PayrollEntries e
                    INNER JOIN PayrollRuns r ON r.PayrollRunId = e.PayrollRunId
                    WHERE e.PayrollRunId = @payrollRunId
                    ORDER BY e.EmployeeName", conn))
                {
                    cmd.Parameters.AddWithValue("@payrollRunId", payrollRunId);
                    using (SqlDataReader r = cmd.ExecuteReader())
                        while (r.Read())
                            list.Add(MapPayrollEntry(r));
                }
            }

            return list;
        }

        public List<PayrollEntry> GetPayrollEntriesByEmployee(int employeeId)
        {
            var list = new List<PayrollEntry>();
            using (SqlConnection conn = _db.GetConnection())
            {
                conn.Open();
                using (SqlCommand cmd = new SqlCommand(@"
                    SELECT e.EntryId, e.PayrollRunId, e.EmployeeId, e.EmployeeName, e.Designation,
                           e.BasicSalary, e.DA, e.HRA, e.SpecialAllowance, e.ConveyanceAllowance,
                           e.MedicalAllowance, e.LTA, e.OtherAllowances, e.OvertimePay, e.Bonus,
                           e.GrossSalary, e.WorkingDaysInMonth, e.DaysPresent, e.DaysAbsent, e.LeaveDays,
                           e.OvertimeHours, e.EPFEmployee, e.ESIEmployee, e.TDSDeducted, e.ProfessionalTax,
                           e.LoanDeduction, e.AdvanceDeduction, e.OtherDeductions, e.TotalDeductions,
                           e.EPFEmployer, e.EPSEmployer, e.ESIEmployer, e.NetSalary, e.TaxRegime,
                           e.UAN, e.ESICNumber, e.BankAccount, e.BankIFSC, e.PayslipGenerated, e.PayslipPath,
                           r.PayrollMonth, r.PayrollYear
                    FROM PayrollEntries e
                    INNER JOIN PayrollRuns r ON r.PayrollRunId = e.PayrollRunId
                    WHERE e.EmployeeId = @employeeId
                    ORDER BY r.PayrollYear DESC, r.PayrollMonth DESC", conn))
                {
                    cmd.Parameters.AddWithValue("@employeeId", employeeId);
                    using (SqlDataReader r = cmd.ExecuteReader())
                        while (r.Read())
                            list.Add(MapPayrollEntry(r));
                }
            }

            return list;
        }

        public void UpdatePayslipStatus(int entryId, string pdfPath)
        {
            using (SqlConnection conn = _db.GetConnection())
            {
                conn.Open();
                using (SqlCommand cmd = new SqlCommand(@"
                    UPDATE PayrollEntries
                    SET PayslipGenerated = 1,
                        PayslipPath = @pdfPath
                    WHERE EntryId = @entryId", conn))
                {
                    cmd.Parameters.AddWithValue("@entryId", entryId);
                    cmd.Parameters.AddWithValue("@pdfPath", string.IsNullOrWhiteSpace(pdfPath) ? (object)DBNull.Value : pdfPath);
                    cmd.ExecuteNonQuery();
                }
            }
        }

        public TDSCalculation GetTdsCalculation(int employeeId, string financialYear)
        {
            return GetTdsCalculation(employeeId, financialYear, null, null);
        }

        public TDSCalculation GetTdsCalculation(int employeeId, string financialYear, SqlConnection existingConn, SqlTransaction tx)
        {
            bool ownsConnection = existingConn == null;
            SqlConnection conn = existingConn ?? _db.GetConnection();
            try
            {
                if (ownsConnection)
                    conn.Open();
                using (SqlCommand cmd = new SqlCommand(@"
                    SELECT TOP 1 TDSCalcId, EmployeeId, FinancialYear, TaxRegime, EstimatedAnnualIncome,
                           Chapter6ADeductions, StandardDeduction, TaxableIncome, AnnualTaxLiability,
                           MonthlyTDS, TDSPaidToDate, LastUpdated
                    FROM TDSCalculations
                    WHERE EmployeeId = @employeeId AND FinancialYear = @financialYear", conn, tx))
                {
                    cmd.Parameters.AddWithValue("@employeeId", employeeId);
                    cmd.Parameters.AddWithValue("@financialYear", financialYear ?? string.Empty);
                    using (SqlDataReader r = cmd.ExecuteReader())
                        return r.Read() ? MapTdsCalculation(r) : null;
                }
            }
            finally
            {
                if (ownsConnection)
                    conn.Dispose();
            }
        }

        public List<TDSCalculation> GetTdsCalculationsByEmployee(int employeeId)
        {
            var list = new List<TDSCalculation>();
            using (SqlConnection conn = _db.GetConnection())
            {
                conn.Open();
                using (SqlCommand cmd = new SqlCommand(@"
                    SELECT TDSCalcId, EmployeeId, FinancialYear, TaxRegime, EstimatedAnnualIncome,
                           Chapter6ADeductions, StandardDeduction, TaxableIncome, AnnualTaxLiability,
                           MonthlyTDS, TDSPaidToDate, LastUpdated
                    FROM TDSCalculations
                    WHERE EmployeeId = @employeeId
                    ORDER BY FinancialYear DESC", conn))
                {
                    cmd.Parameters.AddWithValue("@employeeId", employeeId);
                    using (SqlDataReader r = cmd.ExecuteReader())
                        while (r.Read())
                            list.Add(MapTdsCalculation(r));
                }
            }

            return list;
        }

        public void UpsertTdsCalculation(SqlConnection conn, SqlTransaction tx, TDSCalculation calculation)
        {
            using (SqlCommand cmd = new SqlCommand(@"
                IF EXISTS (SELECT 1 FROM TDSCalculations WHERE EmployeeId = @employeeId AND FinancialYear = @financialYear)
                BEGIN
                    UPDATE TDSCalculations SET
                        TaxRegime = @taxRegime,
                        EstimatedAnnualIncome = @estimatedAnnualIncome,
                        Chapter6ADeductions = @chapter6ADeductions,
                        StandardDeduction = @standardDeduction,
                        TaxableIncome = @taxableIncome,
                        AnnualTaxLiability = @annualTaxLiability,
                        MonthlyTDS = @monthlyTds,
                        TDSPaidToDate = @tdsPaidToDate,
                        LastUpdated = GETDATE()
                    WHERE EmployeeId = @employeeId AND FinancialYear = @financialYear;
                END
                ELSE
                BEGIN
                    INSERT INTO TDSCalculations
                        (EmployeeId, FinancialYear, TaxRegime, EstimatedAnnualIncome, Chapter6ADeductions,
                         StandardDeduction, TaxableIncome, AnnualTaxLiability, MonthlyTDS, TDSPaidToDate)
                    VALUES
                        (@employeeId, @financialYear, @taxRegime, @estimatedAnnualIncome, @chapter6ADeductions,
                         @standardDeduction, @taxableIncome, @annualTaxLiability, @monthlyTds, @tdsPaidToDate);
                END", conn, tx))
            {
                AddTdsParams(cmd, calculation);
                cmd.ExecuteNonQuery();
            }
        }

        public decimal GetGrossPaidInFinancialYear(int employeeId, DateTime fyStart, DateTime beforeMonthStart)
        {
            return GetGrossPaidInFinancialYear(employeeId, fyStart, beforeMonthStart, null, null);
        }

        public decimal GetGrossPaidInFinancialYear(int employeeId, DateTime fyStart, DateTime beforeMonthStart, SqlConnection existingConn, SqlTransaction tx)
        {
            bool ownsConnection = existingConn == null;
            SqlConnection conn = existingConn ?? _db.GetConnection();
            try
            {
                if (ownsConnection)
                    conn.Open();
                using (SqlCommand cmd = new SqlCommand(@"
                    SELECT ISNULL(SUM(e.GrossSalary), 0)
                    FROM PayrollEntries e
                    INNER JOIN PayrollRuns r ON r.PayrollRunId = e.PayrollRunId
                    WHERE e.EmployeeId = @employeeId
                      AND DATEFROMPARTS(r.PayrollYear, r.PayrollMonth, 1) >= @fyStart
                      AND DATEFROMPARTS(r.PayrollYear, r.PayrollMonth, 1) < @beforeMonthStart", conn, tx))
                {
                    cmd.Parameters.AddWithValue("@employeeId", employeeId);
                    cmd.Parameters.AddWithValue("@fyStart", fyStart.Date);
                    cmd.Parameters.AddWithValue("@beforeMonthStart", beforeMonthStart.Date);
                    return Convert.ToDecimal(cmd.ExecuteScalar());
                }
            }
            finally
            {
                if (ownsConnection)
                    conn.Dispose();
            }
        }

        public decimal GetTdsPaidInFinancialYear(int employeeId, DateTime fyStart, DateTime beforeMonthStart)
        {
            return GetTdsPaidInFinancialYear(employeeId, fyStart, beforeMonthStart, null, null);
        }

        public decimal GetTdsPaidInFinancialYear(int employeeId, DateTime fyStart, DateTime beforeMonthStart, SqlConnection existingConn, SqlTransaction tx)
        {
            bool ownsConnection = existingConn == null;
            SqlConnection conn = existingConn ?? _db.GetConnection();
            try
            {
                if (ownsConnection)
                    conn.Open();
                using (SqlCommand cmd = new SqlCommand(@"
                    SELECT ISNULL(SUM(e.TDSDeducted), 0)
                    FROM PayrollEntries e
                    INNER JOIN PayrollRuns r ON r.PayrollRunId = e.PayrollRunId
                    WHERE e.EmployeeId = @employeeId
                      AND DATEFROMPARTS(r.PayrollYear, r.PayrollMonth, 1) >= @fyStart
                      AND DATEFROMPARTS(r.PayrollYear, r.PayrollMonth, 1) < @beforeMonthStart", conn, tx))
                {
                    cmd.Parameters.AddWithValue("@employeeId", employeeId);
                    cmd.Parameters.AddWithValue("@fyStart", fyStart.Date);
                    cmd.Parameters.AddWithValue("@beforeMonthStart", beforeMonthStart.Date);
                    return Convert.ToDecimal(cmd.ExecuteScalar());
                }
            }
            finally
            {
                if (ownsConnection)
                    conn.Dispose();
            }
        }

        public decimal GetProfessionalTaxAmount(string stateCode, decimal grossSalary, DateTime effectiveDate)
        {
            return GetProfessionalTaxAmount(stateCode, grossSalary, effectiveDate, null, null);
        }

        public decimal GetProfessionalTaxAmount(string stateCode, decimal grossSalary, DateTime effectiveDate, SqlConnection existingConn, SqlTransaction tx)
        {
            bool ownsConnection = existingConn == null;
            SqlConnection conn = existingConn ?? _db.GetConnection();
            try
            {
                if (ownsConnection)
                    conn.Open();
                using (SqlCommand cmd = new SqlCommand(@"
                    SELECT TOP 1 MonthlyPT
                    FROM ProfessionalTaxSlabs
                    WHERE StateCode = @stateCode
                      AND @grossSalary >= MinSalary
                      AND (MaxSalary IS NULL OR @grossSalary <= MaxSalary)
                      AND EffectiveFrom <= @effectiveDate
                    ORDER BY EffectiveFrom DESC, MinSalary DESC", conn, tx))
                {
                    cmd.Parameters.AddWithValue("@stateCode", stateCode ?? string.Empty);
                    cmd.Parameters.AddWithValue("@grossSalary", grossSalary);
                    cmd.Parameters.AddWithValue("@effectiveDate", effectiveDate.Date);
                    object value = cmd.ExecuteScalar();
                    return value == null || value == DBNull.Value ? 0m : Convert.ToDecimal(value);
                }
            }
            finally
            {
                if (ownsConnection)
                    conn.Dispose();
            }
        }

        public List<EmployeeLoan> GetActiveLoans(int employeeId)
        {
            return GetActiveLoans(employeeId, null, null);
        }

        public List<EmployeeLoan> GetActiveLoans(int employeeId, SqlConnection existingConn, SqlTransaction tx)
        {
            var list = new List<EmployeeLoan>();
            bool ownsConnection = existingConn == null;
            SqlConnection conn = existingConn ?? _db.GetConnection();
            try
            {
                if (ownsConnection)
                    conn.Open();
                using (SqlCommand cmd = new SqlCommand(@"
                    SELECT LoanId, EmployeeId, LoanAmount, MonthlyDeduction, LoanDate, RemainingBalance, Purpose, IsActive
                    FROM EmployeeLoans
                    WHERE EmployeeId = @employeeId AND IsActive = 1
                    ORDER BY LoanDate DESC", conn, tx))
                {
                    cmd.Parameters.AddWithValue("@employeeId", employeeId);
                    using (SqlDataReader r = cmd.ExecuteReader())
                        while (r.Read())
                            list.Add(MapLoan(r));
                }
            }
            finally
            {
                if (ownsConnection)
                    conn.Dispose();
            }

            return list;
        }

        public List<EmployeeLoan> GetLoansByEmployee(int employeeId)
        {
            var list = new List<EmployeeLoan>();
            using (SqlConnection conn = _db.GetConnection())
            {
                conn.Open();
                using (SqlCommand cmd = new SqlCommand(@"
                    SELECT LoanId, EmployeeId, LoanAmount, MonthlyDeduction, LoanDate, RemainingBalance, Purpose, IsActive
                    FROM EmployeeLoans
                    WHERE EmployeeId = @employeeId
                    ORDER BY LoanDate DESC", conn))
                {
                    cmd.Parameters.AddWithValue("@employeeId", employeeId);
                    using (SqlDataReader r = cmd.ExecuteReader())
                        while (r.Read())
                            list.Add(MapLoan(r));
                }
            }

            return list;
        }

        public List<SalaryAdvance> GetSalaryAdvancesForMonth(int employeeId, int month, int year)
        {
            return GetSalaryAdvancesForMonth(employeeId, month, year, null, null);
        }

        public List<SalaryAdvance> GetSalaryAdvancesForMonth(int employeeId, int month, int year, SqlConnection existingConn, SqlTransaction tx)
        {
            var list = new List<SalaryAdvance>();
            bool ownsConnection = existingConn == null;
            SqlConnection conn = existingConn ?? _db.GetConnection();
            try
            {
                if (ownsConnection)
                    conn.Open();
                using (SqlCommand cmd = new SqlCommand(@"
                    SELECT AdvanceId, EmployeeId, AdvanceAmount, AdvanceDate, RecoveryMonth, RecoveryYear, Recovered
                    FROM SalaryAdvances
                    WHERE EmployeeId = @employeeId
                      AND RecoveryMonth = @month
                      AND RecoveryYear = @year
                      AND Recovered = 0", conn, tx))
                {
                    cmd.Parameters.AddWithValue("@employeeId", employeeId);
                    cmd.Parameters.AddWithValue("@month", month);
                    cmd.Parameters.AddWithValue("@year", year);
                    using (SqlDataReader r = cmd.ExecuteReader())
                        while (r.Read())
                            list.Add(MapAdvance(r));
                }
            }
            finally
            {
                if (ownsConnection)
                    conn.Dispose();
            }

            return list;
        }

        public List<SalaryAdvance> GetAdvancesByEmployee(int employeeId)
        {
            var list = new List<SalaryAdvance>();
            using (SqlConnection conn = _db.GetConnection())
            {
                conn.Open();
                using (SqlCommand cmd = new SqlCommand(@"
                    SELECT AdvanceId, EmployeeId, AdvanceAmount, AdvanceDate, RecoveryMonth, RecoveryYear, Recovered
                    FROM SalaryAdvances
                    WHERE EmployeeId = @employeeId
                    ORDER BY AdvanceDate DESC", conn))
                {
                    cmd.Parameters.AddWithValue("@employeeId", employeeId);
                    using (SqlDataReader r = cmd.ExecuteReader())
                        while (r.Read())
                            list.Add(MapAdvance(r));
                }
            }

            return list;
        }

        public int SaveEmployeeLoan(EmployeeLoan loan)
        {
            using (SqlConnection conn = _db.GetConnection())
            {
                conn.Open();
                using (SqlCommand cmd = new SqlCommand(@"
                    INSERT INTO EmployeeLoans
                        (EmployeeId, LoanAmount, MonthlyDeduction, LoanDate, RemainingBalance, Purpose, IsActive)
                    VALUES
                        (@employeeId, @loanAmount, @monthlyDeduction, @loanDate, @remainingBalance, @purpose, @isActive);
                    SELECT CAST(SCOPE_IDENTITY() AS INT);", conn))
                {
                    cmd.Parameters.AddWithValue("@employeeId", loan.EmployeeId);
                    cmd.Parameters.AddWithValue("@loanAmount", loan.LoanAmount);
                    cmd.Parameters.AddWithValue("@monthlyDeduction", loan.MonthlyDeduction);
                    cmd.Parameters.AddWithValue("@loanDate", loan.LoanDate.Date);
                    cmd.Parameters.AddWithValue("@remainingBalance", loan.RemainingBalance);
                    cmd.Parameters.AddWithValue("@purpose", string.IsNullOrWhiteSpace(loan.Purpose) ? (object)DBNull.Value : loan.Purpose);
                    cmd.Parameters.AddWithValue("@isActive", loan.IsActive);
                    return Convert.ToInt32(cmd.ExecuteScalar());
                }
            }
        }

        public int SaveSalaryAdvance(SalaryAdvance advance)
        {
            using (SqlConnection conn = _db.GetConnection())
            {
                conn.Open();
                using (SqlCommand cmd = new SqlCommand(@"
                    INSERT INTO SalaryAdvances
                        (EmployeeId, AdvanceAmount, AdvanceDate, RecoveryMonth, RecoveryYear, Recovered)
                    VALUES
                        (@employeeId, @advanceAmount, @advanceDate, @recoveryMonth, @recoveryYear, @recovered);
                    SELECT CAST(SCOPE_IDENTITY() AS INT);", conn))
                {
                    cmd.Parameters.AddWithValue("@employeeId", advance.EmployeeId);
                    cmd.Parameters.AddWithValue("@advanceAmount", advance.AdvanceAmount);
                    cmd.Parameters.AddWithValue("@advanceDate", advance.AdvanceDate.Date);
                    cmd.Parameters.AddWithValue("@recoveryMonth", advance.RecoveryMonth);
                    cmd.Parameters.AddWithValue("@recoveryYear", advance.RecoveryYear);
                    cmd.Parameters.AddWithValue("@recovered", advance.Recovered);
                    return Convert.ToInt32(cmd.ExecuteScalar());
                }
            }
        }

        public void ApplyLoanRecovery(SqlConnection conn, SqlTransaction tx, int loanId, decimal deductionAmount)
        {
            using (SqlCommand cmd = new SqlCommand(@"
                UPDATE EmployeeLoans
                SET RemainingBalance = CASE WHEN RemainingBalance - @deductionAmount < 0 THEN 0 ELSE RemainingBalance - @deductionAmount END,
                    IsActive = CASE WHEN RemainingBalance - @deductionAmount <= 0 THEN 0 ELSE 1 END
                WHERE LoanId = @loanId", conn, tx))
            {
                cmd.Parameters.AddWithValue("@loanId", loanId);
                cmd.Parameters.AddWithValue("@deductionAmount", deductionAmount);
                cmd.ExecuteNonQuery();
            }
        }

        public void MarkAdvanceRecovered(SqlConnection conn, SqlTransaction tx, int advanceId)
        {
            using (SqlCommand cmd = new SqlCommand("UPDATE SalaryAdvances SET Recovered = 1 WHERE AdvanceId = @advanceId", conn, tx))
            {
                cmd.Parameters.AddWithValue("@advanceId", advanceId);
                cmd.ExecuteNonQuery();
            }
        }

        public List<StatutoryPayment> GetStatutoryPaymentsByRun(int payrollRunId)
        {
            var list = new List<StatutoryPayment>();
            using (SqlConnection conn = _db.GetConnection())
            {
                conn.Open();
                using (SqlCommand cmd = new SqlCommand(@"
                    SELECT PaymentId, PayrollRunId, PaymentType, Amount, DueDate, PaidDate, ReferenceNumber, Status, Notes
                    FROM StatutoryPayments
                    WHERE PayrollRunId = @payrollRunId
                    ORDER BY DueDate, PaymentType", conn))
                {
                    cmd.Parameters.AddWithValue("@payrollRunId", payrollRunId);
                    using (SqlDataReader r = cmd.ExecuteReader())
                        while (r.Read())
                            list.Add(MapStatutoryPayment(r));
                }
            }

            return list;
        }

        public List<StatutoryPayment> GetStatutoryPaymentsByMonth(int month, int year)
        {
            var list = new List<StatutoryPayment>();
            using (SqlConnection conn = _db.GetConnection())
            {
                conn.Open();
                using (SqlCommand cmd = new SqlCommand(@"
                    SELECT s.PaymentId, s.PayrollRunId, s.PaymentType, s.Amount, s.DueDate, s.PaidDate, s.ReferenceNumber, s.Status, s.Notes
                    FROM StatutoryPayments s
                    INNER JOIN PayrollRuns r ON r.PayrollRunId = s.PayrollRunId
                    WHERE r.PayrollMonth = @month AND r.PayrollYear = @year
                    ORDER BY s.DueDate, s.PaymentType", conn))
                {
                    cmd.Parameters.AddWithValue("@month", month);
                    cmd.Parameters.AddWithValue("@year", year);
                    using (SqlDataReader r = cmd.ExecuteReader())
                        while (r.Read())
                            list.Add(MapStatutoryPayment(r));
                }
            }

            return list;
        }

        public int GetOverdueStatutoryPaymentCount()
        {
            using (SqlConnection conn = _db.GetConnection())
            {
                conn.Open();
                using (SqlCommand cmd = new SqlCommand("SELECT COUNT(*) FROM StatutoryPayments WHERE DueDate < CAST(GETDATE() AS DATE) AND Status <> 'Paid'", conn))
                    return Convert.ToInt32(cmd.ExecuteScalar());
            }
        }

        public void InsertStatutoryPayment(SqlConnection conn, SqlTransaction tx, StatutoryPayment payment)
        {
            using (SqlCommand cmd = new SqlCommand(@"
                INSERT INTO StatutoryPayments
                    (PayrollRunId, PaymentType, Amount, DueDate, PaidDate, ReferenceNumber, Status, Notes)
                VALUES
                    (@payrollRunId, @paymentType, @amount, @dueDate, @paidDate, @referenceNumber, @status, @notes)", conn, tx))
            {
                cmd.Parameters.AddWithValue("@payrollRunId", payment.PayrollRunId);
                cmd.Parameters.AddWithValue("@paymentType", payment.PaymentType ?? string.Empty);
                cmd.Parameters.AddWithValue("@amount", payment.Amount);
                cmd.Parameters.AddWithValue("@dueDate", payment.DueDate.Date);
                cmd.Parameters.AddWithValue("@paidDate", payment.PaidDate.HasValue ? (object)payment.PaidDate.Value.Date : DBNull.Value);
                cmd.Parameters.AddWithValue("@referenceNumber", string.IsNullOrWhiteSpace(payment.ReferenceNumber) ? (object)DBNull.Value : payment.ReferenceNumber);
                cmd.Parameters.AddWithValue("@status", string.IsNullOrWhiteSpace(payment.Status) ? "Pending" : payment.Status);
                cmd.Parameters.AddWithValue("@notes", string.IsNullOrWhiteSpace(payment.Notes) ? (object)DBNull.Value : payment.Notes);
                cmd.ExecuteNonQuery();
            }
        }

        public void MarkStatutoryPaymentPaid(int paymentId, DateTime paidDate, string referenceNumber)
        {
            using (SqlConnection conn = _db.GetConnection())
            {
                conn.Open();
                using (SqlCommand cmd = new SqlCommand(@"
                    UPDATE StatutoryPayments
                    SET PaidDate = @paidDate,
                        ReferenceNumber = @referenceNumber,
                        Status = 'Paid'
                    WHERE PaymentId = @paymentId", conn))
                {
                    cmd.Parameters.AddWithValue("@paymentId", paymentId);
                    cmd.Parameters.AddWithValue("@paidDate", paidDate.Date);
                    cmd.Parameters.AddWithValue("@referenceNumber", string.IsNullOrWhiteSpace(referenceNumber) ? (object)DBNull.Value : referenceNumber);
                    cmd.ExecuteNonQuery();
                }
            }
        }

        public List<AttendanceRecord> GetAttendanceRecordsForMonth(int employeeId, int month, int year)
        {
            return GetAttendanceRecordsForMonth(employeeId, month, year, null, null);
        }

        public List<AttendanceRecord> GetAttendanceRecordsForMonth(int employeeId, int month, int year, SqlConnection existingConn, SqlTransaction tx)
        {
            var list = new List<AttendanceRecord>();
            bool ownsConnection = existingConn == null;
            SqlConnection conn = existingConn ?? _db.GetConnection();
            try
            {
                if (ownsConnection)
                    conn.Open();
                using (SqlCommand cmd = new SqlCommand(@"
                    SELECT AttendanceId, EmployeeId, AttendanceDate, Status, OvertimeHours, Notes
                    FROM AttendanceRecords
                    WHERE EmployeeId = @employeeId
                      AND MONTH(AttendanceDate) = @month
                      AND YEAR(AttendanceDate) = @year
                    ORDER BY AttendanceDate", conn, tx))
                {
                    cmd.Parameters.AddWithValue("@employeeId", employeeId);
                    cmd.Parameters.AddWithValue("@month", month);
                    cmd.Parameters.AddWithValue("@year", year);
                    using (SqlDataReader r = cmd.ExecuteReader())
                        while (r.Read())
                            list.Add(MapAttendance(r));
                }
            }
            finally
            {
                if (ownsConnection)
                    conn.Dispose();
            }

            return list;
        }

        public List<AttendanceRecord> GetAttendanceRecordsForMonth(int month, int year)
        {
            return GetAttendanceRecordsForMonth(month, year, null, null);
        }

        public List<AttendanceRecord> GetAttendanceRecordsForMonth(int month, int year, SqlConnection existingConn, SqlTransaction tx)
        {
            var list = new List<AttendanceRecord>();
            bool ownsConnection = existingConn == null;
            SqlConnection conn = existingConn ?? _db.GetConnection();
            try
            {
                if (ownsConnection)
                    conn.Open();
                using (SqlCommand cmd = new SqlCommand(@"
                    SELECT AttendanceId, EmployeeId, AttendanceDate, Status, OvertimeHours, Notes
                    FROM AttendanceRecords
                    WHERE MONTH(AttendanceDate) = @month
                      AND YEAR(AttendanceDate) = @year
                    ORDER BY EmployeeId, AttendanceDate", conn, tx))
                {
                    cmd.Parameters.AddWithValue("@month", month);
                    cmd.Parameters.AddWithValue("@year", year);
                    using (SqlDataReader r = cmd.ExecuteReader())
                        while (r.Read())
                            list.Add(MapAttendance(r));
                }
            }
            finally
            {
                if (ownsConnection)
                    conn.Dispose();
            }

            return list;
        }

        public AttendanceSummaryDto GetAttendanceSummary(int employeeId, int month, int year, int workWeekMode)
        {
            return GetAttendanceSummary(employeeId, month, year, workWeekMode, null, null);
        }

        public AttendanceSummaryDto GetAttendanceSummary(int employeeId, int month, int year, int workWeekMode, SqlConnection existingConn, SqlTransaction tx)
        {
            List<AttendanceRecord> records = GetAttendanceRecordsForMonth(employeeId, month, year, existingConn, tx);
            var summary = new AttendanceSummaryDto
            {
                WorkingDaysInMonth = CountConfiguredWorkingDays(month, year, workWeekMode)
            };

            if (records.Count == 0)
            {
                summary.DaysPresent = summary.WorkingDaysInMonth;
                return summary;
            }

            foreach (AttendanceRecord record in records)
            {
                string status = (record.Status ?? string.Empty).Trim().ToUpperInvariant();
                if (status == "PRESENT")
                    summary.DaysPresent += 1m;
                else if (status == "HALFDAY")
                {
                    summary.DaysPresent += 0.5m;
                    summary.DaysAbsent += 0.5m;
                }
                else if (status == "LEAVE")
                    summary.LeaveDays += 1m;
                else if (status == "ABSENT")
                    summary.DaysAbsent += 1m;

                summary.OvertimeHours += record.OvertimeHours;
            }

            return summary;
        }

        public void UpsertAttendanceRecord(AttendanceRecord record, SqlConnection existingConn = null, SqlTransaction tx = null)
        {
            bool ownsConnection = existingConn == null;
            SqlConnection conn = existingConn ?? _db.GetConnection();
            if (ownsConnection)
                conn.Open();

            try
            {
                using (SqlCommand cmd = new SqlCommand(@"
                    IF EXISTS (SELECT 1 FROM AttendanceRecords WHERE EmployeeId = @employeeId AND AttendanceDate = @attendanceDate)
                        UPDATE AttendanceRecords
                        SET Status = @status, OvertimeHours = @overtimeHours, Notes = @notes
                        WHERE EmployeeId = @employeeId AND AttendanceDate = @attendanceDate
                    ELSE
                        INSERT INTO AttendanceRecords (EmployeeId, AttendanceDate, Status, OvertimeHours, Notes)
                        VALUES (@employeeId, @attendanceDate, @status, @overtimeHours, @notes)", conn, tx))
                {
                    cmd.Parameters.AddWithValue("@employeeId", record.EmployeeId);
                    cmd.Parameters.AddWithValue("@attendanceDate", record.AttendanceDate.Date);
                    cmd.Parameters.AddWithValue("@status", string.IsNullOrWhiteSpace(record.Status) ? "Present" : record.Status);
                    cmd.Parameters.AddWithValue("@overtimeHours", record.OvertimeHours);
                    cmd.Parameters.AddWithValue("@notes", string.IsNullOrWhiteSpace(record.Notes) ? (object)DBNull.Value : record.Notes);
                    cmd.ExecuteNonQuery();
                }
            }
            finally
            {
                if (ownsConnection)
                    conn.Dispose();
            }
        }

        public List<LeaveBalance> GetLeaveBalances(int employeeId, int year)
        {
            var list = new List<LeaveBalance>();
            using (SqlConnection conn = _db.GetConnection())
            {
                conn.Open();
                using (SqlCommand cmd = new SqlCommand(@"
                    SELECT b.BalanceId, b.EmployeeId, b.LeaveTypeId, b.Year, b.Opening, b.Accrued, b.Used, b.Closing, t.LeaveTypeName
                    FROM LeaveBalances b
                    INNER JOIN LeaveTypes t ON t.LeaveTypeId = b.LeaveTypeId
                    WHERE b.EmployeeId = @employeeId AND b.Year = @year
                    ORDER BY t.LeaveTypeName", conn))
                {
                    cmd.Parameters.AddWithValue("@employeeId", employeeId);
                    cmd.Parameters.AddWithValue("@year", year);
                    using (SqlDataReader r = cmd.ExecuteReader())
                        while (r.Read())
                            list.Add(MapLeaveBalance(r));
                }
            }

            return list;
        }

        public void UpsertLeaveBalance(int employeeId, string leaveTypeName, int year, decimal opening, decimal accrued, decimal used, SqlConnection existingConn = null, SqlTransaction tx = null)
        {
            bool ownsConnection = existingConn == null;
            SqlConnection conn = existingConn ?? _db.GetConnection();
            if (ownsConnection)
                conn.Open();

            try
            {
                int leaveTypeId;
                using (SqlCommand typeCmd = new SqlCommand("SELECT TOP 1 LeaveTypeId FROM LeaveTypes WHERE LeaveTypeName = @name", conn, tx))
                {
                    typeCmd.Parameters.AddWithValue("@name", leaveTypeName ?? string.Empty);
                    object result = typeCmd.ExecuteScalar();
                    if (result == null || result == DBNull.Value)
                        return;
                    leaveTypeId = Convert.ToInt32(result);
                }

                using (SqlCommand cmd = new SqlCommand(@"
                    IF EXISTS (SELECT 1 FROM LeaveBalances WHERE EmployeeId = @employeeId AND LeaveTypeId = @leaveTypeId AND Year = @year)
                        UPDATE LeaveBalances
                        SET Opening = @opening, Accrued = @accrued, Used = @used
                        WHERE EmployeeId = @employeeId AND LeaveTypeId = @leaveTypeId AND Year = @year
                    ELSE
                        INSERT INTO LeaveBalances (EmployeeId, LeaveTypeId, Year, Opening, Accrued, Used)
                        VALUES (@employeeId, @leaveTypeId, @year, @opening, @accrued, @used)", conn, tx))
                {
                    cmd.Parameters.AddWithValue("@employeeId", employeeId);
                    cmd.Parameters.AddWithValue("@leaveTypeId", leaveTypeId);
                    cmd.Parameters.AddWithValue("@year", year);
                    cmd.Parameters.AddWithValue("@opening", opening);
                    cmd.Parameters.AddWithValue("@accrued", accrued);
                    cmd.Parameters.AddWithValue("@used", used);
                    cmd.ExecuteNonQuery();
                }
            }
            finally
            {
                if (ownsConnection)
                    conn.Dispose();
            }
        }

        public int CreateEmployeeFromImport(Employee employee, SqlConnection conn, SqlTransaction tx)
        {
            using (SqlCommand cmd = new SqlCommand(@"
                INSERT INTO Employees
                    (EmployeeCode, Name, Designation, Department, ClientSite, Phone, JoiningDate, DateOfJoining, DateOfBirth,
                     EmploymentType, PAN, AadhaarLast4, UAN, UANNumber, ESICNumber, EPFNumber, TaxRegime, StateCode,
                     EPFApplicable, ESIApplicable, PTApplicable, BankAccountNumber, BankAccount, BankIFSC, IFSCCode, BankName,
                     MaritalStatus, Address, NatureOfWork, BasicSalary, GrossSalary, Status)
                VALUES
                    (@employeeCode, @name, @designation, @department, @clientSite, @phone, @joiningDate, @dateOfJoining, @dateOfBirth,
                     @employmentType, @pan, @aadhaarLast4, @uan, @uanNumber, @esicNumber, @epfNumber, @taxRegime, @stateCode,
                     @epfApplicable, @esiApplicable, @ptApplicable, @bankAccountNumber, @bankAccount, @bankIfsc, @ifscCode, @bankName,
                     @maritalStatus, @address, @natureOfWork, @basicSalary, @grossSalary, @status);
                SELECT CAST(SCOPE_IDENTITY() AS INT);", conn, tx))
            {
                AddEmployeeImportParams(cmd, employee);
                return Convert.ToInt32(cmd.ExecuteScalar());
            }
        }

        public void EnrichEmployeePayrollFields(int employeeId, Employee source, SqlConnection conn, SqlTransaction tx)
        {
            using (SqlCommand cmd = new SqlCommand(@"
                UPDATE Employees SET
                    PAN = CASE WHEN (PAN IS NULL OR PAN = '') AND @pan IS NOT NULL THEN @pan ELSE PAN END,
                    AadhaarLast4 = CASE WHEN (AadhaarLast4 IS NULL OR AadhaarLast4 = '') AND @aadhaarLast4 IS NOT NULL THEN @aadhaarLast4 ELSE AadhaarLast4 END,
                    UAN = CASE WHEN (UAN IS NULL OR UAN = '') AND @uan IS NOT NULL THEN @uan ELSE UAN END,
                    UANNumber = CASE WHEN (UANNumber IS NULL OR UANNumber = '') AND @uanNumber IS NOT NULL THEN @uanNumber ELSE UANNumber END,
                    ESICNumber = CASE WHEN (ESICNumber IS NULL OR ESICNumber = '') AND @esicNumber IS NOT NULL THEN @esicNumber ELSE ESICNumber END,
                    EPFNumber = CASE WHEN (EPFNumber IS NULL OR EPFNumber = '') AND @epfNumber IS NOT NULL THEN @epfNumber ELSE EPFNumber END,
                    DateOfJoining = CASE WHEN DateOfJoining IS NULL AND @dateOfJoining IS NOT NULL THEN @dateOfJoining ELSE DateOfJoining END,
                    DateOfBirth = CASE WHEN DateOfBirth IS NULL AND @dateOfBirth IS NOT NULL THEN @dateOfBirth ELSE DateOfBirth END,
                    EmploymentType = CASE WHEN (EmploymentType IS NULL OR EmploymentType = '') AND @employmentType IS NOT NULL THEN @employmentType ELSE EmploymentType END,
                    TaxRegime = CASE WHEN (TaxRegime IS NULL OR TaxRegime = '') AND @taxRegime IS NOT NULL THEN @taxRegime ELSE TaxRegime END,
                    StateCode = CASE WHEN (StateCode IS NULL OR StateCode = '') AND @stateCode IS NOT NULL THEN @stateCode ELSE StateCode END,
                    BankAccountNumber = CASE WHEN (BankAccountNumber IS NULL OR BankAccountNumber = '') AND @bankAccountNumber IS NOT NULL THEN @bankAccountNumber ELSE BankAccountNumber END,
                    BankAccount = CASE WHEN (BankAccount IS NULL OR BankAccount = '') AND @bankAccount IS NOT NULL THEN @bankAccount ELSE BankAccount END,
                    BankIFSC = CASE WHEN (BankIFSC IS NULL OR BankIFSC = '') AND @bankIfsc IS NOT NULL THEN @bankIfsc ELSE BankIFSC END,
                    IFSCCode = CASE WHEN (IFSCCode IS NULL OR IFSCCode = '') AND @ifscCode IS NOT NULL THEN @ifscCode ELSE IFSCCode END,
                    BankName = CASE WHEN (BankName IS NULL OR BankName = '') AND @bankName IS NOT NULL THEN @bankName ELSE BankName END,
                    MaritalStatus = CASE WHEN (MaritalStatus IS NULL OR MaritalStatus = '') AND @maritalStatus IS NOT NULL THEN @maritalStatus ELSE MaritalStatus END,
                    Address = CASE WHEN (Address IS NULL OR Address = '') AND @address IS NOT NULL THEN @address ELSE Address END,
                    NatureOfWork = CASE WHEN (NatureOfWork IS NULL OR NatureOfWork = '') AND @natureOfWork IS NOT NULL THEN @natureOfWork ELSE NatureOfWork END,
                    EPFApplicable = CASE WHEN @epfApplicable = 1 THEN 1 ELSE EPFApplicable END,
                    ESIApplicable = CASE WHEN @esiApplicable = 1 THEN 1 ELSE ESIApplicable END,
                    PTApplicable = CASE WHEN @ptApplicable = 1 THEN 1 ELSE PTApplicable END
                WHERE EmployeeId = @employeeId", conn, tx))
            {
                cmd.Parameters.AddWithValue("@employeeId", employeeId);
                AddEmployeeImportParams(cmd, source);
                cmd.ExecuteNonQuery();
            }
        }

        private static int CountConfiguredWorkingDays(int month, int year, int workWeekMode)
        {
            DateTime start = new DateTime(year, month, 1);
            DateTime end = start.AddMonths(1).AddDays(-1);
            int workingDays = 0;
            for (DateTime day = start; day <= end; day = day.AddDays(1))
            {
                if (day.DayOfWeek == DayOfWeek.Sunday)
                    continue;
                if (workWeekMode == 5 && day.DayOfWeek == DayOfWeek.Saturday)
                    continue;
                workingDays++;
            }

            return workingDays;
        }

        private static void AddSalaryStructureParams(SqlCommand cmd, SalaryStructure structure)
        {
            cmd.Parameters.AddWithValue("@employeeId", structure.EmployeeId);
            cmd.Parameters.AddWithValue("@effectiveFrom", structure.EffectiveFrom.Date);
            cmd.Parameters.AddWithValue("@effectiveTo", structure.EffectiveTo.HasValue ? (object)structure.EffectiveTo.Value.Date : DBNull.Value);
            cmd.Parameters.AddWithValue("@basicSalary", structure.BasicSalary);
            cmd.Parameters.AddWithValue("@da", structure.DA);
            cmd.Parameters.AddWithValue("@hra", structure.HRA);
            cmd.Parameters.AddWithValue("@specialAllowance", structure.SpecialAllowance);
            cmd.Parameters.AddWithValue("@conveyanceAllowance", structure.ConveyanceAllowance);
            cmd.Parameters.AddWithValue("@medicalAllowance", structure.MedicalAllowance);
            cmd.Parameters.AddWithValue("@lta", structure.LTA);
            cmd.Parameters.AddWithValue("@otherAllowances", structure.OtherAllowances);
            cmd.Parameters.AddWithValue("@isActive", structure.IsActive);
        }

        private static void AddPayrollRunParams(SqlCommand cmd, PayrollRun run)
        {
            cmd.Parameters.AddWithValue("@month", run.PayrollMonth);
            cmd.Parameters.AddWithValue("@year", run.PayrollYear);
            cmd.Parameters.AddWithValue("@status", string.IsNullOrWhiteSpace(run.Status) ? "Draft" : run.Status);
            cmd.Parameters.AddWithValue("@processedBy", string.IsNullOrWhiteSpace(run.ProcessedBy) ? (object)DBNull.Value : run.ProcessedBy);
            cmd.Parameters.AddWithValue("@totalGross", run.TotalGross);
            cmd.Parameters.AddWithValue("@totalNetPay", run.TotalNetPay);
            cmd.Parameters.AddWithValue("@totalEPFEmployee", run.TotalEPFEmployee);
            cmd.Parameters.AddWithValue("@totalEPFEmployer", run.TotalEPFEmployer);
            cmd.Parameters.AddWithValue("@totalESIEmployee", run.TotalESIEmployee);
            cmd.Parameters.AddWithValue("@totalESIEmployer", run.TotalESIEmployer);
            cmd.Parameters.AddWithValue("@totalTDS", run.TotalTDS);
            cmd.Parameters.AddWithValue("@totalPT", run.TotalPT);
            cmd.Parameters.AddWithValue("@notes", string.IsNullOrWhiteSpace(run.Notes) ? (object)DBNull.Value : run.Notes);
        }

        private static void AddPayrollEntryParams(SqlCommand cmd, PayrollEntry entry)
        {
            cmd.Parameters.AddWithValue("@payrollRunId", entry.PayrollRunId);
            cmd.Parameters.AddWithValue("@employeeId", entry.EmployeeId);
            cmd.Parameters.AddWithValue("@employeeName", entry.EmployeeName ?? string.Empty);
            cmd.Parameters.AddWithValue("@designation", string.IsNullOrWhiteSpace(entry.Designation) ? (object)DBNull.Value : entry.Designation);
            cmd.Parameters.AddWithValue("@basicSalary", entry.BasicSalary);
            cmd.Parameters.AddWithValue("@da", entry.DA);
            cmd.Parameters.AddWithValue("@hra", entry.HRA);
            cmd.Parameters.AddWithValue("@specialAllowance", entry.SpecialAllowance);
            cmd.Parameters.AddWithValue("@conveyanceAllowance", entry.ConveyanceAllowance);
            cmd.Parameters.AddWithValue("@medicalAllowance", entry.MedicalAllowance);
            cmd.Parameters.AddWithValue("@lta", entry.LTA);
            cmd.Parameters.AddWithValue("@otherAllowances", entry.OtherAllowances);
            cmd.Parameters.AddWithValue("@overtimePay", entry.OvertimePay);
            cmd.Parameters.AddWithValue("@bonus", entry.Bonus);
            cmd.Parameters.AddWithValue("@grossSalary", entry.GrossSalary);
            cmd.Parameters.AddWithValue("@workingDaysInMonth", entry.WorkingDaysInMonth);
            cmd.Parameters.AddWithValue("@daysPresent", entry.DaysPresent);
            cmd.Parameters.AddWithValue("@daysAbsent", entry.DaysAbsent);
            cmd.Parameters.AddWithValue("@leaveDays", entry.LeaveDays);
            cmd.Parameters.AddWithValue("@overtimeHours", entry.OvertimeHours);
            cmd.Parameters.AddWithValue("@epfEmployee", entry.EPFEmployee);
            cmd.Parameters.AddWithValue("@esiEmployee", entry.ESIEmployee);
            cmd.Parameters.AddWithValue("@tdsDeducted", entry.TDSDeducted);
            cmd.Parameters.AddWithValue("@professionalTax", entry.ProfessionalTax);
            cmd.Parameters.AddWithValue("@loanDeduction", entry.LoanDeduction);
            cmd.Parameters.AddWithValue("@advanceDeduction", entry.AdvanceDeduction);
            cmd.Parameters.AddWithValue("@otherDeductions", entry.OtherDeductions);
            cmd.Parameters.AddWithValue("@totalDeductions", entry.TotalDeductions);
            cmd.Parameters.AddWithValue("@epfEmployer", entry.EPFEmployer);
            cmd.Parameters.AddWithValue("@epsEmployer", entry.EPSEmployer);
            cmd.Parameters.AddWithValue("@esiEmployer", entry.ESIEmployer);
            cmd.Parameters.AddWithValue("@netSalary", entry.NetSalary);
            cmd.Parameters.AddWithValue("@taxRegime", string.IsNullOrWhiteSpace(entry.TaxRegime) ? (object)DBNull.Value : entry.TaxRegime);
            cmd.Parameters.AddWithValue("@uan", string.IsNullOrWhiteSpace(entry.UAN) ? (object)DBNull.Value : entry.UAN);
            cmd.Parameters.AddWithValue("@esicNumber", string.IsNullOrWhiteSpace(entry.ESICNumber) ? (object)DBNull.Value : entry.ESICNumber);
            cmd.Parameters.AddWithValue("@bankAccount", string.IsNullOrWhiteSpace(entry.BankAccount) ? (object)DBNull.Value : entry.BankAccount);
            cmd.Parameters.AddWithValue("@bankIfsc", string.IsNullOrWhiteSpace(entry.BankIFSC) ? (object)DBNull.Value : entry.BankIFSC);
            cmd.Parameters.AddWithValue("@payslipGenerated", entry.PayslipGenerated);
            cmd.Parameters.AddWithValue("@payslipPath", string.IsNullOrWhiteSpace(entry.PayslipPath) ? (object)DBNull.Value : entry.PayslipPath);
        }

        private static void AddTdsParams(SqlCommand cmd, TDSCalculation calculation)
        {
            cmd.Parameters.AddWithValue("@employeeId", calculation.EmployeeId);
            cmd.Parameters.AddWithValue("@financialYear", calculation.FinancialYear ?? string.Empty);
            cmd.Parameters.AddWithValue("@taxRegime", calculation.TaxRegime ?? "New");
            cmd.Parameters.AddWithValue("@estimatedAnnualIncome", calculation.EstimatedAnnualIncome);
            cmd.Parameters.AddWithValue("@chapter6ADeductions", calculation.Chapter6ADeductions);
            cmd.Parameters.AddWithValue("@standardDeduction", calculation.StandardDeduction);
            cmd.Parameters.AddWithValue("@taxableIncome", calculation.TaxableIncome);
            cmd.Parameters.AddWithValue("@annualTaxLiability", calculation.AnnualTaxLiability);
            cmd.Parameters.AddWithValue("@monthlyTds", calculation.MonthlyTDS);
            cmd.Parameters.AddWithValue("@tdsPaidToDate", calculation.TDSPaidToDate);
        }

        private static void AddEmployeeImportParams(SqlCommand cmd, Employee employee)
        {
            cmd.Parameters.AddWithValue("@employeeCode", string.IsNullOrWhiteSpace(employee.EmployeeCode) ? Guid.NewGuid().ToString("N").Substring(0, 8).ToUpperInvariant() : employee.EmployeeCode);
            cmd.Parameters.AddWithValue("@name", employee.Name ?? string.Empty);
            cmd.Parameters.AddWithValue("@designation", string.IsNullOrWhiteSpace(employee.Designation) ? (object)DBNull.Value : employee.Designation);
            cmd.Parameters.AddWithValue("@department", string.IsNullOrWhiteSpace(employee.Department) ? (object)DBNull.Value : employee.Department);
            cmd.Parameters.AddWithValue("@clientSite", string.IsNullOrWhiteSpace(employee.ClientSite) ? (object)DBNull.Value : employee.ClientSite);
            cmd.Parameters.AddWithValue("@phone", string.IsNullOrWhiteSpace(employee.Phone) ? (object)DBNull.Value : employee.Phone);
            cmd.Parameters.AddWithValue("@joiningDate", employee.JoiningDate.HasValue ? (object)employee.JoiningDate.Value.Date : DBNull.Value);
            cmd.Parameters.AddWithValue("@dateOfJoining", employee.DateOfJoining.HasValue ? (object)employee.DateOfJoining.Value.Date : (employee.JoiningDate.HasValue ? (object)employee.JoiningDate.Value.Date : DBNull.Value));
            cmd.Parameters.AddWithValue("@dateOfBirth", employee.DateOfBirth.HasValue ? (object)employee.DateOfBirth.Value.Date : DBNull.Value);
            cmd.Parameters.AddWithValue("@employmentType", string.IsNullOrWhiteSpace(employee.EmploymentType) ? (object)DBNull.Value : employee.EmploymentType);
            cmd.Parameters.AddWithValue("@pan", string.IsNullOrWhiteSpace(employee.PAN) ? (object)DBNull.Value : employee.PAN);
            cmd.Parameters.AddWithValue("@aadhaarLast4", string.IsNullOrWhiteSpace(employee.AadhaarLast4) ? (object)DBNull.Value : employee.AadhaarLast4);
            cmd.Parameters.AddWithValue("@uan", string.IsNullOrWhiteSpace(employee.UAN) ? (object)DBNull.Value : employee.UAN);
            cmd.Parameters.AddWithValue("@uanNumber", string.IsNullOrWhiteSpace(employee.UANNumber) ? (object)DBNull.Value : employee.UANNumber);
            cmd.Parameters.AddWithValue("@esicNumber", string.IsNullOrWhiteSpace(employee.ESICNumber) ? (object)DBNull.Value : employee.ESICNumber);
            cmd.Parameters.AddWithValue("@epfNumber", string.IsNullOrWhiteSpace(employee.EPFNumber) ? (object)DBNull.Value : employee.EPFNumber);
            cmd.Parameters.AddWithValue("@taxRegime", string.IsNullOrWhiteSpace(employee.TaxRegime) ? (object)DBNull.Value : employee.TaxRegime);
            cmd.Parameters.AddWithValue("@stateCode", string.IsNullOrWhiteSpace(employee.StateCode) ? (object)DBNull.Value : employee.StateCode);
            cmd.Parameters.AddWithValue("@epfApplicable", employee.EPFApplicable);
            cmd.Parameters.AddWithValue("@esiApplicable", employee.ESIApplicable);
            cmd.Parameters.AddWithValue("@ptApplicable", employee.PTApplicable);
            cmd.Parameters.AddWithValue("@bankAccountNumber", string.IsNullOrWhiteSpace(employee.BankAccountNumber) ? (object)DBNull.Value : employee.BankAccountNumber);
            cmd.Parameters.AddWithValue("@bankAccount", string.IsNullOrWhiteSpace(employee.BankAccount) ? (object)DBNull.Value : employee.BankAccount);
            cmd.Parameters.AddWithValue("@bankIfsc", string.IsNullOrWhiteSpace(employee.BankIFSC) ? (object)DBNull.Value : employee.BankIFSC);
            cmd.Parameters.AddWithValue("@ifscCode", string.IsNullOrWhiteSpace(employee.IFSCCode) ? (object)DBNull.Value : employee.IFSCCode);
            cmd.Parameters.AddWithValue("@bankName", string.IsNullOrWhiteSpace(employee.BankName) ? (object)DBNull.Value : employee.BankName);
            cmd.Parameters.AddWithValue("@maritalStatus", string.IsNullOrWhiteSpace(employee.MaritalStatus) ? (object)DBNull.Value : employee.MaritalStatus);
            cmd.Parameters.AddWithValue("@address", string.IsNullOrWhiteSpace(employee.Address) ? (object)DBNull.Value : employee.Address);
            cmd.Parameters.AddWithValue("@natureOfWork", string.IsNullOrWhiteSpace(employee.NatureOfWork) ? (object)DBNull.Value : employee.NatureOfWork);
            cmd.Parameters.AddWithValue("@basicSalary", employee.BasicSalary);
            cmd.Parameters.AddWithValue("@grossSalary", employee.GrossSalary);
            cmd.Parameters.AddWithValue("@status", string.IsNullOrWhiteSpace(employee.Status) ? "Active" : employee.Status);
        }

        private static SalaryStructure MapSalaryStructure(SqlDataReader r)
        {
            return new SalaryStructure
            {
                StructureId = Convert.ToInt32(r["StructureId"]),
                EmployeeId = Convert.ToInt32(r["EmployeeId"]),
                EffectiveFrom = Convert.ToDateTime(r["EffectiveFrom"]),
                EffectiveTo = r["EffectiveTo"] == DBNull.Value ? (DateTime?)null : Convert.ToDateTime(r["EffectiveTo"]),
                BasicSalary = Convert.ToDecimal(r["BasicSalary"]),
                DA = Convert.ToDecimal(r["DA"]),
                HRA = Convert.ToDecimal(r["HRA"]),
                SpecialAllowance = Convert.ToDecimal(r["SpecialAllowance"]),
                ConveyanceAllowance = Convert.ToDecimal(r["ConveyanceAllowance"]),
                MedicalAllowance = Convert.ToDecimal(r["MedicalAllowance"]),
                LTA = Convert.ToDecimal(r["LTA"]),
                OtherAllowances = Convert.ToDecimal(r["OtherAllowances"]),
                GrossSalary = Convert.ToDecimal(r["GrossSalary"]),
                IsActive = Convert.ToBoolean(r["IsActive"]),
                CreatedDate = Convert.ToDateTime(r["CreatedDate"])
            };
        }

        private static PayrollRun MapPayrollRun(SqlDataReader r)
        {
            return new PayrollRun
            {
                PayrollRunId = Convert.ToInt32(r["PayrollRunId"]),
                PayrollMonth = Convert.ToInt32(r["PayrollMonth"]),
                PayrollYear = Convert.ToInt32(r["PayrollYear"]),
                RunDate = Convert.ToDateTime(r["RunDate"]),
                Status = Convert.ToString(r["Status"]),
                ProcessedBy = r["ProcessedBy"] as string,
                TotalGross = Convert.ToDecimal(r["TotalGross"]),
                TotalNetPay = Convert.ToDecimal(r["TotalNetPay"]),
                TotalEPFEmployee = Convert.ToDecimal(r["TotalEPFEmployee"]),
                TotalEPFEmployer = Convert.ToDecimal(r["TotalEPFEmployer"]),
                TotalESIEmployee = Convert.ToDecimal(r["TotalESIEmployee"]),
                TotalESIEmployer = Convert.ToDecimal(r["TotalESIEmployer"]),
                TotalTDS = Convert.ToDecimal(r["TotalTDS"]),
                TotalPT = Convert.ToDecimal(r["TotalPT"]),
                Notes = r["Notes"] as string
            };
        }

        private static PayrollEntry MapPayrollEntry(SqlDataReader r)
        {
            return new PayrollEntry
            {
                EntryId = Convert.ToInt32(r["EntryId"]),
                PayrollRunId = Convert.ToInt32(r["PayrollRunId"]),
                EmployeeId = Convert.ToInt32(r["EmployeeId"]),
                EmployeeName = Convert.ToString(r["EmployeeName"]),
                Designation = r["Designation"] as string,
                BasicSalary = Convert.ToDecimal(r["BasicSalary"]),
                DA = Convert.ToDecimal(r["DA"]),
                HRA = Convert.ToDecimal(r["HRA"]),
                SpecialAllowance = Convert.ToDecimal(r["SpecialAllowance"]),
                ConveyanceAllowance = Convert.ToDecimal(r["ConveyanceAllowance"]),
                MedicalAllowance = Convert.ToDecimal(r["MedicalAllowance"]),
                LTA = Convert.ToDecimal(r["LTA"]),
                OtherAllowances = Convert.ToDecimal(r["OtherAllowances"]),
                OvertimePay = Convert.ToDecimal(r["OvertimePay"]),
                Bonus = Convert.ToDecimal(r["Bonus"]),
                GrossSalary = Convert.ToDecimal(r["GrossSalary"]),
                WorkingDaysInMonth = Convert.ToInt32(r["WorkingDaysInMonth"]),
                DaysPresent = Convert.ToDecimal(r["DaysPresent"]),
                DaysAbsent = Convert.ToDecimal(r["DaysAbsent"]),
                LeaveDays = Convert.ToDecimal(r["LeaveDays"]),
                OvertimeHours = Convert.ToDecimal(r["OvertimeHours"]),
                EPFEmployee = Convert.ToDecimal(r["EPFEmployee"]),
                ESIEmployee = Convert.ToDecimal(r["ESIEmployee"]),
                TDSDeducted = Convert.ToDecimal(r["TDSDeducted"]),
                ProfessionalTax = Convert.ToDecimal(r["ProfessionalTax"]),
                LoanDeduction = Convert.ToDecimal(r["LoanDeduction"]),
                AdvanceDeduction = Convert.ToDecimal(r["AdvanceDeduction"]),
                OtherDeductions = Convert.ToDecimal(r["OtherDeductions"]),
                TotalDeductions = Convert.ToDecimal(r["TotalDeductions"]),
                EPFEmployer = Convert.ToDecimal(r["EPFEmployer"]),
                EPSEmployer = Convert.ToDecimal(r["EPSEmployer"]),
                ESIEmployer = Convert.ToDecimal(r["ESIEmployer"]),
                NetSalary = Convert.ToDecimal(r["NetSalary"]),
                TaxRegime = r["TaxRegime"] as string,
                UAN = r["UAN"] as string,
                ESICNumber = r["ESICNumber"] as string,
                BankAccount = r["BankAccount"] as string,
                BankIFSC = r["BankIFSC"] as string,
                PayslipGenerated = r["PayslipGenerated"] != DBNull.Value && Convert.ToBoolean(r["PayslipGenerated"]),
                PayslipPath = r["PayslipPath"] as string,
                PayrollMonth = Convert.ToInt32(r["PayrollMonth"]),
                PayrollYear = Convert.ToInt32(r["PayrollYear"])
            };
        }

        private static TDSCalculation MapTdsCalculation(SqlDataReader r)
        {
            return new TDSCalculation
            {
                TDSCalcId = Convert.ToInt32(r["TDSCalcId"]),
                EmployeeId = Convert.ToInt32(r["EmployeeId"]),
                FinancialYear = Convert.ToString(r["FinancialYear"]),
                TaxRegime = Convert.ToString(r["TaxRegime"]),
                EstimatedAnnualIncome = Convert.ToDecimal(r["EstimatedAnnualIncome"]),
                Chapter6ADeductions = Convert.ToDecimal(r["Chapter6ADeductions"]),
                StandardDeduction = Convert.ToDecimal(r["StandardDeduction"]),
                TaxableIncome = Convert.ToDecimal(r["TaxableIncome"]),
                AnnualTaxLiability = Convert.ToDecimal(r["AnnualTaxLiability"]),
                MonthlyTDS = Convert.ToDecimal(r["MonthlyTDS"]),
                TDSPaidToDate = Convert.ToDecimal(r["TDSPaidToDate"]),
                LastUpdated = Convert.ToDateTime(r["LastUpdated"])
            };
        }

        private static EmployeeLoan MapLoan(SqlDataReader r)
        {
            return new EmployeeLoan
            {
                LoanId = Convert.ToInt32(r["LoanId"]),
                EmployeeId = Convert.ToInt32(r["EmployeeId"]),
                LoanAmount = Convert.ToDecimal(r["LoanAmount"]),
                MonthlyDeduction = Convert.ToDecimal(r["MonthlyDeduction"]),
                LoanDate = Convert.ToDateTime(r["LoanDate"]),
                RemainingBalance = Convert.ToDecimal(r["RemainingBalance"]),
                Purpose = r["Purpose"] as string,
                IsActive = Convert.ToBoolean(r["IsActive"])
            };
        }

        private static SalaryAdvance MapAdvance(SqlDataReader r)
        {
            return new SalaryAdvance
            {
                AdvanceId = Convert.ToInt32(r["AdvanceId"]),
                EmployeeId = Convert.ToInt32(r["EmployeeId"]),
                AdvanceAmount = Convert.ToDecimal(r["AdvanceAmount"]),
                AdvanceDate = Convert.ToDateTime(r["AdvanceDate"]),
                RecoveryMonth = Convert.ToInt32(r["RecoveryMonth"]),
                RecoveryYear = Convert.ToInt32(r["RecoveryYear"]),
                Recovered = Convert.ToBoolean(r["Recovered"])
            };
        }

        private static StatutoryPayment MapStatutoryPayment(SqlDataReader r)
        {
            return new StatutoryPayment
            {
                PaymentId = Convert.ToInt32(r["PaymentId"]),
                PayrollRunId = Convert.ToInt32(r["PayrollRunId"]),
                PaymentType = Convert.ToString(r["PaymentType"]),
                Amount = Convert.ToDecimal(r["Amount"]),
                DueDate = Convert.ToDateTime(r["DueDate"]),
                PaidDate = r["PaidDate"] == DBNull.Value ? (DateTime?)null : Convert.ToDateTime(r["PaidDate"]),
                ReferenceNumber = r["ReferenceNumber"] as string,
                Status = Convert.ToString(r["Status"]),
                Notes = r["Notes"] as string
            };
        }

        private static AttendanceRecord MapAttendance(SqlDataReader r)
        {
            return new AttendanceRecord
            {
                AttendanceId = Convert.ToInt32(r["AttendanceId"]),
                EmployeeId = Convert.ToInt32(r["EmployeeId"]),
                AttendanceDate = Convert.ToDateTime(r["AttendanceDate"]),
                Status = Convert.ToString(r["Status"]),
                OvertimeHours = Convert.ToDecimal(r["OvertimeHours"]),
                Notes = r["Notes"] as string
            };
        }

        private static LeaveBalance MapLeaveBalance(SqlDataReader r)
        {
            return new LeaveBalance
            {
                BalanceId = Convert.ToInt32(r["BalanceId"]),
                EmployeeId = Convert.ToInt32(r["EmployeeId"]),
                LeaveTypeId = Convert.ToInt32(r["LeaveTypeId"]),
                Year = Convert.ToInt32(r["Year"]),
                Opening = Convert.ToDecimal(r["Opening"]),
                Accrued = Convert.ToDecimal(r["Accrued"]),
                Used = Convert.ToDecimal(r["Used"]),
                Closing = Convert.ToDecimal(r["Closing"]),
                LeaveTypeName = Convert.ToString(r["LeaveTypeName"])
            };
        }
    }
}
