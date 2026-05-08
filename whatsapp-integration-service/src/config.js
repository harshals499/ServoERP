const path = require("path");
require("dotenv").config({ path: path.resolve(__dirname, "..", ".env") });

function required(name, fallback) {
  const value = process.env[name] || fallback;
  if (!value) {
    throw new Error(`Missing required environment variable: ${name}`);
  }
  return value;
}

module.exports = {
  port: Number(process.env.PORT || 4300),
  meta: {
    accessToken: required("META_ACCESS_TOKEN", "replace_me"),
    verifyToken: required("META_VERIFY_TOKEN", "replace_me"),
    phoneNumberId: required("PHONE_NUMBER_ID", "replace_me"),
    apiVersion: process.env.META_API_VERSION || "v23.0",
    appSecret: process.env.META_APP_SECRET || ""
  },
  ollama: {
    baseUrl: process.env.OLLAMA_BASE_URL || "http://127.0.0.1:11434",
    model: process.env.OLLAMA_MODEL || "llama3"
  },
  db: {
    connectionString: required("SQLSERVER_CONNECTION_STRING", "Server=.;Database=HVAC_PRO;Trusted_Connection=True;TrustServerCertificate=True;"),
    instance: process.env.SQLSERVER_INSTANCE || "HARSHAL\\SQLEXPRESS",
    database: process.env.SQLSERVER_DATABASE || "HVAC_PRO"
  },
  po: {
    sourceDir: process.env.PO_SOURCE_DIR || path.resolve(process.cwd(), "..", "DATABASE", "Purchase Order AMC", "Purchase Order AMC"),
    ocrEnabled: String(process.env.PO_OCR_ENABLED || "1") !== "0",
    ocrPages: Math.max(1, Number(process.env.PO_OCR_PAGES || 2)),
    uploadDir: process.env.PO_UPLOAD_DIR || path.resolve(process.cwd(), "uploads", "purchase-orders")
  },
  whatsapp: {
    defaultCountryCode: process.env.WHATSAPP_DEFAULT_COUNTRY_CODE || "91"
  },
  socket: {
    corsOrigin: process.env.SOCKET_CORS_ORIGIN || "*"
  }
};
