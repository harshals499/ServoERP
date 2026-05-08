const { ollama } = require("../config");

function safeJsonParse(text) {
  try {
    return JSON.parse(text);
  } catch {
    const match = String(text || "").match(/\{[\s\S]*\}/);
    if (!match) {
      throw new Error(`LLM output was not valid JSON: ${text}`);
    }
    return JSON.parse(match[0]);
  }
}

async function extractIntent(messageText) {
  const prompt = `
You are an HVAC ERP message classifier for Indian field-service teams.
Return only strict JSON with this shape:
{
  "intent": "JOB_COMPLETED|GAS_RECHARGED|CLIENT_INQUIRY|UNKNOWN",
  "confidence": 0.0,
  "entities": {
    "job_number": "",
    "job_id": "",
    "work_order": "",
    "gas_type": "",
    "inventory_item": "",
    "quantity": 0,
    "unit": "",
    "contract_number": "",
    "customer_reference": "",
    "summary": ""
  }
}

Rules:
- JOB_COMPLETED means the technician says a job/work order/visit is completed or finished.
- GAS_RECHARGED means the technician says gas or refrigerant was filled, topped up, or recharged.
- CLIENT_INQUIRY means the customer is asking for status, schedule, price, invoice, or support.
- If uncertain, choose UNKNOWN.
- Extract numbers and references exactly when present.

Message:
${messageText}
  `.trim();

  const response = await fetch(`${ollama.baseUrl}/api/generate`, {
    method: "POST",
    headers: {
      "Content-Type": "application/json"
    },
    body: JSON.stringify({
      model: ollama.model,
      prompt,
      stream: false,
      format: "json"
    })
  });

  if (!response.ok) {
    const detail = await response.text();
    throw new Error(`Ollama request failed: ${response.status} ${detail}`);
  }

  const data = await response.json();
  const parsed = safeJsonParse(data.response);
  parsed.intent = String(parsed.intent || "UNKNOWN").toUpperCase();
  parsed.confidence = Number(parsed.confidence || 0);
  parsed.entities = parsed.entities || {};
  return parsed;
}

module.exports = {
  extractIntent
};
