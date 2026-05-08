using System;
using System.Collections.Generic;
using System.Data;
using System.Data.OleDb;
using System.Data.SqlClient;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using HVAC_Pro_Desktop.DAL;
using HVAC_Pro_Desktop.Models;

namespace HVAC_Pro_Desktop.Services
{
    public class PayrollDataImportService
    {
        private readonly DatabaseManager _db = new DatabaseManager();
        private readonly PayrollRepository _repo = new PayrollRepository();
        private readonly EmployeeService _employeeService = new EmployeeService();
        private readonly SettingsService _settingsService = new SettingsService();

        public bool IsHistoricalImportCompleted()
        {
            return string.Equals(_settingsService.Get("PayrollHistoricalImportCompleted", "0"), "1", StringComparison.OrdinalIgnoreCase);
        }

        public ServiceResult<PayrollImportReport> ImportFromSourceFolder()
        {
            var report = new PayrollImportReport();
            try
            {
                PayrollFolderHelper.EnsureFolders();
                string[] files = Directory.GetFiles(PayrollFolderHelper.SourcePayrollFolder, "*.*", SearchOption.TopDirectoryOnly);
                List<Employee> currentEmployees = _employeeService.GetAll();
                var employeesByCode = currentEmployees.Where(e => !string.IsNullOrWhiteSpace(e.EmployeeCode))
                    .GroupBy(e => e.EmployeeCode.Trim().ToUpperInvariant()).ToDictionary(g => g.Key, g => g.First());
                var employeesByName = currentEmployees.GroupBy(e => Normalize(e.Name)).ToDictionary(g => g.Key, g => g.ToList());

                using (SqlConnection conn = _db.GetConnection())
                {
                    conn.Open();
                    using (SqlTransaction tx = conn.BeginTransaction())
                    {
                        foreach (string file in files)
                        {
                            report.FilesProcessed++;
                            PayrollImportLogger.Log("Processing file: " + file);
                            if (Path.GetExtension(file).Equals(".xls", StringComparison.OrdinalIgnoreCase) || Path.GetExtension(file).Equals(".xlsx", StringComparison.OrdinalIgnoreCase))
                                ImportWorkbook(file, report, employeesByCode, employeesByName, conn, tx);
                        }

                        BackfillMissingSalaryStructures(report, conn, tx);
                        tx.Commit();
                    }
                }

                _settingsService.Set("PayrollHistoricalImportCompleted", "1");
                PayrollImportLogger.Log("Import complete | Files=" + report.FilesProcessed + " | PayrollEntries=" + report.PayrollEntriesImported + " | Attendance=" + report.AttendanceRecordsImported);
                return ServiceResult<PayrollImportReport>.Ok(report, "Import complete: " + report.PayrollEntriesImported + " payroll entries imported.");
            }
            catch (Exception ex)
            {
                AppRuntime.LogException("PayrollDataImportService.ImportFromSourceFolder", ex);
                PayrollImportLogger.Log("ERROR " + ex);
                report.ErrorsEncountered++;
                return ServiceResult<PayrollImportReport>.Fail(ex.Message);
            }
        }

        private void ImportWorkbook(string file, PayrollImportReport report, Dictionary<string, Employee> employeesByCode, Dictionary<string, List<Employee>> employeesByName, SqlConnection conn, SqlTransaction tx)
        {
            Dictionary<string, DataTable> sheets = LoadWorkbookSheets(file);
            Dictionary<string, ImportedEmployeeRow> importedEmployees = ParseMasterSheets(sheets);

            foreach (ImportedEmployeeRow row in importedEmployees.Values.Where(r => !string.IsNullOrWhiteSpace(r.EmployeeName)))
            {
                Employee employee = MatchOrCreateEmployee(row, report, employeesByCode, employeesByName, conn, tx);
                if (employee != null)
                    EnsureImportedSalaryStructure(employee, row, report, conn, tx);
            }

            foreach (KeyValuePair<string, DataTable> sheet in sheets)
            {
                string sheetName = sheet.Key;
                if (sheetName.IndexOf("PAYROLL", StringComparison.OrdinalIgnoreCase) >= 0 || sheetName.IndexOf("PAY ROLL", StringComparison.OrdinalIgnoreCase) >= 0)
                    ImportPayrollSheet(file, sheet.Value, importedEmployees, report, employeesByCode, employeesByName, conn, tx);
                else if (Regex.IsMatch(sheetName, @"\b(?:JAN|FEB|MAR|APR|MAY|JUN|JUL|AUG|SEP|OCT|NOV|DEC)\b", RegexOptions.IgnoreCase))
                    ImportAttendanceSheet(file, sheet.Value, importedEmployees, report, employeesByCode, employeesByName, conn, tx);
            }
        }

