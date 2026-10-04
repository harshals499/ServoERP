using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Windows.Forms;
using HVAC_Pro_Desktop.Models;
using HVAC_Pro_Desktop.UI;

namespace HVAC_Pro_Desktop.Tests
{
    public static class SiteMonitorRevenueSmokeTests
    {
        public static string RunAll()
        {
            using (var form = new GeoIntelligenceForm())
            {
                SetField(form, "_jobs", new List<JobSummaryDto>
                {
                    new JobSummaryDto { JobId = 1, ClientId = 10, SiteId = 101, ClientName = "Acme", SiteName = "Acme Pune", ScheduledDate = DateTime.Today },
                    new JobSummaryDto { JobId = 2, ClientId = 20, SiteId = 201, ClientName = "Multi", SiteName = "Multi North", ScheduledDate = DateTime.Today },
                    new JobSummaryDto { JobId = 3, ClientId = 20, SiteId = 202, ClientName = "Multi", SiteName = "Multi South", ScheduledDate = DateTime.Today }
                });
                SetField(form, "_sites", new List<ClientSite>
                {
                    new ClientSite { SiteID = 101, ClientID = 10, SiteName = "Acme Pune", City = "Pune" },
                    new ClientSite { SiteID = 201, ClientID = 20, SiteName = "Multi North", City = "Mumbai" },
                    new ClientSite { SiteID = 202, ClientID = 20, SiteName = "Multi South", City = "Mumbai" },
                    new ClientSite { SiteID = 303, ClientID = 40, SiteName = "Quiet Chennai", City = "Chennai" }
                });
                SetField(form, "_clients", new List<B2BClient>
                {
                    new B2BClient { ClientID = 10, CompanyName = "Acme" },
                    new B2BClient { ClientID = 20, CompanyName = "Multi" },
                    new B2BClient { ClientID = 40, CompanyName = "Quiet Customer" }
                });
                SetField(form, "_invoices", new List<Invoice>
                {
                    new Invoice { InvoiceID = 1, ClientID = 10, SiteID = 101, ClientName = "Acme", SiteName = "Acme Pune", TotalAmount = 1000m, InvoiceDate = DateTime.Today, PaymentStatus = "Paid" },
                    new Invoice { InvoiceID = 2, ClientID = 20, ClientName = "Multi", TotalAmount = 2500m, InvoiceDate = DateTime.Today, PaymentStatus = "Pending" },
                    new Invoice { InvoiceID = 3, ClientID = 20, ClientName = "Multi", TotalAmount = 500m, InvoiceDate = DateTime.Today, PaymentStatus = "Paid" },
                    new Invoice { InvoiceID = 4, ClientID = 30, ClientName = "Invoice Only", TotalAmount = 700m, InvoiceDate = DateTime.Today, PaymentStatus = "Paid" },
                    new Invoice { InvoiceID = 5, ClientID = 10, SiteID = 101, ClientName = "Acme", SiteName = "Acme Pune", TotalAmount = 999m, InvoiceDate = DateTime.Today, PaymentStatus = "Cancelled" }
                });

                IList rows = (IList)Invoke(form, "BuildSiteMonitorRows");
                decimal total = rows.Cast<object>().Sum(ReadRevenue);
                if (total != 4700m)
                    throw new InvalidOperationException("Site Monitor billed revenue must reconcile exactly once to included invoice totals.");
                if (rows.Cast<object>().Count(r => ReadSite(r).Contains("Unassigned Site")) != 1)
                    throw new InvalidOperationException("Ambiguous company invoices must share one unassigned-site bucket.");
                if (!rows.Cast<object>().Any(r => ReadSite(r) == "Invoice Only" && ReadRevenue(r) == 700m))
                    throw new InvalidOperationException("Invoice-only companies must remain visible in Site Monitor revenue.");
                if (!rows.Cast<object>().Any(r => ReadSite(r) == "Quiet Chennai" && ReadWork(r) == "No active work"))
                    throw new InvalidOperationException("Master sites without jobs must remain visible with a clear no-active-work state.");

                Invoke(form, "BindRegions", rows);
                TextBox search = (TextBox)GetField(form, "_txtSiteRegionSearch");
                ComboBox regionFilter = (ComboBox)GetField(form, "_cmbSiteRegion");
                ComboBox workFilter = (ComboBox)GetField(form, "_cmbSiteWork");
                DataGridView grid = (DataGridView)GetField(form, "_regionGrid");
                search.Text = "Quiet Chennai";
                workFilter.SelectedItem = "No active work";
                Invoke(form, "ApplySiteRegionFilters");
                if (grid.Rows.Count != 1 || Convert.ToString(grid.Rows[0].Cells[1].Value) != "Quiet Chennai")
                    throw new InvalidOperationException("Site Monitor search and work-state filters must combine without hiding the matching site.");

                search.Clear();
                workFilter.SelectedItem = "All work";
                regionFilter.SelectedItem = "Chennai";
                Invoke(form, "ApplySiteRegionFilters");
                if (grid.Rows.Count != 1 || Convert.ToString(grid.Rows[0].Cells[0].Value) != "Chennai")
                    throw new InvalidOperationException("Site Monitor region filter must show only sites from the selected region.");

                Invoke(form, "ClearSiteRegionFilters");
                if (grid.Rows.Count != rows.Count)
                    throw new InvalidOperationException("Clearing Site Monitor filters must restore every loaded site.");
            }
            return "Site Monitor lists master sites, combines search, region, and work-state filters, and preserves exact invoice revenue.";
        }

        private static void SetField(object target, string name, object value)
        {
            target.GetType().GetField(name, BindingFlags.Instance | BindingFlags.NonPublic).SetValue(target, value);
        }

        private static object GetField(object target, string name)
        {
            return target.GetType().GetField(name, BindingFlags.Instance | BindingFlags.NonPublic).GetValue(target);
        }

        private static object Invoke(object target, string name, params object[] arguments)
        {
            return target.GetType().GetMethod(name, BindingFlags.Instance | BindingFlags.NonPublic).Invoke(target, arguments);
        }

        private static decimal ReadRevenue(object row)
        {
            return (decimal)row.GetType().GetProperty("Revenue").GetValue(row, null);
        }

        private static string ReadSite(object row)
        {
            return Convert.ToString(row.GetType().GetProperty("Site").GetValue(row, null));
        }

        private static string ReadWork(object row)
        {
            return Convert.ToString(row.GetType().GetProperty("CurrentWork").GetValue(row, null));
        }
    }
}
