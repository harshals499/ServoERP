using System;
using System.Collections.Generic;
using System.Data.SqlClient;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Dapper;
using HVAC_Pro_Desktop.DAL;
using HVAC_Pro_Desktop.Models;
using HVAC_Pro_Desktop.Services;
using HVAC_Pro_Desktop.Services.Licensing;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace HVAC_Pro_Desktop.OfflineDesktop
{
    internal static class OfflineDesktopCoordinator
    {
        private static readonly SemaphoreSlim Flight=new SemaphoreSlim(1,1);
        private static CancellationTokenSource _cancellation;
        private static Task _worker;
        private static bool _legalConfirmedForSession;
        internal static Guid? TestOfficeId { get; set; }
        public static bool IsDisconnected { get; private set; }
        public static OfflineWorkspaceStore Store { get; private set; }
        public static string Status { get; private set; }="Preparing offline workspace...";
        public static event EventHandler Changed;
        internal static string TestDirectory { get; set; }
        internal static string TestServerKey { get; set; }
        internal static string TestConnectionString { get; set; }
        public static string DirectoryName => TestDirectory ?? Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),"ServoERP","OfflineDesktop");
        internal static string ConnectionString => TestConnectionString ?? DatabaseManager.RequireConfiguredConnectionString();
        internal static string ServerKey { get { var b=new SqlConnectionStringBuilder(ConnectionString);return TestServerKey??(b.DataSource+"|"+b.InitialCatalog); } }
        internal static Guid OfficeId
        {
            get
            {
                if(TestOfficeId.HasValue)return TestOfficeId.Value;
                Guid id;if(!Guid.TryParse(ConfigService.Get("Database","OfficeDatabaseId",string.Empty),out id)) return Guid.Empty;return id;
            }
        }
        private static void Announce(string message)
        {
            Status=message;
            foreach(EventHandler handler in Changed?.GetInvocationList()??new Delegate[0])
                try{handler(null,EventArgs.Empty);}catch(Exception ex){AppLogger.LogError("OfflineDesktop.StatusHandler",ex);}
        }
        public static void RecordOnlineLogin(AppUserDto user,string password)
        {
            if(user==null || !user.IsActive || user.ForcePasswordChange || string.IsNullOrWhiteSpace(password) || OfficeId==Guid.Empty)return;
            try
            {
                var credentials=new AuthRepository().GetCredentialRecord(user.Username);
                if(!credentials.HasValue)return;
                var store=new OfflineWorkspaceStore(DirectoryName,OfficeId,ServerKey,user.UserId);
                var old=store.Profile();
                store.SaveProfile(new OfflineProfile {User=user,VerifierHash=SecurityHelpers.HashPasswordWithBcrypt(password),CredentialMarker=OfflineWorkspaceStore.Hash(credentials.Value.PasswordHash+"|"+credentials.Value.PasswordSalt),LastOnlineUtc=DateTime.UtcNow,LastObservedUtc=DateTime.UtcNow,LastSnapshotUtc=old?.LastSnapshotUtc,LegalAccepted=old?.LegalAccepted??false},true);
            }
            catch(Exception ex) { AppLogger.LogError("OfflineDesktop.Enroll",ex); }
        }
        public static bool HasPreparedWorkspace()
        {
            if(OfficeId==Guid.Empty || !System.IO.Directory.Exists(DirectoryName))return false;
            return System.IO.Directory.GetFiles(DirectoryName,"desktop-*.sqlite").Any();
        }
        public static OfflineProfile OfflineLogin(string username,string password)
        {
            var id=OfflineWorkspaceStore.FindUser(DirectoryName,OfficeId,ServerKey,username);
            if(!id.HasValue)throw new UnauthorizedAccessException("Connect to the office server and sign in online once to prepare this PC.");
            var store=new OfflineWorkspaceStore(DirectoryName,OfficeId,ServerKey,id.Value);var profile=store.Profile();var now=DateTime.UtcNow;
            if(profile==null || !profile.User.IsActive || profile.User.ForcePasswordChange || !profile.LegalAccepted || now-profile.LastOnlineUtc>TimeSpan.FromDays(30) || now<profile.LastObservedUtc.AddMinutes(-5))
                throw new UnauthorizedAccessException("This offline sign-in needs an online refresh. Your pending work has been retained.");
            if(profile.LockoutUntilUtc>now) throw new UnauthorizedAccessException("This account is temporarily locked. Try again after 15 minutes.");
            profile.LastObservedUtc=now;
            if(!SecurityHelpers.VerifyPassword(password,profile.VerifierHash,"bcrypt"))
            {
                profile.FailedAttempts++;if(profile.FailedAttempts>=5)profile.LockoutUntilUtc=now.AddMinutes(15);store.SaveProfile(profile);throw new UnauthorizedAccessException("Invalid username or password.");
            }
            profile.FailedAttempts=0;profile.LockoutUntilUtc=null;store.SaveProfile(profile);
            SessionManager.SetSession(profile.User);Store=store;BeginOfflineStartup();return profile;
        }
        public static bool CanUseCurrentWorkspace()
        {
            try
            {
                if(SessionManager.CurrentUser==null || OfficeId==Guid.Empty)return false;
                if(Store!=null && Store.UserId==SessionManager.CurrentUser.UserId && Store.OfficeId==OfficeId)return true;
                var store=new OfflineWorkspaceStore(DirectoryName,OfficeId,ServerKey,SessionManager.CurrentUser.UserId);
                if(store.Profile()==null)return false;Store=store;return true;
            }
            catch(Exception ex)
            {
                Status="Offline cache needs attention; online work can continue. Local files have been retained.";
                AppLogger.LogError("OfflineDesktop.OpenWorkspace",ex);return false;
            }
        }
        public static int LegacyPendingCount()
        {
            string path=LocalSqliteFallbackStore.GetDatabasePath();if(!File.Exists(path))return 0;
            try
            {
                using(var c=new System.Data.SQLite.SQLiteConnection(new System.Data.SQLite.SQLiteConnectionStringBuilder {DataSource=path,Version=3,ReadOnly=true}.ToString()))
                {
                    c.Open();if(c.QuerySingle<int>("SELECT COUNT(*) FROM sqlite_master WHERE type='table' AND name='OfflineSyncQueue'")==0)return 0;
                    return c.QuerySingle<int>("SELECT COUNT(*) FROM OfflineSyncQueue WHERE Status IN ('Pending','Failed','Conflict')");
                }
            }
            catch(Exception ex){AppLogger.LogError("OfflineDesktop.LegacyQueueReview",ex);return 0;}
        }
        public static void InvalidateKnownCredentials(string username,string passwordHash,string salt,bool active,bool forced)
        {
            if(OfficeId==Guid.Empty || !System.IO.Directory.Exists(DirectoryName))return;
            try
            {
                var user=new AuthRepository().GetUserByUsername(username);if(user==null)return;
                var store=new OfflineWorkspaceStore(DirectoryName,OfficeId,ServerKey,user.UserId);var profile=store.Profile();if(profile==null)return;
                if(!active || forced || profile.CredentialMarker!=OfflineWorkspaceStore.Hash(passwordHash+"|"+salt))
                {profile.User.IsActive=false;profile.User.ForcePasswordChange=forced;store.SaveProfile(profile);}
            }
            catch(Exception ex){AppLogger.LogError("OfflineDesktop.RefreshCredentialState",ex);}
        }
        public static void Start(bool legalAccepted=false)
        {
            if(_worker!=null && !_worker.IsCompleted && _cancellation!=null && !_cancellation.IsCancellationRequested && Store?.UserId==SessionManager.CurrentUser?.UserId)return;
            Stop();if(!CanUseCurrentWorkspace())return;
            _legalConfirmedForSession=legalAccepted;
            if(legalAccepted) {var profile=Store.Profile();if(profile!=null){profile.LegalAccepted=true;Store.SaveProfile(profile);}}_cancellation=new CancellationTokenSource();var token=_cancellation.Token;
            _worker=Task.Run(async()=>
            {
                while(!token.IsCancellationRequested)
                {
                    await SyncOnceAsync(token).ConfigureAwait(false);
                    try { await Task.Delay(IsDisconnected?15000:60000,token).ConfigureAwait(false); }catch(OperationCanceledException){break;}
                }
            },token);
        }
        public static void Stop() { _cancellation?.Cancel();_cancellation=null; }
        public static async Task SyncOnceAsync(CancellationToken token=default(CancellationToken))
        {
            if(!await Flight.WaitAsync(0,token).ConfigureAwait(false))return;
            try
            {
                var store=Store;if(store==null || store.UserId!=SessionManager.CurrentUser?.UserId)return;
                var profile=store.Profile();if(profile==null)return;
                await Task.Run(()=>
                {
                    token.ThrowIfCancellationRequested();store.RecoverInterruptedSends();var server=new DesktopSyncServer(ConnectionString,store,profile);
                    // Authenticate/read the office before claiming any durable local operation.
                    AppUserDto actor;var snapshot=server.Snapshot(out actor);
                    profile.User=actor;profile.LastOnlineUtc=DateTime.UtcNow;
                    store.MergeSnapshot(snapshot,OfflineModules.All.Where(m=>OfflineModules.HasPermission(actor,m,false)));IsDisconnected=false;Announce("Office server connected - synchronizing pending work...");
                    foreach(var pending in store.Operations(true).OrderBy(op=>op.Module=="Clients"?0:op.Module=="Sites"?1:op.Module=="Jobs"?2:3).Take(100))
                    {
                        token.ThrowIfCancellationRequested();var op=store.Claim(pending.OperationId);if(op==null)continue;
                        try { store.Acknowledge(op,server.Apply(op)); }
                        catch(OfflineDependencyException ex) { store.Retry(op,ex.Message); }
                        catch(Exception ex)
                        {
                            if(IsConnectivityFailure(ex)) {store.Retry(op,SensitiveDataRedactor.Redact(ex.Message));throw;}
                            store.Acknowledge(op,new OfflineReceipt {OperationId=op.OperationId,EntityId=op.EntityId,ServerId=op.ServerId,Status="Blocked",Data=op.Data,Revision=op.BaseRevision,Error=SensitiveDataRedactor.Redact(ex.Message)});
                            Announce("Office review or sign-in required: "+SensitiveDataRedactor.Redact(ex.Message));continue;
                        }
                    }
                    snapshot=server.Snapshot(out actor);
                    foreach(var conflict in store.Operations().Where(o=>o.Status=="Conflict"))
                        if(server.WasReviewed(conflict.OperationId))
                        {
                            var current=snapshot.FirstOrDefault(r=>r.EntityId==conflict.EntityId);
                            store.Acknowledge(conflict,new OfflineReceipt {OperationId=conflict.OperationId,EntityId=conflict.EntityId,ServerId=current?.ServerId??conflict.ServerId,Status="Reviewed",Data=current?.Data??conflict.Data,Revision=current?.Revision});
                        }
                    store.CacheOfficeConflicts(server.OpenConflicts());
                    store.MergeSnapshot(snapshot,OfflineModules.All.Where(m=>OfflineModules.HasPermission(actor,m,false)));profile.User=actor;profile.LegalAccepted=_legalConfirmedForSession || profile.LegalAccepted;profile.LastSnapshotUtc=DateTime.UtcNow;profile.LastObservedUtc=DateTime.UtcNow;store.SaveProfile(profile);
                    Announce("Last sync "+DateTime.Now.ToString("dd/MM/yyyy HH:mm")+". "+store.Operations(true).Count+" pending; "+store.CountStatus("Blocked","Conflict")+" need review.");
                },token).ConfigureAwait(false);
            }
            catch(OperationCanceledException) { }
            catch(Exception ex)
            {
                if(IsConnectivityFailure(ex)) { IsDisconnected=true;Announce("Working offline - saves stay on this PC and sync automatically when the office server returns."); }
                else
                {
                    if(ex is UnauthorizedAccessException && Store!=null) {var profile=Store.Profile();if(profile!=null){profile.User.IsActive=false;Store.SaveProfile(profile);}}
                    Announce("Sync needs attention: "+SensitiveDataRedactor.Redact(ex.Message));AppLogger.LogError("OfflineDesktop.Sync",ex);
                }
            }
            finally { Flight.Release(); }
        }
        public static bool IsConnectivityFailure(Exception ex)
        {
            if(ex is OfficeDatabaseIdentityMismatchException || ex is UnauthorizedAccessException)return false;
            var sql=ex as SqlException;
            if(sql!=null) return new[]{-2,2,20,40,53,64,121,233,258,10053,10054,10060,10061,11001}.Contains(sql.Number) || (sql.Number==0 && sql.InnerException is System.ComponentModel.Win32Exception);
            return ex?.InnerException!=null && IsConnectivityFailure(ex.InnerException);
        }
        public static void BeginOfflineStartup() { IsDisconnected=true;Announce("Working offline - saves stay on this PC and sync automatically when the office server returns."); }
        internal static bool TryQueueLegacy<T>(string module,T payload,int? serverRecordId,out OfflineQueueResult result)
        {
            result=null;if(!CanUseCurrentWorkspace() || (module!="Clients" && module!="Sites" && module!="Jobs"))return false;
            var data=JObject.FromObject(payload);string key=module=="Clients"?"ClientID":module=="Sites"?"SiteID":"JobID";
            int id=serverRecordId??((int?)data[key]??0);OfflineRecord record=id<0?Store.FindLocalReference(id):Store.Records(module).FirstOrDefault(r=>id>0&&r.ServerId==id);
            Guid publicId;if(record==null)record=new OfflineRecord {Module=module,ServerId=Math.Max(id,0),EntityId=Guid.TryParse((string)data["SyncPublicId"],out publicId)&&publicId!=Guid.Empty?publicId:Guid.NewGuid()};
            foreach(string relation in new[]{"Client","Site"})
            {
                string field=relation+"ID";int foreignId=(int?)data[field]??0;
                if(foreignId<0) {var parent=Store.FindLocalReference(foreignId);if(parent==null)throw new InvalidOperationException("The related local record is not in this office workspace.");data[field]=parent.ServerId;data[relation+"EntityId"]=parent.EntityId.ToString("D");}
            }
            record.Data=data;Save(record);result=new OfflineQueueResult {QueueId=-Store.LocalReference(record.EntityId),LocalId=Store.LocalReference(record.EntityId),Message="Saved on this PC. Pending automatic synchronization."};return true;
        }
        internal static int SaveFinancialDraft(string module,object payload)
        {
            if(!CanUseCurrentWorkspace())throw new InvalidOperationException("Prepare the offline workspace online first.");
            var data=JObject.FromObject(payload);data["InvoiceNumber"]=null;data["PaymentNumber"]=null;
            if(module=="InvoiceDrafts") {data["InvoiceID"]=0;data["PaymentStatus"]="Draft";data["PaidAmount"]=0;}
            var record=new OfflineRecord {EntityId=Guid.NewGuid(),Module=module,Data=data};Save(record);return Store.LocalReference(record.EntityId);
        }
        public static void Save(OfflineRecord record,bool reviewed=false)
        {
            if(Store==null)throw new InvalidOperationException("Prepare an offline workspace online first.");
            var profile=Store.Profile();if(profile==null || DateTime.UtcNow-profile.LastOnlineUtc>TimeSpan.FromDays(30))throw new UnauthorizedAccessException("Refresh your sign-in online before saving more offline work. Existing local work is retained.");
            if(!OfflineModules.HasPermission(profile?.User,record.Module,true))throw new UnauthorizedAccessException("Your saved permissions do not allow this change.");
            RolePermissionDto permission;profile.User.Permissions.TryGetValue(OfflineModules.PermissionKey(record.Module),out permission);
            if(record.ServerId==0 && string.IsNullOrEmpty(record.Revision)?!permission.CanCreate:!permission.CanEdit)throw new UnauthorizedAccessException("Your saved role does not allow this action.");
            if(!new LicenseService().CanPerform(OfflineModules.PermissionKey(record.Module),record.ServerId==0?"Create":"Edit"))throw new UnauthorizedAccessException("Your current license does not permit this action.");
            var op=new OfflineOperation {Module=record.Module,Data=record.Data};DesktopSyncServer.Validate(op);Store.SaveLocal(record,reviewed);Announce("Saved on this PC - pending automatic sync.");
        }
    }
}
