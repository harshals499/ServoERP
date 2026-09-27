using System;
using System.Collections.Generic;
using System.Linq;
using HVAC_Pro_Desktop.Models;

namespace HVAC_Pro_Desktop.Services
{
    /// <summary>
    /// Derives role-aware work from authoritative business records. Because no task state is copied,
    /// duplicate actions are eliminated and resolved conditions disappear on the next refresh.
    /// </summary>
    public sealed class ActionCenterService
    {
        public ActionCenterSnapshot Build(ActionCenterInput input)
        {
            input = input ?? new ActionCenterInput();
            DateTime now = input.Now == default(DateTime) ? DateTime.Now : input.Now;
            var candidates = new List<ActionCenterItem>();

            AddJobActions(candidates, input.Jobs, input.User, now);
            AddIncidentActions(candidates, input.ServiceIncidents, now);
            AddContractActions(candidates, input.Contracts, now);
            AddQuotationActions(candidates, input.Quotations, now);
            AddInvoiceActions(candidates, input.Invoices, now);
            AddPurchaseActions(candidates, input.PurchaseOrders, now);
            AddInventoryActions(candidates, input.InventoryItems, now);
            AddPaymentActions(candidates, input.Payments, now);
            AddAmcVisitActions(candidates, input.AmcVisits, now);
            AddPartShortageActions(candidates, input.PartShortages, now);

            List<ActionCenterItem> items = candidates
                .Where(item => IsRoleRelevant(item, input.User) && HasModulePermission(item.SourceModule, input.User))
                .GroupBy(item => item.ActionId, StringComparer.OrdinalIgnoreCase)
                .Select(group => group.OrderByDescending(item => item.PriorityScore).First())
                .OrderByDescending(item => item.PriorityScore)
                .ThenBy(item => item.DueAt ?? DateTime.MaxValue)
                .ThenBy(item => item.Reference)
                .ToList();

            foreach (ActionCenterItem item in items)
                item.IsMine = IsMine(item, input.User);

            return new ActionCenterSnapshot
            {
                GeneratedAt = now,
                WorkspaceName = WorkspaceName(input.User == null ? null : input.User.RoleName),
                Items = items,
                RecommendedNextAction = items.FirstOrDefault(item => !item.IsWaiting),
                CriticalCount = items.Count(item => item.Priority == ActionCenterPriority.Critical),
                OverdueCount = items.Count(item => item.IsOverdue),
                TodayCount = items.Count(item => item.Bucket == ActionCenterBucket.Today || (item.DueAt.HasValue && item.DueAt.Value.Date == now.Date)),
                WaitingCount = items.Count(item => item.IsWaiting),
                UpcomingCount = items.Count(item => item.Bucket == ActionCenterBucket.Upcoming)
            };
        }

