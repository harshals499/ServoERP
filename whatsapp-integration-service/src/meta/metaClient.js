const { meta } = require("../config");

function sleep(ms) {
  return new Promise((resolve) => setTimeout(resolve, ms));
}

async function postMeta(path, payload, attempt = 1) {
  const response = await fetch(`https://graph.facebook.com/${meta.apiVersion}${path}`, {
    method: "POST",
    headers: {
      Authorization: `Bearer ${meta.accessToken}`,
      "Content-Type": "application/json"
    },
    body: JSON.stringify(payload)
  });

  const raw = await response.text();
  const body = raw ? JSON.parse(raw) : {};

  if (response.ok) {
    return body;
  }

  const error = body.error || {};
  const isRateLimited = response.status === 429 || error.code === 4 || error.error_subcode === 130429;
  if (isRateLimited && attempt < 4) {
    await sleep(500 * Math.pow(2, attempt - 1));
    return postMeta(path, payload, attempt + 1);
  }

  const reason = error.message || raw || "Unknown Meta API error";
  const wrapped = new Error(`Meta API error ${response.status}: ${reason}`);
  wrapped.status = response.status;
  wrapped.meta = body;
  throw wrapped;
}

module.exports = {
  postMeta
};
