using System;
using System.Collections.Generic;
using System.Data;
using System.Data.SqlClient;
using HVAC_Pro_Desktop.Models;

namespace HVAC_Pro_Desktop.DAL
{
    public class InvoiceRepository
    {
        private readonly DatabaseManager _db = new DatabaseManager();
        private const string InvoiceSelect = @"
            SELECT i.*, c.CompanyName AS ClientName, s.SiteName
            FROM Invoices i
            LEFT JOIN B2BClients c ON i.ClientID = c.ClientID
            LEFT JOIN ClientSites s ON i.SiteID = s.SiteID";

        // ── READ (with client name join) ─────────────────────
        public List<Invoice> GetAll()
        {
            var list = new List<Invoice>();
            using (var conn = _db.GetConnection())
            {
                conn.Open();
                const string sql = InvoiceSelect + " ORDER BY i.InvoiceDate DESC";
                using (var cmd = new SqlCommand(sql, conn))
                using (var r = cmd.ExecuteReader())
                    while (r.Read()) list.Add(Map(r));
            }
            return list;
        }

        public Invoice GetById(int id)
        {
            using (var conn = _db.GetConnection())
            {
                conn.Open();
                const string sql = InvoiceSelect + " WHERE i.InvoiceID = @id";
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
                const string sql = InvoiceSelect + " WHERE i.ClientID = @id ORDER BY i.InvoiceDate DESC";
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
                    InvoiceSelect + " WHERE i.ContractID=@id ORDER BY i.InvoiceDate DESC", conn))
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
                    ORDER BY i.DueDate ASC";
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
                    ORDER BY i.DueDate ASC";
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
                using (SqlTransaction tx = conn.BeginTransaction())
                {
                    try
                    {
                        // Delete existing
                        using (var del = new SqlCommand(
                            "DELETE FROM InvoiceLineItems WHERE InvoiceID = @id", conn, tx))
                        {
                            del.Parameters.AddWithValue("@id", invoiceId);
                            del.ExecuteNonQuery();
                        }

                        // Re-insert
                        foreach (var item in items)
                        {
                            using (var ins = new SqlCommand(@"
                                INSERT INTO InvoiceLineItems
                                    (InvoiceID,StockItemID,Description,HSNCode,Category,Unit,Quantity,Rate,DiscountPercent,GSTPercent,TaxType,TaxAmount,IsStockItem,IsBillable,CoverageNote,Amount)
                                VALUES
                                    (@inv,@stockItemId,@desc,@hsn,@category,@unit,@qty,@rate,@discount,@gst,@taxType,@tax,@isStockItem,@isBillable,@coverageNote,@amt)", conn, tx))
                            {
                                ins.Parameters.AddWithValue("@inv",  invoiceId);
                                ins.Parameters.AddWithValue("@stockItemId", item.StockItemID.HasValue ? (object)item.StockItemID.Value : DBNull.Value);
                                ins.Parameters.AddWithValue("@desc", item.Description ?? "");
                                ins.Parameters.AddWithValue("@hsn",  string.IsNullOrWhiteSpace(item.HSNCode) ? (object)DBNull.Value : item.HSNCode.Trim());
                                ins.Parameters.AddWithValue("@category", string.IsNullOrWhiteSpace(item.Category) ? "Service" : item.Category.Trim());
                                ins.Parameters.AddWithValue("@unit", string.IsNullOrWhiteSpace(item.Unit) ? "Nos" : item.Unit.Trim());
                                ins.Parameters.AddWithValue("@qty",  item.Quantity);
                                ins.Parameters.AddWithValue("@rate", item.Rate);
                                ins.Parameters.AddWithValue("@discount", item.DiscountPercent);
                                ins.Parameters.AddWithValue("@gst",  item.GSTPercent);
                                ins.Parameters.AddWithValue("@taxType", string.IsNullOrWhiteSpace(item.TaxType) ? "Taxable" : item.TaxType.Trim());
                                ins.Parameters.AddWithValue("@tax",  item.TaxAmount);
                                ins.Parameters.AddWithValue("@isStockItem", item.IsStockItem);
                                ins.Parameters.AddWithValue("@isBillable", item.IsBillable);
                                ins.Parameters.AddWithValue("@coverageNote", string.IsNullOrWhiteSpace(item.CoverageNote) ? (object)DBNull.Value : item.CoverageNote.Trim());
                                ins.Parameters.AddWithValue("@amt",  item.Amount);
                                ins.ExecuteNonQuery();
                            }
                        }
                        tx.Commit();
                    }
                    catch { tx.Rollback(); throw; }
                }
            }
        }

