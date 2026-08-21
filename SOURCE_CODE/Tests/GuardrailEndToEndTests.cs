using System;
using System.Data.SqlClient;
using System.IO;
using HVAC_Pro_Desktop.DAL;
using HVAC_Pro_Desktop.Models;
using HVAC_Pro_Desktop.Services;

namespace HVAC_Pro_Desktop.Tests
{
    /// <summary>Executable end-to-end verification for production guardrails using isolated QA-tagged records.</summary>
    public static class GuardrailEndToEndTests
    {
        public static string WriteReport()
        {
            string directory = Path.Combine(@"C:\HVAC_PRO_MSE", "TEST_RESULTS", "guardrail-e2e");
            Directory.CreateDirectory(directory);
            string reportPath = Path.Combine(directory, "guardrail-e2e-" + DateTime.Now.ToString("yyyyMMdd-HHmmss") + ".txt");
            string token = "QA-GUARDRAIL-" + Guid.NewGuid().ToString("N").Substring(0, 12).ToUpperInvariant();
            int invoiceId = 0;
            int stockItemId = 0;
            int purchaseOrderId = 0;
            int vendorId = 0;
            AppUserDto previousUser = SessionManager.CurrentUser;
            var lines = new System.Collections.Generic.List<string>();

            try
            {
                new DatabaseManager().InitializeDatabase();
                AssertGuardrailSchema();
                lines.Add("PASS guardrail schema and uniqueness indexes verified");

                SessionManager.SetSession(new AppUserDto { UserId = 0, Username = "guardrail-qa", DisplayName = "Guardrail QA", RoleName = "Admin", IsActive = true });
                int clientId = EnsureQaClient(token);
                invoiceId = CreateQaInvoice(clientId, token);
                var paymentService = new PaymentService();

                paymentService.RecordPayment(new Payment { InvoiceID = invoiceId, AmountPaid = 40m, PaymentDate = DateTime.Today, PaymentMode = "NEFT", ReferenceNumber = token + "-UTR" });
                ExpectBlocked(() => paymentService.RecordPayment(new Payment { InvoiceID = invoiceId, AmountPaid = 10m, PaymentDate = DateTime.Today, PaymentMode = "NEFT", ReferenceNumber = token + "-UTR" }), "duplicate UTR");
                ExpectBlocked(() => paymentService.RecordPayment(new Payment { InvoiceID = invoiceId, AmountPaid = 100m, PaymentDate = DateTime.Today, PaymentMode = "NEFT", ReferenceNumber = token + "-OVER" }), "overpayment");
                lines.Add("PASS payment duplicate-reference and overpayment guards blocked unsafe writes");

                stockItemId = CreateQaStockItem(token);
                ExpectBlocked(() => new InventoryService().AdjustStock(stockItemId, -2m, token, "QA negative-stock guardrail test"), "negative stock");
                lines.Add("PASS negative-stock guard blocked unsafe adjustment");

                vendorId = CreateQaVendor(token);
                purchaseOrderId = CreateReceivedQaPurchaseOrder(vendorId, token);
                new PurchaseService().MarkReceived(purchaseOrderId);
                if (!string.Equals(GetPurchaseStatus(purchaseOrderId), "Fully Received", StringComparison.OrdinalIgnoreCase))
                    throw new InvalidOperationException("Duplicate PO receipt guard changed a completed purchase order.");
                lines.Add("PASS duplicate purchase receipt guard retained completed status");
                lines.Add("PASS Guardrail end-to-end test completed: " + token);
            }
            catch (Exception ex)
            {
                lines.Add("FAIL " + ex);
            }
            finally
            {
                Cleanup(token, invoiceId, stockItemId, purchaseOrderId, vendorId);
                SessionManager.SetSession(previousUser);
            }

            File.WriteAllLines(reportPath, lines);
            return reportPath;
        }

        private static void AssertGuardrailSchema()
        {
            using (SqlConnection conn = new SqlConnection(DatabaseManager.RequireConfiguredConnectionString()))
            {
                conn.Open();
                foreach (string table in new[] { "GuardrailOverrides", "RecordEditPresence", "OperationIdempotency", "BackupHealthChecks" })
                    AssertExists(conn, "SELECT 1 FROM sys.tables WHERE name=@name", table);
                foreach (string index in new[] { "UX_Payments_ReferenceNumber", "UX_Invoices_InvoiceNumber", "UX_PurchaseOrders_PONumber" })
                    AssertExists(conn, "SELECT 1 FROM sys.indexes WHERE name=@name", index);
            }
        }

        private static int EnsureQaClient(string token)
        {
            using (SqlConnection conn = new SqlConnection(DatabaseManager.RequireConfiguredConnectionString()))
            {
                conn.Open();
                using (SqlCommand cmd = new SqlCommand("INSERT INTO B2BClients (CompanyName, IsActive, Notes) VALUES (@name, 1, @notes); SELECT CAST(SCOPE_IDENTITY() AS INT);", conn))
                {
                    cmd.Parameters.AddWithValue("@name", token + " Client");
                    cmd.Parameters.AddWithValue("@notes", token);
                    return (int)cmd.ExecuteScalar();
                }
            }
        }

