using System.Data;
using System.Globalization;
using System.Text.Json;
using Dapper;
using Microsoft.Data.SqlClient;
using ServoERP.Engineer;

namespace ServoERP.Api.Mobile;

public sealed partial class OfficeWorkspaceService
{
    private sealed class SavedReceipt {public string RequestHash{get;set;}="";public string RecordJson{get;set;}="";}
    private static async Task<DemoRecord?> PriorReceipt(SqlConnection c,SqlTransaction tx,EngineerIdentity identity,Guid operation,string hash,CancellationToken ct)
    {
        var row=await c.QuerySingleOrDefaultAsync<SavedReceipt>(new CommandDefinition("SELECT RequestHash,RecordJson FROM MobileWorkspaceReceipts WITH(UPDLOCK,HOLDLOCK) WHERE CompanyId=@CompanyId AND UserId=@UserId AND OperationId=@operation",new{identity.CompanyId,identity.UserId,operation},tx,cancellationToken:ct));
        if(row==null)return null;
        if(row.RequestHash!=hash)throw new MobileHttpException(409,"This save identifier was already used for a different request. Reload the record.");
        return JsonSerializer.Deserialize<DemoRecord>(row.RecordJson)!;
    }
    private static Task StoreReceipt(SqlConnection c,SqlTransaction tx,EngineerIdentity identity,Guid operation,string hash,DemoRecord saved,string action,string module,string description,CancellationToken ct)=>
        c.ExecuteAsync(new CommandDefinition("INSERT MobileWorkspaceReceipts(CompanyId,UserId,OperationId,RequestHash,RecordJson) VALUES(@CompanyId,@UserId,@operation,@hash,@json); INSERT AuditLog(UserId,Username,Action,ModuleKey,RecordId,Description) VALUES(@UserId,@actor,@action,@module,@id,@description)",new{identity.CompanyId,identity.UserId,operation,hash,json=JsonSerializer.Serialize(saved),actor=identity.DisplayName,action,module,id=saved.ServerId,description=description+"; operation "+operation},tx,cancellationToken:ct));
    private async Task<DemoRecord> SaveAdjustment(EngineerIdentity identity,OfficeSaveRequest request,CancellationToken ct)
    {
        var record=request.Record;
        if(record.ServerId!=0)throw new FieldRuleException("Stock movements cannot be edited. Record a new adjustment with a reason.");
        int item=WorkspaceIds.ToDatabase("material",record.Get("item"));
        if(!decimal.TryParse(record.Get("stock"),NumberStyles.AllowDecimalPoint,CultureInfo.InvariantCulture,out var target)||target<0||target>99999999||decimal.Round(target,2)!=target)throw new FieldRuleException("Enter the counted stock with up to two decimal places.");
        string reason=record.Get("notes").Trim();if(reason.Length is < 5 or > 1000)throw new FieldRuleException("Enter a stock adjustment reason between 5 and 1,000 characters.");
        await using var c=new SqlConnection(connectionString);await c.OpenAsync(ct);await using var tx=(SqlTransaction)await c.BeginTransactionAsync(IsolationLevel.Serializable,ct);
        var inventoryPermission=(await Permissions(c,identity,tx,ct))["material"];if(!inventoryPermission.CanView||!inventoryPermission.CanEdit)throw new MobileHttpException(403,"Your account cannot adjust inventory.");
        string hash=MobileAuthService.Hash(JsonSerializer.Serialize(record));
        var prior=await PriorReceipt(c,tx,identity,request.OperationId,hash,ct);if(prior!=null){await tx.CommitAsync(ct);return prior;}
        var material=Registers.Single(r=>r.Key=="material");var p=new DynamicParameters(identity);p.Add("item",item);
        var row=await c.QuerySingleOrDefaultAsync(new CommandDefinition(Select(material,true)+" AND t.ItemID=@item",p,tx,cancellationToken:ct));
        if(row==null)throw new FieldRuleException("This material is unavailable for your company.");
        var current=ConvertRow(material,(IDictionary<string,object>)row);
        if(record.Get("stockVersion")!=current.Version)throw new MobileHttpException(409,"Stock changed while this form was open. Reload the register, review the new count and enter a fresh adjustment.");
        decimal before=DemoWorkspace.Amount(current,"stock");
        decimal protectedStock=await c.ExecuteScalarAsync<decimal>(new CommandDefinition("SELECT ISNULL(ReservedStock,0)+(SELECT ISNULL(SUM(Quantity),0) FROM EngineerVanStock WITH(UPDLOCK,HOLDLOCK) WHERE CompanyId=@CompanyId AND ItemId=@item) FROM StockItems WHERE CompanyId=@CompanyId AND ItemID=@item",p,tx,cancellationToken:ct));
        if(target<protectedStock)throw new FieldRuleException("The count cannot be lower than reserved and engineer-allocated stock. Reconcile those allocations first.");
        if(target==before)throw new FieldRuleException("The counted stock already matches the recorded quantity.");
        var values=new{identity.CompanyId,identity.UserId,item,target,before,quantity=Math.Abs(target-before),reference=request.OperationId.ToString(),reason,actor=identity.DisplayName};
        await c.ExecuteAsync(new CommandDefinition("UPDATE StockItems SET CurrentStock=@target,LastUpdated=GETDATE() WHERE ItemID=@item AND CompanyId=@CompanyId",values,tx,cancellationToken:ct));
        int id=await c.QuerySingleAsync<int>(new CommandDefinition("INSERT StockMovements(CompanyId,ItemID,MovementType,Quantity,StockBefore,StockAfter,ReferenceNo,Notes,CreatedByUserId,CreatedByName,CreatedDate) OUTPUT INSERTED.MovementID VALUES(@CompanyId,@item,'Adjustment',@quantity,@before,@target,@reference,@reason,@UserId,@actor,GETDATE())",values,tx,cancellationToken:ct));
        var register=Registers.Single(r=>r.Key=="adjustment");p.Add("id",id);
        var saved=ConvertRow(register,(IDictionary<string,object>)await c.QuerySingleAsync(new CommandDefinition(Select(register)+" AND t.MovementID=@id",p,tx,cancellationToken:ct)));
        await StoreReceipt(c,tx,identity,request.OperationId,hash,saved,"Adjust","Inventory",$"Mobile stock count {before} to {target}",ct);await tx.CommitAsync(ct);return saved;
    }
}