        // ── CREATE (with line items in transaction) ──────────
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
                        if (inv.LineItems != null && inv.LineItems.Count > 0)
                        {
                            foreach (var item in inv.LineItems)
                            {
                                using (var ins = new SqlCommand(@"
                                    INSERT INTO InvoiceLineItems
                                        (InvoiceID,StockItemID,Description,HSNCode,Category,Unit,Quantity,Rate,DiscountPercent,GSTPercent,TaxType,TaxAmount,IsStockItem,IsBillable,CoverageNote,Amount)
                                    VALUES
                                        (@inv,@stockItemId,@desc,@hsn,@category,@unit,@qty,@rate,@discount,@gst,@taxType,@tax,@isStockItem,@isBillable,@coverageNote,@amt)", conn, tx))
                                {
                                    ins.Parameters.AddWithValue("@inv",  newId);
                                    ins.Parameters.AddWithValue("@stockItemId", item.StockItemID.HasValue ? (object)item.StockItemID.Value : DBNull.Value);
                                    ins.Parameters.AddWithValue("@desc", item.Description ?? "");
                                    ins.Parameters.AddWithValue("@hsn",  string.IsNullOrWhiteSpace(item.HSNCode) ? (object)DBNull.Value : item.HSNCode.Trim());
                                    ins.Parameters.AddWithValue("@category", string.IsNullOrWhiteSpace(item.Category) ? "Service" : item.Category.Trim());
                                    ins.Parameters.AddWithValue("@unit", string.IsNullOrWhiteSpace(item.Unit) ? "Nos" : item.Unit.Trim());
                                    ins.Parameters.AddWithValue("@qty",  item.Quantity);
                                    ins.Parameters.AddWithValue("@rate", item.Rate);
                                    ins.Parameters.AddWithValue("@discount", item.DiscountPercent);
                                    ins.Parameters.AddWithValue("@gst",  item.GSTPercent);
                                    ins.Parameters.AddWithValue("@taxType", string.IsNullOrWhiteSpace(item.TaxType) ? "Taxable" : item.TaxType.Trim());
                                    ins.Parameters.AddWithValue("@tax",  item.TaxAmount);
                                    ins.Parameters.AddWithValue("@isStockItem", item.IsStockItem);
                                    ins.Parameters.AddWithValue("@isBillable", item.IsBillable);
                                    ins.Parameters.AddWithValue("@coverageNote", string.IsNullOrWhiteSpace(item.CoverageNote) ? (object)DBNull.Value : item.CoverageNote.Trim());
                                    ins.Parameters.AddWithValue("@amt",  item.Amount);
                                    ins.ExecuteNonQuery();
                                }
                            }
                        }

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
                        ModifiedDate = @modifiedDate
                    WHERE InvoiceID = @id";

                using (var cmd = new SqlCommand(sql, conn))
                {
                    cmd.Parameters.AddWithValue("@id", inv.InvoiceID);
                    cmd.Parameters.AddWithValue("@cid", inv.ContractID > 0 ? (object)inv.ContractID : DBNull.Value);
                    cmd.Parameters.AddWithValue("@clientId", inv.ClientID > 0 ? (object)inv.ClientID : DBNull.Value);
                    cmd.Parameters.AddWithValue("@siteId", inv.SiteID > 0 ? (object)inv.SiteID : DBNull.Value);
                    cmd.Parameters.AddWithValue("@quotationBidId", inv.QuotationBidID.HasValue ? (object)inv.QuotationBidID.Value : DBNull.Value);
                    cmd.Parameters.AddWithValue("@num", inv.InvoiceNumber ?? string.Empty);
                    cmd.Parameters.AddWithValue("@date", inv.InvoiceDate);
                    cmd.Parameters.AddWithValue("@due", inv.DueDate);
                    cmd.Parameters.AddWithValue("@sub", inv.SubTotal);
                    cmd.Parameters.AddWithValue("@gst", inv.GSTPercent);
                    cmd.Parameters.AddWithValue("@tax", inv.TaxAmount);
                    cmd.Parameters.AddWithValue("@total", inv.TotalAmount);
                    cmd.Parameters.AddWithValue("@paid", inv.PaidAmount);
                    cmd.Parameters.AddWithValue("@bal", inv.BalanceDue);
                    cmd.Parameters.AddWithValue("@status", inv.PaymentStatus ?? "Draft");
                    cmd.Parameters.AddWithValue("@notes", string.IsNullOrWhiteSpace(inv.Notes) ? (object)DBNull.Value : inv.Notes.Trim());
                    cmd.Parameters.AddWithValue("@title", string.IsNullOrWhiteSpace(inv.InvoiceTitle) ? "TAX INVOICE" : inv.InvoiceTitle.Trim());
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
                    cmd.Parameters.AddWithValue("@modifiedByUserId", inv.ModifiedByUserId.HasValue ? (object)inv.ModifiedByUserId.Value : DBNull.Value);
                    cmd.Parameters.AddWithValue("@modifiedByName", string.IsNullOrWhiteSpace(inv.ModifiedByName) ? (object)DBNull.Value : inv.ModifiedByName);
                    cmd.Parameters.AddWithValue("@modifiedDate", inv.ModifiedDate.HasValue ? (object)inv.ModifiedDate.Value : DBNull.Value);
                    cmd.ExecuteNonQuery();
                }
            }
        }

