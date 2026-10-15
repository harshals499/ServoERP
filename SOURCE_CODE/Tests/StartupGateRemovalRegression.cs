using System;
using System.Collections.Generic;
using System.Data.SqlClient;
using System.IO;
using System.Reflection;
using System.Runtime.Serialization;

internal static class StartupGateRemovalRegression
{
    static int Main(string[] args)
    {
        string directory = Path.GetFullPath(args[0]);
        AppDomain.CurrentDomain.AssemblyResolve += (sender, e) => {
            string path = Path.Combine(directory, new AssemblyName(e.Name).Name + ".dll");
            return File.Exists(path) ? Assembly.LoadFrom(path) : null;
        };
        string database = "ServoERP_StartupGateQA_" + DateTime.UtcNow.ToString("yyyyMMddHHmmss") + "_" + Guid.NewGuid().ToString("N").Substring(0,8);
        if(args.Length > 1)
        {
            database=args[1];
            if(!System.Text.RegularExpressions.Regex.IsMatch(database,@"^ServoERP_StartupGateQA_[0-9]{14}_[a-f0-9]{8}$")) throw new Exception("Only an explicitly named disposable QA database can be reused");
        }
        string master = @"Server=.\SQLEXPRESS;Database=master;Integrated Security=True;TrustServerCertificate=True";
        string cs = master.Replace("Database=master", "Database=" + database);
        try
        {
            if(args.Length == 1)
            {
            string backup;
            using(var c = new SqlConnection(master.Replace("Database=master", "Database=HVAC_PRO")))
            {
                c.Open();
                backup = Convert.ToString(new SqlCommand("SELECT TOP(1) BackupPath FROM dbo.ServoSchemaMigrations WHERE MigrationId='0002_LegacySchema_1.1.513.0'",c).ExecuteScalar());
                if(string.IsNullOrWhiteSpace(backup)) throw new Exception("Existing recovery backup not found");
            }
            using(var c = new SqlConnection(master))
            {
                c.Open();
                string folder = Convert.ToString(new SqlCommand("SELECT CONVERT(nvarchar(4000),SERVERPROPERTY('InstanceDefaultDataPath'))",c).ExecuteScalar());
                var moves = new List<string>();
                var parameters = new List<SqlParameter>();
                using(var files = new SqlCommand("RESTORE FILELISTONLY FROM DISK=@Path",c))
                {
                    files.Parameters.AddWithValue("@Path",backup);
                    using(var reader=files.ExecuteReader())
                    {
                        int i=0;
                        while(reader.Read())
                        {
                            moves.Add("MOVE @Logical"+i+" TO @File"+i);
                            parameters.Add(new SqlParameter("@Logical"+i,Convert.ToString(reader["LogicalName"])));
                            parameters.Add(new SqlParameter("@File"+i,Path.Combine(folder,database+"_"+i+(Convert.ToString(reader["Type"])=="L"?".ldf":".mdf"))));
                            i++;
                        }
                    }
                }
                using(var restore=new SqlCommand("RESTORE DATABASE ["+database+"] FROM DISK=@Path WITH "+string.Join(",",moves),c))
                {
                    restore.CommandTimeout=600;restore.Parameters.AddWithValue("@Path",backup);
                    restore.Parameters.AddRange(parameters.ToArray());restore.ExecuteNonQuery();
                }
            }
            }
            Console.WriteLine("Disposable database retained: " + database);
            bool policyBefore;
            using(var c = new SqlConnection(cs)) {
                c.Open();
                Execute(c, "IF OBJECT_ID('dbo.ServoSchemaMigrations','U') IS NULL CREATE TABLE dbo.ServoSchemaMigrations(MigrationId nvarchar(180),ReleaseVersion nvarchar(40),ScriptHash varchar(64),Status varchar(20),BackupPath nvarchar(2000),StartedUtc datetime2); IF NOT EXISTS(SELECT 1 FROM dbo.ServoSchemaMigrations WHERE MigrationId='QA_Interrupted') INSERT dbo.ServoSchemaMigrations(MigrationId,ReleaseVersion,ScriptHash,Status,BackupPath,StartedUtc) VALUES('QA_Interrupted','1.1.513.0','invalid','Running','QA',SYSUTCDATETIME()),('QA_FutureRelease','99.0.0.0','invalid','Completed','QA',SYSUTCDATETIME())");
            }
            using(var c=new SqlConnection(cs)) { c.Open(); policyBefore=Convert.ToInt32(new SqlCommand("SELECT CASE WHEN OBJECT_ID('dbo.ServoDatabaseUpgradePolicy','U') IS NULL THEN 0 ELSE 1 END",c).ExecuteScalar())==1; }
            var assembly = Assembly.LoadFrom(Path.Combine(directory, "HVAC_Pro_Desktop.exe"));
            var service = assembly.GetType("HVAC_Pro_Desktop.Services.DatabaseUpgradeService", true);
            var run = service.GetMethod("Run", BindingFlags.Static | BindingFlags.NonPublic);
            var flag = service.GetProperty("IsLegacyUpgrade", BindingFlags.Static | BindingFlags.NonPublic);
            int calls = 0;
            Action callback = () => { if (!(bool)flag.GetValue(null)) throw new Exception("Maintenance context missing"); calls++; };
            var scripts = new Dictionary<string,string>();
            for(int i=0;i<2;i++) run.Invoke(null, new object[]{cs,"1.1.515.0",callback,scripts,false});
            if(calls != 2 || (bool)flag.GetValue(null)) throw new Exception("Repeat initialization/context reset failed");
            bool propagated = false;
            try { run.Invoke(null,new object[]{cs,"1.1.515.0",(Action)(() => { throw new InvalidOperationException("Injected schema error"); }),scripts,false}); }
            catch(TargetInvocationException e) { propagated = e.InnerException is InvalidOperationException; }
            if(!propagated || (bool)flag.GetValue(null)) throw new Exception("Real schema error swallowed or maintenance flag leaked");
            if(Array.Exists(assembly.GetManifestResourceNames(),n=>n.Contains("0003_SafeUpgradePolicy"))) throw new Exception("Retired policy embedded");
            var factory = assembly.GetType("HVAC_Pro_Desktop.DAL.DatabaseConnectionFactory",true);
            factory.GetProperty("DiagnosticConnectionString",BindingFlags.Static|BindingFlags.NonPublic).SetValue(null,cs);
            var managerType = assembly.GetType("HVAC_Pro_Desktop.DAL.DatabaseManager",true);
            var manager = FormatterServices.GetUninitializedObject(managerType);
            managerType.GetField("_connectionString",BindingFlags.Instance|BindingFlags.NonPublic).SetValue(manager,cs);
            var guards = managerType.GetMethod("RunSchemaGuards",BindingFlags.Instance|BindingFlags.NonPublic);
            guards.Invoke(manager,null);
            long audits;
            using(var c=new SqlConnection(cs)) { c.Open(); audits=Convert.ToInt64(new SqlCommand("SELECT COUNT_BIG(*) FROM dbo.FinancialRecordAudit",c).ExecuteScalar()); }
            guards.Invoke(manager,null);
            using(var c=new SqlConnection(cs)) { c.Open(); if(Convert.ToInt64(new SqlCommand("SELECT COUNT_BIG(*) FROM dbo.FinancialRecordAudit",c).ExecuteScalar())!=audits) throw new Exception("Schema maintenance replayed business audit history"); }
            using(var c=(SqlConnection)factory.GetMethod("CreateConnection",new[]{typeof(string)}).Invoke(null,new object[]{cs}))
            {
                c.Open();
                if(Convert.ToInt32(new SqlCommand("SELECT ISNULL(TRY_CONVERT(int,SESSION_CONTEXT(N'ServoERP.Maintenance')),0)",c).ExecuteScalar())!=0 || new SqlCommand("SELECT SESSION_CONTEXT(N'ServoERP.SkipAudit')",c).ExecuteScalar()!=DBNull.Value) throw new Exception("Maintenance bypass leaked into business connection");
            }
            using(var c = new SqlConnection(cs)) {
                c.Open();
                if(Convert.ToInt32(new SqlCommand("SELECT COUNT(*) FROM dbo.ServoSchemaMigrations WHERE MigrationId='QA_Interrupted' AND Status='Running'",c).ExecuteScalar())!=1) throw new Exception("Existing journal changed");
                if(Convert.ToInt32(new SqlCommand("SELECT CASE WHEN OBJECT_ID('dbo.TR_Invoices_FinancialProtection','TR') IS NOT NULL AND COL_LENGTH('dbo.Invoices','IsPosted') IS NOT NULL THEN 1 ELSE 0 END",c).ExecuteScalar())!=1) throw new Exception("Financial schema missing");
                if((Convert.ToInt32(new SqlCommand("SELECT CASE WHEN OBJECT_ID('dbo.ServoDatabaseUpgradePolicy','U') IS NULL THEN 0 ELSE 1 END",c).ExecuteScalar())==1)!=policyBefore) throw new Exception("Retired policy state changed");
            }
            Console.WriteLine("PASS: interrupted/future journal ignored and preserved; repeat complete schema initialization; financial protection retained; real errors propagate; maintenance context resets; no audit replay or business bypass; retired policy not embedded and existing policy preserved.");
            return 0;
        }
        catch(Exception e) { Console.WriteLine("FAIL: " + e); return 1; }
    }
    static void Execute(SqlConnection c,string sql) { using(var command=new SqlCommand(sql,c)) { command.CommandTimeout=120;command.ExecuteNonQuery(); } }
}
