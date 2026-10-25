using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using System.Windows.Forms;
using HVAC_Pro_Desktop.DAL;
using HVAC_Pro_Desktop.Models;
using HVAC_Pro_Desktop.Services;
using HVAC_Pro_Desktop.UI;

internal static class OfficeSyncDesktopChecks
{
    private static readonly BindingFlags Hidden = BindingFlags.Instance | BindingFlags.NonPublic;
    private static void Require(bool test, string detail) { if (!test) throw new Exception(detail); Console.WriteLine("PASS " + detail); }
    private static void Pump(Task task) { var deadline = DateTime.UtcNow.AddSeconds(60); while (!task.IsCompleted) { if(DateTime.UtcNow>deadline) throw new TimeoutException("Native QA async operation exceeded 60 seconds."); Application.DoEvents(); System.Threading.Thread.Sleep(10); } task.GetAwaiter().GetResult(); }
    private static IEnumerable<Control> Children(Control parent) { foreach (Control child in parent.Controls) { yield return child; foreach (Control nested in Children(child)) yield return nested; } }
    private sealed class QaWindow : Form { protected override bool ShowWithoutActivation { get { return true; } } }
    [STAThread]
    private static int Main(string[] args)
    {
        try
        {
            if (args.Length != 6 || !System.Text.RegularExpressions.Regex.IsMatch(args[0], "^EngineerTests_[a-f0-9]{32}$")) throw new Exception("Disposable database and three record IDs, mode and output folder required.");
            int clientId = int.Parse(args[1]), employeeId = int.Parse(args[2]), jobId = int.Parse(args[3]); string mode = args[4], output = Path.GetFullPath(args[5]); Directory.CreateDirectory(output);
            Application.SetUnhandledExceptionMode(UnhandledExceptionMode.ThrowException);
            var assembly = typeof(DatabaseManager).Assembly;
            var factory = assembly.GetType("HVAC_Pro_Desktop.DAL.DatabaseConnectionFactory", true);
            factory.GetProperty("DiagnosticConnectionString", BindingFlags.Static | BindingFlags.NonPublic).SetValue(null, "Server=.\\SQLEXPRESS;Database=" + args[0] + ";Integrated Security=True;TrustServerCertificate=True", null);
            SessionManager.SetSession(new AppUserDto { UserId = 1, Username = "synthetic-sync-qa", DisplayName = "Synthetic sync QA", RoleId = 1, RoleName = "Admin", IsActive = true, Permissions = new Dictionary<string, RolePermissionDto> { { "Clients", new RolePermissionDto { CanView=true,CanCreate=true,CanEdit=true } }, { "Employees", new RolePermissionDto { CanView=true,CanCreate=true,CanEdit=true } }, { "WorkOrders", new RolePermissionDto { CanView=true,CanCreate=true,CanEdit=true } } } });
            var clients = new ClientRepository(); var employees = new EmployeeRepository(); var jobs = new JobRepository();
            var client = clients.GetById(clientId); var employee = employees.GetById(employeeId); var job = jobs.GetById(jobId);
            Require(client != null && client.CompanyName.StartsWith("Sync QA Client "), "mobile-created client is readable through desktop ClientRepository");
            Require(employee != null && employee.Name.StartsWith("Sync QA Employee "), "mobile-created employee is readable through desktop EmployeeRepository");
            Require(job != null && job.Title.StartsWith("Sync QA Job "), "mobile-created job is readable through desktop JobRepository");
            if (mode == "edit")
            {
                client.CompanyName += " desktop"; clients.Update(client);
                employee.Name += " desktop"; employees.Update(employee);
                job.Title += " desktop"; job.JobTitle = job.Title; jobs.Update(job);
                Require(clients.GetById(clientId).CompanyName.EndsWith(" desktop"), "desktop client edit persists to the shared record");
                Require(employees.GetById(employeeId).Name.EndsWith(" desktop"), "desktop employee edit persists to the shared record");
                Require(jobs.GetById(jobId).Title.EndsWith(" desktop"), "desktop job edit persists to the shared record");
            }
            else if (mode == "verify")
            {
                Require(client.CompanyName.EndsWith(" desktop mobile") && employee.Name.EndsWith(" desktop mobile") && job.Title.EndsWith(" desktop mobile"), "all three mobile re-edits return through desktop repositories");
                TestRefreshGate(assembly);
                System.Threading.SynchronizationContext.SetSynchronizationContext(new WindowsFormsSynchronizationContext());
                RenderPage(new ClientManagementForm(), "RefreshOfficeClientsAsync", "Clients", output);
                System.Threading.SynchronizationContext.SetSynchronizationContext(new WindowsFormsSynchronizationContext());
                RenderPage(new EmployeeForm(), "RefreshOfficeEmployeesAsync", "Employees", output);
                System.Threading.SynchronizationContext.SetSynchronizationContext(new WindowsFormsSynchronizationContext());
                RenderPage(new JobManagementForm(), "RefreshOfficeJobsAsync", "Jobs", output);
            }
            else if (mode == "lookup")
            {
                string stamp = DateTime.Now.ToString("HHmmss");
                var newClient = new B2BClient { CompanyName="Desktop live client "+stamp, Phone="9999999999", CustomerSince=DateTime.Today, IsActive=true, SyncPublicId=Guid.NewGuid() };
                int newClientId=clients.Create(newClient);
                var newEmployee = new Employee { EmployeeCode="SYNC"+stamp, Name="Desktop live employee "+stamp, JoiningDate=DateTime.Today, DateOfJoining=DateTime.Today, Status="Active" };
                int newEmployeeId=employees.Create(newEmployee);
                var newJob = new Job { JobNumber="SYNC-"+stamp, Title="Desktop live job "+stamp, JobTitle="Desktop live job "+stamp, ClientID=newClientId, AssignedEmployeeID=1, ScheduledDate=DateTime.Today.AddHours(10), JobType="General", Priority="Medium", Status="Pending", PipelineStatus="Assigned", SyncPublicId=Guid.NewGuid(), SyncVersion=1 };
                int newJobId=jobs.Create(newJob);
                File.WriteAllLines(Path.Combine(output,"desktop-live-lookups.txt"),new[]{newClient.CompanyName,newEmployee.Name,newJob.Title,newClientId.ToString(),newEmployeeId.ToString(),newJobId.ToString()});
                Require(newClientId>0&&newEmployeeId>0&&newJobId>0,"desktop repositories created new reference records for live mobile refresh");
            }
            else throw new Exception("Unknown test mode.");
            File.WriteAllText(Path.Combine(output, "desktop-" + mode + "-passed.txt"), DateTime.UtcNow.ToString("o") + "\n" + args[0]);
            return 0;
        }
        catch (Exception ex) { Console.Error.WriteLine(ex); return 1; }
    }
    private static void TestRefreshGate(Assembly assembly)
    {
        using (var panel = new Panel())
        {
            panel.CreateControl(); int calls = 0; var release = new TaskCompletionSource<bool>(); bool allowed = true;
            var type = assembly.GetType("HVAC_Pro_Desktop.Services.LiveOfficeDataRefresh", true);
            var attach = type.GetMethod("Attach", BindingFlags.NonPublic | BindingFlags.Static);
            var subscription = (IDisposable)attach.Invoke(null, new object[] { panel, new Func<bool>(() => allowed), new Func<Task>(() => { calls++; return release.Task; }) });
            var run = subscription.GetType().GetMethod("RunAsync", Hidden);
            var first = (Task)run.Invoke(subscription, null); var second = (Task)run.Invoke(subscription, null);
            Require(calls == 1 && second.IsCompleted, "automatic desktop refresh never overlaps an in-flight read");
            release.SetResult(true); Pump(first); allowed = false; Pump((Task)run.Invoke(subscription,null)); Require(calls == 1, "editor guard prevents an automatic refresh");
            subscription.Dispose(); allowed = true; Pump((Task)run.Invoke(subscription,null)); Require(calls == 1, "disposed refresh subscription stops requesting data");
        }
    }
    private static void RenderPage(Control page, string refresh, string name, string output)
    {
        using (var form = new QaWindow { Text = "ServoERP synthetic shared-data QA", ShowInTaskbar = false, StartPosition = FormStartPosition.Manual, Location = new Point(-20000,-20000), ClientSize = new Size(1440,900) })
        using (page)
        {
            Console.WriteLine("STAGE render "+name); page.Dock = DockStyle.Fill; form.Controls.Add(page); form.Show();
            var until = DateTime.UtcNow.AddSeconds(3); while (DateTime.UtcNow < until) { Application.DoEvents(); System.Threading.Thread.Sleep(10); }
            System.Threading.SynchronizationContext.SetSynchronizationContext(new WindowsFormsSynchronizationContext());
            Console.WriteLine("STAGE refresh "+name); Pump((Task)page.GetType().GetMethod(refresh,Hidden).Invoke(page,null)); Application.DoEvents();
            string dataField = name == "Clients" ? "_clients" : name == "Employees" ? "_employeeSummaries" : "_allJobs";
            var records = (System.Collections.IEnumerable)page.GetType().GetField(dataField,Hidden).GetValue(page);
            bool found = false;
            foreach (var row in records) foreach (var property in row.GetType().GetProperties()) if (property.PropertyType == typeof(string)) { var text = property.GetValue(row,null) as string; if (text != null && text.Contains("Sync QA") && text.EndsWith(" desktop mobile")) found = true; }
            Require(found, name + " native list binds the shared round-trip record");
            string searchName = name == "Clients" ? "_dashboardTableSearch" : name == "Employees" ? "_txtDashboardSearch" : "_dashboardSearch";
            var searchField = page.GetType().GetField(searchName,Hidden); var search = searchField == null ? null : searchField.GetValue(page) as TextBox;
            if (search != null) search.Text = "Sync QA";
            until = DateTime.UtcNow.AddSeconds(1); while (DateTime.UtcNow < until) { Application.DoEvents(); System.Threading.Thread.Sleep(10); }
            using (var bitmap = new Bitmap(form.Width,form.Height)) { form.DrawToBitmap(bitmap,new Rectangle(0,0,bitmap.Width,bitmap.Height)); bitmap.Save(Path.Combine(output,"desktop-"+name+".png")); }
            Require(Children(page).Any(), name + " native desktop refresh rendered without replacing an editor"); form.Close();
        }
    }
}