        private static void AddJobActions(ICollection<ActionCenterItem> items, IEnumerable<Job> source, AppUserDto user, DateTime now)
        {
            foreach (Job job in source ?? Enumerable.Empty<Job>())
            {
                if (job == null) continue;
                string status = First(job.PipelineStatus, job.Status, "Created");
                bool terminal = Any(status, "Closed", "Invoiced", "Cancelled");
                bool completed = Any(status, "Completed") || (job.CompletedDate.HasValue && !terminal);
                string reference = First(job.JobNumber, "Job #" + job.JobID);
                decimal value = Math.Max(job.ActualRevenue, Math.Max(job.QuotedRevenue, job.Revenue));

                if (!terminal && !completed && !job.AssignedEmployeeID.HasValue)
                {
                    bool overdue = job.ScheduledDate != default(DateTime) && job.ScheduledDate < now;
                    bool emergency = Any(job.Priority, "Emergency", "Critical");
                    items.Add(Item("AssignTechnician", "Jobs", job.JobID, reference, "Assign a technician",
                        reference + " has no technician assigned.", job.ClientID, job.SiteID, job.ClientName, job.SiteName,
                        null, "Dispatcher", status, "Assign Technician", "Edit", job.CreatedDate, job.ScheduledDate, null, value,
                        emergency ? ActionCenterPriority.Critical : (overdue ? ActionCenterPriority.Urgent : ActionCenterPriority.Today),
                        overdue || emergency ? ActionCenterBucket.NeedsAttention : ActionCenterBucket.Today, overdue, false,
                        emergency ? "Critical job is unassigned." : (overdue ? "Scheduled time has passed and the job is unassigned." : "Today's work needs an owner."), now));
                }

                if (!terminal && !completed && job.AssignedEmployeeID.HasValue && (EmployeeMatches(job.AssignedEmployeeID, job.AssignedEmployeeName, user) || IsOperationsRole(user)))
                {
                    bool overdue = job.IsOverdue || (job.ScheduledDate != default(DateTime) && job.ScheduledDate < now);
                    bool today = job.ScheduledDate != default(DateTime) && job.ScheduledDate.Date == now.Date;
                    items.Add(Item("WorkJob", "Jobs", job.JobID, reference,
                        overdue ? "Job is overdue" : (today ? "Today's scheduled job" : "Upcoming assigned job"),
                        reference + " is assigned to " + First(job.AssignedEmployeeName, "a technician") + ".",
                        job.ClientID, job.SiteID, job.ClientName, job.SiteName, job.AssignedEmployeeName, "Technician", status,
                        Any(status, "In Progress") ? "Continue Job" : "Start Job", "Edit", job.CreatedDate, job.ScheduledDate, null, value,
                        overdue ? ActionCenterPriority.Urgent : (today ? ActionCenterPriority.Today : ActionCenterPriority.Normal),
                        overdue ? ActionCenterBucket.NeedsAttention : (today ? ActionCenterBucket.Today : ActionCenterBucket.Upcoming),
                        overdue, false, overdue ? "Scheduled time has passed." : (today ? "Scheduled for today." : "Scheduled work is approaching."), now,
                        job.AssignedEmployeeID));
                }

                if (completed && !job.InvoiceId.HasValue)
                {
                    bool billable = value > 0m;
                    items.Add(Item("InvoiceCompletedJob", billable ? "Invoices" : "Jobs", job.JobID, reference,
                        billable ? "Create invoice for completed work" : "Review completed job",
                        reference + " is completed and is not linked to an invoice.", job.ClientID, job.SiteID, job.ClientName,
                        job.SiteName, job.AssignedEmployeeName, billable ? "Accounts" : "Dispatcher", status,
                        billable ? "Create Invoice" : "Review Job", billable ? "CreateFromJob" : "Review",
                        job.CreatedDate, job.CompletedDate ?? now, null, value,
                        billable ? ActionCenterPriority.Urgent : ActionCenterPriority.Today, ActionCenterBucket.MyActions,
                        false, false, billable ? "Completed billable work is holding up invoicing." : "Completion requires an operational review.", now));
                }
            }
        }

        private static void AddIncidentActions(ICollection<ActionCenterItem> items, IEnumerable<ServiceDeskIncident> source, DateTime now)
        {
            foreach (ServiceDeskIncident incident in source ?? Enumerable.Empty<ServiceDeskIncident>())
            {
                if (incident == null || Any(incident.Status, "Resolved", "Closed", "Cancelled")) continue;
                bool breached = incident.SlaBreached || incident.SlaDueAt <= now;
                bool near = !breached && incident.SlaDueAt <= now.AddHours(2);
                bool critical = Any(incident.Priority, "Emergency", "Critical");
                string reference = First(incident.IncidentNumber, "Incident #" + incident.IncidentId);
                items.Add(Item(incident.AssignedEmployeeId.HasValue ? "ResolveIncident" : "AssignIncident", "ServiceDesk",
                    incident.IncidentId, reference, incident.AssignedEmployeeId.HasValue ? "Resolve service incident" : "New service request needs assignment",
                    First(incident.ShortDescription, incident.Description, "Open service request"), incident.ClientId, incident.SiteId,
                    incident.ClientName, incident.SiteName, incident.AssignedEmployeeName, "Dispatcher", incident.Status,
                    incident.AssignedEmployeeId.HasValue ? "Open Incident" : "Assign Technician", "Open", incident.OpenedAt,
                    incident.SlaDueAt, incident.SlaDueAt, 0m,
                    breached || critical ? ActionCenterPriority.Critical : (near ? ActionCenterPriority.Urgent : ActionCenterPriority.Today),
                    breached || near || critical ? ActionCenterBucket.NeedsAttention : ActionCenterBucket.MyActions, breached, false,
                    breached ? "Critical — SLA is breached." : (near ? "Urgent — SLA expires in " + Duration(incident.SlaDueAt - now) + "." :
                    (critical ? "Critical customer-impact incident." : "Open service incident requires progress.")), now, incident.AssignedEmployeeId));
            }
        }