        // ── UPDATE STATUS / PAYMENT ──────────────────────────
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
                        PaymentDate   = CASE WHEN @status = 'Paid' THEN GETDATE() ELSE PaymentDate END
                    WHERE InvoiceID = @id";
                using (var cmd = new SqlCommand(sql, conn))
                {
                    cmd.Parameters.AddWithValue("@paid",   paidAmount);
                    cmd.Parameters.AddWithValue("@status", status);
                    cmd.Parameters.AddWithValue("@id",     invoiceId);
                    cmd.ExecuteNonQuery();
                }
            }
        }

        // ── INVOICE NUMBER GENERATION ────────────────────────
        public void Delete(int invoiceId)
        {
            using (var conn = _db.GetConnection())
            {
                conn.Open();
                using (SqlTransaction tx = conn.BeginTransaction())
                {
                    try
                    {
                        ExecuteDelete(conn, tx, "DELETE FROM Payments WHERE InvoiceID=@id", invoiceId);
                        ExecuteDelete(conn, tx, "DELETE FROM InvoiceInventoryReservations WHERE InvoiceID=@id", invoiceId);
                        ExecuteDelete(conn, tx, "UPDATE InventoryUsageLog SET InvoiceID=NULL WHERE InvoiceID=@id", invoiceId);
                        ExecuteDelete(conn, tx, "UPDATE Jobs SET InvoiceId=NULL WHERE InvoiceId=@id", invoiceId);
                        ExecuteDelete(conn, tx, "DELETE FROM InvoiceLineItems WHERE InvoiceID=@id", invoiceId);
                        ExecuteDelete(conn, tx, "DELETE FROM Invoices WHERE InvoiceID=@id", invoiceId);
                        tx.Commit();
                    }
                    catch
                    {
                        tx.Rollback();
                        throw;
                    }
                }
            }
        }

        private static void ExecuteDelete(SqlConnection conn, SqlTransaction tx, string sql, int id)
        {
            using (var cmd = new SqlCommand(sql, conn, tx))
            {
                cmd.Parameters.AddWithValue("@id", id);
                cmd.ExecuteNonQuery();
            }
        }

        public string GenerateInvoiceNumber()
        {
            using (var conn = _db.GetConnection())
            {
                conn.Open();
                string prefix = "INV-" + DateTime.Now.ToString("yyyy-MM");
                using (var cmd = new SqlCommand(
                    "SELECT COUNT(*) FROM Invoices WHERE InvoiceNumber LIKE @p", conn))
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
                    FROM Invoices
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
            try { inv.ClientID    = r["ClientID"] != DBNull.Value ? ToInt(r["ClientID"]) : 0; }    catch { }
            try { inv.SiteID      = r["SiteID"] != DBNull.Value ? ToInt(r["SiteID"]) : 0; }        catch { }
            try { inv.QuotationBidID = r["QuotationBidID"] != DBNull.Value ? (int?)ToInt(r["QuotationBidID"]) : null; } catch { }
            try { inv.GSTPercent  = r["GSTPercent"] != DBNull.Value ? ToDecimal(r["GSTPercent"]) : 18; } catch { }
            try { inv.BalanceDue  = r["BalanceDue"] != DBNull.Value ? ToDecimal(r["BalanceDue"]) : inv.TotalAmount - inv.PaidAmount; } catch { }
            try { inv.Notes       = r["Notes"].ToString(); }       catch { }
            try { inv.InvoiceTitle = r["InvoiceTitle"].ToString(); } catch { }
            try { inv.Subject = r["Subject"] == DBNull.Value ? "" : r["Subject"].ToString(); } catch { }
            try { inv.PONumber = r["PONumber"] == DBNull.Value ? "" : r["PONumber"].ToString(); } catch { }
            try { inv.PODate = r["PODate"] != DBNull.Value ? (DateTime?)ToDate(r["PODate"]) : null; } catch { }
            try { inv.SendInvoiceTo = r["SendInvoiceTo"] == DBNull.Value ? "" : r["SendInvoiceTo"].ToString(); } catch { }
            try { inv.CertificationNote = r["CertificationNote"] == DBNull.Value ? "" : r["CertificationNote"].ToString(); } catch { }
            try { inv.TemplateCode = r["TemplateCode"] == DBNull.Value ? "" : r["TemplateCode"].ToString(); } catch { }
            try { inv.WorkflowType = r["WorkflowType"] == DBNull.Value ? "" : r["WorkflowType"].ToString(); } catch { }
            try { inv.GSTMode = r["GSTMode"] == DBNull.Value ? "IGST" : r["GSTMode"].ToString(); } catch { }
            try { inv.PaymentTerms = r["PaymentTerms"] == DBNull.Value ? "" : r["PaymentTerms"].ToString(); } catch { }
            try { inv.PlaceOfSupply = r["PlaceOfSupply"] == DBNull.Value ? "" : r["PlaceOfSupply"].ToString(); } catch { }
            try { inv.RoundOff = r["RoundOff"] != DBNull.Value ? ToDecimal(r["RoundOff"]) : 0m; } catch { }
            try { inv.CGSTAmount = r["CGSTAmount"] != DBNull.Value ? ToDecimal(r["CGSTAmount"]) : 0m; } catch { }
            try { inv.SGSTAmount = r["SGSTAmount"] != DBNull.Value ? ToDecimal(r["SGSTAmount"]) : 0m; } catch { }
            try { inv.IGSTAmount = r["IGSTAmount"] != DBNull.Value ? ToDecimal(r["IGSTAmount"]) : 0m; } catch { }
            try { inv.ContractCoverageType = r["ContractCoverageType"] == DBNull.Value ? "" : r["ContractCoverageType"].ToString(); } catch { }
            try { inv.ServiceChecklist = r["ServiceChecklist"] == DBNull.Value ? "" : r["ServiceChecklist"].ToString(); } catch { }
            try { inv.AssetDetails = r["AssetDetails"] == DBNull.Value ? "" : r["AssetDetails"].ToString(); } catch { }
            try { inv.WarrantyStatus = r["WarrantyStatus"] == DBNull.Value ? "" : r["WarrantyStatus"].ToString(); } catch { }
            try { inv.WarrantyExpiry = r["WarrantyExpiry"] != DBNull.Value ? (DateTime?)ToDate(r["WarrantyExpiry"]) : null; } catch { }
            try { inv.PreventiveVisitDate = r["PreventiveVisitDate"] != DBNull.Value ? (DateTime?)ToDate(r["PreventiveVisitDate"]) : null; } catch { }
            try { inv.NextServiceDueDate = r["NextServiceDueDate"] != DBNull.Value ? (DateTime?)ToDate(r["NextServiceDueDate"]) : null; } catch { }
            try { inv.InventoryReservationStatus = r["InventoryReservationStatus"] == DBNull.Value ? "" : r["InventoryReservationStatus"].ToString(); } catch { }
            try { inv.CreatedByUserId = r["CreatedByUserId"] != DBNull.Value ? (int?)ToInt(r["CreatedByUserId"]) : null; } catch { }
            try { inv.CreatedByName = r["CreatedByName"] == DBNull.Value ? null : r["CreatedByName"].ToString(); } catch { }
            try { inv.ModifiedByUserId = r["ModifiedByUserId"] != DBNull.Value ? (int?)ToInt(r["ModifiedByUserId"]) : null; } catch { }
            try { inv.ModifiedByName = r["ModifiedByName"] == DBNull.Value ? null : r["ModifiedByName"].ToString(); } catch { }
            try { inv.ModifiedDate = r["ModifiedDate"] != DBNull.Value ? (DateTime?)ToDate(r["ModifiedDate"]) : null; } catch { }
            try { inv.ClientName  = r["ClientName"].ToString(); }  catch { }
            try { inv.SiteName    = r["SiteName"].ToString(); }    catch { }
            return inv;
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
    }
}
