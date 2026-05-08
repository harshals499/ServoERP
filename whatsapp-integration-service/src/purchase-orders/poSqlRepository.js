const fs = require("fs");
const path = require("path");
const { queryJson, queryScalar, quoteSql, runSql } = require("../db/sqlcmd");

function toIsoSql(dateValue) {
  const value = dateValue instanceof Date && !Number.isNaN(dateValue.getTime()) ? dateValue : new Date();
  return value.toISOString().replace("T", " ").slice(0, 19);
}

function buildLineItemInsert(poIdExpression, item) {
  return `
    INSERT INTO PurchaseLineItems
      (POID, InventoryItemId, Description, HsnSacCode, Quantity, UOM, Rate, GSTRate, CGSTRate, SGSTRate, IGSTRate, JobLink, LinkedWorkOrderId, LinkedWorkOrderName, PriceVariance, HistoricalRate, Amount)
    VALUES
      (${poIdExpression},
       ${item.inventoryItemId != null ? Number(item.inventoryItemId) : "NULL"},
       ${quoteSql(item.description || item.Description || "")},
       ${quoteSql(item.hsnSacCode || item.HsnSacCode)},
       ${Number(item.quantity || item.Quantity || 1)},
       ${quoteSql(item.uom || item.UOM || "Nos")},
       ${Number(item.rate || item.Rate || 0)},
       ${Number(item.gstRate || item.GSTRate || 0)},
       ${Number(item.cgstRate || item.CGSTRate || 0)},
       ${Number(item.sgstRate || item.SGSTRate || 0)},
       ${Number(item.igstRate || item.IGSTRate || 0)},
       ${quoteSql(item.jobLink || item.JobLink || "General")},
       ${item.linkedWorkOrderId != null ? Number(item.linkedWorkOrderId) : "NULL"},
       ${quoteSql(item.linkedWorkOrderName || item.LinkedWorkOrderName)},
       ${Number(item.priceVariance || item.PriceVariance || 0)},
       ${Number(item.historicalRate || item.HistoricalRate || 0)},
       ${Number(item.amount || item.Amount || 0)}
      );`;
}

class PurchaseOrderSqlRepository {
  async ensureSchema() {
    const schemaPath = path.resolve(__dirname, "..", "..", "schema", "purchase_orders.sql");
    const sql = fs.readFileSync(schemaPath, "utf8");
    await runSql(sql);
  }

  async upsertVendor(vendor) {
    const vendorName = String(vendor?.vendorName || vendor?.VendorName || "").trim();
    const gstin = String(vendor?.gstin || vendor?.GSTNumber || "").trim();
    const creditDays = Number(vendor?.defaultCreditDays || vendor?.DefaultCreditDays || 30) || 30;

    if (!vendorName && !gstin) {
      throw new Error("Vendor name is required.");
    }

    const existingId = await queryScalar(`
      SELECT COALESCE((
        SELECT TOP 1 VendorID
        FROM Vendors
        WHERE VendorName = ${quoteSql(vendorName || gstin)}
           OR (${gstin ? `GSTNumber = ${quoteSql(gstin)}` : "1 = 0"})
        ORDER BY VendorID
      ), 0) AS VendorID;
    `);

    if (Number(existingId) > 0) {
      await runSql(`
        UPDATE Vendors
        SET VendorName = ${quoteSql(vendorName || gstin)},
            GSTNumber = COALESCE(${quoteSql(gstin)}, GSTNumber),
            DefaultCreditDays = ${creditDays > 0 ? creditDays : 30},
            PANNumber = COALESCE(${quoteSql(vendor?.PANNumber || vendor?.panNumber)}, PANNumber),
            Phone = COALESCE(${quoteSql(vendor?.Phone || vendor?.phone)}, Phone),
            Email = COALESCE(${quoteSql(vendor?.Email || vendor?.email)}, Email),
            Address = COALESCE(${quoteSql(vendor?.Address || vendor?.address)}, Address),
            City = COALESCE(${quoteSql(vendor?.City || vendor?.city)}, City),
            Category = COALESCE(${quoteSql(vendor?.Category || vendor?.category)}, Category),
            IsActive = 1
        WHERE VendorID = ${Number(existingId)};
      `);
      return Number(existingId);
    }

    const inserted = await queryScalar(`
      INSERT INTO Vendors (VendorName, GSTNumber, DefaultCreditDays, PANNumber, Phone, Email, Address, City, Category, IsActive)
      VALUES (
        ${quoteSql(vendorName || gstin)},
        ${quoteSql(gstin)},
        ${creditDays > 0 ? creditDays : 30},
        ${quoteSql(vendor?.PANNumber || vendor?.panNumber)},
        ${quoteSql(vendor?.Phone || vendor?.phone)},
        ${quoteSql(vendor?.Email || vendor?.email)},
        ${quoteSql(vendor?.Address || vendor?.address)},
        ${quoteSql(vendor?.City || vendor?.city)},
        ${quoteSql(vendor?.Category || vendor?.category)},
        1
      );
      SELECT CAST(SCOPE_IDENTITY() AS INT) AS VendorID;
    `);

    return Number(inserted);
  }

