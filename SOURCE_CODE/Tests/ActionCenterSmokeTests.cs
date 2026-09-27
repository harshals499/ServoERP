using System;
using System.Collections.Generic;
using System.Linq;
using HVAC_Pro_Desktop.Models;
using HVAC_Pro_Desktop.Services;

namespace HVAC_Pro_Desktop.Tests
{
    public static class ActionCenterSmokeTests
    {
        public static List<string> RunAll()
        {
            var passed = new List<string>();
            DateTime now = new DateTime(2026, 9, 27, 10, 0, 0);
            var service = new ActionCenterService();
            AppUserDto manager = User("Manager", "Harshal");

            ActionCenterSnapshot operational = service.Build(new ActionCenterInput
            {
                User = manager,
                Now = now,
                Jobs = new List<Job>
                {
                    new Job { JobID = 1, JobNumber = "JOB-URGENT", ClientID = 1, SiteID = 2, ClientName = "ABC Industries",
                        SiteName = "Plant 1", Priority = "Critical", PipelineStatus = "Created", ScheduledDate = now.AddMinutes(-43), CreatedDate = now.AddHours(-1) },
                    new Job { JobID = 2, JobNumber = "JOB-DONE", ClientID = 1, SiteID = 2, ClientName = "ABC Industries",
                        PipelineStatus = "Completed", CompletedDate = now.AddHours(-2), ScheduledDate = now.AddDays(-1), QuotedRevenue = 25000m, CreatedDate = now.AddDays(-2) }
                },
                ServiceIncidents = new List<ServiceDeskIncident>
                {
                    new ServiceDeskIncident { IncidentId = 10, IncidentNumber = "INC-10", ClientName = "ABC Industries", Priority = "High",
                        Status = "Open", ShortDescription = "AC breakdown", OpenedAt = now.AddHours(-1), SlaDueAt = now.AddMinutes(42) }
                },
                InventoryItems = new List<StockItem>
                {
                    new StockItem { ItemID = 20, ItemName = "Compressor", CurrentStock = 0m, ReorderLevel = 2m,
                        LastPurchaseRate = 8000m, LastUpdated = now, IsActive = true }
                },
                AmcVisits = new List<ActionCenterAmcVisit>
                {
                    new ActionCenterAmcVisit { VisitId = 31, ContractId = 12, VisitNumber = 2, ScheduledDate = now.AddDays(-1),
                        Status = "Scheduled", ClientName = "ABC Industries", SiteName = "Plant 1" }
                },
                PartShortages = new List<ActionCenterPartShortage>
                {
                    new ActionCenterPartShortage { PartUsedId = 41, JobId = 1, JobNumber = "JOB-URGENT", ItemDescription = "Compressor",
                        RequiredQuantity = 1m, AvailableQuantity = 0m, Unit = "Nos", ScheduledDate = now.AddMinutes(-43) }
                }
            });
            Assert(operational.Items.Count(item => item.ActionId == "AssignTechnician:Jobs:1") == 1, "Unassigned job action must be unique.");
            Assert(operational.Items.Any(item => item.ActionId == "InvoiceCompletedJob:Invoices:2"), "Completed billable job should create an invoicing action.");
            Assert(operational.Items.Any(item => item.ActionId == "ReorderStock:Inventory:20"), "Unavailable stock should create a procurement action.");
            Assert(operational.Items.Any(item => item.ActionType == "CompleteAmcVisit" && item.SourceChildRecordId == 31), "Due AMC visit should create a visit-specific action.");
            Assert(operational.Items.Any(item => item.ActionType == "ProcureJobPart" && item.SourceChildRecordId == 41), "Job-specific stock shortage should create a blocking action.");
            Assert(operational.Items.All(item => !string.IsNullOrWhiteSpace(item.PriorityReason)), "Every action should explain its priority.");
            Assert(operational.RecommendedNextAction != null && operational.RecommendedNextAction.PriorityScore ==
                operational.Items.Where(item => !item.IsWaiting).Max(item => item.PriorityScore), "Recommended action must use the highest deterministic score.");
            passed.Add("service breakdown, invoice handoff, inventory warning and deterministic priority");

            ActionCenterSnapshot resolved = service.Build(new ActionCenterInput
            {
                User = manager,
                Now = now,
                Jobs = new List<Job>
                {
                    new Job { JobID = 1, JobNumber = "JOB-URGENT", AssignedEmployeeID = 7, AssignedEmployeeName = "Tech One",
                        PipelineStatus = "Assigned", ScheduledDate = now.AddMinutes(-43), CreatedDate = now.AddHours(-1) },
                    new Job { JobID = 2, JobNumber = "JOB-DONE", PipelineStatus = "Invoiced", InvoiceId = 99,
                        CompletedDate = now.AddHours(-2), ScheduledDate = now.AddDays(-1), QuotedRevenue = 25000m, CreatedDate = now.AddDays(-2) }
                }
            });
            Assert(!resolved.Items.Any(item => item.ActionId == "AssignTechnician:Jobs:1"), "Assignment action should clear when assigned.");
            Assert(!resolved.Items.Any(item => item.ActionId == "InvoiceCompletedJob:Invoices:2"), "Invoice action should clear when invoiced.");
            passed.Add("source-state resolution automatically clears stale actions");

            ActionCenterSnapshot technician = service.Build(new ActionCenterInput
            {
                User = User("Technician", "A Different Login Name", 7),
                Now = now,
                Jobs = new List<Job>
                {
                    new Job { JobID = 3, JobNumber = "JOB-MINE", AssignedEmployeeID = 7, AssignedEmployeeName = "Tech One", PipelineStatus = "Assigned", ScheduledDate = now },
                    new Job { JobID = 4, JobNumber = "JOB-OTHER", AssignedEmployeeID = 8, AssignedEmployeeName = "Tech Two", PipelineStatus = "Assigned", ScheduledDate = now },
                    new Job { JobID = 5, JobNumber = "JOB-UNASSIGNED", PipelineStatus = "Created", ScheduledDate = now }
                },
                Invoices = new List<Invoice>
                {
                    new Invoice { InvoiceID = 7, InvoiceNumber = "INV-7", BalanceDue = 10000m, InvoiceDate = now.AddDays(-30), DueDate = now.AddDays(-1), PaymentStatus = "Pending" }
                }
            });
            Assert(technician.Items.Count == 1 && technician.Items[0].Reference == "JOB-MINE", "Technician scope leaked unrelated work.");
            passed.Add("technician role excludes finance, unassigned work and other technicians");

            ActionCenterSnapshot accounts = service.Build(new ActionCenterInput
            {
                User = User("Accountant", "Accounts User"),
                Now = now,
                Invoices = new List<Invoice>
                {
                    new Invoice { InvoiceID = 8, InvoiceNumber = "INV-OVERDUE", BalanceDue = 120000m,
                        InvoiceDate = now.AddDays(-45), DueDate = now.AddDays(-15), PaymentStatus = "Partial" }
                },
                PurchaseOrders = new List<PurchaseOrder>
                {
                    new PurchaseOrder { POID = 9, PONumber = "PO-APPROVAL", PODate = now.AddDays(-1), PayByDate = now.AddDays(15),
                        TotalAmount = 30000m, Status = "Pending Approval" }
                },
                Payments = new List<Payment>
                {
                    new Payment { PaymentID = 14, PaymentNumber = "PAY-14", ClientID = 1, ClientName = "ABC Industries",
                        AmountPaid = 18000m, PaymentDate = now.AddDays(-4), CreatedDate = now.AddDays(-4), ReconciliationStatus = "Unreconciled" }
                }
            });
            Assert(accounts.Items.Any(item => item.ActionType == "FollowUpInvoice"), "Accounts should see overdue receivables.");
            Assert(accounts.Items.Any(item => item.ActionType == "ReviewPurchaseOrder"), "Accounts should see PO approvals.");
            Assert(accounts.Items.Any(item => item.ActionType == "ReconcilePayment" && item.IsOverdue), "Accounts should see stale unreconciled payments.");
            passed.Add("accounts workspace contains receivables and procurement obligations");

            AppUserDto restrictedManager = User("Manager", "Restricted Manager");
            restrictedManager.Permissions["WorkOrders"] = new RolePermissionDto { ModuleKey = "WorkOrders", CanView = true };
            ActionCenterSnapshot restricted = service.Build(new ActionCenterInput
            {
                User = restrictedManager,
                Now = now,
                Jobs = new List<Job>
                {
                    new Job { JobID = 11, JobNumber = "JOB-ALLOWED", PipelineStatus = "Created", ScheduledDate = now }
                },
                Invoices = new List<Invoice>
                {
                    new Invoice { InvoiceID = 12, InvoiceNumber = "INV-BLOCKED", BalanceDue = 5000m, InvoiceDate = now.AddDays(-5),
                        DueDate = now.AddDays(-1), PaymentStatus = "Pending" }
                }
            });
            Assert(restricted.Items.Any(item => item.SourceModule == "Jobs") && !restricted.Items.Any(item => item.SourceModule == "Invoices"),
                "Action Center must honor existing module permissions.");
            passed.Add("module permission filtering blocks unauthorized actions");

            ActionCenterSnapshot empty = service.Build(new ActionCenterInput { User = manager, Now = now });
            Assert(empty.Items.Count == 0 && empty.RecommendedNextAction == null, "Empty installation fabricated work.");
            passed.Add("empty installation remains empty");
            return passed;
        }

        private static AppUserDto User(string role, string name, int? employeeId = null)
        {
            return new AppUserDto { UserId = 1, EmployeeId = employeeId, Username = name.Replace(" ", ".").ToLowerInvariant(), DisplayName = name, RoleName = role, IsActive = true };
        }

        private static void Assert(bool condition, string message)
        {
            if (!condition) throw new InvalidOperationException(message);
        }
    }
}
