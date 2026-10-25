using System.Text.Json;
using System.Text.RegularExpressions;
using Dapper;
using Microsoft.Data.SqlClient;
using ServoERP.Api.Mobile;
using ServoERP.Engineer;

if(args.Length!=3 || !Regex.IsMatch(args[0],"^EngineerTests_[a-f0-9]{32}$"))throw new Exception("An explicitly disposable integration database, mode and output folder are required.");
string database=args[0],mode=args[1],output=Path.GetFullPath(args[2]);Directory.CreateDirectory(output);
string connection=$"Server=.\\SQLEXPRESS;Database={database};Integrated Security=True;TrustServerCertificate=True";
await using var db=new SqlConnection(connection);await db.OpenAsync();
string company=await db.QuerySingleAsync<string>("SELECT CompanyName FROM Companies WHERE CompanyId=1");
if(!company.Contains("disposable",StringComparison.OrdinalIgnoreCase))throw new Exception("The source must identify a synthetic disposable company before any write.");
var identity=new EngineerIdentity(1,1,1,1,"Synthetic sync QA");var service=new OfficeWorkspaceService(connection);var checks=new List<string>();
void Require(bool value,string detail){if(!value)throw new Exception(detail);checks.Add(detail);}
DemoRecord New(string form,params (string Key,string Value)[] values)=>new(){Form=form,Values=values.ToDictionary(v=>v.Key,v=>v.Value)};
if(mode=="seed")
{
 await db.ExecuteAsync("UPDATE RolePermissions SET CanView=1,CanCreate=1,CanEdit=1 WHERE RoleId=1 AND ModuleKey IN('Clients','Employees','WorkOrders','GeoIntelligence')");
 var snapshot=await service.Read(identity,default);Require(snapshot.Permissions["client"].CanCreate,"client creation follows configured office permission");
 string suffix=Guid.NewGuid().ToString("N")[..8];
 var clientRequest=new OfficeSaveRequest(Guid.NewGuid(),New("client",("name","Sync QA Client "+suffix),("phone","9999999999"),("email","qa@example.invalid"),("address","Synthetic test address"),("city","Pune")));
 var client=await service.Save(identity,clientRequest,default);var retry=await service.Save(identity,clientRequest,default);Require(client.ServerId==retry.ServerId,"repeated mobile client save replays the same receipt");
 Require(await db.ExecuteScalarAsync<int>("SELECT COUNT(*) FROM B2BClients WHERE ClientID=@id AND CompanyId=1 AND SyncPublicId IS NOT NULL",new{id=client.ServerId})==1,"mobile client is one shared desktop SQL record with sync identity");
 var employee=await service.Save(identity,new(Guid.NewGuid(),New("employee",("name","Sync QA Employee "+suffix),("date",DateTime.Today.ToString("dd/MM/yyyy")),("status","Active"))),default);
 Require(await db.ExecuteScalarAsync<int>("SELECT COUNT(*) FROM Employees WHERE EmployeeID=@id AND Name=@name",new{id=employee.ServerId,name=employee.Title})==1,"mobile employee is in the desktop Employees table");
 var jobRequest=new OfficeSaveRequest(Guid.NewGuid(),New("job",("name","Sync QA Job "+suffix),("client",client.Id.ToString()),("employee",employee.Id.ToString()),("date",DateTime.Today.ToString("dd/MM/yyyy")),("time","09:30"),("type","General"),("priority","Medium"),("status","Created")));
 var job=await service.Save(identity,jobRequest,default);Require((await service.Save(identity,jobRequest,default)).ServerId==job.ServerId,"repeated mobile job save does not create a second job");
 Require(await db.ExecuteScalarAsync<int>("SELECT COUNT(*) FROM Jobs WHERE JobID=@id AND ClientID=@client AND AssignedEmployeeID=@employee AND Title=JobTitle",new{id=job.ServerId,client=client.ServerId,employee=employee.ServerId})==1,"mobile job uses real desktop client/employee IDs and desktop title aliases");
 var fresh=await service.Read(identity,default);Require(new[]{client.Id,employee.Id,job.Id}.All(id=>fresh.Records.Any(r=>r.Id==id)),"automatic workspace snapshot contains newly created client, employee and job");
 try{await service.Save(identity with{CompanyId=2},new(Guid.NewGuid(),client),default);throw new Exception("Cross-company edit accepted");}catch(MobileHttpException ex)when(ex.Status==404){checks.Add("cross-company client edit is rejected");}
 try{await service.Save(identity,new(Guid.NewGuid(),New("client",("name","Invalid email QA"),("email","invalid"))),default);throw new Exception("Invalid client accepted");}catch(FieldRuleException){checks.Add("desktop-equivalent client email validation rejects invalid input");}
 var fieldJobs=await new MobileJobService(connection).Jobs(identity with{EmployeeId=employee.ServerId},default);
 Require(fieldJobs.Any(j=>j.Number==job.Get("number")),"assigned jobs without an optional site use the real desktop company scope");
 await File.WriteAllTextAsync(Path.Combine(output,"records.json"),JsonSerializer.Serialize(new{client,employee,job},new JsonSerializerOptions{WriteIndented=true}));
}
else if(mode=="configure")
{
 await db.ExecuteAsync("UPDATE AppUsers SET Username='syncqa',Email='syncqa@example.invalid',DisplayName='Synthetic sync QA',PasswordHash=@hash,PasswordSalt='bcrypt',IsActive=1,ForcePasswordChange=0,FailedAttempts=0,LockoutUntil=NULL,EmployeeID=1,RoleId=1 WHERE UserId=1",new{hash=BCrypt.Net.BCrypt.HashPassword("SyncQa-Only9!",4)});
 var tokens=await new MobileAuthService(connection).Login(new("syncqa","SyncQa-Only9!",1,"synthetic-sync-qa"),default);
 Require(tokens.CompanyId==1,"synthetic account authenticates only against the disposable test database");
}
else if(mode=="live")
{
 var lines=await File.ReadAllLinesAsync(Path.Combine(output,"desktop-live-lookups.txt"));
 var snapshot=await service.Read(identity,default);
 foreach(string form in new[]{"client","employee","job"})
 {
  int index=Array.IndexOf(new[]{"client","employee","job"},form);
  Require(snapshot.Records.Any(r=>r.Form==form&&r.Title==lines[index]),$"new desktop {form} automatically appears in the company-scoped mobile snapshot");
 }
 var jobs=await new MobileJobService(connection).Jobs(identity,default);
 Require(jobs.Any(j=>j.PublicId==db.QuerySingle<Guid>("SELECT SyncPublicId FROM Jobs WHERE JobID=@id",new{id=int.Parse(lines[5])})),"assigned desktop job without optional site loads through the field Jobs endpoint");
}
else if(mode=="verify")
{
 using var saved=JsonDocument.Parse(await File.ReadAllTextAsync(Path.Combine(output,"records.json")));
 var snapshot=await service.Read(identity,default);
 foreach(string name in new[]{"client","employee","job"})
 {
  var original=JsonSerializer.Deserialize<DemoRecord>(saved.RootElement.GetProperty(name))!;
  var current=snapshot.Records.Single(r=>r.Id==original.Id);Require(current.Title.EndsWith(" desktop"),$"desktop {name} edit returns through mobile workspace refresh");
  try{await service.Save(identity,new(Guid.NewGuid(),original),default);throw new Exception("Stale edit accepted");}catch(MobileHttpException ex)when(ex.Status==409){checks.Add($"stale mobile {name} edit is rejected without overwriting desktop changes");}
  current.Values["name"]+=" mobile";var changed=await service.Save(identity,new(Guid.NewGuid(),current),default);
  Require(changed.Title.EndsWith(" desktop mobile"),$"refreshed {name} remains editable from mobile after desktop edit");
 }
}
else throw new Exception("Use seed or verify mode.");
await File.WriteAllTextAsync(Path.Combine(output,mode+"-validation.json"),JsonSerializer.Serialize(new{database,mode,passed=true,checks,utc=DateTime.UtcNow.ToString("o")},new JsonSerializerOptions{WriteIndented=true}));
foreach(var check in checks)Console.WriteLine("PASS "+check);