  async findByPoNumber(poNumber) {
    if (!poNumber) {
      return null;
    }
    const value = await queryScalar(`
      SELECT COALESCE((
        SELECT TOP 1 POID
        FROM PurchaseOrders
        WHERE PONumber = ${quoteSql(poNumber)}
        ORDER BY POID DESC
      ), 0) AS POID;
    `);
    return Number(value) > 0 ? Number(value) : null;
  }

  async getById(id) {
    const purchaseOrder = await queryJson(`
      SELECT (
        SELECT TOP 1
          p.*, v.VendorName, v.GSTNumber AS VendorGSTIN, c.CompanyName AS ClientName, s.SiteName,
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
        WHERE p.POID = ${Number(id)}
        FOR JSON PATH, WITHOUT_ARRAY_WRAPPER, INCLUDE_NULL_VALUES
      ) AS JsonResult;
    `);

    if (!purchaseOrder) {
      return null;
    }

    const lineItems = await queryJson(`
      SELECT (
        SELECT *
        FROM PurchaseLineItems
        WHERE POID = ${Number(id)}
        ORDER BY LineItemID
        FOR JSON PATH, INCLUDE_NULL_VALUES
      ) AS JsonResult;
    `);

    return {
      purchaseOrder,
      lineItems: Array.isArray(lineItems) ? lineItems : (lineItems ? [lineItems] : [])
    };
  }

  async list(filters = {}) {
    const conditions = [];

    if (filters.status) {
      conditions.push(`p.Status = ${quoteSql(filters.status)}`);
    }

    if (filters.vendor) {
      const vendor = String(filters.vendor).trim();
      conditions.push(`(v.VendorName LIKE ${quoteSql(`%${vendor}%`)} OR CAST(p.VendorID AS NVARCHAR(20)) = ${quoteSql(vendor)})`);
    }

    if (filters.fromDate) {
      conditions.push(`p.PODate >= ${quoteSql(new Date(filters.fromDate))}`);
    }

    if (filters.toDate) {
      conditions.push(`p.PODate <= ${quoteSql(new Date(filters.toDate))}`);
    }

    if (filters.unpaidOnly) {
      conditions.push(`ISNULL(p.PaidAmount, 0) < ISNULL(p.TotalAmount, 0)`);
    }

    const where = conditions.length > 0 ? `WHERE ${conditions.join(" AND ")}` : "";
    const rows = await queryJson(`
      SELECT (
        SELECT
          p.*, v.VendorName, v.GSTNumber AS VendorGSTIN, c.CompanyName AS ClientName, s.SiteName,
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
        ${where}
        ORDER BY CASE WHEN ISNULL(p.PaidAmount, 0) < ISNULL(p.TotalAmount, 0) THEN 0 ELSE 1 END,
                 p.PayByDate ASC,
                 p.PODate DESC,
                 p.POID DESC
        FOR JSON PATH, INCLUDE_NULL_VALUES
      ) AS JsonResult;
    `);

    return Array.isArray(rows) ? rows : (rows ? [rows] : []);
  }

