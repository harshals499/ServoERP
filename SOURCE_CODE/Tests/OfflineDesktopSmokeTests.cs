using System;
using System.Collections.Generic;
using System.Data.SqlClient;
using System.Data.SQLite;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using System.Windows.Forms;
using Dapper;
using HVAC_Pro_Desktop.DAL;
using HVAC_Pro_Desktop.Models;
using HVAC_Pro_Desktop.OfflineDesktop;
using HVAC_Pro_Desktop.Services;
using HVAC_Pro_Desktop.Services.Licensing;
using HVAC_Pro_Desktop.UI;
using Newtonsoft.Json.Linq;

namespace HVAC_Pro_Desktop.Tests
{
    internal static class OfflineDesktopSmokeTests
    {
        private static void Check(bool condition,string message) { if(!condition)throw new InvalidOperationException(message); }
        private static void Refused(Action action,string message) { bool refused=false;try{action();}catch{refused=true;}Check(refused,message); }
        internal static string RunAll(string connectionString,string directory,string evidence)
        {
            var builder=new SqlConnectionStringBuilder(connectionString);
            Check(builder.InitialCatalog.StartsWith("ServoERP_StartupGateQA_",StringComparison.Ordinal)||builder.InitialCatalog.StartsWith("ServoERP_OfflineQA_",StringComparison.Ordinal),"Only an explicitly identified disposable QA database is permitted.");
            Directory.CreateDirectory(directory);Directory.CreateDirectory(evidence);
            string oldDiagnostic=DatabaseConnectionFactory.DiagnosticConnectionString;var oldUser=SessionManager.CurrentUser;
            var licenseField=typeof(LicenseService).GetField("_lastValidation",BindingFlags.Static|BindingFlags.NonPublic);var oldLicense=licenseField.GetValue(null);
            var validLicense=new LicenseValidationResult {Success=true,IsFrozen=false,Snapshot=new LicenseSnapshot {PlanType=LicensePlanType.Enterprise,Status=LicenseStatus.Active,EnabledModules=LicenseFeatureCatalog.GetModulesForPlan(LicensePlanType.Enterprise),ExpiryDateUtc=DateTime.UtcNow.AddYears(1)}};
            licenseField.SetValue(null,validLicense);DatabaseConnectionFactory.DiagnosticConnectionString=connectionString;
            OfflineDesktopCoordinator.TestDirectory=directory;OfflineDesktopCoordinator.TestConnectionString=connectionString;
            OfflineDesktopCoordinator.TestServerKey=builder.DataSource+"|"+builder.InitialCatalog;
            var checks=new List<string>();
            try
            {
                Guid office;AppUserDto actor;string hash;const string password="Offline-QA-Test-516!";
                using(var c=new SqlConnection(connectionString))
                {
                    c.Open();office=c.QuerySingle<Guid>("SELECT OfficeDatabaseId FROM dbo.OfficeDatabaseIdentity WHERE IdentityKey=1");
                    // Additive schema DDL is deliberately executed only in the guarded disposable database.
                    using(var command=new SqlCommand(DatabaseUpgradeService.ReadScripts()["0005_OfflineDesktop"],c)){command.CommandTimeout=120;command.ExecuteNonQuery();}
                    string suffix=Guid.NewGuid().ToString("N").Substring(0,8);
                    int role=c.QuerySingle<int>("INSERT dbo.AppRoles(RoleName,Description) VALUES(@Name,'Disposable offline QA role');SELECT CAST(SCOPE_IDENTITY() AS int)",new{Name="OfflineQA-"+suffix});
                    foreach(string module in new[]{"Clients","WorkOrders","Invoices","Payments","Settings"}) c.Execute("INSERT dbo.RolePermissions(RoleId,ModuleKey,CanView,CanCreate,CanEdit,CanDelete) VALUES(@Role,@Module,1,1,1,0)",new{Role=role,Module=module});
                    hash=SecurityHelpers.HashPasswordWithBcrypt(password);
                    int user=c.QuerySingle<int>("INSERT dbo.AppUsers(Username,DisplayName,PasswordHash,PasswordSalt,RoleId,IsActive,ForcePasswordChange) VALUES(@Username,'Offline QA',@Hash,'bcrypt',@Role,1,0);SELECT CAST(SCOPE_IDENTITY() AS int)",new{Username="offlineqa-"+suffix,Hash=hash,Role=role});
                    actor=new AppUserDto {UserId=user,Username="offlineqa-"+suffix,DisplayName="Offline QA",RoleId=role,RoleName="Offline QA",IsActive=true,Permissions=c.Query<RolePermissionDto>("SELECT * FROM dbo.RolePermissions WHERE RoleId=@Role",new{Role=role}).ToDictionary(p=>p.ModuleKey,StringComparer.OrdinalIgnoreCase)};
                }
                OfflineDesktopCoordinator.TestOfficeId=office;SessionManager.SetSession(actor);
                var store=new OfflineWorkspaceStore(directory,office,OfflineDesktopCoordinator.ServerKey,actor.UserId);
                var profile=new OfflineProfile {User=actor,VerifierHash=hash,CredentialMarker=OfflineWorkspaceStore.Hash(hash+"|bcrypt"),LegalAccepted=true,LastOnlineUtc=DateTime.UtcNow,LastObservedUtc=DateTime.UtcNow,LastSnapshotUtc=DateTime.UtcNow};store.SaveProfile(profile);
                var server=new DesktopSyncServer(connectionString,store,profile,(module,action)=>true);
                SessionManager.ClearSession();
                Refused(()=>OfflineSyncService.Queue("Clients","Create",new B2BClient {CompanyName="Unscoped attempt"},null,false,"QA"),"An unprepared account wrote to the unscoped legacy queue.");
                Check(OfflineSyncService.TryReplayPending()==0,"Unscoped legacy work replayed automatically.");SessionManager.SetSession(actor);
                Check(new AuthService().Login(actor.Username,password,false).Success,"Normal online authentication did not enroll the offline identity.");
                Check(store.Profile().VerifierHash!=hash,"The normal online login did not seed a protected offline verifier.");

                Check(OfflineWorkspaceStore.Revision(JObject.Parse("{\"amount\":1.00,\"nested\":{\"b\":2,\"a\":1}}"))==OfflineWorkspaceStore.Revision(JObject.Parse("{\"nested\":{\"a\":1.0,\"b\":2.0},\"amount\":1}")),"Canonical fingerprints changed after JSON round-trip.");
                for(int i=0;i<5;i++)Refused(()=>OfflineDesktopCoordinator.OfflineLogin(actor.Username,"wrong-password"),"Wrong password accepted.");
                Refused(()=>OfflineDesktopCoordinator.OfflineLogin(actor.Username,password),"Offline lockout was bypassed.");profile=store.Profile();profile.FailedAttempts=0;profile.LockoutUntilUtc=null;store.SaveProfile(profile);
                OfflineDesktopCoordinator.OfflineLogin(actor.Username,password);OfflineDesktopCoordinator.Stop();
                Check(new OfflineWorkspaceStore(directory,office,OfflineDesktopCoordinator.ServerKey,actor.UserId+100000).Profile()==null,"Cross-account cache was readable.");
                Check(new OfflineWorkspaceStore(directory,Guid.NewGuid(),OfflineDesktopCoordinator.ServerKey,actor.UserId).Profile()==null,"Cross-office cache was readable.");
                profile=store.Profile();profile.LastOnlineUtc=DateTime.UtcNow.AddDays(-31);store.SaveProfile(profile);Refused(()=>OfflineDesktopCoordinator.OfflineLogin(actor.Username,password),"Expired cached identity accepted.");profile.LastOnlineUtc=DateTime.UtcNow;store.SaveProfile(profile);OfflineDesktopCoordinator.OfflineLogin(actor.Username,password);
                checks.Add("PASS cached authentication, five-attempt lockout, expiry, office/account isolation and canonical fingerprints");
                var client=new OfflineRecord {EntityId=Guid.NewGuid(),Module="Clients",Data=new JObject { ["CompanyName"]="Offline QA client "+Guid.NewGuid().ToString("N").Substring(0,8),["Phone"]="9000000000",["Email"]="offlineqa@example.invalid",["PrimaryContact"]="QA contact",["City"]="Pune",["Notes"]="offline encryption marker"}};
                using(var c=new SQLiteConnection(new SQLiteConnectionStringBuilder {DataSource=store.PathName,Version=3}.ToString()))
                {
                    c.Open();c.Execute("CREATE TRIGGER QA_RejectRecord BEFORE INSERT ON Records WHEN NEW.Module='Clients' BEGIN SELECT RAISE(ABORT,'QA injected disk failure'); END");
                    Refused(()=>store.SaveLocal(client),"Injected local transaction failure was accepted.");Check(store.Record(client.EntityId)==null&&store.Operations().Count==0,"Local record/outbox committed separately.");c.Execute("DROP TRIGGER QA_RejectRecord");
                }
                store.SaveLocal(client);var queued=store.Operations(true).Single();var submitted=store.Claim(queued.OperationId);
                Refused(()=>store.SaveLocal(client),"An in-flight operation was mutated.");
                var receipt=server.Apply(submitted);int clientId=receipt.ServerId;
                var restarted=new OfflineWorkspaceStore(directory,office,OfflineDesktopCoordinator.ServerKey,actor.UserId);restarted.RecoverInterruptedSends();var retry=restarted.Claim(queued.OperationId);Check(retry.OperationId==submitted.OperationId,"Restart generated a new operation ID.");
                Refused(()=>restarted.SaveLocal(client),"An attempted operation was mutated before receipt recovery.");
                var replay=server.Apply(retry);restarted.Acknowledge(retry,replay);
                using(var c=new SqlConnection(connectionString)){c.Open();Check(c.QuerySingle<int>("SELECT COUNT(*) FROM dbo.B2BClients WHERE SyncPublicId=@Id",new{Id=client.EntityId})==1,"Lost response duplicated a client.");Check(c.QuerySingle<int>("SELECT COUNT(*) FROM dbo.AuditLog WHERE Action='OFFLINE_SYNC' AND Description LIKE @Operation",new{Operation="%"+queued.OperationId+"%"})==1,"Retry duplicated the audit record.");}
                var tampered=new OfflineOperation {OperationId=retry.OperationId,DeviceId=retry.DeviceId,EntityId=Guid.NewGuid(),Module=retry.Module,Data=retry.Data,PayloadHash=retry.PayloadHash};Refused(()=>server.Apply(tampered),"Operation identity reuse was accepted.");
                Check(!System.Text.Encoding.UTF8.GetString(File.ReadAllBytes(store.PathName)).Contains("offline encryption marker"),"Business payload appeared in plaintext SQLite.");
                checks.Add("PASS atomic encrypted local save/outbox, restart recovery, stable attempted operations, exactly-once receipt/audit replay and identity tampering refusal");
                var first=restarted.Record(client.EntityId);var secondStore=new OfflineWorkspaceStore(Path.Combine(directory,"second-pc"),office,OfflineDesktopCoordinator.ServerKey,actor.UserId);secondStore.SaveProfile(profile);secondStore.MergeSnapshot(new[]{first});
                first.Data["Phone"]="9111111111";restarted.SaveLocal(first);var a=restarted.Claim(restarted.Operations(true).Single().OperationId);restarted.Acknowledge(a,server.Apply(a));
                var second=secondStore.Record(client.EntityId);second.Data["CompanyName"]="Second PC proposal";secondStore.SaveLocal(second);var b=secondStore.Claim(secondStore.Operations(true).Single().OperationId);var serverB=new DesktopSyncServer(connectionString,secondStore,profile,(m,action)=>true);var conflict=serverB.Apply(b);secondStore.Acknowledge(b,conflict);
                Check(conflict.Status=="Conflict" && (string)conflict.ServerCopy["Phone"]=="9111111111" && (string)conflict.Data["CompanyName"]=="Second PC proposal","Concurrent edits lost one side.");
                Check(serverB.OpenConflicts().Any(o=>o.OperationId==b.OperationId),"Conflict was not retained at the office.");
                serverB.KeepOfficeCopy(b.OperationId);secondStore.KeepServer(b);Check(!serverB.OpenConflicts().Any(o=>o.OperationId==b.OperationId),"Office conflict resolution did not persist.");
                restarted.MergeSnapshot(new OfflineRecord[0],new[]{"Clients"});Check(restarted.Record(client.EntityId)==null,"Authoritative deletion was not reflected locally.");
                secondStore.SaveLocal(new OfflineRecord {EntityId=Guid.NewGuid(),Module="Clients",Data=new JObject {["CompanyName"]="Pending preserved"}});var pendingId=secondStore.Operations(true).Single().EntityId;secondStore.MergeSnapshot(new OfflineRecord[0],new[]{"Clients"});Check(secondStore.Record(pendingId)!=null,"Refresh discarded pending local work.");
                checks.Add("PASS two-PC concurrency, both-copy office review, resolved history, snapshot tombstones and preservation of pending local work");
                AppUserDto freshActor;var currentClient=server.Snapshot(out freshActor).Single(record=>record.EntityId==client.EntityId);
                var reviewedOperation=new OfflineOperation {OperationId=Guid.NewGuid(),DeviceId=secondStore.DeviceId,EntityId=client.EntityId,Module="Clients",ServerId=clientId,BaseRevision=currentClient.Revision,Data=(JObject)currentClient.Data.DeepClone()};reviewedOperation.Data["PrimaryContact"]="Office-reviewed contact";reviewedOperation.PayloadHash=OfflineWorkspaceStore.Revision(reviewedOperation.Data);
                using(var c=new SqlConnection(connectionString)){c.Open();c.Execute("UPDATE dbo.B2BClients SET City='Office concurrent edit' WHERE ClientID=@Id",new{Id=clientId});}
                var needsReview=serverB.Apply(reviewedOperation);Check(needsReview.Status=="Conflict","A changed office row was overwritten before review.");
                var retained=serverB.OpenConflicts().Single(operation=>operation.OperationId==reviewedOperation.OperationId);
                var applied=serverB.ApplyReviewed(retained,retained.Data);var appliedAgain=serverB.ApplyReviewed(retained,retained.Data);
                Check(applied.Status=="Synced" && appliedAgain.Status=="Synced" && !serverB.OpenConflicts().Any(operation=>operation.OperationId==retained.OperationId),"Reviewed proposal was not idempotent or resolved atomically.");
                var rollbackOperation=new OfflineOperation {OperationId=Guid.NewGuid(),DeviceId=store.DeviceId,EntityId=Guid.NewGuid(),Module="Clients",Data=new JObject {["CompanyName"]="QA rollback marker"}};rollbackOperation.PayloadHash=OfflineWorkspaceStore.Revision(rollbackOperation.Data);
                string trigger="QA_RejectOfflineReceipt_"+rollbackOperation.OperationId.ToString("N").Substring(0,8);
                using(var c=new SqlConnection(connectionString))
                {
                    c.Open();
                    // Trusted test-fixture DDL only; identifiers and literal GUID are generated internally.
                    c.Execute("CREATE TRIGGER dbo.["+trigger+"] ON dbo.DesktopOfflineReceipts AFTER INSERT AS BEGIN IF EXISTS(SELECT 1 FROM inserted WHERE OperationId='"+rollbackOperation.OperationId+"') THROW 51091,'QA injected receipt failure',1; END;");
                    try {Refused(()=>server.Apply(rollbackOperation),"Injected receipt failure was swallowed.");Check(c.QuerySingle<int>("SELECT COUNT(*) FROM dbo.B2BClients WHERE SyncPublicId=@Id",new{Id=rollbackOperation.EntityId})==0,"Business mutation escaped receipt rollback.");Check(c.QuerySingle<int>("SELECT COUNT(*) FROM dbo.DesktopOfflineReceipts WHERE OperationId=@Id",new{Id=rollbackOperation.OperationId})==0,"Failed transaction left a receipt.");}
                    finally {c.Execute("DROP TRIGGER dbo.["+trigger+"]");}
                }
                checks.Add("PASS revised office proposal retries are idempotent; an injected receipt failure rolls back its business mutation and receipt together");
                var site=new OfflineRecord {EntityId=Guid.NewGuid(),Module="Sites",Data=new JObject {["ClientID"]=0,["ClientEntityId"]=client.EntityId.ToString("D"),["SiteName"]="Offline QA site",["Address"]="QA address",["City"]="Pune"}};store.SaveLocal(site);var siteOp=store.Claim(store.Operations(true).Single().OperationId);var siteReceipt=server.Apply(siteOp);store.Acknowledge(siteOp,siteReceipt);
                var job=new OfflineRecord {EntityId=Guid.NewGuid(),Module="Jobs",Data=new JObject {["ClientID"]=0,["ClientEntityId"]=client.EntityId.ToString("D"),["SiteID"]=0,["SiteEntityId"]=site.EntityId.ToString("D"),["Title"]="Offline QA service",["JobTitle"]="Offline QA service",["ScheduledDate"]=DateTime.Today.AddDays(1),["Priority"]="Medium",["Notes"]="Captured offline"}};store.SaveLocal(job);var jobOp=store.Claim(store.Operations(true).Single().OperationId);var jobReceipt=server.Apply(jobOp);store.Acknowledge(jobOp,jobReceipt);Check(jobReceipt.ServerId>0,"Dependent offline job did not synchronize.");
                checks.Add("PASS dependent client/site/job reconciliation using the standard job validation, numbering and transaction-bound checklist/activity primitives");
                var invoiceDraft=new OfflineRecord {EntityId=Guid.NewGuid(),Module="InvoiceDrafts",Data=JObject.FromObject(new Invoice {ClientID=clientId,InvoiceDate=DateTime.Today,DueDate=DateTime.Today.AddDays(30),PaymentStatus="Draft",Notes="Offline QA draft",LineItems=new List<InvoiceLineItem>{new InvoiceLineItem {Description="QA service labour",Quantity=1,Rate=100,GSTPercent=18,IsBillable=true,Category="Service"}}})};
                int invoiceCount;using(var c=new SqlConnection(connectionString)){c.Open();invoiceCount=c.QuerySingle<int>("SELECT COUNT(*) FROM dbo.Invoices");}
                store.SaveLocal(invoiceDraft);var draftOp=store.Claim(store.Operations(true).Single().OperationId);var draftReceipt=server.Apply(draftOp);store.Acknowledge(draftOp,draftReceipt);
                using(var c=new SqlConnection(connectionString)){c.Open();Check(c.QuerySingle<int>("SELECT COUNT(*) FROM dbo.Invoices")==invoiceCount,"Draft upload posted an invoice.");}
                Refused(()=>new DesktopSyncServer("Server=127.0.0.1,1;Database=master;Integrated Security=True;Connect Timeout=1",store,profile,(m,action)=>true).FinalizeDraft(invoiceDraft.EntityId,draftReceipt.Revision),"Financial finalization succeeded without a server.");
                int invoiceId=server.FinalizeDraft(invoiceDraft.EntityId,draftReceipt.Revision);Check(invoiceId>0,"Online invoice finalization failed.");
                using(var c=new SqlConnection(connectionString)){c.Open();c.Execute("UPDATE dbo.DesktopOfflineDrafts SET Status='Draft',ResultId=NULL WHERE EntityId=@Id",new{Id=invoiceDraft.EntityId});}
                Check(server.FinalizeDraft(invoiceDraft.EntityId,draftReceipt.Revision)==invoiceId,"Lost finalization response duplicated the invoice.");
                var paymentDraft=new OfflineRecord {EntityId=Guid.NewGuid(),Module="PaymentDrafts",Data=JObject.FromObject(new Payment {ClientID=clientId,InvoiceID=invoiceId,AmountPaid=25,PaymentDate=DateTime.Today,PaymentMode="UPI",ReferenceNumber="QA-"+Guid.NewGuid().ToString("N")})};
                store.SaveLocal(paymentDraft);var paymentOp=store.Claim(store.Operations(true).Single().OperationId);var paymentReceipt=server.Apply(paymentOp);store.Acknowledge(paymentOp,paymentReceipt);
                using(var c=new SqlConnection(connectionString)){c.Open();Check(c.QuerySingle<int>("SELECT COUNT(*) FROM dbo.Payments WHERE OfflineDraftId=@Id",new{Id=paymentDraft.EntityId})==0,"Draft upload posted a payment.");}
                int paymentId=server.FinalizeDraft(paymentDraft.EntityId,paymentReceipt.Revision);
                using(var c=new SqlConnection(connectionString)){c.Open();c.Execute("UPDATE dbo.DesktopOfflineDrafts SET Status='Draft',ResultId=NULL WHERE EntityId=@Id",new{Id=paymentDraft.EntityId});}
                Check(server.FinalizeDraft(paymentDraft.EntityId,paymentReceipt.Revision)==paymentId,"Lost finalization response duplicated payment.");
                using(var c=new SqlConnection(connectionString)){c.Open();Check(c.QuerySingle<int>("SELECT COUNT(*) FROM dbo.Payments WHERE OfflineDraftId=@Id",new{Id=paymentDraft.EntityId})==1,"Duplicate financial row exists.");}
                checks.Add("PASS offline financial drafts do not post; finalization requires a reachable office server; lost invoice/payment finalization acknowledgements reconcile without duplicates");
                var noRights=new DesktopSyncServer(connectionString,store,profile,(m,action)=>false);Refused(()=>noRights.Apply(new OfflineOperation {OperationId=Guid.NewGuid(),DeviceId=store.DeviceId,EntityId=Guid.NewGuid(),Module="Clients",Data=new JObject {["CompanyName"]="Denied"},PayloadHash=OfflineWorkspaceStore.Revision(new JObject {["CompanyName"]="Denied"})}),"License denial was bypassed.");
                var revokedProfile=store.Profile();using(var c=new SqlConnection(connectionString)){c.Open();c.Execute("UPDATE dbo.AppUsers SET IsActive=0 WHERE UserId=@Id",new{Id=actor.UserId});}Refused(()=>server.Snapshot(out var ignored),"Revoked identity synchronized.");Check(!new AuthService().Login(actor.Username,password,false).Success,"An inactive account signed in online.");Refused(()=>OfflineDesktopCoordinator.OfflineLogin(actor.Username,password),"Known online revocation did not invalidate offline access.");using(var c=new SqlConnection(connectionString)){c.Open();c.Execute("UPDATE dbo.AppUsers SET IsActive=1 WHERE UserId=@Id",new{Id=actor.UserId});}Check(new AuthService().Login(actor.Username,password,false).Success,"Reactivated identity could not re-enroll online.");
                checks.Add("PASS current server account revocation and license permissions are rechecked before synchronization");
                // Exercise the same coordinator used by the normal and offline shells with a real server.
                OfflineDesktopCoordinator.OfflineLogin(actor.Username,password);OfflineDesktopCoordinator.SyncOnceAsync().GetAwaiter().GetResult();
                Check(!OfflineDesktopCoordinator.IsDisconnected,"Coordinator did not reconnect to SQL.");Check(OfflineDesktopCoordinator.Store.Profile().LastSnapshotUtc.HasValue,"Coordinator did not persist its download checkpoint.");
                checks.Add("PASS reconnect worker pipeline uploads/refreshes and retains its durable synchronization checkpoint");
                // Native forms are rendered from the built EXE without launching Program.Main or touching other running applications.
                Application.EnableVisualStyles();
                using(var window=new OfflineWorkspaceForm())
                {
                    window.Show();Application.DoEvents();window.Size=new Size(1120,760);Application.DoEvents();Capture(window,Path.Combine(evidence,"workspace-normal.png"));window.Size=new Size(850,560);Application.DoEvents();Capture(window,Path.Combine(evidence,"workspace-compact.png"));window.Close();
                }
                OfflineDesktopCoordinator.BeginOfflineStartup();
                using(var editor=new OfflineRecordEditor(invoiceDraft)){editor.Show();Application.DoEvents();Capture(editor,Path.Combine(evidence,"invoice-draft-editor.png"));editor.Close();}
                checks.Add("PASS native offline workspace normal/compact and financial draft editor screenshots captured for visual review");
                // Construct/render the normal shell without OnShown, which would start unrelated
                // backup/update/onboarding workflows. All repository access remains on the QA DB.
                OfflineDesktopCoordinator.SyncOnceAsync().GetAwaiter().GetResult();
                using(var main=new OfflineShellPreview())
                {
                    main.WindowState=FormWindowState.Normal;main.Size=new Size(1120,760);main.Show();Application.DoEvents();
                    OfflineDesktopCoordinator.BeginOfflineStartup();
                    typeof(MainForm).GetMethod("RefreshDatabaseStatusBanner",BindingFlags.Instance|BindingFlags.NonPublic).Invoke(main,new object[]{new DatabaseConnectionStateSnapshot {State=DatabaseConnectionStateKind.Offline,BusinessWritesAllowed=false}});
                    main.Refresh();Application.DoEvents();Capture(main,Path.Combine(evidence,"main-shell-offline-banner.png"));
                }

                // Open through the real offline startup branch, sign in in this test process only,
                // then restore connectivity and observe the background worker without a manual sync.
                var autoDraft=new OfflineRecord {EntityId=Guid.NewGuid(),Module="InvoiceDrafts",Data=(JObject)invoiceDraft.Data.DeepClone()};
                OfflineDesktopCoordinator.Store.SaveLocal(autoDraft);
                OfflineDesktopCoordinator.TestConnectionString="Server=127.0.0.1,1;Database=master;Integrated Security=True;Connect Timeout=1";
                DateTime reconnectAfter=DateTime.MaxValue;bool signed=false,sawOffline=false,sawAutomaticSync=false,timedOut=false;var deadline=DateTime.UtcNow.AddSeconds(55);
                using(var timer=new System.Windows.Forms.Timer {Interval=200})
                {
                    timer.Tick+=(sender,eventArgs)=>
                    {
                        foreach(Form form in Application.OpenForms.Cast<Form>().ToArray())
                        {
                            if(DateTime.UtcNow>deadline) {timedOut=true;form.Close();continue;}
                            if(form is OfflineSignInForm && !signed)
                            {
                                Capture(form,Path.Combine(evidence,"offline-sign-in.png"));
                                var controls=Descendants(form).ToList();controls.OfType<TextBox>().Single(box=>!box.UseSystemPasswordChar).Text=actor.Username;controls.OfType<TextBox>().Single(box=>box.UseSystemPasswordChar).Text=password;
                                signed=true;controls.OfType<Button>().Single(button=>button.Text=="Sign in offline").PerformClick();
                            }
                            if(form is OfflineWorkspaceForm)
                            {
                                if(!sawOffline)
                                {
                                    sawOffline=true;Capture(form,Path.Combine(evidence,"offline-startup-workspace.png"));
                                    reconnectAfter=DateTime.UtcNow;OfflineDesktopCoordinator.TestConnectionString=connectionString;
                                }
                                var current=OfflineDesktopCoordinator.Store.Record(autoDraft.EntityId);
                                if(!sawAutomaticSync && current?.Status=="Draft" && !OfflineDesktopCoordinator.IsDisconnected && OfflineDesktopCoordinator.Store.Profile().LastSnapshotUtc>=reconnectAfter && OfflineDesktopCoordinator.Status.StartsWith("Last sync"))
                                {
                                    sawAutomaticSync=true;timer.Stop();Application.DoEvents();Capture(form,Path.Combine(evidence,"automatic-reconnect.png"));form.Close();
                                }
                            }
                        }
                    };
                    timer.Start();bool handled=OfflineDesktopStartup.TryRunBeforeSqlStartup(()=>validLicense);timer.Stop();
                    Check(handled&&signed&&sawOffline&&!timedOut&&sawAutomaticSync,"Offline startup or automatic reconnect did not complete without a manual sync.");
                }
                checks.Add("PASS real offline startup/sign-in without SQL initialization; returning connectivity triggers background synchronization without a manual command");
                File.WriteAllLines(Path.Combine(evidence,"offline-smoke-results.txt"),checks);return string.Join(Environment.NewLine,checks);
            }
            finally
            {
                OfflineDesktopCoordinator.Stop();DatabaseConnectionFactory.DiagnosticConnectionString=oldDiagnostic;SessionManager.SetSession(oldUser);licenseField.SetValue(null,oldLicense);
                OfflineDesktopCoordinator.TestDirectory=null;OfflineDesktopCoordinator.TestConnectionString=null;OfflineDesktopCoordinator.TestServerKey=null;OfflineDesktopCoordinator.TestOfficeId=null;
            }
        }
        internal static string PreviewMain(string connectionString,string directory,string evidence)
        {
            var builder=new SqlConnectionStringBuilder(connectionString);Check(builder.InitialCatalog.StartsWith("ServoERP_StartupGateQA_"),"Disposable QA database required.");
            DatabaseConnectionFactory.DiagnosticConnectionString=connectionString;
            Guid office;using(var c=new SqlConnection(connectionString)){c.Open();office=c.QuerySingle<Guid>("SELECT OfficeDatabaseId FROM dbo.OfficeDatabaseIdentity WHERE IdentityKey=1");}
            string key=builder.DataSource+"|"+builder.InitialCatalog;var lookup=new OfflineWorkspaceStore(directory,office,key,0);int user;
            using(var c=new SQLiteConnection(new SQLiteConnectionStringBuilder{DataSource=lookup.PathName,Version=3}.ToString())){c.Open();user=c.QuerySingle<int>("SELECT UserId FROM Profiles");}
            var store=new OfflineWorkspaceStore(directory,office,key,user);var profile=store.Profile();SessionManager.SetSession(profile.User);
            typeof(LicenseService).GetField("_lastValidation",BindingFlags.Static|BindingFlags.NonPublic).SetValue(null,new LicenseValidationResult {Success=true,Snapshot=new LicenseSnapshot {PlanType=LicensePlanType.Enterprise,Status=LicenseStatus.Active,EnabledModules=LicenseFeatureCatalog.GetModulesForPlan(LicensePlanType.Enterprise),ExpiryDateUtc=DateTime.UtcNow.AddYears(1)}});
            OfflineDesktopCoordinator.TestDirectory=directory;OfflineDesktopCoordinator.TestConnectionString=connectionString;OfflineDesktopCoordinator.TestServerKey=key;OfflineDesktopCoordinator.TestOfficeId=office;
            Application.EnableVisualStyles();
            using(var main=new OfflineShellPreview())
            {
                main.WindowState=FormWindowState.Normal;main.Size=new Size(1120,760);main.Show();Application.DoEvents();OfflineDesktopCoordinator.BeginOfflineStartup();Application.DoEvents();
                typeof(MainForm).GetMethod("RefreshDatabaseStatusBanner",BindingFlags.Instance|BindingFlags.NonPublic).Invoke(main,new object[]{new DatabaseConnectionStateSnapshot {State=DatabaseConnectionStateKind.Offline,BusinessWritesAllowed=false}});
                main.PerformLayout();main.Refresh();Application.DoEvents();
                var banner=(Control)typeof(MainForm).GetField("_pnlDatabaseStatusBanner",BindingFlags.Instance|BindingFlags.NonPublic).GetValue(main);var lines=new List<string>();
                for(var node=banner;node!=null;node=node.Parent)lines.Add(node.GetType().Name+" "+node.Name+" visible="+node.Visible+" bounds="+node.Bounds+" dock="+node.Dock+" children="+node.Controls.Count);
                foreach(Control child in banner.Controls)lines.Add("CHILD "+child.GetType().Name+" "+child.Text+" visible="+child.Visible+" bounds="+child.Bounds+" dock="+child.Dock);
                File.WriteAllLines(Path.Combine(evidence,"main-banner-layout.txt"),lines);Capture(main,Path.Combine(evidence,"main-shell-offline-banner.png"));
                var entry=banner.Controls.OfType<Button>().Single(button=>button.Text=="Offline workspace");
                var label=banner.Controls.OfType<Label>().Single();
                var content=(Control)typeof(MainForm).GetField("_content",BindingFlags.Instance|BindingFlags.NonPublic).GetValue(main);
                Check(entry.Visible && entry.Bottom<=banner.ClientSize.Height && label.Right<=entry.Left && content.Top>=banner.Bottom,"Offline entry/status strip is clipped or covered by the page host.");
                bool opened=false;
                using(var closeChild=new System.Windows.Forms.Timer {Interval=150})
                {
                    closeChild.Tick+=(sender,args)=> {var child=Application.OpenForms.Cast<Form>().OfType<OfflineWorkspaceForm>().FirstOrDefault();if(child!=null){opened=true;closeChild.Stop();Capture(child,Path.Combine(evidence,"main-entry-opened-workspace.png"));child.Close();}};
                    closeChild.Start();entry.PerformClick();closeChild.Stop();
                }
                Check(opened,"The normal shell offline entry did not open the scoped workspace.");
                var draft=store.Records("InvoiceDrafts").FirstOrDefault();
                if(draft!=null)Check(!OfflinePresentation.Describe(draft,store).Contains("Subtotal:"),"A provisional invoice showed a misleading authoritative subtotal.");
                return string.Join(Environment.NewLine,lines)+Environment.NewLine+"PASS status strip geometry and normal-shell offline-workspace entry.";
            }
        }
        private sealed class OfflineShellPreview : MainForm
        {
            // Render the real shell without starting its unrelated backup/updater/onboarding Shown handlers.
            protected override void OnShown(EventArgs e) { }
        }
        private static IEnumerable<Control> Descendants(Control root)
        {
            foreach(Control child in root.Controls) {yield return child;foreach(var nested in Descendants(child))yield return nested;}
        }
        private static void Capture(Form form,string path) {using(var bitmap=new Bitmap(form.Width,form.Height)){form.DrawToBitmap(bitmap,new Rectangle(Point.Empty,form.Size));bitmap.Save(path,System.Drawing.Imaging.ImageFormat.Png);}}
    }
}
