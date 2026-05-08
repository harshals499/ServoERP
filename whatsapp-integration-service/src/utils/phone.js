const { whatsapp } = require("../config");

function normalizeIndianPhone(raw) {
  const digits = String(raw || "").replace(/\D/g, "");
  if (!digits) {
    throw new Error("Phone number is empty.");
  }

  if (digits.length === 10) {
    return `${whatsapp.defaultCountryCode}${digits}`;
  }

  if (digits.length === 12 && digits.startsWith(whatsapp.defaultCountryCode)) {
    return digits;
  }

  if (digits.length > 10 && digits.startsWith("0")) {
    return `${whatsapp.defaultCountryCode}${digits.slice(1)}`;
  }

  return digits;
}

module.exports = {
  normalizeIndianPhone
};
