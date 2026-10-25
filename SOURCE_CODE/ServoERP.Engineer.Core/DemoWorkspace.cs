using System.Globalization;
using System.Text.Json;
using FluentValidation;

namespace ServoERP.Engineer;

public record DemoField(string Key, string Label, string Kind = "text", bool Required = false, string[]? Choices = null, string? Relation = null, bool ReadOnly = false, int MaxLength = 0)
{
    public Dictionary<string,string>? ChoiceLabels { get; init; }
}
public record DemoForm(string Key, string Module, string Title, string Description, DemoField[] Fields);
public sealed class DemoRecord
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string Form { get; set; } = "";
    public Dictionary<string, string> Values { get; set; } = [];
    public DateTimeOffset UpdatedUtc { get; set; } = DateTimeOffset.UtcNow;
    public int ServerId { get; set; }
    public string Version { get; set; } = "";
    public string Get(string key) => Values.GetValueOrDefault(key, "");
    public string Title => Get("name").Length > 0 ? Get("name") : Get("number");
    public DemoRecord Copy() => new() { Id = Id, Form = Form, Values = new(Values), UpdatedUtc = UpdatedUtc, ServerId = ServerId, Version = Version };
}

/// <summary>Explicitly isolated sample records. This store has no office connection or sync transport.</summary>
public sealed class DemoWorkspace : IFormWorkspace
{
    private readonly string _path;
    private readonly object _gate = new();
    private List<DemoRecord> _records;
    public static readonly CultureInfo India = CultureInfo.GetCultureInfo("en-IN");
    public static readonly string[] Modules = ["Dashboard", "My work", "Site monitor", "Attendance", "Inventory", "Employees"];
    private static DemoField Text(string key, string label, bool required = false) => new(key, label, Required: required);
    private static DemoField Choice(string key, string label, params string[] choices) => new(key, label, "choice", true, choices);
    private static DemoField Link(string key, string label, string form, bool required = true) => new(key, label, "relation", required, Relation: form);
    private static DemoField Date(string key, string label, bool required = false) => new(key, label, "date", required);
    private static DemoField Number(string key, string label, bool required = false) => new(key, label, "number", required);
    public static readonly DemoForm[] Forms = [
        new("job", "My work", "Job", "Schedule service, assign a technician and track progress.", [Text("number","Job number",true),Text("name","Job title",true),Link("site","Client / Site","site"),Link("employee","Assigned employee","employee"),Choice("type","Job type","Breakdown","Preventive maintenance","Installation","AMC"),Choice("priority","Priority","Normal","High","Urgent"),Choice("status","Status","Created","Assigned","In progress","Completed","Cancelled"),Date("date","Scheduled date",true),Text("time","Scheduled time (HH:mm)",true),new("fault","Fault / Description","notes",true)]),
        new("report","My work","Service report","Record diagnosis, work, tests and follow-up notes.",[Text("number","Report number",true),Link("job","Job","job"),new("diagnosis","Diagnosis","notes",true),new("work","Work performed","notes",true),new("tests","Test results","notes",true),new("notes","Additional notes","notes")]),
        new("checklist","My work","Service checklist","Record checks and readings for a job.",[Text("name","Check / Inspection",true),Link("job","Job","job"),Choice("outcome","Outcome","Pass","Fail","N/A"),Text("reading","Reading / Value"),new("notes","Engineer notes","notes")]),
        new("jobpart","My work","Job parts","Record sample material consumption against a job.",[Text("number","Reference",true),Link("job","Job","job"),Link("item","Material","material"),Number("quantity","Quantity",true),new("notes","Notes","notes")]),
        new("site","Site monitor","Site","Maintain client, location and service health details.",[Text("name","Site name",true),Text("client","Client name",true),Text("address","Address",true),Text("region","Region / City",true),Text("contact","Site contact"),new("phone","Phone","phone"),Choice("status","Health","Healthy","Attention","Critical"),Date("visit","Next service date",true),new("notes","Site notes","notes")]),
        new("equipment","Site monitor","Equipment","Maintain assets and maintenance due dates.",[Text("number","Asset code",true),Text("name","Equipment name",true),Link("site","Site","site"),Text("make","Make / Model"),Text("serial","Serial number"),Choice("status","Condition","Operational","Needs service","Breakdown"),Date("date","Maintenance due",true)]),
        new("issue","Site monitor","Site issue","Raise a service issue and assign ownership.",[Text("number","Issue number",true),Text("name","Issue summary",true),Link("site","Site","site"),Link("employee","Assigned employee","employee"),Choice("priority","Priority","Normal","High","Urgent"),Choice("status","Status","Open","In progress","Resolved"),Date("date","Due date",true),new("notes","Description","notes",true)]),
        new("dispatch","Site monitor","Dispatch","Plan technician visits and track availability.",[Text("number","Dispatch reference",true),Link("job","Job","job"),Link("employee","Technician","employee"),Date("date","Visit date",true),Text("time","Visit time (HH:mm)",true),Choice("status","Status","Scheduled","Travelling","On site","Completed"),new("notes","Dispatch notes","notes")]),
        new("attendance","Attendance","Attendance entry","Record a daily status, site and working hours.",[Link("employee","Employee","employee"),Link("site","Client / Site","site"),Date("date","Attendance date",true),Choice("status","Status","Present","Absent","Leave","Half day","Weekly off","Holiday"),Text("in","Check-in (HH:mm)"),Text("out","Check-out (HH:mm)"),Number("hours","Overtime hours"),new("notes","Remarks","notes")]),
        new("leave","Attendance","Leave request","Plan leave dates and record the approval status.",[Link("employee","Employee","employee"),Date("date","From date",true),Date("end","To date",true),Choice("type","Leave type","Casual","Sick","Earned","Unpaid"),Choice("status","Status","Pending","Approved","Rejected"),new("notes","Reason","notes",true)]),
        new("material","Inventory","Material","Desktop material, buying rate, planning quantity and preferred vendor fields.",[Text("number","Item code",true),Text("name","Item name",true),Choice("category","Category","Filters","Refrigerant","Compressors","Valves","Belts","Electrical","Copper","Tools","HVAC Spares","General"),Choice("unit","Unit","Nos","Kg","Litre","Metre","Set","Box"),Number("stock","Current quantity",true),Number("rate","Last purchase rate (₹)",true),Number("reorder","Typical purchase quantity",true),Text("vendor","Preferred vendor"),Text("godown","Godown"),new("notes","Notes","notes")]),
        new("rate","Inventory","Supplier rate","Compare supplier offers for a material.",[Link("item","Material","material"),Text("name","Supplier name",true),Number("rate","Rate (₹)",true),Number("quantity","Minimum quantity",true),Date("date","Valid until",true),Choice("preferred","Preferred vendor","Yes","No"),new("notes","Terms","notes")]),
        new("request","Inventory","Purchase request","Plan material procurement for a site.",[Text("number","Request number",true),Link("item","Material","material"),Link("site","Site","site"),Number("quantity","Required quantity",true),Date("date","Required by",true),Choice("status","Status","Draft","Requested","Ordered","Received"),new("notes","Purpose","notes",true)]),
        new("employee","Employees","Employee profile","Desktop profile, site assignment, emergency contact and joining details.",[Text("number","Employee code",true),Text("name","Employee name",true),Text("designation","Designation",true),Text("department","Department",true),Link("site","Client / Site","site",false),new("phone","Phone","phone"),new("whatsapp","WhatsApp","phone"),Choice("blood","Blood group","Not provided","A+","A-","B+","B-","AB+","AB-","O+","O-"),Text("aadhaar","Aadhaar"),Text("pan","PAN"),Text("emergency","Emergency contact name"),new("emergencyphone","Emergency contact phone","phone"),Date("date","Joining date",true),Date("probation","Probation end"),Date("confirmation","Confirmation date"),Date("lastday","Last working day"),Choice("status","Status","Active","Inactive","Leave"),Choice("rehire","Rehire","No","Yes")]),
        new("skill","Employees","Skill / Certification","Maintain compliance skills and expiry dates.",[Link("employee","Employee","employee"),Text("name","Skill / Certificate",true),Text("issuer","Issuer"),Choice("level","Proficiency","Beginner","Intermediate","Advanced"),Date("date","Expiry date",true),new("notes","Notes","notes")]),
        new("document","Employees","Employee document","Record document metadata; real document attachments remain in office storage.",[Link("employee","Employee","employee"),Text("name","Document name",true),Choice("type","Document type","Identity","Address","Qualification","Medical","Contract","Other"),Text("reference","Reference number"),Date("date","Expiry date"),new("notes","Notes","notes")]),
        new("pay","Employees","Pay profile","Desktop salary setup fields for sample payroll review.",[Link("employee","Employee","employee"),Number("basic","Basic salary (₹)",true),Number("hra","HRA (₹)",true),Number("allowance","Other allowance (₹)",true),Number("deduction","Monthly deductions (₹)",true),Date("date","Effective date",true),Text("bank","Bank name"),Text("account","Account number"),Text("ifsc","IFSC"),Choice("status","Status","Draft","Reviewed"),new("notes","Notes","notes")])
    ];
    public static DemoForm Definition(string key) => Forms.Single(f => f.Key == key);
    public IReadOnlyList<DemoForm> FormCatalog => Forms;
    public IReadOnlyDictionary<string,FormPermission> Permissions => Forms.ToDictionary(f => f.Key,f => new FormPermission(true,true,true));
    public bool IsOffice => false;
    public string Caption => "TEST WORKSPACE · Sample data only";
    public Task Refresh(CancellationToken cancellationToken = default) => Task.CompletedTask;
    public Task<DemoRecord> SaveAsync(DemoRecord record, Guid operationId, CancellationToken cancellationToken = default) => Task.FromResult(Save(record));
    public static bool AcceptsCredentials(string username, string password) => username.Trim().Equals("admin", StringComparison.OrdinalIgnoreCase) && password == "admin";
    public DemoWorkspace(string path)
    {
        _path = Path.GetFullPath(path);
        Directory.CreateDirectory(Path.GetDirectoryName(_path)!);
        _records = File.Exists(_path) ? JsonSerializer.Deserialize<List<DemoRecord>>(File.ReadAllText(_path)) ?? throw new FieldRuleException("Unable to read the test workspace. Your sample records have been retained.") : Seed();
        if (!File.Exists(_path)) Persist(_records);
    }
    public List<DemoRecord> Records(string form) { lock (_gate) return _records.Where(r => r.Form == form).Select(r => r.Copy()).ToList(); }
    public string Label(string id) { lock (_gate) return _records.FirstOrDefault(r => r.Id.ToString() == id)?.Title ?? "Not assigned"; }
    public DemoRecord Save(DemoRecord value)
    {
        lock (_gate)
        {
            var validation = new DemoRecordValidator(this).Validate(value);
            if (!validation.IsValid) throw new FieldRuleException(string.Join("\n", validation.Errors.Select(e => e.ErrorMessage)));
            var copy = value.Copy(); copy.UpdatedUtc = DateTimeOffset.UtcNow;
            var next = _records.Where(r => r.Id != copy.Id).Select(r => r.Copy()).ToList(); next.Add(copy);
            Persist(next); _records = next; return copy.Copy();
        }
    }
    private void Persist(List<DemoRecord> records)
    {
        string temporary = _path + ".tmp";
        File.WriteAllText(temporary, JsonSerializer.Serialize(records));
        File.Move(temporary, _path, true);
    }
    public static decimal Amount(DemoRecord record, string field) => decimal.TryParse(record.Get(field), NumberStyles.Number, India, out var value) ? value : 0;
    private static List<DemoRecord> Seed()
    {
        var records = new List<DemoRecord>();
        DemoRecord Add(string form, params (string, string)[] pairs) { var r = new DemoRecord { Form = form, Values = pairs.ToDictionary(p => p.Item1,p => p.Item2) }; records.Add(r); return r; }
        string Today(int offset = 0) => DateTime.Today.AddDays(offset).ToString("dd/MM/yyyy", India);
        var s1 = Add("site",("name","Chakan Manufacturing Plant"),("client","Sample Industries"),("address","MIDC, Chakan"),("region","Pune"),("status","Attention"),("visit",Today(1)),("contact","Sample site manager"));
        var s2 = Add("site",("name","Hinjawadi Office Campus"),("client","Sample Technologies"),("address","Phase 1, Hinjawadi"),("region","Pune"),("status","Healthy"),("visit",Today(3)));
        var s3 = Add("site",("name","Nashik Cold Store"),("client","Sample Foods"),("address","Ambad Industrial Area"),("region","Nashik"),("status","Critical"),("visit",Today()));
        var people = new List<DemoRecord>();
        string[] names = ["Ravi Patil", "Sneha Kulkarni", "Amit Shinde", "Pooja Deshmukh"];
        for (int i = 0; i < names.Length; i++) people.Add(Add("employee",("number",$"EMP-{i+1:000}"),("name",names[i]),("designation",i == 1 ? "Service supervisor" : "HVAC technician"),("department","Field service"),("site",(i == 2 ? s3 : s1).Id.ToString()),("date",Today(-180)),("status",i == 3 ? "Leave" : "Active"),("blood","Not provided"),("rehire","No")));
        var jobs = new List<DemoRecord>();
        string[] titles = ["AHU cooling failure", "Monthly AC preventive service", "Cold room temperature alarm", "Replace return-air filters", "Inspect compressor vibration"];
        for (int i = 0; i < titles.Length; i++) jobs.Add(Add("job",("number",$"JOB-{1042+i}"),("name",titles[i]),("site",(i == 2 ? s3 : i == 1 ? s2 : s1).Id.ToString()),("employee",people[i%3].Id.ToString()),("type",i == 1 ? "Preventive maintenance" : "Breakdown"),("priority",i == 0 || i == 2 ? "Urgent" : "Normal"),("status",i == 0 ? "In progress" : "Assigned"),("date",Today(i < 3 ? 0 : 1)),("time",$"{9+i:00}:00"),("fault",titles[i] + " — sample service request.")));
        var m1 = Add("material",("number","MAT-001"),("name","CT-440 Contactor"),("category","Electrical"),("unit","Nos"),("stock","4"),("rate","1450"),("reorder","5"),("vendor","Sample Electrical Supply"),("godown","Pune main godown"));
        var m2 = Add("material",("number","MAT-002"),("name","AHU return-air filter"),("category","Filters"),("unit","Nos"),("stock","18"),("rate","650"),("reorder","10"),("vendor","Sample HVAC Traders"),("godown","Pune main godown"));
        Add("material",("number","MAT-003"),("name","R-410A Refrigerant"),("category","Refrigerant"),("unit","Kg"),("stock","2"),("rate","980"),("reorder","8"),("vendor","Sample Cooling Supply"),("godown","Van stock"));
        Add("equipment",("number","AHU-04"),("name","Air handling unit 04"),("site",s1.Id.ToString()),("make","Sample HVAC · 20 TR"),("serial","DEMO-AHU-004"),("status","Needs service"),("date",Today(1)));
        Add("equipment",("number","CR-02"),("name","Cold room condensing unit"),("site",s3.Id.ToString()),("make","Sample Cooling · 8 TR"),("serial","DEMO-CR-002"),("status","Breakdown"),("date",Today()));
        Add("issue",("number","INC-0091"),("name","Production AHU stopped"),("site",s1.Id.ToString()),("employee",people[0].Id.ToString()),("priority","Urgent"),("status","In progress"),("date",Today()),("notes","Investigate the contactor and record supply voltage."));
        Add("dispatch",("number","DSP-001"),("job",jobs[0].Id.ToString()),("employee",people[0].Id.ToString()),("date",Today()),("time","09:00"),("status","On site"));
        for (int i = 0; i < people.Count; i++) Add("attendance",("employee",people[i].Id.ToString()),("site",s1.Id.ToString()),("date",Today()),("status",i == 3 ? "Leave" : "Present"),("in",i == 3 ? "" : "09:00"),("hours","0"));
        Add("leave",("employee",people[3].Id.ToString()),("date",Today()),("end",Today(1)),("type","Casual"),("status","Approved"),("notes","Sample personal leave."));
        Add("report",("number","RPT-001"),("job",jobs[0].Id.ToString()),("diagnosis","Contactor coil fault"),("work","Isolated supply and checked connections"),("tests","Supply voltage recorded; replacement pending"),("notes","Sample report for mobile testing."));
        Add("checklist",("name","Supply voltage checked"),("job",jobs[0].Id.ToString()),("outcome","Pass"),("reading","415 V"));
        Add("jobpart",("number","PART-001"),("job",jobs[0].Id.ToString()),("item",m1.Id.ToString()),("quantity","1"));
        Add("rate",("item",m1.Id.ToString()),("name","Sample Electrical Supply"),("rate","1450"),("quantity","1"),("date",Today(30)),("preferred","Yes"));
        Add("rate",("item",m1.Id.ToString()),("name","Sample HVAC Traders"),("rate","1520"),("quantity","2"),("date",Today(14)),("preferred","No"));
        Add("request",("number","PR-001"),("item",m2.Id.ToString()),("site",s1.Id.ToString()),("quantity","12"),("date",Today(2)),("status","Requested"),("notes","Preventive maintenance filter replacement."));
        Add("skill",("employee",people[0].Id.ToString()),("name","Electrical safety"),("issuer","Sample training centre"),("level","Advanced"),("date",Today(90)));
        Add("document",("employee",people[0].Id.ToString()),("name","Training certificate"),("type","Qualification"),("reference","DEMO-CERT-001"),("date",Today(90)));
        Add("pay",("employee",people[0].Id.ToString()),("basic","22000"),("hra","5000"),("allowance","2500"),("deduction","1800"),("date",Today(-30)),("status","Reviewed"),("notes","Sample pay profile, not a payroll transaction."));
        return records;
    }
}

