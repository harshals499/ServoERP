const crypto = require("crypto");
const { meta } = require("../config");
const { extractIntent } = require("../ai/ollamaIntentExtractor");
const { buildQueriesFromIntent } = require("../db/sqlQueries");
const { executeTransaction } = require("../db/database");
const liveBridge = require("../liveBridge");

function verifyMetaSignature(rawBody, signatureHeader) {
  if (!meta.appSecret) {
    return true;
  }

  if (!signatureHeader) {
    return false;
  }

  const expected = `sha256=${crypto.createHmac("sha256", meta.appSecret).update(rawBody).digest("hex")}`;
  return crypto.timingSafeEqual(Buffer.from(expected), Buffer.from(signatureHeader));
}

function parseWebhookMessages(payload) {
  const results = [];
  for (const entry of payload.entry || []) {
    for (const change of entry.changes || []) {
      const value = change.value || {};
      const contactsByWaId = new Map((value.contacts || []).map((contact) => [contact.wa_id, contact]));

      for (const message of value.messages || []) {
        const contact = contactsByWaId.get(message.from) || {};
        const textBody =
          message.text?.body ||
          message.button?.text ||
          message.interactive?.button_reply?.title ||
          message.interactive?.list_reply?.title ||
          "";

        results.push({
          messageId: message.id,
          from: message.from,
          profileName: contact.profile?.name || "",
          timestamp: message.timestamp,
          type: message.type,
          body: textBody.trim(),
          raw: message
        });
      }
    }
  }
  return results;
}

async function processIncomingMessage(inbound) {
  if (!inbound.body) {
    return {
      skipped: true,
      reason: "No text body present."
    };
  }

  const parsed = await extractIntent(inbound.body);
  const steps = buildQueriesFromIntent(parsed, inbound);
  const dbResults = await executeTransaction(steps);

  const payload = {
    source: "whatsapp",
    receivedAt: new Date().toISOString(),
    inbound,
    parsed,
    dbResults
  };

  liveBridge.emit("WHATSAPP_UPDATE", payload);
  return payload;
}

module.exports = {
  verifyMetaSignature,
  parseWebhookMessages,
  processIncomingMessage
};