        private static void AddContractActions(ICollection<ActionCenterItem> items, IEnumerable<AMCContract> source, DateTime now)
        {
            foreach (AMCContract contract in source ?? Enumerable.Empty<AMCContract>())
            {
                if (contract == null || Any(contract.ContractStatus, "Cancelled", "Expired")) continue;
                int days = (contract.EndDate.Date - now.Date).Days;
                if (days < 0 || days > 90) continue;
                items.Add(Item("RenewContract", "Contracts", contract.ContractID, "AMC #" + contract.ContractID, "Start AMC renewal",
                    "The " + First(contract.ContractType, "AMC") + " contract expires in " + days + " day(s).", contract.ClientID,
                    contract.SiteID, null, null, null, "Sales", contract.ContractStatus, "View Contract", "Open", contract.StartDate,
                    contract.EndDate, null, contract.AnnualValue, days <= 14 ? ActionCenterPriority.Urgent :
                    (days <= 30 ? ActionCenterPriority.Today : ActionCenterPriority.Normal),
                    days <= 30 ? ActionCenterBucket.NeedsAttention : ActionCenterBucket.Upcoming, false, false,
                    days <= 14 ? "Renewal window is nearly closed." : "Contract renewal is approaching.", now));
            }
        }

        private static void AddQuotationActions(ICollection<ActionCenterItem> items, IEnumerable<TenderBid> source, DateTime now)
        {
            foreach (TenderBid quote in source ?? Enumerable.Empty<TenderBid>())
            {
                if (quote == null) continue;
                string reference = First(quote.QuotationNumber, "Quotation #" + quote.BidID);
                DateTime activity = quote.ModifiedDate ?? quote.SubmittedDate ?? quote.DueDate;
                decimal value = quote.TotalWithGST > 0m ? quote.TotalWithGST : quote.BidValue;
                if (Any(quote.Status, "Sent", "Submitted"))
                {
                    int days = Math.Max(0, (now.Date - (quote.SubmittedDate ?? activity).Date).Days);
                    items.Add(Item("FollowUpQuotation", "Quotations", quote.BidID, reference, "Follow up quotation",
                        reference + " is awaiting the customer's response for " + days + " day(s).", quote.ClientID, quote.SiteID,
                        quote.ClientName, quote.SiteName, null, "Sales", quote.Status, "Follow Up", "Open", activity, quote.DueDate,
                        null, value, days >= 7 ? ActionCenterPriority.Urgent : ActionCenterPriority.Today, ActionCenterBucket.Waiting,
                        quote.DueDate.Date < now.Date, true, days >= 7 ? "Customer response has been pending for a week or more." :
                        "Waiting on customer response.", now));
                }
                else if (Any(quote.Status, "Won", "Accepted", "Approved"))
                {
                    items.Add(Item("ConvertQuotation", "Quotations", quote.BidID, reference, "Convert accepted quotation",
                        reference + " is accepted and ready for the next business record.", quote.ClientID, quote.SiteID, quote.ClientName,
                        quote.SiteName, null, "Sales", quote.Status, "Open Quotation", "Open", activity, now, null, value,
                        ActionCenterPriority.Urgent, ActionCenterBucket.MyActions, false, false,
                        "Accepted business should move into delivery.", now));
                }
                else if (Any(quote.Status, "Draft") && quote.DueDate.Date <= now.Date.AddDays(7))
                {
                    bool overdue = quote.DueDate.Date < now.Date;
                    items.Add(Item("PrepareQuotation", "Quotations", quote.BidID, reference,
                        overdue ? "Quotation deadline passed" : "Complete quotation",
                        reference + " is still in draft and is due " + quote.DueDate.ToString("dd/MM/yyyy") + ".", quote.ClientID,
                        quote.SiteID, quote.ClientName, quote.SiteName, null, "Sales", quote.Status, "Open Quotation", "Open", activity,
                        quote.DueDate, null, value, overdue ? ActionCenterPriority.Critical : ActionCenterPriority.Today,
                        overdue ? ActionCenterBucket.NeedsAttention : ActionCenterBucket.Upcoming, overdue, false,
                        overdue ? "Quotation deadline has passed." : "Submission deadline is within seven days.", now));
                }
            }
        }

