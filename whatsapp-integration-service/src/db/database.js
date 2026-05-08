const sql = require("mssql");
const { db } = require("../config");

let poolPromise;

function getPool() {
  if (!poolPromise) {
    poolPromise = sql.connect(db.connectionString);
  }
  return poolPromise;
}

async function executeTransaction(steps) {
  const pool = await getPool();
  const tx = new sql.Transaction(pool);
  await tx.begin();

  try {
    const results = [];
    for (const step of steps) {
      const request = new sql.Request(tx);
      for (const [key, value] of Object.entries(step.params || {})) {
        request.input(key, value);
      }
      const result = await request.query(step.sql);
      results.push({
        label: step.label,
        rowsAffected: result.rowsAffected,
        recordset: result.recordset
      });
    }

    await tx.commit();
    return results;
  } catch (error) {
    await tx.rollback();
    throw error;
  }
}

module.exports = {
  getPool,
  executeTransaction
};
