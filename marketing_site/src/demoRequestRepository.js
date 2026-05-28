const STATUS_VALUES = new Set(["Pending", "Contacted", "Converted"]);

export class DemoRequestRepository {
  constructor(database) {
    this.database = database;
    this.database.exec(`
      CREATE TABLE IF NOT EXISTS demo_requests (
        id INTEGER PRIMARY KEY AUTOINCREMENT,
        name TEXT NOT NULL,
        company_name TEXT NOT NULL,
        email TEXT NOT NULL,
        phone_number TEXT NOT NULL,
        business_type TEXT NOT NULL,
        message TEXT NOT NULL,
        users TEXT,
        plan TEXT,
        workflow TEXT,
        status TEXT NOT NULL DEFAULT 'Pending',
        source TEXT NOT NULL DEFAULT 'website',
        ip_address TEXT,
        user_agent TEXT,
        created_at TEXT NOT NULL
      )
    `);
  }

  save(request) {
    const status = STATUS_VALUES.has(request.status) ? request.status : "Pending";
    const createdAt = request.createdAt || new Date().toISOString();
    const statement = this.database.prepare(`
      INSERT INTO demo_requests (
        name,
        company_name,
        email,
        phone_number,
        business_type,
        message,
        users,
        plan,
        workflow,
        status,
        source,
        ip_address,
        user_agent,
        created_at
      )
      VALUES (?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?)
    `);

    const result = statement.run(
      request.name,
      request.companyName,
      request.email,
      request.phoneNumber,
      request.businessType,
      request.message,
      request.users || "",
      request.plan || "",
      request.workflow || "",
      status,
      request.source || "website",
      request.ipAddress || "",
      request.userAgent || "",
      createdAt
    );

    return {
      id: Number(result.lastInsertRowid),
      ...request,
      status,
      createdAt,
    };
  }

  list() {
    const rows = this.database
      .prepare(
        `SELECT
          id,
          name,
          company_name AS companyName,
          email,
          phone_number AS phoneNumber,
          business_type AS businessType,
          message,
          users,
          plan,
          workflow,
          status,
          source,
          ip_address AS ipAddress,
          user_agent AS userAgent,
          created_at AS createdAt
        FROM demo_requests
        ORDER BY created_at DESC`
      )
      .all();

    return rows;
  }
}