        private static void AddInvoiceActions(ICollection<ActionCenterItem> items, IEnumerable<Invoice> source, DateTime now)
        {
            foreach (Invoice invoice in source ?? Enumerable.Empty<Invoice>())
            {
                if (invoice == null || invoice.BalanceDue <= 0.01m || Any(invoice.PaymentStatus, "Paid", "Cancelled")) continue;
                bool overdue = invoice.DueDate.Date < now.Date;
                string reference = First(invoice.InvoiceNumber, "Invoice #" + invoice.InvoiceID);
                items.Add(Item(overdue ? "FollowUpInvoice" : "MonitorInvoice", "Invoices", invoice.InvoiceID, reference,
                    overdue ? "Follow up overdue invoice" : "Invoice payment is due",
                    reference + " has an outstanding balance of " + IndiaFormatHelper.FormatCurrency(invoice.BalanceDue) + ".",
                    invoice.ClientID, invoice.SiteID, invoice.ClientName, invoice.SiteName, null, "Accounts", invoice.PaymentStatus,
                    overdue ? "Follow Up" : "View Invoice", "Open", invoice.InvoiceDate, invoice.DueDate, null, invoice.BalanceDue,
                    overdue ? ActionCenterPriority.Urgent : ActionCenterPriority.Normal,
                    overdue ? ActionCenterBucket.NeedsAttention : ActionCenterBucket.Upcoming, overdue, !overdue,
                    overdue ? "Payment due date has passed." : "Waiting for customer payment.", now));
            }
        }

        private static void AddPurchaseActions(ICollection<ActionCenterItem> items, IEnumerable<PurchaseOrder> source, DateTime now)
        {
            foreach (PurchaseOrder order in source ?? Enumerable.Empty<PurchaseOrder>())
            {
                if (order == null) continue;
                string reference = First(order.PONumber, "PO #" + order.POID);
                if (Any(order.Status, "Draft", "Pending", "Pending Approval"))
                {
                    items.Add(Item("ReviewPurchaseOrder", "Purchases", order.POID, reference, "Review purchase order",
                        reference + " requires approval before procurement can continue.", order.ClientID, order.SiteID, order.ClientName,
                        order.SiteName, order.AssignedTechnicianName, "Manager", order.Status, "Review PO", "Open",
                        order.CreatedByDate ?? order.PODate, order.PayByDate, null, order.TotalAmount, ActionCenterPriority.Today,
                        ActionCenterBucket.MyActions, false, false, "Procurement is waiting for approval.", now));
                }
                else if (order.IsOverdue)
                {
                    items.Add(Item("VendorPayment", "Purchases", order.POID, reference, "Review overdue vendor obligation",
                        reference + " has an overdue balance of " + IndiaFormatHelper.FormatCurrency(order.BalanceDue) + ".",
                        order.ClientID, order.SiteID, order.ClientName, order.SiteName, order.AssignedTechnicianName, "Accounts",
                        order.Status, "Open PO", "Open", order.PODate, order.PayByDate, null, order.BalanceDue,
                        ActionCenterPriority.Urgent, ActionCenterBucket.NeedsAttention, true, false,
                        "Vendor payment date has passed.", now));
                }
            }
        }

        private static void AddInventoryActions(ICollection<ActionCenterItem> items, IEnumerable<StockItem> source, DateTime now)
        {
            foreach (StockItem stock in source ?? Enumerable.Empty<StockItem>())
            {
                if (stock == null || !stock.IsActive || (!stock.IsLowStock && stock.AvailableStock > 0m)) continue;
                bool unavailable = stock.AvailableStock <= 0m;
                items.Add(Item("ReorderStock", "Inventory", stock.ItemID, "Stock #" + stock.ItemID,
                    unavailable ? "Stock unavailable" : "Reorder low stock",
                    First(stock.ItemName, "Inventory item") + " has " + stock.AvailableStock.ToString("0.##") + " " +
                    First(stock.Unit, "units") + " available.", null, null, null, null, null, "Procurement", "Low Stock",
                    "Open Inventory", "Open", stock.LastUpdated, now, null,
                    Math.Max(0m, stock.LastPurchaseRate * Math.Max(stock.ReorderLevel - stock.AvailableStock, 0m)),
                    unavailable ? ActionCenterPriority.Urgent : ActionCenterPriority.Today,
                    unavailable ? ActionCenterBucket.NeedsAttention : ActionCenterBucket.MyActions, unavailable, false,
                    unavailable ? "No available stock remains." : "Available quantity is at or below reorder level.", now));
            }
        }