        private Dictionary<string, DataTable> LoadWorkbookSheets(string file)
        {
            var map = new Dictionary<string, DataTable>(StringComparer.OrdinalIgnoreCase);
            string extProps = Path.GetExtension(file).Equals(".xls", StringComparison.OrdinalIgnoreCase) ? "Excel 8.0;HDR=NO;IMEX=1" : "Excel 12.0 Xml;HDR=NO;IMEX=1";
            using (var conn = new OleDbConnection("Provider=Microsoft.ACE.OLEDB.12.0;Data Source=" + file + ";Extended Properties='" + extProps + "';"))
            {
                conn.Open();
                DataTable schema = conn.GetOleDbSchemaTable(OleDbSchemaGuid.Tables, null);
                foreach (DataRow row in schema.Rows)
                {
                    string sheetName = Convert.ToString(row["TABLE_NAME"]);
                    if (string.IsNullOrWhiteSpace(sheetName) || (!sheetName.EndsWith("$") && !sheetName.EndsWith("$'")))
                        continue;
                    var table = new DataTable();
                    using (var adapter = new OleDbDataAdapter("SELECT * FROM [" + sheetName + "]", conn))
                        adapter.Fill(table);
                    map[CleanSheetName(sheetName)] = table;
                }
            }
            return map;
        }

        private Dictionary<string, ImportedEmployeeRow> ParseMasterSheets(Dictionary<string, DataTable> sheets)
        {
            var map = new Dictionary<string, ImportedEmployeeRow>(StringComparer.OrdinalIgnoreCase);
            foreach (KeyValuePair<string, DataTable> pair in sheets)
            {
                if (pair.Key.IndexOf("MASTER", StringComparison.OrdinalIgnoreCase) < 0)
                    continue;
                int headerRow = FindRow(pair.Value, row => RowContains(row, "NAMEOFEMPLOYEE") && (RowContains(row, "IDNO") || RowContains(row, "UANNUMBER")));
                if (headerRow < 0)
                    continue;
                Dictionary<string, int> cols = BuildHeaderIndex(pair.Value, headerRow, 1);
                for (int rowIndex = headerRow + 1; rowIndex < pair.Value.Rows.Count; rowIndex++)
                {
                    string name = ToTitle(Cell(pair.Value.Rows[rowIndex], FindColumn(cols, "NAMEOFEMPLOYEE")));
                    string code = Cell(pair.Value.Rows[rowIndex], FindColumn(cols, "IDNO"));
                    if (string.IsNullOrWhiteSpace(name) && string.IsNullOrWhiteSpace(code))
                        continue;
                    var row = new ImportedEmployeeRow
                    {
                        EmployeeCode = code,
                        EmployeeName = name,
                        DateOfJoining = ParseDate(Cell(pair.Value.Rows[rowIndex], FindColumn(cols, "DOJ"))),
                        DateOfBirth = ParseDate(Cell(pair.Value.Rows[rowIndex], FindColumn(cols, "DOB"))),
                        UAN = CleanUan(Cell(pair.Value.Rows[rowIndex], FindColumn(cols, "UANNUMBER"))),
                        ESICNumber = CleanEsic(Cell(pair.Value.Rows[rowIndex], FindColumn(cols, "ESICNUMBER", "INSURANCENUMBER"))),
                        PAN = CleanPan(Cell(pair.Value.Rows[rowIndex], FindColumn(cols, "PANNUMBER"))),
                        AadhaarLast4 = Last4(Cell(pair.Value.Rows[rowIndex], FindColumn(cols, "AADHARCARDNUMBER"))),
                        BankAccountNumber = CleanBankAccount(Cell(pair.Value.Rows[rowIndex], FindColumn(cols, "BANKACNUMBER")))
                    };
                    if (!string.IsNullOrWhiteSpace(row.EmployeeCode))
                        map[row.EmployeeCode.Trim().ToUpperInvariant()] = row;
                    map[Normalize(row.EmployeeName)] = row;
                }
            }
            return map;
        }

