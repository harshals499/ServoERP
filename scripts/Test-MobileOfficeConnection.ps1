#Requires -Version 7.4
param([Parameter(Mandatory)][string]$ConnectionRecord)
$ErrorActionPreference='Stop'
$record=Get-Content -LiteralPath $ConnectionRecord -Raw | ConvertFrom-Json
Add-Type -TypeDefinition @'
using System;
using System.Net.Http;
using System.Security.Cryptography;
public static class ServoMobileHealthProbe {
 public static string Get(string address,string fingerprint) {
  var uri=new Uri(address);
  if(uri.Scheme!="https"||!string.IsNullOrEmpty(uri.UserInfo))throw new ArgumentException("An office HTTPS address is required.");
  using(var handler=new HttpClientHandler {AllowAutoRedirect=false}) {
   handler.ServerCertificateCustomValidationCallback=(request,certificate,chain,errors)=>certificate!=null && request.RequestUri.Authority==uri.Authority && certificate.NotBefore.ToUniversalTime()<=DateTime.UtcNow && certificate.NotAfter.ToUniversalTime()>DateTime.UtcNow && string.Equals(certificate.GetCertHashString(HashAlgorithmName.SHA256),fingerprint,StringComparison.OrdinalIgnoreCase);
   using(var client=new HttpClient(handler){Timeout=TimeSpan.FromSeconds(8)})return client.GetStringAsync(address.TrimEnd('/')+"/health/ready").GetAwaiter().GetResult();
  }
 }
}
'@
$results=@(foreach($address in $record.Addresses){
 $healthy=$false;$failure=''
 try{$healthy=[ServoMobileHealthProbe]::Get($address,$record.CertificateSHA256).Trim() -eq 'Healthy'}catch{$failure=$_.Exception.GetBaseException().Message}
 [pscustomobject]@{Address=$address;Ready=$healthy;Certificate='Exact SHA-256 pin and validity period checked';Error=$failure}
})
$results | ConvertTo-Json -Depth 4
if(!$results.Count -or @($results | Where-Object {!$_.Ready}).Count){throw 'Office HTTPS/SQL readiness validation failed.'}
