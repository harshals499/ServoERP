# Multi-Tenant Deployment Strategy

## Decision

Use one SQL Server host with one database per HVAC company:

- `HVAC_PRO` for the current Madhusuman Enterprises deployment.
- `HVAC_PRO_<TENANT_CODE>` for each new customer, for example `HVAC_PRO_BLUEJET`.

This avoids adding `TenantId` to every table and query while also avoiding a separate SQL Server instance per customer. The existing repositories stay tenant-safe because the running app process connects to exactly one database.

## Why This Fits The Current App

The app already centralizes database access through `DatabaseManager.GetConnection()`. All DAL classes open connections from that manager, and `DatabaseManager.InitializeDatabase()` creates the schema for the configured database. That means tenant separation can happen at the connection string/database boundary without rewriting every repository.

## Runtime Configuration

Tenant identity and database routing live in `HVACPro.config`:

```xml
<Database>
  <Server>.\SQLEXPRESS</Server>
  <DatabaseName>HVAC_PRO_BLUEJET</DatabaseName>
  <UseWindowsAuth>true</UseWindowsAuth>
  <Username></Username>
  <Password></Password>
</Database>
<Tenant>
  <TenantCode>BLUEJET</TenantCode>
  <SeedDemoData>false</SeedDemoData>
</Tenant>
<Company>
  <CompanyName>Bluejet Airconditioning Services</CompanyName>
</Company>
```

`SeedDemoData=false` is important for customer deployments. It prevents the app from loading Madhusuman sample/import data into a clean customer database. The current default config keeps `SeedDemoData=true` so the existing MSE install behavior is preserved.

## Provisioning A New Company

Run this from the machine where the app is installed:

```powershell
powershell -ExecutionPolicy Bypass -File C:\HVAC_PRO_MSE\TOOLS\Provision-TenantDatabase.ps1 `
  -TenantCode BLUEJET `
  -CompanyName "Bluejet Airconditioning Services" `
  -Server ".\SQLEXPRESS"
```

The script writes `HVACPro.config`, derives the tenant database name when one is not supplied, and runs the app's `/firstrun` initializer if the installed exe is present.

## Operating Model

- Small customer/on-prem: install app and SQL Express on that customer's machine, database name `HVAC_PRO`.
- Hosted multi-company server: run one SQL Server instance, create one database per company, and point each customer install at its assigned database.
- Backups: back up and restore per tenant database. A restore for one customer does not touch other customers.
- Updates: app binaries are shared; schema migration runs against the configured tenant database on startup.

## What Not To Do Yet

Do not add `TenantId` across all business tables unless you are moving to a single shared database SaaS architecture with central web authentication, row-level security, and strict tenant-scoped query enforcement. That is a larger product rewrite, not the shortest path to selling to 10 HVAC companies.