        private void ImportPayrollSheet(string file, DataTable table, Dictionary<string, ImportedEmployeeRow> importedEmployees, PayrollImportReport report, Dictionary<string, Employee> employeesByCode, Dictionary<string, List<Employee>> employeesByName, SqlConnection conn, SqlTransaction tx)
        {
            int month;
            int year;
            ResolveMonthYear(file, out month, out year);
            PayrollRun run = GetOrCreateHistoricalRun(conn, tx, month, year);
            var existing = new HashSet<string>(GetExistingRunEmployeeKeys(conn, tx, run.PayrollRunId), StringComparer.OrdinalIgnoreCase);

            int headerRow = FindRow(table, row => RowContains(row, "FULLNAMEOFTHEEMPLOYEE") && RowContains(row, "TOTALDAYSWORKED"));
            if (headerRow < 0)
                return;

            Dictionary<string, int> cols = BuildHeaderIndex(table, headerRow, 2);
            for (int rowIndex = headerRow + 1; rowIndex < table.Rows.Count; rowIndex++)
            {
                string employeeCode = Cell(table.Rows[rowIndex], FindColumn(cols, "IDNO"));
                string employeeName = ToTitle(Cell(table.Rows[rowIndex], FindColumn(cols, "FULLNAMEOFTHEEMPLOYEE")));
                if (string.IsNullOrWhiteSpace(employeeCode) && string.IsNullOrWhiteSpace(employeeName))
                    continue;

                ImportedEmployeeRow imported = FindImportedEmployee(importedEmployees, employeeCode, employeeName);
                Employee employee = MatchOrCreateEmployee(imported ?? new ImportedEmployeeRow { EmployeeCode = employeeCode, EmployeeName = employeeName }, report, employeesByCode, employeesByName, conn, tx);
                if (employee == null)
                    continue;

                string key = run.PayrollRunId + "|" + employee.EmployeeID;
                if (existing.Contains(key))
                    continue;

                decimal basic = ParseDecimal(Cell(table.Rows[rowIndex], FindColumn(cols, "NORMALWAGESBASICDA", "NORMALWAGESBASICSPLALLOW")));
                decimal hra = ParseDecimal(Cell(table.Rows[rowIndex], FindColumn(cols, "HRAPAYABLE")));
                decimal other = ParseDecimal(Cell(table.Rows[rowIndex], FindColumn(cols, "OTHERALLOW")));
                decimal overtime = ParseDecimal(Cell(table.Rows[rowIndex], FindColumn(cols, "OVERTIMEEARNINIG", "OVERTIMEEARNING")));
                decimal gross = ParseDecimal(Cell(table.Rows[rowIndex], FindColumn(cols, "TOTALWAGESPAYABLE")));
                decimal pf = ParseDecimal(Cell(table.Rows[rowIndex], FindColumn(cols, "DEDUCTIONSPF12", "PF12")));
                decimal esi = ParseDecimal(Cell(table.Rows[rowIndex], FindColumn(cols, "ESIC075", "ESIC0.75")));
                decimal pt = ParseDecimal(Cell(table.Rows[rowIndex], FindColumn(cols, "PTAX")));
                decimal advance = ParseDecimal(Cell(table.Rows[rowIndex], FindColumn(cols, "ADVANCE")));
                decimal totalDeduction = ParseDecimal(Cell(table.Rows[rowIndex], FindColumn(cols, "TOTALDEDUCTION")));
                decimal net = ParseDecimal(Cell(table.Rows[rowIndex], FindColumn(cols, "NETWAGESPAID")));
                decimal daysWorked = ParseDecimal(Cell(table.Rows[rowIndex], FindColumn(cols, "TOTALDAYSWORKED")));

                if (gross <= 0m)
                    gross = basic + hra + other + overtime;
                if (totalDeduction <= 0m)
                    totalDeduction = pf + esi + pt + advance;
                if (net <= 0m)
                    net = gross - totalDeduction;

                _repo.InsertPayrollEntry(conn, tx, new PayrollEntry
                {
                    PayrollRunId = run.PayrollRunId,
                    EmployeeId = employee.EmployeeID,
                    EmployeeName = employee.Name,
                    Designation = employee.Designation,
                    BasicSalary = basic,
                    HRA = hra,
                    OtherAllowances = other,
                    OvertimePay = overtime,
                    GrossSalary = gross,
                    WorkingDaysInMonth = 26,
                    DaysPresent = daysWorked > 0m ? daysWorked : 26m,
                    DaysAbsent = Math.Max(0m, 26m - daysWorked),
                    EPFEmployee = pf,
                    ESIEmployee = esi,
                    ProfessionalTax = pt,
                    AdvanceDeduction = advance,
                    TotalDeductions = totalDeduction,
                    EPSEmployer = pf > 0m ? Math.Min(basic * 0.0833m, 1250m) : 0m,
                    EPFEmployer = pf > 0m ? Math.Max(0m, Math.Min(basic * 0.12m, 1800m) - Math.Min(basic * 0.0833m, 1250m)) : 0m,
                    ESIEmployer = esi > 0m ? Math.Round(gross * 0.0325m, 2, MidpointRounding.AwayFromZero) : 0m,
                    NetSalary = net,
                    TaxRegime = "New",
                    UAN = employee.UAN,
                    ESICNumber = employee.ESICNumber,
                    BankAccount = employee.BankAccountNumber,
                    BankIFSC = employee.BankIFSC
                });

                existing.Add(key);
                report.PayrollEntriesImported++;
                EnsureImportedSalaryStructure(employee, imported ?? new ImportedEmployeeRow { BasicSalary = basic, GrossSalary = gross, HRA = hra, OtherAllowances = other, DateOfJoining = employee.DateOfJoining ?? employee.JoiningDate, EmployeeName = employee.Name }, report, conn, tx);
                PayrollImportLogger.Log("Imported payroll entry for " + employee.Name + " from " + Path.GetFileName(file));
            }
        }

