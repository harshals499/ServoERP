using System;
using System.Collections.Generic;
using System.Data.SqlClient;
using System.IO;
using System.Linq;
using System.Text;
using System.Web.Script.Serialization;
using System.Windows.Forms;
using HVAC_Pro_Desktop.DAL;
using HVAC_Pro_Desktop.Models;

namespace HVAC_Pro_Desktop.Services
{
    public sealed class AgentSimulationService
    {
        private const string AgentPrefix = "[AGENT]";
        private const int TimerIntervalMs = SimulationClock.RealSecondsPerSimulatedDay * 1000;
        private static readonly string LogRoot = Path.Combine(@"C:\HVAC_PRO_MSE", "LOGS");
        private static readonly string OutputRoot = Path.Combine(@"C:\HVAC_PRO_MSE", "outputs");
        private static readonly string StatePath = Path.Combine(LogRoot, "AgentState.json");
        private static readonly AgentSimulationService _instance = new AgentSimulationService();

        private readonly object _sync = new object();
        private readonly JavaScriptSerializer _json = new JavaScriptSerializer { MaxJsonLength = int.MaxValue };
        private readonly Random _random = new Random();
        private readonly DatabaseManager _db = new DatabaseManager();
        private readonly ClientRepository _clientRepo = new ClientRepository();
        private readonly VendorRepository _vendorRepo = new VendorRepository();
        private readonly InventoryRepository _inventoryRepo = new InventoryRepository();
        private readonly TenderService _tenderService = new TenderService();
        private readonly InvoiceRepository _invoiceRepo = new InvoiceRepository();
        private readonly PaymentRepository _paymentRepo = new PaymentRepository();
        private readonly PurchaseRepository _purchaseRepo = new PurchaseRepository();
        private readonly JobRepository _jobRepo = new JobRepository();
        private Timer _timer;
        private bool _busy;
        private AgentSimulationState _state;

        public event EventHandler<AgentSimulationProgressEventArgs> ProgressChanged;
        public event EventHandler<AgentSimulationCompletedEventArgs> Completed;

        public static AgentSimulationService Instance
        {
            get { return _instance; }
        }

        public AgentSimulationState CurrentState
        {
            get { lock (_sync) { return _state ?? LoadState(); } }
        }

        private AgentSimulationService()
        {
            Directory.CreateDirectory(LogRoot);
            Directory.CreateDirectory(OutputRoot);
        }

        public void StartOrResume()
        {
            lock (_sync)
            {
                _state = LoadState();
                if (_state == null || _state.IsCompleted)
                    _state = CreateNewState();

                _state.IsRunning = true;
                _state.IsPaused = false;
                SimulationClock.Configure(_state.BaseDate, _state.SimulatedDay);
                EnsureTimer();
                _timer.Start();
                SaveState();
            }

            PublishProgress();
            RunNextDay();
        }

        public void Pause()
        {
            lock (_sync)
            {
                if (_state == null)
                    return;

                _state.IsPaused = true;
                _state.IsRunning = true;
                if (_timer != null)
                    _timer.Stop();
                SaveState();
            }

            PublishProgress();
        }

        public void Resume()
        {
            StartOrResume();
        }

        public void Stop()
        {
            lock (_sync)
            {
                if (_timer != null)
                    _timer.Stop();
                if (_state == null)
                    _state = LoadState();
                if (_state != null)
                {
                    _state.IsRunning = false;
                    _state.IsPaused = false;
                    SaveState();
                }
            }

            PublishProgress();
        }

        public void RunNextDay()
        {
            lock (_sync)
            {
                if (_busy || _state == null || !_state.IsRunning || _state.IsPaused || _state.IsCompleted)
                    return;
                _busy = true;
            }

            try
            {
                ExecuteDay();
            }
            catch (Exception ex)
            {
                AddIssue("Functional", "High", "Simulation day failed: " + ex.Message, "Open logs and inspect the last generated [AGENT] record.");
                AppLogger.LogError("AgentSimulationService.RunNextDay", ex);
            }
            finally
            {
                lock (_sync)
                {
                    _busy = false;
                    SaveState();
                }
            }

            PublishProgress();
        }

        public string BuildLatestReport()
        {
            lock (_sync)
            {
                if (_state == null)
                    _state = LoadState() ?? CreateNewState();
                _state.ReportPath = WriteReport(_state);
                SaveState();
                return _state.ReportPath;
            }
        }