        private static int CreateQaInvoice(int clientId, string token)
        {
            using (SqlConnection conn = new SqlConnection(DatabaseManager.RequireConfiguredConnectionString()))
            {
                conn.Open();
                using (SqlCommand cmd = new SqlCommand(@"INSERT INTO Invoices (ClientID, InvoiceNumber, InvoiceDate, DueDate, SubTotal, GSTPercent, TaxAmount, TotalAmount, PaidAmount, BalanceDue, PaymentStatus, Notes)
VALUES (@client, @number, GETDATE(), DATEADD(day, 30, GETDATE()), 50, 0, 0, 50, 0, 50, 'Pending', @notes); SELECT CAST(SCOPE_IDENTITY() AS INT);", conn))
                {
                    cmd.Parameters.AddWithValue("@client", clientId);
                    cmd.Parameters.AddWithValue("@number", token + "-INV");
                    cmd.Parameters.AddWithValue("@notes", token);
                    return (int)cmd.ExecuteScalar();
                }
            }
        }

        private static int CreateQaStockItem(string token)
        {
            using (SqlConnection conn = new SqlConnection(DatabaseManager.RequireConfiguredConnectionString()))
            {
                conn.Open();
                using (SqlCommand cmd = new SqlCommand(@"INSERT INTO StockItems (ItemName, Category, CurrentStock, Unit, LastPurchaseRate, ReorderLevel, IsActive)
VALUES (@name, 'QA', 1, 'Nos', 0, 0, 1); SELECT CAST(SCOPE_IDENTITY() AS INT);", conn))
                {
                    cmd.Parameters.AddWithValue("@name", token + " Stock");
                    return (int)cmd.ExecuteScalar();
                }
            }
        }

        private static int CreateQaVendor(string token)
        {
            using (SqlConnection conn = new SqlConnection(DatabaseManager.RequireConfiguredConnectionString()))
            {
                conn.Open();
                using (SqlCommand cmd = new SqlCommand("INSERT INTO Vendors (VendorName, IsActive, IsSupplier, Notes) VALUES (@name, 1, 1, @notes); SELECT CAST(SCOPE_IDENTITY() AS INT);", conn))
                {
                    cmd.Parameters.AddWithValue("@name", token + " Supplier");
                    cmd.Parameters.AddWithValue("@notes", token);
                    return (int)cmd.ExecuteScalar();
                }
            }
        }

        private static int CreateReceivedQaPurchaseOrder(int vendorId, string token)
        {
            using (SqlConnection conn = new SqlConnection(DatabaseManager.RequireConfiguredConnectionString()))
            {
                conn.Open();
                using (SqlCommand cmd = new SqlCommand(@"INSERT INTO PurchaseOrders (VendorID, PONumber, PODate, PayByDate, TotalAmount, PaidAmount, Status, Notes)
VALUES (@vendor, @number, GETDATE(), GETDATE(), 0, 0, 'Fully Received', @notes); SELECT CAST(SCOPE_IDENTITY() AS INT);", conn))
                {
                    cmd.Parameters.AddWithValue("@vendor", vendorId);
                    cmd.Parameters.AddWithValue("@number", token + "-PO");
                    cmd.Parameters.AddWithValue("@notes", token);
                    return (int)cmd.ExecuteScalar();
                }
            }
        }

        private static string GetPurchaseStatus(int poId)
        {
            using (SqlConnection conn = new SqlConnection(DatabaseManager.RequireConfiguredConnectionString()))
            using (SqlCommand cmd = new SqlCommand("SELECT Status FROM PurchaseOrders WHERE POID=@id", conn))
            {
                conn.Open(); cmd.Parameters.AddWithValue("@id", poId); return Convert.ToString(cmd.ExecuteScalar());
            }
        }

        private static void Cleanup(string token, int invoiceId, int stockItemId, int purchaseOrderId, int vendorId)
        {
            try
            {
                using (SqlConnection conn = new SqlConnection(DatabaseManager.RequireConfiguredConnectionString()))
                {
                    conn.Open();
                    Execute(conn, "DELETE FROM Payments WHERE ReferenceNumber LIKE @token OR InvoiceID=@invoiceId", token + "%", invoiceId);
                    Execute(conn, "DELETE FROM InvoiceLineItems WHERE InvoiceID=@invoiceId", token, invoiceId);
                    Execute(conn, "DELETE FROM Invoices WHERE InvoiceID=@invoiceId", token, invoiceId);
                    Execute(conn, "DELETE FROM StockMovements WHERE ReferenceNo LIKE @token", token + "%", 0);
                    Execute(conn, "DELETE FROM StockItems WHERE ItemID=@stockItemId", token, stockItemId);
                    Execute(conn, "DELETE FROM PurchaseLineItems WHERE POID=@purchaseOrderId", token, purchaseOrderId);
                    Execute(conn, "DELETE FROM PurchaseOrders WHERE POID=@purchaseOrderId", token, purchaseOrderId);
                    Execute(conn, "DELETE FROM Vendors WHERE VendorID=@vendorId", token, vendorId);
                    Execute(conn, "DELETE FROM B2BClients WHERE Notes=@token", token, 0);
                }
            }
            catch (Exception ex) { AppLogger.LogError("GuardrailEndToEndTests.Cleanup", ex); }
        }

        private static void Execute(SqlConnection conn, string sql, string token, int id)
        {
            using (SqlCommand cmd = new SqlCommand(sql, conn)) { cmd.Parameters.AddWithValue("@token", token ?? string.Empty); cmd.Parameters.AddWithValue("@invoiceId", id); cmd.Parameters.AddWithValue("@stockItemId", id); cmd.Parameters.AddWithValue("@purchaseOrderId", id); cmd.Parameters.AddWithValue("@vendorId", id); cmd.ExecuteNonQuery(); }
        }
        private static void AssertExists(SqlConnection conn, string sql, string value) { using (SqlCommand cmd = new SqlCommand(sql, conn)) { cmd.Parameters.AddWithValue("@name", value); if (cmd.ExecuteScalar() == null) throw new InvalidOperationException("Missing guardrail database object: " + value); } }
        private static void ExpectBlocked(Action action, string name) { try { action(); } catch (InvalidOperationException) { return; } throw new InvalidOperationException("Guardrail did not block " + name + "."); }
    }
}
