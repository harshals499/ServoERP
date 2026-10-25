using System;
using System.Data.SqlClient;
using System.IO;
using System.Reflection;
using Dapper;
using HVAC_Pro_Desktop.DAL;
using HVAC_Pro_Desktop.Models;
internal static class LegacyClientCompatibilityChecks
{
 [STAThread] private static int Main(string[] args)
 {
  try {
   if(args.Length!=3 || !System.Text.RegularExpressions.Regex.IsMatch(args[0],"^EngineerTests_[a-f0-9]{32}$"))throw new Exception("Explicit disposable legacy database required.");
   string connection="Server=.\\SQLEXPRESS;Database="+args[0]+";Integrated Security=True;TrustServerCertificate=True";
   typeof(DatabaseManager).Assembly.GetType("HVAC_Pro_Desktop.DAL.DatabaseConnectionFactory",true).GetProperty("DiagnosticConnectionString",BindingFlags.NonPublic|BindingFlags.Static).SetValue(null,connection,null);
   using(var db=new SqlConnection(connection)) {db.Open();if(db.ExecuteScalar<int>("SELECT CASE WHEN COL_LENGTH('dbo.B2BClients','CompanyId') IS NULL THEN 0 ELSE 1 END")!=0)throw new Exception("Legacy fixture must omit the optional company column.");}
   int id;
   try {id=new ClientRepository().Create(new B2BClient{CompanyName="Disposable legacy client QA",Phone="9999999999",CustomerSince=DateTime.Today,IsActive=true,SyncPublicId=Guid.NewGuid()});}
   catch(SqlException e) {if(args[1]=="old"&&e.Number==207){File.WriteAllText(args[2],"PASS reproduced old 1.1.523 missing optional CompanyId failure in disposable database.");return 0;}throw;}
   if(args[1]=="old")throw new Exception("The expected regression did not reproduce.");
   using(var db=new SqlConnection(connection)){db.Open();if(db.QuerySingle<string>("SELECT CompanyName FROM B2BClients WHERE ClientID=@id",new{id})!="Disposable legacy client QA")throw new Exception("Legacy client save did not persist.");}
   File.WriteAllText(args[2],"PASS fixed client creation in a desktop-only disposable schema without optional CompanyId; actual desktop repository, parameterized save and shared row verified.");return 0;
  }catch(Exception e){File.WriteAllText(args[2],e.ToString());return 1;}
 }
}