        public void DeleteCreatedAgentData()
        {
            lock (_sync)
            {
                if (_state == null)
                    _state = LoadState();
                if (_state == null)
                    return;

                using (SqlConnection conn = _db.GetConnection())
                {
                    conn.Open();
                    using (SqlTransaction tx = conn.BeginTransaction())
                    {
                        DeleteTrackedRecords(conn, tx);
                        tx.Commit();
                    }
                }

                AddNote("Data", "Deleted tracked [AGENT] records created by this simulation run.");
                _state.Records.Clear();
                _state.IsRunning = false;
                _state.IsPaused = false;
                SaveState();
            }

            PublishProgress();
        }

        private void EnsureTimer()
        {
            if (_timer != null)
                return;

            _timer = new Timer { Interval = TimerIntervalMs };
            _timer.Tick += (s, e) => RunNextDay();
        }

        private AgentSimulationState CreateNewState()
        {
            var state = new AgentSimulationState
            {
                RunId = DateTime.Now.ToString("yyyyMMdd-HHmmss"),
                StartedAt = DateTime.Now,
                BaseDate = DateTime.Today,
                SimulatedDate = DateTime.Today,
                SimulatedDay = 0,
                MaxDays = SimulationClock.MaxSimulatedDays,
                Score = 0,
                LastScore = 0,
                Approach = "Synthetic [AGENT] records only; no broad reads, no real-record mutation."
            };
            foreach (string item in new[] { "Master data", "Quotations", "Jobs", "Invoices", "Payments", "Purchases", "PDF previews", "Observations", "Cleanup ready" })
                state.Checklist[item] = false;
            AddNote(state, "Approach", "Starting isolated simulation run with client/site/vendor/item data tagged " + AgentPrefix + ".");
            return state;
        }

        private AgentSimulationState LoadState()
        {
            try
            {
                if (!File.Exists(StatePath))
                    return null;

                string json = File.ReadAllText(StatePath);
                AgentSimulationState state = _json.Deserialize<AgentSimulationState>(json);
                if (state != null)
                    SimulationClock.Configure(state.BaseDate, state.SimulatedDay);
                return state;
            }
            catch (Exception ex)
            {
                AppLogger.LogError("AgentSimulationService.LoadState", ex);
                return null;
            }
        }

        private void SaveState()
        {
            Directory.CreateDirectory(LogRoot);
            if (_state != null)
                File.WriteAllText(StatePath, _json.Serialize(_state));
        }

        private void ExecuteDay()
        {
            lock (_sync)
            {
                if (_state.SimulatedDay >= _state.MaxDays)
                {
                    CompleteSimulation();
                    return;
                }

                if (!HasRecords("B2BClients"))
                    EnsureAgentMasterData();

                SimulationClock.Configure(_state.BaseDate, _state.SimulatedDay);
                _state.SimulatedDate = SimulationClock.AdvanceOneDay();
                _state.SimulatedDay = SimulationClock.SimulatedDay;
            }

            int day = _state.SimulatedDay;
            CreateQuotation(day);
            if (day % 2 == 0)
                CreateJob(day);
            if (day % 3 == 0)
                CreateInvoice(day);
            if (day % 4 == 0)
                CreatePayment(day);
            if (day % 5 == 0)
                CreatePurchase(day);

            UpdateScoreAndChecklist();
            AddObservationSweep(day);

            if (_state.SimulatedDay >= _state.MaxDays || _state.Score >= 90)
                CompleteSimulation();
        }

