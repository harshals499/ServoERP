using Dapper;
using Microsoft.Data.SqlClient;
using ServoERP.Engineer;

namespace ServoERP.Api.Mobile;

public sealed partial class OfficeWorkspaceService
{
    private static async Task ApplyDesktopPipeline(SqlConnection c,SqlTransaction tx,EngineerIdentity identity,DemoRecord record,DemoRecord? original,bool create,CancellationToken ct)
    {
        string[] steps=["Created","Assigned","InProgress","ChecklistDone","Closed","Invoiced"];
        string Normal(string value){var text=value.Replace(" ","");return steps.FirstOrDefault(s=>s.Equals(text,StringComparison.OrdinalIgnoreCase))??(text=="Completed"?"Closed":"Created");}
        string current=create?"Created":Normal(original!.Get("status"));string requested=record.Get("status");
        if(requested.Length==0)return;string target=Normal(requested);
        if(!steps.Contains(requested.Replace(" ",""),StringComparer.OrdinalIgnoreCase)&&requested!=original?.Get("status"))throw new FieldRuleException("Choose a valid job status.");
        if(!create&&current==target)return;
        int currentRank=Array.IndexOf(steps,current),targetRank=Array.IndexOf(steps,target);
        if(targetRank<currentRank)throw new FieldRuleException("This action only supports forward pipeline movement.");
        if(create&&targetRank>1)throw new FieldRuleException("Save the job before advancing its pipeline.");
        var row=await c.QuerySingleAsync(new CommandDefinition("SELECT AssignedEmployeeID,InvoiceId FROM Jobs WITH(UPDLOCK,HOLDLOCK) WHERE JobID=@id",new{id=record.ServerId},tx,cancellationToken:ct));
        if(targetRank>=1 && row.AssignedEmployeeID is null)throw new FieldRuleException("Assign a technician before moving this job forward.");
        if(targetRank>=3 && targetRank>currentRank)
        {
            var checklist=await c.QuerySingleAsync(new CommandDefinition("SELECT COUNT(*) Total,ISNULL(SUM(CASE WHEN IsCompleted=1 THEN 1 ELSE 0 END),0) Done FROM JobChecklistItems WITH(UPDLOCK,HOLDLOCK) WHERE JobId=@id",new{id=record.ServerId},tx,cancellationToken:ct));
            if((int)checklist.Total==0||(int)checklist.Done<(int)checklist.Total)throw new FieldRuleException("Complete the checklist before moving to Checklist Done or closing the job.");
        }
        if(target=="Invoiced" && row.InvoiceId is null)throw new FieldRuleException("Create an invoice before moving this job to Invoiced.");
        string legacy=targetRank>=4?"Completed":targetRank>=2?"In Progress":"Pending";
        await c.ExecuteAsync(new CommandDefinition("UPDATE Jobs SET PipelineStatus=@target,Status=@legacy,CompletedDate=CASE WHEN @closed=1 THEN COALESCE(CompletedDate,GETDATE()) ELSE CompletedDate END,ClosedDate=CASE WHEN @closed=1 THEN COALESCE(ClosedDate,GETDATE()) ELSE ClosedDate END,IsOverdue=CASE WHEN CAST(ScheduledDate AS date)<CAST(GETDATE() AS date) AND @closed=0 THEN 1 ELSE 0 END WHERE JobID=@id",new{target,legacy,closed=targetRank>=4,id=record.ServerId},tx,cancellationToken:ct));
    }
    private static async Task<string> NextDesktopReference(SqlConnection c,SqlTransaction tx,Register register,CancellationToken ct)
    {
        if(register.Key=="issue")return "INC"+(1+await c.ExecuteScalarAsync<int>(new CommandDefinition("SELECT ISNULL(MAX(IncidentId),0) FROM ServiceDeskIncidents WITH(UPDLOCK,HOLDLOCK)",transaction:tx,cancellationToken:ct))).ToString("000000");
        string prefix=register.Key=="job"?"JOB-"+DateTime.Now.ToString("yyMMdd")+"-":"PO-"+DateTime.Now.ToString("yyyyMMdd")+"-";
        // Identifiers are from the fixed register allowlist, never request input. Hold range locks through receipt commit.
        string column=register.Key=="job"?"JobNumber":"PONumber";
        int sequence=1+await c.ExecuteScalarAsync<int>(new CommandDefinition($"SELECT ISNULL(MAX(TRY_CAST(SUBSTRING([{column}],@start,20) AS int)),0) FROM [{register.Table}] WITH(UPDLOCK,HOLDLOCK) WHERE [{column}] LIKE @pattern",new{start=prefix.Length+1,pattern=prefix+"%"},tx,cancellationToken:ct));
        return prefix+sequence.ToString(register.Key=="job"?"0000":"000");
    }
    private static async Task<bool> IsDesktopChoice(SqlConnection c,SqlTransaction tx,string form,string field,string value,CancellationToken ct)
    {
        string? category=(form,field) switch{("employee","status")=>"HR.EmployeeStatus",("job","priority") or ("issue","priority")=>"Jobs.Priority",("issue","status")=>"ServiceDesk.Status",_=>null};
        if(category is null)return false;
        string[] fallback=category switch{"HR.EmployeeStatus"=>["Active","Inactive","Leave"],"Jobs.Priority"=>["Low","Medium","High","Critical"],"ServiceDesk.Status"=>["New","Assigned","In Progress","On Hold","Resolved","Closed","Cancelled"],_=>[]};
        if(await c.ExecuteScalarAsync<int>(new CommandDefinition("SELECT CASE WHEN OBJECT_ID('MasterLookupCategories','U') IS NOT NULL AND OBJECT_ID('MasterLookupValues','U') IS NOT NULL THEN 1 ELSE 0 END",transaction:tx,cancellationToken:ct))==0)return fallback.Contains(value);
        var choices=(await c.QueryAsync<string>(new CommandDefinition("SELECT v.DisplayText FROM MasterLookupValues v JOIN MasterLookupCategories c ON c.CategoryId=v.CategoryId WHERE c.CategoryKey=@category AND v.IsActive=1",new{category},tx,cancellationToken:ct))).ToArray();
        return (choices.Length==0?fallback:choices).Contains(value);
    }
    public async Task<byte[]> EmployeePhoto(EngineerIdentity identity,int id,CancellationToken ct)
    {
        await using var c=new SqlConnection(connectionString);await c.OpenAsync(ct);
        if(!(await Permissions(c,identity,null,ct))["employee"].CanView)throw new MobileHttpException(403,"Your account cannot access employee photos.");
        return await c.QuerySingleOrDefaultAsync<byte[]>(new CommandDefinition("SELECT t.Photo FROM Employees t WHERE t.EmployeeID=@Id AND "+EmployeeScope,new{Id=id,identity.CompanyId},cancellationToken:ct))??throw new MobileHttpException(404,"No employee photo is available.");
    }
    private static async Task ApplyDesktopFormat(SqlConnection connection, EngineerIdentity identity, OfficeWorkspaceSnapshot snapshot, CancellationToken ct)
    {
        async Task<string[]> Lookup(string category, string[] fallback)
        {
            // Metadata guard supports older office databases without changing their schema.
            if (await connection.ExecuteScalarAsync<int>(new CommandDefinition("SELECT CASE WHEN OBJECT_ID('MasterLookupCategories','U') IS NOT NULL AND OBJECT_ID('MasterLookupValues','U') IS NOT NULL THEN 1 ELSE 0 END", cancellationToken: ct)) == 0) return fallback;
            var choices = (await connection.QueryAsync<string>(new CommandDefinition("SELECT v.DisplayText FROM MasterLookupValues v JOIN MasterLookupCategories c ON c.CategoryId=v.CategoryId WHERE c.CategoryKey=@category AND v.IsActive=1 ORDER BY v.SortOrder,v.DisplayText", new { category }, cancellationToken: ct))).Distinct(StringComparer.OrdinalIgnoreCase).ToArray();
            return choices.Length == 0 ? fallback : choices;
        }
        var blood = await Lookup("HR.BloodGroup", ["A+","A-","B+","B-","AB+","AB-","O+","O-"]);
        var status = await Lookup("HR.EmployeeStatus", ["Active","Inactive","Leave"]);
        var categories = await Lookup("Inventory.Category", ["Filters","Refrigerant","Compressors","Valves","Belts","Electrical","Copper","Tools","HVAC Spares","General"]);
        var priorities=await Lookup("Jobs.Priority",["Low","Medium","High","Critical"]);
        var incidentStatus=await Lookup("ServiceDesk.Status",["New","Assigned","In Progress","On Hold","Resolved","Closed","Cancelled"]);
        var incidentCategory=await Lookup("ServiceDesk.Category",["AC Breakdown","Chiller","Electrical","Plumbing","AMC","Installation","Gas Charging","Customer Complaint","Emergency","General"]);
        var equipment=await Lookup("ServiceDesk.EquipmentType",["Split AC","Cassette AC","Ductable AC","VRF/VRV","Chiller","Cooling Tower","Pump","Panel","Other"]);
        var jobTypes=await Lookup("Jobs.JobType",["PM Visit","Breakdown","Installation","AMC Visit","Gas Charging","General"]);
        string[] units=["NOS","PCS","KG","LTR","MTR","SET","BOX"];
        var unitLabels=new Dictionary<string,string>{{"NOS","Nos"},{"PCS","Pcs"},{"KG","kg"},{"LTR","L"},{"MTR","m"},{"SET","Set"},{"BOX","Box"}};
        if(await connection.ExecuteScalarAsync<int>(new CommandDefinition("SELECT CASE WHEN OBJECT_ID('UnitMeasurements','U') IS NULL THEN 0 ELSE 1 END",cancellationToken:ct))==1)
        {
            var unitRows=(await connection.QueryAsync(new CommandDefinition("SELECT UnitCode,COALESCE(NULLIF(ShortCode,''),NULLIF(DisplayName,''),UnitCode) Label FROM UnitMeasurements WHERE IsActive=1 ORDER BY Category,DisplayName",cancellationToken:ct))).ToList();
            if(unitRows.Count>0){units=unitRows.Select(r=>(string)r.UnitCode).ToArray();unitLabels=unitRows.ToDictionary(r=>(string)r.UnitCode,r=>(string)r.Label);}
        }
        // Match SiteService.ApplyDisplayNames: collapse duplicate rows, then qualify duplicate names.
        bool hasGeocode=await connection.ExecuteScalarAsync<int>(new CommandDefinition("SELECT CASE WHEN COL_LENGTH('ClientSites','GeocodeAddress') IS NULL THEN 0 ELSE 1 END",cancellationToken:ct))==1;
        var siteRows=(await connection.QueryAsync<DesktopSiteOption>(new CommandDefinition("SELECT s.SiteID,s.ClientID,s.SiteName,s.City,s.Address,"+(hasGeocode?"s.GeocodeAddress":"CAST(NULL AS nvarchar(4000))")+" GeocodeAddress FROM ClientSites s JOIN B2BClients c ON c.ClientID=s.ClientID WHERE c.CompanyId=@CompanyId AND c.DeletedUtc IS NULL AND s.DeletedUtc IS NULL",identity,cancellationToken:ct))).ToList();
        string Normal(string? text)=>string.Join(" ",(text??"").Trim().Split([' ','\t','\r','\n'],StringSplitOptions.RemoveEmptyEntries));
        var distinctSites=siteRows.GroupBy(s=>s.ClientID+"|"+Normal(s.SiteName)+"|"+Normal(s.City)+"|"+Normal(s.Address),StringComparer.OrdinalIgnoreCase).Select(g=>g.OrderBy(s=>s.SiteID).First()).OrderBy(s=>s.SiteName).ThenBy(s=>s.City).ThenBy(s=>s.SiteID).ToList();
        var duplicateNames=distinctSites.GroupBy(s=>Normal(s.SiteName),StringComparer.OrdinalIgnoreCase).Where(g=>g.Count()>1).Select(g=>g.Key).ToHashSet(StringComparer.OrdinalIgnoreCase);
        var sites=distinctSites.Select(s=>{
            string name=string.IsNullOrWhiteSpace(s.SiteName)?"Site "+s.SiteID:s.SiteName.Trim();
            string qualifier=new[]{s.City,s.Address,s.GeocodeAddress,"Site "+s.SiteID}.First(v=>!string.IsNullOrWhiteSpace(v))!.Trim();
            return duplicateNames.Contains(Normal(s.SiteName))?name+" - "+qualifier:name;
        }).Distinct().ToArray();
        for (int i = 0; i < snapshot.Forms.Count; i++)
        {
            var form = snapshot.Forms[i];
            var fields = form.Fields.Select(field => (form.Key, field.Key) switch
            {
                ("employee", "blood") => field with { Kind="choice", Choices=blood },
                ("employee", "status") => field with { Kind="choice", Choices=status },
                ("employee", "site") => field with { Kind="choice", Choices=sites },
                ("material", "category") => field with { Kind="choice", Choices=categories },
                ("material", "unit") => field with { Kind="choice", Choices=units,ChoiceLabels=unitLabels },
                ("job", "type") => field with { Kind="choice", Choices=jobTypes },
                ("job", "status") => field with { Kind="choice", Choices=["Created","Assigned","InProgress","ChecklistDone","Closed","Invoiced"],ReadOnly=false },
                ("job", "number") or ("issue", "number") or ("request", "number") => field with { ReadOnly=true, Required=false },
                ("employee", "phone") or ("employee", "whatsapp") or ("employee", "emergencyphone") or ("issue", "callerphone") => field with { Kind="phone",Required=form.Key=="issue" },
                ("job", "priority") or ("issue", "priority") => field with { Kind="choice", Choices=priorities },
                ("issue", "status") => field with { Choices=incidentStatus },
                ("issue", "category") => field with { Kind="choice", Choices=incidentCategory },
                ("issue", "equipment") => field with { Kind="choice", Choices=equipment, Required=true },
                ("issue", "caller") => field with { Required=true },
                ("issue", "notes") or ("issue", "rootcause") => field with { Kind="notes" },
                ("issue", "date") or ("issue", "time") => field with { ReadOnly=true, Required=false },
                _ => field
            }).ToList();
            if (form.Key == "employee") fields.Add(new("photobase64","Upload Photo (JPEG or PNG, up to 5 MB)","photo"));
            if(form.Key=="request")foreach(var pair in new[]{("suppliergst","Supplier GSTIN"),("supplierphone","Phone"),("supplieremail","Email"),("currency","Currency"),("exchange","Exchange Rate"),("terms","Payment Terms"),("credit","Credit Days"),("purchasetype","Purchase Type"),("priority","Priority"),("department","Department")})fields.Add(new(pair.Item1,pair.Item2,ReadOnly:true));
            snapshot.Forms[i] = form with { Fields=fields.ToArray() };
        }
        foreach(var order in snapshot.Records.Where(r=>r.Form=="request"))
        {
            var vendor=snapshot.Records.FirstOrDefault(r=>r.Form=="vendor"&&r.Id.ToString()==order.Get("vendor"));
            order.Values["suppliergst"]=vendor?.Get("gst")??"";order.Values["supplierphone"]=vendor?.Get("phone")??"";order.Values["supplieremail"]=vendor?.Get("email")??"";
            order.Values["currency"]="INR - Indian Rupee";order.Values["exchange"]="1.0000";order.Values["credit"]=vendor?.Get("credit")??"";order.Values["terms"]=order.Get("credit").Length>0?order.Get("credit")+" Days":"";order.Values["purchasetype"]="General Purchase";order.Values["priority"]="Normal";order.Values["department"]="Purchase";
        }
    }
    private sealed class DesktopSiteOption
    {
        public int SiteID { get; set; }
        public int ClientID { get; set; }
        public string? SiteName { get; set; }
        public string? City { get; set; }
        public string? Address { get; set; }
        public string? GeocodeAddress { get; set; }
    }
}
