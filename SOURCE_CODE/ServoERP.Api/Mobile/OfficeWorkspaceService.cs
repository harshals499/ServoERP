using System.Data;
using System.Globalization;
using System.Text.Json;
using Dapper;
using FluentValidation;
using Microsoft.Data.SqlClient;
using ServoERP.Engineer;

namespace ServoERP.Api.Mobile;

// Identifiers and predicates below are a server-owned allowlist. Client keys never become SQL identifiers.
public sealed partial class OfficeWorkspaceService(string connectionString)
{
    private sealed class SaveValidator : AbstractValidator<OfficeSaveRequest>
    {
        public SaveValidator()
        {
            RuleFor(x=>x.OperationId).NotEmpty();RuleFor(x=>x.Record).NotNull();
            When(x=>x.Record!=null,()=>{RuleFor(x=>x.Record.Id).NotEmpty();RuleFor(x=>x.Record.ServerId).GreaterThanOrEqualTo(0);RuleFor(x=>x.Record.Form).NotEmpty().MaximumLength(40);RuleFor(x=>x.Record.Values).NotNull().Must(v=>v!=null&&v.Count<=64&&v.All(p=>p.Key.Length<=60&&p.Value!=null&&p.Value.Length<=7_000_000));});
        }
    }
    internal sealed record Column(DemoField Field, string Name);
    internal sealed record Register(string Key, string Module, string Title, string Permission, string Table, string Id, string Scope, Column[] Columns, bool Create = true, bool Edit = true);
    private static Column Text(string key,string label,string column,bool required=false,int length=250) => new(new(key,label,key is "notes" or "fault" or "address"?"notes":"text",Required:required,MaxLength:length),column);
    private static Column Num(string key,string label,string column,bool readOnly=false) => new(new(key,label,"number",true,ReadOnly:readOnly),column);
    private static Column Date(string key,string label,string column,bool required=false) => new(new(key,label,"date",required),column);
    private static Column Link(string key,string label,string column,string relation,bool required=true) => new(new(key,label,"relation",required,Relation:relation),column);
    private static Column Choice(string key,string label,string column,params string[] choices) => new(new(key,label,"choice",true,choices),column);
    internal const string EmployeeScope = "(EXISTS(SELECT 1 FROM MobileEmployeeCompanies ec WHERE ec.EmployeeId=t.EmployeeID AND ec.CompanyId=@CompanyId) OR ((SELECT COUNT(*) FROM Companies)=1 AND EXISTS(SELECT 1 FROM Companies WHERE CompanyId=@CompanyId AND IsActive=1)))";
    private const string EmployeeChild = "EXISTS(SELECT 1 FROM Employees e WHERE e.EmployeeID=t.EmployeeID AND (EXISTS(SELECT 1 FROM MobileEmployeeCompanies ec WHERE ec.EmployeeId=e.EmployeeID AND ec.CompanyId=@CompanyId) OR ((SELECT COUNT(*) FROM Companies)=1 AND EXISTS(SELECT 1 FROM Companies WHERE CompanyId=@CompanyId AND IsActive=1))))";
    private const string ClientScope = "EXISTS(SELECT 1 FROM B2BClients c WHERE c.ClientID=t.ClientID AND c.CompanyId=@CompanyId AND c.DeletedUtc IS NULL)";
    private const string JobScope = ClientScope+" AND t.DeletedUtc IS NULL AND (t.AssignedEmployeeID=@EmployeeId OR EXISTS(SELECT 1 FROM RolePermissions WHERE RoleId=@RoleId AND ModuleKey='WorkOrders' AND (CanCreate=1 OR CanDelete=1)))";
    internal static readonly Register[] Registers = [
        new("client","Site monitor","Client","Clients","B2BClients","ClientID","t.CompanyId=@CompanyId AND t.DeletedUtc IS NULL",[Text("name","Client name","CompanyName",true,255)],false,false),
        new("contract","My work","Linked contract","Contracts","AMCContracts","ContractID",ClientScope,[Text("name","Linked contract","ContractID")],false,false),
        new("vendor","Inventory","Vendor","Vendors","Vendors","VendorID","t.CompanyId=@CompanyId AND t.IsActive=1",[Text("name","Vendor name","VendorName",true,255),new(new("gst","Supplier GSTIN",ReadOnly:true),"GSTNumber"),new(new("phone","Phone",ReadOnly:true),"Phone"),new(new("email","Email",ReadOnly:true),"Email"),new(new("credit","Credit days","number",ReadOnly:true),"DefaultCreditDays")],false,false),
        new("site","Site monitor","Site","GeoIntelligence","ClientSites","SiteID",ClientScope+" AND t.DeletedUtc IS NULL",[Text("name","Site name","SiteName",true,255),Link("client","Client","ClientID","client"),Text("address","Address","Address",true,1000),Text("region","City","City",false,100),Num("ac","AC systems","ACSystemCount"),Num("refrigeration","Refrigeration systems","RefrigerationSystemCount"),Num("towers","Cooling towers","CoolingTowerCount")]),
        new("employee","Employees","Employee profile","Employees","Employees","EmployeeID",EmployeeScope,[Text("number","Employee code","EmployeeCode",false,20),Text("name","Employee name","Name",true,150),Text("designation","Designation","Designation",false,100),Text("department","Department","Department",false,100),Text("phone","Phone","Phone",false,20),Text("whatsapp","WhatsApp","WhatsAppNumber",false,15),Text("emergency","Emergency contact","EmergencyContactName",false,100),Text("emergencyphone","Emergency phone","EmergencyContactPhone",false,15),Date("date","Joining date","JoiningDate"),Date("birth","Date of birth","DateOfBirth"),Text("employment","Employment type","EmploymentType",false,20),Text("site","Site assignment","ClientSite",false,255),Text("aadhaar","Aadhaar","AadhaarNumber",false,12),Text("pan","PAN","PANNumber",false,10),Text("blood","Blood group","BloodGroup",false,5),Date("probation","Probation end","ProbationEndDate"),Date("confirmation","Confirmation date","ConfirmationDate"),Date("lastday","Last working day","LastWorkingDay"),Text("bank","Bank name","BankName",false,100),Text("account","Bank account","BankAccountNumber",false,20),Text("ifsc","IFSC","BankIFSC",false,11),new(new("status","Status","choice",Choices:["Active","Inactive","Leave"]),"Status"),new(new("rehire","Employee is a rehire","boolean"),"IsRehire")]),
        new("job","My work","Job","WorkOrders","Jobs","JobID",JobScope,[Text("number","Job number","JobNumber",false,50),Text("name","Job title","Title",true,255),Text("type","Job type","JobType",false,100),Link("contract","Linked contract","LinkedContractId","contract",false),Link("client","Client","ClientID","client"),Link("site","Site","SiteID","site",false),Link("employee","Assigned employee","AssignedEmployeeID","employee",false),Date("date","Scheduled date","ScheduledDate",true),Choice("priority","Priority","Priority","Medium","Low","High","Urgent"),Text("fault","Description","Description",false,8000),Text("notes","Office notes","Notes",false,8000),new(new("status","Office status",ReadOnly:true),"PipelineStatus"),new(new("fieldjobid","Field service identifier",ReadOnly:true),"SyncPublicId")]),
        new("equipment","Site monitor","Equipment","GeoIntelligence","ClientAssets","AssetId",ClientScope,[Text("number","Asset tag","AssetTag",true,80),Text("name","Equipment type","EquipmentType",true,100),Link("client","Client","ClientId","client"),Link("site","Site","SiteId","site",false),Text("make","Brand","Brand",false,100),Text("model","Model","ModelNumber",false,100),Text("serial","Serial number","SerialNumber",false,120),Text("location","Location","LocationDetail",false,255),Date("date","Installation date","InstallDate"),Date("warranty","Warranty expiry","WarrantyExpiry")]),
        new("issue","Site monitor","Site issue","ServiceDesk","ServiceDeskIncidents","IncidentId",ClientScope,[Text("number","Incident number","IncidentNumber",false,50),Text("name","Summary","ShortDescription",true,250),Link("client","Client","ClientId","client"),Link("site","Site","SiteId","site",false),Link("employee","Assigned employee","AssignedEmployeeId","employee",false),Text("category","Category","Category",true,80),Choice("priority","Priority","Priority","Low","Normal","High","Urgent"),Choice("status","Status","Status","Open","Assigned","In Progress","Resolved","Closed"),Date("date","SLA deadline","SlaDueAt",true),Text("notes","Description","Description",false,8000),Text("caller","Caller name","CallerName",false,200),Text("callerphone","Caller phone","CallerPhone",false,30),Text("equipment","Equipment","EquipmentType",false,100),Text("serial","Asset / serial number","AssetSerialNumber",false,120),Text("rootcause","Root cause / resolution","RootCause",false,8000)]),
        new("attendance","Attendance","Attendance entry","Attendance","AttendanceRecords","AttendanceId",EmployeeChild,[Link("employee","Employee","EmployeeId","employee"),Date("date","Attendance date","AttendanceDate",true),Choice("status","Status","Status","Present","Absent","Leave","HalfDay","WeeklyOff","Holiday","Late"),Num("hours","Overtime hours","OvertimeHours"),Text("notes","Remarks","Notes",false,200)]),
        new("clock","Attendance","Clock records","Attendance","EmployeeAttendance","AttendanceID",EmployeeChild,[Link("employee","Employee","EmployeeID","employee"),Date("date","Clock date","AttendanceDate"),new(new("in","Check-in",ReadOnly:true),"CheckInTime"),new(new("out","Check-out",ReadOnly:true),"CheckOutTime"),new(new("status","Status",ReadOnly:true),"Status")],false,false),
        new("skill","Employees","Skill / Certification","Employees","EmployeeSkills","SkillID",EmployeeChild,[Link("employee","Employee","EmployeeID","employee"),Text("name","Skill / Certificate","SkillName",true,100),Text("number","Certificate number","CertificationNumber",false,50),Date("date","Expiry date","ExpiryDate")]),
        new("pay","Employees","Pay profile","Payroll","SalaryStructure","SalaryID",EmployeeChild,[Link("employee","Employee","EmployeeID","employee"),Num("basic","Basic salary (₹)","BasicSalary"),Num("hra","HRA (₹)","HRA"),Num("allowance","Allowances (₹)","Allowances"),Num("pf","PF deduction (₹)","PFDeduction"),Num("esi","ESI deduction (₹)","ESICDeduction"),Date("date","Effective from","EffectiveFrom",true)]),
        new("advance","Employees","Salary advances","Payroll","SalaryAdvances","AdvanceId",EmployeeChild,[Link("employee","Employee","EmployeeId","employee"),Date("date","Advance Date","AdvanceDate"),Num("amount","Amount (₹)","AdvanceAmount",true),Num("month","Recovery Month","RecoveryMonth",true),Num("year","Recovery Year","RecoveryYear",true),new(new("recovered","Recovered","boolean",ReadOnly:true),"Recovered")],false,false),
        new("document","Employees","Employee document","Employees","EmployeeDocuments","DocumentID",EmployeeChild,[Link("employee","Employee","EmployeeID","employee"),Text("name","File name","FileName",true,200),Text("type","Document type","DocumentType",true,50),Date("date","Expiry date","ExpiryDate")]),
        new("leavetype","Attendance","Leave type","Payroll","LeaveTypes","LeaveTypeId","1=1",[Text("name","Leave type","LeaveTypeName",true,50)],false,false),
        new("leave","Attendance","Leave balance","Payroll","LeaveBalances","BalanceId",EmployeeChild,[Link("employee","Employee","EmployeeId","employee"),Link("type","Leave type","LeaveTypeId","leavetype"),Num("year","Year","Year"),Num("opening","Opening balance","Opening"),Num("accrued","Accrued","Accrued"),Num("used","Used","Used"),Num("closing","Closing balance","Closing",true)]),
        new("dispatch","Site monitor","Dispatch","WorkOrders","Jobs","JobID",JobScope,[Text("number","Job number","JobNumber",false,50),Text("name","Job title","Title",true,255),Link("employee","Technician","AssignedEmployeeID","employee",false),Date("date","Visit date","ScheduledDate",true),new(new("status","Office status",ReadOnly:true),"PipelineStatus")],false,true),
        new("checklist","My work","Service checklist","WorkOrders","JobChecklistItems","ChecklistItemId","EXISTS(SELECT 1 FROM Jobs j JOIN B2BClients c ON c.ClientID=j.ClientID WHERE j.JobID=t.JobId AND c.CompanyId=@CompanyId AND j.DeletedUtc IS NULL AND (j.AssignedEmployeeID=@EmployeeId OR EXISTS(SELECT 1 FROM RolePermissions WHERE RoleId=@RoleId AND ModuleKey='WorkOrders' AND (CanCreate=1 OR CanDelete=1))))",[Link("job","Job","JobId","job"),Text("name","Inspection","ItemText",true,200),Num("sort","Sort order","SortOrder"),new(new("outcome","Outcome",ReadOnly:true),"EngineerOutcome"),new(new("reading","Reading",ReadOnly:true),"EngineerReading"),new(new("notes","Engineer notes",ReadOnly:true),"EngineerNote")]),
        new("material","Inventory","Material","Inventory","StockItems","ItemID","t.CompanyId=@CompanyId AND t.IsActive=1",[Text("name","Material name","ItemName",true,200),Text("category","Category","Category",false,100),Text("unit","Unit","Unit",true,30),Num("stock","Current stock","CurrentStock"),Num("rate","Last purchase rate (₹)","LastPurchaseRate"),Num("reorder","Reorder level","ReorderLevel"),Text("godown","Godown","TallyGodownName",false,100),Link("vendor","Preferred vendor","VendorID","vendor",false)],true,true),
        new("adjustment","Inventory","Stock adjustment / movements","Inventory","StockMovements","MovementID","t.CompanyId=@CompanyId",[Link("item","Material","ItemID","material"),Num("before","Previous stock","StockBefore",true),Num("stock","Counted stock","StockAfter"),Num("quantity","Movement quantity","Quantity",true),new(new("status","Movement type",ReadOnly:true),"MovementType"),new(new("number","Reference",ReadOnly:true),"ReferenceNo"),Text("notes","Reason","Notes",true,1000)],true,false),
        new("rate","Inventory","Supplier rate","Inventory","SupplierItemPrices","PriceID","EXISTS(SELECT 1 FROM Vendors v WHERE v.VendorID=t.VendorID AND v.CompanyId=@CompanyId)",[Link("vendor","Vendor","VendorID","vendor"),Link("item","Material","ItemID","material"),Text("name","Item description","ItemName",true,255),Text("unit","Unit","Unit",true,50),Num("rate","Rate (₹)","Rate"),Date("date","Effective date","EffectiveDate",true),Text("notes","Terms","Notes",false,500)]),
        new("request","Inventory","Purchase order","Purchases","PurchaseOrders","POID","t.CompanyId=@CompanyId",[Text("number","PO number","PONumber",false,50),Link("vendor","Vendor","VendorID","vendor"),Date("date","PO date","PODate",true),new(new("status","Status",ReadOnly:true),"Status"),Num("total","Total (₹)","TotalAmount",true),Num("paid","Paid amount","PaidAmount",true),Text("notes","Notes","Notes",false,1000),Date("requiredby","Required By","PayByDate"),Text("supplierinvoice","Supplier Invoice #","VendorInvoiceNumber",false,100),Link("client","Client","ClientID","client",false),Link("site","Project / Site (optional)","SiteID","site",false),Link("contract","Linked contract","RelatedContractID","contract",false),Link("job","Linked work order","LinkedToId","job",false),Choice("delivery","Delivery Mode","DeliveryMode","TechPickup","SiteDelivery"),Link("employee","Assigned Technician","AssignedTechnicianId","employee",false),Text("address","Address","DeliveryAddress",false,1000),new(new("createdby","Created By",ReadOnly:true),"CreatedByName"),new(new("linkedtype","Linked To",ReadOnly:true),"LinkedToType"),new(new("linkedid","Linked record reference",ReadOnly:true),"LinkedToId")],true,true),
        new("purchaseline","Inventory","Purchase order line","Purchases","PurchaseLineItems","LineItemID","t.CompanyId=@CompanyId AND EXISTS(SELECT 1 FROM PurchaseOrders po WHERE po.POID=t.POID AND po.CompanyId=@CompanyId)",[Link("request","Purchase order","POID","request"),Link("item","Material","InventoryItemId","material"),Text("name","Description","Description",true,500),Text("unit","Unit","UOM",true,30),Num("quantity","Quantity","Quantity"),Num("rate","Rate (₹)","Rate"),Num("total","Amount (₹)","Amount",true),Date("expected","Expected Delivery Date","ExpectedDeliveryDate"),new(new("hsn","HSN / SAC",ReadOnly:true),"HsnSacCode"),new(new("gst","GST %","number",ReadOnly:true),"GSTRate")])
    ];
    private static string Expression(Register r,Column c)=>r.Key=="request"&&c.Field.Key=="contract"?"CASE WHEN t.LinkedToType='Contract' THEN COALESCE(t.RelatedContractID,t.LinkedToId) ELSE t.RelatedContractID END":r.Key=="request"&&c.Field.Key=="job"?"CASE WHEN t.LinkedToType='WorkOrder' THEN t.LinkedToId ELSE NULL END":r.Key=="contract"&&c.Field.Key=="name"?"CONCAT('AMC-',t.ContractID)":r.Key is "job" or "dispatch" && c.Field.Key=="status"?"COALESCE(NULLIF(t.PipelineStatus,''),t.Status)":r.Key=="employee"&&c.Field.Key=="date"?"COALESCE(t.DateOfJoining,t.JoiningDate)":r.Key=="employee"&&c.Field.Key=="pan"?"COALESCE(NULLIF(t.PANNumber,''),t.PAN)":$"t.[{c.Name}]";
    private static string Select(Register r,bool locked=false) => $"SELECT t.[{r.Id}] ServerId,{string.Join(',',r.Columns.Select(c=>$"{Expression(r,c)} [{c.Field.Key}]"))}{(r.Key=="employee"?",CONVERT(varchar(64),HASHBYTES('SHA2_256',t.Photo),2) PhotoHash":"")}{(r.Key=="document"?",CONVERT(varchar(64),HASHBYTES('SHA2_256',t.FileData),2) AttachmentHash":"")}{(r.Key=="attendance"?",(SELECT TOP(1) CONVERT(varchar(8),a.CheckInTime,108) FROM EmployeeAttendance a WHERE a.EmployeeID=t.EmployeeId AND CONVERT(date,a.AttendanceDate)=t.AttendanceDate ORDER BY a.AttendanceID DESC) ClockIn,(SELECT TOP(1) CONVERT(varchar(8),a.CheckOutTime,108) FROM EmployeeAttendance a WHERE a.EmployeeID=t.EmployeeId AND CONVERT(date,a.AttendanceDate)=t.AttendanceDate ORDER BY a.AttendanceID DESC) ClockOut":"")} FROM [{r.Table}] t{(locked?" WITH(UPDLOCK,HOLDLOCK)":"")} WHERE {r.Scope}";
    private static DemoRecord ConvertRow(Register r,IDictionary<string,object> row)
    {
        int id=Convert.ToInt32(row["ServerId"]); var record=new DemoRecord {Form=r.Key,ServerId=id,Id=WorkspaceIds.FromDatabase(r.Key,id)};
        foreach(var col in r.Columns) {var value=row[col.Field.Key];record.Values[col.Field.Key]=value is not null && col.Field.Kind=="relation"?WorkspaceIds.FromDatabase(col.Field.Relation!,Convert.ToInt32(value)).ToString():value is null?"":value is TimeSpan time?time.ToString(@"hh\:mm"):value is DateTime d?d.ToString("dd/MM/yyyy",CultureInfo.InvariantCulture):value is decimal n?n.ToString(CultureInfo.InvariantCulture):Convert.ToString(value,CultureInfo.InvariantCulture)??"";}
        if(r.Key is "job" or "dispatch" or "issue" && row["date"] is DateTime scheduled)record.Values["time"]=scheduled.ToString("HH:mm",CultureInfo.InvariantCulture);
        if(r.Key=="attendance") {record.Values["in"]=(Convert.ToString(row["ClockIn"])??"").PadRight(5)[..5].Trim();record.Values["out"]=(Convert.ToString(row["ClockOut"])??"").PadRight(5)[..5].Trim();}
        if(r.Key=="employee")record.Values["hasphoto"]=(!string.IsNullOrEmpty(Convert.ToString(row["PhotoHash"]))).ToString();
        record.Version=MobileAuthService.Hash(JsonSerializer.Serialize(row)); return record;
    }
    private static async Task<Dictionary<string,FormPermission>> Permissions(SqlConnection c,EngineerIdentity identity,SqlTransaction? tx,CancellationToken ct)
    {
        var rows=await c.QueryAsync(new CommandDefinition("SELECT ModuleKey,CanView,CanCreate,CanEdit FROM RolePermissions WHERE RoleId=@RoleId",identity,tx,cancellationToken:ct));
        var byModule=rows.ToDictionary(x=>(string)x.ModuleKey,x=>new FormPermission((bool)x.CanView,(bool)x.CanCreate,(bool)x.CanEdit));
        return Registers.ToDictionary(r=>r.Key,r=>byModule.TryGetValue(r.Permission,out var p)?new FormPermission(p.CanView,p.CanView&&(r.Key=="adjustment"?p.CanEdit:p.CanCreate)&&r.Create,p.CanView&&p.CanEdit&&r.Edit):new(false,false,false));
    }
    public async Task<OfficeWorkspaceSnapshot> Read(EngineerIdentity identity,CancellationToken ct)
    {
        await using var c=new SqlConnection(connectionString);await c.OpenAsync(ct);
        var permissions=await Permissions(c,identity,null,ct);
        var result=new OfficeWorkspaceSnapshot {CompanyId=identity.CompanyId,CompanyName=await c.QuerySingleAsync<string>(new CommandDefinition("SELECT CompanyName FROM Companies WHERE CompanyId=@CompanyId",identity,cancellationToken:ct)),Permissions=permissions};
        // Relation lookups expose only names; full employee/pay records still require their own permission.
        var visible=Registers.Where(r=>permissions[r.Key].CanView).ToList();
        var lookups=visible.SelectMany(r=>r.Columns.Where(c=>c.Field.Relation!=null).Select(c=>c.Field.Relation!)).ToHashSet();
        if(permissions["material"].CanView)lookups.Add("employee");
        foreach(var r in Registers.Where(r=>permissions[r.Key].CanView||lookups.Contains(r.Key)))
        {
            if(r.Key=="advance" && await c.ExecuteScalarAsync<int>(new CommandDefinition("SELECT CASE WHEN OBJECT_ID('SalaryAdvances','U') IS NULL THEN 0 ELSE 1 END",cancellationToken:ct))==0)continue;
            if(r.Key=="contract" && await c.ExecuteScalarAsync<int>(new CommandDefinition("SELECT CASE WHEN OBJECT_ID('AMCContracts','U') IS NULL THEN 0 ELSE 1 END",cancellationToken:ct))==0)continue;
            bool full=permissions[r.Key].CanView;
            if(full)result.Forms.Add(new(r.Key,r.Module,r.Title,r.Key=="leave"?"Desktop leave balances. Daily leave attendance is recorded in Attendance entry.":"Shared with ServoERP Desktop.",r.Columns.Where(x=>x.Field.Key!="fieldjobid").Select(x=>x.Field).Concat(r.Key=="document"?new[]{new DemoField("filebase64","Attachment (PDF, JPEG or PNG, up to 5 MB)","file")}:r.Key=="attendance"?[new DemoField("in","Check-in (HH:mm)"),new DemoField("out","Check-out (HH:mm)")]:r.Key is "job" or "dispatch" or "issue"?[new DemoField("time",r.Key=="issue"?"SLA deadline time (HH:mm)":"Scheduled time (HH:mm)",Required:true)]:[]).ToArray()));
            var label=r.Columns.FirstOrDefault(c=>c.Field.Key=="name")??r.Columns.FirstOrDefault(c=>c.Field.Key=="number")??r.Columns[0];
            var sql=full?Select(r):$"SELECT t.[{r.Id}] ServerId,{Expression(r,label)} name FROM [{r.Table}] t WHERE {r.Scope}";
            foreach(IDictionary<string,object> row in await c.QueryAsync(new CommandDefinition(sql,identity,cancellationToken:ct)))
            {
                if(full)result.Records.Add(ConvertRow(r,row));
                else {int id=Convert.ToInt32(row["ServerId"]);result.Records.Add(new(){Form=r.Key,ServerId=id,Id=WorkspaceIds.FromDatabase(r.Key,id),Values=new(){["name"]=Convert.ToString(row["name"])??""}});}
            }
        }
        await ReadFieldRecords(c,identity,result,ct);
        if(permissions["material"].CanView)
        {
            result.Forms.Add(new("van","Inventory","Engineer stock allocation","Allocate existing stock to a technician. Allocation does not increase total stock.",[new("employee","Technician","relation",true,Relation:"employee"),new("item","Material","relation",true,Relation:"material"),new("quantity","Allocated quantity","number",true)]));
            result.Permissions["van"]=new(true,permissions["material"].CanEdit,permissions["material"].CanEdit);
            foreach(var row in await c.QueryAsync(new CommandDefinition("SELECT EmployeeId,ItemId,Quantity FROM EngineerVanStock WHERE CompanyId=@CompanyId",identity,cancellationToken:ct)))result.Records.Add(Allocation((int)row.EmployeeId,(int)row.ItemId,(decimal)row.Quantity));
        }
        string[] primaryForms=["job","site","attendance","material","employee"];
        result.Forms=result.Forms.OrderBy(f=>Array.IndexOf(primaryForms,f.Key) is var index&&index>=0?index:100).ToList();
        await ApplyDesktopFormat(c,identity,result,ct);
        return result;
    }
    public async Task<DemoRecord> Save(EngineerIdentity identity,OfficeSaveRequest request,CancellationToken ct)
    {
        if(!(await new SaveValidator().ValidateAsync(request,ct)).IsValid)throw new MobileHttpException(400,"Check the form and save operation identifier.");
        if(request.Record.Form=="van")return await SaveAllocation(identity,request,ct);
        if(request.Record.Form=="adjustment")return await SaveAdjustment(identity,request,ct);
        var record=request.Record.Copy();var r=Registers.SingleOrDefault(x=>x.Key==record.Form)??throw new MobileHttpException(400,"Unknown form.");
        await using var c=new SqlConnection(connectionString);await c.OpenAsync(ct);await using var tx=(SqlTransaction)await c.BeginTransactionAsync(IsolationLevel.Serializable,ct);
        var permission=(await Permissions(c,identity,tx,ct))[r.Key];bool create=record.ServerId==0;
        if(!permission.CanView||!(create?permission.CanCreate:permission.CanEdit))throw new MobileHttpException(403,"Your account cannot save this form.");
        var hash=MobileAuthService.Hash(JsonSerializer.Serialize(request.Record));
        var prior=await PriorReceipt(c,tx,identity,request.OperationId,hash,ct);
        if(prior!=null){await tx.CommitAsync(ct);return prior;}
        var p=new DynamicParameters(identity);p.Add("Id",record.ServerId);p.Add("actor",identity.DisplayName);
        DemoRecord? original=null;IDictionary<string,object>? originalValues=null;
        if(!create)
        {
            var row=await c.QuerySingleOrDefaultAsync(new CommandDefinition(Select(r,true)+$" AND t.[{r.Id}]=@Id",p,tx,cancellationToken:ct));
            if(row==null)throw new MobileHttpException(404,"This record is unavailable for your company.");originalValues=(IDictionary<string,object>)row;original=ConvertRow(r,originalValues);
            if(record.Id!=original.Id||record.Version!=original.Version)throw new MobileHttpException(409,"This record changed in the office. Reload it before saving; your entered values have been retained.");
        }
        if(r.Key=="request")
        {
            if(record.Get("contract").Length>0&&record.Get("job").Length>0)throw new FieldRuleException("Link the purchase to either a contract or a work order.");
            if(record.Get("delivery").Length==0)record.Values["delivery"]="TechPickup";
            if(record.Get("requiredby").Length==0) { int vendorId=WorkspaceIds.ToDatabase("vendor",record.Get("vendor"));int days=await c.ExecuteScalarAsync<int>(new CommandDefinition("SELECT DefaultCreditDays FROM Vendors WHERE VendorID=@vendorId AND CompanyId=@CompanyId",new{vendorId,identity.CompanyId},tx,cancellationToken:ct));if(!DateTime.TryParseExact(record.Get("date"),"dd/MM/yyyy",CultureInfo.InvariantCulture,DateTimeStyles.None,out var date))throw new FieldRuleException("Use DD/MM/YYYY for PO date.");record.Values["requiredby"]=date.AddDays(Math.Max(1,days)).ToString("dd/MM/yyyy",CultureInfo.InvariantCulture); }
        }
        DateTime? incidentDue=null;
        if(r.Key=="issue")
        {
            // Same priority/OpenAt rule as desktop ServiceDeskService.ComputeSlaDue; no independent mobile SLA policy.
            DateTime opened=create?DateTime.Now:await c.QuerySingleAsync<DateTime>(new CommandDefinition("SELECT COALESCE(OpenedAt,GETDATE()) FROM ServiceDeskIncidents WHERE IncidentId=@Id",p,tx,cancellationToken:ct));
            DateTime due=record.Get("priority").Trim().ToUpperInvariant() switch{"CRITICAL"=>opened.AddHours(2),"HIGH"=>opened.AddHours(4),"LOW"=>opened.AddDays(2),_=>opened.AddDays(1)};
            incidentDue=due;record.Values["date"]=due.ToString("dd/MM/yyyy",CultureInfo.InvariantCulture);record.Values["time"]=due.ToString("HH:mm",CultureInfo.InvariantCulture);p.Add("opened",opened);
        }
        if(create && (r.Key is "job" or "issue" or "request") && string.IsNullOrWhiteSpace(record.Get("number")))record.Values["number"]=await NextDesktopReference(c,tx,r,ct);
        if(r.Key=="material")foreach(string key in new[]{"stock","rate","reorder"})if(string.IsNullOrWhiteSpace(record.Get(key)))record.Values[key]="0";
        if(r.Key=="material" && !create) { decimal target=DemoWorkspace.Amount(record,"stock");decimal protectedStock=await c.ExecuteScalarAsync<decimal>(new CommandDefinition("SELECT ISNULL(ReservedStock,0)+(SELECT ISNULL(SUM(Quantity),0) FROM EngineerVanStock WITH(UPDLOCK,HOLDLOCK) WHERE CompanyId=@CompanyId AND ItemId=@Id) FROM StockItems WITH(UPDLOCK,HOLDLOCK) WHERE ItemID=@Id AND CompanyId=@CompanyId",p,tx,cancellationToken:ct));if(target<protectedStock)throw new FieldRuleException("Current quantity cannot be lower than reserved and engineer-allocated stock."); }
        if(r.Key=="employee" && !record.Values.ContainsKey("rehire"))record.Values["rehire"]=original?.Get("rehire")??"False";
        if(r.Key=="employee" && create && string.IsNullOrWhiteSpace(record.Get("number"))) { int next=1+await c.ExecuteScalarAsync<int>(new CommandDefinition("SELECT ISNULL(MAX(TRY_CAST(SUBSTRING(EmployeeCode,4,10) AS int)),0) FROM Employees WITH(UPDLOCK,HOLDLOCK) WHERE EmployeeCode LIKE 'EMP%'",transaction:tx,cancellationToken:ct)); record.Values["number"]="EMP"+next.ToString("000",CultureInfo.InvariantCulture); }
        if(r.Key=="employee")
        {
            if(string.IsNullOrWhiteSpace(record.Get("status")))record.Values["status"]="Active";
            foreach(string key in new[]{"number","pan","ifsc"})record.Values[key]=record.Get(key).Trim().ToUpperInvariant();
            if(record.Get("phone").Length>0&&!System.Text.RegularExpressions.Regex.IsMatch(record.Get("phone"),@"^[0-9+\-\s]{10,20}$"))throw new FieldRuleException("Employee phone must be a valid Indian mobile or landline number.");
            if(record.Get("aadhaar").Length>0&&(record.Get("aadhaar").Length!=12||!record.Get("aadhaar").All(char.IsDigit)))throw new FieldRuleException("Aadhaar must contain 12 digits.");
            if(record.Get("pan").Length>0&&!System.Text.RegularExpressions.Regex.IsMatch(record.Get("pan"),"^[A-Z]{5}[0-9]{4}[A-Z]$"))throw new FieldRuleException("Enter a valid uppercase PAN.");
            if(record.Get("ifsc").Length>0&&!System.Text.RegularExpressions.Regex.IsMatch(record.Get("ifsc"),"^[A-Z]{4}0[A-Z0-9]{6}$"))throw new FieldRuleException("Enter a valid uppercase IFSC.");
        }
        if(r.Key=="issue")foreach(string key in new[]{"caller","callerphone","equipment"})if(record.Values.ContainsKey(key)&&string.IsNullOrWhiteSpace(record.Get(key)))throw new FieldRuleException("Complete the caller and equipment details before saving.");
        var writable=r.Columns.Where(x=>!x.Field.ReadOnly).ToList();
        byte[]? attachment=null;
        if(r.Key=="document" && record.Get("filebase64").Length>0)
        {
            try{attachment=Convert.FromBase64String(record.Get("filebase64"));}catch(FormatException){throw new FieldRuleException("Choose a valid PDF, JPEG or PNG document.");}
            if(attachment.Length is < 4 or > 5242880 || !(attachment.AsSpan().StartsWith("%PDF-"u8)||attachment.AsSpan().StartsWith(new byte[]{255,216,255})||attachment.AsSpan().StartsWith(new byte[]{137,80,78,71,13,10,26,10})))throw new FieldRuleException("Choose a PDF, JPEG or PNG document up to 5 MB.");
            p.Add("attachment",attachment,DbType.Binary);
        }
        byte[]? employeePhoto=null;
        if(r.Key=="employee" && record.Get("photobase64").Length>0) { byte[] input;try {input=Convert.FromBase64String(record.Get("photobase64"));}catch(FormatException){throw new FieldRuleException("Choose a JPEG or PNG employee photo.");} if(input.Length>5242880)throw new FieldRuleException("Choose an employee photo under 5 MB."); employeePhoto=MobilePhoto.Normalize(input);p.Add("employeePhoto",employeePhoto,DbType.Binary); }
        if(r.Key=="document"&&create&&attachment==null)throw new FieldRuleException("Choose a document attachment before saving.");
        foreach(var col in writable)
        {
            var f=col.Field;string value=record.Get(f.Key).Trim();object? parsed=value.Length==0?(f.Kind=="number"?(object)0:null):value;
            if(f.Required&&value.Length==0)throw new FieldRuleException(f.Label+" is required.");
            if(f.MaxLength>0&&value.Length>f.MaxLength)throw new FieldRuleException(f.Label+" is too long.");
            if(value.Length>0&&f.Kind=="choice"&&!f.Choices!.Contains(value)&&original?.Get(f.Key)!=value&&!await IsDesktopChoice(c,tx,r.Key,f.Key,value,ct))throw new FieldRuleException("Choose a valid "+f.Label+".");
            if(value.Length>0&&f.Kind=="number") {if(!decimal.TryParse(value,NumberStyles.AllowDecimalPoint|NumberStyles.AllowLeadingSign,CultureInfo.InvariantCulture,out var n)||(n<0&&!(r.Key=="leave"&&f.Key=="opening"))||n>999999999||n< -999999999||decimal.Round(n,2)!=n)throw new FieldRuleException("Enter a non-negative number with at most two decimals for "+f.Label+".");if(col.Name is "Year" or "ACSystemCount" or "RefrigerationSystemCount" or "CoolingTowerCount" or "SortOrder" && decimal.Truncate(n)!=n)throw new FieldRuleException("Enter a whole number for "+f.Label+".");parsed=n;}
            if(value.Length>0&&f.Kind=="date") {if(!DateTime.TryParseExact(value,"dd/MM/yyyy",CultureInfo.InvariantCulture,DateTimeStyles.None,out var date))throw new FieldRuleException("Use DD/MM/YYYY for "+f.Label+".");parsed=originalValues!=null&&originalValues[f.Key] is DateTime oldDate?date.Date.Add(oldDate.TimeOfDay):date;}
            if(value.Length>0&&f.Kind=="relation") {int id=WorkspaceIds.ToDatabase(f.Relation!,value);var target=Registers.Single(x=>x.Key==f.Relation);var q=new DynamicParameters(identity);q.Add("id",id);if(await c.ExecuteScalarAsync<int>(new CommandDefinition($"SELECT COUNT(*) FROM [{target.Table}] t WITH(UPDLOCK,HOLDLOCK) WHERE t.[{target.Id}]=@id AND {target.Scope}",q,tx,cancellationToken:ct))!=1)throw new FieldRuleException("Choose an available "+f.Label+" for this company.");parsed=id;}
            if(f.Kind=="boolean") { if(value.Length>0&&!bool.TryParse(value,out _))throw new FieldRuleException("Choose a valid "+f.Label+"."); parsed=bool.TryParse(value,out var flag)&&flag; }
            p.Add(f.Key,parsed);
        }
        if(r.Columns.Any(x=>x.Field.Key=="site")&&record.Get("site").Length>0&&record.Get("client").Length>0)
        {p.Add("siteCheck",WorkspaceIds.ToDatabase("site",record.Get("site")));p.Add("clientCheck",WorkspaceIds.ToDatabase("client",record.Get("client")));if(await c.ExecuteScalarAsync<int>(new CommandDefinition("SELECT COUNT(*) FROM ClientSites WHERE SiteID=@siteCheck AND ClientID=@clientCheck",p,tx,cancellationToken:ct))!=1)throw new FieldRuleException("The site must belong to the selected client.");}
        if(r.Key=="job" && original?.Get("status")=="Closed" && (r.Columns.Where(x=>!x.Field.ReadOnly).Any(x=>record.Get(x.Field.Key)!=original.Get(x.Field.Key))||record.Get("time")!=original.Get("time")))throw new FieldRuleException("Closed job details cannot be changed while advancing its invoice status.");
        if(r.Key=="job" && record.Get("contract").Length>0) { if(await c.ExecuteScalarAsync<int>(new CommandDefinition("SELECT COUNT(*) FROM AMCContracts WHERE ContractID=@contract AND ClientID=@client AND (@site IS NULL OR SiteID=@site)",p,tx,cancellationToken:ct))!=1)throw new FieldRuleException("The linked contract must belong to the selected client and site."); }
        if(r.Key is "job" or "dispatch" or "issue")
        {
            if(!TimeOnly.TryParseExact(record.Get("time"),"HH:mm",CultureInfo.InvariantCulture,DateTimeStyles.None,out var time))throw new FieldRuleException("Enter scheduled time as HH:mm.");
            p.Add("date",originalValues!=null&&originalValues["date"] is DateTime originalDate&&original!.Get("date")==record.Get("date")&&original.Get("time")==record.Get("time")?originalDate:DateTime.ParseExact(record.Get("date"),"dd/MM/yyyy",CultureInfo.InvariantCulture).Add(time.ToTimeSpan()));
        }
        if(incidentDue.HasValue)p.Add("date",incidentDue.Value);
        if(r.Key is "job" or "employee" or "issue" or "request")
        {
            string referenceColumn=r.Columns.Single(x=>x.Field.Key=="number").Name;
            if(await c.ExecuteScalarAsync<int>(new CommandDefinition($"SELECT COUNT(*) FROM [{r.Table}] t WITH(UPDLOCK,HOLDLOCK) WHERE t.[{referenceColumn}]=@number AND t.[{r.Id}]<>@Id AND {r.Scope}",p,tx,cancellationToken:ct))>0)throw new FieldRuleException("This reference already exists. Edit the existing record or use a unique reference.");
        }
        if(r.Key is "attendance" or "pay")
        {string dateColumn=r.Key=="attendance"?"AttendanceDate":"EffectiveFrom";if(await c.ExecuteScalarAsync<int>(new CommandDefinition($"SELECT COUNT(*) FROM [{r.Table}] WITH(UPDLOCK,HOLDLOCK) WHERE EmployeeId=@employee AND [{dateColumn}]=@date AND [{r.Id}]<>@Id",p,tx,cancellationToken:ct))>0)throw new FieldRuleException("An entry exists for this employee and date. Edit that entry.");}
        if(r.Key=="leave")
        {
            if(!int.TryParse(record.Get("year"),out int year)||year is < 2000 or > 2200)throw new FieldRuleException("Enter a valid leave year.");
            if(await c.ExecuteScalarAsync<int>(new CommandDefinition("SELECT COUNT(*) FROM LeaveBalances WITH(UPDLOCK,HOLDLOCK) WHERE EmployeeId=@employee AND LeaveTypeId=@type AND Year=@year AND BalanceId<>@Id",p,tx,cancellationToken:ct))>0)throw new FieldRuleException("A balance exists for this employee, leave type and year.");
        }
        if(r.Key=="purchaseline")
        {
            if(DemoWorkspace.Amount(record,"quantity")<=0)throw new FieldRuleException("Quantity must be greater than zero.");
            if(original!=null&&original.Get("request")!=record.Get("request"))throw new FieldRuleException("A line cannot be moved to another purchase order.");
            string status=await c.QuerySingleAsync<string>(new CommandDefinition("SELECT Status FROM PurchaseOrders WITH(UPDLOCK,HOLDLOCK) WHERE POID=@request AND CompanyId=@CompanyId",p,tx,cancellationToken:ct));
            if(status!="Draft")throw new FieldRuleException("Only draft purchase lines can be edited.");
            if(!create&&await c.ExecuteScalarAsync<int>(new CommandDefinition("SELECT COUNT(*) FROM PurchaseLineItems WHERE LineItemID=@Id AND (GSTRate<>0 OR CGSTRate<>0 OR SGSTRate<>0 OR IGSTRate<>0)",p,tx,cancellationToken:ct))>0)throw new FieldRuleException("Edit tax-bearing purchase lines in ServoERP Desktop.");
            p.Add("lineAmount",DemoWorkspace.Amount(record,"quantity")*DemoWorkspace.Amount(record,"rate"));
        }
        if(r.Key=="job"&&record.Get("type").Length==0)p.Add("type","General");
        if(r.Key=="request"&&!create&&original!.Get("status")!="Draft")throw new FieldRuleException("Only draft purchase orders can be edited.");
        if(r.Key is "job" or "dispatch" && !create && !(r.Key=="job"&&record.Get("status")=="Invoiced"&&original?.Get("status")=="Closed") && await c.ExecuteScalarAsync<int>(new CommandDefinition("SELECT COUNT(*) FROM Jobs WHERE JobID=@Id AND (Status IN ('Closed','Completed','Cancelled','Invoiced') OR PipelineStatus IN ('Closed','Completed','Cancelled','Invoiced'))",p,tx,cancellationToken:ct))>0)
            throw new FieldRuleException("Review closed, completed, cancelled or invoiced office jobs in ServoERP Desktop.");
        if(r.Key=="checklist"&&original!=null&&original.Get("job")!=record.Get("job"))throw new FieldRuleException("A checklist item cannot be moved to another job.");
        if(r.Key=="checklist")
        {
            var json=await c.QuerySingleOrDefaultAsync<string>(new CommandDefinition("SELECT ReportJson FROM EngineerReports WITH(UPDLOCK,HOLDLOCK) WHERE JobId=@job AND CompanyId=@CompanyId",p,tx,cancellationToken:ct));
            if(json!=null&&JsonSerializer.Deserialize<FieldReport>(json)?.State==FieldState.Completed)throw new FieldRuleException("A completed service report cannot have its checklist changed. Create a follow-up job for additional work.");
        }
        if(create)
        {
            var columns=writable.Select(x=>$"[{x.Name}]").ToList();var values=writable.Select(x=>"@"+x.Field.Key).ToList();
            if(r.Key=="employee"&&employeePhoto!=null){columns.Add("Photo");values.Add("@employeePhoto");}
            if(r.Key=="document"){columns.Add("FileData");values.Add("@attachment");}
            if(r.Key is "material" or "request" or "purchaseline"){columns.Add("CompanyId");values.Add("@CompanyId");}
            if(r.Key=="request"){columns.AddRange(["Status","TotalAmount","CreatedByUserId","CreatedByName","CreatedByDate"]);values.AddRange(["'Draft'","0","@UserId","@actor","GETDATE()"]);}
            if(r.Key=="material"){columns.Add("IsActive");values.Add("1");}
            if(r.Key=="purchaseline"){columns.Add("Amount");values.Add("@lineAmount");}
            if(r.Key=="job"){columns.AddRange(["Status","PipelineStatus","JobTitle","SyncPublicId","SyncVersion","CreatedByUserId","CreatedByName"]);values.AddRange(["'Pending'","CASE WHEN @employee IS NULL THEN 'Created' ELSE 'Assigned' END","@name","NEWID()","1","@UserId","@actor"]);}
            if(r.Key=="equipment"){columns.Add("IsActive");values.Add("1");}
            if(r.Key=="issue"){columns.AddRange(["OpenedAt","CreatedByName"]);values.AddRange(["@opened","@actor"]);}
            record.ServerId=await c.QuerySingleAsync<int>(new CommandDefinition($"INSERT [{r.Table}]({string.Join(',',columns)}) OUTPUT INSERTED.[{r.Id}] VALUES({string.Join(',',values)})",p,tx,cancellationToken:ct));
            p.Add("Id",record.ServerId);
        if(r.Key=="employee")await c.ExecuteAsync(new CommandDefinition("INSERT MobileEmployeeCompanies(CompanyId,EmployeeId) VALUES(@CompanyId,@Id)",p,tx,cancellationToken:ct));
        }
        else await c.ExecuteAsync(new CommandDefinition($"UPDATE t SET {string.Join(',',writable.Select(x=>$"[{x.Name}]=@{x.Field.Key}"))}{(r.Key is "job" or "dispatch"?",SyncVersion=ISNULL(SyncVersion,0)+1,ModifiedDate=GETDATE()":"")}{(employeePhoto!=null?",Photo=@employeePhoto":"")}{(attachment!=null?",FileData=@attachment":"")}{(r.Key=="purchaseline"?",Amount=@lineAmount":"")} FROM [{r.Table}] t WHERE t.[{r.Id}]=@Id AND {r.Scope}",p,tx,cancellationToken:ct));
        if(r.Key=="request") { int? contract=record.Get("contract").Length>0?WorkspaceIds.ToDatabase("contract",record.Get("contract")):null;int? job=record.Get("job").Length>0?WorkspaceIds.ToDatabase("job",record.Get("job")):null;await c.ExecuteAsync(new CommandDefinition("UPDATE PurchaseOrders SET LinkedToType=@type,LinkedToId=@linked,AssignedTechnicianName=(SELECT Name FROM Employees WHERE EmployeeID=AssignedTechnicianId),ModifiedByUserId=@UserId,ModifiedByName=@actor,ModifiedDate=GETDATE() WHERE POID=@Id AND CompanyId=@CompanyId",new{type=contract.HasValue?"Contract":job.HasValue?"WorkOrder":original?.Get("linkedtype") is string oldType&&oldType.Length>0&&oldType is not ("Contract" or "WorkOrder")?oldType:"General",linked=contract??job??(original?.Get("linkedtype") is not ("Contract" or "WorkOrder")&&int.TryParse(original?.Get("linkedid"),out var oldLink)?oldLink:(int?)null),Id=record.ServerId,identity.CompanyId,identity.UserId,actor=identity.DisplayName},tx,cancellationToken:ct)); }
        if(r.Key=="issue")await c.ExecuteAsync(new CommandDefinition("UPDATE ServiceDeskIncidents SET OpenedAt=COALESCE(OpenedAt,@opened),AssignedAt=CASE WHEN AssignedEmployeeId IS NOT NULL THEN COALESCE(AssignedAt,GETDATE()) ELSE AssignedAt END,ResolvedAt=CASE WHEN Status='Resolved' THEN COALESCE(ResolvedAt,GETDATE()) ELSE ResolvedAt END,ClosedAt=CASE WHEN Status='Closed' THEN COALESCE(ClosedAt,GETDATE()) ELSE ClosedAt END,ModifiedByName=@actor,ModifiedDate=GETDATE() WHERE IncidentId=@Id",p,tx,cancellationToken:ct));
        if(r.Key=="job")await ApplyDesktopPipeline(c,tx,identity,record,original,create,ct);
        if(r.Key=="material") { decimal before=original is null?0:DemoWorkspace.Amount(original,"stock"),after=DemoWorkspace.Amount(record,"stock"); if(before!=after)await c.ExecuteAsync(new CommandDefinition("INSERT StockMovements(CompanyId,ItemID,MovementType,Quantity,StockBefore,StockAfter,ReferenceNo,Notes,CreatedByUserId,CreatedByName,CreatedDate) VALUES(@CompanyId,@Id,'Adjustment',@quantity,@before,@after,@reference,'Material form quantity save',@UserId,@actor,GETDATE()); UPDATE StockItems SET LastUpdated=GETDATE() WHERE ItemID=@Id AND CompanyId=@CompanyId",new{identity.CompanyId,identity.UserId,Id=record.ServerId,quantity=Math.Abs(after-before),before,after,reference=request.OperationId.ToString(),actor=identity.DisplayName},tx,cancellationToken:ct)); }
        if(r.Key is "job" or "dispatch")
        {
            await c.ExecuteAsync(new CommandDefinition("UPDATE Jobs SET JobTitle=Title WHERE JobID=@Id; INSERT JobActivityLog(JobId,ActivityText,PerformedBy,ActivityDate,ActivityType) VALUES(@Id,@activity,@actor,GETDATE(),'Info')",new{Id=record.ServerId,actor=identity.DisplayName,activity="Mobile job "+(create?"created":"updated")+"; operation "+request.OperationId},tx,cancellationToken:ct));
            if(create && await c.ExecuteScalarAsync<int>(new CommandDefinition("SELECT CASE WHEN OBJECT_ID('JobChecklistTemplates','U') IS NULL THEN 0 ELSE 1 END",transaction:tx,cancellationToken:ct))==1)await c.ExecuteAsync(new CommandDefinition("INSERT JobChecklistItems(JobId,ItemText,IsCompleted,SortOrder) SELECT @Id,ItemText,0,SortOrder FROM JobChecklistTemplates WHERE JobType=@type",p,tx,cancellationToken:ct));
        }
        if(r.Key=="employee")await c.ExecuteAsync(new CommandDefinition("UPDATE Employees SET DateOfJoining=JoiningDate,AadhaarLast4=CASE WHEN LEN(AadhaarNumber)=12 THEN RIGHT(AadhaarNumber,4) ELSE AadhaarLast4 END,PAN=PANNumber,ModifiedByUserId=@UserId,ModifiedByName=@actor,ModifiedDate=GETDATE() WHERE EmployeeID=@Id",new{Id=record.ServerId,identity.UserId,actor=identity.DisplayName},tx,cancellationToken:ct));
        if(r.Key=="purchaseline")await c.ExecuteAsync(new CommandDefinition("UPDATE PurchaseOrders SET TotalAmount=(SELECT SUM(Amount) FROM PurchaseLineItems WHERE POID=@request AND CompanyId=@CompanyId) WHERE POID=@request AND CompanyId=@CompanyId",p,tx,cancellationToken:ct));
        if(r.Key=="checklist")await c.ExecuteAsync(new CommandDefinition("UPDATE Jobs SET SyncVersion=ISNULL(SyncVersion,0)+1,ModifiedDate=GETDATE() WHERE JobID=@job",p,tx,cancellationToken:ct));
        if(r.Key=="attendance" && (record.Get("in")!=original?.Get("in")||record.Get("out")!=original?.Get("out")) && (original!=null||record.Get("in").Length>0||record.Get("out").Length>0))
        {
            TimeSpan? ParseClock(string key){string value=record.Get(key);if(value.Length==0)return null;if(!TimeOnly.TryParseExact(value,"HH:mm",CultureInfo.InvariantCulture,DateTimeStyles.None,out var time))throw new FieldRuleException("Use HH:mm for check-in and check-out.");return time.ToTimeSpan();}
            var clockIn=ParseClock("in");var clockOut=ParseClock("out");
            if(clockOut.HasValue&&(!clockIn.HasValue||clockOut<=clockIn))throw new FieldRuleException("Check-out must follow check-in for this daily entry.");
            p.Add("clockIn",clockIn,DbType.Time);p.Add("clockOut",clockOut,DbType.Time);
            if(await c.ExecuteScalarAsync<int>(new CommandDefinition("SELECT COUNT(*) FROM EmployeeAttendance WITH(UPDLOCK,HOLDLOCK) WHERE EmployeeID=@employee AND CONVERT(date,AttendanceDate)=@date",p,tx,cancellationToken:ct))>1)throw new FieldRuleException("Multiple clock entries exist for this day. Reconcile them in the office before editing times.");
            await c.ExecuteAsync(new CommandDefinition("UPDATE EmployeeAttendance SET CheckInTime=@clockIn,CheckOutTime=@clockOut,Status=@status WHERE EmployeeID=@employee AND CONVERT(date,AttendanceDate)=@date; IF @@ROWCOUNT=0 INSERT EmployeeAttendance(EmployeeID,AttendanceDate,CheckInTime,CheckOutTime,Status) VALUES(@employee,@date,@clockIn,@clockOut,@status)",p,tx,cancellationToken:ct));
        }
        var saved=ConvertRow(r,(IDictionary<string,object>)await c.QuerySingleAsync(new CommandDefinition(Select(r)+$" AND t.[{r.Id}]=@Id",p,tx,cancellationToken:ct)));
        await StoreReceipt(c,tx,identity,request.OperationId,hash,saved,create?"Create":"Update",r.Permission,"Mobile "+r.Title,ct);
        await tx.CommitAsync(ct);return saved;
    }
    public async Task<(byte[] Bytes,string Name)> Document(EngineerIdentity identity,int id,CancellationToken ct)
    {
        await using var c=new SqlConnection(connectionString);await c.OpenAsync(ct);
        if(!(await Permissions(c,identity,null,ct))["document"].CanView)throw new MobileHttpException(403,"Your account cannot access employee documents.");
        var row=await c.QuerySingleOrDefaultAsync<DocumentFile>(new CommandDefinition("SELECT t.FileData,t.FileName FROM EmployeeDocuments t WHERE t.DocumentID=@id AND "+EmployeeChild,new{identity.CompanyId,id},cancellationToken:ct));
        if(row?.FileData==null)throw new MobileHttpException(404,"This document is unavailable.");
        return (row.FileData,Path.GetFileName(row.FileName));
    }
    private sealed class DocumentFile {public byte[]? FileData{get;set;}public string FileName{get;set;}="";}
    private static DemoRecord Allocation(int employee,int item,decimal quantity)=>new(){Form="van",ServerId=item,Id=WorkspaceIds.FromDatabase("van/"+employee,item),Version=MobileAuthService.Hash(employee+"/"+item+"/"+quantity.ToString("G29",CultureInfo.InvariantCulture)),Values=new(){["employee"]=WorkspaceIds.FromDatabase("employee",employee).ToString(),["item"]=WorkspaceIds.FromDatabase("material",item).ToString(),["quantity"]=quantity.ToString(CultureInfo.InvariantCulture)}};
    private async Task<DemoRecord> SaveAllocation(EngineerIdentity identity,OfficeSaveRequest request,CancellationToken ct)
    {
        var record=request.Record;int employee=WorkspaceIds.ToDatabase("employee",record.Get("employee")),item=WorkspaceIds.ToDatabase("material",record.Get("item"));
        if(!decimal.TryParse(record.Get("quantity"),NumberStyles.AllowDecimalPoint|NumberStyles.AllowLeadingSign,CultureInfo.InvariantCulture,out var quantity)||quantity<0||quantity>99999999||decimal.Round(quantity,2)!=quantity)throw new FieldRuleException("Enter a non-negative allocation with at most two decimals.");
        await using var c=new SqlConnection(connectionString);await c.OpenAsync(ct);await using var tx=(SqlTransaction)await c.BeginTransactionAsync(IsolationLevel.Serializable,ct);
        var inventoryPermission=(await Permissions(c,identity,tx,ct))["material"];if(!inventoryPermission.CanView||!inventoryPermission.CanEdit)throw new MobileHttpException(403,"Your account cannot allocate stock.");
        var p=new{identity.CompanyId,identity.UserId,request.OperationId,employee,item,quantity};string hash=MobileAuthService.Hash(JsonSerializer.Serialize(record));
        var prior=await PriorReceipt(c,tx,identity,request.OperationId,hash,ct);
        if(prior!=null){await tx.CommitAsync(ct);return prior;}
        if(await c.ExecuteScalarAsync<int>(new CommandDefinition("SELECT COUNT(*) FROM Employees t WHERE t.EmployeeID=@employee AND "+EmployeeScope,p,tx,cancellationToken:ct))!=1)throw new FieldRuleException("Choose a technician in this company.");
        var available=await c.QuerySingleOrDefaultAsync<decimal?>(new CommandDefinition("SELECT CurrentStock-ISNULL(ReservedStock,0) FROM StockItems WITH(UPDLOCK,HOLDLOCK) WHERE ItemID=@item AND CompanyId=@CompanyId AND IsActive=1",p,tx,cancellationToken:ct));
        if(available==null)throw new FieldRuleException("Choose an active material in this company.");
        var current=await c.QuerySingleOrDefaultAsync<decimal?>(new CommandDefinition("SELECT Quantity FROM EngineerVanStock WITH(UPDLOCK,HOLDLOCK) WHERE CompanyId=@CompanyId AND EmployeeId=@employee AND ItemId=@item",p,tx,cancellationToken:ct));
        if(current.HasValue ? record.Version!=Allocation(employee,item,current.Value).Version : record.Version.Length>0)throw new MobileHttpException(409,"This allocation changed. Reload it before saving.");
        decimal others=await c.ExecuteScalarAsync<decimal>(new CommandDefinition("SELECT ISNULL(SUM(Quantity),0) FROM EngineerVanStock WITH(UPDLOCK,HOLDLOCK) WHERE CompanyId=@CompanyId AND ItemId=@item AND EmployeeId<>@employee",p,tx,cancellationToken:ct));
        if(others+quantity>available)throw new FieldRuleException("Allocation exceeds unreserved stock available for this material.");
        await c.ExecuteAsync(new CommandDefinition("UPDATE EngineerVanStock SET Quantity=@quantity WHERE CompanyId=@CompanyId AND EmployeeId=@employee AND ItemId=@item; IF @@ROWCOUNT=0 INSERT EngineerVanStock(CompanyId,EmployeeId,ItemId,Quantity) VALUES(@CompanyId,@employee,@item,@quantity)",p,tx,cancellationToken:ct));
        var saved=Allocation(employee,item,quantity);
        await StoreReceipt(c,tx,identity,request.OperationId,hash,saved,"Allocate","Inventory",$"Mobile allocation for employee {employee}: {quantity}",ct);
        await tx.CommitAsync(ct);return saved;
    }
    public async Task<DemoRecord> Receive(EngineerIdentity identity,OfficeSaveRequest request,CancellationToken ct)
    {
        if(request.OperationId==Guid.Empty||request.Record is null||request.Record.Form!="request"||request.Record.ServerId<=0)throw new FieldRuleException("Choose a purchase order to receive.");
        await using var c=new SqlConnection(connectionString);await c.OpenAsync(ct);await using var tx=(SqlTransaction)await c.BeginTransactionAsync(IsolationLevel.Serializable,ct);
        var permissions=await Permissions(c,identity,tx,ct);
        if(!permissions["request"].CanView||!permissions["request"].CanEdit||!permissions["material"].CanView||!permissions["material"].CanEdit)throw new MobileHttpException(403,"Receiving requires purchase and inventory editing permission.");
        var p=new {identity.CompanyId,identity.UserId,request.OperationId,Id=request.Record.ServerId};
        string hash=MobileAuthService.Hash("Receive/"+JsonSerializer.Serialize(request.Record));
        var prior=await PriorReceipt(c,tx,identity,request.OperationId,hash,ct);
        if(prior!=null){await tx.CommitAsync(ct);return prior;}
        var r=Registers.Single(x=>x.Key=="request");
        var row=await c.QuerySingleOrDefaultAsync(new CommandDefinition(Select(r,true)+" AND t.POID=@Id",p,tx,cancellationToken:ct));
        if(row==null)throw new MobileHttpException(404,"This purchase order is unavailable.");
        var current=ConvertRow(r,(IDictionary<string,object>)row);
        if(current.Version!=request.Record.Version)throw new MobileHttpException(409,"The purchase order changed. Refresh and review it before receiving.");
        if(current.Get("status") is not ("Draft" or "Pending" or "Pending Approval" or "Approved" or "Ordered"))throw new FieldRuleException("This order is already received, paid, cancelled or partially processed. Review it in ServoERP Desktop.");
        var lines=(await c.QueryAsync(new CommandDefinition("SELECT InventoryItemId,Description,Quantity,Rate FROM PurchaseLineItems WITH(UPDLOCK,HOLDLOCK) WHERE POID=@Id AND CompanyId=@CompanyId",p,tx,cancellationToken:ct))).ToList();
        if(lines.Count==0||lines.Any(l=>l.InventoryItemId==null||(decimal)l.Quantity<=0))throw new FieldRuleException("Add valid material lines before receiving this purchase order.");
        foreach(var line in lines)
        {
            var stock=await c.QuerySingleOrDefaultAsync(new CommandDefinition("SELECT CurrentStock,Unit FROM StockItems WITH(UPDLOCK,HOLDLOCK) WHERE ItemID=@item AND CompanyId=@CompanyId AND IsActive=1",new {item=(int)line.InventoryItemId,identity.CompanyId},tx,cancellationToken:ct));
            if(stock==null)throw new FieldRuleException("A material is unavailable for this company.");
            decimal before=(decimal)stock.CurrentStock,quantity=(decimal)line.Quantity;
            await c.ExecuteAsync(new CommandDefinition("UPDATE StockItems SET CurrentStock=CurrentStock+@quantity,LastPurchaseRate=CASE WHEN @rate>0 THEN @rate ELSE LastPurchaseRate END,LastUpdated=GETDATE() WHERE ItemID=@item AND CompanyId=@CompanyId; INSERT StockMovements(CompanyId,ItemID,MovementType,Quantity,StockBefore,StockAfter,ToLocation,ReferenceNo,Notes,CreatedByUserId,CreatedByName,CreatedDate) VALUES(@CompanyId,@item,'PurchaseReceive',@quantity,@before,@after,'Main Stock',@reference,'Mobile purchase receipt',@UserId,@actor,GETDATE())",new{identity.CompanyId,identity.UserId,item=(int)line.InventoryItemId,quantity,rate=(decimal)line.Rate,before,after=before+quantity,reference="PO#"+request.Record.ServerId,actor=identity.DisplayName},tx,cancellationToken:ct));
        }
        await c.ExecuteAsync(new CommandDefinition("UPDATE PurchaseOrders SET Status='Fully Received',PaidAmount=TotalAmount WHERE POID=@Id AND CompanyId=@CompanyId",p,tx,cancellationToken:ct));
        var saved=ConvertRow(r,(IDictionary<string,object>)await c.QuerySingleAsync(new CommandDefinition(Select(r)+" AND t.POID=@Id",p,tx,cancellationToken:ct)));
        await StoreReceipt(c,tx,identity,request.OperationId,hash,saved,"Receive","Purchases","Mobile purchase receipt",ct);
        await tx.CommitAsync(ct);return saved;
    }
    private static async Task ReadFieldRecords(SqlConnection c,EngineerIdentity identity,OfficeWorkspaceSnapshot result,CancellationToken ct)
    {
        if(!result.Permissions["job"].CanView)return;
        string scope=" JOIN Jobs j ON j.JobID=t.JobId JOIN B2BClients b ON b.ClientID=j.ClientID WHERE b.CompanyId=@CompanyId AND j.DeletedUtc IS NULL AND (j.AssignedEmployeeID=@EmployeeId OR EXISTS(SELECT 1 FROM RolePermissions WHERE RoleId=@RoleId AND ModuleKey='WorkOrders' AND (CanCreate=1 OR CanDelete=1)))";
        var reports=await c.QueryAsync(new CommandDefinition("SELECT t.JobId,t.ReportJson FROM EngineerReports t"+scope,identity,cancellationToken:ct));
        void Add(string key,string title,DemoField[] fields){result.Forms.Add(new(key,"My work",title,"Recorded through the assigned job's field service tools.",fields));result.Permissions[key]=new(true,false,false);}
        Add("report","Service report",[new("job","Job","relation",Relation:"job"),new("diagnosis","Diagnosis","notes"),new("work","Work performed","notes"),new("tests","Test results","notes"),new("notes","Notes","notes")]);
        foreach(var row in reports){var report=JsonSerializer.Deserialize<FieldReport>((string)row.ReportJson)!;int id=(int)row.JobId;result.Records.Add(new(){Form="report",ServerId=id,Id=WorkspaceIds.FromDatabase("report",id),Values=new(){["job"]=WorkspaceIds.FromDatabase("job",id).ToString(),["diagnosis"]=report.Diagnosis,["work"]=report.WorkPerformed,["tests"]=report.TestResults,["notes"]=report.Notes}});}
        Add("jobpart","Job parts",[new("job","Job","relation",Relation:"job"),new("name","Material"),new("quantity","Quantity","number"),new("status","Stock status")]);
        foreach(var row in await c.QueryAsync(new CommandDefinition("SELECT t.PartUsedId,t.JobId,t.ItemDescription,t.QuantityUsed,t.StockStatus FROM JobPartsUsed t"+scope,identity,cancellationToken:ct)))result.Records.Add(new(){Form="jobpart",Id=WorkspaceIds.FromDatabase("jobpart",(int)row.PartUsedId),Values=new(){["job"]=WorkspaceIds.FromDatabase("job",(int)row.JobId).ToString(),["name"]=(string?)row.ItemDescription??"",["quantity"]=((decimal)row.QuantityUsed).ToString(CultureInfo.InvariantCulture),["status"]=(string?)row.StockStatus??""}});
    }
}