        private void EnsureAgentMasterData()
        {
            for (int i = 1; i <= 3; i++)
            {
                var client = new B2BClient
                {
                    CompanyName = AgentPrefix + " HVAC Client " + i.ToString("D2"),
                    IndustryType = i == 1 ? "Manufacturing" : i == 2 ? "Healthcare" : "Commercial",
                    PrimaryContact = "Agent Contact " + i,
                    Phone = "90000000" + i.ToString("D2"),
                    Email = "agent.client" + i + "@servoerp.local",
                    GSTNumber = "27AAAAA000" + i + "A1Z5",
                    PANNumber = "AAAAA000" + i + "A",
                    PaymentTermsDays = 30,
                    CreditLimit = 250000m,
                    BillingAddress = AgentPrefix + " Billing Address, Thane, Maharashtra",
                    City = "Thane",
                    CustomerSince = SimulationClock.Today,
                    RelationshipStage = "Simulation",
                    Tags = AgentPrefix + ", simulation",
                    HealthScore = 80,
                    Notes = AgentPrefix + " created by Agent Simulation"
                };
                int clientId = _clientRepo.Create(client);
                Track("Clients", "B2BClients", clientId, "CLIENT-" + i.ToString("D2"), client.CompanyName, null);

                var site = new ClientSite
                {
                    ClientID = clientId,
                    SiteName = AgentPrefix + " Site " + i.ToString("D2"),
                    Address = AgentPrefix + " Service Site, Ghodbunder Road, Thane",
                    City = "Thane",
                    ACSystemCount = 12 + i,
                    RefrigerationSystemCount = 2,
                    CoolingTowerCount = 1,
                    IsCritical = i == 1
                };
                int siteId = _clientRepo.CreateSite(site);
                Track("Sites", "ClientSites", siteId, "SITE-" + i.ToString("D2"), site.SiteName, null);
            }

            for (int i = 1; i <= 2; i++)
            {
                var vendor = new Vendor
                {
                    VendorName = AgentPrefix + " Vendor " + i.ToString("D2"),
                    GSTNumber = "27BBBBB000" + i + "B1Z5",
                    DefaultCreditDays = 30,
                    PANNumber = "BBBBB000" + i + "B",
                    Phone = "91111111" + i.ToString("D2"),
                    Email = "agent.vendor" + i + "@servoerp.local",
                    Address = AgentPrefix + " Vendor Address, Mumbai",
                    City = "Mumbai",
                    Category = i == 1 ? "HVAC Spares" : "Electrical",
                    VendorType = "Supplier",
                    MSMERegistered = "Yes",
                    GSTRegistrationType = "Regular",
                    IsActive = true,
                    Notes = AgentPrefix + " created by Agent Simulation"
                };
                int vendorId = _vendorRepo.Create(vendor);
                Track("Vendors", "Vendors", vendorId, "VENDOR-" + i.ToString("D2"), vendor.VendorName, null);
            }

            int preferredVendor = FirstId("Vendors");
            string[] items = { "Copper pipe 1/4 inch", "Compressor replacement kit", "Preventive maintenance labour" };
            for (int i = 0; i < items.Length; i++)
            {
                var item = new StockItem
                {
                    ItemName = AgentPrefix + " " + items[i],
                    Category = i == 2 ? "Service" : "Spares",
                    CurrentStock = 10 + i,
                    Unit = i == 2 ? "Visit" : "Nos",
                    LastPurchaseRate = 650 + (i * 450),
                    ReorderLevel = 3,
                    VendorID = preferredVendor
                };
                int itemId = _inventoryRepo.Create(item);
                Track("Inventory", "StockItems", itemId, "ITEM-" + (i + 1).ToString("D2"), item.ItemName, null);
            }

            _state.Checklist["Master data"] = true;
            AddNote("Data", "Created isolated [AGENT] clients, sites, vendors, and stock items.");
        }

        private void CreateQuotation(int day)
        {
            int clientId = PickId("B2BClients");
            int siteId = day % 2 == 0 ? 0 : PickId("ClientSites");
            decimal taxable = 12000m + (day * 725m);
            decimal gst = Math.Round(taxable * 0.18m, 2);
            string number = AgentPrefix + " QTN-" + _state.RunId + "-" + day.ToString("D3");
            var bid = new TenderBid
            {
                QuotationNumber = number,
                TenderName = AgentPrefix + " HVAC quotation day " + day,
                ClientID = clientId,
                SiteID = siteId,
                ClientName = AgentPrefix + " Simulated client",
                SystemCount = 1 + (day % 4),
                DueDate = SimulationClock.Today.AddDays(7),
                SubmittedDate = SimulationClock.Today,
                RequiredByDate = SimulationClock.Today.AddDays(10),
                Status = Pick(new[] { "Draft", "Submitted", "Won", "Lost" }),
                RequirementCategory = "Service",
                ItemName = AgentPrefix + " HVAC scope",
                RequiredQuantity = 1,
                Unit = "Nos",
                BidValue = taxable + gst,
                TotalTaxableValue = taxable,
                TotalGSTAmount = gst,
                TotalWithGST = taxable + gst,
                AverageMarginPct = 22m,
                IsMultiLine = true,
                CommercialFlow = "Revenue",
                CustomerDocumentStatus = "Quote Draft",
                SupplierDocumentStatus = "Not Required",
                Notes = AgentPrefix + " simulation quotation. Site optional test: " + (siteId > 0 ? "site selected" : "client-only quotation")
            };
            bid.LineItems.Add(BuildTenderLine(1, "Preventive maintenance visit", 1, 6500m));
            bid.LineItems.Add(BuildTenderLine(2, "Consumables and minor spares", 1, taxable - 6500m));
            int id = _tenderService.SaveTenderBid(bid);
            string pdf = WriteDocumentPdf("Quotations", number, "Quotation", number, taxable + gst, bid.Status);
            Track("Quotations", "Quotations", id, number, bid.TenderName, pdf);
            _state.Checklist["Quotations"] = true;
        }

