using System;
using System.Collections.Generic;
using System.Data.SQLite;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using Dapper;
using HVAC_Pro_Desktop.Services;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace HVAC_Pro_Desktop.OfflineDesktop
{
    internal sealed class OfflineWorkspaceStore
    {
        private readonly object _sync = new object();
        public string PathName { get; }
        public Guid OfficeId { get; }
        public int UserId { get; }
        public Guid DeviceId { get; private set; }
        public string WorkspaceKey { get; }
        private byte[] Entropy => Encoding.UTF8.GetBytes(WorkspaceKey + ":" + UserId);

        public OfflineWorkspaceStore(string directory, Guid officeId, string serverKey, int userId)
        {
            if(officeId == Guid.Empty) throw new InvalidOperationException("An initial online office enrollment is required.");
            OfficeId=officeId; UserId=userId;
            WorkspaceKey=Hash(officeId.ToString("D")+"|"+serverKey.ToLowerInvariant());
            PathName=Path.Combine(directory,"desktop-"+WorkspaceKey.Substring(0,24)+".sqlite");
            if(OneDriveStorageService.IsPathInsideOneDrive(PathName)) throw new InvalidOperationException("Offline work must be stored outside OneDrive.");
            Directory.CreateDirectory(directory);
            using(var c=Open())
            {
                // SQLite schema DDL is intentionally explicit; business operations use Dapper parameters.
                c.Execute(@"CREATE TABLE IF NOT EXISTS Profiles(UserId INTEGER PRIMARY KEY,Username TEXT NOT NULL,Payload TEXT NOT NULL);
CREATE UNIQUE INDEX IF NOT EXISTS IX_ProfileUsername ON Profiles(Username);
CREATE TABLE IF NOT EXISTS Metadata(Key TEXT PRIMARY KEY,Value TEXT NOT NULL);
CREATE TABLE IF NOT EXISTS OfficeConflicts(UserId INTEGER NOT NULL,OperationId TEXT NOT NULL,Payload TEXT NOT NULL,PRIMARY KEY(UserId,OperationId));
CREATE TABLE IF NOT EXISTS Records(UserId INTEGER NOT NULL,EntityId TEXT NOT NULL,Module TEXT NOT NULL,ServerId INTEGER NOT NULL DEFAULT 0,Payload TEXT NOT NULL,PRIMARY KEY(UserId,EntityId));
CREATE TABLE IF NOT EXISTS Operations(Sequence INTEGER PRIMARY KEY AUTOINCREMENT,UserId INTEGER NOT NULL,OperationId TEXT NOT NULL UNIQUE,EntityId TEXT NOT NULL,Module TEXT NOT NULL,Status TEXT NOT NULL,Payload TEXT NOT NULL);
CREATE INDEX IF NOT EXISTS IX_OfflineOperations ON Operations(UserId,Status,Sequence);");
                c.Execute("INSERT OR IGNORE INTO Metadata(Key,Value) VALUES('DeviceId',@Id)",new{Id=Guid.NewGuid().ToString("D")});
                DeviceId=Guid.Parse(c.QuerySingle<string>("SELECT Value FROM Metadata WHERE Key='DeviceId'"));
                // Additive local schema metadata guard for existing offline workspaces.
                if(!c.Query("PRAGMA table_info(Records)").Any(row=>(string)row.name=="ServerId")) c.Execute("ALTER TABLE Records ADD COLUMN ServerId INTEGER NOT NULL DEFAULT 0");
                c.Execute("CREATE INDEX IF NOT EXISTS IX_OfflineRecords_Server ON Records(UserId,Module,ServerId)");
            }
        }
        private SQLiteConnection Open()
        {
            var c=new SQLiteConnection(new SQLiteConnectionStringBuilder { DataSource=PathName,Version=3,ForeignKeys=true,DefaultTimeout=5 }.ToString());
            try { c.Open();c.Execute("PRAGMA synchronous=FULL; PRAGMA busy_timeout=5000;");return c; }
            catch { c.Dispose();throw; }
        }
        public static string Hash(string value) { using(var h=SHA256.Create()) return string.Concat(h.ComputeHash(Encoding.UTF8.GetBytes(value ?? "")).Select(b=>b.ToString("x2"))); }
        public static string Revision(JObject data) => Hash(Canonical(data));
        private static string Canonical(JToken value)
        {
            if(value is JObject obj) return "{"+string.Join(",",obj.Properties().OrderBy(p=>p.Name,StringComparer.Ordinal).Select(p=>JsonConvert.SerializeObject(p.Name)+":"+Canonical(p.Value)))+"}";
            if(value is JArray array) return "["+string.Join(",",array.Select(Canonical))+"]";
            if(value.Type==JTokenType.Float || value.Type==JTokenType.Integer) return Convert.ToDecimal(((JValue)value).Value,System.Globalization.CultureInfo.InvariantCulture).ToString("G29",System.Globalization.CultureInfo.InvariantCulture);
            return value.ToString(Formatting.None);
        }
        public static Guid EntityIdentity(Guid office,string module,int id)
        {
            using(var h=SHA256.Create()) return new Guid(h.ComputeHash(Encoding.UTF8.GetBytes(office+"|"+module+"|"+id)).Take(16).ToArray());
        }
        private string Protect<T>(T value) => Convert.ToBase64String(ProtectedData.Protect(Encoding.UTF8.GetBytes(JsonConvert.SerializeObject(value)),Entropy,DataProtectionScope.CurrentUser));
        private T Unprotect<T>(string value) => JsonConvert.DeserializeObject<T>(Encoding.UTF8.GetString(ProtectedData.Unprotect(Convert.FromBase64String(value),Entropy,DataProtectionScope.CurrentUser)));
        public void SaveProfile(OfflineProfile profile,bool replaceVerifier=false)
        {
            if(profile?.User?.UserId != UserId) throw new InvalidOperationException("Offline profile identity mismatch.");
            lock(_sync) using(var c=Open()) using(var t=c.BeginTransaction())
            {
                string previous=c.QuerySingleOrDefault<string>("SELECT Payload FROM Profiles WHERE UserId=@UserId",new{UserId},t);
                if(!replaceVerifier && previous!=null && Unprotect<OfflineProfile>(previous).VerifierHash!=profile.VerifierHash)throw new InvalidOperationException("The offline sign-in profile was refreshed; retry synchronization.");
                c.Execute("INSERT OR REPLACE INTO Profiles(UserId,Username,Payload) VALUES(@UserId,@Username,@Payload)",new { UserId,Username=profile.User.Username.ToLowerInvariant(),Payload=Protect(profile) },t);t.Commit();
            }
        }
        public OfflineProfile Profile()
        {
            lock(_sync) using(var c=Open())
            {
                string value=c.QuerySingleOrDefault<string>("SELECT Payload FROM Profiles WHERE UserId=@UserId",new { UserId });
                return value == null ? null : Unprotect<OfflineProfile>(value);
            }
        }
        public static int? FindUser(string directory,Guid office,string serverKey,string username)
        {
            var store=new OfflineWorkspaceStore(directory,office,serverKey,0);
            using(var c=store.Open()) return c.QuerySingleOrDefault<int?>("SELECT UserId FROM Profiles WHERE Username=@Username",new { Username=(username??"").Trim().ToLowerInvariant() });
        }
        public void RecoverInterruptedSends()
        {
            lock(_sync) using(var c=Open()) using(var t=c.BeginTransaction())
            {
                foreach(string value in c.Query<string>("SELECT Payload FROM Operations WHERE UserId=@UserId AND Status='Sending'",new {UserId},t))
                {
                    var op=Unprotect<OfflineOperation>(value);op.Status="Pending";
                    c.Execute("UPDATE Operations SET Status='Pending',Payload=@Payload WHERE OperationId=@Id AND UserId=@UserId",new {Payload=Protect(op),Id=op.OperationId.ToString("D"),UserId},t);
                }
                t.Commit();
            }
        }
        public List<OfflineRecord> Records(string module)
        {
            lock(_sync) using(var c=Open()) return c.Query<string>("SELECT Payload FROM Records WHERE UserId=@UserId AND Module=@Module",new{UserId,Module=module}).Select(Unprotect<OfflineRecord>).OrderBy(r=>r.DisplayName).ToList();
        }
        public OfflineRecord Record(Guid id)
        {
            lock(_sync) using(var c=Open()) { var value=c.QuerySingleOrDefault<string>("SELECT Payload FROM Records WHERE UserId=@UserId AND EntityId=@Id",new {UserId,Id=id.ToString("D")});return value==null?null:Unprotect<OfflineRecord>(value); }
        }
        public OfflineRecord ByServerId(string module,int serverId)
        {
            if(serverId<=0)return null;
            lock(_sync)using(var c=Open()) {var payload=c.QueryFirstOrDefault<string>("SELECT Payload FROM Records WHERE UserId=@UserId AND Module=@Module AND ServerId=@ServerId",new{UserId,Module=module,ServerId=serverId});return payload==null?null:Unprotect<OfflineRecord>(payload);}
        }
        public int CountStatus(params string[] statuses)
        {
            using(var c=Open())return c.QuerySingle<int>("SELECT COUNT(*) FROM Operations WHERE UserId=@UserId AND Status IN @Statuses",new{UserId,Statuses=statuses});
        }
        public OfflineOperation LatestIssue(Guid entityId)
        {
            using(var c=Open()) {var value=c.QueryFirstOrDefault<string>("SELECT Payload FROM Operations WHERE UserId=@UserId AND EntityId=@Id AND Status IN ('Blocked','Conflict') ORDER BY Sequence DESC LIMIT 1",new{UserId,Id=entityId.ToString("D")});return value==null?null:Unprotect<OfflineOperation>(value);}
        }
        public List<OfflineOperation> Operations(bool pendingOnly=false)
        {
            lock(_sync) using(var c=Open()) return c.Query<string>("SELECT Payload FROM Operations WHERE UserId=@UserId "+(pendingOnly?"AND Status='Pending' ":"")+"ORDER BY Sequence",new{UserId}).Select(Unprotect<OfflineOperation>).ToList();
        }
        public void SaveLocal(OfflineRecord record,bool reviewed=false)
        {
            if(!OfflineModules.CanWrite(record.Module)) throw new InvalidOperationException("This module is available offline for viewing only.");
            lock(_sync) using(var c=Open()) using(var t=c.BeginTransaction())
            {
                var previous=c.QuerySingleOrDefault<string>("SELECT Payload FROM Operations WHERE UserId=@UserId AND EntityId=@EntityId AND Status IN ('Pending','Sending','Conflict','Blocked') ORDER BY Sequence DESC LIMIT 1",new{UserId,EntityId=record.EntityId.ToString("D")},t);
                var op=previous==null?null:Unprotect<OfflineOperation>(previous);
                if(op?.Attempted==true && op.Status=="Pending") throw new InvalidOperationException("This save is waiting for its server receipt. Synchronize before editing it again.");
                if(op?.Status=="Sending") throw new InvalidOperationException("This record is synchronizing. Try saving again shortly.");
                if((op?.Status=="Conflict" || op?.Status=="Blocked") && !reviewed) throw new InvalidOperationException("Keep both copies until the office reviews this conflict.");
                if(op==null || op.Status=="Conflict" || op.Status=="Blocked")
                {
                    op=new OfflineOperation {OperationId=Guid.NewGuid(),DeviceId=DeviceId,EntityId=record.EntityId,Module=record.Module,ServerId=record.ServerId,BaseRevision=record.Revision};
                    if(reviewed)
                    {
                        var old=Unprotect<OfflineOperation>(previous);old.Status="Reviewed";
                        c.Execute("UPDATE Operations SET Status='Reviewed',Payload=@Payload WHERE UserId=@UserId AND OperationId=@Id",new{UserId,Id=old.OperationId.ToString("D"),Payload=Protect(old)},t);
                    }
                }
                op.Data=(JObject)record.Data.DeepClone();op.PayloadHash=Revision(op.Data);op.Status="Pending";op.Error=null;
                c.Execute("INSERT INTO Operations(UserId,OperationId,EntityId,Module,Status,Payload) VALUES(@UserId,@OperationId,@EntityId,@Module,@Status,@Payload) ON CONFLICT(OperationId) DO UPDATE SET Status=excluded.Status,Payload=excluded.Payload",new{UserId,OperationId=op.OperationId.ToString("D"),EntityId=record.EntityId.ToString("D"),op.Module,op.Status,Payload=Protect(op)},t);
                record.Status="Pending";Put(c,t,record);t.Commit();
            }
        }
        private void Put(SQLiteConnection c,SQLiteTransaction t,OfflineRecord record) => c.Execute("INSERT OR REPLACE INTO Records(UserId,EntityId,Module,ServerId,Payload) VALUES(@UserId,@EntityId,@Module,@ServerId,@Payload)",new{UserId,EntityId=record.EntityId.ToString("D"),record.Module,record.ServerId,Payload=Protect(record)},t);
        public OfflineOperation Claim(Guid operationId)
        {
            lock(_sync) using(var c=Open()) using(var t=c.BeginTransaction())
            {
                string value=c.QuerySingleOrDefault<string>("SELECT Payload FROM Operations WHERE UserId=@UserId AND OperationId=@Id AND Status='Pending'",new{UserId,Id=operationId.ToString("D")},t);
                if(value==null)return null;
                var op=Unprotect<OfflineOperation>(value);op.Status="Sending";op.Attempted=true;
                c.Execute("UPDATE Operations SET Status='Sending',Payload=@Payload WHERE UserId=@UserId AND OperationId=@Id",new{UserId,Id=operationId.ToString("D"),Payload=Protect(op)},t);t.Commit();return op;
            }
        }
        public void Acknowledge(OfflineOperation operation,OfflineReceipt receipt)
        {
            if(receipt.OperationId!=operation.OperationId || receipt.EntityId!=operation.EntityId) throw new InvalidOperationException("Sync receipt identity mismatch.");
            lock(_sync) using(var c=Open()) using(var t=c.BeginTransaction())
            {
                operation.Status=receipt.Status;operation.Error=receipt.Error;operation.ServerCopy=receipt.ServerCopy??operation.ServerCopy;
                c.Execute("UPDATE Operations SET Status=@Status,Payload=@Payload WHERE UserId=@UserId AND OperationId=@Id",new{UserId,Id=operation.OperationId.ToString("D"),operation.Status,Payload=Protect(operation)},t);
                var record=new OfflineRecord {EntityId=operation.EntityId,Module=operation.Module,ServerId=receipt.ServerId>0?receipt.ServerId:operation.ServerId,Revision=receipt.Revision??operation.BaseRevision,Data=receipt.Data??operation.Data,Status=receipt.Status};
                Put(c,t,record);t.Commit();
            }
        }
        public void Retry(OfflineOperation op,string error)
        {
            op.Status="Pending";op.Error=error;
            lock(_sync) using(var c=Open()) c.Execute("UPDATE Operations SET Status='Pending',Payload=@Payload WHERE UserId=@UserId AND OperationId=@Id",new{UserId,Id=op.OperationId.ToString("D"),Payload=Protect(op)});
        }
        public void MergeSnapshot(IEnumerable<OfflineRecord> records,IEnumerable<string> completeModules=null)
        {
            lock(_sync) using(var c=Open()) using(var t=c.BeginTransaction())
            {
                var downloaded=records.ToList();
                foreach(var module in completeModules??Enumerable.Empty<string>())
                {
                    var present=new HashSet<string>(downloaded.Where(r=>r.Module==module).Select(r=>r.EntityId.ToString("D")));
                    foreach(string id in c.Query<string>("SELECT EntityId FROM Records WHERE UserId=@UserId AND Module=@Module",new{UserId,Module=module},t))
                        if(!present.Contains(id))c.Execute("DELETE FROM Records WHERE UserId=@UserId AND EntityId=@Id AND NOT EXISTS(SELECT 1 FROM Operations WHERE UserId=@UserId AND EntityId=@Id AND Status IN ('Pending','Sending','Conflict','Blocked'))",new{UserId,Id=id},t);
                }
                foreach(var record in downloaded)
                {
                    int pending=c.QuerySingle<int>("SELECT COUNT(*) FROM Operations WHERE UserId=@UserId AND EntityId=@Id AND Status IN ('Pending','Sending','Conflict','Blocked')",new{UserId,Id=record.EntityId.ToString("D")},t);
                    if(pending==0) Put(c,t,record);
                }
                t.Commit();
            }
        }
        public int LocalReference(Guid entity)
        {
            using(var c=Open()) {long sequence=c.QuerySingle<long>("SELECT MIN(Sequence) FROM Operations WHERE UserId=@UserId AND EntityId=@Id",new{UserId,Id=entity.ToString("D")});if(sequence>int.MaxValue)throw new InvalidOperationException("Synchronize this PC before adding more local records.");return -(int)sequence;}
        }
        public OfflineRecord FindLocalReference(int reference)
        {
            if(reference>=0)return null;
            using(var c=Open()) {var identity=c.QuerySingleOrDefault<string>("SELECT EntityId FROM Operations WHERE UserId=@UserId AND Sequence=@Sequence",new{UserId,Sequence=-(long)reference});return identity==null?null:Record(Guid.Parse(identity));}
        }
        public void CacheOfficeConflicts(IEnumerable<OfflineOperation> conflicts)
        {
            lock(_sync) using(var c=Open()) using(var t=c.BeginTransaction())
            {
                c.Execute("DELETE FROM OfficeConflicts WHERE UserId=@UserId",new{UserId},t);
                foreach(var op in conflicts)c.Execute("INSERT INTO OfficeConflicts(UserId,OperationId,Payload) VALUES(@UserId,@Id,@Payload)",new{UserId,Id=op.OperationId.ToString("D"),Payload=Protect(op)},t);
                t.Commit();
            }
        }
        public List<OfflineOperation> OfficeConflicts()
        {
            lock(_sync)using(var c=Open())return c.Query<string>("SELECT Payload FROM OfficeConflicts WHERE UserId=@UserId",new{UserId}).Select(Unprotect<OfflineOperation>).ToList();
        }
        public void KeepServer(OfflineOperation op)
        {
            if(op.ServerCopy==null) throw new InvalidOperationException("The office must resolve a deleted record explicitly.");
            Acknowledge(op,new OfflineReceipt {OperationId=op.OperationId,EntityId=op.EntityId,ServerId=op.ServerId,Status="Reviewed",Revision=Revision(op.ServerCopy),Data=op.ServerCopy});
        }
    }
}
