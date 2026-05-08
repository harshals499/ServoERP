function buildQueriesFromIntent(parsed, inbound) {
  switch (parsed.intent) {
    case "JOB_COMPLETED":
      return buildJobCompletedQueries(parsed, inbound);
    case "GAS_RECHARGED":
      return buildGasRechargedQueries(parsed, inbound);
    case "CLIENT_INQUIRY":
      return buildClientInquiryQueries(parsed, inbound);
    default:
      return buildUnknownQueries(parsed, inbound);
  }
}

function buildJobCompletedQueries(parsed, inbound) {
  const jobRef = parsed.entities.job_number || parsed.entities.job_id || parsed.entities.work_order || "";
  const completionNote = parsed.entities.summary || inbound.body;

  return [
    {
      label: "mark-job-complete",
      sql: `
UPDATE Jobs
SET
  Status = 'Completed',
  CompletedDate = GETDATE(),
  Notes = CONCAT(ISNULL(Notes, ''), CASE WHEN ISNULL(Notes, '') = '' THEN '' ELSE CHAR(13) + CHAR(10) END, @note)
WHERE JobNumber = @jobRef OR CAST(JobID AS NVARCHAR(50)) = @jobRef;
SELECT @@ROWCOUNT AS affectedRows;`,
      params: {
        jobRef,
        note: `WhatsApp update from ${inbound.from}: ${completionNote}`
      }
    }
  ];
}

function buildGasRechargedQueries(parsed, inbound) {
  const itemName = parsed.entities.inventory_item || parsed.entities.gas_type || "Refrigerant Gas";
  const quantity = Number(parsed.entities.quantity || 0);
  const unit = parsed.entities.unit || "kg";
  const jobRef = parsed.entities.job_number || parsed.entities.job_id || parsed.entities.work_order || "";
  const note = parsed.entities.summary || inbound.body;

  return [
    {
      label: "annotate-job",
      sql: `
UPDATE Jobs
SET
  Notes = CONCAT(ISNULL(Notes, ''), CASE WHEN ISNULL(Notes, '') = '' THEN '' ELSE CHAR(13) + CHAR(10) END, @note)
WHERE JobNumber = @jobRef OR CAST(JobID AS NVARCHAR(50)) = @jobRef;
SELECT @@ROWCOUNT AS affectedRows;`,
      params: {
        jobRef,
        note: `Gas recharged via WhatsApp: ${note}`
      }
    },
    {
      label: "deduct-stock",
      sql: `
UPDATE StockItems
SET
  CurrentStock = CurrentStock - @quantity,
  LastUpdated = GETDATE()
WHERE ItemName LIKE '%' + @itemName + '%';
SELECT @@ROWCOUNT AS affectedRows;`,
      params: {
        itemName,
        quantity
      }
    },
    {
      label: "inventory-usage-log",
      sql: `
INSERT INTO InventoryUsageLog (InvoiceID, StockItemID, Quantity, UsageAction, Notes, LoggedAt)
SELECT
  NULL,
  ItemID,
  @quantity,
  'WhatsAppDeduction',
  @note,
  GETDATE()
FROM StockItems
WHERE ItemName LIKE '%' + @itemName + '%';`,
      params: {
        itemName,
        quantity,
        note: `WhatsApp from ${inbound.from}: ${quantity} ${unit} ${itemName} used${jobRef ? ` for ${jobRef}` : ""}.`
      }
    }
  ];
}

function buildClientInquiryQueries(parsed, inbound) {
  const reference = parsed.entities.job_number || parsed.entities.contract_number || parsed.entities.customer_reference || "";
  const note = parsed.entities.summary || inbound.body;

  return [
    {
      label: "append-inquiry-note",
      sql: `
UPDATE Jobs
SET
  Notes = CONCAT(ISNULL(Notes, ''), CASE WHEN ISNULL(Notes, '') = '' THEN '' ELSE CHAR(13) + CHAR(10) END, @note)
WHERE JobNumber = @reference OR CAST(JobID AS NVARCHAR(50)) = @reference;
SELECT @@ROWCOUNT AS affectedRows;`,
      params: {
        reference,
        note: `Client inquiry via WhatsApp (${inbound.from}): ${note}`
      }
    }
  ];
}

function buildUnknownQueries(parsed, inbound) {
  return [
    {
      label: "no-op",
      sql: "SELECT @body AS messageBody, @intent AS detectedIntent;",
      params: {
        body: inbound.body,
        intent: parsed.intent || "UNKNOWN"
      }
    }
  ];
}

module.exports = {
  buildQueriesFromIntent
};
