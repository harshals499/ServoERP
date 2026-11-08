using System; using System.IO; using System.Reflection; using System.Drawing; using System.Windows.Forms; using HVAC_Pro_Desktop.Services; using HVAC_Pro_Desktop.Models; using HVAC_Pro_Desktop.UI;
class BannerTest {
 class Preview : MainForm {protected override void OnShown(EventArgs e){} protected override void OnActivated(EventArgs e){} protected override void OnFormClosing(FormClosingEventArgs e){} }
 static int count;
 static void Check(bool ok,string text){if(!ok)throw new Exception(text);Console.WriteLine("PASS "+text);count++;}
 static void Capture(Form f,string path){Application.DoEvents();using(var b=new Bitmap(f.ClientSize.Width,f.ClientSize.Height)){f.DrawToBitmap(b,new Rectangle(Point.Empty,b.Size));b.Save(path);}}
 static object Field(object o,string name){return typeof(MainForm).GetField(name,BindingFlags.NonPublic|BindingFlags.Instance).GetValue(o);}
 public static int Run(string[] args){try{
 if (!args[2].StartsWith("ServoERP_MarathiQA_",StringComparison.Ordinal)) throw new Exception("Explicit disposable QA database required");
 var app=typeof(MainForm).Assembly;app.GetType("HVAC_Pro_Desktop.DAL.DatabaseConnectionFactory").GetProperty("DiagnosticConnectionString",BindingFlags.NonPublic|BindingFlags.Static).SetValue(null,new System.Data.SqlClient.SqlConnectionStringBuilder { DataSource = @".\SQLEXPRESS", InitialCatalog = args[2], IntegratedSecurity = true, ConnectTimeout = 5 }.ConnectionString,null);
 Application.EnableVisualStyles();SessionManager.SetSession(new AppUserDto{UserId=1,Username="banner-qa",DisplayName="Banner QA",RoleName="Admin",IsActive=true});
 var refresh=typeof(MainForm).GetMethod("RefreshDatabaseStatusBanner",BindingFlags.NonPublic|BindingFlags.Instance);
 foreach(var language in new[]{LanguageManager.English,LanguageManager.Marathi}) { LanguageManager.SetLanguage(language,false);
 using(var f=new Preview{WindowState=FormWindowState.Normal,ClientSize=new Size(1440,900),Location=new Point(-2500,-2500),StartPosition=FormStartPosition.Manual,ShowInTaskbar=false}){
 f.Show();var snapshot=new DatabaseConnectionStateSnapshot{State=DatabaseConnectionStateKind.Online,BusinessWritesAllowed=true};refresh.Invoke(f,new object[]{snapshot});LanguageManager.ApplyControlTree(f);Application.DoEvents();var panel=(Panel)Field(f,"_pnlDatabaseStatusBanner");var button=(Button)panel.Controls["HideDatabaseStatusBanner"];
 Check(panel.Visible,"initial notice visible / "+language);Check(button.Text==LanguageManager.Get("Hide")&&button.TabStop,"localized keyboard-accessible Hide / "+language);Check(TextRenderer.MeasureText(button.Text,button.Font).Width<button.Width,"Hide caption fits / "+language);
 var content=(Panel)Field(f,"_content");int originalTop=content.Top;
 Capture(f,Path.Combine(args[0],"banner-visible-"+language+".png"));button.PerformClick();
 Check(content.Top==originalTop-panel.Height,"hidden banner returns page space / "+language);Check(!panel.Visible,"Hide click collapses banner / "+language);refresh.Invoke(f,new object[]{snapshot});Check(!panel.Visible,"same-status refresh stays hidden / "+language);Capture(f,Path.Combine(args[0],"banner-hidden-"+language+".png"));snapshot.State=DatabaseConnectionStateKind.Offline;snapshot.BusinessWritesAllowed=false;refresh.Invoke(f,new object[]{snapshot});Check(panel.Visible,"new offline status resurfaces / "+language);button.PerformClick();Check(!panel.Visible,"offline notice dismisses / "+language);snapshot.State=DatabaseConnectionStateKind.Online;snapshot.BusinessWritesAllowed=true;refresh.Invoke(f,new object[]{snapshot});Check(panel.Visible,"status recovery resurfaces notice / "+language);f.Close();}}
 using(var stream=app.GetManifestResourceStream("ServoERP.ReleaseNotes.Current.md"))using(var reader=new StreamReader(stream))Check(reader.ReadToEnd()==File.ReadAllText(Path.Combine(args[1],"RELEASE_NOTES_1.1.533.md")),"embedded notes exactly match versioned output");
 Console.WriteLine("PASS TOTAL "+count);return 0;}catch(Exception e){Console.WriteLine("FAIL "+e);return 1;}}
}
class Bootstrap{[STAThread]static int Main(string[] a){string build=Path.GetFullPath(a[1]);AppDomain.CurrentDomain.AssemblyResolve+=(s,e)=>{var n=new AssemblyName(e.Name);string p=Path.Combine(build,n.CultureName??"",n.Name+(n.Name=="HVAC_Pro_Desktop"?".exe":".dll"));return File.Exists(p)?Assembly.LoadFrom(p):null;};return BannerTest.Run(a);}}
