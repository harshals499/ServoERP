const express = require("express");
const multer = require("multer");
const fs = require("fs");
const path = require("path");
const { po } = require("../config");
const { PurchaseOrderService } = require("./poService");

const upload = multer({
  storage: multer.diskStorage({
    destination: (req, file, cb) => {
      const dir = path.resolve(po.uploadDir);
      fs.mkdirSync(dir, { recursive: true });
      cb(null, dir);
    },
    filename: (req, file, cb) => {
      const stamp = new Date().toISOString().replace(/[:.]/g, "-");
      cb(null, `${stamp}_${file.originalname}`);
    }
  }),
  fileFilter: (req, file, cb) => {
    if (/\.pdf$/i.test(file.originalname) || file.mimetype === "application/pdf") {
      cb(null, true);
      return;
    }
    cb(new Error("Only PDF files are accepted."));
  }
});

function createPurchaseOrderRouter() {
  const router = express.Router();
  const service = new PurchaseOrderService();

  router.get("/po", async (req, res) => {
    try {
      const rows = await service.list({
        status: req.query.status,
        vendor: req.query.vendor,
        fromDate: req.query.fromDate,
        toDate: req.query.toDate,
        unpaidOnly: String(req.query.unpaidOnly || "").toLowerCase() === "true"
      });
      res.json({ ok: true, count: rows.length, data: rows });
    } catch (error) {
      res.status(500).json({ ok: false, error: error.message });
    }
  });

  router.post("/po/upload", upload.single("file"), async (req, res) => {
    try {
      if (!req.file) {
        return res.status(400).json({ ok: false, error: "Please upload a PDF file using the 'file' field." });
      }

      const result = await service.importPdfFile(req.file.path);
      res.json({ ok: true, ...result });
    } catch (error) {
      res.status(500).json({ ok: false, error: error.message });
    }
  });

  router.post("/po/import-folder", async (req, res) => {
    try {
      const folderPath = req.body?.folderPath || po.sourceDir;
      const result = await service.importFolder(folderPath);
      res.json({ ok: true, ...result });
    } catch (error) {
      res.status(500).json({ ok: false, error: error.message });
    }
  });

  router.post("/po/batch-pay", async (req, res) => {
    try {
      const ids = Array.isArray(req.body?.ids) ? req.body.ids : [];
      const paymentReference = req.body?.paymentReference || "";
      await service.batchPay(ids, paymentReference);
      res.json({ ok: true, paidCount: ids.length, paymentReference });
    } catch (error) {
      res.status(500).json({ ok: false, error: error.message });
    }
  });

  router.get("/po/:id", async (req, res) => {
    try {
      const id = Number(req.params.id);
      const record = await service.getById(id);
      if (!record) {
        return res.status(404).json({ ok: false, error: "Purchase order not found." });
      }
      res.json({ ok: true, data: record });
    } catch (error) {
      res.status(500).json({ ok: false, error: error.message });
    }
  });

  router.put("/po/:id", async (req, res) => {
    try {
      const id = Number(req.params.id);
      const record = await service.update(id, req.body || {});
      res.json({ ok: true, data: record });
    } catch (error) {
      res.status(500).json({ ok: false, error: error.message });
    }
  });

  router.delete("/po/:id", async (req, res) => {
    try {
      const id = Number(req.params.id);
      await service.delete(id);
      res.json({ ok: true });
    } catch (error) {
      res.status(500).json({ ok: false, error: error.message });
    }
  });

  return router;
}

module.exports = {
  createPurchaseOrderRouter
};