        private static void AddPaymentActions(ICollection<ActionCenterItem> items, IEnumerable<Payment> source, DateTime now)
        {
            foreach (Payment payment in source ?? Enumerable.Empty<Payment>())
            {
                if (payment == null || Any(payment.ReconciliationStatus, "Reconciled")) continue;
                int age = Math.Max(0, (now.Date - payment.PaymentDate.Date).Days);
                ActionCenterItem item = Item("ReconcilePayment", "Payments", payment.PaymentID,
                    First(payment.PaymentNumber, "Payment #" + payment.PaymentID), "Reconcile customer payment",
                    First(payment.PaymentNumber, "Payment #" + payment.PaymentID) + " for " + IndiaFormatHelper.FormatCurrency(payment.AmountPaid) +
                    " is recorded but not reconciled.", payment.ClientID, null, payment.ClientName, null, null, "Accounts",
                    First(payment.ReconciliationStatus, "Unreconciled"), "Reconcile Payment", "Reconcile", payment.CreatedDate,
                    payment.PaymentDate.AddDays(1), null, payment.AmountPaid, age >= 3 ? ActionCenterPriority.Urgent : ActionCenterPriority.Today,
                    age >= 3 ? ActionCenterBucket.NeedsAttention : ActionCenterBucket.MyActions, age >= 3, false,
                    age >= 3 ? "Payment has remained unreconciled for three or more days." : "Recorded receipt needs bank reconciliation.", now);
                items.Add(item);
            }
        }

        private static void AddAmcVisitActions(ICollection<ActionCenterItem> items, IEnumerable<ActionCenterAmcVisit> source, DateTime now)
        {
            foreach (ActionCenterAmcVisit visit in source ?? Enumerable.Empty<ActionCenterAmcVisit>())
            {
                if (visit == null || Any(visit.Status, "Completed", "Cancelled", "Closed")) continue;
                bool overdue = visit.ScheduledDate.Date < now.Date;
                bool today = visit.ScheduledDate.Date == now.Date;
                string module = visit.JobId.HasValue ? "Jobs" : "Contracts";
                int recordId = visit.JobId ?? visit.ContractId;
                ActionCenterItem item = Item("CompleteAmcVisit", module, recordId,
                    "AMC #" + visit.ContractId + " / Visit " + visit.VisitNumber,
                    overdue ? "AMC visit is overdue" : (today ? "Today's AMC visit" : "Schedule upcoming AMC service"),
                    "Preventive maintenance visit " + visit.VisitNumber + " is " + (overdue ? "past due" : "scheduled for " + visit.ScheduledDate.ToString("dd/MM/yyyy")) + ".",
                    visit.ClientId, visit.SiteId, visit.ClientName, visit.SiteName, visit.TechnicianName, "Dispatcher", visit.Status,
                    visit.JobId.HasValue ? "Open Job" : "View Contract", "Open", visit.ScheduledDate, visit.ScheduledDate, null, 0m,
                    overdue ? ActionCenterPriority.Urgent : (today ? ActionCenterPriority.Today : ActionCenterPriority.Normal),
                    overdue ? ActionCenterBucket.NeedsAttention : (today ? ActionCenterBucket.Today : ActionCenterBucket.Upcoming), overdue, false,
                    overdue ? "A contracted maintenance obligation is overdue." : (today ? "Contracted visit is due today." : "Contracted visit is approaching."),
                    now, visit.AssignedEmployeeId);
                item.SourceChildRecordId = visit.VisitId;
                items.Add(item);
            }
        }

