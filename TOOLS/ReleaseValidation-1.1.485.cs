using System;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Data.SqlClient;
using OfficeOpenXml;
using System.Windows.Forms;
using System.Drawing;
using HVAC_Pro_Desktop.UI;
using HVAC_Pro_Desktop.DAL;
using HVAC_Pro_Desktop.Models;
using HVAC_Pro_Desktop.Services;
class ReleaseValidation
{
    static readonly string AppDir = @"C:\HVAC_PRO_MSE\SOURCE_CODE\bin\Release";
    static readonly string Output = @"C:\HVAC_PRO_MSE\release";
    [STAThread] static void Main()
    {
        AppDomain.CurrentDomain.AssemblyResolve += (s,e) => { string path = Path.Combine(AppDir,new AssemblyName(e.Name).Name+".dll"); return File.Exists(path) ? Assembly.LoadFrom(path) : null; };
        Run();
    }
    static void Run()
    {
        try
        {
            Application.EnableVisualStyles();
            var user = new AppUserDto { UserId=1, Username="codex-smoke", DisplayName="Codex Smoke Tester", RoleId=1, RoleName="Administrator", IsActive=true };
            foreach (var module in new[]{"Clients","Contracts","MasterData","Inventory","Jobs","Quotations","Invoices"}) user.Permissions[module]=new RolePermissionDto { ModuleKey=module, CanView=true, CanCreate=true, CanEdit=true, CanDelete=true };
            SessionManager.SetSession(user,null,null);
            var tests = HVAC_Pro_Desktop.Tests.SmartImportDuplicateDetectorSmokeTests.RunAll();
            tests.AddRange(HVAC_Pro_Desktop.Tests.ImportPreflightSmokeTests.RunAll());
            var contract = new ContractRepository().GetAll().First();
            VerifyImportGuards();
            tests.Add("PASS real Excel imports reject numeric/quotation-term clients and cross-client quotation-number overwrite; saved amount unchanged");
            VerifyDeleteRollback(contract.ContractID);
            tests.Add("PASS AMC linked-record transaction deletes equipment/visits and contract, then rolls back with original contract restored");
            var edit = new AddAMCForm(contract.ContractID);
            edit.Text = "AMC edit validation - 1.1.485";
            var review = new ClientImportRepairDialog();
            edit.Show(); review.Show();
            var timer = new Timer { Interval=12000 };
            timer.Tick += (s,e) => {
                timer.Stop();
                Capture(edit,"amc-edit-1.1.485.png"); Capture(review,"client-import-review-1.1.485.png");
                var grid=(DataGridView)typeof(ClientImportRepairDialog).GetField("_records",BindingFlags.NonPublic|BindingFlags.Instance).GetValue(review);
                grid.Rows.Add(-1,"1.1","Q-EXAMPLE-001 / 5,40,000.00");
                grid.Rows.Add(-2,"Quotation Is Valid Upto 07 Days","No linked quotations");
                review.Text="Client import review - synthetic visual fixture";
                Capture(review,"client-import-review-fixture-1.1.485.png");
                tests.Add("PASS Release WinForms edit and client review loaded and rendered; no permanent business-data changes committed");
                File.WriteAllLines(Path.Combine(Output,"validation-native-1.1.485.log"),tests);
                edit.Close(); review.Close(); Application.ExitThread();
            };
            timer.Start(); Application.Run();
        }
        catch(Exception ex) { File.WriteAllText(Path.Combine(Output,"validation-native-1.1.485.log"),ex.ToString()); }
    }
    static void VerifyImportGuards()
    {
        using(var connection = new DatabaseManager().GetConnection())
        {
            connection.Open();
            int before;
            using(var count = new SqlCommand("SELECT COUNT(*) FROM B2BClients",connection)) before=Convert.ToInt32(count.ExecuteScalar());
            string file = Path.Combine(Path.GetTempPath(),Guid.NewGuid()+".xlsx");
            try
            {
                using(var package=new ExcelPackage())
                {
                    var sheet=package.Workbook.Worksheets.Add("Clients"); sheet.Cells[1,1].Value="ClientName";
                    sheet.Cells[2,1].Value="1.1"; sheet.Cells[3,1].Value="Quotation Is Valid Upto 07 Days";
                    package.SaveAs(new FileInfo(file));
                }
                var result=new ExcelImportService().Import(ExcelImportModule.Clients,file);
                if(result.SuccessCount!=0 || result.Errors.Count<2) throw new Exception("Client artifact import was accepted");
                using(var count=new SqlCommand("SELECT COUNT(*) FROM B2BClients",connection)) if(Convert.ToInt32(count.ExecuteScalar())!=before) throw new Exception("Client guard created records");
            }
            finally { if(File.Exists(file)) File.Delete(file); }
            int bidId; string number; decimal amount; string otherClient;
            using(var command=new SqlCommand("SELECT TOP 1 q.BidID,q.QuotationNumber,ISNULL(q.BidValue,0) Amount,c.CompanyName OtherClient FROM Quotations q CROSS JOIN B2BClients c WHERE q.ClientID IS NOT NULL AND c.ClientID<>q.ClientID AND c.IsActive=1 AND NULLIF(q.QuotationNumber,'') IS NOT NULL AND (SELECT COUNT(*) FROM Quotations d WHERE d.QuotationNumber=q.QuotationNumber)=1 ORDER BY q.BidID",connection))
            using(var reader=command.ExecuteReader()) { if(!reader.Read()) throw new Exception("No existing quotation available to verify overwrite guard"); bidId=reader.GetInt32(0); number=reader.GetString(1); amount=reader.GetDecimal(2); otherClient=reader.GetString(3); }
            file=Path.Combine(Path.GetTempPath(),Guid.NewGuid()+".xlsx");
            try
            {
                using(var package=new ExcelPackage())
                {
                    var sheet=package.Workbook.Worksheets.Add("Quotations");
                    string[] headers={"QuotationNumber","ClientName","Description","Amount"};
                    object[] values={number,otherClient,"QA overwrite prevention",999999m};
                    for(int i=0;i<headers.Length;i++) { sheet.Cells[1,i+1].Value=headers[i]; sheet.Cells[2,i+1].Value=values[i]; }
                    package.SaveAs(new FileInfo(file));
                }
                var result=new ExcelImportService().Import(ExcelImportModule.Quotations,file);
                if(result.SuccessCount!=0 || !result.Errors.Any(e=>e.Contains("belongs to another client"))) throw new Exception("Cross-client quotation guard failed: "+string.Join("; ",result.Errors));
                using(var command=new SqlCommand("SELECT ISNULL(BidValue,0) FROM Quotations WHERE BidID=@id",connection)) { command.Parameters.AddWithValue("@id",bidId); if(Convert.ToDecimal(command.ExecuteScalar())!=amount) throw new Exception("Quotation amount changed"); }
            }
            finally { if(File.Exists(file)) File.Delete(file); }
        }
    }
    static void VerifyDeleteRollback(int id)
    {
        using(var connection = new DatabaseManager().GetConnection())
        {
            connection.Open();
            using(var tx=connection.BeginTransaction())
            {
                try
                {
                    using(var add=new SqlCommand("INSERT INTO AMCEquipment(AMCID,EquipmentName) VALUES(@id,'QA rollback equipment'); INSERT INTO AMCVisits(AMCID,VisitNumber,ScheduledDate) VALUES(@id,99999,GETDATE());",connection,tx)) { add.Parameters.AddWithValue("@id",id); add.ExecuteNonQuery(); }
                    typeof(ContractRepository).GetMethod("DeleteLinkedRecords", BindingFlags.NonPublic|BindingFlags.Static).Invoke(null,new object[]{connection,tx,id});
                    using(var check=new SqlCommand("SELECT (SELECT COUNT(*) FROM AMCContracts WHERE ContractID=@id)+(SELECT COUNT(*) FROM AMCEquipment WHERE AMCID=@id)+(SELECT COUNT(*) FROM AMCVisits WHERE AMCID=@id)",connection,tx))
                    {
                        check.Parameters.AddWithValue("@id",id);
                        if(Convert.ToInt32(check.ExecuteScalar())!=0) throw new Exception("AMC child deletion left rows behind");
                    }
                }
                finally { tx.Rollback(); }
            }
            using(var check=new SqlCommand("SELECT COUNT(*) FROM AMCContracts WHERE ContractID=@id",connection)) { check.Parameters.AddWithValue("@id",id); if(Convert.ToInt32(check.ExecuteScalar())!=1) throw new Exception("Rollback did not restore AMC"); }
        }
    }
    static void Capture(Form form,string name) { using(var bitmap=new Bitmap(form.Width,form.Height)) { form.DrawToBitmap(bitmap,new Rectangle(0,0,form.Width,form.Height)); bitmap.Save(Path.Combine(Output,name)); } }
}
