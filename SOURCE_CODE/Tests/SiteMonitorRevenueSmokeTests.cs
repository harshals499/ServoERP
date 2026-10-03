using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
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
            }
            return "Site Monitor revenue uses exact invoice totals without hiding or double-counting company invoices.";
        }

        private static void SetField(object target, string name, object value)
        {
            target.GetType().GetField(name, BindingFlags.Instance | BindingFlags.NonPublic).SetValue(target, value);
        }

        private static object Invoke(object target, string name)
        {
            return target.GetType().GetMethod(name, BindingFlags.Instance | BindingFlags.NonPublic).Invoke(target, null);
        }

        private static decimal ReadRevenue(object row)
        {
            return (decimal)row.GetType().GetProperty("Revenue").GetValue(row, null);
        }

        private static string ReadSite(object row)
        {
            return Convert.ToString(row.GetType().GetProperty("Site").GetValue(row, null));
        }
    }
}
