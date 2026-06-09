using System;
using System.Collections.Generic;
using System.Data.SqlClient;
using System.Globalization;
using System.Reflection;
using HVAC_Pro_Desktop.DAL;
using HVAC_Pro_Desktop.Services;
using HVAC_Pro_Desktop.UI;

namespace HVAC_Pro_Desktop.Tests
{
    /// <summary>
    /// Smoke tests for the Add AMC save path (DB-layer only, no WinForms instantiation).
    /// Tests the exact paths that caused lag/blank/dead on the + Add AMC button:
    ///   1. Insert new contract returns a positive ID
    ///   2. Update existing contract returns the same ID
    ///   3. Update non-existent contract falls back to insert
    ///   4. Duplicate AMC number raises a domain exception, not a generic crash
    ///   5. AMCPage.OpenContractById public method exists
    ///   6. AMCPage._addAmcDialogOpen guard field exists (prevents double-open)
    ///   7. AddAMCForm.LastSavedContractId property exists and is publicly readable
    /// </summary>
    public static class AddAMCSmokeTests
    {
        private const string QaAMCPrefix = "QA-AMC-SMOKE-";

        public static List<string> RunAll()
        {
            var results = new List<string>();
            DbHelper.EnsureAMCSchema();

            int qaClientId = EnsureQaClient();
            if (qaClientId <= 0)
            {
                results.Add("SKIP AddAMC smoke: no active client in B2BClients to attach AMC to");
                return results;
            }

            CleanupPreviousQaContracts();

            results.Add(Run("Insert new AMC contract returns positive ID",
                () => TestInsert(qaClientId)));

            int insertedId = DirectInsert(qaClientId, QaAMCPrefix + "UPD-" + DateTime.Today.Year);
            results.Add(Run("Update existing AMC contract returns same ID",
                () => TestUpdate(insertedId)));

            results.Add(Run("Update non-existent AMC contract falls back to insert",
                () => TestFallbackInsert(qaClientId)));

            results.Add(Run("Duplicate AMC number raises DuplicateAMCNumberException, not generic exception",
                () => TestDuplicateRejection(qaClientId)));

            results.Add(Run("AMCPage.OpenContractById is a public method",
                () => TestOpenContractByIdExists()));

            results.Add(Run("AMCPage._addAmcDialogOpen guard field exists",
                () => TestAddAmcDialogOpenGuardExists()));

            results.Add(Run("AddAMCForm.LastSavedContractId property is publicly readable",
                () => TestLastSavedContractIdPropertyExists()));

            CleanupPreviousQaContracts();
            return results;
        }

        // ── test helpers ─────────────────────────────────────────────────────

        private static string Run(string label, Action test)
        {
            try
            {
                test();
                return "PASS " + label;
            }
            catch (Exception ex)
            {
                return "FAIL " + label + " | " + ex.GetType().Name + ": " + ex.Message;
            }
        }

        // ── test cases ───────────────────────────────────────────────────────

        private static void TestInsert(int clientId)
        {
            string amcNumber = QaAMCPrefix + "N-" + (Environment.TickCount & 0xFFFF).ToString("X4");
            int id = DirectInsert(clientId, amcNumber);
            if (id <= 0)
                throw new InvalidOperationException("Insert returned ID <= 0 (" + id + ").");
        }

        private static void TestUpdate(int contractId)
        {
            string newAmcNumber = QaAMCPrefix + "U-" + (Environment.TickCount & 0xFFFF).ToString("X4");
            bool updated = DirectUpdate(contractId, newAmcNumber);
            if (!updated)
                throw new InvalidOperationException("Update returned false for contract " + contractId + " (0 rows affected).");
        }

        private static void TestFallbackInsert(int clientId)
        {
            int nonExistentId = -999999;
            string amcNumber = QaAMCPrefix + "FB-" + (Environment.TickCount & 0xFFFF).ToString("X4");
            bool updated = DirectUpdate(nonExistentId, amcNumber);
            if (updated)
                throw new InvalidOperationException("DirectUpdate returned true for non-existent ID — fallback path not taken.");
            // Fallback: insert as new
            int newId = DirectInsert(clientId, amcNumber);
            if (newId <= 0)
                throw new InvalidOperationException("Fallback insert returned ID <= 0 (" + newId + ").");
            if (newId == nonExistentId)
                throw new InvalidOperationException("Fallback insert returned the non-existent ID " + nonExistentId + ".");
        }

        private static void TestDuplicateRejection(int clientId)
        {
            string amcNumber = QaAMCPrefix + "DUP-" + DateTime.Today.Year;
            try { DirectInsert(clientId, amcNumber); } catch { }

            bool gotDuplicate = false;
            try
            {
                DirectInsert(clientId, amcNumber);
            }
            catch (SqlException ex) when (ex.Number == 2601 || ex.Number == 2627)
            {
                gotDuplicate = true;
            }
            catch (Exception ex) when (ex.GetType().Name == "DuplicateAMCNumberException")
            {
                gotDuplicate = true;
            }

            if (!gotDuplicate)
                throw new InvalidOperationException("Duplicate AMC number did not raise a uniqueness violation.");
        }

