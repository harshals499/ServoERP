using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using System.Windows.Forms;
using HVAC_Pro_Desktop.Models;
using HVAC_Pro_Desktop.UI;

namespace HVAC_Pro_Desktop.Tests
{
    public static class SiteMonitorRegionFilterSmokeTests
    {
        public static string RunAll()
        {
            using (var form = new GeoIntelligenceForm())
            {
                SetField(form, "_jobs", new List<JobSummaryDto>
                {
                    new JobSummaryDto
                    {
                        JobId = 1,
                        JobNumber = "JOB-PUNE-1",
                        JobTitle = "Emergency chiller repair",
                        JobType = "Emergency",
                        PipelineStatus = "Unassigned",
                        Priority = "Critical",
                        ClientId = 10,
                        SiteId = 101,
                        ClientName = "Acme",
                        SiteName = "Acme Pune",
                        ScheduledDate = DateTime.Today
                    },
                    new JobSummaryDto
                    {
                        JobId = 2,
                        JobNumber = "JOB-MUMBAI-1",
                        JobTitle = "Preventive maintenance",
                        JobType = "AMC",
                        PipelineStatus = "In Progress",
                        Priority = "Normal",
                        ClientId = 20,
                        SiteId = 201,
                        ClientName = "Multi",
                        SiteName = "Multi Mumbai",
                        TechnicianName = "Ravi Technician",
                        ScheduledDate = DateTime.Today
                    }
                });
                SetField(form, "_sites", new List<ClientSite>
                {
                    new ClientSite { SiteID = 101, ClientID = 10, SiteName = "Acme Pune", City = "Pune" },
                    new ClientSite { SiteID = 201, ClientID = 20, SiteName = "Multi Mumbai", City = "Mumbai" },
                    new ClientSite { SiteID = 301, ClientID = 30, SiteName = "Quiet Chennai", City = "Chennai" }
                });
                SetField(form, "_clients", new List<B2BClient>
                {
                    new B2BClient { ClientID = 10, CompanyName = "Acme" },
                    new B2BClient { ClientID = 20, CompanyName = "Multi" },
                    new B2BClient { ClientID = 30, CompanyName = "Quiet Customer" }
                });

                IList rows = (IList)Invoke(form, "BuildSiteMonitorRows");
                Invoke(form, "BindRegions", rows);

                TextBox search = (TextBox)GetField(form, "_txtSiteRegionSearch");
                ComboBox region = (ComboBox)GetField(form, "_cmbSiteRegion");
                ComboBox work = (ComboBox)GetField(form, "_cmbSiteWork");
                Label count = (Label)GetField(form, "_lblSiteFilterCount");
                DataGridView grid = (DataGridView)GetField(form, "_regionGrid");

                if (search == null || region == null || work == null || count == null)
                    throw new InvalidOperationException("All Sites by Region filter controls must be constructed with the page.");
                if (!region.Items.Contains("Pune") || !region.Items.Contains("Mumbai") || !region.Items.Contains("Chennai"))
                    throw new InvalidOperationException("The region filter must contain every loaded site region.");

                search.Text = "Quiet Chennai";
                work.SelectedItem = "No active work";
                Invoke(form, "ApplySiteRegionFilters");
                AssertSingleSite(grid, "Quiet Chennai", "Combined search and no-active-work filters");

                search.Clear();
                work.SelectedItem = "All work";
                region.SelectedItem = "Mumbai";
                Invoke(form, "ApplySiteRegionFilters");
                AssertSingleSite(grid, "Multi Mumbai", "Region filter");

                region.SelectedItem = "All regions";
                work.SelectedItem = "Critical / SLA risk";
                Invoke(form, "ApplySiteRegionFilters");
                AssertSingleSite(grid, "Acme Pune", "Critical / SLA risk filter");

                work.SelectedItem = "Unassigned technician";
                Invoke(form, "ApplySiteRegionFilters");
                AssertSingleSite(grid, "Acme Pune", "Unassigned technician filter");

                Invoke(form, "ClearSiteRegionFilters");
                if (grid.Rows.Count != rows.Count)
                    throw new InvalidOperationException("Clear must restore every loaded site.");
                if (!string.Equals(search.Text, string.Empty, StringComparison.Ordinal)
                    || !string.Equals(Convert.ToString(region.SelectedItem), "All regions", StringComparison.Ordinal)
                    || !string.Equals(Convert.ToString(work.SelectedItem), "All work", StringComparison.Ordinal))
                    throw new InvalidOperationException("Clear must reset every All Sites by Region filter control.");
                if (!count.Text.Contains(rows.Count.ToString()))
                    throw new InvalidOperationException("The filter count must reflect the restored site total.");
            }

            return "Site Monitor All Sites by Region search, region, work-state, count, and clear filters verified";
        }

        private static void AssertSingleSite(DataGridView grid, string siteName, string scenario)
        {
            if (grid.Rows.Count != 1 || !string.Equals(Convert.ToString(grid.Rows[0].Cells[1].Value), siteName, StringComparison.Ordinal))
                throw new InvalidOperationException(scenario + " must show only " + siteName + ".");
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
    }
}