        private static void AddPartShortageActions(ICollection<ActionCenterItem> items, IEnumerable<ActionCenterPartShortage> source, DateTime now)
        {
            foreach (ActionCenterPartShortage shortage in source ?? Enumerable.Empty<ActionCenterPartShortage>())
            {
                if (shortage == null) continue;
                decimal missing = Math.Max(0m, shortage.RequiredQuantity - shortage.AvailableQuantity);
                bool blocksToday = shortage.ScheduledDate.Date <= now.Date;
                ActionCenterItem item = Item("ProcureJobPart", "Jobs", shortage.JobId,
                    First(shortage.JobNumber, "Job #" + shortage.JobId), "Procure required job part",
                    First(shortage.ItemDescription, "Required part") + " is short by " + missing.ToString("0.##") + " " + First(shortage.Unit, "units") + ".",
                    shortage.ClientId, shortage.SiteId, shortage.ClientName, shortage.SiteName, shortage.AssignedEmployeeName, "Procurement",
                    First(shortage.StockStatus, "Shortage"), "Open Job Parts", "Edit", shortage.ScheduledDate, shortage.ScheduledDate, null, 0m,
                    blocksToday ? ActionCenterPriority.Critical : ActionCenterPriority.Urgent,
                    ActionCenterBucket.NeedsAttention, blocksToday, false,
                    blocksToday ? "Required part is blocking current work." : "Required job stock is not available.", now, shortage.AssignedEmployeeId);
                item.SourceChildRecordId = shortage.PartUsedId;
                items.Add(item);
            }
        }

        private static ActionCenterItem Item(string type, string module, int id, string reference, string title, string explanation,
            int? clientId, int? siteId, string client, string site, string assignedUser, string role, string status, string action,
            string intent, DateTime created, DateTime? due, DateTime? sla, decimal value, ActionCenterPriority priority,
            ActionCenterBucket bucket, bool overdue, bool waiting, string reason, DateTime now, int? assignedEmployeeId = null)
        {
            var item = new ActionCenterItem
            {
                ActionId = type + ":" + module + ":" + id, ActionType = type, SourceModule = module, SourceRecordId = id,
                ClientId = clientId, SiteId = siteId, Reference = reference, ClientName = client, SiteName = site, Title = title,
                Explanation = explanation, AssignedUser = assignedUser, AssignedEmployeeId = assignedEmployeeId, AssignedRole = role, CurrentStatus = status,
                SuggestedAction = action, DeepLinkIntent = intent, CreatedAt = created == default(DateTime) ? now : created,
                DueAt = due, SlaDeadline = sla, FinancialValue = value, Priority = priority, Bucket = bucket, IsOverdue = overdue,
                IsWaiting = waiting, PriorityReason = reason
            };
            item.PriorityScore = Score(item, now);
            return item;
        }

        private static int Score(ActionCenterItem item, DateTime now)
        {
            int score = item.Priority == ActionCenterPriority.Critical ? 1000 : item.Priority == ActionCenterPriority.Urgent ? 700 :
                item.Priority == ActionCenterPriority.Today ? 430 : 180;
            if (item.IsOverdue)
            {
                int days = item.DueAt.HasValue ? Math.Max(1, (now.Date - item.DueAt.Value.Date).Days) : 1;
                score += 180 + Math.Min(250, days * 15);
            }
            if (item.SlaDeadline.HasValue)
            {
                double hours = (item.SlaDeadline.Value - now).TotalHours;
                score += hours <= 0 ? 400 : (hours <= 2 ? 300 : (hours <= 8 ? 120 : 0));
            }
            if (item.DueAt.HasValue && item.DueAt.Value.Date == now.Date) score += 100;
            score += (int)Math.Min(120m, Math.Max(0m, item.FinancialValue) / 10000m);
            if (item.IsWaiting) score -= 80;
            return score;
        }

        private static bool IsRoleRelevant(ActionCenterItem item, AppUserDto user)
        {
            string role = Normalize(user == null ? null : user.RoleName);
            if (role.Length == 0 || Has(role, "admin") || Has(role, "manager") || Has(role, "owner")) return true;
            if (Has(role, "technician"))
                return Any(item.SourceModule, "Jobs", "ServiceDesk") && IsAssignedToUser(item, user);
            if (Has(role, "dispatcher") || Has(role, "coordinator") || Has(role, "supervisor"))
                return Any(item.SourceModule, "Jobs", "ServiceDesk", "Contracts", "Inventory");
            if (Has(role, "account") || Has(role, "procurement") || Has(role, "purchase"))
                return Any(item.SourceModule, "Invoices", "Payments", "Purchases", "Inventory") ||
                    item.ActionType == "InvoiceCompletedJob" || item.ActionType == "ProcureJobPart";
            if (Has(role, "sales") || Has(role, "estimation"))
                return Any(item.SourceModule, "Quotations", "Contracts", "Clients");
            return true;
        }

