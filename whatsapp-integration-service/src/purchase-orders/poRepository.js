const fs = require("fs");
const path = require("path");
const sql = require("mssql");
const { getPool } = require("../db/database");

function toSqlDate(value) {
  return value instanceof Date && !Number.isNaN(value.getTime()) ? value : new Date();
}

function valueOrNull(value) {
  return value == null || value === "" ? null : value;
}

function ensureLogDir() {
  const dir = "C:\\HVAC_PRO_MSE\\LOGS";
  if (!fs.existsSync(dir)) {
    fs.mkdirSync(dir, { recursive: true });
  }
  return dir;
}

class PurchaseOrderRepository {
  async ensureSchema() {
    const pool = await getPool();
    const statements = [
      `IF COL_LENGTH('Vendors', 'DefaultCreditDays') IS NULL
         ALTER TABLE Vendors ADD DefaultCreditDays INT NOT NULL CONSTRAINT DF_Vendors_DefaultCreditDays DEFAULT 30;`,
      `IF COL_LENGTH('PurchaseOrders', 'VendorInvoiceNumber') IS NULL
         ALTER TABLE PurchaseOrders ADD VendorInvoiceNumber NVARCHAR(100) NULL;`,
      `IF COL_LENGTH('PurchaseOrders', 'LinkedToType') IS NULL
         ALTER TABLE PurchaseOrders ADD LinkedToType NVARCHAR(30) NULL;`,
      `IF COL_LENGTH('PurchaseOrders', 'LinkedToId') IS NULL
         ALTER TABLE PurchaseOrders ADD LinkedToId INT NULL;`,
      `IF COL_LENGTH('PurchaseOrders', 'DeliveryMode') IS NULL
         ALTER TABLE PurchaseOrders ADD DeliveryMode NVARCHAR(30) NOT NULL CONSTRAINT DF_PurchaseOrders_DeliveryMode DEFAULT('TechPickup');`,
      `IF COL_LENGTH('PurchaseOrders', 'AssignedTechnicianId') IS NULL
         ALTER TABLE PurchaseOrders ADD AssignedTechnicianId INT NULL;`,
      `IF COL_LENGTH('PurchaseOrders', 'AssignedTechnicianName') IS NULL
         ALTER TABLE PurchaseOrders ADD AssignedTechnicianName NVARCHAR(100) NULL;`,
      `IF COL_LENGTH('PurchaseOrders', 'DeliveryAddress') IS NULL
         ALTER TABLE PurchaseOrders ADD DeliveryAddress NVARCHAR(500) NULL;`,
      `IF COL_LENGTH('PurchaseOrders', 'AddToClientInvoice') IS NULL
         ALTER TABLE PurchaseOrders ADD AddToClientInvoice BIT NOT NULL CONSTRAINT DF_PurchaseOrders_AddToClientInvoice DEFAULT(0);`,
      `IF COL_LENGTH('PurchaseOrders', 'PendingChargeCreated') IS NULL
         ALTER TABLE PurchaseOrders ADD PendingChargeCreated BIT NOT NULL CONSTRAINT DF_PurchaseOrders_PendingChargeCreated DEFAULT(0);`,
      `IF COL_LENGTH('PurchaseOrders', 'ReceiptImagePath') IS NULL
         ALTER TABLE PurchaseOrders ADD ReceiptImagePath NVARCHAR(500) NULL;`,
      `IF COL_LENGTH('PurchaseOrders', 'PriceVarianceFlag') IS NULL
         ALTER TABLE PurchaseOrders ADD PriceVarianceFlag BIT NOT NULL CONSTRAINT DF_PurchaseOrders_PriceVarianceFlag DEFAULT(0);`,
      `IF COL_LENGTH('PurchaseOrders', 'CreatedByUserId') IS NULL
         ALTER TABLE PurchaseOrders ADD CreatedByUserId INT NULL;`,
      `IF COL_LENGTH('PurchaseOrders', 'CreatedByName') IS NULL
         ALTER TABLE PurchaseOrders ADD CreatedByName NVARCHAR(100) NULL;`,
      `IF COL_LENGTH('PurchaseOrders', 'CreatedByDate') IS NULL
         ALTER TABLE PurchaseOrders ADD CreatedByDate DATETIME NULL CONSTRAINT DF_PurchaseOrders_CreatedByDate DEFAULT(GETDATE());`,
      `IF COL_LENGTH('PurchaseOrders', 'PaymentReference') IS NULL
         ALTER TABLE PurchaseOrders ADD PaymentReference NVARCHAR(100) NULL;`,
      `IF COL_LENGTH('PurchaseOrders', 'ComparisonNotes') IS NULL
         ALTER TABLE PurchaseOrders ADD ComparisonNotes NVARCHAR(MAX) NULL;`,
      `IF COL_LENGTH('PurchaseOrders', 'Notes') IS NULL
         ALTER TABLE PurchaseOrders ADD Notes NVARCHAR(MAX) NULL;`,
      `IF COL_LENGTH('PurchaseOrders', 'Status') IS NULL
         ALTER TABLE PurchaseOrders ADD Status NVARCHAR(50) NOT NULL CONSTRAINT DF_PurchaseOrders_Status DEFAULT('Pending');`,
      `IF COL_LENGTH('PurchaseOrders', 'PayByDate') IS NULL
         ALTER TABLE PurchaseOrders ADD PayByDate DATETIME NOT NULL CONSTRAINT DF_PurchaseOrders_PayByDate DEFAULT(GETDATE());`,
      `IF COL_LENGTH('PurchaseOrders', 'VendorID') IS NULL
         ALTER TABLE PurchaseOrders ADD VendorID INT NOT NULL CONSTRAINT DF_PurchaseOrders_VendorID DEFAULT(1);`,
      `IF COL_LENGTH('PurchaseLineItems', 'InventoryItemId') IS NULL
         ALTER TABLE PurchaseLineItems ADD InventoryItemId INT NULL;`,
      `IF COL_LENGTH('PurchaseLineItems', 'Description') IS NULL
         ALTER TABLE PurchaseLineItems ADD Description NVARCHAR(500) NULL;`,
      `IF COL_LENGTH('PurchaseLineItems', 'HsnSacCode') IS NULL
         ALTER TABLE PurchaseLineItems ADD HsnSacCode NVARCHAR(50) NULL;`,
      `IF COL_LENGTH('PurchaseLineItems', 'UOM') IS NULL
         ALTER TABLE PurchaseLineItems ADD UOM NVARCHAR(30) NOT NULL CONSTRAINT DF_PurchaseLineItems_UOM DEFAULT('Nos');`,
      `IF COL_LENGTH('PurchaseLineItems', 'GSTRate') IS NULL
         ALTER TABLE PurchaseLineItems ADD GSTRate DECIMAL(5,2) NOT NULL CONSTRAINT DF_PurchaseLineItems_GSTRate DEFAULT(0);`,
      `IF COL_LENGTH('PurchaseLineItems', 'CGSTRate') IS NULL
         ALTER TABLE PurchaseLineItems ADD CGSTRate DECIMAL(5,2) NOT NULL CONSTRAINT DF_PurchaseLineItems_CGSTRate DEFAULT(0);`,
      `IF COL_LENGTH('PurchaseLineItems', 'SGSTRate') IS NULL
         ALTER TABLE PurchaseLineItems ADD SGSTRate DECIMAL(5,2) NOT NULL CONSTRAINT DF_PurchaseLineItems_SGSTRate DEFAULT(0);`,
      `IF COL_LENGTH('PurchaseLineItems', 'IGSTRate') IS NULL
         ALTER TABLE PurchaseLineItems ADD IGSTRate DECIMAL(5,2) NOT NULL CONSTRAINT DF_PurchaseLineItems_IGSTRate DEFAULT(0);`,
      `IF COL_LENGTH('PurchaseLineItems', 'JobLink') IS NULL
         ALTER TABLE PurchaseLineItems ADD JobLink NVARCHAR(50) NOT NULL CONSTRAINT DF_PurchaseLineItems_JobLink DEFAULT('General');`,
      `IF COL_LENGTH('PurchaseLineItems', 'LinkedWorkOrderId') IS NULL
         ALTER TABLE PurchaseLineItems ADD LinkedWorkOrderId INT NULL;`,
      `IF COL_LENGTH('PurchaseLineItems', 'LinkedWorkOrderName') IS NULL
         ALTER TABLE PurchaseLineItems ADD LinkedWorkOrderName NVARCHAR(200) NULL;`,
      `IF COL_LENGTH('PurchaseLineItems', 'PriceVariance') IS NULL
         ALTER TABLE PurchaseLineItems ADD PriceVariance DECIMAL(5,2) NOT NULL CONSTRAINT DF_PurchaseLineItems_PriceVariance DEFAULT(0);`,
      `IF COL_LENGTH('PurchaseLineItems', 'HistoricalRate') IS NULL
         ALTER TABLE PurchaseLineItems ADD HistoricalRate DECIMAL(12,2) NOT NULL CONSTRAINT DF_PurchaseLineItems_HistoricalRate DEFAULT(0);`,
      `IF COL_LENGTH('PurchaseOrderImportLog', 'ImportID') IS NULL
         BEGIN
           IF OBJECT_ID('PurchaseOrderImportLog', 'U') IS NULL
           CREATE TABLE PurchaseOrderImportLog (
             ImportID INT IDENTITY(1,1) PRIMARY KEY,
             SourceFileName NVARCHAR(260) NOT NULL,
             SourceFilePath NVARCHAR(500) NOT NULL,
             PONumber NVARCHAR(100) NULL,
             Status NVARCHAR(50) NOT NULL,
             Message NVARCHAR(MAX) NULL,
             RawText NVARCHAR(MAX) NULL,
             CreatedDate DATETIME NOT NULL DEFAULT(GETDATE())
           );
         END`
    ];

    for (const statement of statements) {
      await pool.request().query(statement);
    }

    try {
      await pool.request().query(`
        IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'UX_PurchaseOrders_PONumber' AND object_id = OBJECT_ID('PurchaseOrders'))
        BEGIN
          CREATE UNIQUE INDEX UX_PurchaseOrders_PONumber ON PurchaseOrders(PONumber) WHERE PONumber IS NOT NULL;
        END
      `);
    } catch (error) {
      this.logFailure("schema", "schema", null, `Unable to create unique purchase order index: ${error.message}`, "");
    }
  }