        private void ImportAttendanceSheet(string file, DataTable table, Dictionary<string, ImportedEmployeeRow> importedEmployees, PayrollImportReport report, Dictionary<string, Employee> employeesByCode, Dictionary<string, List<Employee>> employeesByName, SqlConnection conn, SqlTransaction tx)
        {
            int month;
            int year;
            ResolveMonthYear(file, out month, out year);
            int headerRow = FindRow(table, row => RowContains(row, "SRNO") && RowContains(row, "NAME"));
            if (headerRow < 0)
                return;

            Dictionary<int, int> dayCols = FindDayColumns(table, headerRow, 3);
            Dictionary<string, int> headerCols = BuildHeaderIndex(table, headerRow, 0);
            int codeCol = FindColumn(headerCols, "ID");
            int nameCol = FindColumn(headerCols, "NAME");
            var existingAttendance = new HashSet<string>(_repo.GetAttendanceRecordsForMonth(month, year, conn, tx).Select(a => a.EmployeeId + "|" + a.AttendanceDate.ToString("yyyyMMdd", CultureInfo.InvariantCulture)), StringComparer.OrdinalIgnoreCase);

            for (int rowIndex = headerRow + 1; rowIndex < table.Rows.Count; rowIndex++)
            {
                string employeeCode = Cell(table.Rows[rowIndex], codeCol);
                string employeeName = ToTitle(Cell(table.Rows[rowIndex], nameCol));
                if (string.IsNullOrWhiteSpace(employeeCode) && string.IsNullOrWhiteSpace(employeeName))
                    continue;

                ImportedEmployeeRow imported = FindImportedEmployee(importedEmployees, employeeCode, employeeName);
                Employee employee = MatchOrCreateEmployee(imported ?? new ImportedEmployeeRow { EmployeeCode = employeeCode, EmployeeName = employeeName }, report, employeesByCode, employeesByName, conn, tx);
                if (employee == null)
                    continue;

                foreach (KeyValuePair<int, int> dayCol in dayCols)
                {
                    string status = NormalizeStatusCode(Cell(table.Rows[rowIndex], dayCol.Key));
                    if (string.IsNullOrWhiteSpace(status))
                        continue;
                    DateTime attendanceDate = new DateTime(year, month, dayCol.Value);
                    string key = employee.EmployeeID + "|" + attendanceDate.ToString("yyyyMMdd", CultureInfo.InvariantCulture);
                    if (existingAttendance.Contains(key))
                        continue;

                    _repo.UpsertAttendanceRecord(new AttendanceRecord { EmployeeId = employee.EmployeeID, AttendanceDate = attendanceDate, Status = status, OvertimeHours = 0m }, conn, tx);
                    existingAttendance.Add(key);
                    report.AttendanceRecordsImported++;
                }
            }
        }