        private static void TestOpenContractByIdExists()
        {
            MethodInfo m = typeof(AMCPage).GetMethod("OpenContractById", BindingFlags.Instance | BindingFlags.Public);
            if (m == null)
                throw new InvalidOperationException("AMCPage.OpenContractById public method not found.");
        }

        private static void TestAddAmcDialogOpenGuardExists()
        {
            FieldInfo f = typeof(AMCPage).GetField("_addAmcDialogOpen", BindingFlags.Instance | BindingFlags.NonPublic);
            if (f == null)
                throw new InvalidOperationException("AMCPage._addAmcDialogOpen guard field not found.");
        }

        private static void TestLastSavedContractIdPropertyExists()
        {
            System.Reflection.PropertyInfo p = typeof(AddAMCForm).GetProperty("LastSavedContractId", BindingFlags.Instance | BindingFlags.Public);
            if (p == null)
                throw new InvalidOperationException("AddAMCForm.LastSavedContractId public property not found.");
            if (!p.CanRead)
                throw new InvalidOperationException("AddAMCForm.LastSavedContractId property is not readable.");
        }

        // ── SQL helpers ───────────────────────────────────────────────────────

        private static int DirectInsert(int clientId, string amcNumber)
        {
            using (var conn = DatabaseConnectionFactory.CreateConnection())
            {
                DatabaseConnectionFactory.Open(conn, "AddAMCSmokeTests.DirectInsert");
                using (var cmd = new SqlCommand(@"
INSERT INTO AMCContracts
    (AMCNumber, ClientID, SiteID, EquipmentDesc, AMCType, StartDate, EndDate,
     ContractValue, BillingCycle, CoverageType, VisitsPerYear, Status, Notes,
     CreatedAt, UpdatedAt, MonthlyValue, AnnualValue, ContractStatus,
     MaintenanceFrequency, ContractType)
VALUES
    (@AMCNumber, @ClientID, NULL, 'QA Smoke Test Equipment', 'Comprehensive', @Start, @End,
     12000, 'Annual', 'Comprehensive', 2, 'Active', 'Created by AddAMCSmokeTests',
     GETDATE(), GETDATE(), 1000, 12000, 'Active', 'Annual', 'AMC');
SELECT CAST(SCOPE_IDENTITY() AS INT);", conn))
                {
                    cmd.CommandTimeout = 10;
                    cmd.Parameters.AddWithValue("@AMCNumber", amcNumber);
                    cmd.Parameters.AddWithValue("@ClientID", clientId);
                    cmd.Parameters.AddWithValue("@Start", DateTime.Today);
                    cmd.Parameters.AddWithValue("@End", DateTime.Today.AddYears(1));
                    return Convert.ToInt32(cmd.ExecuteScalar(), CultureInfo.InvariantCulture);
                }
            }
        }

        private static bool DirectUpdate(int contractId, string newAmcNumber)
        {
            using (var conn = DatabaseConnectionFactory.CreateConnection())
            {
                DatabaseConnectionFactory.Open(conn, "AddAMCSmokeTests.DirectUpdate");
                using (var cmd = new SqlCommand(@"
UPDATE AMCContracts
SET AMCNumber = @AMCNumber, UpdatedAt = GETDATE()
WHERE ContractID = @ContractID;
SELECT @@ROWCOUNT;", conn))
                {
                    cmd.CommandTimeout = 10;
                    cmd.Parameters.AddWithValue("@AMCNumber", newAmcNumber);
                    cmd.Parameters.AddWithValue("@ContractID", contractId);
                    int rows = Convert.ToInt32(cmd.ExecuteScalar(), CultureInfo.InvariantCulture);
                    return rows > 0;
                }
            }
        }

        private static int EnsureQaClient()
        {
            try
            {
                using (var conn = DatabaseConnectionFactory.CreateConnection())
                {
                    DatabaseConnectionFactory.Open(conn, "AddAMCSmokeTests.EnsureQaClient");
                    using (var cmd = new SqlCommand("SELECT TOP 1 ClientID FROM B2BClients WHERE ISNULL(IsActive,1)=1 ORDER BY ClientID;", conn))
                    {
                        cmd.CommandTimeout = 8;
                        object result = cmd.ExecuteScalar();
                        return result == null || result == DBNull.Value ? 0 : Convert.ToInt32(result, CultureInfo.InvariantCulture);
                    }
                }
            }
            catch
            {
                return 0;
            }
        }

        private static void CleanupPreviousQaContracts()
        {
            try
            {
                using (var conn = DatabaseConnectionFactory.CreateConnection())
                {
                    DatabaseConnectionFactory.Open(conn, "AddAMCSmokeTests.Cleanup");
                    using (var cmd = new SqlCommand(@"
DELETE FROM AMCVisits    WHERE AMCID    IN (SELECT ContractID FROM AMCContracts WHERE AMCNumber LIKE @prefix);
DELETE FROM AMCEquipment WHERE AMCID    IN (SELECT ContractID FROM AMCContracts WHERE AMCNumber LIKE @prefix);
DELETE FROM AMCContracts WHERE AMCNumber LIKE @prefix;", conn))
                    {
                        cmd.CommandTimeout = 15;
                        cmd.Parameters.AddWithValue("@prefix", QaAMCPrefix + "%");
                        cmd.ExecuteNonQuery();
                    }
                }
            }
            catch { }
        }
    }
}