        private TenderBidLineItem BuildTenderLine(int order, string description, decimal quantity, decimal rate)
        {
            decimal taxable = Math.Round(quantity * rate, 2);
            decimal gst = Math.Round(taxable * 0.18m, 2);
            return new TenderBidLineItem
            {
                SortOrder = order,
                Category = "Service",
                ItemDescription = AgentPrefix + " " + description,
                Quantity = quantity,
                Unit = "Nos",
                HsnSacCode = "998519",
                GSTRatePct = 18m,
                CostPerUnit = Math.Round(rate * 0.72m, 2),
                SellPricePerUnit = rate,
                TaxableLineTotal = taxable,
                GSTAmount = gst,
                MarginPct = 28m,
                AnalysisStatus = "Simulated",
                AnalysisNotes = AgentPrefix + " generated line",
                IsSellPriceManual = true,
                CreatedDate = SimulationClock.Now,
                ModifiedDate = SimulationClock.Now
            };
        }

        private void CreateJob(int day)
        {
            int clientId = PickId("B2BClients");
            int siteId = day % 4 == 0 ? 0 : PickId("ClientSites");
            string number = AgentPrefix + " JOB-" + _state.RunId + "-" + day.ToString("D3");
            var job = new Job
            {
                JobNumber = number,
                ClientID = clientId,
                SiteID = siteId,
                Title = AgentPrefix + " Service call day " + day,
                JobTitle = AgentPrefix + " Service call day " + day,
                Description = AgentPrefix + " simulated work order with optional site path.",
                ScheduledDate = SimulationClock.Today.AddDays(1),
                CompletedDate = day % 6 == 0 ? (DateTime?)SimulationClock.Today.AddDays(2) : null,
                Priority = Pick(new[] { "Low", "Medium", "High", "Critical" }),
                Status = day % 6 == 0 ? "Completed" : "Pending",
                PipelineStatus = day % 6 == 0 ? "Closed" : "Created",
                JobType = Pick(new[] { "Installation", "Maintenance", "Repair", "Inspection" }),
                EstimatedCost = 3500m + day * 100m,
                Revenue = 7500m + day * 250m,
                QuotedRevenue = 7500m + day * 250m,
                ActualRevenue = day % 6 == 0 ? 7500m + day * 250m : 0m,
                Notes = AgentPrefix + " simulation job. Site optional test: " + (siteId > 0 ? "site selected" : "client-only job"),
                CreatedByName = "Agent Simulation"
            };
            int id = _jobRepo.Create(job);
            Track("Jobs", "Jobs", id, number, job.JobTitle, null);
            _state.Checklist["Jobs"] = true;
        }

        private void CreateInvoice(int day)
        {
            int clientId = PickId("B2BClients");
            int siteId = day % 6 == 0 ? 0 : PickId("ClientSites");
            int quoteId = PickId("Quotations");
            decimal subTotal = 18000m + day * 610m;
            decimal tax = Math.Round(subTotal * 0.18m, 2);
            string number = AgentPrefix + " INV-" + _state.RunId + "-" + day.ToString("D3");
            var invoice = new Invoice
            {
                ContractID = 0,
                ClientID = clientId,
                SiteID = siteId,
                QuotationBidID = quoteId > 0 ? (int?)quoteId : null,
                InvoiceNumber = number,
                InvoiceDate = SimulationClock.Today,
                DueDate = SimulationClock.Today.AddDays(30),
                SubTotal = subTotal,
                GSTPercent = 18m,
                TaxAmount = tax,
                TotalAmount = subTotal + tax,
                PaidAmount = 0m,
                BalanceDue = subTotal + tax,
                PaymentStatus = Pick(new[] { "Draft", "Pending", "Overdue" }),
                InvoiceTitle = "TAX INVOICE",
                Subject = AgentPrefix + " HVAC service invoice",
                Notes = AgentPrefix + " simulation invoice. Site optional test: " + (siteId > 0 ? "site selected" : "client-only invoice"),
                WorkflowType = "Service",
                GSTMode = "CGST/SGST",
                PaymentTerms = "30 Days",
                PlaceOfSupply = "Maharashtra",
                CGSTAmount = Math.Round(tax / 2m, 2),
                SGSTAmount = Math.Round(tax / 2m, 2),
                InventoryReservationStatus = "None",
                CreatedByName = "Agent Simulation"
            };
            invoice.LineItems.Add(new InvoiceLineItem
            {
                Description = AgentPrefix + " HVAC service and consumables",
                HSNCode = "998519",
                Category = "Service",
                Unit = "Nos",
                Quantity = 1m,
                Rate = subTotal,
                GSTPercent = 18m,
                TaxType = "Taxable",
                TaxAmount = tax,
                IsBillable = true,
                Amount = subTotal
            });
            int id = _invoiceRepo.Create(invoice);
            string pdf = WriteDocumentPdf("Invoices", number, "Invoice", number, invoice.TotalAmount, invoice.PaymentStatus);
            Track("Invoices", "Invoices", id, number, invoice.Subject, pdf);
            _state.Checklist["Invoices"] = true;
        }

