param(
    [Parameter(Mandatory=$true)][string]$ConnectionString,
    [Parameter(Mandatory=$true)][int]$UserId,
    [int]$CompanyId = 1,
    [int]$ValidDays = 90
)

$bytes = New-Object byte[] 32
[System.Security.Cryptography.RandomNumberGenerator]::Create().GetBytes($bytes)
$plainToken = [Convert]::ToBase64String($bytes)
$hash = [System.Security.Cryptography.SHA256]::HashData([System.Text.Encoding]::UTF8.GetBytes($plainToken))
$connection = New-Object System.Data.SqlClient.SqlConnection $ConnectionString
try {
    $connection.Open()
    $cmd = $connection.CreateCommand()
    $cmd.CommandText = @'
IF NOT EXISTS (SELECT 1 FROM UserCompanies WHERE UserId=@UserId AND CompanyId=@CompanyId AND IsActive=1)
    THROW 51000, 'User is not authorized for the requested company.', 1;
UPDATE ApiUserTokens SET IsActive=0 WHERE UserId=@UserId AND IsActive=1;
INSERT ApiUserTokens(UserId,TokenHash,ExpiresUtc) VALUES(@UserId,@TokenHash,DATEADD(day,@ValidDays,SYSUTCDATETIME()));
'@
    [void]$cmd.Parameters.AddWithValue('@UserId',$UserId)
    [void]$cmd.Parameters.AddWithValue('@CompanyId',$CompanyId)
    $parameter=$cmd.Parameters.Add('@TokenHash',[System.Data.SqlDbType]::VarBinary,32); $parameter.Value=$hash
    [void]$cmd.Parameters.AddWithValue('@ValidDays',$ValidDays)
    [void]$cmd.ExecuteNonQuery()
    Write-Host 'User API token created. Give this value to the named user only through a secure channel; it is shown once:'
    Write-Output $plainToken
}
finally { $connection.Dispose() }