  async upsertVendor(vendor, tx = null) {
    const pool = tx || (await getPool());
    const request = pool.request ? pool.request() : new sql.Request(pool);

    const vendorName = String(vendor?.vendorName || vendor?.VendorName || "").trim();
    const gstin = String(vendor?.gstin || vendor?.GSTNumber || "").trim();
    const creditDays = Number(vendor?.defaultCreditDays || vendor?.DefaultCreditDays || 30);
    const vendorLookup = vendorName || gstin;

    if (!vendorLookup) {
      throw new Error("Vendor name is required.");
    }

    request.input("vendorName", sql.NVarChar(255), vendorName || vendorLookup);
    request.input("gstin", sql.NVarChar(20), valueOrNull(gstin));
    request.input("creditDays", sql.Int, Number.isFinite(creditDays) && creditDays > 0 ? creditDays : 30);
    request.input("pan", sql.NVarChar(20), valueOrNull(vendor?.PANNumber || vendor?.panNumber));
    request.input("phone", sql.NVarChar(20), valueOrNull(vendor?.Phone || vendor?.phone));
    request.input("email", sql.NVarChar(255), valueOrNull(vendor?.Email || vendor?.email));
    request.input("address", sql.NVarChar(sql.MAX), valueOrNull(vendor?.Address || vendor?.address));
    request.input("city", sql.NVarChar(100), valueOrNull(vendor?.City || vendor?.city));
    request.input("category", sql.NVarChar(100), valueOrNull(vendor?.Category || vendor?.category));

    const existing = await pool.request()
      .input("vendorName", sql.NVarChar(255), vendorName || vendorLookup)
      .input("gstin", sql.NVarChar(20), valueOrNull(gstin))
      .query(`
        SELECT TOP 1 VendorID
        FROM Vendors
        WHERE VendorName = @vendorName
           OR (@gstin IS NOT NULL AND GSTNumber = @gstin)
        ORDER BY VendorID
      `);

    if (existing.recordset.length > 0) {
      const vendorId = existing.recordset[0].VendorID;
      await pool.request()
        .input("vendorId", sql.Int, vendorId)
        .input("vendorName", sql.NVarChar(255), vendorName || vendorLookup)
        .input("gstin", sql.NVarChar(20), valueOrNull(gstin))
        .input("creditDays", sql.Int, Number.isFinite(creditDays) && creditDays > 0 ? creditDays : 30)
        .input("pan", sql.NVarChar(20), valueOrNull(vendor?.PANNumber || vendor?.panNumber))
        .input("phone", sql.NVarChar(20), valueOrNull(vendor?.Phone || vendor?.phone))
        .input("email", sql.NVarChar(255), valueOrNull(vendor?.Email || vendor?.email))
        .input("address", sql.NVarChar(sql.MAX), valueOrNull(vendor?.Address || vendor?.address))
        .input("city", sql.NVarChar(100), valueOrNull(vendor?.City || vendor?.city))
        .input("category", sql.NVarChar(100), valueOrNull(vendor?.Category || vendor?.category))
        .query(`
          UPDATE Vendors
          SET VendorName = @vendorName,
              GSTNumber = COALESCE(@gstin, GSTNumber),
              DefaultCreditDays = CASE WHEN @creditDays > 0 THEN @creditDays ELSE ISNULL(DefaultCreditDays, 30) END,
              PANNumber = COALESCE(@pan, PANNumber),
              Phone = COALESCE(@phone, Phone),
              Email = COALESCE(@email, Email),
              Address = COALESCE(@address, Address),
              City = COALESCE(@city, City),
              Category = COALESCE(@category, Category),
              IsActive = 1
          WHERE VendorID = @vendorId
        `);
      return vendorId;
    }

    const inserted = await pool.request()
      .input("vendorName", sql.NVarChar(255), vendorName || vendorLookup)
      .input("gstin", sql.NVarChar(20), valueOrNull(gstin))
      .input("creditDays", sql.Int, Number.isFinite(creditDays) && creditDays > 0 ? creditDays : 30)
      .input("pan", sql.NVarChar(20), valueOrNull(vendor?.PANNumber || vendor?.panNumber))
      .input("phone", sql.NVarChar(20), valueOrNull(vendor?.Phone || vendor?.phone))
      .input("email", sql.NVarChar(255), valueOrNull(vendor?.Email || vendor?.email))
      .input("address", sql.NVarChar(sql.MAX), valueOrNull(vendor?.Address || vendor?.address))
      .input("city", sql.NVarChar(100), valueOrNull(vendor?.City || vendor?.city))
      .input("category", sql.NVarChar(100), valueOrNull(vendor?.Category || vendor?.category))
      .query(`
        INSERT INTO Vendors (VendorName, GSTNumber, DefaultCreditDays, PANNumber, Phone, Email, Address, City, Category, IsActive)
        VALUES (@vendorName, @gstin, @creditDays, @pan, @phone, @email, @address, @city, @category, 1);
        SELECT CAST(SCOPE_IDENTITY() AS INT) AS VendorID;
      `);

    return inserted.recordset[0].VendorID;
  }

