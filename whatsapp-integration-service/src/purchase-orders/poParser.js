const fs = require("fs/promises");
const path = require("path");
const { PDFParse } = require("pdf-parse");
const { recognize } = require("tesseract.js");
const { po } = require("../config");

const MONEY_LABELS = [
  "PO Value",
  "Purchase Value",
  "Grand Total",
  "Net Payable",
  "Total",
  "Amount"
];

function normalizeText(text) {
  return String(text || "")
    .replace(/\r/g, "\n")
    .replace(/[ \t]+\n/g, "\n")
    .replace(/\n{3,}/g, "\n\n")
    .replace(/\u00a0/g, " ")
    .replace(/[ \t]{2,}/g, " ")
    .trim();
}

function cleanSingleLine(text) {
  return normalizeText(text).replace(/\n+/g, " ").replace(/\s{2,}/g, " ").trim();
}

function parseIndianNumber(value) {
  if (value == null) {
    return null;
  }

  const cleaned = String(value).replace(/[^0-9.-]/g, "");
  if (!cleaned) {
    return null;
  }

  const parsed = Number(cleaned);
  return Number.isFinite(parsed) ? parsed : null;
}

function toMoney(value) {
  const parsed = parseIndianNumber(value);
  return parsed == null ? null : Math.round(parsed * 100) / 100;
}

function toDateOrNull(value) {
  if (value instanceof Date && !Number.isNaN(value.getTime())) {
    return value;
  }

  if (!value) {
    return null;
  }

  const text = String(value).trim();
  if (!text) {
    return null;
  }

  const direct = new Date(text);
  if (!Number.isNaN(direct.getTime())) {
    return direct;
  }

  const match = text.match(/(\d{1,2})[.\-\/](\d{1,2}|[A-Za-z]{3,9})[.\-\/](\d{2,4})/);
  if (!match) {
    return null;
  }

  const day = Number(match[1]);
  const monthToken = match[2];
  let month = Number(monthToken);
  if (Number.isNaN(month)) {
    const monthNames = {
      jan: 1, january: 1,
      feb: 2, february: 2,
      mar: 3, march: 3,
      apr: 4, april: 4,
      may: 5,
      jun: 6, june: 6,
      jul: 7, july: 7,
      aug: 8, august: 8,
      sep: 9, sept: 9, september: 9,
      oct: 10, october: 10,
      nov: 11, november: 11,
      dec: 12, december: 12
    };
    month = monthNames[String(monthToken).slice(0, 3).toLowerCase()] || monthNames[String(monthToken).toLowerCase()] || 0;
  }

  let year = Number(match[3]);
  if (year < 100) {
    year = year < 70 ? 2000 + year : 1900 + year;
  }

  if (!day || !month || !year) {
    return null;
  }

  const dt = new Date(year, month - 1, day);
  return Number.isNaN(dt.getTime()) ? null : dt;
}

function addDays(date, days) {
  const base = date instanceof Date && !Number.isNaN(date.getTime()) ? new Date(date.getTime()) : new Date();
  base.setDate(base.getDate() + Number(days || 0));
  return base;
}

function extractFirstMatch(text, patterns) {
  for (const pattern of patterns) {
    const match = text.match(pattern);
    if (match && match[1]) {
      return String(match[1]).trim();
    }
  }
  return null;
}

