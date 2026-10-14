using System;
using System.Collections.Generic;
using System.Data;
using System.Data.SqlClient;
using System.Linq;
using Dapper;
using HVAC_Pro_Desktop.Models;
using HVAC_Pro_Desktop.Services;

namespace HVAC_Pro_Desktop.DAL
{
    public class InvoiceRepository
    {
        private readonly DatabaseManager _db = new DatabaseManager();
        private const int DefaultRecentRows = 1000;
        private const string InvoiceSelect = @"
            SELECT i.*, c.CompanyName AS ClientName, s.SiteName
            FROM dbo.FinancialInvoices i
            LEFT JOIN B2BClients c ON i.ClientID = c.ClientID
            LEFT JOIN ClientSites s ON i.SiteID = s.SiteID";

        // ── READ (with client name join) ─────────────────────
        public List<Invoice> GetAll()
        {
            return GetRecent(DefaultRecentRows);
        }

        public List<Invoice> GetRevenueSource()
        {
            using (var conn = _db.GetConnection())
            {
                conn.Open();
                return conn.Query<Invoice>(InvoiceSelect + " ORDER BY i.InvoiceDate DESC, i.InvoiceID DESC").ToList();
            }
        }

        public List<Invoice> GetRecent(int maxRows)
        {
            var list = new List<Invoice>();
            using (var conn = _db.GetConnection())
            {
                conn.Open();
                const string sql = @"
            SELECT TOP (@maxRows) i.*, c.CompanyName AS ClientName, s.SiteName
            FROM dbo.ActiveInvoices i
            LEFT JOIN B2BClients c ON i.ClientID = c.ClientID
            LEFT JOIN ClientSites s ON i.SiteID = s.SiteID
            ORDER BY i.InvoiceDate DESC, i.InvoiceID DESC";
                using (var cmd = new SqlCommand(sql, conn))
                {
                    cmd.Parameters.AddWithValue("@maxRows", Math.Max(1, maxRows));
                    using (var r = cmd.ExecuteReader())
                        while (r.Read()) list.Add(Map(r));
                }
            }
            return list;
        }