        private Employee MatchOrCreateEmployee(ImportedEmployeeRow row, PayrollImportReport report, Dictionary<string, Employee> employeesByCode, Dictionary<string, List<Employee>> employeesByName, SqlConnection conn, SqlTransaction tx)
        {
            if (row == null || !IsImportableEmployeeName(row.EmployeeName))
                return null;

            Employee employee;
            if (!string.IsNullOrWhiteSpace(row.EmployeeCode) && employeesByCode.TryGetValue(row.EmployeeCode.Trim().ToUpperInvariant(), out employee))
            {
                _repo.EnrichEmployeePayrollFields(employee.EmployeeID, row.ToEmployee(), conn, tx);
                report.EmployeesMatched++;
                return employee;
            }

            string normalizedName = Normalize(row.EmployeeName);
            if (employeesByName.TryGetValue(normalizedName, out List<Employee> matches))
            {
                if (matches.Count == 1)
                {
                    employee = matches[0];
                    _repo.EnrichEmployeePayrollFields(employee.EmployeeID, row.ToEmployee(), conn, tx);
                    report.EmployeesMatched++;
                    return employee;
                }

                report.Warnings.Add("Ambiguous employee match skipped: " + row.EmployeeName);
                report.ErrorsEncountered++;
                PayrollImportLogger.Log("WARNING Ambiguous employee match: " + row.EmployeeName);
                return null;
            }

            employee = row.ToEmployee();
            employee.Status = "Active";
            employee.EmployeeID = _repo.CreateEmployeeFromImport(employee, conn, tx);
            if (!string.IsNullOrWhiteSpace(employee.EmployeeCode))
                employeesByCode[employee.EmployeeCode.Trim().ToUpperInvariant()] = employee;
            employeesByName[normalizedName] = new List<Employee> { employee };
            report.NewEmployeesCreated++;
            PayrollImportLogger.Log("New employee created from import: " + employee.Name);
            return employee;
        }