        private void CreatePayment(int day)
        {
            int invoiceId = PickId("Invoices");
            if (invoiceId <= 0)
                return;

            Invoice invoice = _invoiceRepo.GetById(invoiceId);
            if (invoice == null || invoice.BalanceDue <= 0)
                return;

            decimal amount = day % 8 == 0 ? invoice.BalanceDue : Math.Round(invoice.BalanceDue / 2m, 2);
            string number = AgentPrefix + " PAY-" + _state.RunId + "-" + day.ToString("D3");
            var payment = new Payment
            {
                PaymentNumber = number,
                InvoiceID = invoice.InvoiceID,
                ClientID = invoice.ClientID,
                AmountPaid = amount,
                PaymentDate = SimulationClock.Today,
                PaymentMode = Pick(new[] { "Bank Transfer", "UPI", "Cheque" }),
                ReferenceNumber = AgentPrefix + " REF-" + day.ToString("D3"),
                Notes = AgentPrefix + " simulation payment",
                CreatedByName = "Agent Simulation"
            };
            int id = _paymentRepo.Create(payment);
            decimal paid = Math.Min(invoice.TotalAmount, invoice.PaidAmount + amount);
            string status = paid >= invoice.TotalAmount ? "Paid" : "Partial";
            _invoiceRepo.UpdatePaymentStatus(invoice.InvoiceID, paid, status);
            Track("Payments", "Payments", id, number, "Payment against " + invoice.InvoiceNumber, null);
            _state.Checklist["Payments"] = true;
        }

        private void CreatePurchase(int day)
        {
            int vendorId = PickId("Vendors");
            int clientId = PickId("B2BClients");
            int siteId = day % 10 == 0 ? 0 : PickId("ClientSites");
            int bidId = PickId("Quotations");
            decimal amount = 9000m + day * 350m;
            string number = AgentPrefix + " PO-" + _state.RunId + "-" + day.ToString("D3");
            var po = new PurchaseOrder
            {
                VendorID = vendorId,
                ClientID = clientId,
                SiteID = siteId,
                RecommendedByBidID = bidId > 0 ? (int?)bidId : null,
                PONumber = number,
                PODate = SimulationClock.Today,
                PayByDate = SimulationClock.Today.AddDays(30),
                LinkedToType = "General",
                DeliveryMode = siteId > 0 ? "SiteDelivery" : "TechPickup",
                DeliveryAddress = siteId > 0 ? AgentPrefix + " simulated site address" : string.Empty,
                TotalAmount = amount,
                PaidAmount = day % 15 == 0 ? amount : 0m,
                Status = day % 15 == 0 ? "Paid" : "Pending",
                PaymentReference = day % 15 == 0 ? AgentPrefix + " ADV-" + day.ToString("D3") : string.Empty,
                ComparisonNotes = AgentPrefix + " simulation purchase linked to quotation shortfall",
                Notes = AgentPrefix + " simulation purchase. Site optional test: " + (siteId > 0 ? "site selected" : "client-only purchase"),
                CreatedByName = "Agent Simulation",
                CreatedByDate = SimulationClock.Now
            };
            po.LineItems.Add(new PurchaseLineItem
            {
                InventoryItemId = PickId("StockItems"),
                Description = AgentPrefix + " Compressor spares",
                HsnSacCode = "8415",
                Quantity = 1m,
                UOM = "Nos",
                Rate = amount,
                GSTRate = 18m,
                CGSTRate = 9m,
                SGSTRate = 9m,
                IGSTRate = 0m,
                JobLink = "General",
                Amount = amount
            });
            int id = _purchaseRepo.Create(po);
            string pdf = WriteDocumentPdf("Purchases", number, "Purchase Order", number, amount, po.Status);
            Track("Purchases", "PurchaseOrders", id, number, "Purchase order " + number, pdf);
            _state.Checklist["Purchases"] = true;
        }

