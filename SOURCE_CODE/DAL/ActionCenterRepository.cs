using System;
using System.Data.SqlClient;
using System.Linq;
using Dapper;
using HVAC_Pro_Desktop.Models;

namespace HVAC_Pro_Desktop.DAL
{
    /// <summary>
    /// Loads only unresolved operational records needed by My Work in one SQL round trip.
    /// Business mutations remain in their owning module services.
    /// </summary>
    public sealed class ActionCenterRepository
    {
        public ActionCenterInput GetActionable(AppUserDto user, DateTime now)
        {
            const string sql = @"
SELECT TOP (300) j.*, c.CompanyName AS ClientName, s.SiteName, e.Name AS AssignedEmployeeName
FROM dbo.Jobs j
LEFT JOIN dbo.B2BClients c ON c.ClientID=j.ClientID
LEFT JOIN dbo.ClientSites s ON s.SiteID=j.SiteID
LEFT JOIN dbo.Employees e ON e.EmployeeID=j.AssignedEmployeeID
WHERE ISNULL(j.PipelineStatus,j.Status) NOT IN ('Closed','Invoiced','Cancelled')
   OR (ISNULL(j.PipelineStatus,j.Status)='Completed' AND j.InvoiceId IS NULL)
ORDER BY CASE WHEN j.ScheduledDate < @now THEN 0 ELSE 1 END, j.ScheduledDate, j.JobID DESC;

SELECT TOP (250) i.*, c.CompanyName AS ClientName, s.SiteName, e.Name AS AssignedEmployeeName
FROM dbo.ServiceDeskIncidents i
LEFT JOIN dbo.B2BClients c ON c.ClientID=i.ClientId
LEFT JOIN dbo.ClientSites s ON s.SiteID=i.SiteId
LEFT JOIN dbo.Employees e ON e.EmployeeID=i.AssignedEmployeeId
WHERE i.Status NOT IN ('Resolved','Closed','Cancelled')
ORDER BY i.SlaDueAt, i.OpenedAt DESC;

SELECT TOP (200) c.* FROM dbo.AMCContracts c
WHERE c.ContractStatus NOT IN ('Cancelled','Expired') AND c.EndDate <= DATEADD(day,90,@now)
ORDER BY c.EndDate;

SELECT TOP (250) q.*, c.CompanyName AS ClientName, s.SiteName
FROM dbo.Quotations q
LEFT JOIN dbo.B2BClients c ON c.ClientID=q.ClientID
LEFT JOIN dbo.ClientSites s ON s.SiteID=q.SiteID
WHERE q.Status IN ('Draft','Sent','Submitted','Won','Accepted','Approved')
ORDER BY q.DueDate, q.BidID DESC;

SELECT TOP (250) i.*, c.CompanyName AS ClientName, s.SiteName
FROM dbo.Invoices i
LEFT JOIN dbo.B2BClients c ON c.ClientID=i.ClientID
LEFT JOIN dbo.ClientSites s ON s.SiteID=i.SiteID
WHERE ISNULL(i.BalanceDue,0)>0.01 AND ISNULL(i.PaymentStatus,'') NOT IN ('Paid','Cancelled')
  AND i.DueDate <= DATEADD(day,30,@now)
ORDER BY i.DueDate, i.InvoiceID DESC;

SELECT TOP (250) p.*, v.VendorName, c.CompanyName AS ClientName, s.SiteName
FROM dbo.PurchaseOrders p
LEFT JOIN dbo.Vendors v ON v.VendorID=p.VendorID
LEFT JOIN dbo.B2BClients c ON c.ClientID=p.ClientID
LEFT JOIN dbo.ClientSites s ON s.SiteID=p.SiteID
WHERE p.Status IN ('Draft','Pending','Pending Approval')
   OR (p.PayByDate < @now AND ISNULL(p.PaidAmount,0)<ISNULL(p.TotalAmount,0) AND p.Status NOT IN ('Fully Received','Received','Paid','Closed'))
ORDER BY p.PayByDate, p.POID DESC;

SELECT TOP (250) s.*, v.VendorName FROM dbo.StockItems s
LEFT JOIN dbo.Vendors v ON v.VendorID=s.VendorID
WHERE ISNULL(s.IsActive,1)=1 AND (ISNULL(s.CurrentStock,0)-ISNULL(s.ReservedStock,0)<=ISNULL(s.ReorderLevel,0))
ORDER BY (ISNULL(s.CurrentStock,0)-ISNULL(s.ReservedStock,0)), s.ItemID;

SELECT TOP (250) p.*, i.InvoiceNumber, c.CompanyName AS ClientName
FROM dbo.Payments p
INNER JOIN dbo.Invoices i ON i.InvoiceID=p.InvoiceID
INNER JOIN dbo.B2BClients c ON c.ClientID=p.ClientID
WHERE ISNULL(p.ReconciliationStatus,'Unreconciled')<>'Reconciled'
ORDER BY p.PaymentDate, p.PaymentID;

SELECT TOP (250) v.VisitID, v.AMCID AS ContractId, v.JobID, v.VisitNumber, v.ScheduledDate, v.Status,
       COALESCE(e.Name,v.TechnicianName) AS TechnicianName, j.AssignedEmployeeID,
       a.ClientID, a.SiteID, c.CompanyName AS ClientName, s.SiteName
FROM dbo.AMCVisits v
INNER JOIN dbo.AMCContracts a ON a.ContractID=v.AMCID
LEFT JOIN dbo.Jobs j ON j.JobID=v.JobID
LEFT JOIN dbo.Employees e ON e.EmployeeID=j.AssignedEmployeeID
LEFT JOIN dbo.B2BClients c ON c.ClientID=a.ClientID
LEFT JOIN dbo.ClientSites s ON s.SiteID=a.SiteID
WHERE v.Status NOT IN ('Completed','Cancelled','Closed') AND v.ScheduledDate<=DATEADD(day,30,@now)
ORDER BY v.ScheduledDate, v.VisitID;

SELECT TOP (250) p.PartUsedId, p.JobId, j.JobNumber, p.ItemDescription,
       p.QuantityUsed AS RequiredQuantity,
       CASE WHEN si.ItemID IS NULL THEN 0 ELSE ISNULL(si.CurrentStock,0)-ISNULL(si.ReservedStock,0) END AS AvailableQuantity,
       p.Unit, p.StockStatus, j.AssignedEmployeeID, e.Name AS AssignedEmployeeName,
       j.ClientID, j.SiteID, c.CompanyName AS ClientName, s.SiteName, j.ScheduledDate
FROM dbo.JobPartsUsed p
INNER JOIN dbo.Jobs j ON j.JobID=p.JobId
LEFT JOIN dbo.StockItems si ON si.ItemID=p.InventoryItemId
LEFT JOIN dbo.Employees e ON e.EmployeeID=j.AssignedEmployeeID
LEFT JOIN dbo.B2BClients c ON c.ClientID=j.ClientID
LEFT JOIN dbo.ClientSites s ON s.SiteID=j.SiteID
WHERE ISNULL(j.PipelineStatus,j.Status) NOT IN ('Closed','Invoiced','Cancelled','Completed')
  AND p.LinkedPoId IS NULL
  AND (p.StockStatus IN ('Unavailable','Shortage','Insufficient','Out of Stock')
       OR (p.IsFromInventory=1 AND p.QuantityUsed>CASE WHEN si.ItemID IS NULL THEN 0 ELSE ISNULL(si.CurrentStock,0)-ISNULL(si.ReservedStock,0) END))
ORDER BY j.ScheduledDate, p.PartUsedId;";

            using (SqlConnection connection = DapperDatabase.CreateConnection())
            using (SqlMapper.GridReader grid = connection.QueryMultiple(sql, new { now }, commandTimeout: 45))
            {
                return new ActionCenterInput
                {
                    Jobs = grid.Read<Job>().ToList(),
                    ServiceIncidents = grid.Read<ServiceDeskIncident>().ToList(),
                    Contracts = grid.Read<AMCContract>().ToList(),
                    Quotations = grid.Read<TenderBid>().ToList(),
                    Invoices = grid.Read<Invoice>().ToList(),
                    PurchaseOrders = grid.Read<PurchaseOrder>().ToList(),
                    InventoryItems = grid.Read<StockItem>().ToList(),
                    Payments = grid.Read<Payment>().ToList(),
                    AmcVisits = grid.Read<ActionCenterAmcVisit>().ToList(),
                    PartShortages = grid.Read<ActionCenterPartShortage>().ToList(),
                    User = user,
                    Now = now
                };
            }
        }
    }
}
