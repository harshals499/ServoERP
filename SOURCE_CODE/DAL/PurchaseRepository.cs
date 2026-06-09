using System;
using System.Collections.Generic;
using System.Data.SqlClient;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using HVAC_Pro_Desktop.Models;

namespace HVAC_Pro_Desktop.DAL
{
    public class PurchaseRepository
    {
        private readonly DatabaseManager _db = new DatabaseManager();

        public List<PurchaseOrder> GetAll()
        {
            var list = new List<PurchaseOrder>();
            using (SqlConnection conn = _db.GetConnection())
            {
                conn.Open();
                using (SqlCommand cmd = new SqlCommand(@"
                    SELECT p.*, v.VendorName, v.GSTNumber AS VendorGSTIN, c.CompanyName AS ClientName, s.SiteName,
                           CASE
                               WHEN p.LinkedToType = 'Contract' AND p.LinkedToId IS NOT NULL THEN 'Contract #' + CAST(p.LinkedToId AS NVARCHAR(20))
                               WHEN p.LinkedToType = 'WorkOrder' AND j.JobNumber IS NOT NULL THEN j.JobNumber
                               WHEN p.LinkedToType = 'General' THEN 'General'
                               ELSE NULL
                           END AS LinkedToLabel
                    FROM PurchaseOrders p
                    LEFT JOIN Vendors v ON p.VendorID = v.VendorID
                    LEFT JOIN B2BClients c ON p.ClientID = c.ClientID
                    LEFT JOIN ClientSites s ON p.SiteID = s.SiteID
                    LEFT JOIN Jobs j ON p.LinkedToType = 'WorkOrder' AND p.LinkedToId = j.JobID
                    ORDER BY CASE WHEN ISNULL(p.Status,'') IN ('Fully Received','Received','Paid','Closed') OR p.PaidAmount >= p.TotalAmount THEN 1 ELSE 0 END,
                             p.PayByDate ASC,
                             p.PODate DESC", conn))
                using (SqlDataReader r = cmd.ExecuteReader())
                    while (r.Read()) list.Add(MapOrder(r));
            }
            return list;
        }

        public PurchaseOrder GetById(int id)
        {
            PurchaseOrder po = null;
            using (SqlConnection conn = _db.GetConnection())
            {
                conn.Open();
                using (SqlCommand cmd = new SqlCommand(@"
                    SELECT p.*, v.VendorName, v.GSTNumber AS VendorGSTIN, c.CompanyName AS ClientName, s.SiteName,
                           CASE
                               WHEN p.LinkedToType = 'Contract' AND p.LinkedToId IS NOT NULL THEN 'Contract #' + CAST(p.LinkedToId AS NVARCHAR(20))
                               WHEN p.LinkedToType = 'WorkOrder' AND j.JobNumber IS NOT NULL THEN j.JobNumber
                               WHEN p.LinkedToType = 'General' THEN 'General'
                               ELSE NULL
                           END AS LinkedToLabel
                    FROM PurchaseOrders p
                    LEFT JOIN Vendors v ON p.VendorID = v.VendorID
                    LEFT JOIN B2BClients c ON p.ClientID = c.ClientID
                    LEFT JOIN ClientSites s ON p.SiteID = s.SiteID
                    LEFT JOIN Jobs j ON p.LinkedToType = 'WorkOrder' AND p.LinkedToId = j.JobID
                    WHERE p.POID = @id", conn))
                {
                    cmd.Parameters.AddWithValue("@id", id);
                    using (SqlDataReader r = cmd.ExecuteReader())
                        if (r.Read()) po = MapOrder(r);
                }
                if (po != null)
                {
                    using (SqlCommand cmd2 = new SqlCommand(
                        "SELECT * FROM PurchaseLineItems WHERE POID = @id ORDER BY LineItemID", conn))
                    {
                        cmd2.Parameters.AddWithValue("@id", id);
                        using (SqlDataReader r2 = cmd2.ExecuteReader())
                            while (r2.Read()) po.LineItems.Add(MapLine(r2));
                    }
                }
            }
            return po;
        }

        public List<PurchaseOrder> GetByVendorId(int vendorId)
        {
            var list = new List<PurchaseOrder>();
            using (SqlConnection conn = _db.GetConnection())
            {
                conn.Open();
                using (SqlCommand cmd = new SqlCommand(@"
                    SELECT p.*, v.VendorName, v.GSTNumber AS VendorGSTIN, c.CompanyName AS ClientName, s.SiteName,
                           CASE
                               WHEN p.LinkedToType = 'Contract' AND p.LinkedToId IS NOT NULL THEN 'Contract #' + CAST(p.LinkedToId AS NVARCHAR(20))
                               WHEN p.LinkedToType = 'WorkOrder' AND j.JobNumber IS NOT NULL THEN j.JobNumber
                               WHEN p.LinkedToType = 'General' THEN 'General'
                               ELSE NULL
                           END AS LinkedToLabel
                    FROM PurchaseOrders p
                    LEFT JOIN Vendors v ON p.VendorID = v.VendorID
                    LEFT JOIN B2BClients c ON p.ClientID = c.ClientID
                    LEFT JOIN ClientSites s ON p.SiteID = s.SiteID
                    LEFT JOIN Jobs j ON p.LinkedToType = 'WorkOrder' AND p.LinkedToId = j.JobID
                    WHERE p.VendorID = @vid ORDER BY p.PayByDate ASC, p.PODate DESC", conn))
                {
                    cmd.Parameters.AddWithValue("@vid", vendorId);
                    using (SqlDataReader r = cmd.ExecuteReader())
                        while (r.Read()) list.Add(MapOrder(r));
                }
            }
            return list;
        }

        public decimal GetTotalPurchasedByVendor(int vendorId)
        {
            if (vendorId <= 0)
                return 0m;

            using (SqlConnection conn = _db.GetConnection())
            {
                conn.Open();
                using (SqlCommand cmd = new SqlCommand(
                    "SELECT ISNULL(SUM(TotalAmount), 0) FROM PurchaseOrders WHERE VendorID = @vendorId", conn))
                {
                    cmd.Parameters.AddWithValue("@vendorId", vendorId);
                    object result = cmd.ExecuteScalar();
                    return result == DBNull.Value || result == null ? 0m : Convert.ToDecimal(result);
                }
            }
        }

        public int Create(PurchaseOrder po)
        {
            po.ApplyPaymentCompletionRule();
            using (SqlConnection conn = _db.GetConnection())
            {
                conn.Open();
                using (SqlTransaction tx = conn.BeginTransaction())
                {
                    int poid;
                    using (SqlCommand cmd = new SqlCommand(@"
                        INSERT INTO PurchaseOrders (VendorID,ClientID,SiteID,RelatedContractID,RecommendedByBidID,PONumber,PODate,PayByDate,VendorInvoiceNumber,LinkedToType,LinkedToId,DeliveryMode,AssignedTechnicianId,AssignedTechnicianName,DeliveryAddress,AddToClientInvoice,PendingChargeCreated,ReceiptImagePath,PriceVarianceFlag,CreatedByUserId,CreatedByName,CreatedByDate,TotalAmount,PaidAmount,Status,PaymentReference,ComparisonNotes,Notes)
                        VALUES (@vid,@clientId,@siteId,@contractId,@bidId,@no,@dt,@payBy,@vendorInvoice,@linkedType,@linkedId,@deliveryMode,@techId,@techName,@deliveryAddress,@billToClient,@pendingChargeCreated,@receiptImagePath,@priceVarianceFlag,@createdByUserId,@createdByName,@createdByDate,@tot,@paid,@st,@paymentRef,@cmp,@no2);
                        SELECT SCOPE_IDENTITY();", conn, tx))
                    {
                        cmd.Parameters.AddWithValue("@vid",  po.VendorID);
                        cmd.Parameters.AddWithValue("@clientId", po.ClientID > 0 ? (object)po.ClientID : DBNull.Value);
                        cmd.Parameters.AddWithValue("@siteId", po.SiteID > 0 ? (object)po.SiteID : DBNull.Value);
                        cmd.Parameters.AddWithValue("@contractId", po.RelatedContractID.HasValue ? (object)po.RelatedContractID.Value : DBNull.Value);
                        cmd.Parameters.AddWithValue("@bidId", po.RecommendedByBidID.HasValue ? (object)po.RecommendedByBidID.Value : DBNull.Value);
                        cmd.Parameters.AddWithValue("@no",   po.PONumber);
                        cmd.Parameters.AddWithValue("@dt",   po.PODate);
                        cmd.Parameters.AddWithValue("@payBy", po.PayByDate == default ? po.PODate : po.PayByDate);
                        cmd.Parameters.AddWithValue("@vendorInvoice", string.IsNullOrWhiteSpace(po.VendorInvoiceNumber) ? (object)DBNull.Value : po.VendorInvoiceNumber.Trim());
                        cmd.Parameters.AddWithValue("@linkedType", string.IsNullOrWhiteSpace(po.LinkedToType) ? (object)DBNull.Value : po.LinkedToType.Trim());
                        cmd.Parameters.AddWithValue("@linkedId", po.LinkedToId.HasValue ? (object)po.LinkedToId.Value : DBNull.Value);
                        cmd.Parameters.AddWithValue("@deliveryMode", string.IsNullOrWhiteSpace(po.DeliveryMode) ? "TechPickup" : po.DeliveryMode.Trim());
                        cmd.Parameters.AddWithValue("@techId", po.AssignedTechnicianId.HasValue ? (object)po.AssignedTechnicianId.Value : DBNull.Value);
                        cmd.Parameters.AddWithValue("@techName", string.IsNullOrWhiteSpace(po.AssignedTechnicianName) ? (object)DBNull.Value : po.AssignedTechnicianName.Trim());
                        cmd.Parameters.AddWithValue("@deliveryAddress", string.IsNullOrWhiteSpace(po.DeliveryAddress) ? (object)DBNull.Value : po.DeliveryAddress.Trim());
                        cmd.Parameters.AddWithValue("@billToClient", po.AddToClientInvoice);
                        cmd.Parameters.AddWithValue("@pendingChargeCreated", po.PendingChargeCreated);
                        cmd.Parameters.AddWithValue("@receiptImagePath", string.IsNullOrWhiteSpace(po.ReceiptImagePath) ? (object)DBNull.Value : po.ReceiptImagePath.Trim());
                        cmd.Parameters.AddWithValue("@priceVarianceFlag", po.PriceVarianceFlag);
                        cmd.Parameters.AddWithValue("@createdByUserId", po.CreatedByUserId.HasValue ? (object)po.CreatedByUserId.Value : DBNull.Value);
                        cmd.Parameters.AddWithValue("@createdByName", string.IsNullOrWhiteSpace(po.CreatedByName) ? (object)DBNull.Value : po.CreatedByName.Trim());
                        cmd.Parameters.AddWithValue("@createdByDate", po.CreatedByDate.HasValue ? (object)po.CreatedByDate.Value : DateTime.Now);
                        cmd.Parameters.AddWithValue("@tot",  po.TotalAmount);
                        cmd.Parameters.AddWithValue("@paid", po.PaidAmount);
                        cmd.Parameters.AddWithValue("@st",   po.Status ?? "Pending");
                        cmd.Parameters.AddWithValue("@paymentRef", string.IsNullOrWhiteSpace(po.PaymentReference) ? (object)DBNull.Value : po.PaymentReference.Trim());
                        cmd.Parameters.AddWithValue("@cmp",  (object)po.ComparisonNotes ?? DBNull.Value);
                        cmd.Parameters.AddWithValue("@no2",  (object)po.Notes ?? DBNull.Value);
                        poid = (int)(decimal)cmd.ExecuteScalar();
                    }
                    foreach (var li in po.LineItems)
                    {
                        using (SqlCommand cmd2 = new SqlCommand(@"
                            INSERT INTO PurchaseLineItems (POID,InventoryItemId,Description,HsnSacCode,Quantity,UOM,Rate,GSTRate,CGSTRate,SGSTRate,IGSTRate,JobLink,LinkedWorkOrderId,LinkedWorkOrderName,PriceVariance,Amount)
                            VALUES (@pid,@inventoryItemId,@desc,@hsn,@qty,@uom,@rate,@gst,@cgst,@sgst,@igst,@jobLink,@linkedWorkOrderId,@linkedWorkOrderName,@priceVariance,@amt)", conn, tx))
                        {
                            cmd2.Parameters.AddWithValue("@pid",  poid);
                            cmd2.Parameters.AddWithValue("@inventoryItemId", li.InventoryItemId.HasValue ? (object)li.InventoryItemId.Value : DBNull.Value);
                            cmd2.Parameters.AddWithValue("@desc", li.Description);
                            cmd2.Parameters.AddWithValue("@hsn", string.IsNullOrWhiteSpace(li.HsnSacCode) ? (object)DBNull.Value : li.HsnSacCode.Trim());
                            cmd2.Parameters.AddWithValue("@qty",  li.Quantity);
                            cmd2.Parameters.AddWithValue("@uom", string.IsNullOrWhiteSpace(li.UOM) ? "Nos" : li.UOM.Trim());
                            cmd2.Parameters.AddWithValue("@rate", li.Rate);
                            cmd2.Parameters.AddWithValue("@gst", li.GSTRate);
                            cmd2.Parameters.AddWithValue("@cgst", li.CGSTRate);
                            cmd2.Parameters.AddWithValue("@sgst", li.SGSTRate);
                            cmd2.Parameters.AddWithValue("@igst", li.IGSTRate);
                            cmd2.Parameters.AddWithValue("@jobLink", string.IsNullOrWhiteSpace(li.JobLink) ? "General" : li.JobLink.Trim());
                            cmd2.Parameters.AddWithValue("@linkedWorkOrderId", li.LinkedWorkOrderId.HasValue ? (object)li.LinkedWorkOrderId.Value : DBNull.Value);
                            cmd2.Parameters.AddWithValue("@linkedWorkOrderName", string.IsNullOrWhiteSpace(li.LinkedWorkOrderName) ? (object)DBNull.Value : li.LinkedWorkOrderName.Trim());
                            cmd2.Parameters.AddWithValue("@priceVariance", li.PriceVariance);
                            cmd2.Parameters.AddWithValue("@amt",  li.Amount);
                            cmd2.ExecuteNonQuery();
                        }
                    }
                    tx.Commit();
                    return poid;
                }
            }
        }

        public void Update(PurchaseOrder po)
        {
            po.ApplyPaymentCompletionRule();
            using (SqlConnection conn = _db.GetConnection())
            {
                conn.Open();
                using (SqlTransaction tx = conn.BeginTransaction())
                {
                    using (SqlCommand cmd = new SqlCommand(@"
                        UPDATE PurchaseOrders
                        SET VendorID=@vid,
                            ClientID=@clientId,
                            SiteID=@siteId,
                            RelatedContractID=@contractId,
                            RecommendedByBidID=@bidId,
                            PONumber=@no,
                            PODate=@dt,
                            PayByDate=@payBy,
                            VendorInvoiceNumber=@vendorInvoice,
                            LinkedToType=@linkedType,
                            LinkedToId=@linkedId,
                            DeliveryMode=@deliveryMode,
                            AssignedTechnicianId=@techId,
                            AssignedTechnicianName=@techName,
                            DeliveryAddress=@deliveryAddress,
                            AddToClientInvoice=@billToClient,
                            PendingChargeCreated=@pendingChargeCreated,
                            ReceiptImagePath=@receiptImagePath,
                            PriceVarianceFlag=@priceVarianceFlag,
                            CreatedByUserId=@createdByUserId,
                            CreatedByName=@createdByName,
                            CreatedByDate=@createdByDate,
                            ModifiedByUserId=@modifiedByUserId,
                            ModifiedByName=@modifiedByName,
                            ModifiedDate=@modifiedDate,
                            TotalAmount=@tot,
                            PaidAmount=@paid,
                            Status=@st,
                            PaymentReference=@paymentRef,
                            ComparisonNotes=@cmp,
                            Notes=@no2
                        WHERE POID=@id", conn, tx))
                    {
                        cmd.Parameters.AddWithValue("@id", po.POID);
                        cmd.Parameters.AddWithValue("@vid", po.VendorID);
                        cmd.Parameters.AddWithValue("@clientId", po.ClientID > 0 ? (object)po.ClientID : DBNull.Value);
                        cmd.Parameters.AddWithValue("@siteId", po.SiteID > 0 ? (object)po.SiteID : DBNull.Value);
                        cmd.Parameters.AddWithValue("@contractId", po.RelatedContractID.HasValue ? (object)po.RelatedContractID.Value : DBNull.Value);
                        cmd.Parameters.AddWithValue("@bidId", po.RecommendedByBidID.HasValue ? (object)po.RecommendedByBidID.Value : DBNull.Value);
                        cmd.Parameters.AddWithValue("@no", po.PONumber ?? string.Empty);
                        cmd.Parameters.AddWithValue("@dt", po.PODate);
                        cmd.Parameters.AddWithValue("@payBy", po.PayByDate == default ? po.PODate : po.PayByDate);
                        cmd.Parameters.AddWithValue("@vendorInvoice", string.IsNullOrWhiteSpace(po.VendorInvoiceNumber) ? (object)DBNull.Value : po.VendorInvoiceNumber.Trim());
                        cmd.Parameters.AddWithValue("@linkedType", string.IsNullOrWhiteSpace(po.LinkedToType) ? (object)DBNull.Value : po.LinkedToType.Trim());
                        cmd.Parameters.AddWithValue("@linkedId", po.LinkedToId.HasValue ? (object)po.LinkedToId.Value : DBNull.Value);
                        cmd.Parameters.AddWithValue("@deliveryMode", string.IsNullOrWhiteSpace(po.DeliveryMode) ? "TechPickup" : po.DeliveryMode.Trim());
                        cmd.Parameters.AddWithValue("@techId", po.AssignedTechnicianId.HasValue ? (object)po.AssignedTechnicianId.Value : DBNull.Value);
                        cmd.Parameters.AddWithValue("@techName", string.IsNullOrWhiteSpace(po.AssignedTechnicianName) ? (object)DBNull.Value : po.AssignedTechnicianName.Trim());
                        cmd.Parameters.AddWithValue("@deliveryAddress", string.IsNullOrWhiteSpace(po.DeliveryAddress) ? (object)DBNull.Value : po.DeliveryAddress.Trim());
                        cmd.Parameters.AddWithValue("@billToClient", po.AddToClientInvoice);
                        cmd.Parameters.AddWithValue("@pendingChargeCreated", po.PendingChargeCreated);
                        cmd.Parameters.AddWithValue("@receiptImagePath", string.IsNullOrWhiteSpace(po.ReceiptImagePath) ? (object)DBNull.Value : po.ReceiptImagePath.Trim());
                        cmd.Parameters.AddWithValue("@priceVarianceFlag", po.PriceVarianceFlag);
                        cmd.Parameters.AddWithValue("@createdByUserId", po.CreatedByUserId.HasValue ? (object)po.CreatedByUserId.Value : DBNull.Value);
                        cmd.Parameters.AddWithValue("@createdByName", string.IsNullOrWhiteSpace(po.CreatedByName) ? (object)DBNull.Value : po.CreatedByName.Trim());
                        cmd.Parameters.AddWithValue("@createdByDate", po.CreatedByDate.HasValue ? (object)po.CreatedByDate.Value : DateTime.Now);
                        cmd.Parameters.AddWithValue("@modifiedByUserId", po.ModifiedByUserId.HasValue ? (object)po.ModifiedByUserId.Value : DBNull.Value);
                        cmd.Parameters.AddWithValue("@modifiedByName", string.IsNullOrWhiteSpace(po.ModifiedByName) ? (object)DBNull.Value : po.ModifiedByName.Trim());
                        cmd.Parameters.AddWithValue("@modifiedDate", po.ModifiedDate.HasValue ? (object)po.ModifiedDate.Value : DBNull.Value);
                        cmd.Parameters.AddWithValue("@tot", po.TotalAmount);
                        cmd.Parameters.AddWithValue("@paid", po.PaidAmount);
                        cmd.Parameters.AddWithValue("@st", po.Status ?? "Pending");
                        cmd.Parameters.AddWithValue("@paymentRef", string.IsNullOrWhiteSpace(po.PaymentReference) ? (object)DBNull.Value : po.PaymentReference.Trim());
                        cmd.Parameters.AddWithValue("@cmp", (object)po.ComparisonNotes ?? DBNull.Value);
                        cmd.Parameters.AddWithValue("@no2", (object)po.Notes ?? DBNull.Value);
                        cmd.ExecuteNonQuery();
                    }

                    using (SqlCommand delete = new SqlCommand("DELETE FROM PurchaseLineItems WHERE POID=@id", conn, tx))
                    {
                        delete.Parameters.AddWithValue("@id", po.POID);
                        delete.ExecuteNonQuery();
                    }

                    foreach (var li in po.LineItems ?? Enumerable.Empty<PurchaseLineItem>())
                    {
                        using (SqlCommand cmd2 = new SqlCommand(@"
                            INSERT INTO PurchaseLineItems (POID,InventoryItemId,Description,HsnSacCode,Quantity,UOM,Rate,GSTRate,CGSTRate,SGSTRate,IGSTRate,JobLink,LinkedWorkOrderId,LinkedWorkOrderName,PriceVariance,Amount)
                            VALUES (@pid,@inventoryItemId,@desc,@hsn,@qty,@uom,@rate,@gst,@cgst,@sgst,@igst,@jobLink,@linkedWorkOrderId,@linkedWorkOrderName,@priceVariance,@amt)", conn, tx))
                        {
                            cmd2.Parameters.AddWithValue("@pid", po.POID);
                            cmd2.Parameters.AddWithValue("@inventoryItemId", li.InventoryItemId.HasValue ? (object)li.InventoryItemId.Value : DBNull.Value);
                            cmd2.Parameters.AddWithValue("@desc", li.Description ?? string.Empty);
                            cmd2.Parameters.AddWithValue("@hsn", string.IsNullOrWhiteSpace(li.HsnSacCode) ? (object)DBNull.Value : li.HsnSacCode.Trim());
                            cmd2.Parameters.AddWithValue("@qty", li.Quantity);
                            cmd2.Parameters.AddWithValue("@uom", string.IsNullOrWhiteSpace(li.UOM) ? "Nos" : li.UOM.Trim());
                            cmd2.Parameters.AddWithValue("@rate", li.Rate);
                            cmd2.Parameters.AddWithValue("@gst", li.GSTRate);
                            cmd2.Parameters.AddWithValue("@cgst", li.CGSTRate);
                            cmd2.Parameters.AddWithValue("@sgst", li.SGSTRate);
                            cmd2.Parameters.AddWithValue("@igst", li.IGSTRate);
                            cmd2.Parameters.AddWithValue("@jobLink", string.IsNullOrWhiteSpace(li.JobLink) ? "General" : li.JobLink.Trim());
                            cmd2.Parameters.AddWithValue("@linkedWorkOrderId", li.LinkedWorkOrderId.HasValue ? (object)li.LinkedWorkOrderId.Value : DBNull.Value);
                            cmd2.Parameters.AddWithValue("@linkedWorkOrderName", string.IsNullOrWhiteSpace(li.LinkedWorkOrderName) ? (object)DBNull.Value : li.LinkedWorkOrderName.Trim());
                            cmd2.Parameters.AddWithValue("@priceVariance", li.PriceVariance);
                            cmd2.Parameters.AddWithValue("@amt", li.Amount);
                            cmd2.ExecuteNonQuery();
                        }
                    }

                    tx.Commit();
                }
            }
        }

        public decimal GetTotalSpendThisMonth()
        {
            using (SqlConnection conn = _db.GetConnection())
            {
                conn.Open();
                using (SqlCommand cmd = new SqlCommand(@"
                    SELECT ISNULL(SUM(TotalAmount),0) FROM PurchaseOrders
                    WHERE MONTH(PODate)=MONTH(GETDATE()) AND YEAR(PODate)=YEAR(GETDATE())", conn))
                    return (decimal)cmd.ExecuteScalar();
            }
        }

        public void MarkReceived(int poId)
        {
            using (SqlConnection conn = _db.GetConnection())
            {
                conn.Open();
                using (SqlTransaction tx = conn.BeginTransaction())
                {
                    try
                    {
                        string previousStatus;
                        string poNumber;
                        using (SqlCommand statusCmd = new SqlCommand(
                            "SELECT Status, PONumber FROM PurchaseOrders WITH (UPDLOCK, ROWLOCK) WHERE POID=@id", conn, tx))
                        {
                            statusCmd.Parameters.AddWithValue("@id", poId);
                            using (SqlDataReader reader = statusCmd.ExecuteReader())
                            {
                                if (!reader.Read())
                                    throw new InvalidOperationException("Purchase order not found.");

                                previousStatus = reader["Status"] == DBNull.Value ? string.Empty : Convert.ToString(reader["Status"]);
                                poNumber = reader["PONumber"] == DBNull.Value ? string.Empty : Convert.ToString(reader["PONumber"]);
                            }
                        }

                        bool wasAlreadyReceived = PurchaseOrder.IsPaymentCompletedStatus(previousStatus);
                        if (!wasAlreadyReceived)
                            PostReceivedInventory(conn, tx, poId, poNumber);

                        using (SqlCommand cmd = new SqlCommand(
                            "UPDATE PurchaseOrders SET Status='Fully Received', PaidAmount=TotalAmount WHERE POID=@id", conn, tx))
                        {
                            cmd.Parameters.AddWithValue("@id", poId);
                            cmd.ExecuteNonQuery();
                        }

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

        private static void PostReceivedInventory(SqlConnection conn, SqlTransaction tx, int poId, string poNumber)
        {
            var lines = new List<PurchaseLineItem>();
            using (SqlCommand cmd = new SqlCommand(@"
                SELECT InventoryItemId, Description, Quantity, Rate
                FROM PurchaseLineItems
                WHERE POID=@id
                  AND InventoryItemId IS NOT NULL
                  AND Quantity > 0", conn, tx))
            {
                cmd.Parameters.AddWithValue("@id", poId);
                using (SqlDataReader reader = cmd.ExecuteReader())
                {
                    while (reader.Read())
                    {
                        lines.Add(new PurchaseLineItem
                        {
                            InventoryItemId = reader["InventoryItemId"] == DBNull.Value ? (int?)null : Convert.ToInt32(reader["InventoryItemId"]),
                            Description = reader["Description"] == DBNull.Value ? string.Empty : Convert.ToString(reader["Description"]),
                            Quantity = reader["Quantity"] == DBNull.Value ? 0m : Convert.ToDecimal(reader["Quantity"]),
                            Rate = reader["Rate"] == DBNull.Value ? 0m : Convert.ToDecimal(reader["Rate"])
                        });
                    }
                }
            }

            foreach (PurchaseLineItem line in lines)
            {
                decimal stockBefore;
                using (SqlCommand stockCmd = new SqlCommand(
                    "SELECT CurrentStock FROM StockItems WITH (UPDLOCK, ROWLOCK) WHERE ItemID=@itemId AND ISNULL(IsActive, 1)=1", conn, tx))
                {
                    stockCmd.Parameters.AddWithValue("@itemId", line.InventoryItemId.Value);
                    object value = stockCmd.ExecuteScalar();
                    if (value == null || value == DBNull.Value)
                        throw new InvalidOperationException("A material linked to this purchase order was not found in inventory.");
                    stockBefore = Convert.ToDecimal(value);
                }

                decimal stockAfter = stockBefore + line.Quantity;
                using (SqlCommand updateCmd = new SqlCommand(@"
                    UPDATE StockItems
                    SET CurrentStock=@stockAfter,
                        LastPurchaseRate=CASE WHEN @rate > 0 THEN @rate ELSE LastPurchaseRate END,
                        LastUpdated=GETDATE()
                    WHERE ItemID=@itemId AND ISNULL(IsActive, 1)=1", conn, tx))
                {
                    updateCmd.Parameters.AddWithValue("@stockAfter", stockAfter);
                    updateCmd.Parameters.AddWithValue("@rate", line.Rate);
                    updateCmd.Parameters.AddWithValue("@itemId", line.InventoryItemId.Value);
                    updateCmd.ExecuteNonQuery();
                }

                using (SqlCommand movementCmd = new SqlCommand(@"
                    INSERT INTO StockMovements
                        (ItemID, MovementType, Quantity, StockBefore, StockAfter, ToLocation, ReferenceNo, Notes, CreatedDate)
                    VALUES
                        (@itemId, 'PurchaseReceive', @qty, @before, @after, 'Main Stock', @ref, @notes, GETDATE())", conn, tx))
                {
                    movementCmd.Parameters.AddWithValue("@itemId", line.InventoryItemId.Value);
                    movementCmd.Parameters.AddWithValue("@qty", line.Quantity);
                    movementCmd.Parameters.AddWithValue("@before", stockBefore);
                    movementCmd.Parameters.AddWithValue("@after", stockAfter);
                    movementCmd.Parameters.AddWithValue("@ref", string.IsNullOrWhiteSpace(poNumber) ? "PO#" + poId.ToString() : poNumber);
                    movementCmd.Parameters.AddWithValue("@notes", "Received from purchase order " + (string.IsNullOrWhiteSpace(poNumber) ? "#" + poId.ToString() : poNumber) + ": " + (line.Description ?? string.Empty));
                    movementCmd.ExecuteNonQuery();
                }
            }
        }

        public void Delete(int poId)
        {
            using (SqlConnection conn = _db.GetConnection())
            {
                conn.Open();
                using (SqlTransaction tx = conn.BeginTransaction())
                {
                    try
                    {
                        ExecuteDelete(conn, tx, "DELETE FROM PendingCharges WHERE SourcePoId=@id", poId);
                        ExecuteDelete(conn, tx, "UPDATE VendorAdvancePayments SET POID=NULL WHERE POID=@id", poId);
                        ExecuteDelete(conn, tx, "DELETE FROM PurchaseLineItems WHERE POID=@id", poId);
                        ExecuteDelete(conn, tx, "DELETE FROM PurchaseOrders WHERE POID=@id", poId);
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
            using (SqlCommand cmd = new SqlCommand(sql, conn, tx))
            {
                cmd.Parameters.AddWithValue("@id", id);
                cmd.ExecuteNonQuery();
            }
        }

        public void UpdateContractLink(int poId, int contractId)
        {
            using (SqlConnection conn = _db.GetConnection())
            {
                conn.Open();
                using (SqlCommand cmd = new SqlCommand(
                    "UPDATE PurchaseOrders SET RelatedContractID=@cid, LinkedToType='Contract', LinkedToId=@cid WHERE POID=@id", conn))
                {
                    cmd.Parameters.AddWithValue("@cid", contractId);
                    cmd.Parameters.AddWithValue("@id",  poId);
                    cmd.ExecuteNonQuery();
                }
            }
        }

        public void BatchMarkPaid(IEnumerable<int> poIds, string paymentReference)
        {
            List<int> ids = (poIds ?? Enumerable.Empty<int>()).Where(id => id > 0).Distinct().ToList();
            if (ids.Count == 0)
                return;

            using (SqlConnection conn = _db.GetConnection())
            {
                conn.Open();
                foreach (int id in ids)
                {
                    using (SqlCommand cmd = new SqlCommand(@"
                        UPDATE PurchaseOrders
                        SET PaidAmount = TotalAmount,
                            Status = 'Paid',
                            PaymentReference = @paymentRef
                        WHERE POID = @id", conn))
                    {
                        cmd.Parameters.AddWithValue("@id", id);
                        cmd.Parameters.AddWithValue("@paymentRef", string.IsNullOrWhiteSpace(paymentReference) ? (object)DBNull.Value : paymentReference.Trim());
                        cmd.ExecuteNonQuery();
                    }
                }
            }
        }

        public void UpdateDeliveryDetails(int poId, string deliveryMode, string deliveryAddress)
        {
            using (SqlConnection conn = _db.GetConnection())
            {
                conn.Open();
                using (SqlCommand cmd = new SqlCommand("UPDATE PurchaseOrders SET DeliveryMode=@mode, DeliveryAddress=@address WHERE POID=@id", conn))
                {
                    cmd.Parameters.AddWithValue("@id", poId);
                    cmd.Parameters.AddWithValue("@mode", string.IsNullOrWhiteSpace(deliveryMode) ? "TechPickup" : deliveryMode.Trim());
                    cmd.Parameters.AddWithValue("@address", string.IsNullOrWhiteSpace(deliveryAddress) ? (object)DBNull.Value : deliveryAddress.Trim());
                    cmd.ExecuteNonQuery();
                }
            }
        }

        public void UpdateAssignedTechnician(int poId, int? technicianId, string technicianName)
        {
            using (SqlConnection conn = _db.GetConnection())
            {
                conn.Open();
                using (SqlCommand cmd = new SqlCommand("UPDATE PurchaseOrders SET AssignedTechnicianId=@techId, AssignedTechnicianName=@techName WHERE POID=@id", conn))
                {
                    cmd.Parameters.AddWithValue("@id", poId);
                    cmd.Parameters.AddWithValue("@techId", technicianId.HasValue ? (object)technicianId.Value : DBNull.Value);
                    cmd.Parameters.AddWithValue("@techName", string.IsNullOrWhiteSpace(technicianName) ? (object)DBNull.Value : technicianName.Trim());
                    cmd.ExecuteNonQuery();
                }
            }
        }

        public void UpdatePendingChargeStatus(int poId, bool created)
        {
            using (SqlConnection conn = _db.GetConnection())
            {
                conn.Open();
                using (SqlCommand cmd = new SqlCommand("UPDATE PurchaseOrders SET PendingChargeCreated=@created WHERE POID=@id", conn))
                {
                    cmd.Parameters.AddWithValue("@id", poId);
                    cmd.Parameters.AddWithValue("@created", created);
                    cmd.ExecuteNonQuery();
                }
            }
        }

        public void UpdateCreatedBy(int poId, int? userId, string userName, DateTime createdDate)
        {
            using (SqlConnection conn = _db.GetConnection())
            {
                conn.Open();
                using (SqlCommand cmd = new SqlCommand("UPDATE PurchaseOrders SET CreatedByUserId=@userId, CreatedByName=@userName, CreatedByDate=@createdDate WHERE POID=@id", conn))
                {
                    cmd.Parameters.AddWithValue("@id", poId);
                    cmd.Parameters.AddWithValue("@userId", userId.HasValue ? (object)userId.Value : DBNull.Value);
                    cmd.Parameters.AddWithValue("@userName", string.IsNullOrWhiteSpace(userName) ? (object)DBNull.Value : userName.Trim());
                    cmd.Parameters.AddWithValue("@createdDate", createdDate);
                    cmd.ExecuteNonQuery();
                }
            }
        }

        private static PurchaseOrder MapOrder(SqlDataReader r) => new PurchaseOrder
        {
            POID        = (int)r["POID"],
            VendorID    = (int)r["VendorID"],
            ClientID    = r["ClientID"] == DBNull.Value ? 0 : (int)r["ClientID"],
            SiteID      = r["SiteID"] == DBNull.Value ? 0 : (int)r["SiteID"],
            RelatedContractID = r["RelatedContractID"] == DBNull.Value ? (int?)null : (int)r["RelatedContractID"],
            RecommendedByBidID = r["RecommendedByBidID"] == DBNull.Value ? (int?)null : (int)r["RecommendedByBidID"],
            VendorName  = r["VendorName"] as string,
            VendorGSTIN = r["VendorGSTIN"] as string,
            ClientName  = r["ClientName"] as string,
            SiteName    = r["SiteName"] as string,
            PONumber    = r["PONumber"]   as string,
            PODate      = (DateTime)r["PODate"],
            PayByDate   = r["PayByDate"] == DBNull.Value ? (DateTime)r["PODate"] : (DateTime)r["PayByDate"],
            VendorInvoiceNumber = r["VendorInvoiceNumber"] == DBNull.Value ? null : r["VendorInvoiceNumber"] as string,
            LinkedToType = r["LinkedToType"] == DBNull.Value ? null : r["LinkedToType"] as string,
            LinkedToId   = r["LinkedToId"] == DBNull.Value ? (int?)null : (int)r["LinkedToId"],
            LinkedToLabel = r["LinkedToLabel"] == DBNull.Value ? null : r["LinkedToLabel"] as string,
            DeliveryMode = r["DeliveryMode"] == DBNull.Value ? "TechPickup" : r["DeliveryMode"] as string,
            AssignedTechnicianId = r["AssignedTechnicianId"] == DBNull.Value ? (int?)null : (int)r["AssignedTechnicianId"],
            AssignedTechnicianName = r["AssignedTechnicianName"] == DBNull.Value ? null : r["AssignedTechnicianName"] as string,
            DeliveryAddress = r["DeliveryAddress"] == DBNull.Value ? null : r["DeliveryAddress"] as string,
            AddToClientInvoice = r["AddToClientInvoice"] != DBNull.Value && Convert.ToBoolean(r["AddToClientInvoice"]),
            PendingChargeCreated = r["PendingChargeCreated"] != DBNull.Value && Convert.ToBoolean(r["PendingChargeCreated"]),
            ReceiptImagePath = r["ReceiptImagePath"] == DBNull.Value ? null : r["ReceiptImagePath"] as string,
            PdfPath = ResolvePurchaseOrderPdfPath(r["ReceiptImagePath"] == DBNull.Value ? null : r["ReceiptImagePath"] as string, r["Notes"] as string),
            PriceVarianceFlag = r["PriceVarianceFlag"] != DBNull.Value && Convert.ToBoolean(r["PriceVarianceFlag"]),
            CreatedByUserId = r["CreatedByUserId"] == DBNull.Value ? (int?)null : (int)r["CreatedByUserId"],
            CreatedByName = r["CreatedByName"] == DBNull.Value ? null : r["CreatedByName"] as string,
            CreatedByDate = r["CreatedByDate"] == DBNull.Value ? (DateTime?)null : (DateTime)r["CreatedByDate"],
            ModifiedByUserId = r["ModifiedByUserId"] == DBNull.Value ? (int?)null : (int)r["ModifiedByUserId"],
            ModifiedByName = r["ModifiedByName"] == DBNull.Value ? null : r["ModifiedByName"] as string,
            ModifiedDate = r["ModifiedDate"] == DBNull.Value ? (DateTime?)null : (DateTime)r["ModifiedDate"],
            TotalAmount = (decimal)r["TotalAmount"],
            PaidAmount  = PurchaseOrder.IsPaymentCompletedStatus(r["Status"] as string) ? (decimal)r["TotalAmount"] : (decimal)r["PaidAmount"],
            Status      = r["Status"]     as string,
            PaymentReference = r["PaymentReference"] == DBNull.Value ? null : r["PaymentReference"] as string,
            ComparisonNotes = r["ComparisonNotes"] as string,
            Notes       = r["Notes"]      as string,
            CreatedDate = (DateTime)r["CreatedDate"],
        };

        private static string ResolvePurchaseOrderPdfPath(string receiptImagePath, string notes)
        {
            if (IsPdfPath(receiptImagePath))
                return receiptImagePath.Trim();

            string sourcePath = ExtractArchiveSourcePath(notes);
            return IsPdfPath(sourcePath) ? sourcePath.Trim() : null;
        }

        private static string ExtractArchiveSourcePath(string notes)
        {
            if (string.IsNullOrWhiteSpace(notes))
                return null;

            Match match = Regex.Match(notes, @"Source\s+Path:\s*(?<path>.+?)(\s+\|\s+|$)", RegexOptions.IgnoreCase);
            return match.Success ? match.Groups["path"].Value.Trim() : null;
        }

        private static bool IsPdfPath(string path)
        {
            return !string.IsNullOrWhiteSpace(path)
                && string.Equals(Path.GetExtension(path.Trim()), ".pdf", StringComparison.OrdinalIgnoreCase);
        }

        private static PurchaseLineItem MapLine(SqlDataReader r) => new PurchaseLineItem
        {
            LineItemID  = (int)r["LineItemID"],
            POID        = (int)r["POID"],
            InventoryItemId = r["InventoryItemId"] == DBNull.Value ? (int?)null : (int)r["InventoryItemId"],
            Description = r["Description"] as string,
            HsnSacCode  = r["HsnSacCode"] == DBNull.Value ? string.Empty : r["HsnSacCode"] as string,
            Quantity    = (decimal)r["Quantity"],
            UOM         = r["UOM"] == DBNull.Value ? "Nos" : r["UOM"] as string,
            Rate        = (decimal)r["Rate"],
            GSTRate     = r["GSTRate"] == DBNull.Value ? 0m : (decimal)r["GSTRate"],
            CGSTRate    = r["CGSTRate"] == DBNull.Value ? 0m : (decimal)r["CGSTRate"],
            SGSTRate    = r["SGSTRate"] == DBNull.Value ? 0m : (decimal)r["SGSTRate"],
            IGSTRate    = r["IGSTRate"] == DBNull.Value ? 0m : (decimal)r["IGSTRate"],
            JobLink     = r["JobLink"] == DBNull.Value ? "General" : r["JobLink"] as string,
            LinkedWorkOrderId = r["LinkedWorkOrderId"] == DBNull.Value ? (int?)null : (int)r["LinkedWorkOrderId"],
            LinkedWorkOrderName = r["LinkedWorkOrderName"] == DBNull.Value ? null : r["LinkedWorkOrderName"] as string,
            PriceVariance = r["PriceVariance"] == DBNull.Value ? 0m : (decimal)r["PriceVariance"],
            Amount      = (decimal)r["Amount"],
        };
    }
}