  async findByPoNumber(poNumber, tx = null) {
    if (!poNumber) {
      return null;
    }
    const pool = tx || (await getPool());
    const request = pool.request ? pool.request() : new sql.Request(pool);
    const result = await request.input("poNumber", sql.NVarChar(100), poNumber.trim()).query(`
      SELECT TOP 1 POID
      FROM PurchaseOrders
      WHERE PONumber = @poNumber
      ORDER BY POID DESC
    `);
    return result.recordset.length > 0 ? result.recordset[0].POID : null;
  }

  async getById(id) {
    const pool = await getPool();
    const order = await pool.request()
      .input("id", sql.Int, id)
      .query(`
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
        WHERE p.POID = @id
      `);

    if (order.recordset.length === 0) {
      return null;
    }

    const lineItems = await pool.request()
      .input("id", sql.Int, id)
      .query(`
        SELECT *
        FROM PurchaseLineItems
        WHERE POID = @id
        ORDER BY LineItemID
      `);

    return {
      purchaseOrder: order.recordset[0],
      lineItems: lineItems.recordset
    };
  }

  async list(filters = {}) {
    const pool = await getPool();
    const conditions = [];
    const request = pool.request();

    if (filters.status) {
      conditions.push(`p.Status = @status`);
      request.input("status", sql.NVarChar(50), filters.status);
    }

    if (filters.vendor) {
      conditions.push(`(v.VendorName LIKE @vendor OR CAST(p.VendorID AS NVARCHAR(20)) = @vendorId)`);
      request.input("vendor", sql.NVarChar(255), `%${filters.vendor}%`);
      request.input("vendorId", sql.NVarChar(20), String(filters.vendor).trim());
    }

    if (filters.fromDate) {
      conditions.push(`p.PODate >= @fromDate`);
      request.input("fromDate", sql.DateTime, toSqlDate(filters.fromDate));
    }

    if (filters.toDate) {
      conditions.push(`p.PODate <= @toDate`);
      request.input("toDate", sql.DateTime, toSqlDate(filters.toDate));
    }

    if (filters.unpaidOnly) {
      conditions.push(`ISNULL(p.PaidAmount, 0) < ISNULL(p.TotalAmount, 0)`);
    }

    const query = `
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
      ${conditions.length > 0 ? "WHERE " + conditions.join(" AND ") : ""}
      ORDER BY CASE WHEN ISNULL(p.PaidAmount, 0) < ISNULL(p.TotalAmount, 0) THEN 0 ELSE 1 END,
               p.PayByDate ASC,
               p.PODate DESC,
               p.POID DESC
    `;

    const result = await request.query(query);
    return result.recordset;
  }

