# Automatic desktop/mobile office data

Candidates: desktop 1.1.523, Office API 1.1.506, Android 1.1.506 (528). Publication is tracked in the release artifact evidence; the desktop and mobile/API use separate activation gates.

The office SQL Server database remains authoritative. Mobile client, employee and job forms use the same rows and real relationship IDs as desktop repositories. There is no separate business workspace or SQLite business-write store. Existing field-service queues remain unchanged.

While mobile is active, reference data refreshes every 30 seconds and on resume/reconnection. Open forms update their relationship options while retaining typed input, selected record identities and the original edit version. A confirmed save remains confirmed when its subsequent refresh disconnects. A stale edit is rejected for review rather than overwriting a desktop change. Administrative saves require the office connection.

Desktop Clients, Employees and Jobs dashboards refresh every 30 seconds while visible and active. Refreshes do not overlap; hidden pages, editors and active input are protected. New clients and employees use OfficeApi/ActiveCompanyId (existing default 1). Employees are linked through the existing MobileEmployeeCompanies table in the same transaction as creation. Desktop-only databases without that optional table retain their prior employee create behavior. Existing unmapped employees in multi-company databases still require an intentional company assignment; they are not exposed to every company automatically.

The phone and desktop must connect to the same office database/company. Existing role permissions determine visibility and create/edit access. Client validation follows desktop rules; company scope, idempotent save receipts and optimistic conflicts remain enforced. No new service, permission grant or production schema migration is introduced.

## Evidence and limits

87 standalone mobile/core tests pass. Android Release build and both ABI assembly-store checks pass. Office API and desktop Release builds pass. SQL integration tests use only EngineerTests_f7fe0dfae9fb4ccebcda63b7914c24df, cloned from synthetic MobileE2E data. Actual desktop repositories create/read/edit the mobile records, and API refresh returns desktop changes. Stale edits and cross-company client edits are rejected. Native desktop Clients/Employees/Jobs screenshots and refresh-gate checks are recorded under artifacts/mobile-sync. The final APK native test additionally created job 16 using newly refreshed desktop client 7 and employee 22, and SQL verified those exact IDs. The stalled offscreen desktop harness was diagnosed as a missing EXE binding configuration; rerunning with the actual desktop config passed all 13 native checks on 1.1.523. Native Android evidence is under artifacts/1.1.506/screenshots in the mobile worktree.

Production HVAC_PRO was not used for write tests. Test-only missing legacy fixture objects are isolated in ServoERP.OfficeSync.Checks/DesktopTestSchema.sql. No customer migration, closed-app background guarantee, production deployment or public updater campaign is claimed. Full update-install validation and release packaging remain separate release gates.

The Office API baseline is the user-approved verified deployed 1.1.504 source archive (see ApprovedOfficeApiBaseline.md). Existing committed findings/handoffs/return-visit contracts and service workflows are preserved. Optional job sites use the actual desktop schema: company scope through B2BClients, with a LEFT JOIN for ClientSites.
