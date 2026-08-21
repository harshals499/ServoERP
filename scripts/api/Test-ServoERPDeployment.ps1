param(
 [Parameter(Mandatory=$true)][string]$ApiUrl,
 [Parameter(Mandatory=$true)][string]$ApiKey,
 [int]$UserId,
 [int]$CompanyId,
 [string]$UserToken,
 [int]$UnauthorizedCompanyId
)
$ErrorActionPreference='Stop'
$api=$ApiUrl.TrimEnd('/')
$live=Invoke-RestMethod "$api/health" -TimeoutSec 10
if($live.status -ne 'running'){throw 'API liveness failed.'}
try { Invoke-WebRequest "$api/api/v1/health" -UseBasicParsing -TimeoutSec 10|Out-Null; throw 'Unauthenticated health unexpectedly succeeded.' } catch { if($_.Exception.Response.StatusCode.value__ -ne 401){throw} }
if($UserId -gt 0 -and $CompanyId -gt 0 -and $UserToken){
 $headers=@{'X-ServoERP-Api-Key'=$ApiKey;'X-ServoERP-User-Id'=$UserId;'X-ServoERP-Company-Id'=$CompanyId;'X-ServoERP-User-Token'=$UserToken}
 $health=Invoke-RestMethod "$api/api/v1/health" -Headers $headers -TimeoutSec 10
 if($health.api -ne 'online' -or $health.companyIsolationSchemaVersion -ne '2'){throw 'Authenticated API readiness/schema test failed.'}
 if($UnauthorizedCompanyId -gt 0){
   $forbidden=@{} + $headers; $forbidden['X-ServoERP-Company-Id']=$UnauthorizedCompanyId
   try { Invoke-WebRequest "$api/api/v1/health" -Headers $forbidden -UseBasicParsing -TimeoutSec 10|Out-Null; throw 'Unauthorized company health unexpectedly succeeded.' } catch { if($_.Exception.Response.StatusCode.value__ -ne 403){throw} }
 }
}
Write-Output 'PASS ServoERP API deployment smoke checks.'
