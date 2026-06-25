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

            int insertedId = SaveViaForm(null, qaClientId, BuildUniqueAmcNumber("UPD"));
            results.Add(Run("Update existing AMC contract returns same ID",
                () => TestUpdate(insertedId)));

            results.Add(Run("Update non-existent AMC contract falls back to insert",
                () => TestFallbackInsert(qaClientId)));

            results.Add(Run("Duplicate AMC number raises DuplicateAMCNumberException, not generic exception",
                () => TestDuplicateRejection(qaClientId)));

            results.Add(Run("Over-length AMC number is rejected before SQL save",
                () => TestLengthValidation(qaClientId)));

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
            string amcNumber = BuildUniqueAmcNumber("N");
            int id = SaveViaForm(null, clientId, amcNumber);
            if (id <= 0)
                throw new InvalidOperationException("Insert returned ID <= 0 (" + id + ").");
        }

        private static void TestUpdate(int contractId)
        {
            int clientId = GetClientIdForContract(contractId);
            string newAmcNumber = BuildUniqueAmcNumber("U");
            int savedId = SaveViaForm(contractId, clientId, newAmcNumber);
            if (savedId != contractId)
                throw new InvalidOperationException("Update returned " + savedId + " instead of the existing contract " + contractId + ".");
        }

        private static void TestFallbackInsert(int clientId)
        {
            int nonExistentId = -999999;
            string amcNumber = BuildUniqueAmcNumber("FB");
            int newId = SaveViaForm(nonExistentId, clientId, amcNumber);
            if (newId <= 0)
                throw new InvalidOperationException("Fallback insert returned ID <= 0 (" + newId + ").");
            if (newId == nonExistentId)
                throw new InvalidOperationException("Fallback insert returned the non-existent ID " + nonExistentId + ".");
        }

        private static void TestDuplicateRejection(int clientId)
        {
            string amcNumber = BuildUniqueAmcNumber("DUP");
            SaveViaForm(null, clientId, amcNumber);

            bool gotDuplicate = false;
            try
            {
                SaveViaForm(null, clientId, amcNumber);
            }
            catch (Exception ex) when (Unwrap(ex).GetType().Name == "DuplicateAMCNumberException")
            {
                gotDuplicate = true;
            }

            if (!gotDuplicate)
                throw new InvalidOperationException("Duplicate AMC number did not raise a uniqueness violation.");
        }

        private static void TestLengthValidation(int clientId)
        {
            int maxLength = GetAmcNumberMaxLength();
            string tooLong = new string('A', maxLength + 1);
            bool gotLengthValidation = false;
            try
            {
                SaveViaForm(null, clientId, tooLong);
            }
            catch (Exception ex)
            {
                Exception baseException = Unwrap(ex);
                if (baseException.GetType().Name == "AMCNumberLengthException")
                    gotLengthValidation = true;
                else if (baseException is SqlException)
                    throw new InvalidOperationException("AMC number length validation reached SQL instead of failing cleanly.", baseException);
            }

            if (!gotLengthValidation)
                throw new InvalidOperationException("Over-length AMC number did not raise the expected validation exception.");
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

        // ── save helpers ──────────────────────────────────────────────────────

        private static int SaveViaForm(int? contractId, int clientId, string amcNumber)
        {
            using (var form = contractId.HasValue ? new AddAMCForm(contractId.Value) : new AddAMCForm())
            {
                object input = CreateInput(form.GetType(), contractId.GetValueOrDefault(), clientId, amcNumber);
                return InvokeSaveInputOn(form, input);
            }
        }

        private static object CreateInput(Type formType, int contractId, int clientId, string amcNumber)
        {
            Type inputType = formType.GetNestedType("AMCInput", BindingFlags.NonPublic);
            if (inputType == null)
                throw new InvalidOperationException("AddAMCForm.AMCInput nested type not found.");

            object input = Activator.CreateInstance(inputType, true);
            SetField(inputType, input, "ContractId", contractId);
            SetField(inputType, input, "AMCNumber", amcNumber);
            SetField(inputType, input, "ClientId", clientId);
            SetField(inputType, input, "SiteId", null);
            SetField(inputType, input, "EquipmentDesc", "QA Smoke Test Equipment");
            SetField(inputType, input, "AMCType", "Comprehensive");
            SetField(inputType, input, "CoverageType", "Comprehensive");
            SetField(inputType, input, "StartDate", DateTime.Today);
            SetField(inputType, input, "EndDate", DateTime.Today.AddYears(1));
            SetField(inputType, input, "ContractValue", 12000m);
            SetField(inputType, input, "BillingCycle", "Annual");
            SetField(inputType, input, "VisitsPerYear", 2);
            SetField(inputType, input, "Status", "Active");
            SetField(inputType, input, "Notes", "Created by AddAMCSmokeTests");
            return input;
        }

        private static int InvokeSaveInputOn(AddAMCForm form, object input)
        {
            MethodInfo method = typeof(AddAMCForm).GetMethod("SaveInput", BindingFlags.Instance | BindingFlags.NonPublic);
            if (method == null)
                throw new InvalidOperationException("AddAMCForm.SaveInput private method not found.");

            try
            {
                return Convert.ToInt32(method.Invoke(form, new[] { input }), CultureInfo.InvariantCulture);
            }
            catch (TargetInvocationException ex)
            {
                throw ex.InnerException ?? ex;
            }
        }

        private static void SetField(Type type, object target, string fieldName, object value)
        {
            FieldInfo field = type.GetField(fieldName, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
            if (field == null)
                throw new InvalidOperationException("Field not found on AMCInput: " + fieldName);

            field.SetValue(target, value);
        }

        private static string BuildUniqueAmcNumber(string marker)
        {
            int maxLength = GetAmcNumberMaxLength();
            string suffix = "-" + marker + "-" + DateTime.Now.ToString("HHmmss", CultureInfo.InvariantCulture) + "-" + (Environment.TickCount & 0xFFFF).ToString("X4", CultureInfo.InvariantCulture);
            string prefix = QaAMCPrefix.TrimEnd('-');
            int availablePrefix = Math.Max(1, maxLength - suffix.Length);
            if (prefix.Length > availablePrefix)
                prefix = prefix.Substring(0, availablePrefix);
            return prefix + suffix;
        }

        private static int GetAmcNumberMaxLength()
        {
            using (var conn = DatabaseConnectionFactory.CreateConnection())
            {
                DatabaseConnectionFactory.Open(conn, "AddAMCSmokeTests.GetAmcNumberMaxLength");
                using (var cmd = new SqlCommand("SELECT COL_LENGTH('dbo.AMCContracts', 'AMCNumber');", conn))
                {
                    cmd.CommandTimeout = 8;
                    object result = cmd.ExecuteScalar();
                    if (result == null || result == DBNull.Value)
                        return 30;

                    int sqlLengthBytes = Convert.ToInt32(result, CultureInfo.InvariantCulture);
                    return sqlLengthBytes > 0 ? Math.Max(1, sqlLengthBytes / 2) : 30;
                }
            }
        }

        private static int GetClientIdForContract(int contractId)
        {
            using (var conn = DatabaseConnectionFactory.CreateConnection())
            {
                DatabaseConnectionFactory.Open(conn, "AddAMCSmokeTests.GetClientIdForContract");
                using (var cmd = new SqlCommand("SELECT TOP 1 ClientID FROM AMCContracts WHERE ContractID = @ContractID;", conn))
                {
                    cmd.CommandTimeout = 8;
                    cmd.Parameters.AddWithValue("@ContractID", contractId);
                    object result = cmd.ExecuteScalar();
                    if (result == null || result == DBNull.Value)
                        throw new InvalidOperationException("AMC contract " + contractId + " was not found for update smoke.");

                    return Convert.ToInt32(result, CultureInfo.InvariantCulture);
                }
            }
        }

        private static Exception Unwrap(Exception ex)
        {
            return ex is TargetInvocationException tie && tie.InnerException != null ? tie.InnerException : ex;
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