        private void UpdateScoreAndChecklist()
        {
            int moduleScore = _state.Checklist.Count(kv => kv.Value) * 8;
            int recordScore = Math.Min(22, _state.Records.Count / 2);
            int pdfScore = Math.Min(18, _state.Records.Count(r => !string.IsNullOrWhiteSpace(r.PdfPath)) * 2);
            int dayScore = Math.Min(20, _state.SimulatedDay / 3);
            int issuePenalty = Math.Min(20, _state.Issues.Count(i => string.Equals(i.Severity, "High", StringComparison.OrdinalIgnoreCase)) * 6);
            int score = Math.Max(0, Math.Min(100, moduleScore + recordScore + pdfScore + dayScore - issuePenalty));

            if (score <= _state.LastScore)
                _state.StalledDays++;
            else
                _state.StalledDays = 0;
            _state.LastScore = _state.Score;
            _state.Score = score;
            _state.Checklist["PDF previews"] = _state.Records.Any(r => !string.IsNullOrWhiteSpace(r.PdfPath));
            _state.Checklist["Observations"] = _state.Notes.Count >= 10;
            _state.Checklist["Cleanup ready"] = _state.Records.Count > 0;

            if (_state.StalledDays >= 5)
            {
                AddIssue("Stall", "Medium", "Simulation score has not improved for five simulated days.", "Check whether all modules are still creating records.");
                _state.StalledDays = 0;
            }
        }

        private void AddObservationSweep(int day)
        {
            AddNote("Functional", "Day " + day + ": created/checked sales, operations, billing, payment, and purchase flows where scheduled.");
            AddNote("Data", "All generated records are tagged " + AgentPrefix + " and tracked by exact table/id in AgentState.json.");
            AddNote("UI", "Live panel/status bar updated after the simulated day tick.");
            AddNote("UX", "Simulation uses client-only records on alternating days to exercise optional site workflows.");
            AddNote("Consistency", "Numbers use India-first document naming and DD/MM/YYYY report output.");
            AddNote("Resolution", _state.Issues.Count == 0 ? "No blocking issue recorded for this tick." : "Issue list retained for final report.");
        }

        private void CompleteSimulation()
        {
            _state.IsRunning = false;
            _state.IsPaused = false;
            _state.IsCompleted = true;
            if (_timer != null)
                _timer.Stop();
            _state.ReportPath = WriteReport(_state);
            SaveState();
            OnCompleted(_state.ReportPath);
        }

        private string WriteReport(AgentSimulationState state)
        {
            Directory.CreateDirectory(LogRoot);
            string path = Path.Combine(LogRoot, "AgentReport_" + DateTime.Now.ToString("yyyyMMdd_HHmmss") + ".txt");
            var sb = new StringBuilder();
            sb.AppendLine("ServoERP Agent Simulation Report");
            sb.AppendLine("Run ID        : " + state.RunId);
            sb.AppendLine("Started       : " + state.StartedAt.ToString("dd/MM/yyyy HH:mm"));
            sb.AppendLine("Simulated Day : " + state.SimulatedDay + "/" + state.MaxDays);
            sb.AppendLine("Sim Date      : " + state.SimulatedDate.ToString("dd/MM/yyyy"));
            sb.AppendLine("Score         : " + state.Score + "/100");
            sb.AppendLine("Approach      : " + state.Approach);
            sb.AppendLine();
            sb.AppendLine("Checklist");
            foreach (var item in state.Checklist.OrderBy(k => k.Key))
                sb.AppendLine(" - " + item.Key + ": " + (item.Value ? "PASS" : "PENDING"));
            sb.AppendLine();
            sb.AppendLine("Records Created");
            foreach (var group in state.Records.GroupBy(r => r.Module).OrderBy(g => g.Key))
                sb.AppendLine(" - " + group.Key + ": " + group.Count());
            sb.AppendLine();
            sb.AppendLine("Issues");
            if (state.Issues.Count == 0)
                sb.AppendLine(" - None recorded.");
            foreach (AgentSimulationIssue issue in state.Issues)
                sb.AppendLine(" - [" + issue.Severity + "] " + issue.Category + ": " + issue.Message + " | Fix: " + issue.ResolutionHint);
            sb.AppendLine();
            sb.AppendLine("Observation Notes");
            foreach (AgentSimulationNote note in state.Notes.Take(400))
                sb.AppendLine(" - Day " + note.SimulatedDay + " | " + note.Category + " | " + note.Message);
            File.WriteAllText(path, sb.ToString());
            return path;
        }

        private string WriteDocumentPdf(string folderName, string fileStem, string title, string number, decimal amount, string status)
        {
            string folder = Path.Combine(OutputRoot, folderName);
            Directory.CreateDirectory(folder);
            string safe = SanitizeFileName(fileStem);
            string path = Path.Combine(folder, safe + ".pdf");
            var lines = new List<string>
            {
                "ServoERP Agent Simulation",
                title + ": " + number,
                "Date: " + SimulationClock.Today.ToString("dd/MM/yyyy"),
                "Amount: " + IndiaFormatHelper.FormatCurrency(amount),
                "Status: " + status,
                "Source: " + AgentPrefix + " synthetic record"
            };
            SimplePdfWriter.Write(path, title, lines);
            _state.Checklist["PDF previews"] = true;
            return path;
        }

