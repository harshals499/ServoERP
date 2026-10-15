using System;
using System.Collections.Generic;
using System.Data;
using System.Data.SqlClient;
using System.Linq;
using Dapper;
using FluentValidation;
using HVAC_Pro_Desktop.Models;
using HVAC_Pro_Desktop.Services;
using HVAC_Pro_Desktop.Services.Licensing;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using ServoERP.Validators;

namespace HVAC_Pro_Desktop.OfflineDesktop
{
    internal sealed class OfflineDependencyException : InvalidOperationException { public OfflineDependencyException(string message):base(message) {} }
    internal sealed partial class DesktopSyncServer
    {
        private readonly string _connectionString;
        private readonly OfflineWorkspaceStore _store;
        private readonly OfflineProfile _profile;
        private readonly Func<string,string,bool> _license;
        private static readonly Dictionary<string,string[]> Tables=new Dictionary<string,string[]> {
            {"Clients",new[]{"B2BClients","ClientID"}}, {"Sites",new[]{"ClientSites","SiteID"}}, {"Jobs",new[]{"Jobs","JobID"}}, {"Invoices",new[]{"Invoices","InvoiceID"}}, {"Payments",new[]{"Payments","PaymentID"}}
        };
        public DesktopSyncServer(string connectionString,OfflineWorkspaceStore store,OfflineProfile profile,Func<string,string,bool> license=null) { _connectionString=connectionString;_store=store;_profile=profile;_license=license??((module,action)=>new LicenseService().CanPerform(module,action)); }
        private SqlConnection Open()
        {
            var builder=new SqlConnectionStringBuilder(_connectionString){ConnectTimeout=3};
            var connection=new SqlConnection(builder.ConnectionString);
            try
            {
                connection.Open();
                Guid? office=connection.QuerySingleOrDefault<Guid?>("SELECT OfficeDatabaseId FROM dbo.OfficeDatabaseIdentity WHERE IdentityKey=1");
                if(office!=_store.OfficeId)throw new OfficeDatabaseIdentityMismatchException("This server belongs to a different office. Offline work was retained and not uploaded.");
                FinancialRecordProtection.StampActor(connection);return connection;
            }
            catch { connection.Dispose();throw; }
        }
        public AppUserDto Authenticate(SqlConnection c,SqlTransaction t=null)
        {
            var actor=c.QuerySingleOrDefault<AppUserDto>(@"SELECT u.UserId,u.Username,u.DisplayName,u.RoleId,r.RoleName,u.IsActive,u.ForcePasswordChange,u.LockoutUntil,u.EmployeeID FROM AppUsers u JOIN AppRoles r ON r.RoleId=u.RoleId WHERE u.UserId=@UserId",new{_store.UserId},t);
            var credentials=c.QuerySingleOrDefault("SELECT PasswordHash,PasswordSalt FROM AppUsers WHERE UserId=@UserId",new{_store.UserId},t);
            if(actor==null || !actor.IsActive || actor.ForcePasswordChange || actor.LockoutUntil>DateTime.Now || credentials==null ||
                OfflineWorkspaceStore.Hash((string)credentials.PasswordHash+"|"+(string)credentials.PasswordSalt)!=_profile.CredentialMarker)
                throw new UnauthorizedAccessException("Sign in online again before synchronizing. Your local work has been retained.");
            actor.Permissions=c.Query<RolePermissionDto>("SELECT PermissionId,RoleId,ModuleKey,CanView,CanCreate,CanEdit,CanDelete FROM RolePermissions WHERE RoleId=@RoleId",new{actor.RoleId},t).ToDictionary(p=>p.ModuleKey,StringComparer.OrdinalIgnoreCase);
            return actor;
        }
        private void Demand(AppUserDto user,string module,bool create=false)
        {
            RolePermissionDto p;
            if(user.Permissions==null || !user.Permissions.TryGetValue(OfflineModules.PermissionKey(module),out p) || !p.CanView || (create?!p.CanCreate:!p.CanEdit))
                throw new UnauthorizedAccessException("Your current office permissions do not allow this change.");
            if(!_license(OfflineModules.PermissionKey(module),create?"Create":"Edit")) throw new UnauthorizedAccessException("The current license does not permit this change.");
        }
        public List<OfflineRecord> Snapshot(out AppUserDto actor)
        {
            var records=new List<OfflineRecord>();
            using(var c=Open())
            {
                actor=Authenticate(c);
                foreach(var table in Tables)
                {
                    if(!OfflineModules.HasPermission(actor,table.Key,false) || !_license(OfflineModules.PermissionKey(table.Key),"View")) continue;
                    // Table/column identifiers come only from this fixed whitelist, never user input.
                    foreach(var row in c.Query("SELECT r.*,"+ArchiveProjection(c,null,table.Key)+" FROM dbo.["+table.Value[0]+"] r",new{Module=table.Key},commandTimeout:60))
                    {
                        var data=JObject.FromObject((IDictionary<string,object>)row);
                        int id=data.Value<int>(table.Value[1]);
                        Guid publicId;var identity=Guid.TryParse((string)data["SyncPublicId"],out publicId)&&publicId!=Guid.Empty?publicId:OfflineWorkspaceStore.EntityIdentity(_store.OfficeId,table.Key,id);
                        records.Add(new OfflineRecord {EntityId=identity,Module=table.Key,ServerId=id,Data=data,Revision=OfflineWorkspaceStore.Revision(data),Status=IsArchived(table.Key,data)?"Archived":"Downloaded"});
                    }
                }
                foreach(var row in c.Query("SELECT EntityId,Module,PayloadJson,Revision,Status,ResultId FROM dbo.DesktopOfflineDrafts WHERE OfficeId=@OfficeId",new{_store.OfficeId}))
                {
                    string module=row.Module;if(!OfflineModules.HasPermission(actor,module,false) || !_license(OfflineModules.PermissionKey(module),"View"))continue;
                    records.Add(new OfflineRecord {EntityId=row.EntityId,Module=module,Data=JObject.Parse((string)row.PayloadJson),Revision=row.Revision,Status=row.Status,ServerId=row.ResultId??0});
                }
            }
            return records;
        }
        private static JObject Read(SqlConnection c,SqlTransaction t,string module,int id)
        {
            var map=Tables[module];
            var row=c.QuerySingleOrDefault("SELECT r.*,"+ArchiveProjection(c,t,module)+" FROM dbo.["+map[0]+"] r WITH(UPDLOCK,HOLDLOCK) WHERE r.["+map[1]+"]=@Id",new{Id=id,Module=module},t);
            return row==null?null:JObject.FromObject((IDictionary<string,object>)row);
        }
        private static string ArchiveProjection(SqlConnection connection,SqlTransaction transaction,string module)
        {
            // Preserve the existing client merge-archive boundary in cached lists and conflict fingerprints.
            if(module=="Clients" && connection.QuerySingle<int>("SELECT CASE WHEN OBJECT_ID('dbo.DuplicateMergeArchive','U') IS NULL THEN 0 ELSE 1 END",transaction:transaction)==1)
                return "CAST(CASE WHEN EXISTS(SELECT 1 FROM dbo.DuplicateMergeArchive a WHERE a.ModuleName=@Module AND a.DuplicateRecordID=CONVERT(varchar(30),r.ClientID)) THEN 1 ELSE 0 END AS bit) AS OfflineArchived";
            return "CAST(0 AS bit) AS OfflineArchived";
        }
        private static bool IsArchived(string module,JObject data) => (bool?)data["OfflineArchived"]==true || (bool?)data["IsDeleted"]==true || (module=="Clients" && (bool?)data["IsActive"]==false) || (data["DeletedUtc"]!=null && data["DeletedUtc"].Type!=JTokenType.Null);
        private string Resource(Guid entity) => "ServoERP.Offline."+_store.OfficeId.ToString("N")+"."+entity.ToString("N");
        private void Acquire(SqlConnection c,Guid entity)
        {
            int result=c.QuerySingle<int>("DECLARE @Result int; EXEC @Result=sys.sp_getapplock @Resource=@Resource,@LockMode='Exclusive',@LockOwner='Session',@LockTimeout=0; SELECT @Result",new{Resource=Resource(entity)});
            if(result<0) throw new OfflineDependencyException("Another terminal is synchronizing this record; it will be retried.");
        }
        public OfflineReceipt Apply(OfflineOperation op)
        {
            if(!OfflineModules.CanWrite(op.Module) || op.OperationId==Guid.Empty || op.EntityId==Guid.Empty || op.DeviceId==Guid.Empty || OfflineWorkspaceStore.Revision(op.Data)!=op.PayloadHash) throw new InvalidOperationException("Invalid offline operation envelope.");
            using(var c=Open())
            {
                Acquire(c,op.EntityId);
                try
                {
                    using(var t=c.BeginTransaction(IsolationLevel.Serializable))
                    {
                        var actor=Authenticate(c,t);Demand(actor,op.Module,op.ServerId==0 && string.IsNullOrEmpty(op.BaseRevision));
                        var prior=c.QuerySingleOrDefault("SELECT OfficeId,ActorUserId,EntityId,DeviceId,Module,BaseRevision,RequestedServerId,PayloadHash,ResultJson FROM dbo.DesktopOfflineReceipts WHERE OperationId=@OperationId",new{op.OperationId},t);
                        if(prior!=null)
                        {
                            if(prior.OfficeId!=_store.OfficeId || prior.ActorUserId!=_store.UserId || prior.EntityId!=op.EntityId || (int?)prior.RequestedServerId!=op.ServerId || prior.DeviceId!=op.DeviceId || (string)prior.Module!=op.Module || (string)prior.BaseRevision!=op.BaseRevision || (string)prior.PayloadHash!=op.PayloadHash)throw new InvalidOperationException("An operation ID was reused with different content or identity.");
                            var replay=JsonConvert.DeserializeObject<OfflineReceipt>((string)prior.ResultJson);t.Commit();return replay;
                        }
                        if(op.ReviewOfOperationId.HasValue && c.QuerySingle<int>("SELECT COUNT(*) FROM dbo.DesktopOfflineConflicts WHERE OperationId=@ReviewOfOperationId AND OfficeId=@OfficeId AND EntityId=@EntityId AND Module=@Module AND ResolvedUtc IS NULL",new{op.ReviewOfOperationId,_store.OfficeId,op.EntityId,op.Module},t)!=1)
                            throw new InvalidOperationException("This proposal was already reviewed or belongs to another record.");
                        OfflineReceipt result;
                        if(OfflineModules.IsDraft(op.Module)) result=ApplyDraft(c,t,op);
                        else
                        {
                            var before=op.ServerId>0?Read(c,t,op.Module,op.ServerId):null;
                            int existingId=0;
                            if(op.ServerId==0) {var map=Tables[op.Module];existingId=c.QuerySingleOrDefault<int>("SELECT ["+map[1]+"] FROM dbo.["+map[0]+"] WITH(UPDLOCK,HOLDLOCK) WHERE SyncPublicId=@Id",new{Id=op.EntityId},t);}
                            if(existingId>0)result=Conflict(c,t,op,Read(c,t,op.Module,existingId),"The record already exists at the office. Review it before applying another creation.",existingId);
                            else if(op.ServerId>0 && (before==null || OfflineWorkspaceStore.Revision(before)!=op.BaseRevision)) result=Conflict(c,t,op,before,"The office record changed while this PC was offline.");
                            else if(before!=null && IsArchived(op.Module,before)) result=Conflict(c,t,op,before,"This record was archived at the office. Use the office workflow to review it.");
                            else if(op.Module=="Jobs" && before!=null && ((bool?)before["IsDeleted"]==true || (int?)before["InvoiceId"]>0 || (string)before["PipelineStatus"]=="Closed" || (string)before["PipelineStatus"]=="Invoiced")) result=Conflict(c,t,op,before,"This job is archived, closed or invoiced and needs office review.");
                            else
                            {
                                int id=SaveBusiness(c,t,op,actor);
                                var data=Read(c,t,op.Module,id);
                                result=new OfflineReceipt {OperationId=op.OperationId,EntityId=op.EntityId,ServerId=id,Status="Synced",PreviousData=before,Data=data,Revision=OfflineWorkspaceStore.Revision(data)};
                            }
                        }
                        if(op.ReviewOfOperationId.HasValue && result.Status!="Conflict")
                            c.Execute("UPDATE dbo.DesktopOfflineConflicts SET ResolvedUtc=SYSUTCDATETIME(),ResolvedByUserId=@UserId,Resolution='Applied reviewed proposal' WHERE OperationId=@ReviewOfOperationId AND OfficeId=@OfficeId AND EntityId=@EntityId AND ResolvedUtc IS NULL",new{op.ReviewOfOperationId,_store.OfficeId,_store.UserId,op.EntityId},t);
                        c.Execute("INSERT dbo.DesktopOfflineReceipts(OperationId,DeviceId,Module,BaseRevision,RequestedServerId,OfficeId,ActorUserId,EntityId,PayloadHash,ResultJson) VALUES(@OperationId,@DeviceId,@Module,@BaseRevision,@ServerId,@OfficeId,@UserId,@EntityId,@PayloadHash,@ResultJson)",new{op.OperationId,op.DeviceId,op.Module,op.BaseRevision,op.ServerId,_store.OfficeId,_store.UserId,op.EntityId,op.PayloadHash,ResultJson=JsonConvert.SerializeObject(result)},t);
                        c.Execute("INSERT dbo.AuditLog(UserId,Username,Action,ModuleKey,RecordId,Description) VALUES(@UserId,@Username,'OFFLINE_SYNC',@Module,@RecordId,@Description)",new{actor.UserId,actor.Username,Module=OfflineModules.PermissionKey(op.Module),RecordId=result.ServerId>0?(int?)result.ServerId:null,Description="Offline operation "+op.OperationId+" from device "+op.DeviceId+": "+result.Status},t);
                        t.Commit();return result;
                    }
                }
                finally { c.Execute("EXEC sys.sp_releaseapplock @Resource=@Resource,@LockOwner='Session'",new{Resource=Resource(op.EntityId)}); }
            }
        }
        private OfflineReceipt ApplyDraft(SqlConnection c,SqlTransaction t,OfflineOperation op)
        {
            var previous=c.QuerySingleOrDefault("SELECT PayloadJson,Revision,Status FROM dbo.DesktopOfflineDrafts WITH(UPDLOCK,HOLDLOCK) WHERE EntityId=@EntityId AND OfficeId=@OfficeId",new{op.EntityId,_store.OfficeId},t);
            if(previous!=null && ((string)previous.Status=="Finalized" || (string)previous.Revision!=op.BaseRevision)) return Conflict(c,t,op,JObject.Parse((string)previous.PayloadJson),"The office draft changed or was finalized. Review both copies.");
            if(previous==null && !string.IsNullOrEmpty(op.BaseRevision)) return Conflict(c,t,op,null,"The office draft no longer exists.");
            Validate(op);
            c.Execute(@"IF EXISTS(SELECT 1 FROM dbo.DesktopOfflineDrafts WHERE EntityId=@EntityId) UPDATE dbo.DesktopOfflineDrafts SET PayloadJson=@Payload,Revision=@Revision,UpdatedUtc=SYSUTCDATETIME() WHERE EntityId=@EntityId AND OfficeId=@OfficeId;
ELSE INSERT dbo.DesktopOfflineDrafts(EntityId,OfficeId,ActorUserId,Module,PayloadJson,Revision) VALUES(@EntityId,@OfficeId,@UserId,@Module,@Payload,@Revision);",new{op.EntityId,_store.OfficeId,_store.UserId,op.Module,Payload=op.Data.ToString(Formatting.None),Revision=op.PayloadHash},t);
            return new OfflineReceipt {OperationId=op.OperationId,EntityId=op.EntityId,Status="Draft",Data=op.Data,Revision=op.PayloadHash};
        }
        private OfflineReceipt Conflict(SqlConnection c,SqlTransaction t,OfflineOperation op,JObject server,string reason,int? actualServerId=null)
        {
            c.Execute("INSERT dbo.DesktopOfflineConflicts(OperationId,OfficeId,ActorUserId,EntityId,Module,ServerId,BaseRevision,LocalJson,ServerJson,Reason) VALUES(@OperationId,@OfficeId,@UserId,@EntityId,@Module,@ServerId,@BaseRevision,@LocalJson,@ServerJson,@Reason)",new{op.OperationId,_store.OfficeId,_store.UserId,op.EntityId,op.Module,ServerId=actualServerId??op.ServerId,op.BaseRevision,LocalJson=op.Data.ToString(Formatting.None),ServerJson=server?.ToString(Formatting.None),Reason=reason},t);
            return new OfflineReceipt {OperationId=op.OperationId,EntityId=op.EntityId,ServerId=actualServerId??op.ServerId,Status="Conflict",Data=op.Data,Revision=op.BaseRevision,ServerCopy=server,Error=reason};
        }
        internal static void Validate(OfflineOperation op)
        {
            if(op.Data==null || op.Data.ToString(Formatting.None).Length>200000)throw new InvalidOperationException("Offline record is missing or too large.");
            if(op.Module=="Clients") new B2BClientValidator().ValidateAndThrow(OfflineJson.Model<B2BClient>(op.Data));
            else if(op.Module=="Sites") { if(string.IsNullOrWhiteSpace((string)op.Data["SiteName"]))throw new FluentValidation.ValidationException("Site name is required."); }
            else if(op.Module=="Jobs")
            {
                var job=OfflineJson.Model<Job>(op.Data);if(job.ClientID==0 && op.Data["ClientEntityId"]!=null)job.ClientID=1;
                new JobValidator().ValidateAndThrow(job);
            }
            else if(op.Module=="InvoiceDrafts")
            {
                var items=op.Data["LineItems"] as JArray;
                if(items==null || items.Count==0 || items.Any(item=>string.IsNullOrWhiteSpace((string)item["Description"]) || ((decimal?)item["Quantity"]??0)<=0 || ((decimal?)item["Rate"]??-1)<0))throw new FluentValidation.ValidationException("Enter invoice descriptions, positive quantities and nonnegative rates.");
            }
            else if(op.Module=="PaymentDrafts" && (((int?)op.Data["InvoiceID"]??0)<=0 || ((decimal?)op.Data["AmountPaid"]??0)<=0)) throw new FluentValidation.ValidationException("Select a downloaded invoice and enter a positive payment amount.");
        }
        private int ResolveClient(SqlConnection c,SqlTransaction t,JObject data)
        {
            int id=(int?)data["ClientID"]??0;
            if(id<=0) { Guid identity=Guid.Parse((string)data["ClientEntityId"]);id=c.QuerySingleOrDefault<int>("SELECT ClientID FROM dbo.B2BClients WHERE SyncPublicId=@Identity",new{Identity=identity},t); }
            if(id<=0 || c.QuerySingle<int>("SELECT COUNT(*) FROM dbo.B2BClients WHERE ClientID=@Id AND IsActive=1",new{Id=id},t)!=1)throw new OfflineDependencyException("The related client must synchronize or be reviewed first.");
            var client=Read(c,t,"Clients",id);
            if(client==null || IsArchived("Clients",client))throw new OfflineDependencyException("The related client was archived at the office and needs review.");
            return id;
        }
        private int SaveBusiness(SqlConnection c,SqlTransaction t,OfflineOperation op,AppUserDto actor)
        {
            Validate(op);int id=op.ServerId;var p=new DynamicParameters();
            p.Add("Id",id);p.Add("PublicId",op.EntityId);p.Add("UserId",actor.UserId);p.Add("Name",actor.DisplayName);
            string table=Tables[op.Module][0],key=Tables[op.Module][1];
            if(id==0)
            {
                // A different operation may have created the same GUID; never overwrite it implicitly.
                int existing=c.QuerySingleOrDefault<int>("SELECT ["+key+"] FROM dbo.["+table+"] WHERE SyncPublicId=@PublicId",new{PublicId=op.EntityId},t);
                if(existing>0)throw new InvalidOperationException("This entity already exists under another operation. Refresh and review it.");
            }
            if(op.Module=="Clients")
            {
                var data=OfflineJson.Model<B2BClient>(op.Data);p.Add("CompanyName",data.CompanyName);p.Add("Phone",data.Phone);p.Add("Email",data.Email);p.Add("PrimaryContact",data.PrimaryContact);p.Add("BillingAddress",data.BillingAddress);p.Add("City",data.City);p.Add("Notes",data.Notes);
                if(id==0) id=c.QuerySingle<int>(@"INSERT dbo.B2BClients(CompanyName,Phone,Email,PrimaryContact,BillingAddress,City,Notes,CustomerSince,IsActive,PaymentTermsDays,CreditLimit,TotalAnnualValue,SyncPublicId) VALUES(@CompanyName,@Phone,@Email,@PrimaryContact,@BillingAddress,@City,@Notes,GETDATE(),1,30,0,0,@PublicId);SELECT CAST(SCOPE_IDENTITY() AS int)",p,t);
                else c.Execute("UPDATE dbo.B2BClients SET CompanyName=@CompanyName,Phone=@Phone,Email=@Email,PrimaryContact=@PrimaryContact,BillingAddress=@BillingAddress,City=@City,Notes=@Notes WHERE ClientID=@Id",p,t);
            }
            else if(op.Module=="Sites")
            {
                p.Add("ClientID",ResolveClient(c,t,op.Data));p.Add("SiteName",(string)op.Data["SiteName"]);p.Add("Address",(string)op.Data["Address"]);p.Add("City",(string)op.Data["City"]);
                if(id==0) id=c.QuerySingle<int>("INSERT dbo.ClientSites(ClientID,SiteName,Address,City,ACSystemCount,RefrigerationSystemCount,CoolingTowerCount,IsCritical,TravelRateINR,SyncPublicId) VALUES(@ClientID,@SiteName,@Address,@City,0,0,0,0,0,@PublicId);SELECT CAST(SCOPE_IDENTITY() AS int)",p,t);
                else c.Execute("UPDATE dbo.ClientSites SET ClientID=@ClientID,SiteName=@SiteName,Address=@Address,City=@City WHERE SiteID=@Id",p,t);
            }
            else
            {
                int client=ResolveClient(c,t,op.Data);p.Add("ClientID",client);int? site=(int?)op.Data["SiteID"];
                if((site??0)==0 && op.Data["SiteEntityId"]!=null && Guid.TryParse((string)op.Data["SiteEntityId"],out var siteGuid))site=c.QuerySingleOrDefault<int?>("SELECT SiteID FROM dbo.ClientSites WHERE SyncPublicId=@Id AND ClientID=@ClientID",new{Id=siteGuid,ClientID=client},t);
                if((site??0)>0 && c.QuerySingle<int>("SELECT COUNT(*) FROM dbo.ClientSites WHERE SiteID=@SiteID AND ClientID=@ClientID",new{SiteID=site,ClientID=client},t)!=1)throw new InvalidOperationException("The job site does not belong to this client.");
                var job=OfflineJson.Model<Job>(op.Data);job.JobID=id;job.ClientID=client;job.SiteID=site??0;
                new JobService().PrepareOfflineSync(job,id==0);
                p.Add("SiteID",site>0?site:null);p.Add("Title",job.Title);p.Add("Description",job.Description);p.Add("ScheduledDate",job.ScheduledDate);p.Add("Priority",job.Priority);p.Add("Notes",job.Notes);p.Add("Status",job.Status);p.Add("Pipeline",job.PipelineStatus);p.Add("JobType",job.JobType);
                if(id==0) { p.Add("Number",job.JobNumber); id=c.QuerySingle<int>(@"INSERT dbo.Jobs(JobNumber,ClientID,SiteID,Title,JobTitle,Description,ScheduledDate,Priority,Status,PipelineStatus,JobType,EstimatedCost,Revenue,QuotedRevenue,ActualRevenue,IsOverdue,Notes,CreatedByUserId,CreatedByName,SyncPublicId) VALUES(@Number,@ClientID,@SiteID,@Title,@Title,@Description,@ScheduledDate,@Priority,@Status,@Pipeline,@JobType,0,0,0,0,0,@Notes,@UserId,@Name,@PublicId);SELECT CAST(SCOPE_IDENTITY() AS int)",p,t); }
                else c.Execute("UPDATE dbo.Jobs SET ClientID=@ClientID,SiteID=@SiteID,Title=@Title,JobTitle=@Title,Description=@Description,ScheduledDate=@ScheduledDate,Priority=@Priority,Notes=@Notes,ModifiedByUserId=@UserId,ModifiedByName=@Name,ModifiedDate=GETDATE() WHERE JobID=@Id",p,t);
                if(op.ServerId==0)HVAC_Pro_Desktop.DAL.JobRepository.ApplyChecklistTemplate(c,t,id,job.JobType);
                HVAC_Pro_Desktop.DAL.JobRepository.AppendActivity(c,t,id,op.ServerId==0?"Job created from offline work":"Job scheduling / notes synchronized",actor.DisplayName,"Success");
            }
            p.Add("Id",id);
            c.Execute("UPDATE dbo.["+table+"] SET SyncPublicId=COALESCE(SyncPublicId,@PublicId),SyncVersion=ISNULL(SyncVersion,0)+1,UpdatedUtc=SYSUTCDATETIME() WHERE ["+key+"]=@Id",p,t);
            return id;
        }
    }
}
