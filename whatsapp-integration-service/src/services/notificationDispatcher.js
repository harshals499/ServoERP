const { meta } = require("../config");
const { normalizeIndianPhone } = require("../utils/phone");
const { postMeta } = require("../meta/metaClient");

function ensureTemplateComponents(components) {
  if (!Array.isArray(components)) {
    throw new Error("Template components must be an array.");
  }
  return components;
}

function isLikelyInvalidPhone(error) {
  const message = String(error.message || "").toLowerCase();
  return message.includes("phone") || message.includes("recipient") || message.includes("wa_id");
}

async function sendWhatsAppTemplate(to, templateName, components, languageCode = "en") {
  const normalizedTo = normalizeIndianPhone(to);
  const payload = {
    messaging_product: "whatsapp",
    to: normalizedTo,
    type: "template",
    template: {
      name: templateName,
      language: {
        code: languageCode
      },
      components: ensureTemplateComponents(components)
    }
  };

  try {
    return await postMeta(`/${meta.phoneNumberId}/messages`, payload);
  } catch (error) {
    if (isLikelyInvalidPhone(error)) {
      const wrapped = new Error(`Invalid WhatsApp phone number after normalization: ${normalizedTo}`);
      wrapped.cause = error;
      throw wrapped;
    }
    throw error;
  }
}

module.exports = {
  sendWhatsAppTemplate
};