        private static bool HasModulePermission(string module, AppUserDto user)
        {
            if (user == null || user.Permissions == null || user.Permissions.Count == 0) return true;
            string key = Any(module, "Jobs", "ServiceDesk") ? "WorkOrders" : module;
            RolePermissionDto permission;
            return user.Permissions.TryGetValue(key, out permission) && permission != null && permission.CanView;
        }

        private static bool IsMine(ActionCenterItem item, AppUserDto user)
        {
            if (IsAssignedToUser(item, user)) return true;
            string role = Normalize(user == null ? null : user.RoleName);
            string assigned = Normalize(item.AssignedRole);
            if (Has(role, "account") && Has(assigned, "account")) return true;
            if ((Has(role, "dispatcher") || Has(role, "coordinator") || Has(role, "supervisor")) && Has(assigned, "dispatch")) return true;
            if ((Has(role, "sales") || Has(role, "estimation")) && Has(assigned, "sales")) return true;
            if (Has(role, "procurement") && Has(assigned, "procurement")) return true;
            return Has(role, "admin") || Has(role, "manager") || Has(role, "owner");
        }

        private static bool IsOperationsRole(AppUserDto user)
        {
            string role = Normalize(user == null ? null : user.RoleName);
            return Has(role, "admin") || Has(role, "manager") || Has(role, "owner") || Has(role, "dispatcher") ||
                Has(role, "coordinator") || Has(role, "supervisor");
        }

        private static string WorkspaceName(string role)
        {
            string value = Normalize(role);
            if (Has(value, "technician")) return "Technician workspace";
            if (Has(value, "dispatcher") || Has(value, "coordinator") || Has(value, "supervisor")) return "Service coordination workspace";
            if (Has(value, "account") || Has(value, "procurement")) return "Accounts & procurement workspace";
            if (Has(value, "sales") || Has(value, "estimation")) return "Sales & estimation workspace";
            return "Operational command center";
        }

        private static bool NameMatches(string name, AppUserDto user)
        {
            return !string.IsNullOrWhiteSpace(name) && user != null &&
                (Normalize(name) == Normalize(user.DisplayName) || Normalize(name) == Normalize(user.Username));
        }

        private static bool IsAssignedToUser(ActionCenterItem item, AppUserDto user)
        {
            if (item == null || user == null) return false;
            if (item.AssignedEmployeeId.HasValue && user.EmployeeId.HasValue)
                return item.AssignedEmployeeId.Value == user.EmployeeId.Value;
            return NameMatches(item.AssignedUser, user);
        }

        private static bool EmployeeMatches(int? employeeId, string employeeName, AppUserDto user)
        {
            if (user == null) return false;
            if (employeeId.HasValue && user.EmployeeId.HasValue)
                return employeeId.Value == user.EmployeeId.Value;
            return NameMatches(employeeName, user);
        }

        private static bool Any(string actual, params string[] expected)
        {
            return expected.Any(value => string.Equals((actual ?? string.Empty).Trim(), value, StringComparison.OrdinalIgnoreCase));
        }

        private static bool Has(string source, string value)
        {
            return (source ?? string.Empty).IndexOf(value ?? string.Empty, StringComparison.OrdinalIgnoreCase) >= 0;
        }

        private static string Normalize(string value) { return (value ?? string.Empty).Trim().ToLowerInvariant(); }
        private static string First(params string[] values) { return values.FirstOrDefault(value => !string.IsNullOrWhiteSpace(value)) ?? string.Empty; }
        private static string Duration(TimeSpan value)
        {
            if (value.TotalMinutes <= 0) return "now";
            if (value.TotalHours < 1) return Math.Max(1, (int)Math.Ceiling(value.TotalMinutes)) + " minutes";
            return Math.Max(1, (int)Math.Floor(value.TotalHours)) + "h" + (value.Minutes > 0 ? " " + value.Minutes + "m" : string.Empty);
        }
    }
}