        private void DeleteTrackedRecords(SqlConnection conn, SqlTransaction tx)
        {
            DeleteByIds(conn, tx, "DELETE FROM Payments WHERE PaymentID IN ({0})", Ids("Payments"));
            DeleteByIds(conn, tx, "DELETE FROM InvoiceInventoryReservations WHERE InvoiceID IN ({0})", Ids("Invoices"));
            DeleteByIds(conn, tx, "DELETE FROM InvoiceLineItems WHERE InvoiceID IN ({0})", Ids("Invoices"));
            DeleteByIds(conn, tx, "DELETE FROM Invoices WHERE InvoiceID IN ({0})", Ids("Invoices"));
            DeleteByIds(conn, tx, "DELETE FROM PendingCharges WHERE SourcePoId IN ({0})", Ids("PurchaseOrders"));
            DeleteByIds(conn, tx, "DELETE FROM PurchaseLineItems WHERE POID IN ({0})", Ids("PurchaseOrders"));
            DeleteByIds(conn, tx, "DELETE FROM PurchaseOrders WHERE POID IN ({0})", Ids("PurchaseOrders"));
            DeleteByIds(conn, tx, "DELETE FROM QuotationLineItems WHERE TenderBidId IN ({0})", Ids("Quotations"));
            DeleteByIds(conn, tx, "DELETE FROM Quotations WHERE BidID IN ({0})", Ids("Quotations"));
            DeleteByIds(conn, tx, "DELETE FROM PendingCharges WHERE WorkOrderId IN ({0})", Ids("Jobs"));
            DeleteByIds(conn, tx, "DELETE FROM JobActivityLog WHERE JobId IN ({0})", Ids("Jobs"));
            DeleteByIds(conn, tx, "DELETE FROM JobPartsUsed WHERE JobId IN ({0})", Ids("Jobs"));
            DeleteByIds(conn, tx, "DELETE FROM JobChecklistItems WHERE JobId IN ({0})", Ids("Jobs"));
            DeleteByIds(conn, tx, "DELETE FROM Jobs WHERE JobID IN ({0})", Ids("Jobs"));
            DeleteByIds(conn, tx, "DELETE FROM StockMovements WHERE ItemID IN ({0})", Ids("StockItems"));
            DeleteByIds(conn, tx, "DELETE FROM StockItems WHERE ItemID IN ({0})", Ids("StockItems"));
            DeleteByIds(conn, tx, "DELETE FROM ClientSites WHERE SiteID IN ({0})", Ids("ClientSites"));
            DeleteByIds(conn, tx, "DELETE FROM B2BClients WHERE ClientID IN ({0})", Ids("B2BClients"));
            DeleteByIds(conn, tx, "DELETE FROM Vendors WHERE VendorID IN ({0})", Ids("Vendors"));
            AppDataCache.RemovePrefix("tenders:");
            AppDataCache.RemovePrefix("invoices:");
            AppDataCache.RemovePrefix("payments:");
            AppDataCache.RemovePrefix("purchases:");
            AppDataCache.RemovePrefix("jobs:");
            AppDataCache.RemovePrefix("clients:");
            AppDataCache.RemovePrefix("vendors:");
            AppDataCache.RemovePrefix("inventory:");
        }

        private void DeleteByIds(SqlConnection conn, SqlTransaction tx, string sqlFormat, List<int> ids)
        {
            if (ids == null || ids.Count == 0)
                return;

            string[] names = ids.Select((id, index) => "@id" + index).ToArray();
            using (SqlCommand cmd = new SqlCommand(string.Format(sqlFormat, string.Join(",", names)), conn, tx))
            {
                for (int i = 0; i < ids.Count; i++)
                    cmd.Parameters.AddWithValue(names[i], ids[i]);
                cmd.ExecuteNonQuery();
            }
        }

        private List<int> Ids(string tableName)
        {
            return _state.Records
                .Where(r => string.Equals(r.TableName, tableName, StringComparison.OrdinalIgnoreCase))
                .Select(r => r.Id)
                .Where(id => id > 0)
                .Distinct()
                .ToList();
        }

        private bool HasRecords(string tableName)
        {
            return _state != null && _state.Records.Any(r => string.Equals(r.TableName, tableName, StringComparison.OrdinalIgnoreCase));
        }

        private int FirstId(string tableName)
        {
            AgentRecordRef record = _state.Records.FirstOrDefault(r => string.Equals(r.TableName, tableName, StringComparison.OrdinalIgnoreCase));
            return record == null ? 0 : record.Id;
        }

        private int PickId(string tableName)
        {
            List<int> ids = Ids(tableName);
            if (ids.Count == 0)
                return 0;
            return ids[_random.Next(ids.Count)];
        }