function extractVendorName(text) {
  const lines = normalizeText(text)
    .split("\n")
    .map((line) => line.trim())
    .filter(Boolean);

  const madhu = lines.find((line) => /madhusuman enterprises/i.test(line));
  if (madhu) {
    return madhu.replace(/\s{2,}/g, " ").trim();
  }

  const vendorIndex = lines.findIndex((line) => /^vendor\b/i.test(line) || /^supplier\b/i.test(line));
  if (vendorIndex >= 0) {
    const collected = [];
    for (let i = vendorIndex + 1; i < lines.length; i++) {
      const line = lines[i];
      if (/^(gstin|order no|order date|your reference|invoice no|buyer|consignee|notes|ship to|bill to)\b/i.test(line)) {
        break;
      }
      if (/^(company|vendor|supplier|buyer|consignee)$/i.test(line)) {
        continue;
      }
      if (/(ENTERPRISES|LIMITED|PVT|PRIVATE LIMITED|INDUSTRIES|SUPPLIES|TRADERS|ENGINEERING|WORKS|COMPANY)/i.test(line) && line.length > 2) {
        collected.push(line);
      }
    }

    const candidate = collected.find((line) => /[A-Za-z]/.test(line) && !/^\d+$/.test(line)) || collected[0];
    if (candidate) {
      return candidate.replace(/\s{2,}/g, " ").trim();
    }
  }

  const keywordCandidates = [
    "MADHUSUMAN ENTERPRISES",
    "ENTERPRISES",
    "ENGINEERING",
    "LIMITED",
    "PVT",
    "PRIVATE LIMITED",
    "INDUSTRIES",
    "SUPPLIES",
    "TRADERS",
    "WORKS",
    "COMPANY"
  ];

  for (const keyword of keywordCandidates) {
    const regex = new RegExp(`(^|\\n)([A-Z0-9&., /-]*${keyword}[A-Z0-9&., /-]*)`, "i");
    const match = text.match(regex);
    if (match && match[2]) {
      const candidate = match[2].trim();
      if (candidate.length >= 3) {
        return candidate.replace(/\s{2,}/g, " ");
      }
    }
  }

  return null;
}

function extractGstin(text) {
  return extractFirstMatch(text, [
    /\b([0-9]{2}[A-Z]{5}[0-9]{4}[A-Z][0-9A-Z][Zz][0-9A-Z])\b/
  ]);
}

function extractPoNumber(text, fileStem) {
  const fromLabel = extractFirstMatch(text, [
    /(?:PO\s*Number|PO\s*No\.?|Purchase\s*Order\s*No\.?|Order\s*No\.?|Order\s*Number)\s*[:\-]?\s*([A-Z0-9\/_.-]+)/i,
    /(AR\/SERV\/[A-Z0-9\/_.-]+)/i,
    /(AR\/[A-Z0-9\/_.-]+)/i
  ]);

  if (fromLabel) {
    const cleaned = fromLabel.replace(/\s+/g, "").trim();
    const looksLikeDate = /^\d{1,2}[.\-\/]\d{1,2}[.\-\/]\d{2,4}$/.test(cleaned)
      || /^\d{1,2}[.\-\/][A-Za-z]{3,9}[.\-\/]\d{2,4}$/i.test(cleaned)
      || /^(jan|feb|mar|apr|may|jun|jul|aug|sep|oct|nov|dec)/i.test(cleaned);
    if (!looksLikeDate && /\d{5,}/.test(cleaned)) {
      return cleaned;
    }
  }

  const digits = String(fileStem || "").match(/(\d{5,})/);
  if (digits) {
    return digits[1];
  }

  const fileMatch = String(fileStem || "").match(/^([A-Z0-9\/_.-]{5,})/i);
  if (fileMatch) {
    return fileMatch[1].replace(/\s+/g, "").trim();
  }

  return cleanSingleLine(fileStem || "PO-" + Date.now());
}