public sealed class DemoRecordValidator : AbstractValidator<DemoRecord>
{
    public DemoRecordValidator(DemoWorkspace store)
    {
        RuleFor(r => r).Custom((record, context) =>
        {
            var form = DemoWorkspace.Forms.SingleOrDefault(f => f.Key == record.Form);
            if (form is null) { context.AddFailure("Choose a supported form."); return; }
            foreach (var field in form.Fields)
            {
                var value = record.Get(field.Key).Trim();
                if (field.Required && value.Length == 0) { context.AddFailure(field.Label + " is required."); continue; }
                if (value.Length == 0) continue;
                if (value.Length > (field.Kind == "notes" ? 8000 : 250)) context.AddFailure(field.Label + " is too long.");
                if (field.Kind == "number" && (!decimal.TryParse(value, NumberStyles.Number, DemoWorkspace.India, out var n) || n < 0 || n > 999999999 || ((field.Key == "quantity") && n <= 0))) context.AddFailure("Enter a valid " + field.Label + (field.Key == "quantity" ? " greater than zero." : " (zero or greater)."));
                if (field.Kind == "date" && !DateTime.TryParseExact(value,"dd/MM/yyyy",DemoWorkspace.India,DateTimeStyles.None,out _)) context.AddFailure("Use DD/MM/YYYY for " + field.Label + ".");
                if (field.Kind == "choice" && !field.Choices!.Contains(value)) context.AddFailure("Choose a valid " + field.Label + ".");
                if (field.Kind == "relation" && !store.Records(field.Relation!).Any(r => r.Id.ToString() == value)) context.AddFailure("Choose an existing " + field.Label + ".");
                if (field.Kind == "phone" && (new string(value.Where(char.IsDigit).ToArray()).Length is < 10 or > 13 || value.Any(c => !char.IsDigit(c) && c is not ('+' or ' ' or '-')))) context.AddFailure("Enter a valid " + field.Label + ".");
                if ((field.Key is "time" or "in" or "out") && !TimeOnly.TryParseExact(value,"HH:mm",CultureInfo.InvariantCulture,DateTimeStyles.None,out _)) context.AddFailure("Use HH:mm for " + field.Label + ".");
            }
            if (record.Get("number").Length > 0 && store.Records(record.Form).Any(r => r.Id != record.Id && r.Get("number").Equals(record.Get("number"),StringComparison.OrdinalIgnoreCase))) context.AddFailure("This reference already exists. Use a unique code or number.");
            if (record.Form is "attendance" or "pay" && store.Records(record.Form).Any(r => r.Id != record.Id && r.Get("employee") == record.Get("employee") && r.Get("date") == record.Get("date"))) context.AddFailure("An entry for this employee and date already exists. Edit the existing entry.");
            if (record.Form == "leave" && DateTime.TryParseExact(record.Get("date"),"dd/MM/yyyy",DemoWorkspace.India,DateTimeStyles.None,out var start) && DateTime.TryParseExact(record.Get("end"),"dd/MM/yyyy",DemoWorkspace.India,DateTimeStyles.None,out var end) && end < start) context.AddFailure("To date cannot be earlier than From date.");
            if (record.Form == "attendance" && TimeOnly.TryParse(record.Get("in"),out var checkIn) && TimeOnly.TryParse(record.Get("out"),out var checkOut) && checkOut <= checkIn) context.AddFailure("Check-out must be later than check-in for this daily entry.");
            if (record.Get("aadhaar").Length > 0 && (record.Get("aadhaar").Length != 12 || !record.Get("aadhaar").All(char.IsDigit))) context.AddFailure("Aadhaar must contain 12 digits.");
            if (record.Get("pan").Length > 0 && !System.Text.RegularExpressions.Regex.IsMatch(record.Get("pan"),"^[A-Z]{5}[0-9]{4}[A-Z]$")) context.AddFailure("Enter a valid PAN in uppercase.");
        });
    }
}
