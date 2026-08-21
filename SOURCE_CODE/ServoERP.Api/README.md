# ServoERP.Api

Private-office API migration slice. It is intentionally separate from the WinForms release and uses the same authoritative `HVAC_PRO` SQL Server.

## Run locally

Set two machine-level secrets (do not put them in source control): `SERVOERP_DATABASE_CONNECTION` and a long random `SERVOERP_API_KEY`. Then run:

```powershell
dotnet run --project SOURCE_CODE\ServoERP.Api --urls https://localhost:7443
```

`GET /health` is liveness only. All `/api/v1/*` endpoints require `X-ServoERP-Api-Key`. `GET /api/v1/health` verifies that the API is reaching the expected central SQL Server.

## Production deployment

Publish with `dotnet publish SOURCE_CODE\ServoERP.Api -c Release -o C:\ServoERP\Api`. Install on the office server with `scripts\api\Install-ServoERPApiService.ps1`. Bind an office-server TLS certificate before opening port 7443 to LAN clients. Do not expose SQL Server or this API to the internet; use a VPN for remote users.

The API uses `Microsoft.Data.SqlClient`, which validates SQL Server certificates by default. Production SQL Server must use a certificate trusted by the API service account. `Encrypt=False` is only appropriate for an isolated development smoke test and must not be used in the production service configuration.

After the company-isolation migration, issue a per-user API token with `scripts\api\New-ServoERPApiUserToken.ps1`. The desktop sends the installation key, the signed-in user identity, the DPAPI-protected user token, and the selected authorized company. The server validates all four before executing an API route.

The desktop now routes payments, stock movements, and purchase receiving through the Office API when enabled. Run `HVAC_Pro_Desktop.exe /officeapiconfig` on each client PC, test the authenticated health endpoint, then save. In API mode those writes fail closed: they do not fall back to SQL if the API is unavailable. Other desktop modules remain on the existing SQL compatibility path and must be migrated before SQL credentials can be removed from client PCs.