function extractVendorInvoiceNumber(text) {
  return extractFirstMatch(text, [
    /(?:Vendor\s*Invoice\s*(?:No\.?|#)?|Invoice\s*(?:No\.?|#)?|Tax\s*Invoice\s*No\.?|Bill\s*No\.?)\s*[:\-]?\s*([A-Z0-9\/_.-]+)/i
  ]);
}

function extractPurchaseDate(text, fallbackDate) {
  const fromLabel = extractFirstMatch(text, [
    /(?:Order\s*Date|PO\s*Date|Purchase\s*Date|Dated)\s*[:\-]?\s*([0-3]?\d[.\-\/][A-Za-z0-9]{1,9}[.\-\/][0-9]{2,4})/i,
    /(?:Order\s*Date|PO\s*Date|Purchase\s*Date|Dated)\s*[:\-]?\s*([0-3]?\d[.\-\/][0-9]{1,2}[.\-\/][0-9]{2,4})/i
  ]);
  const parsed = toDateOrNull(fromLabel);
  if (parsed) {
    return parsed;
  }

  const fallback = toDateOrNull(fallbackDate) || new Date();
  return fallback;
}

function extractExplicitPayByDate(text) {
  const fromLabel = extractFirstMatch(text, [
    /(?:Pay\s*By\s*Date|Due\s*Date|Delivery\s*Schedule|Need\s*By\s*Date)\s*[:\-]?\s*([0-3]?\d[.\-\/][A-Za-z0-9]{1,9}[.\-\/][0-9]{2,4})/i,
    /(?:Pay\s*By\s*Date|Due\s*Date|Delivery\s*Schedule|Need\s*By\s*Date)\s*[:\-]?\s*([0-3]?\d[.\-\/][0-9]{1,2}[.\-\/][0-9]{2,4})/i
  ]);
  return toDateOrNull(fromLabel);
}

function extractCreditDays(text) {
  const candidates = [
    /(\d{1,3})\s*days?\s*(?:after\s*receipt|from\s*receipt|from\s*invoice)/i,
    /Payment\s*Term[s]?\s*[:\-]?\s*(\d{1,3})\s*days?/i,
    /(\d{1,3})\s*days?/i
  ];

  for (const pattern of candidates) {
    const match = text.match(pattern);
    if (match && match[1]) {
      const days = Number(match[1]);
      if (Number.isFinite(days) && days > 0) {
        return days;
      }
    }
  }

  return null;
}

function extractAmount(text) {
  const normalized = normalizeText(text);

  for (const label of MONEY_LABELS) {
    const pattern = new RegExp(`${label}\\s*[:\\-]?\\s*₹?\\s*([0-9][0-9,]*(?:\\.[0-9]{1,2})?)`, "i");
    const match = normalized.match(pattern);
    if (match && match[1]) {
      const amount = toMoney(match[1]);
      if (amount != null) {
        return amount;
      }
    }
  }

  const allMoneyMatches = [...normalized.matchAll(/₹?\s*([0-9][0-9,]*(?:\.[0-9]{1,2})?)/g)]
    .map((match) => toMoney(match[1]))
    .filter((value) => value != null && value > 0 && value < 1000000000);

  if (allMoneyMatches.length === 0) {
    return 0;
  }

  return Math.max(...allMoneyMatches);
}

function inferCategory(text, fileStem) {
  const searchSpace = `${fileStem || ""} ${text || ""}`.toLowerCase();
  const mappings = [
    ["cold room", "Refrigeration"],
    ["refrigeration", "Refrigeration"],
    ["electric", "Electrical"],
    ["electrical", "Electrical"],
    ["compressor", "Compressors"],
    ["ahu", "HVAC"],
    ["chiller", "HVAC"],
    ["fan", "HVAC"],
    ["filter", "Filters"],
    ["amc", "AMC"],
    ["service", "Service"],
    ["gas", "Gas"],
    ["spare", "Spares"]
  ];

  for (const [needle, value] of mappings) {
    if (searchSpace.includes(needle)) {
      return value;
    }
  }

  return "General";
}

function inferStatus(totalAmount, payByDate) {
  if (!totalAmount || totalAmount <= 0) {
    return "Draft";
  }
  if (payByDate && payByDate.getTime() < Date.now()) {
    return "Received";
  }
  return "Received";
}

function buildNotes(text, sourceFileName, sourceFilePath, parsed) {
  const snippets = [];
  if (parsed.vendorInvoiceNumber) {
    snippets.push(`Vendor Invoice #: ${parsed.vendorInvoiceNumber}`);
  }
  if (parsed.paymentTermsText) {
    snippets.push(`Payment Terms: ${parsed.paymentTermsText}`);
  }
  if (parsed.referenceText) {
    snippets.push(`Reference: ${parsed.referenceText}`);
  }

  snippets.push(`Source File: ${sourceFileName}`);
  snippets.push(`Source Path: ${sourceFilePath}`);

  const notes = snippets.join(" | ");
  return notes.length > 1000 ? notes.slice(0, 1000) : notes;
}

function inferPrimaryDescription(fileStem, text) {
  const description = extractFirstMatch(text, [
    /Description\s*[:\-]?\s*(.+?)(?:\n|$)/i,
    /Item\s*[:\-]?\s*(.+?)(?:\n|$)/i
  ]);

  if (description) {
    return cleanSingleLine(description);
  }

  return cleanSingleLine(fileStem || "Imported PO");
}

function buildFallbackLineItem(totalAmount, description, hsnSac, category) {
  return {
    inventoryItemId: null,
    description: description || category || "Imported purchase order",
    hsnSacCode: hsnSac || "9987",
    quantity: 1,
    uom: "Nos",
    rate: totalAmount || 0,
    gstRate: 0,
    cgstRate: 0,
    sgstRate: 0,
    igstRate: 0,
    jobLink: "General",
    linkedWorkOrderId: null,
    linkedWorkOrderName: null,
    priceVariance: 0,
    historicalRate: 0,
    amount: totalAmount || 0
  };
}

function parseLineItems(text, fallbackDescription, totalAmount) {
  const lines = normalizeText(text)
    .split("\n")
    .map((line) => line.trim())
    .filter(Boolean);

  const rows = [];
  for (const line of lines) {
    const normalized = line.replace(/\s{2,}/g, " ");
    const moneyValues = [...normalized.matchAll(/([0-9][0-9,]*(?:\.[0-9]{1,2})?)/g)].map((m) => m[1]);
    if (moneyValues.length < 3) {
      continue;
    }

    if (/^(purchase order|vendor|gstin|notes|terms|delivery|banker|consignee|buyer|ship to|bill to|order no|order date|payment term)/i.test(normalized)) {
      continue;
    }

    const amount = toMoney(moneyValues[moneyValues.length - 1]);
    if (amount == null || amount <= 0) {
      continue;
    }

    const rate = toMoney(moneyValues[moneyValues.length - 2]) || amount;
    const quantityMatch = normalized.match(/(?:^|\s)(\d+(?:\.\d+)?)\s*(?:nos|nos\.|days?|hrs?|hours?|kg|mtr|ltr|set|box|pieces?|pcs)?\s*$/i);
    const quantity = quantityMatch ? Number(quantityMatch[1]) : 1;

    let description = normalized
      .replace(/([0-9][0-9,]*(?:\.[0-9]{1,2})?)/g, " ")
      .replace(/\s{2,}/g, " ")
      .trim();

    if (!description || description.length < 3) {
      continue;
    }

    if (description.length > 240) {
      description = description.slice(0, 240);
    }

    rows.push({
      inventoryItemId: null,
      description,
      hsnSacCode: extractFirstMatch(normalized, [/\b(\d{4,8})\b/]) || "9987",
      quantity: Number.isFinite(quantity) && quantity > 0 ? quantity : 1,
      uom: extractFirstMatch(normalized, [/\b(Nos|Nos\.|Days|Hour|Daily|Mtr|Meter|Kg|Ltr|Set|Box|Kit)\b/i]) || "Nos",
      rate,
      gstRate: extractFirstMatch(normalized, [/\b(0|5|12|18|28)\%/i]) ? Number(extractFirstMatch(normalized, [/\b(0|5|12|18|28)\%/i])) : 0,
      cgstRate: 0,
      sgstRate: 0,
      igstRate: 0,
      jobLink: "General",
      linkedWorkOrderId: null,
      linkedWorkOrderName: null,
      priceVariance: 0,
      historicalRate: 0,
      amount
    });
  }

  if (rows.length > 0 && rows.length <= 8) {
    return rows;
  }

  return [buildFallbackLineItem(totalAmount, fallbackDescription)];
}

async function collectOcrText(parser, pagesToScan) {
  const fragments = [];
  const totalPages = Math.max(1, pagesToScan || po.ocrPages || 2);

  for (let page = 1; page <= totalPages; page++) {
    try {
      const shot = await parser.getScreenshot({
        partial: [page],
        scale: 1,
        imageDataUrl: false,
        imageBuffer: true
      });

      const imageBuffer = shot?.pages?.[0]?.data;
      if (!imageBuffer) {
        continue;
      }

      const result = await recognize(imageBuffer, "eng");
      if (result?.data?.text) {
        fragments.push(result.data.text);
      }
    } catch {
      // OCR is a best-effort fallback. If one page fails, continue with the rest.
    }
  }

  return fragments.join("\n");
}

async function parsePurchaseOrderPdf(filePath) {
  const buffer = await fs.readFile(filePath);
  const fileName = path.basename(filePath);
  const fileStem = path.basename(filePath, path.extname(filePath));

  const parser = new PDFParse({ data: buffer });
  const textResult = await parser.getText();
  const pages = Array.isArray(textResult?.pages) ? textResult.pages : [];
  const rawText = normalizeText(pages.map((page) => page?.text || "").join("\n\n"));

  let combinedText = rawText;
  const ocrText = await collectOcrText(parser, Math.min(po.ocrPages || 2, Math.max(1, pages.length || 2)));
  if (ocrText && ocrText.trim().length > Math.max(120, combinedText.length / 2)) {
    combinedText = normalizeText(`${combinedText}\n\n${ocrText}`);
  }

  const vendorInvoiceNumber = extractVendorInvoiceNumber(combinedText);
  const vendorName = extractVendorName(combinedText) || "Unknown Vendor";
  const vendorGstin = extractGstin(combinedText);
  const purchaseDate = extractPurchaseDate(combinedText, filePath);
  const explicitPayByDate = extractExplicitPayByDate(combinedText);
  const creditDays = extractCreditDays(combinedText) || 30;
  const payByDate = explicitPayByDate || addDays(purchaseDate, creditDays);
  const poNumber = extractPoNumber(combinedText, fileStem);
  const totalAmount = extractAmount(combinedText);
  const category = inferCategory(combinedText, fileStem);
  const description = inferPrimaryDescription(fileStem, combinedText);
  const referenceText = extractFirstMatch(combinedText, [
    /(?:Your\s*Reference|Supplier\s*Ref|Supplier\s*Reference)\s*[:\-]?\s*(.+?)(?:\n|$)/i
  ]);
  const paymentTermsText = extractFirstMatch(combinedText, [
    /(?:Payment\s*Term[s]?|Payment\s*Terms)\s*[:\-]?\s*(.+?)(?:\n|$)/i
  ]);

  const lineItems = parseLineItems(combinedText, description, totalAmount);
  const notes = buildNotes(combinedText, fileName, filePath, {
    vendorInvoiceNumber,
    paymentTermsText,
    referenceText
  });

  return {
    sourceFileName: fileName,
    sourceFilePath: filePath,
    sourceFileStem: fileStem,
    extractionMethod: ocrText ? "text+ocr" : "text",
    rawText: combinedText,
    vendor: {
      vendorName,
      gstin: vendorGstin,
      defaultCreditDays: creditDays
    },
    purchaseOrder: {
      poNumber,
      vendorInvoiceNumber,
      vendorName,
      vendorGstin,
      purchaseDate,
      payByDate,
      category,
      status: inferStatus(totalAmount, payByDate),
      amount: totalAmount,
      notes,
      linkedToType: "General",
      linkedToId: null
    },
    lineItems,
    warnings: []
  };
}

module.exports = {
  addDays,
  cleanSingleLine,
  extractAmount,
  extractCreditDays,
  extractExplicitPayByDate,
  extractPoNumber,
  extractPurchaseDate,
  extractVendorInvoiceNumber,
  extractVendorName,
  normalizeText,
  parseIndianNumber,
  parsePurchaseOrderPdf,
  toDateOrNull
};