  async replaceLineItems(poId, lineItems, tx = null) {
    const pool = tx || (await getPool());
    const request = pool.request ? pool.request() : new sql.Request(pool);
    await request.input("poId", sql.Int, poId).query(`DELETE FROM PurchaseLineItems WHERE POID = @poId`);

    for (const item of lineItems || []) {
      await pool.request()
        .input("poId", sql.Int, poId)
        .input("inventoryItemId", sql.Int, item.inventoryItemId || item.InventoryItemId || null)
        .input("description", sql.NVarChar(500), item.description || item.Description || "")
        .input("hsnSacCode", sql.NVarChar(50), valueOrNull(item.hsnSacCode || item.HsnSacCode))
        .input("quantity", sql.Decimal(18, 2), Number(item.quantity || item.Quantity || 1))
        .input("uom", sql.NVarChar(30), item.uom || item.UOM || "Nos")
        .input("rate", sql.Decimal(18, 2), Number(item.rate || item.Rate || 0))
        .input("gstRate", sql.Decimal(18, 2), Number(item.gstRate || item.GSTRate || 0))
        .input("cgstRate", sql.Decimal(18, 2), Number(item.cgstRate || item.CGSTRate || 0))
        .input("sgstRate", sql.Decimal(18, 2), Number(item.sgstRate || item.SGSTRate || 0))
        .input("igstRate", sql.Decimal(18, 2), Number(item.igstRate || item.IGSTRate || 0))
        .input("jobLink", sql.NVarChar(50), item.jobLink || item.JobLink || "General")
        .input("linkedWorkOrderId", sql.Int, item.linkedWorkOrderId || item.LinkedWorkOrderId || null)
        .input("linkedWorkOrderName", sql.NVarChar(200), valueOrNull(item.linkedWorkOrderName || item.LinkedWorkOrderName))
        .input("priceVariance", sql.Decimal(18, 2), Number(item.priceVariance || item.PriceVariance || 0))
        .input("historicalRate", sql.Decimal(18, 2), Number(item.historicalRate || item.HistoricalRate || 0))
        .input("amount", sql.Decimal(18, 2), Number(item.amount || item.Amount || 0))
        .query(`
          INSERT INTO PurchaseLineItems
            (POID, InventoryItemId, Description, HsnSacCode, Quantity, UOM, Rate, GSTRate, CGSTRate, SGSTRate, IGSTRate, JobLink, LinkedWorkOrderId, LinkedWorkOrderName, PriceVariance, HistoricalRate, Amount)
          VALUES
            (@poId, @inventoryItemId, @description, @hsnSacCode, @quantity, @uom, @rate, @gstRate, @cgstRate, @sgstRate, @igstRate, @jobLink, @linkedWorkOrderId, @linkedWorkOrderName, @priceVariance, @historicalRate, @amount)
        `);
    }
  }