  async replaceLineItems(poId, lineItems) {
    const items = Array.isArray(lineItems) ? lineItems : [];
    let batch = `DELETE FROM PurchaseLineItems WHERE POID = ${Number(poId)};`;
    for (const item of items) {
      batch += "\n" + buildLineItemInsert(poId, item);
    }
    await runSql(batch);
  }

  async upsertPurchaseOrder(payload) {
    await this.ensureSchema();
    const vendorId = await this.upsertVendor(payload.vendor);
    const poNumber = String(payload.purchaseOrder?.poNumber || payload.poNumber || "").trim();

    if (!poNumber) {
      throw new Error("Purchase order number is missing.");
    }

    const purchaseDate = payload.purchaseOrder?.purchaseDate || payload.purchaseDate || new Date();
    const payByDate = payload.purchaseOrder?.payByDate || payload.payByDate || purchaseDate;
    const totalAmount = Number(payload.purchaseOrder?.amount ?? payload.amount ?? 0);
    const status = String(payload.purchaseOrder?.status || payload.status || "Received");
    const notes = payload.purchaseOrder?.notes || payload.notes || "";
    const lineItems = Array.isArray(payload.lineItems) ? payload.lineItems : (Array.isArray(payload.purchaseOrder?.lineItems) ? payload.purchaseOrder.lineItems : []);

    let batch = `
      BEGIN TRY
        BEGIN TRAN;
        DECLARE @PoId INT = (
          SELECT TOP 1 POID
          FROM PurchaseOrders
          WHERE PONumber = ${quoteSql(poNumber)}
          ORDER BY POID DESC
        );

        IF @PoId IS NULL
        BEGIN
          INSERT INTO PurchaseOrders
            (VendorID, VendorInvoiceNumber, LinkedToType, LinkedToId, PONumber, PODate, PayByDate, TotalAmount, PaidAmount, Status, Notes, DeliveryMode, DeliveryAddress, PaymentReference, ComparisonNotes, CreatedByUserId, CreatedByName, CreatedByDate)
          VALUES
            (${vendorId},
             ${quoteSql(payload.purchaseOrder?.vendorInvoiceNumber || payload.vendorInvoiceNumber)},
             ${quoteSql(payload.purchaseOrder?.linkedToType || payload.linkedToType || "General")},
             ${payload.purchaseOrder?.linkedToId != null || payload.linkedToId != null ? Number(payload.purchaseOrder?.linkedToId ?? payload.linkedToId) : "NULL"},
             ${quoteSql(poNumber)},
             ${quoteSql(purchaseDate)},
             ${quoteSql(payByDate)},
             ${totalAmount},
             0,
             ${quoteSql(status)},
             ${quoteSql(notes)},
             ${quoteSql(payload.purchaseOrder?.deliveryMode || payload.deliveryMode || "TechPickup")},
             ${quoteSql(payload.purchaseOrder?.deliveryAddress || payload.deliveryAddress)},
             ${quoteSql(payload.purchaseOrder?.paymentReference || payload.paymentReference)},
             ${quoteSql(payload.purchaseOrder?.comparisonNotes || payload.comparisonNotes)},
             ${payload.purchaseOrder?.createdByUserId != null || payload.createdByUserId != null ? Number(payload.purchaseOrder?.createdByUserId ?? payload.createdByUserId) : "NULL"},
             ${quoteSql(payload.purchaseOrder?.createdByName || payload.createdByName)},
             ${quoteSql(payload.purchaseOrder?.createdByDate || payload.createdByDate || new Date())}
          );
          SET @PoId = CAST(SCOPE_IDENTITY() AS INT);
        END
        ELSE
        BEGIN
          UPDATE PurchaseOrders
          SET VendorID = ${vendorId},
              VendorInvoiceNumber = ${quoteSql(payload.purchaseOrder?.vendorInvoiceNumber || payload.vendorInvoiceNumber)},
              LinkedToType = ${quoteSql(payload.purchaseOrder?.linkedToType || payload.linkedToType || "General")},
              LinkedToId = ${payload.purchaseOrder?.linkedToId != null || payload.linkedToId != null ? Number(payload.purchaseOrder?.linkedToId ?? payload.linkedToId) : "NULL"},
              PONumber = ${quoteSql(poNumber)},
              PODate = ${quoteSql(purchaseDate)},
              PayByDate = ${quoteSql(payByDate)},
              TotalAmount = ${totalAmount},
              Status = ${quoteSql(status)},
              Notes = ${quoteSql(notes)},
              DeliveryMode = ${quoteSql(payload.purchaseOrder?.deliveryMode || payload.deliveryMode || "TechPickup")},
              DeliveryAddress = ${quoteSql(payload.purchaseOrder?.deliveryAddress || payload.deliveryAddress)},
              PaymentReference = ${quoteSql(payload.purchaseOrder?.paymentReference || payload.paymentReference)},
              ComparisonNotes = ${quoteSql(payload.purchaseOrder?.comparisonNotes || payload.comparisonNotes)},
              CreatedByUserId = ${payload.purchaseOrder?.createdByUserId != null || payload.createdByUserId != null ? Number(payload.purchaseOrder?.createdByUserId ?? payload.createdByUserId) : "NULL"},
              CreatedByName = ${quoteSql(payload.purchaseOrder?.createdByName || payload.createdByName)},
              CreatedByDate = ${quoteSql(payload.purchaseOrder?.createdByDate || payload.createdByDate || new Date())}
          WHERE POID = @PoId;
          DELETE FROM PurchaseLineItems WHERE POID = @PoId;
        END
    `;

    for (const item of lineItems) {
    batch += "\n" + buildLineItemInsert("@PoId", item);
    }

    batch += `
        COMMIT;
        SELECT @PoId AS POID;
      END TRY
      BEGIN CATCH
        IF @@TRANCOUNT > 0 ROLLBACK;
        THROW;
      END CATCH
    `;

    const poId = await queryScalar(batch);
    return Number(poId);
  }

