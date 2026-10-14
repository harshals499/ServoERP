param([string]$OutputDir = 'C:\HVAC_PRO_MSE\TEST_RESULTS\settings-1.1.504')
$ErrorActionPreference = 'Stop'
Add-Type -AssemblyName System.Windows.Forms,System.Drawing
$release = Join-Path (Split-Path $PSScriptRoot -Parent) 'SOURCE_CODE\bin\Release'
[Environment]::CurrentDirectory = $release
$assembly = [Reflection.Assembly]::LoadFrom((Join-Path $release 'HVAC_Pro_Desktop.exe'))
$taskSource = @'
using System;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Collections.Generic;
using System.Drawing;
using System.Windows.Forms;
using HVAC_Pro_Desktop.UI;
using HVAC_Pro_Desktop.Models;
using HVAC_Pro_Desktop.Services;
public static class SettingsDirectoryVerification
{
    [STAThread] public static void Main(string[] args) { Run(args[0]); }
    static IEnumerable<Control> Tree(Control c) { yield return c; foreach(Control child in c.Controls) foreach(Control descendant in Tree(child)) yield return descendant; }
    static void Assert(bool condition, string message) { if(!condition) throw new Exception(message); }
    static void Capture(Form form, string path) { Application.DoEvents(); using(var image = new Bitmap(form.ClientSize.Width, form.ClientSize.Height)) { form.DrawToBitmap(image, new Rectangle(Point.Empty,image.Size)); image.Save(path); } }
    public static void Run(string output)
    {
        Directory.CreateDirectory(output);
        Application.EnableVisualStyles();
        var report = new List<string>();
        foreach(bool admin in new[] {true, false})
        {
            SessionManager.SetSession(new AppUserDto { UserId=1, Username="settings-test", DisplayName="Settings test", RoleId=admin ? 1 : 2, RoleName=admin ? "Admin" : "Staff", IsActive=true });
            using(var host = new Form { Text="ServoERP Settings verification", Size=new Size(1440,900) })
            using(var page = new SettingsForm { Dock=DockStyle.Fill })
            {
                host.Controls.Add(page);
                typeof(SettingsForm).GetMethod("LoadPreviewForVisualTest", BindingFlags.Instance | BindingFlags.NonPublic).Invoke(page, null);
                host.Show(); Application.DoEvents();
                UIHelper.ApplyButtonAlignment(page);
                typeof(SettingsForm).Assembly.GetType("HVAC_Pro_Desktop.UI.GlobalCardContextMenu").GetMethod("ApplyToTree").Invoke(null, new object[] { page });
                typeof(SettingsForm).GetMethod("ReflowSettingsCards", BindingFlags.Instance | BindingFlags.NonPublic).Invoke(page, null);
                var links=Tree(page).OfType<Button>().Where(b=>b.Name.StartsWith("SettingsLink_")).ToArray();
                Assert(links.Length==(admin ? 13 : 9), "Incorrect directory permission count: "+links.Length);
                if(!admin) Assert(!links.Any(b=>b.Text.StartsWith("Users &") || b.Text.StartsWith("Audit log") || b.Text.StartsWith("Backup &") || b.Text.StartsWith("Data management")), "Admin entry exposed");
                Capture(host, Path.Combine(output, admin ? "directory-admin.png" : "directory-staff.png"));
                var search=Tree(page).OfType<TextBox>().Single(c=>c.Name=="SettingsDirectorySearch");
                search.Text="zz-no-result"; Application.DoEvents();
                Assert(!links.Any(b=>b.Visible), "Search did not hide unmatched links");
                Assert(Tree(page).Single(c=>c.Name=="SettingsSearchEmpty").Visible, "Missing empty search message");
                search.Text="GST"; Application.DoEvents();
                Assert(links.Count(b=>b.Visible)==1, "GST search did not filter directory");
                Capture(host, Path.Combine(output, admin ? "search-admin.png" : "search-staff.png"));
                search.Text="";
                var tabs=page.Controls.OfType<TabControl>().Single();
                var company=(TextBox)typeof(SettingsForm).GetField("_txtCompanyName", BindingFlags.Instance|BindingFlags.NonPublic).GetValue(page);
                company.Text="Unsaved navigation verification";
                foreach(var link in links)
                {
                    tabs.SelectedIndex=0; Application.DoEvents();
                    link.PerformClick(); Application.DoEvents();
                    if(link.Text.StartsWith("Users &")) Assert(tabs.SelectedTab.Text=="Users & Logins", "Users route failed");
                    else if(link.Text.StartsWith("Audit log")) Assert(tabs.SelectedTab.Text=="Audit Log", "Audit route failed");
                    else
                    {
                        var directory=Tree(page).Single(c=>c.Name=="SettingsDirectory");
                        Assert(!directory.Visible, "Directory still visible on editor route: "+link.Text);
                        Assert(Tree(page).OfType<Panel>().Any(c=>Equals(c.Tag,"settings-card") && c.Visible), "No editor shown: "+link.Text);
                        Capture(host, Path.Combine(output, (admin ? "admin-" : "staff-")+link.Name.Replace("/", "-")+".png"));
                        Tree(page).OfType<Button>().Single(b=>b.Name=="SettingsBack").PerformClick();
                        Assert(directory.Visible,"Back navigation failed");
                    }
                    Assert(company.Text=="Unsaved navigation verification", "Navigation discarded unsaved input");
                    report.Add("PASS "+(admin ? "admin " : "staff ")+link.Text);
                }
                tabs.SelectedIndex=0;
                foreach(int width in new[] {1920, 1440, 1280, 1024, 820, 640})
                {
                    host.Size=new Size(width,900); Application.DoEvents();
                    UIHelper.ApplyButtonAlignment(page);
                    typeof(SettingsForm).Assembly.GetType("HVAC_Pro_Desktop.UI.GlobalCardContextMenu").GetMethod("ApplyToTree").Invoke(null, new object[] { page });
                    typeof(SettingsForm).GetMethod("ReflowSettingsCards", BindingFlags.Instance | BindingFlags.NonPublic).Invoke(page, null);
                    Application.DoEvents();
                    var directory=Tree(page).Single(c=>c.Name=="SettingsDirectory");
                    Assert(!Tree(directory).Any(c=>c.Name.IndexOf("ResizeGrip",StringComparison.OrdinalIgnoreCase)>=0),"Directory has resize grips");
                    foreach(var link in links.Where(b=>b.Visible))
                    {
                        Assert(link.Location==Point.Empty && link.Size==link.Parent.ClientSize,"Full-row target was moved at "+width+": "+link.Text+" bounds "+link.Bounds+" parent "+link.Parent.ClientSize+" padding "+link.Parent.Padding);
                        using(var font=new Font("Segoe UI",9f))
                        {
                            var required=TextRenderer.MeasureText(link.AccessibleDescription,font,new Size(link.Width-84,1000),TextFormatFlags.WordBreak|TextFormatFlags.NoPrefix);
                            Assert(required.Height<=link.Height-46,"Description clipped at width "+width+": "+link.Text);
                        }
                    }
                    Capture(host,Path.Combine(output,(admin ? "admin-" : "staff-")+width+".png"));
                    report.Add("PASS full-row alignment, wrapped descriptions and fixed sections at "+width+": "+(admin ? "admin" : "staff"));
                }
                report.Add("PASS search, unsaved input retention, permissions and compact rendering: "+(admin ? "admin" : "staff"));
                host.Close();
            }
        }
        SessionManager.ClearSession();
        File.WriteAllLines(Path.Combine(output,"verification.txt"),report);
    }
}
'@
New-Item -ItemType Directory -Force -Path $OutputDir | Out-Null
$taskSourcePath = Join-Path $OutputDir 'SettingsDirectoryVerification.cs'
[IO.File]::WriteAllText($taskSourcePath, $taskSource)
$taskHarness = Join-Path $release 'SettingsDirectoryVerification.exe'
& "$env:WINDIR\Microsoft.NET\Framework64\v4.0.30319\csc.exe" /nologo /target:exe "/out:$taskHarness" "/reference:$(Join-Path $release 'HVAC_Pro_Desktop.exe')" /reference:System.Windows.Forms.dll /reference:System.Drawing.dll /reference:System.Core.dll $taskSourcePath
if ($LASTEXITCODE -ne 0) { throw 'Settings verification compile failed' }
Copy-Item -LiteralPath (Join-Path $release 'HVAC_Pro_Desktop.exe.config') -Destination ($taskHarness + '.config') -Force
try {
    & $taskHarness $OutputDir
    if ($LASTEXITCODE -ne 0) { throw 'Settings verification failed' }
} finally {
    Move-Item -LiteralPath $taskHarness -Destination (Join-Path $OutputDir 'SettingsDirectoryVerification.exe') -Force -ErrorAction SilentlyContinue
    Move-Item -LiteralPath ($taskHarness + '.config') -Destination (Join-Path $OutputDir 'SettingsDirectoryVerification.exe.config') -Force -ErrorAction SilentlyContinue
}
Get-Content (Join-Path $OutputDir 'verification.txt')