  async upsertPurchaseOrder(payload) {
    await this.ensureSchema();
    const pool = await getPool();
    const transaction = new sql.Transaction(pool);
    await transaction.begin();

    try {
      const vendorId = await this.upsertVendor(payload.vendor, transaction);
      const poNumber = String(payload.purchaseOrder?.poNumber || payload.poNumber || "").trim();
      if (!poNumber) {
        throw new Error("Purchase order number is missing.");
      }

      const purchaseDate = toSqlDate(payload.purchaseOrder?.purchaseDate || payload.purchaseDate);
      const payByDate = toSqlDate(payload.purchaseOrder?.payByDate || payload.payByDate);
      const totalAmount = Number(payload.purchaseOrder?.amount ?? payload.amount ?? 0);
      const existingId = await this.findByPoNumber(poNumber, transaction);

      let poId = existingId;
      const request = transaction.request();
      if (existingId) {
        await request
          .input("poId", sql.Int, existingId)
          .input("vendorId", sql.Int, vendorId)
          .input("vendorInvoiceNumber", sql.NVarChar(100), valueOrNull(payload.purchaseOrder?.vendorInvoiceNumber || payload.vendorInvoiceNumber))
          .input("linkedToType", sql.NVarChar(30), valueOrNull(payload.purchaseOrder?.linkedToType || payload.linkedToType || "General"))
          .input("linkedToId", sql.Int, payload.purchaseOrder?.linkedToId || payload.linkedToId || null)
          .input("poNumber", sql.NVarChar(100), poNumber)
          .input("purchaseDate", sql.DateTime, purchaseDate)
          .input("payByDate", sql.DateTime, payByDate)
          .input("status", sql.NVarChar(50), payload.purchaseOrder?.status || payload.status || "Received")
          .input("amount", sql.Decimal(18, 2), totalAmount)
          .input("notes", sql.NVarChar(sql.MAX), valueOrNull(payload.purchaseOrder?.notes || payload.notes))
          .input("deliveryMode", sql.NVarChar(30), payload.purchaseOrder?.deliveryMode || payload.deliveryMode || "TechPickup")
          .input("deliveryAddress", sql.NVarChar(500), valueOrNull(payload.purchaseOrder?.deliveryAddress || payload.deliveryAddress))
          .input("paymentReference", sql.NVarChar(100), valueOrNull(payload.purchaseOrder?.paymentReference || payload.paymentReference))
          .input("comparisonNotes", sql.NVarChar(sql.MAX), valueOrNull(payload.purchaseOrder?.comparisonNotes || payload.comparisonNotes))
          .input("createdByUserId", sql.Int, payload.purchaseOrder?.createdByUserId || payload.createdByUserId || null)
          .input("createdByName", sql.NVarChar(100), valueOrNull(payload.purchaseOrder?.createdByName || payload.createdByName))
          .input("createdByDate", sql.DateTime, payload.purchaseOrder?.createdByDate || payload.createdByDate || new Date())
          .query(`
            UPDATE PurchaseOrders
            SET VendorID = @vendorId,
                VendorInvoiceNumber = @vendorInvoiceNumber,
                LinkedToType = @linkedToType,
                LinkedToId = @linkedToId,
                PONumber = @poNumber,
                PODate = @purchaseDate,
                PayByDate = @payByDate,
                Status = @status,
                TotalAmount = @amount,
                Notes = @notes,
                DeliveryMode = @deliveryMode,
                DeliveryAddress = @deliveryAddress,
                PaymentReference = @paymentReference,
                ComparisonNotes = @comparisonNotes,
                CreatedByUserId = @createdByUserId,
                CreatedByName = @createdByName,
                CreatedByDate = @createdByDate
            WHERE POID = @poId
          `);
      } else {
        const inserted = await request
          .input("vendorId", sql.Int, vendorId)
          .input("vendorInvoiceNumber", sql.NVarChar(100), valueOrNull(payload.purchaseOrder?.vendorInvoiceNumber || payload.vendorInvoiceNumber))
          .input("linkedToType", sql.NVarChar(30), valueOrNull(payload.purchaseOrder?.linkedToType || payload.linkedToType || "General"))
          .input("linkedToId", sql.Int, payload.purchaseOrder?.linkedToId || payload.linkedToId || null)
          .input("poNumber", sql.NVarChar(100), poNumber)
          .input("purchaseDate", sql.DateTime, purchaseDate)
          .input("payByDate", sql.DateTime, payByDate)
          .input("status", sql.NVarChar(50), payload.purchaseOrder?.status || payload.status || "Received")
          .input("amount", sql.Decimal(18, 2), totalAmount)
          .input("notes", sql.NVarChar(sql.MAX), valueOrNull(payload.purchaseOrder?.notes || payload.notes))
          .input("deliveryMode", sql.NVarChar(30), payload.purchaseOrder?.deliveryMode || payload.deliveryMode || "TechPickup")
          .input("deliveryAddress", sql.NVarChar(500), valueOrNull(payload.purchaseOrder?.deliveryAddress || payload.deliveryAddress))
          .input("paymentReference", sql.NVarChar(100), valueOrNull(payload.purchaseOrder?.paymentReference || payload.paymentReference))
          .input("comparisonNotes", sql.NVarChar(sql.MAX), valueOrNull(payload.purchaseOrder?.comparisonNotes || payload.comparisonNotes))
          .input("createdByUserId", sql.Int, payload.purchaseOrder?.createdByUserId || payload.createdByUserId || null)
          .input("createdByName", sql.NVarChar(100), valueOrNull(payload.purchaseOrder?.createdByName || payload.createdByName))
          .input("createdByDate", sql.DateTime, payload.purchaseOrder?.createdByDate || payload.createdByDate || new Date())
          .query(`
            INSERT INTO PurchaseOrders
              (VendorID, VendorInvoiceNumber, LinkedToType, LinkedToId, PONumber, PODate, PayByDate, TotalAmount, PaidAmount, Status, Notes, DeliveryMode, DeliveryAddress, PaymentReference, ComparisonNotes, CreatedByUserId, CreatedByName, CreatedByDate)
            VALUES
              (@vendorId, @vendorInvoiceNumber, @linkedToType, @linkedToId, @poNumber, @purchaseDate, @payByDate, @amount, 0, @status, @notes, @deliveryMode, @deliveryAddress, @paymentReference, @comparisonNotes, @createdByUserId, @createdByName, @createdByDate);
            SELECT CAST(SCOPE_IDENTITY() AS INT) AS POID;
          `);
        poId = inserted.recordset[0].POID;
      }

      await transaction.request()
        .input("poId", sql.Int, poId)
        .query(`DELETE FROM PurchaseLineItems WHERE POID = @poId`);

      await this.replaceLineItems(poId, payload.lineItems || payload.purchaseOrder?.lineItems || [], transaction);

      await transaction.commit();
      return poId;
    } catch (error) {
      await transaction.rollback();
      throw error;
    }
  }

