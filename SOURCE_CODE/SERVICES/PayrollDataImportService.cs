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
            PayrollFolderHelper.EnsureFolders();
            string[] files = Directory.GetFiles(PayrollFolderHelper.SourcePayrollFolder, "*.*", SearchOption.TopDirectoryOnly);
            ServiceResult<PayrollImportReport> result = ImportFilesInternal(files, DateTime.Today.Month, DateTime.Today.Year, "PayrollDataImportService.ImportFromSourceFolder");
            if (result.Success)
                _settingsService.Set("PayrollHistoricalImportCompleted", "1");
            return result;
        }

        public ServiceResult<PayrollImportReport> ImportFiles(IEnumerable<string> filePaths, int defaultMonth, int defaultYear)
        {
            return ImportFilesInternal(filePaths, defaultMonth, defaultYear, "PayrollDataImportService.ImportFiles");
        }

        private ServiceResult<PayrollImportReport> ImportFilesInternal(IEnumerable<string> filePaths, int defaultMonth, int defaultYear, string operationName)
        {
            var report = new PayrollImportReport();
            try
            {
                string[] files = (filePaths ?? Enumerable.Empty<string>())
                    .Where(File.Exists)
                    .Where(path => IsSupportedImportFile(path))
                    .Distinct(StringComparer.OrdinalIgnoreCase)
                    .ToArray();

                if (files.Length == 0)
                    return ServiceResult<PayrollImportReport>.Fail("Select at least one payroll or attendance Excel/CSV file.");

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
                            ImportFile(file, report, employeesByCode, employeesByName, defaultMonth, defaultYear, conn, tx);
                        }

                        BackfillMissingSalaryStructures(report, conn, tx);
                        tx.Commit();
                    }
                }

                string message = BuildSuccessMessage(report);
                PayrollImportLogger.Log("Import complete | Files=" + report.FilesProcessed + " | PayrollEntries=" + report.PayrollEntriesImported + " | Attendance=" + report.AttendanceRecordsImported);
                return ServiceResult<PayrollImportReport>.Ok(report, message);
            }
            catch (Exception ex)
            {
                AppRuntime.LogException(operationName, ex);
                PayrollImportLogger.Log("ERROR " + ex);
                report.ErrorsEncountered++;
                return ServiceResult<PayrollImportReport>.Fail(ex.Message);
            }
        }

        private void ImportFile(string file, PayrollImportReport report, Dictionary<string, Employee> employeesByCode, Dictionary<string, List<Employee>> employeesByName, int defaultMonth, int defaultYear, SqlConnection conn, SqlTransaction tx)
        {
            string extension = Path.GetExtension(file) ?? string.Empty;
            if (extension.Equals(".xls", StringComparison.OrdinalIgnoreCase) || extension.Equals(".xlsx", StringComparison.OrdinalIgnoreCase))
            {
                ImportWorkbook(file, report, employeesByCode, employeesByName, defaultMonth, defaultYear, conn, tx);
                return;
            }

            if (extension.Equals(".csv", StringComparison.OrdinalIgnoreCase))
            {
                ImportCsvFile(file, report, employeesByCode, employeesByName, defaultMonth, defaultYear, conn, tx);
                return;
            }

            report.Warnings.Add("Skipped unsupported file: " + Path.GetFileName(file));
        }

        private void ImportWorkbook(string file, PayrollImportReport report, Dictionary<string, Employee> employeesByCode, Dictionary<string, List<Employee>> employeesByName, int defaultMonth, int defaultYear, SqlConnection conn, SqlTransaction tx)
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
                if (IsMasterSheet(sheetName, sheet.Value))
                    continue;
                if (IsPayrollSheet(sheetName, sheet.Value))
                    ImportPayrollSheet(file, sheetName, sheet.Value, importedEmployees, report, employeesByCode, employeesByName, defaultMonth, defaultYear, conn, tx);
                else if (IsAttendanceSheet(sheetName, sheet.Value))
                    ImportAttendanceSheet(file, sheetName, sheet.Value, importedEmployees, report, employeesByCode, employeesByName, defaultMonth, defaultYear, conn, tx);
            }
        }

        private void ImportCsvFile(string file, PayrollImportReport report, Dictionary<string, Employee> employeesByCode, Dictionary<string, List<Employee>> employeesByName, int defaultMonth, int defaultYear, SqlConnection conn, SqlTransaction tx)
        {
            DataTable table = LoadCsvAsTable(file);
            var sheets = new Dictionary<string, DataTable>(StringComparer.OrdinalIgnoreCase)
            {
                { Path.GetFileNameWithoutExtension(file) ?? "Sheet1", table }
            };
            Dictionary<string, ImportedEmployeeRow> importedEmployees = ParseMasterSheets(sheets);

            string contextName = Path.GetFileNameWithoutExtension(file) ?? "CSV";
            if (IsPayrollSheet(contextName, table))
                ImportPayrollSheet(file, contextName, table, importedEmployees, report, employeesByCode, employeesByName, defaultMonth, defaultYear, conn, tx);
            else if (IsAttendanceSheet(contextName, table))
                ImportAttendanceSheet(file, contextName, table, importedEmployees, report, employeesByCode, employeesByName, defaultMonth, defaultYear, conn, tx);
            else if (IsMasterSheet(contextName, table))
                PayrollImportLogger.Log("Master-only CSV detected: " + file);
            else
                report.Warnings.Add("Could not detect payroll/attendance layout in " + Path.GetFileName(file) + ".");
        }

        private Dictionary<string, DataTable> LoadWorkbookSheets(string file)
        {
            var map = new Dictionary<string, DataTable>(StringComparer.OrdinalIgnoreCase);
            string extProps = Path.GetExtension(file).Equals(".xls", StringComparison.OrdinalIgnoreCase) ? "Excel 8.0;HDR=NO;IMEX=1" : "Excel 12.0 Xml;HDR=NO;IMEX=1";
            var builder = new OleDbConnectionStringBuilder
            {
                Provider = "Microsoft.ACE.OLEDB.12.0",
                DataSource = file
            };
            builder["Extended Properties"] = extProps;

            using (var conn = new OleDbConnection(builder.ConnectionString))
            {
                conn.Open();
                DataTable schema = conn.GetOleDbSchemaTable(OleDbSchemaGuid.Tables, null);
                foreach (DataRow row in schema.Rows)
                {
                    string sheetName = Convert.ToString(row["TABLE_NAME"]);
                    if (string.IsNullOrWhiteSpace(sheetName) || (!sheetName.EndsWith("$") && !sheetName.EndsWith("$'")))
                        continue;
                    var table = new DataTable();
                    using (var adapter = new OleDbDataAdapter("SELECT * FROM [" + EscapeSheetIdentifier(sheetName) + "]", conn))
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
                if (!IsMasterSheet(pair.Key, pair.Value))
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

        private void ImportPayrollSheet(string file, string contextName, DataTable table, Dictionary<string, ImportedEmployeeRow> importedEmployees, PayrollImportReport report, Dictionary<string, Employee> employeesByCode, Dictionary<string, List<Employee>> employeesByName, int defaultMonth, int defaultYear, SqlConnection conn, SqlTransaction tx)
        {
            int month;
            int year;
            ResolveMonthYear(file, contextName, table, defaultMonth, defaultYear, out month, out year);
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

        private void ImportAttendanceSheet(string file, string contextName, DataTable table, Dictionary<string, ImportedEmployeeRow> importedEmployees, PayrollImportReport report, Dictionary<string, Employee> employeesByCode, Dictionary<string, List<Employee>> employeesByName, int defaultMonth, int defaultYear, SqlConnection conn, SqlTransaction tx)
        {
            int month;
            int year;
            ResolveMonthYear(file, contextName, table, defaultMonth, defaultYear, out month, out year);
            int headerRow = FindRow(table, row => RowContains(row, "SRNO") && RowContains(row, "NAME"));
            if (headerRow >= 0)
            {
                ImportAttendanceRegisterLayout(table, headerRow, month, year, importedEmployees, report, employeesByCode, employeesByName, conn, tx);
                return;
            }

            int daysRow = FindRow(table, row => Normalize(Cell(row, 0)) == "DAYS");
            if (daysRow >= 0)
                ImportAttendanceEmployeeBlockLayout(table, daysRow, month, year, importedEmployees, report, employeesByCode, employeesByName, conn, tx);
        }

        private void ImportAttendanceRegisterLayout(DataTable table, int headerRow, int month, int year, Dictionary<string, ImportedEmployeeRow> importedEmployees, PayrollImportReport report, Dictionary<string, Employee> employeesByCode, Dictionary<string, List<Employee>> employeesByName, SqlConnection conn, SqlTransaction tx)
        {
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

        private void ImportAttendanceEmployeeBlockLayout(DataTable table, int daysRow, int month, int year, Dictionary<string, ImportedEmployeeRow> importedEmployees, PayrollImportReport report, Dictionary<string, Employee> employeesByCode, Dictionary<string, List<Employee>> employeesByName, SqlConnection conn, SqlTransaction tx)
        {
            Dictionary<int, int> dayCols = FindDayColumns(table, daysRow, 0);
            if (dayCols.Count == 0)
                return;

            var existingAttendance = new HashSet<string>(_repo.GetAttendanceRecordsForMonth(month, year, conn, tx).Select(a => a.EmployeeId + "|" + a.AttendanceDate.ToString("yyyyMMdd", CultureInfo.InvariantCulture)), StringComparer.OrdinalIgnoreCase);

            for (int rowIndex = daysRow + 1; rowIndex < table.Rows.Count; rowIndex++)
            {
                if (Normalize(Cell(table.Rows[rowIndex], 0)) != "EMPLOYEE")
                    continue;

                string employeeDescriptor = FindEmployeeDescriptor(table.Rows[rowIndex]);
                if (string.IsNullOrWhiteSpace(employeeDescriptor))
                    continue;

                ParseEmployeeDescriptor(employeeDescriptor, out string employeeCode, out string employeeName);
                if (string.IsNullOrWhiteSpace(employeeCode) && string.IsNullOrWhiteSpace(employeeName))
                    continue;

                ImportedEmployeeRow imported = FindImportedEmployee(importedEmployees, employeeCode, employeeName);
                Employee employee = MatchOrCreateEmployee(imported ?? new ImportedEmployeeRow { EmployeeCode = employeeCode, EmployeeName = employeeName }, report, employeesByCode, employeesByName, conn, tx);
                if (employee == null)
                    continue;

                int statusRowIndex = FindNextRow(table, rowIndex + 1, row => Normalize(Cell(row, 0)) == "STATUS");
                if (statusRowIndex < 0)
                    continue;

                foreach (KeyValuePair<int, int> dayCol in dayCols)
                {
                    string status = NormalizeStatusCode(Cell(table.Rows[statusRowIndex], dayCol.Key));
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

        private static bool IsSupportedImportFile(string path)
        {
            string extension = Path.GetExtension(path) ?? string.Empty;
            return extension.Equals(".xls", StringComparison.OrdinalIgnoreCase)
                || extension.Equals(".xlsx", StringComparison.OrdinalIgnoreCase)
                || extension.Equals(".csv", StringComparison.OrdinalIgnoreCase);
        }

        private static string BuildSuccessMessage(PayrollImportReport report)
        {
            return "Import complete. Files: " + report.FilesProcessed
                + " | Payroll entries: " + report.PayrollEntriesImported
                + " | Attendance rows: " + report.AttendanceRecordsImported
                + " | New employees: " + report.NewEmployeesCreated
                + (report.Warnings.Count > 0 ? " | Warnings: " + report.Warnings.Count : string.Empty);
        }

        private static bool IsMasterSheet(string sheetName, DataTable table)
        {
            return (sheetName ?? string.Empty).IndexOf("MASTER", StringComparison.OrdinalIgnoreCase) >= 0
                || FindRow(table, row => RowContains(row, "NAMEOFEMPLOYEE") && (RowContains(row, "IDNO") || RowContains(row, "UANNUMBER"))) >= 0;
        }

        private static bool IsPayrollSheet(string sheetName, DataTable table)
        {
            if ((sheetName ?? string.Empty).IndexOf("PAYROLL", StringComparison.OrdinalIgnoreCase) >= 0
                || (sheetName ?? string.Empty).IndexOf("PAY ROLL", StringComparison.OrdinalIgnoreCase) >= 0)
                return true;

            return FindRow(table, row =>
                (RowContains(row, "FULLNAMEOFTHEEMPLOYEE") || RowContains(row, "EMPLOYEE NAME") || RowContains(row, "EMPLOYEE"))
                && (RowContains(row, "TOTALDAYSWORKED") || RowContains(row, "NETWAGESPAID") || RowContains(row, "TOTALWAGESPAYABLE") || RowContains(row, "GROSS"))) >= 0;
        }

        private static bool IsAttendanceSheet(string sheetName, DataTable table)
        {
            int headerRow = FindRow(table, row => (RowContains(row, "SRNO") || RowContains(row, "EMPLOYEE")) && RowContains(row, "NAME"));
            if (headerRow >= 0 && FindDayColumns(table, headerRow, 3).Count >= 5)
                return true;

            int daysRow = FindRow(table, row => Normalize(Cell(row, 0)) == "DAYS");
            if (daysRow >= 0 && FindDayColumns(table, daysRow, 0).Count >= 5 && FindRow(table, row => Normalize(Cell(row, 0)) == "EMPLOYEE") >= 0)
                return true;

            return Regex.IsMatch(sheetName ?? string.Empty, @"\b(?:JAN|FEB|MAR|APR|MAY|JUN|JUL|AUG|SEP|OCT|NOV|DEC)\b", RegexOptions.IgnoreCase)
                && FindRow(table, row => RowContains(row, "STATUS")) >= 0;
        }

        private static DataTable LoadCsvAsTable(string file)
        {
            var table = new DataTable();
            foreach (string line in File.ReadLines(file))
            {
                string[] fields = ParseCsvLine(line).ToArray();
                while (table.Columns.Count < fields.Length)
                    table.Columns.Add("Column" + table.Columns.Count, typeof(string));

                DataRow row = table.NewRow();
                for (int i = 0; i < fields.Length; i++)
                    row[i] = fields[i];

                table.Rows.Add(row);
            }

            return table;
        }

        private static IEnumerable<string> ParseCsvLine(string line)
        {
            if (line == null)
                yield break;

            bool insideQuotes = false;
            var buffer = new List<char>();
            for (int i = 0; i < line.Length; i++)
            {
                char current = line[i];
                if (current == '"')
                {
                    if (insideQuotes && i + 1 < line.Length && line[i + 1] == '"')
                    {
                        buffer.Add('"');
                        i++;
                    }
                    else
                    {
                        insideQuotes = !insideQuotes;
                    }
                }
                else if (current == ',' && !insideQuotes)
                {
                    yield return new string(buffer.ToArray()).Trim();
                    buffer.Clear();
                }
                else
                {
                    buffer.Add(current);
                }
            }

            yield return new string(buffer.ToArray()).Trim();
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
                    if (TryParseDayNumber(Cell(table.Rows[headerRow + offset], column), out int day))
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

        private static int FindNextRow(DataTable table, int startIndex, Func<DataRow, bool> predicate)
        {
            for (int i = Math.Max(0, startIndex); i < table.Rows.Count; i++)
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

        private static bool TryParseDayNumber(string value, out int day)
        {
            day = 0;
            if (string.IsNullOrWhiteSpace(value))
                return false;

            Match match = Regex.Match(value.Trim(), @"^(?<day>\d{1,2})\b");
            if (!match.Success)
                return false;

            return int.TryParse(match.Groups["day"].Value, NumberStyles.Integer, CultureInfo.InvariantCulture, out day) && day >= 1 && day <= 31;
        }

        private static string FindEmployeeDescriptor(DataRow row)
        {
            for (int column = 1; column < row.Table.Columns.Count; column++)
            {
                string value = Cell(row, column);
                if (!string.IsNullOrWhiteSpace(value))
                    return value;
            }

            return string.Empty;
        }

        private static void ParseEmployeeDescriptor(string descriptor, out string employeeCode, out string employeeName)
        {
            employeeCode = string.Empty;
            employeeName = string.Empty;
            if (string.IsNullOrWhiteSpace(descriptor))
                return;

            Match match = Regex.Match(descriptor.Trim(), @"^(?<code>[^:]+?)\s*:\s*(?<name>.+)$");
            if (match.Success)
            {
                employeeCode = match.Groups["code"].Value.Trim();
                employeeName = ToTitle(match.Groups["name"].Value.Trim());
                return;
            }

            employeeName = ToTitle(descriptor.Trim());
        }

        private static void ResolveMonthYear(string fileName, string contextName, DataTable table, int defaultMonth, int defaultYear, out int month, out int year)
        {
            month = defaultMonth >= 1 && defaultMonth <= 12 ? defaultMonth : DateTime.Today.Month;
            year = defaultYear >= 2000 ? defaultYear : DateTime.Today.Year;

            Match match = Regex.Match((Path.GetFileNameWithoutExtension(fileName) ?? string.Empty) + " " + (contextName ?? string.Empty), @"(Jan|Feb|Mar|Apr|May|Jun|Jul|Aug|Sep|Oct|Nov|Dec)[^\d]*(\d{2,4})", RegexOptions.IgnoreCase);
            if (match.Success)
            {
                month = DateTime.ParseExact(match.Groups[1].Value, "MMM", CultureInfo.InvariantCulture).Month;
                year = int.Parse(match.Groups[2].Value, CultureInfo.InvariantCulture);
                if (year < 100)
                    year += 2000;
                return;
            }

            for (int rowIndex = 0; rowIndex < Math.Min(table?.Rows.Count ?? 0, 8); rowIndex++)
            {
                foreach (object cell in table.Rows[rowIndex].ItemArray)
                {
                    string text = Convert.ToString(cell);
                    if (string.IsNullOrWhiteSpace(text))
                        continue;

                    match = Regex.Match(text, @"(Jan|Feb|Mar|Apr|May|Jun|Jul|Aug|Sep|Oct|Nov|Dec)[^\d]*(\d{2,4})", RegexOptions.IgnoreCase);
                    if (!match.Success)
                        continue;

                    month = DateTime.ParseExact(match.Groups[1].Value, "MMM", CultureInfo.InvariantCulture).Month;
                    year = int.Parse(match.Groups[2].Value, CultureInfo.InvariantCulture);
                    if (year < 100)
                        year += 2000;
                    return;
                }
            }
        }

        private static string CleanSheetName(string rawName) => rawName.Trim('\'').TrimEnd('$').Trim();
        private static string EscapeSheetIdentifier(string sheetName) => (sheetName ?? string.Empty).Replace("]", "]]");
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
        private static string NormalizeStatusCode(string raw)
        {
            string value = Normalize(raw);
            if (value == "A")
                return "Absent";
            if (value == "L" || value == "C" || value == "CL" || value == "E" || value == "EL" || value == "PL")
                return "Leave";
            if (value == "HL" || value == "HD" || value == "P2" || value == "PH2")
                return "HalfDay";
            if (value == "W" || value == "WO")
                return "WeekOff";
            if (value == "H" || value == "HO" || value == "PH" || value == "WHO")
                return "Holiday";
            if (value == "P")
                return "Present";
            return value.Length > 0 ? "Present" : string.Empty;
        }
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