        private string Pick(string[] values)
        {
            if (values == null || values.Length == 0)
                return string.Empty;
            return values[_random.Next(values.Length)];
        }

        private void Track(string module, string tableName, int id, string number, string label, string pdfPath)
        {
            _state.Records.Add(new AgentRecordRef
            {
                Module = module,
                TableName = tableName,
                Id = id,
                Number = number,
                Label = label,
                PdfPath = pdfPath,
                CreatedOn = SimulationClock.Now
            });
            AddNote("Data", "Created " + module + " record " + number + " (ID " + id + ").");
        }

        private void AddIssue(string category, string severity, string message, string resolutionHint)
        {
            lock (_sync)
            {
                if (_state == null)
                    return;
                _state.Issues.Add(new AgentSimulationIssue
                {
                    LoggedAt = DateTime.Now,
                    SimulatedDay = _state.SimulatedDay,
                    Category = category,
                    Severity = severity,
                    Message = message,
                    ResolutionHint = resolutionHint
                });
            }
        }

        private void AddNote(string category, string message)
        {
            AddNote(_state, category, message);
        }

        private static void AddNote(AgentSimulationState state, string category, string message)
        {
            if (state == null)
                return;
            state.Notes.Add(new AgentSimulationNote
            {
                LoggedAt = DateTime.Now,
                SimulatedDay = state.SimulatedDay,
                Category = category,
                Message = message
            });
        }

        private void PublishProgress()
        {
            EventHandler<AgentSimulationProgressEventArgs> handler = ProgressChanged;
            if (handler != null)
                handler(this, new AgentSimulationProgressEventArgs(CurrentState));
        }

        private void OnCompleted(string reportPath)
        {
            EventHandler<AgentSimulationCompletedEventArgs> handler = Completed;
            if (handler != null)
                handler(this, new AgentSimulationCompletedEventArgs(_state, reportPath));
        }

        private static string SanitizeFileName(string value)
        {
            string safe = value ?? "AgentDocument";
            foreach (char c in Path.GetInvalidFileNameChars())
                safe = safe.Replace(c, '-');
            return safe.Replace("[", "").Replace("]", "").Replace(" ", "_");
        }

        private static class SimplePdfWriter
        {
            public static void Write(string path, string title, IList<string> lines)
            {
                string text = string.Join("\\n", (lines ?? new List<string>()).Select(EscapeText));
                string content = "BT /F1 14 Tf 50 780 Td (" + EscapeText(title ?? "ServoERP Document") + ") Tj " +
                                 "/F1 10 Tf 0 -28 Td (" + text + ") Tj ET";
                byte[] contentBytes = Encoding.ASCII.GetBytes(content);
                var objects = new List<string>
                {
                    "1 0 obj << /Type /Catalog /Pages 2 0 R >> endobj\n",
                    "2 0 obj << /Type /Pages /Kids [3 0 R] /Count 1 >> endobj\n",
                    "3 0 obj << /Type /Page /Parent 2 0 R /MediaBox [0 0 595 842] /Resources << /Font << /F1 4 0 R >> >> /Contents 5 0 R >> endobj\n",
                    "4 0 obj << /Type /Font /Subtype /Type1 /BaseFont /Helvetica >> endobj\n",
                    "5 0 obj << /Length " + contentBytes.Length + " >> stream\n" + content + "\nendstream endobj\n"
                };
                using (FileStream fs = new FileStream(path, FileMode.Create, FileAccess.Write))
                {
                    WriteAscii(fs, "%PDF-1.4\n");
                    var offsets = new List<long> { 0 };
                    foreach (string obj in objects)
                    {
                        offsets.Add(fs.Position);
                        WriteAscii(fs, obj);
                    }
                    long xref = fs.Position;
                    WriteAscii(fs, "xref\n0 " + (objects.Count + 1) + "\n0000000000 65535 f \n");
                    for (int i = 1; i < offsets.Count; i++)
                        WriteAscii(fs, offsets[i].ToString("D10") + " 00000 n \n");
                    WriteAscii(fs, "trailer << /Size " + (objects.Count + 1) + " /Root 1 0 R >>\nstartxref\n" + xref + "\n%%EOF");
                }
            }

            private static void WriteAscii(Stream stream, string text)
            {
                byte[] bytes = Encoding.ASCII.GetBytes(text);
                stream.Write(bytes, 0, bytes.Length);
            }

            private static string EscapeText(string value)
            {
                return (value ?? string.Empty)
                    .Replace("\\", "\\\\")
                    .Replace("(", "\\(")
                    .Replace(")", "\\)")
                    .Replace("\r", " ")
                    .Replace("\n", "\\n");
            }
        }
    }
}
