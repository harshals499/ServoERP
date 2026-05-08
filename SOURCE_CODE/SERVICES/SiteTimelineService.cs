using System;
using System.Collections.Generic;
using System.Linq;
using HVAC_Pro_Desktop.DAL;
using HVAC_Pro_Desktop.Models;

namespace HVAC_Pro_Desktop.Services
{
    public sealed class SiteTimelineService
    {
        private readonly DbExecutor _db = new DbExecutor();

        public List<SiteTimelineItem> GetTimeline(int siteId, int max = 100)
        {
            var items = new List<SiteTimelineItem>();
            if (siteId <= 0)
                return items;

            AddJobs(siteId, items);
            AddInvoices(siteId, items);
            AddPurchases(siteId, items);
            AddServiceDesk(siteId, items);
            AddContracts(siteId, items);

            return items
                .OrderByDescending(i => i.EventDate)
                .Take(Math.Max(1, max))
                .ToList();
        }

        private void AddJobs(int siteId, List<SiteTimelineItem> items)
        {
            AddRows(@"SELECT JobID AS Id, ScheduledDate AS EventDate, ISNULL(JobNumber,'') + ' - ' + ISNULL(JobTitle, Title) AS Title, ISNULL(PipelineStatus, Status) AS Detail FROM Jobs WHERE SiteID=@siteId", siteId, "Job", "Jobs", items);
        }

        private void AddInvoices(int siteId, List<SiteTimelineItem> items)
        {
            AddRows(@"SELECT InvoiceID AS Id, InvoiceDate AS EventDate, InvoiceNumber AS Title, PaymentStatus + ' | Total ' + CONVERT(NVARCHAR(40), TotalAmount) AS Detail FROM Invoices WHERE SiteID=@siteId", siteId, "Invoice", "Invoices", items);
        }

        private void AddPurchases(int siteId, List<SiteTimelineItem> items)
        {
            AddRows(@"SELECT POID AS Id, PODate AS EventDate, PONumber AS Title, ISNULL(Status,'') + ' | Total ' + CONVERT(NVARCHAR(40), TotalAmount) AS Detail FROM PurchaseOrders WHERE SiteID=@siteId", siteId, "Purchase", "Purchases", items);
        }

        private void AddServiceDesk(int siteId, List<SiteTimelineItem> items)
        {
            AddRows(@"IF OBJECT_ID('ServiceDeskIncidents', 'U') IS NOT NULL SELECT IncidentId AS Id, OpenedAt AS EventDate, IncidentNumber + ' - ' + ShortDescription AS Title, Status + ' | ' + Priority AS Detail FROM ServiceDeskIncidents WHERE SiteId=@siteId", siteId, "Service Desk", "ServiceDesk", items);
        }

        private void AddContracts(int siteId, List<SiteTimelineItem> items)
        {
            AddRows(@"SELECT ContractID AS Id, StartDate AS EventDate, 'Contract #' + CONVERT(NVARCHAR(20), ContractID) AS Title, ContractStatus + ' | Ends ' + CONVERT(NVARCHAR(20), EndDate, 106) AS Detail FROM AMCContracts WHERE SiteID=@siteId", siteId, "Contract", "Contracts", items);
        }

        private void AddRows(string sql, int siteId, string eventType, string pageKey, List<SiteTimelineItem> items)
        {
            try
            {
                items.AddRange(_db.Query(sql, reader =>
                    {
                        return new SiteTimelineItem
                        {
                            EventType = eventType,
                            PageKey = pageKey,
                            RecordId = reader["Id"] == DBNull.Value ? (int?)null : Convert.ToInt32(reader["Id"]),
                            EventDate = reader["EventDate"] == DBNull.Value ? DateTime.MinValue : Convert.ToDateTime(reader["EventDate"]),
                            Title = Convert.ToString(reader["Title"]),
                            Detail = Convert.ToString(reader["Detail"])
                        };
                    },
                    DbExecutor.Param("@siteId", siteId)));
            }
            catch (Exception ex)
            {
                AppLogger.LogError("SiteTimelineService." + eventType, ex);
            }
        }
    }
}
