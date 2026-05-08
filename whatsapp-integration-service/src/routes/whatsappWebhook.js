const express = require("express");
const { meta } = require("../config");
const {
  verifyMetaSignature,
  parseWebhookMessages,
  processIncomingMessage
} = require("../services/webhookProcessor");

function createWhatsappRouter() {
  const router = express.Router();

  router.get("/api/whatsapp/webhook", (req, res) => {
    const mode = req.query["hub.mode"];
    const token = req.query["hub.verify_token"];
    const challenge = req.query["hub.challenge"];

    if (mode === "subscribe" && token === meta.verifyToken) {
      return res.status(200).send(challenge);
    }

    return res.status(403).json({ error: "Verification failed." });
  });

  router.post("/api/whatsapp/webhook", async (req, res) => {
    try {
      const signature = req.headers["x-hub-signature-256"];
      if (!verifyMetaSignature(req.rawBody || "", signature)) {
        return res.status(401).json({ error: "Invalid webhook signature." });
      }

      const messages = parseWebhookMessages(req.body);
      const processed = [];

      for (const message of messages) {
        processed.push(await processIncomingMessage(message));
      }

      return res.status(200).json({
        ok: true,
        processedCount: processed.length,
        processed
      });
    } catch (error) {
      return res.status(500).json({
        ok: false,
        error: error.message
      });
    }
  });

  return router;
}

module.exports = {
  createWhatsappRouter
};
