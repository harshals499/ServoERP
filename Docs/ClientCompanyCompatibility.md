# Desktop client creation compatibility

Version 1.1.524 is a forward patch for the 1.1.523 client insert. Desktop-only databases may not contain the optional Office API B2BClients.CompanyId column. The unconditional insert in 1.1.523 fails with SQL error 207 in that schema.

ClientRepository.Create now reads column metadata, uses the existing desktop insert when the column is absent, and keeps the configured company link when the mobile company schema is installed. This performs no schema changes and does not infer company links for existing records.

Acceptance evidence: the actual 1.1.523 repository reproduced SQL error 207 in a separately named, empty EngineerTests database. The actual corrected repository persisted and read back a client with that same column absent. The original fixture defaults are preserved because SELECT INTO does not carry defaults. The company-enabled desktop repository creation and 13 native dashboard/refresh checks also pass against synthetic data. No customer business-save tests, table/column drops or production migrations were performed.

Run TOOLS/LegacyClientCompatibilityChecks.cs as an STA executable beside the Release EXE, Dapper.dll and an EXE.config copied from HVAC_Pro_Desktop.exe.config. The harness rejects any database name outside EngineerTests_<32 hex characters>. Arguments are database name, old or fixed, and result path. The old mode expects SQL error 207; fixed mode requires a successful shared-table save. Fixture setup metadata and results are retained under artifacts/mobile-sync.

1.1.523 release artifacts are immutable. Install the latest 1.1.524 updater/installer instead. Roll back binaries only with deliberate review, preserving SQL data; 1.1.522 is the known pre-regression desktop release. Mobile/API 1.1.506 is unchanged.
