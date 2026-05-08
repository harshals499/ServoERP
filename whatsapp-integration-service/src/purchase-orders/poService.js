const fs = require("fs");
const path = require("path");
const liveBridge = require("../liveBridge");
const { po } = require("../config");
const { parsePurchaseOrderPdf } = require("./poParser");
const { PurchaseOrderSqlRepository } = require("./poSqlRepository");

function ensureLogFile() {
  const logDir = "C:\\HVAC_PRO_MSE\\LOGS";
  if (!fs.existsSync(logDir)) {
    fs.mkdirSync(logDir, { recursive: true });
  }
  const logFile = path.join(logDir, "po-import.log");
  if (!fs.existsSync(logFile)) {
    fs.writeFileSync(logFile, "", "utf8");
  }
  return logFile;
}

function formatDate(value) {
  if (!(value instanceof Date) || Number.isNaN(value.getTime())) {
    return null;
  }
  return value.toISOString();
}

function computeAgeDays(dateValue) {
  if (!(dateValue instanceof Date) || Number.isNaN(dateValue.getTime())) {
    return 0;
  }

  const start = new Date(dateValue.getFullYear(), dateValue.getMonth(), dateValue.getDate());
  const today = new Date();
  const end = new Date(today.getFullYear(), today.getMonth(), today.getDate());
  return Math.max(0, Math.floor((end - start) / (24 * 60 * 60 * 1000)));
}

class PurchaseOrderService {
  constructor() {
    this.repository = new PurchaseOrderSqlRepository();
    ensureLogFile();
  }

  async ensureReady() {
    await this.repository.ensureSchema();
  }

  async importPdfFile(filePath) {
    await this.ensureReady();
    const parsed = await parsePurchaseOrderPdf(filePath);

    const payload = {
      vendor: parsed.vendor,
      purchaseOrder: {
        ...parsed.purchaseOrder,
        purchaseDate: parsed.purchaseOrder.purchaseDate,
        payByDate: parsed.purchaseOrder.payByDate,
        notes: parsed.purchaseOrder.notes,
        lineItems: parsed.lineItems
      },
      lineItems: parsed.lineItems,
      sourceFileName: parsed.sourceFileName,
      sourceFilePath: parsed.sourceFilePath
    };

    const missing = [];
    if (!payload.purchaseOrder.poNumber) missing.push("PO number");
    if (!payload.vendor.vendorName) missing.push("vendor name");
    if (!payload.purchaseOrder.amount || payload.purchaseOrder.amount <= 0) missing.push("amount");
    if (!payload.purchaseOrder.purchaseDate) missing.push("purchase date");

    const poId = await this.repository.upsertPurchaseOrder(payload);
    let result = null;
    try {
      result = await this.getById(poId);
    } catch (error) {
      fs.appendFileSync(
        path.join("C:\\HVAC_PRO_MSE\\LOGS", "po-import.log"),
        `${new Date().toISOString()} | READBACK_WARN | ${parsed.purchaseOrder.poNumber} | ${parsed.sourceFileName} | ${error.message}\n`,
        "utf8"
      );
    }
    const response = {
      ok: true,
      poId,
      sourceFileName: parsed.sourceFileName,
      sourceFilePath: parsed.sourceFilePath,
      extracted: {
        vendorName: parsed.vendor.vendorName,
        vendorGstin: parsed.vendor.gstin,
        poNumber: parsed.purchaseOrder.poNumber,
        vendorInvoiceNumber: parsed.purchaseOrder.vendorInvoiceNumber,
        purchaseDate: formatDate(parsed.purchaseOrder.purchaseDate),
        payByDate: formatDate(parsed.purchaseOrder.payByDate),
        category: parsed.purchaseOrder.category,
        status: parsed.purchaseOrder.status,
        amount: parsed.purchaseOrder.amount,
        notes: parsed.purchaseOrder.notes,
        lineItems: parsed.lineItems
      },
      warnings: missing.length > 0 ? [`Missing ${missing.join(", ")}`] : [],
      reviewRequired: missing.length > 0
    };

    fs.appendFileSync(
      path.join("C:\\HVAC_PRO_MSE\\LOGS", "po-import.log"),
      `${new Date().toISOString()} | IMPORTED | ${parsed.purchaseOrder.poNumber} | ${parsed.sourceFileName} | ${missing.length > 0 ? `Review: ${missing.join(", ")}` : "OK"}\n`,
      "utf8"
    );

    liveBridge.emit("PURCHASE_ORDER_UPDATE", {
      event: "imported",
      sourceFileName: parsed.sourceFileName,
      purchaseOrderId: poId,
      poNumber: parsed.purchaseOrder.poNumber,
      vendorName: parsed.vendor.vendorName,
      amount: parsed.purchaseOrder.amount,
      payByDate: formatDate(parsed.purchaseOrder.payByDate)
    });

    return {
      ...response,
      saved: result
    };
  }

  async importFolder(folderPath = po.sourceDir) {
    await this.ensureReady();
    const absolute = path.resolve(folderPath);
    const files = [];

    const walk = (dir) => {
      for (const entry of fs.readdirSync(dir, { withFileTypes: true })) {
        const full = path.join(dir, entry.name);
        if (entry.isDirectory()) {
          walk(full);
          continue;
        }
        if (entry.isFile() && /\.pdf$/i.test(entry.name)) {
          files.push(full);
        }
      }
    };

    walk(absolute);

    const results = [];
    for (const filePath of files) {
      try {
        results.push(await this.importPdfFile(filePath));
      } catch (error) {
        await this.repository.logFailure(path.basename(filePath), filePath, null, error.message, "");
        results.push({
          ok: false,
          sourceFileName: path.basename(filePath),
          sourceFilePath: filePath,
          error: error.message
        });
      }
    }

    return {
      folderPath: absolute,
      total: results.length,
      imported: results.filter((item) => item.ok).length,
      failed: results.filter((item) => !item.ok).length,
      results
    };
  }