  async delete(id) {
    await runSql(`
      BEGIN TRY
        BEGIN TRAN;
        DELETE FROM PurchaseLineItems WHERE POID = ${Number(id)};
        DELETE FROM PurchaseOrders WHERE POID = ${Number(id)};
        COMMIT;
      END TRY
      BEGIN CATCH
        IF @@TRANCOUNT > 0 ROLLBACK;
        THROW;
      END CATCH
    `);
  }

  async logFailure(sourceFileName, sourceFilePath, poNumber, message, rawText = "") {
    const logLine = `${new Date().toISOString()} | ${sourceFileName} | ${poNumber || ""} | ${message}\n`;
    const logFile = path.join("C:\\HVAC_PRO_MSE\\LOGS", "po-import.log");
    fs.mkdirSync(path.dirname(logFile), { recursive: true });
    fs.appendFileSync(logFile, logLine, "utf8");

    try {
      await runSql(`
        INSERT INTO PurchaseOrderImportLog
          (SourceFileName, SourceFilePath, PONumber, Status, Message, RawText, CreatedDate)
        VALUES
          (${quoteSql(sourceFileName || "")},
           ${quoteSql(sourceFilePath || "")},
           ${quoteSql(poNumber)},
           'Failed',
           ${quoteSql(message || "")},
           ${quoteSql(rawText || "")},
           GETDATE());
      `);
    } catch {
      // Logging should never block ingestion.
    }
  }
}

module.exports = {
  PurchaseOrderSqlRepository
};