  async delete(id) {
    const pool = await getPool();
    const transaction = new sql.Transaction(pool);
    await transaction.begin();

    try {
      await transaction.request().input("id", sql.Int, id).query(`DELETE FROM PurchaseLineItems WHERE POID = @id`);
      await transaction.request().input("id", sql.Int, id).query(`DELETE FROM PurchaseOrders WHERE POID = @id`);
      await transaction.commit();
    } catch (error) {
      await transaction.rollback();
      throw error;
    }
  }

  async logFailure(sourceFileName, sourceFilePath, poNumber, message, rawText = "") {
    const pool = await getPool();
    ensureLogDir();
    const line = `${new Date().toISOString()} | ${sourceFileName} | ${poNumber || ""} | ${message}\n`;
    fs.appendFileSync(path.join("C:\\HVAC_PRO_MSE\\LOGS", "po-import.log"), line, "utf8");

    try {
      await pool.request()
        .input("sourceFileName", sql.NVarChar(260), sourceFileName || "")
        .input("sourceFilePath", sql.NVarChar(500), sourceFilePath || "")
        .input("poNumber", sql.NVarChar(100), valueOrNull(poNumber))
        .input("status", sql.NVarChar(50), "Failed")
        .input("message", sql.NVarChar(sql.MAX), message || "")
        .input("rawText", sql.NVarChar(sql.MAX), rawText || "")
        .query(`
          INSERT INTO PurchaseOrderImportLog
            (SourceFileName, SourceFilePath, PONumber, Status, Message, RawText, CreatedDate)
          VALUES
            (@sourceFileName, @sourceFilePath, @poNumber, @status, @message, @rawText, GETDATE())
        `);
    } catch {
      // Logging should never block ingestion.
    }
  }
}

module.exports = {
  PurchaseOrderRepository
};
