using System;
using System.Collections.Generic;
using System.Linq;
using Dapper;
using HVAC_Pro_Desktop.Models;
using HVAC_Pro_Desktop.Services;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace HVAC_Pro_Desktop.OfflineDesktop
{
    internal sealed partial class DesktopSyncServer
    {
        public List<OfflineOperation> OpenConflicts()
        {
            using(var c=Open())
            {
                var actor=Authenticate(c);var result=new List<OfflineOperation>();
                foreach(var row in c.Query("SELECT OperationId,EntityId,Module,ServerId,BaseRevision,LocalJson,ServerJson,Reason FROM dbo.DesktopOfflineConflicts WHERE OfficeId=@OfficeId AND ResolvedUtc IS NULL ORDER BY CreatedUtc",new{_store.OfficeId}))
                {
                    string module=row.Module;RolePermissionDto permission;
                    if(!actor.Permissions.TryGetValue(OfflineModules.PermissionKey(module),out permission) || !permission.CanView || !permission.CanEdit || !_license(OfflineModules.PermissionKey(module),"View"))continue;
                    result.Add(new OfflineOperation {OperationId=row.OperationId,EntityId=row.EntityId,Module=module,ServerId=row.ServerId,BaseRevision=row.BaseRevision,Data=JObject.Parse((string)row.LocalJson),ServerCopy=row.ServerJson==null?null:JObject.Parse((string)row.ServerJson),Status="Conflict",Error=row.Reason});
                }
                return result;
            }
        }
        public void KeepOfficeCopy(Guid operationId)
        {
            using(var c=Open()) using(var t=c.BeginTransaction())
            {
                var actor=Authenticate(c,t);var conflict=c.QuerySingleOrDefault("SELECT Module FROM dbo.DesktopOfflineConflicts WITH(UPDLOCK,HOLDLOCK) WHERE OperationId=@OperationId AND OfficeId=@OfficeId AND ResolvedUtc IS NULL",new{OperationId=operationId,_store.OfficeId},t);
                if(conflict==null)throw new InvalidOperationException("This conflict has already been reviewed.");
                Demand(actor,(string)conflict.Module);
                c.Execute("UPDATE dbo.DesktopOfflineConflicts SET ResolvedUtc=SYSUTCDATETIME(),ResolvedByUserId=@UserId,Resolution='Kept office copy' WHERE OperationId=@OperationId AND OfficeId=@OfficeId",new{OperationId=operationId,_store.OfficeId,_store.UserId},t);t.Commit();
            }
        }
        public OfflineReceipt ApplyReviewed(OfflineOperation original,JObject revised)
        {
            using(var c=Open())
            {
                var actor=Authenticate(c);Demand(actor,original.Module);
                var stored=c.QuerySingleOrDefault("SELECT Module,EntityId,ServerId FROM dbo.DesktopOfflineConflicts WHERE OperationId=@Id AND OfficeId=@OfficeId",new{Id=original.OperationId,_store.OfficeId});
                if(stored==null || (Guid)stored.EntityId!=original.EntityId || (string)stored.Module!=original.Module)throw new InvalidOperationException("This conflict is no longer available for review.");
            }
            // The reviewer explicitly submits a new operation against the displayed office copy.
            var operation=new OfflineOperation {OperationId=OfflineWorkspaceStore.EntityIdentity(original.OperationId,"review-"+OfflineWorkspaceStore.Revision(revised),_store.UserId),DeviceId=_store.DeviceId,ReviewOfOperationId=original.OperationId,EntityId=original.EntityId,Module=original.Module,ServerId=original.ServerId,BaseRevision=original.ServerCopy==null?original.BaseRevision:OfflineWorkspaceStore.Revision(original.ServerCopy),Data=revised,PayloadHash=OfflineWorkspaceStore.Revision(revised)};
            var result=Apply(operation);
            return result;
        }
        public bool WasReviewed(Guid operationId)
        {
            using(var c=Open()) { Authenticate(c);return c.QuerySingle<int>("SELECT COUNT(*) FROM dbo.DesktopOfflineConflicts WHERE OperationId=@Id AND OfficeId=@OfficeId AND ActorUserId=@UserId AND ResolvedUtc IS NOT NULL",new{Id=operationId,_store.OfficeId,_store.UserId})==1; }
        }
        public int FinalizeDraft(Guid entityId,string expectedRevision)
        {
            using(var c=Open())
            {
                Acquire(c,entityId);
                try
                {
                    var actor=Authenticate(c);var draft=c.QuerySingleOrDefault("SELECT Module,PayloadJson,Revision,Status,ResultId FROM dbo.DesktopOfflineDrafts WHERE EntityId=@EntityId AND OfficeId=@OfficeId",new{EntityId=entityId,_store.OfficeId});
                    if(draft==null)throw new InvalidOperationException("Synchronize the draft before finalizing it.");
                    string module=draft.Module;Demand(actor,module,true);
                    if((string)draft.Status=="Finalized")return (int)draft.ResultId;
                    if((string)draft.Revision!=expectedRevision)throw new InvalidOperationException("The office draft changed. Refresh and review it before finalizing.");
                    bool invoice=module=="InvoiceDrafts";
                    string lookup=invoice?"SELECT InvoiceID FROM dbo.Invoices WHERE OfflineDraftId=@Id":"SELECT PaymentID FROM dbo.Payments WHERE OfflineDraftId=@Id";
                    int result=c.QuerySingleOrDefault<int>(lookup,new{Id=entityId});
                    if(result==0)
                    {
                        var data=JObject.Parse((string)draft.PayloadJson);
                        if(invoice)
                        {
                            var value=OfflineJson.Model<Invoice>(data);value.ClientID=ResolveClient(c,null,data);value.OfflineDraftId=entityId;value.InvoiceID=0;value.InvoiceNumber=null;value.PaymentStatus="Pending";value.PaidAmount=0;
                            result=new InvoiceService().CreateInvoiceWithLineItems(value);
                        }
                        else
                        {
                            var value=OfflineJson.Model<Payment>(data);value.OfflineDraftId=entityId;value.PaymentID=0;value.PaymentNumber=null;
                            result=new PaymentService().RecordPayment(value);
                        }
                        if(result<=0)throw new InvalidOperationException("The entry was not finalized on the office server.");
                    }
                    else if(!invoice)
                    {
                        int invoiceId=c.QuerySingle<int>("SELECT InvoiceID FROM dbo.Payments WHERE PaymentID=@Id",new{Id=result});
                        new PaymentService().RecalculateInvoiceStatus(invoiceId);
                    }
                    c.Execute("UPDATE dbo.DesktopOfflineDrafts SET Status='Finalized',ResultId=@Result,UpdatedUtc=SYSUTCDATETIME() WHERE EntityId=@EntityId AND OfficeId=@OfficeId",new{Result=result,EntityId=entityId,_store.OfficeId});
                    return result;
                }
                finally { c.Execute("EXEC sys.sp_releaseapplock @Resource=@Resource,@LockOwner='Session'",new{Resource=Resource(entityId)}); }
            }
        }
    }
}