        private void EnsureImportedSalaryStructure(Employee employee, ImportedEmployeeRow row, PayrollImportReport report, SqlConnection conn, SqlTransaction tx)
        {
            decimal basic = row?.BasicSalary ?? 0m;
            decimal gross = row?.GrossSalary ?? 0m;
            if (basic <= 0m && gross <= 0m)
                return;

            DateTime effectiveFrom = (row?.DateOfJoining ?? employee.DateOfJoining ?? employee.JoiningDate ?? DateTime.Today).Date;
            using (SqlCommand check = new SqlCommand("SELECT TOP 1 StructureId FROM SalaryStructures WHERE EmployeeId = @employeeId AND EffectiveFrom = @effectiveFrom", conn, tx))
            {
                check.Parameters.AddWithValue("@employeeId", employee.EmployeeID);
                check.Parameters.AddWithValue("@effectiveFrom", effectiveFrom);
                if (check.ExecuteScalar() != null)
                    return;
            }

            decimal hra = row?.HRA ?? 0m;
            decimal other = row?.OtherAllowances ?? 0m;
            if (gross > 0m && hra <= 0m)
                hra = Math.Round(gross * 0.2m, 2, MidpointRounding.AwayFromZero);
            if (gross > 0m && other <= 0m)
                other = Math.Max(0m, gross - basic - hra);

            using (SqlCommand insert = new SqlCommand(@"
                INSERT INTO SalaryStructures
                    (EmployeeId, EffectiveFrom, BasicSalary, DA, HRA, SpecialAllowance, ConveyanceAllowance, MedicalAllowance, LTA, OtherAllowances, IsActive)
                VALUES
                    (@employeeId, @effectiveFrom, @basicSalary, 0, @hra, 0, 0, 0, 0, @otherAllowances, 1)", conn, tx))
            {
                insert.Parameters.AddWithValue("@employeeId", employee.EmployeeID);
                insert.Parameters.AddWithValue("@effectiveFrom", effectiveFrom);
                insert.Parameters.AddWithValue("@basicSalary", basic);
                insert.Parameters.AddWithValue("@hra", hra);
                insert.Parameters.AddWithValue("@otherAllowances", other);
                insert.ExecuteNonQuery();
                report.SalaryStructuresImported++;
            }
        }

        private PayrollRun GetOrCreateHistoricalRun(SqlConnection conn, SqlTransaction tx, int month, int year)
        {
            using (SqlCommand cmd = new SqlCommand("SELECT TOP 1 PayrollRunId FROM PayrollRuns WHERE PayrollMonth = @month AND PayrollYear = @year", conn, tx))
            {
                cmd.Parameters.AddWithValue("@month", month);
                cmd.Parameters.AddWithValue("@year", year);
                object existing = cmd.ExecuteScalar();
                if (existing != null && existing != DBNull.Value)
                    return new PayrollRun { PayrollRunId = Convert.ToInt32(existing), PayrollMonth = month, PayrollYear = year, Status = "Locked" };
            }

            var run = new PayrollRun { PayrollMonth = month, PayrollYear = year, Status = "Locked", Notes = "Historical import" };
            run.PayrollRunId = _repo.CreatePayrollRun(conn, tx, run);
            _repo.UpdatePayrollRun(conn, tx, run);
            return run;
        }

        private static void BackfillMissingSalaryStructures(PayrollImportReport report, SqlConnection conn, SqlTransaction tx)
        {
            using (SqlCommand cmd = new SqlCommand(@"
                ;WITH LatestPayroll AS (
                    SELECT p.EmployeeId,
                           p.BasicSalary,
                           p.DA,
                           p.HRA,
                           p.SpecialAllowance,
                           p.ConveyanceAllowance,
                           p.MedicalAllowance,
                           p.LTA,
                           p.OtherAllowances,
                           p.GrossSalary,
                           r.PayrollYear,
                           r.PayrollMonth,
                           ROW_NUMBER() OVER (PARTITION BY p.EmployeeId ORDER BY r.PayrollYear DESC, r.PayrollMonth DESC, p.EntryId DESC) AS rn
                    FROM PayrollEntries p
                    INNER JOIN PayrollRuns r ON r.PayrollRunId = p.PayrollRunId
                )
                INSERT INTO SalaryStructures
                    (EmployeeId, EffectiveFrom, BasicSalary, DA, HRA, SpecialAllowance, ConveyanceAllowance, MedicalAllowance, LTA, OtherAllowances, IsActive)
                SELECT lp.EmployeeId,
                       ISNULL(e.DateOfJoining, DATEFROMPARTS(lp.PayrollYear, lp.PayrollMonth, 1)),
                       CASE
                           WHEN ISNULL(lp.BasicSalary, 0) + ISNULL(lp.DA, 0) > 0 THEN lp.BasicSalary
                           ELSE ROUND(ISNULL(lp.GrossSalary, 0) * 0.5, 2)
                       END,
                       CASE
                           WHEN ISNULL(lp.BasicSalary, 0) + ISNULL(lp.DA, 0) > 0 THEN ISNULL(lp.DA, 0)
                           ELSE 0
                       END,
                       CASE
                           WHEN ISNULL(lp.HRA, 0) > 0 THEN lp.HRA
                           ELSE ROUND(ISNULL(lp.GrossSalary, 0) * 0.2, 2)
                       END,
                       ISNULL(lp.SpecialAllowance, 0),
                       ISNULL(lp.ConveyanceAllowance, 0),
                       ISNULL(lp.MedicalAllowance, 0),
                       ISNULL(lp.LTA, 0),
                       CASE
                           WHEN ISNULL(lp.OtherAllowances, 0) > 0 THEN lp.OtherAllowances
                           ELSE
                               CASE
                                   WHEN ISNULL(lp.GrossSalary, 0)
                                        - (
                                            CASE
                                                WHEN ISNULL(lp.BasicSalary, 0) + ISNULL(lp.DA, 0) > 0 THEN ISNULL(lp.BasicSalary, 0) + ISNULL(lp.DA, 0)
                                                ELSE ROUND(ISNULL(lp.GrossSalary, 0) * 0.5, 2)
                                            END
                                            + CASE WHEN ISNULL(lp.HRA, 0) > 0 THEN ISNULL(lp.HRA, 0) ELSE ROUND(ISNULL(lp.GrossSalary, 0) * 0.2, 2) END
                                            + ISNULL(lp.SpecialAllowance, 0)
                                            + ISNULL(lp.ConveyanceAllowance, 0)
                                            + ISNULL(lp.MedicalAllowance, 0)
                                            + ISNULL(lp.LTA, 0)
                                          ) > 0
                                       THEN ISNULL(lp.GrossSalary, 0)
                                            - (
                                                CASE
                                                    WHEN ISNULL(lp.BasicSalary, 0) + ISNULL(lp.DA, 0) > 0 THEN ISNULL(lp.BasicSalary, 0) + ISNULL(lp.DA, 0)
                                                    ELSE ROUND(ISNULL(lp.GrossSalary, 0) * 0.5, 2)
                                                END
                                                + CASE WHEN ISNULL(lp.HRA, 0) > 0 THEN ISNULL(lp.HRA, 0) ELSE ROUND(ISNULL(lp.GrossSalary, 0) * 0.2, 2) END
                                                + ISNULL(lp.SpecialAllowance, 0)
                                                + ISNULL(lp.ConveyanceAllowance, 0)
                                                + ISNULL(lp.MedicalAllowance, 0)
                                                + ISNULL(lp.LTA, 0)
                                              )
                                   ELSE 0
                               END
                       END,
                       1
                FROM LatestPayroll lp
                INNER JOIN Employees e ON e.EmployeeID = lp.EmployeeId
                WHERE lp.rn = 1
                  AND ISNULL(lp.GrossSalary, 0) > 0
                  AND NOT EXISTS (SELECT 1 FROM SalaryStructures s WHERE s.EmployeeId = lp.EmployeeId);
                SELECT @@ROWCOUNT;", conn, tx))
            {
                object inserted = cmd.ExecuteScalar();
                if (inserted != null && inserted != DBNull.Value)
                    report.SalaryStructuresImported += Convert.ToInt32(inserted);
            }
        }

        private static List<string> GetExistingRunEmployeeKeys(SqlConnection conn, SqlTransaction tx, int payrollRunId)
        {
            var keys = new List<string>();
            using (SqlCommand cmd = new SqlCommand("SELECT EmployeeId FROM PayrollEntries WHERE PayrollRunId = @payrollRunId", conn, tx))
            {
                cmd.Parameters.AddWithValue("@payrollRunId", payrollRunId);
                using (SqlDataReader reader = cmd.ExecuteReader())
                    while (reader.Read())
                        keys.Add(payrollRunId + "|" + Convert.ToInt32(reader["EmployeeId"]));
            }
            return keys;
        }

        private static Dictionary<string, int> BuildHeaderIndex(DataTable table, int headerRow, int extraRows)
        {
            var headers = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
            for (int column = 0; column < table.Columns.Count; column++)
            {
                string combined = string.Empty;
                for (int offset = 0; offset <= extraRows && headerRow + offset < table.Rows.Count; offset++)
                    combined += " " + Cell(table.Rows[headerRow + offset], column);
                string normalized = Normalize(combined);
                if (!string.IsNullOrWhiteSpace(normalized) && !headers.ContainsKey(normalized))
                    headers[normalized] = column;
            }
            return headers;
        }

        private static int FindColumn(Dictionary<string, int> headers, params string[] tokens)
        {
            foreach (KeyValuePair<string, int> pair in headers)
            {
                bool matched = true;
                foreach (string token in tokens)
                {
                    if (!pair.Key.Contains(Normalize(token)))
                    {
                        matched = false;
                        break;
                    }
                }
                if (matched)
                    return pair.Value;
            }
            return -1;
        }

        private static Dictionary<int, int> FindDayColumns(DataTable table, int headerRow, int extraRows)
        {
            var map = new Dictionary<int, int>();
            for (int column = 0; column < table.Columns.Count; column++)
            {
                for (int offset = 0; offset <= extraRows && headerRow + offset < table.Rows.Count; offset++)
                {
                    if (int.TryParse(Cell(table.Rows[headerRow + offset], column), out int day) && day >= 1 && day <= 31)
                    {
                        map[column] = day;
                        break;
                    }
                }
            }
            return map;
        }

        private static int FindRow(DataTable table, Func<DataRow, bool> predicate)
        {
            for (int i = 0; i < table.Rows.Count; i++)
                if (predicate(table.Rows[i]))
                    return i;
            return -1;
        }

        private static bool RowContains(DataRow row, string token)
        {
            string normalizedToken = Normalize(token);
            return row.ItemArray.Any(value => Normalize(Convert.ToString(value)).Contains(normalizedToken));
        }

        private static string Cell(DataRow row, int index)
        {
            if (row == null || index < 0 || index >= row.Table.Columns.Count)
                return string.Empty;
            return Convert.ToString(row[index])?.Trim() ?? string.Empty;
        }

        private static void ResolveMonthYear(string fileName, out int month, out int year)
        {
            month = 3;
            year = 2026;
            Match match = Regex.Match(Path.GetFileNameWithoutExtension(fileName), @"(Jan|Feb|Mar|Apr|May|Jun|Jul|Aug|Sep|Oct|Nov|Dec)[^\d]*(\d{2,4})", RegexOptions.IgnoreCase);
            if (match.Success)
            {
                month = DateTime.ParseExact(match.Groups[1].Value, "MMM", CultureInfo.InvariantCulture).Month;
                year = int.Parse(match.Groups[2].Value, CultureInfo.InvariantCulture);
                if (year < 100) year += 2000;
            }
        }

        private static string CleanSheetName(string rawName) => rawName.Trim('\'').TrimEnd('$').Trim();
        private static string Normalize(string value) => string.IsNullOrWhiteSpace(value) ? string.Empty : new string(value.Trim().ToUpperInvariant().Where(char.IsLetterOrDigit).ToArray());
        private static string ToTitle(string value) => string.IsNullOrWhiteSpace(value) ? string.Empty : CultureInfo.InvariantCulture.TextInfo.ToTitleCase(value.Trim().ToLowerInvariant());
        private static DateTime? ParseDate(string value) { DateTime parsed; return DateTime.TryParse(value, out parsed) ? (DateTime?)parsed.Date : null; }
        private static decimal ParseDecimal(string value) { decimal parsed; string cleaned = (value ?? string.Empty).Replace(",", string.Empty).Replace("₹", string.Empty).Trim(); return decimal.TryParse(cleaned, NumberStyles.Any, CultureInfo.InvariantCulture, out parsed) ? parsed : 0m; }
        private static string CleanNotApplicable(string value) => string.IsNullOrWhiteSpace(value) || value.Trim().Equals("NOT APPLICABLE", StringComparison.OrdinalIgnoreCase) ? null : value.Trim();
        private static string CleanUan(string value)
        {
            string digits = new string((CleanNotApplicable(value) ?? string.Empty).Where(char.IsDigit).ToArray());
            return digits.Length == 12 ? digits : null;
        }
        private static string CleanEsic(string value)
        {
            string digits = new string((CleanNotApplicable(value) ?? string.Empty).Where(char.IsDigit).ToArray());
            if (string.IsNullOrWhiteSpace(digits))
                return null;
            return digits.Length <= 20 ? digits : digits.Substring(0, 20);
        }
        private static string CleanPan(string value)
        {
            string normalized = new string((CleanNotApplicable(value) ?? string.Empty).ToUpperInvariant().Where(char.IsLetterOrDigit).ToArray());
            return normalized.Length == 10 ? normalized : null;
        }
        private static string CleanBankAccount(string value)
        {
            string normalized = new string((CleanNotApplicable(value) ?? string.Empty).Where(char.IsLetterOrDigit).ToArray());
            if (string.IsNullOrWhiteSpace(normalized))
                return null;
            return normalized.Length <= 20 ? normalized : normalized.Substring(0, 20);
        }
        private static string Last4(string value) { string digits = new string((value ?? string.Empty).Where(char.IsDigit).ToArray()); return digits.Length <= 4 ? digits : digits.Substring(digits.Length - 4); }
        private static string NormalizeStatusCode(string raw) { string value = Normalize(raw); if (value == "A") return "Absent"; if (value == "L") return "Leave"; if (value == "HL" || value == "HD") return "HalfDay"; if (value == "W" || value == "WO" || value == "H" || value == "HO") return "WeekOff"; return value.Length > 0 ? "Present" : string.Empty; }
        private static bool IsImportableEmployeeName(string value)
        {
            string normalized = Normalize(value);
            return normalized.Length >= 3 &&
                   normalized != "BLANK" &&
                   normalized != "NAME" &&
                   normalized != "TOTAL" &&
                   normalized != "GRANDTOTAL";
        }

        private static ImportedEmployeeRow FindImportedEmployee(Dictionary<string, ImportedEmployeeRow> importedEmployees, string employeeCode, string employeeName)
        {
            ImportedEmployeeRow row;
            if (!string.IsNullOrWhiteSpace(employeeCode) && importedEmployees.TryGetValue(employeeCode.Trim().ToUpperInvariant(), out row))
                return row;
            if (!string.IsNullOrWhiteSpace(employeeName) && importedEmployees.TryGetValue(Normalize(employeeName), out row))
                return row;
            return null;
        }

        private class ImportedEmployeeRow
        {
            public string EmployeeCode { get; set; }
            public string EmployeeName { get; set; }
            public DateTime? DateOfJoining { get; set; }
            public DateTime? DateOfBirth { get; set; }
            public string ESICNumber { get; set; }
            public string UAN { get; set; }
            public string PAN { get; set; }
            public string AadhaarLast4 { get; set; }
            public string BankAccountNumber { get; set; }
            public decimal BasicSalary { get; set; }
            public decimal GrossSalary { get; set; }
            public decimal HRA { get; set; }
            public decimal OtherAllowances { get; set; }

            public Employee ToEmployee()
            {
                return new Employee
                {
                    EmployeeCode = EmployeeCode,
                    Name = EmployeeName,
                    DateOfJoining = DateOfJoining,
                    JoiningDate = DateOfJoining,
                    DateOfBirth = DateOfBirth,
                    ESICNumber = ESICNumber,
                    UAN = UAN,
                    UANNumber = UAN,
                    PAN = PAN,
                    AadhaarLast4 = AadhaarLast4,
                    BankAccountNumber = BankAccountNumber,
                    BankAccount = BankAccountNumber,
                    EPFApplicable = !string.IsNullOrWhiteSpace(UAN),
                    ESIApplicable = !string.IsNullOrWhiteSpace(ESICNumber),
                    TaxRegime = "New",
                    EmploymentType = "Permanent",
                    BasicSalary = BasicSalary,
                    GrossSalary = GrossSalary
                };
            }
        }
    }
}