  async list(filters = {}) {
    await this.ensureReady();
    const rows = await this.repository.list(filters);
    return rows.map((row) => this.enrichPurchaseOrder(row));
  }

  async getById(id) {
    await this.ensureReady();
    const payload = await this.repository.getById(id);
    if (!payload) {
      return null;
    }

    return {
      ...this.enrichPurchaseOrder(payload.purchaseOrder),
      lineItems: (payload.lineItems || []).map((line) => ({
        ...line,
        quantity: Number(line.Quantity ?? line.quantity ?? 0),
        rate: Number(line.Rate ?? line.rate ?? 0),
        amount: Number(line.Amount ?? line.amount ?? 0)
      }))
    };
  }

  async update(id, body) {
    await this.ensureReady();
    const current = await this.getById(id);
    if (!current) {
      throw new Error("Purchase order not found.");
    }

    const payload = {
      vendor: body.vendor || { vendorName: body.vendorName || current.VendorName, gstin: body.vendorGstin || current.VendorGSTIN },
      purchaseOrder: {
        poNumber: body.poNumber || current.PONumber,
        vendorInvoiceNumber: body.vendorInvoiceNumber || current.VendorInvoiceNumber,
        linkedToType: body.linkedToType || current.LinkedToType || "General",
        linkedToId: body.linkedToId != null ? body.linkedToId : current.LinkedToId,
        purchaseDate: body.purchaseDate ? new Date(body.purchaseDate) : new Date(current.PODate),
        payByDate: body.payByDate ? new Date(body.payByDate) : new Date(current.PayByDate),
        amount: body.amount != null ? Number(body.amount) : Number(current.TotalAmount || 0),
        notes: body.notes != null ? body.notes : current.Notes,
        deliveryMode: body.deliveryMode || current.DeliveryMode,
        deliveryAddress: body.deliveryAddress != null ? body.deliveryAddress : current.DeliveryAddress,
        paymentReference: body.paymentReference != null ? body.paymentReference : current.PaymentReference,
        comparisonNotes: body.comparisonNotes != null ? body.comparisonNotes : current.ComparisonNotes,
        createdByUserId: body.createdByUserId != null ? body.createdByUserId : current.CreatedByUserId,
        createdByName: body.createdByName != null ? body.createdByName : current.CreatedByName,
        createdByDate: body.createdByDate ? new Date(body.createdByDate) : current.CreatedByDate ? new Date(current.CreatedByDate) : new Date(),
        status: body.status || current.Status || "Received",
        lineItems: Array.isArray(body.lineItems) ? body.lineItems : current.lineItems || []
      },
      lineItems: Array.isArray(body.lineItems) ? body.lineItems : current.lineItems || []
    };

    await this.repository.upsertPurchaseOrder(payload);
    return this.getById(id);
  }

  async delete(id) {
    await this.ensureReady();
    await this.repository.delete(id);
    liveBridge.emit("PURCHASE_ORDER_UPDATE", {
      event: "deleted",
      purchaseOrderId: id
    });
  }

  async markReceived(id) {
    await this.ensureReady();
    const current = await this.getById(id);
    if (!current) {
      throw new Error("Purchase order not found.");
    }
    return this.update(id, { status: "Received" });
  }

  async batchPay(ids, paymentReference) {
    await this.ensureReady();
    const { runSql, quoteSql } = require("../db/sqlcmd");
    const validIds = (ids || []).map((id) => Number(id)).filter((id) => Number.isFinite(id) && id > 0);

    if (validIds.length === 0) {
      return;
    }

    const batch = `
      BEGIN TRY
        BEGIN TRAN;
        ${validIds.map((id) => `
          UPDATE PurchaseOrders
          SET PaidAmount = TotalAmount,
              Status = 'Paid',
              PaymentReference = ${quoteSql(paymentReference || null)}
          WHERE POID = ${id};
        `).join("\n")}
        COMMIT;
      END TRY
      BEGIN CATCH
        IF @@TRANCOUNT > 0 ROLLBACK;
        THROW;
      END CATCH
    `;

    await runSql(batch);

    liveBridge.emit("PURCHASE_ORDER_UPDATE", {
      event: "batch-paid",
      ids: validIds,
      paymentReference
    });
  }

  enrichPurchaseOrder(row) {
    if (!row) {
      return null;
    }

    const payByDate = row.PayByDate ? new Date(row.PayByDate) : null;
    const poDate = row.PODate ? new Date(row.PODate) : null;
    const paidAmount = Number(row.PaidAmount || 0);
    const totalAmount = Number(row.TotalAmount || 0);
    const balanceDue = Math.max(totalAmount - paidAmount, 0);
    const isOverdue = balanceDue > 0 && payByDate && payByDate.getTime() < new Date(new Date().toDateString()).getTime();

    return {
      ...row,
      PayByDate: payByDate,
      PODate: poDate,
      PaidAmount: paidAmount,
      TotalAmount: totalAmount,
      BalanceDue: balanceDue,
      IsOverdue: isOverdue,
      AgeDays: computeAgeDays(poDate)
    };
  }
}

module.exports = {
  PurchaseOrderService
};