        public decimal GetPendingBalanceTotal()
        {
            using (var conn = _db.GetConnection())
            {
                conn.Open();
                using (var cmd = new SqlCommand(@"
                    SELECT ISNULL(SUM(BalanceDue), 0)
                    FROM dbo.FinancialInvoices
                    WHERE PaymentStatus IN ('Pending','Overdue','Partial','Draft')", conn))
                {
                    return Convert.ToDecimal(cmd.ExecuteScalar());
                }
            }
        }

        public bool InvoiceNumberExists(string invoiceNumber, int excludeInvoiceId)
        {
            using (var conn = _db.GetConnection())
            {
                conn.Open();
                using (var cmd = new SqlCommand(@"
                    SELECT TOP 1 1
                    FROM dbo.Invoices
                    WHERE InvoiceID <> @excludeInvoiceId
                      AND InvoiceNumber = @invoiceNumber", conn))
                {
                    cmd.Parameters.AddWithValue("@excludeInvoiceId", excludeInvoiceId);
                    cmd.Parameters.AddWithValue("@invoiceNumber", invoiceNumber ?? string.Empty);
                    return cmd.ExecuteScalar() != null;
                }
            }
        }

        public Invoice GetById(int id)
        {
            using (var conn = _db.GetConnection())
            {
                conn.Open();
                string sql = InvoiceSelect.Replace("dbo.FinancialInvoices", "dbo.Invoices") + " WHERE i.InvoiceID = @id";
                using (var cmd = new SqlCommand(sql, conn))
                {
                    cmd.Parameters.AddWithValue("@id", id);
                    using (var r = cmd.ExecuteReader())
                        if (r.Read()) return Map(r);
                }
            }
            return null;
        }

        public List<Invoice> GetByClientId(int clientId)
        {
            var list = new List<Invoice>();
            using (var conn = _db.GetConnection())
            {
                conn.Open();
                const string sql = InvoiceSelect + " WHERE i.ClientID = @id ORDER BY i.InvoiceDate DESC, i.InvoiceID DESC";
                using (var cmd = new SqlCommand(sql, conn))
                {
                    cmd.Parameters.AddWithValue("@id", clientId);
                    using (var r = cmd.ExecuteReader())
                        while (r.Read()) list.Add(Map(r));
                }
            }
            return list;
        }

        public List<Invoice> GetByContractId(int contractId)
        {
            var list = new List<Invoice>();
            using (var conn = _db.GetConnection())
            {
                conn.Open();
                using (var cmd = new SqlCommand(
                    InvoiceSelect + " WHERE i.ContractID=@id ORDER BY i.InvoiceDate DESC, i.InvoiceID DESC", conn))
                {
                    cmd.Parameters.AddWithValue("@id", contractId);
                    using (var r = cmd.ExecuteReader())
                        while (r.Read()) list.Add(Map(r));
                }
            }
            return list;
        }

        public List<Invoice> GetPendingInvoices()
        {
            var list = new List<Invoice>();
            using (var conn = _db.GetConnection())
            {
                conn.Open();
                const string sql = InvoiceSelect + @"
                    WHERE i.PaymentStatus IN ('Pending','Overdue','Partial','Draft')
                    ORDER BY i.DueDate ASC, i.InvoiceID DESC";
                using (var cmd = new SqlCommand(sql, conn))
                using (var r = cmd.ExecuteReader())
                    while (r.Read()) list.Add(Map(r));
            }
            return list;
        }

        public List<Invoice> GetOverdueInvoices()
        {
            var list = new List<Invoice>();
            using (var conn = _db.GetConnection())
            {
                conn.Open();
                const string sql = InvoiceSelect + @"
                    WHERE i.PaymentStatus IN ('Pending','Partial','Overdue')
                    AND i.DueDate < GETDATE()
                    ORDER BY i.DueDate ASC, i.InvoiceID DESC";
                using (var cmd = new SqlCommand(sql, conn))
                using (var r = cmd.ExecuteReader())
                    while (r.Read()) list.Add(Map(r));
            }
            return list;
        }

        // ── LINE ITEMS ───────────────────────────────────────
        public List<InvoiceLineItem> GetLineItems(int invoiceId)
        {
            var list = new List<InvoiceLineItem>();
            using (var conn = _db.GetConnection())
            {
                conn.Open();
                using (var cmd = new SqlCommand(
                    "SELECT * FROM InvoiceLineItems WHERE InvoiceID = @id ORDER BY LineItemID", conn))
                {
                    cmd.Parameters.AddWithValue("@id", invoiceId);
                    using (var r = cmd.ExecuteReader())
                        while (r.Read())
                            list.Add(new InvoiceLineItem
                            {
                                JobID = HasColumn(r, "JobID") && r["JobID"] != DBNull.Value ? (int?)Convert.ToInt32(r["JobID"]) : null,
                                LineItemID  = (int)r["LineItemID"],
                                InvoiceID   = (int)r["InvoiceID"],
                                StockItemID = r["StockItemID"] != DBNull.Value ? (int?)r["StockItemID"] : null,
                                Description = r["Description"].ToString(),
                                HSNCode     = r["HSNCode"] == DBNull.Value ? "" : r["HSNCode"].ToString(),
                                Category    = HasColumn(r, "Category") && r["Category"] != DBNull.Value ? r["Category"].ToString() : "Service",
                                Unit        = r["Unit"] == DBNull.Value ? "Nos" : r["Unit"].ToString(),
                                Quantity    = (decimal)r["Quantity"],
                                Rate        = (decimal)r["Rate"],
                                DiscountPercent = HasColumn(r, "DiscountPercent") && r["DiscountPercent"] != DBNull.Value ? (decimal)r["DiscountPercent"] : 0m,
                                GSTPercent  = r["GSTPercent"] != DBNull.Value ? (decimal)r["GSTPercent"] : 18m,
                                TaxType     = HasColumn(r, "TaxType") && r["TaxType"] != DBNull.Value ? r["TaxType"].ToString() : "Taxable",
                                TaxAmount   = r["TaxAmount"] != DBNull.Value ? (decimal)r["TaxAmount"] : 0m,
                                IsStockItem = r["IsStockItem"] != DBNull.Value && (bool)r["IsStockItem"],
                                IsBillable  = r["IsBillable"] == DBNull.Value || (bool)r["IsBillable"],
                                CoverageNote = r["CoverageNote"] == DBNull.Value ? "" : r["CoverageNote"].ToString(),
                                Amount      = (decimal)r["Amount"]
                            });
                }
            }
            return list;
        }

        public void SaveLineItems(int invoiceId, List<InvoiceLineItem> items)
        {
            using (var conn = _db.GetConnection())
            {
                conn.Open();
                using (var tx = conn.BeginTransaction())
                {
                    FinancialRecordProtection.EnsureDraft(conn, tx, invoiceId);
                    ReplaceLineItems(conn, tx, invoiceId, items);
                    tx.Commit();
                }
            }
        }

        private static void ReplaceLineItems(SqlConnection conn, SqlTransaction tx, int invoiceId, List<InvoiceLineItem> items)
        {
            conn.Execute("DELETE FROM InvoiceLineItems WHERE InvoiceID = @id", new { id = invoiceId }, tx);

            foreach (var item in CleanInvoiceLineItems(items))
            {
                conn.Execute(@"
                    INSERT INTO InvoiceLineItems
                        (InvoiceID,JobID,StockItemID,Description,HSNCode,Category,Unit,Quantity,Rate,DiscountPercent,GSTPercent,TaxType,TaxAmount,IsStockItem,IsBillable,CoverageNote,Amount)
                    VALUES
                        (@inv,@jobId,@stockItemId,@desc,@hsn,@category,@unit,@qty,@rate,@discount,@gst,@taxType,@tax,@isStockItem,@isBillable,@coverageNote,@amt)",
                    new
                {
                    inv = invoiceId,
                    jobId = item.JobID,
                    stockItemId = item.StockItemID,
                    desc = item.Description ?? "",
                    hsn = string.IsNullOrWhiteSpace(item.HSNCode) ? null : item.HSNCode.Trim(),
                    category = string.IsNullOrWhiteSpace(item.Category) ? "Service" : item.Category.Trim(),
                    unit = string.IsNullOrWhiteSpace(item.Unit) ? "Nos" : item.Unit.Trim(),
                    qty = item.Quantity,
                    rate = item.Rate,
                    discount = item.DiscountPercent,
                    gst = item.GSTPercent,
                    taxType = string.IsNullOrWhiteSpace(item.TaxType) ? "Taxable" : item.TaxType.Trim(),
                    tax = item.TaxAmount,
                    isStockItem = item.IsStockItem,
                    isBillable = item.IsBillable,
                    coverageNote = string.IsNullOrWhiteSpace(item.CoverageNote) ? null : item.CoverageNote.Trim(),
                    amt = item.Amount
                }, tx);
            }
        }

        public void UpdateWithLineItems(Invoice inv)
        {
            using (var conn = _db.GetConnection())
            {
                conn.Open();
                using (var tx = conn.BeginTransaction())
                {
                    FinancialRecordProtection.EnsureDraft(conn, tx, inv.InvoiceID);
                    ReplaceLineItems(conn, tx, inv.InvoiceID, inv.LineItems ?? new List<InvoiceLineItem>());
                    ExecuteInvoiceUpdate(conn, tx, inv);
                    tx.Commit();
                }
            }
        }

        public int Create(Invoice inv)
        {
            using (var conn = _db.GetConnection())
            {
                conn.Open();
                using (SqlTransaction tx = conn.BeginTransaction())
                {
                    try
                    {
                        const string sql = @"
                            INSERT INTO Invoices
                                (ContractID,ClientID,SiteID,QuotationBidID,InvoiceNumber,InvoiceDate,DueDate,
                                 SubTotal,GSTPercent,TaxAmount,TotalAmount,PaidAmount,
                                 BalanceDue,PaymentStatus,Notes,InvoiceTitle,Subject,PONumber,PODate,SendInvoiceTo,CertificationNote,
                                 TemplateCode,WorkflowType,GSTMode,PaymentTerms,PlaceOfSupply,RoundOff,CGSTAmount,SGSTAmount,IGSTAmount,
                                 ContractCoverageType,ServiceChecklist,AssetDetails,WarrantyStatus,WarrantyExpiry,PreventiveVisitDate,NextServiceDueDate,InventoryReservationStatus,
                                 CreatedByUserId,CreatedByName)
                            VALUES
                                (@cid,@clientId,@siteId,@quotationBidId,@num,@date,@due,
                                 @sub,@gst,@tax,@total,@paid,
                                 @bal,@status,@notes,@title,@subject,@poNumber,@poDate,@sendInvoiceTo,@certificationNote,
                                 @templateCode,@workflowType,@gstMode,@paymentTerms,@placeOfSupply,@roundOff,@cgst,@sgst,@igst,
                                 @coverageType,@checklist,@assetDetails,@warrantyStatus,@warrantyExpiry,@preventiveVisitDate,@nextServiceDueDate,@inventoryReservationStatus,
                                 @createdByUserId,@createdByName);
                            SELECT SCOPE_IDENTITY();";

                        int newId;
                        using (var cmd = new SqlCommand(sql, conn, tx))
                        {
                            cmd.Parameters.AddWithValue("@cid",     inv.ContractID > 0 ? (object)inv.ContractID : DBNull.Value);
                            cmd.Parameters.AddWithValue("@clientId",inv.ClientID);
                            cmd.Parameters.AddWithValue("@siteId",  inv.SiteID > 0 ? (object)inv.SiteID : DBNull.Value);
                            cmd.Parameters.AddWithValue("@quotationBidId", inv.QuotationBidID.HasValue ? (object)inv.QuotationBidID.Value : DBNull.Value);
                            cmd.Parameters.AddWithValue("@num",     inv.InvoiceNumber ?? "");
                            cmd.Parameters.AddWithValue("@date",    inv.InvoiceDate);
                            cmd.Parameters.AddWithValue("@due",     inv.DueDate);
                            cmd.Parameters.AddWithValue("@sub",     inv.SubTotal);
                            cmd.Parameters.AddWithValue("@gst",     inv.GSTPercent);
                            cmd.Parameters.AddWithValue("@tax",     inv.TaxAmount);
                            cmd.Parameters.AddWithValue("@total",   inv.TotalAmount);
                            cmd.Parameters.AddWithValue("@paid",    inv.PaidAmount);
                            cmd.Parameters.AddWithValue("@bal",     inv.BalanceDue);
                            cmd.Parameters.AddWithValue("@status",  inv.PaymentStatus ?? "Draft");
                            cmd.Parameters.AddWithValue("@notes",   inv.Notes ?? "");
                            cmd.Parameters.AddWithValue("@title",   inv.InvoiceTitle ?? "TAX INVOICE");
                            cmd.Parameters.AddWithValue("@subject", string.IsNullOrWhiteSpace(inv.Subject) ? (object)DBNull.Value : inv.Subject.Trim());
                            cmd.Parameters.AddWithValue("@poNumber", string.IsNullOrWhiteSpace(inv.PONumber) ? (object)DBNull.Value : inv.PONumber.Trim());
                            cmd.Parameters.AddWithValue("@poDate", inv.PODate.HasValue ? (object)inv.PODate.Value : DBNull.Value);
                            cmd.Parameters.AddWithValue("@sendInvoiceTo", string.IsNullOrWhiteSpace(inv.SendInvoiceTo) ? (object)DBNull.Value : inv.SendInvoiceTo.Trim());
                            cmd.Parameters.AddWithValue("@certificationNote", string.IsNullOrWhiteSpace(inv.CertificationNote) ? (object)DBNull.Value : inv.CertificationNote.Trim());
                            cmd.Parameters.AddWithValue("@templateCode", string.IsNullOrWhiteSpace(inv.TemplateCode) ? (object)DBNull.Value : inv.TemplateCode.Trim());
                            cmd.Parameters.AddWithValue("@workflowType", string.IsNullOrWhiteSpace(inv.WorkflowType) ? (object)DBNull.Value : inv.WorkflowType.Trim());
                            cmd.Parameters.AddWithValue("@gstMode", string.IsNullOrWhiteSpace(inv.GSTMode) ? "IGST" : inv.GSTMode.Trim());
                            cmd.Parameters.AddWithValue("@paymentTerms", string.IsNullOrWhiteSpace(inv.PaymentTerms) ? (object)DBNull.Value : inv.PaymentTerms.Trim());
                            cmd.Parameters.AddWithValue("@placeOfSupply", string.IsNullOrWhiteSpace(inv.PlaceOfSupply) ? (object)DBNull.Value : inv.PlaceOfSupply.Trim());
                            cmd.Parameters.AddWithValue("@roundOff", inv.RoundOff);
                            cmd.Parameters.AddWithValue("@cgst", inv.CGSTAmount);
                            cmd.Parameters.AddWithValue("@sgst", inv.SGSTAmount);
                            cmd.Parameters.AddWithValue("@igst", inv.IGSTAmount);
                            cmd.Parameters.AddWithValue("@coverageType", string.IsNullOrWhiteSpace(inv.ContractCoverageType) ? (object)DBNull.Value : inv.ContractCoverageType.Trim());
                            cmd.Parameters.AddWithValue("@checklist", string.IsNullOrWhiteSpace(inv.ServiceChecklist) ? (object)DBNull.Value : inv.ServiceChecklist.Trim());
                            cmd.Parameters.AddWithValue("@assetDetails", string.IsNullOrWhiteSpace(inv.AssetDetails) ? (object)DBNull.Value : inv.AssetDetails.Trim());
                            cmd.Parameters.AddWithValue("@warrantyStatus", string.IsNullOrWhiteSpace(inv.WarrantyStatus) ? (object)DBNull.Value : inv.WarrantyStatus.Trim());
                            cmd.Parameters.AddWithValue("@warrantyExpiry", inv.WarrantyExpiry.HasValue ? (object)inv.WarrantyExpiry.Value : DBNull.Value);
                            cmd.Parameters.AddWithValue("@preventiveVisitDate", inv.PreventiveVisitDate.HasValue ? (object)inv.PreventiveVisitDate.Value : DBNull.Value);
                            cmd.Parameters.AddWithValue("@nextServiceDueDate", inv.NextServiceDueDate.HasValue ? (object)inv.NextServiceDueDate.Value : DBNull.Value);
                            cmd.Parameters.AddWithValue("@inventoryReservationStatus", string.IsNullOrWhiteSpace(inv.InventoryReservationStatus) ? "None" : inv.InventoryReservationStatus.Trim());
                            cmd.Parameters.AddWithValue("@createdByUserId", inv.CreatedByUserId.HasValue ? (object)inv.CreatedByUserId.Value : DBNull.Value);
                            cmd.Parameters.AddWithValue("@createdByName", string.IsNullOrWhiteSpace(inv.CreatedByName) ? (object)DBNull.Value : inv.CreatedByName);
                            newId = Convert.ToInt32(cmd.ExecuteScalar());
                        }

                        // Save line items
                        List<InvoiceLineItem> lineItems = CleanInvoiceLineItems(inv.LineItems).ToList();
                        if (lineItems.Count > 0)
                        {
                            foreach (var item in lineItems)
                            {
                                conn.Execute(@"
                                    INSERT INTO InvoiceLineItems
                                        (InvoiceID,JobID,StockItemID,Description,HSNCode,Category,Unit,Quantity,Rate,DiscountPercent,GSTPercent,TaxType,TaxAmount,IsStockItem,IsBillable,CoverageNote,Amount)
                                    VALUES
                                        (@inv,@jobId,@stockItemId,@desc,@hsn,@category,@unit,@qty,@rate,@discount,@gst,@taxType,@tax,@isStockItem,@isBillable,@coverageNote,@amt)",
                                    new
                                {
                                    inv = newId,
                                    jobId = item.JobID,
                                    stockItemId = item.StockItemID,
                                    desc = item.Description ?? "",
                                    hsn = string.IsNullOrWhiteSpace(item.HSNCode) ? null : item.HSNCode.Trim(),
                                    category = string.IsNullOrWhiteSpace(item.Category) ? "Service" : item.Category.Trim(),
                                    unit = string.IsNullOrWhiteSpace(item.Unit) ? "Nos" : item.Unit.Trim(),
                                    qty = item.Quantity,
                                    rate = item.Rate,
                                    discount = item.DiscountPercent,
                                    gst = item.GSTPercent,
                                    taxType = string.IsNullOrWhiteSpace(item.TaxType) ? "Taxable" : item.TaxType.Trim(),
                                    tax = item.TaxAmount,
                                    isStockItem = item.IsStockItem,
                                    isBillable = item.IsBillable,
                                    coverageNote = string.IsNullOrWhiteSpace(item.CoverageNote) ? null : item.CoverageNote.Trim(),
                                    amt = item.Amount
                                }, tx);
                            }
                        }

                        conn.Execute("UPDATE dbo.Invoices SET IsPosted=CASE WHEN PaymentStatus='Draft' THEN 0 ELSE 1 END,PostedUtc=CASE WHEN PaymentStatus='Draft' THEN NULL ELSE SYSUTCDATETIME() END WHERE InvoiceID=@Id", new { Id = newId }, tx);
                        tx.Commit();
                        return newId;
                    }
                    catch { tx.Rollback(); throw; }
                }
            }
        }

        public void Update(Invoice inv)
        {
            using (var conn = _db.GetConnection())
            {
                conn.Open();
                using (var tx = conn.BeginTransaction())
                {
                    ExecuteInvoiceUpdate(conn, tx, inv);
                    tx.Commit();
                }
            }
        }

        internal static void ExecuteInvoiceUpdate(SqlConnection conn, SqlTransaction tx, Invoice inv)
        {
                const string sql = @"
                    UPDATE Invoices SET
                        ContractID = @cid,
                        ClientID = @clientId,
                        SiteID = @siteId,
                        QuotationBidID = @quotationBidId,
                        InvoiceNumber = @num,
                        InvoiceDate = @date,
                        DueDate = @due,
                        SubTotal = @sub,
                        GSTPercent = @gst,
                        TaxAmount = @tax,
                        TotalAmount = @total,
                        PaidAmount = @paid,
                        BalanceDue = @bal,
                        PaymentStatus = @status,
                        Notes = @notes,
                        InvoiceTitle = @title,
                        Subject = @subject,
                        PONumber = @poNumber,
                        PODate = @poDate,
                        SendInvoiceTo = @sendInvoiceTo,
                        CertificationNote = @certificationNote,
                        TemplateCode = @templateCode,
                        WorkflowType = @workflowType,
                        GSTMode = @gstMode,
                        PaymentTerms = @paymentTerms,
                        PlaceOfSupply = @placeOfSupply,
                        RoundOff = @roundOff,
                        CGSTAmount = @cgst,
                        SGSTAmount = @sgst,
                        IGSTAmount = @igst,
                        ContractCoverageType = @coverageType,
                        ServiceChecklist = @checklist,
                        AssetDetails = @assetDetails,
                        WarrantyStatus = @warrantyStatus,
                        WarrantyExpiry = @warrantyExpiry,
                        PreventiveVisitDate = @preventiveVisitDate,
                        NextServiceDueDate = @nextServiceDueDate,
                        InventoryReservationStatus = @inventoryReservationStatus,
                        ModifiedByUserId = @modifiedByUserId,
                        ModifiedByName = @modifiedByName,
                        ModifiedDate = @modifiedDate,
                        IsPosted=CASE WHEN @status='Draft' THEN IsPosted ELSE 1 END,
                        PostedUtc=CASE WHEN IsPosted=0 AND @status<>'Draft' THEN SYSUTCDATETIME() ELSE PostedUtc END
                    WHERE InvoiceID = @id";

            var parameters = new DynamicParameters();
                    parameters.Add("@id", inv.InvoiceID);
                    parameters.Add("@cid", inv.ContractID > 0 ? (object)inv.ContractID : null);
                    parameters.Add("@clientId", inv.ClientID > 0 ? (object)inv.ClientID : null);
                    parameters.Add("@siteId", inv.SiteID > 0 ? (object)inv.SiteID : null);
                    parameters.Add("@quotationBidId", inv.QuotationBidID.HasValue ? (object)inv.QuotationBidID.Value : null);
                    parameters.Add("@num", inv.InvoiceNumber ?? string.Empty);
                    parameters.Add("@date", inv.InvoiceDate);
                    parameters.Add("@due", inv.DueDate);
                    parameters.Add("@sub", inv.SubTotal);
                    parameters.Add("@gst", inv.GSTPercent);
                    parameters.Add("@tax", inv.TaxAmount);
                    parameters.Add("@total", inv.TotalAmount);
                    parameters.Add("@paid", inv.PaidAmount);
                    parameters.Add("@bal", inv.BalanceDue);
                    parameters.Add("@status", inv.PaymentStatus ?? "Draft");
                    parameters.Add("@notes", string.IsNullOrWhiteSpace(inv.Notes) ? (object)null : inv.Notes.Trim());
                    parameters.Add("@title", string.IsNullOrWhiteSpace(inv.InvoiceTitle) ? "TAX INVOICE" : inv.InvoiceTitle.Trim());
                    parameters.Add("@subject", string.IsNullOrWhiteSpace(inv.Subject) ? (object)null : inv.Subject.Trim());
                    parameters.Add("@poNumber", string.IsNullOrWhiteSpace(inv.PONumber) ? (object)null : inv.PONumber.Trim());
                    parameters.Add("@poDate", inv.PODate.HasValue ? (object)inv.PODate.Value : null);
                    parameters.Add("@sendInvoiceTo", string.IsNullOrWhiteSpace(inv.SendInvoiceTo) ? (object)null : inv.SendInvoiceTo.Trim());
                    parameters.Add("@certificationNote", string.IsNullOrWhiteSpace(inv.CertificationNote) ? (object)null : inv.CertificationNote.Trim());
                    parameters.Add("@templateCode", string.IsNullOrWhiteSpace(inv.TemplateCode) ? (object)null : inv.TemplateCode.Trim());
                    parameters.Add("@workflowType", string.IsNullOrWhiteSpace(inv.WorkflowType) ? (object)null : inv.WorkflowType.Trim());
                    parameters.Add("@gstMode", string.IsNullOrWhiteSpace(inv.GSTMode) ? "IGST" : inv.GSTMode.Trim());
                    parameters.Add("@paymentTerms", string.IsNullOrWhiteSpace(inv.PaymentTerms) ? (object)null : inv.PaymentTerms.Trim());
                    parameters.Add("@placeOfSupply", string.IsNullOrWhiteSpace(inv.PlaceOfSupply) ? (object)null : inv.PlaceOfSupply.Trim());
                    parameters.Add("@roundOff", inv.RoundOff);
                    parameters.Add("@cgst", inv.CGSTAmount);
                    parameters.Add("@sgst", inv.SGSTAmount);
                    parameters.Add("@igst", inv.IGSTAmount);
                    parameters.Add("@coverageType", string.IsNullOrWhiteSpace(inv.ContractCoverageType) ? (object)null : inv.ContractCoverageType.Trim());
                    parameters.Add("@checklist", string.IsNullOrWhiteSpace(inv.ServiceChecklist) ? (object)null : inv.ServiceChecklist.Trim());
                    parameters.Add("@assetDetails", string.IsNullOrWhiteSpace(inv.AssetDetails) ? (object)null : inv.AssetDetails.Trim());
                    parameters.Add("@warrantyStatus", string.IsNullOrWhiteSpace(inv.WarrantyStatus) ? (object)null : inv.WarrantyStatus.Trim());
                    parameters.Add("@warrantyExpiry", inv.WarrantyExpiry.HasValue ? (object)inv.WarrantyExpiry.Value : null);
                    parameters.Add("@preventiveVisitDate", inv.PreventiveVisitDate.HasValue ? (object)inv.PreventiveVisitDate.Value : null);
                    parameters.Add("@nextServiceDueDate", inv.NextServiceDueDate.HasValue ? (object)inv.NextServiceDueDate.Value : null);
                    parameters.Add("@inventoryReservationStatus", string.IsNullOrWhiteSpace(inv.InventoryReservationStatus) ? "None" : inv.InventoryReservationStatus.Trim());
                    parameters.Add("@modifiedByUserId", inv.ModifiedByUserId.HasValue ? (object)inv.ModifiedByUserId.Value : null);
                    parameters.Add("@modifiedByName", string.IsNullOrWhiteSpace(inv.ModifiedByName) ? (object)null : inv.ModifiedByName);
                    parameters.Add("@modifiedDate", inv.ModifiedDate.HasValue ? (object)inv.ModifiedDate.Value : null);
            conn.Execute(sql, parameters, tx);
        }

        public void UpdatePaymentStatus(int invoiceId, decimal paidAmount, string status)
        {
            using (var conn = _db.GetConnection())
            {
                conn.Open();
                const string sql = @"
                    UPDATE Invoices SET
                        PaidAmount    = @paid,
                        BalanceDue    = TotalAmount - @paid,
                        PaymentStatus = @status,
                        IsPosted = CASE WHEN @status = 'Draft' THEN IsPosted ELSE 1 END,
                        PostedUtc = CASE WHEN IsPosted=0 AND @status<>'Draft' THEN SYSUTCDATETIME() ELSE PostedUtc END,
                        PaymentDate   = CASE WHEN @status = 'Paid' THEN GETDATE() ELSE PaymentDate END
                    WHERE InvoiceID = @id";
                conn.Execute(sql, new { paid = paidAmount, status, id = invoiceId });
            }
        }

        // ── INVOICE NUMBER GENERATION ────────────────────────
        public void Delete(int invoiceId)
        {
            FinancialRecordProtection.ArchiveInvoice(invoiceId);
        }

        public string GenerateInvoiceNumber()
        {
            using (var conn = _db.GetConnection())
            {
                conn.Open();
                string prefix = "INV-" + DateTime.Now.ToString("yyyy-MM");
                using (var cmd = new SqlCommand(
                    "SELECT COUNT(*) FROM dbo.Invoices WHERE InvoiceNumber LIKE @p", conn))
                {
                    cmd.Parameters.AddWithValue("@p", prefix + "-%");
                    int count = (int)cmd.ExecuteScalar();
                    return $"{prefix}-{(count + 1):D5}";
                }
            }
        }

        // ── AGING REPORT ─────────────────────────────────────
        public DataTable GetAgingReport()
        {
            DataTable dt = new DataTable();
            dt.Columns.Add("Age Bucket");
            dt.Columns.Add("Count",  typeof(int));
            dt.Columns.Add("Amount", typeof(decimal));

            using (var conn = _db.GetConnection())
            {
                conn.Open();
                const string sql = @"
                    SELECT
                        CASE
                            WHEN DATEDIFF(day,DueDate,GETDATE()) BETWEEN 0  AND 30 THEN '0-30 days'
                            WHEN DATEDIFF(day,DueDate,GETDATE()) BETWEEN 31 AND 60 THEN '31-60 days'
                            WHEN DATEDIFF(day,DueDate,GETDATE()) BETWEEN 61 AND 90 THEN '61-90 days'
                            ELSE '90+ days'
                        END AS Bucket,
                        COUNT(*) AS Cnt,
                        SUM(BalanceDue) AS Amt
                    FROM dbo.FinancialInvoices
                    WHERE PaymentStatus IN ('Pending','Overdue','Partial')
                    AND DueDate < GETDATE()
                    GROUP BY
                        CASE
                            WHEN DATEDIFF(day,DueDate,GETDATE()) BETWEEN 0  AND 30 THEN '0-30 days'
                            WHEN DATEDIFF(day,DueDate,GETDATE()) BETWEEN 31 AND 60 THEN '31-60 days'
                            WHEN DATEDIFF(day,DueDate,GETDATE()) BETWEEN 61 AND 90 THEN '61-90 days'
                            ELSE '90+ days'
                        END
                    ORDER BY MIN(DueDate)";
                using (var cmd = new SqlCommand(sql, conn))
                using (var r = cmd.ExecuteReader())
                    while (r.Read())
                        dt.Rows.Add(r["Bucket"].ToString(), (int)r["Cnt"], (decimal)r["Amt"]);
            }
            return dt;
        }

        // ── HELPERS ──────────────────────────────────────────
        private Invoice Map(SqlDataReader r)
        {
            var inv = new Invoice
            {
                InvoiceID     = ToInt(r["InvoiceID"]),
                ContractID    = r["ContractID"] != DBNull.Value ? ToInt(r["ContractID"]) : 0,
                InvoiceNumber = r["InvoiceNumber"].ToString(),
                InvoiceDate   = ToDate(r["InvoiceDate"]),
                DueDate       = ToDate(r["DueDate"]),
                SubTotal      = ToDecimal(r["SubTotal"]),
                TaxAmount     = ToDecimal(r["TaxAmount"]),
                TotalAmount   = ToDecimal(r["TotalAmount"]),
                PaidAmount    = ToDecimal(r["PaidAmount"]),
                PaymentStatus = r["PaymentStatus"].ToString(),
                PaymentDate   = r["PaymentDate"] != DBNull.Value ? (DateTime?)ToDate(r["PaymentDate"]) : null
            };
            inv.ClientID = GetInt32(r, "ClientID", 0);
            inv.SiteID = GetInt32(r, "SiteID", 0);
            inv.QuotationBidID = GetNullableInt32(r, "QuotationBidID");
            inv.GSTPercent = GetDecimal(r, "GSTPercent", 18m);
            inv.BalanceDue = GetDecimal(r, "BalanceDue", inv.TotalAmount - inv.PaidAmount);
            inv.Notes = GetString(r, "Notes");
            inv.InvoiceTitle = GetString(r, "InvoiceTitle");
            inv.Subject = GetString(r, "Subject");
            inv.PONumber = GetString(r, "PONumber");
            inv.PODate = GetNullableDate(r, "PODate");
            inv.SendInvoiceTo = GetString(r, "SendInvoiceTo");
            inv.CertificationNote = GetString(r, "CertificationNote");
            inv.TemplateCode = GetString(r, "TemplateCode");
            inv.WorkflowType = GetString(r, "WorkflowType");
            inv.GSTMode = GetString(r, "GSTMode", "IGST");
            inv.PaymentTerms = GetString(r, "PaymentTerms");
            inv.PlaceOfSupply = GetString(r, "PlaceOfSupply");
            inv.RoundOff = GetDecimal(r, "RoundOff", 0m);
            inv.CGSTAmount = GetDecimal(r, "CGSTAmount", 0m);
            inv.SGSTAmount = GetDecimal(r, "SGSTAmount", 0m);
            inv.IGSTAmount = GetDecimal(r, "IGSTAmount", 0m);
            inv.ContractCoverageType = GetString(r, "ContractCoverageType");
            inv.ServiceChecklist = GetString(r, "ServiceChecklist");
            inv.AssetDetails = GetString(r, "AssetDetails");
            inv.WarrantyStatus = GetString(r, "WarrantyStatus");
            inv.WarrantyExpiry = GetNullableDate(r, "WarrantyExpiry");
            inv.PreventiveVisitDate = GetNullableDate(r, "PreventiveVisitDate");
            inv.NextServiceDueDate = GetNullableDate(r, "NextServiceDueDate");
            inv.InventoryReservationStatus = GetString(r, "InventoryReservationStatus");
            inv.CreatedByUserId = GetNullableInt32(r, "CreatedByUserId");
            inv.CreatedByName = GetNullableString(r, "CreatedByName");
            inv.ModifiedByUserId = GetNullableInt32(r, "ModifiedByUserId");
            inv.ModifiedByName = GetNullableString(r, "ModifiedByName");
            inv.ModifiedDate = GetNullableDate(r, "ModifiedDate");
            inv.IsPosted = HasColumn(r, "IsPosted") && r["IsPosted"] != DBNull.Value && Convert.ToBoolean(r["IsPosted"]);
            inv.IsDeleted = HasColumn(r, "IsDeleted") && r["IsDeleted"] != DBNull.Value && Convert.ToBoolean(r["IsDeleted"]);
            inv.ClientName = GetString(r, "ClientName");
            inv.SiteName = GetString(r, "SiteName");
            return inv;
        }

        private static bool HasColumn(IDataRecord record, string columnName)
        {
            for (int index = 0; index < record.FieldCount; index++)
            {
                if (string.Equals(record.GetName(index), columnName, StringComparison.OrdinalIgnoreCase))
                    return true;
            }

            return false;
        }

        private static string GetString(IDataRecord record, string columnName, string defaultValue = "")
        {
            if (!HasColumn(record, columnName))
                return defaultValue;

            object value = record[columnName];
            return value == DBNull.Value ? defaultValue : Convert.ToString(value) ?? defaultValue;
        }

        private static string GetNullableString(IDataRecord record, string columnName)
        {
            if (!HasColumn(record, columnName))
                return null;

            object value = record[columnName];
            return value == DBNull.Value ? null : Convert.ToString(value);
        }

        private static int GetInt32(IDataRecord record, string columnName, int defaultValue)
        {
            if (!HasColumn(record, columnName))
                return defaultValue;

            object value = record[columnName];
            return value == DBNull.Value ? defaultValue : Convert.ToInt32(value);
        }

        private static int? GetNullableInt32(IDataRecord record, string columnName)
        {
            if (!HasColumn(record, columnName))
                return null;

            object value = record[columnName];
            return value == DBNull.Value ? (int?)null : Convert.ToInt32(value);
        }

        private static decimal GetDecimal(IDataRecord record, string columnName, decimal defaultValue)
        {
            if (!HasColumn(record, columnName))
                return defaultValue;

            object value = record[columnName];
            return value == DBNull.Value ? defaultValue : Convert.ToDecimal(value);
        }

        private static DateTime? GetNullableDate(IDataRecord record, string columnName)
        {
            if (!HasColumn(record, columnName))
                return null;

            object value = record[columnName];
            return value == DBNull.Value ? (DateTime?)null : ToDate(value);
        }

        private static int ToInt(object value)
        {
            return value == null || value == DBNull.Value ? 0 : Convert.ToInt32(value);
        }

        private static decimal ToDecimal(object value)
        {
            return value == null || value == DBNull.Value ? 0m : Convert.ToDecimal(value);
        }

        private static DateTime ToDate(object value)
        {
            return value == null || value == DBNull.Value ? DateTime.Today : Convert.ToDateTime(value);
        }

        private static bool HasColumn(SqlDataReader reader, string columnName)
        {
            for (int i = 0; i < reader.FieldCount; i++)
                if (string.Equals(reader.GetName(i), columnName, StringComparison.OrdinalIgnoreCase))
                    return true;
            return false;
        }

        private static IEnumerable<InvoiceLineItem> CleanInvoiceLineItems(IEnumerable<InvoiceLineItem> items)
        {
            return (items ?? Enumerable.Empty<InvoiceLineItem>())
                .Where(item => item != null)
                .Where(item =>
                    !string.IsNullOrWhiteSpace(item.Description)
                    || item.Rate > 0m
                    || item.Amount > 0m
                    || item.Quantity > 1m);
        }
    }
}
