const path = require("path");
const { po } = require("../src/config");
const { PurchaseOrderService } = require("../src/purchase-orders/poService");

async function main() {
  const folderPath = process.argv[2] ? path.resolve(process.argv[2]) : path.resolve(po.sourceDir);
  const service = new PurchaseOrderService();

  console.log(`Importing purchase orders from: ${folderPath}`);
  const result = await service.importFolder(folderPath);

  console.log(`Imported ${result.imported}/${result.total} PDFs. Failed: ${result.failed}`);
  for (const item of result.results) {
    const status = item.ok ? "OK" : "FAIL";
    console.log(`[${status}] ${item.sourceFileName}${item.error ? ` - ${item.error}` : ""}`);
  }
}

main().catch((error) => {
  console.error(error);
  process.exitCode = 1;
});
