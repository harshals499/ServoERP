using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Windows.Forms;
using HVAC_Pro_Desktop.Models;
using HVAC_Pro_Desktop.Services;
using HVAC_Pro_Desktop.Services.Licensing;
using HVAC_Pro_Desktop.UI;

namespace HVAC_Pro_Desktop.Tests
{
    public static class DashboardRecentActivitySmokeTests
    {
        private const string QaKey = "QA-DASH-RECENTS-20260621";
        private const string SiteName = "QA Dashboard Recent Site";
        private const string ItemName = "QA Dashboard Recent Copper Coil";

        public static List<string> RunAll()
        {
            AppUserDto previousUser = SessionManager.CurrentUser;
            Guid? previousSessionId = SessionManager.CurrentSessionId;
            DateTime? previousExpiry = SessionManager.ExpiresAt;

            var results = new List<string>();
            var created = new CreatedRecords();
            EventRecorder recorder = null;

            try
            {
                EnsureQaLicense();
                SessionManager.SetSession(new AppUserDto
                {
                    UserId = 0,
                    Username = "qa-dashboard-recents",
                    DisplayName = "ServoERP QA",
                    RoleName = "Administrator",
                    IsActive = true
                }, Guid.NewGuid(), DateTime.Now.AddHours(1));

                BaselineData baseline = EnsureBaselineData();
                recorder = new EventRecorder();
                DashboardRefreshService.RefreshRequested += recorder.Handle;

                var jobService = new JobService();
                var tenderService = new TenderService();
                var purchaseService = new PurchaseService();
                var invoiceService = new InvoiceService();

                created.JobId = CreateJob(jobService, baseline);
                Job createdJob = jobService.GetById(created.JobId);
                Assert(createdJob != null && createdJob.JobID > 0, "Job was not created.");
                Assert(recorder.ContainsModule("Jobs"), "Job save did not raise dashboard refresh.");
                AssertDashboardContains(createdJob.JobNumber, "Job");
                AssertDashboardViewOpens("Job", createdJob.JobID);
                results.Add("PASS saved job appears in dashboard recent activity as " + createdJob.JobNumber);

                created.QuotationId = CreateQuotation(tenderService, baseline, createdJob);
                TenderBid createdQuote = tenderService.GetByIdDetailed(created.QuotationId);
                Assert(createdQuote != null && createdQuote.BidID > 0, "Quotation was not created.");
                Assert(recorder.ContainsModule("Quotations"), "Quotation save did not raise dashboard refresh.");
                AssertDashboardContains(createdQuote.QuotationNumber, "Quotation");
                AssertDashboardViewOpens("Quotation", createdQuote.BidID);
                results.Add("PASS saved quotation appears in dashboard recent activity as " + createdQuote.QuotationNumber);

                created.PurchaseOrderId = CreatePurchaseOrder(purchaseService, baseline, createdJob, createdQuote);
                PurchaseOrder createdPo = purchaseService.GetById(created.PurchaseOrderId);
                Assert(createdPo != null && createdPo.POID > 0, "Purchase order was not created.");
                Assert(recorder.ContainsModule("Purchases"), "Purchase order save did not raise dashboard refresh.");
                AssertDashboardContains(createdPo.PONumber, "Purchase");
                AssertDashboardViewOpens("Purchase", createdPo.POID);
                results.Add("PASS saved purchase order appears in dashboard recent activity as " + createdPo.PONumber);

                created.InvoiceId = CreateInvoice(invoiceService, baseline, createdJob, createdQuote);
                Invoice createdInvoice = invoiceService.GetInvoiceById(created.InvoiceId);
                Assert(createdInvoice != null && createdInvoice.InvoiceID > 0, "Invoice was not created.");
                Assert(recorder.ContainsModule("Invoices"), "Invoice save did not raise dashboard refresh.");
                AssertDashboardContains(createdInvoice.InvoiceNumber, "Invoice");
                AssertDashboardViewOpens("Invoice", createdInvoice.InvoiceID);
                results.Add("PASS saved invoice appears in dashboard recent activity as " + createdInvoice.InvoiceNumber);

                results.Add("PASS dashboard refresh notifications fired for Jobs, Quotations, Purchases, and Invoices");
            }
            catch (Exception ex)
            {
                results.Add("FAIL dashboard recent activity smoke | " + ex.GetType().Name + ": " + ex.Message);
            }
            finally
            {
                if (recorder != null)
                    DashboardRefreshService.RefreshRequested -= recorder.Handle;

                Cleanup(created);
                SessionManager.SetSession(previousUser, previousSessionId, previousExpiry);
            }

            return results;
        }

        public static List<string> RunJobsOnly()
        {
            AppUserDto previousUser = SessionManager.CurrentUser;
            Guid? previousSessionId = SessionManager.CurrentSessionId;
            DateTime? previousExpiry = SessionManager.ExpiresAt;

            var results = new List<string>();
            var created = new CreatedRecords();
            EventRecorder recorder = null;

            try
            {
                EnsureQaLicense();
                SessionManager.SetSession(new AppUserDto
                {
                    UserId = 0,
                    Username = "qa-job-recents",
                    DisplayName = "ServoERP QA",
                    RoleName = "Administrator",
                    IsActive = true
                }, Guid.NewGuid(), DateTime.Now.AddHours(1));

                BaselineData baseline = EnsureBaselineData();
                recorder = new EventRecorder();
                DashboardRefreshService.RefreshRequested += recorder.Handle;

                var jobService = new JobService();
                Job existingJob = jobService.GetAll()
                    .Where(j => j != null && j.JobID > 0 && !string.IsNullOrWhiteSpace(j.JobNumber))
                    .OrderByDescending(j => j.ModifiedDate ?? j.CreatedDate)
                    .FirstOrDefault();

                Assert(existingJob != null, "No existing job is available for recent activity verification.");
                AssertDashboardContains(existingJob.JobNumber, "Job");
                AssertDashboardViewOpens("Job", existingJob.JobID);
                results.Add("PASS existing job opens from dashboard recent activity as " + existingJob.JobNumber);

                created.JobId = CreateJob(jobService, baseline);
                Job createdJob = jobService.GetById(created.JobId);
                Assert(createdJob != null && createdJob.JobID > 0, "Job was not created.");
                Assert(recorder.ContainsModule("Jobs"), "Job save did not raise dashboard refresh.");
                AssertDashboardContains(createdJob.JobNumber, "Job");
                AssertDashboardViewOpens("Job", createdJob.JobID);
                results.Add("PASS newly created job opens from dashboard recent activity as " + createdJob.JobNumber);
            }
            catch (Exception ex)
            {
                results.Add("FAIL dashboard job recent activity smoke | " + ex.GetType().Name + ": " + ex.Message);
            }
            finally
            {
                if (recorder != null)
                    DashboardRefreshService.RefreshRequested -= recorder.Handle;

                Cleanup(created);
                SessionManager.SetSession(previousUser, previousSessionId, previousExpiry);
            }

            return results;
        }

        public static List<string> RunPurchasesOnly()
        {
            AppUserDto previousUser = SessionManager.CurrentUser;
            Guid? previousSessionId = SessionManager.CurrentSessionId;
            DateTime? previousExpiry = SessionManager.ExpiresAt;

            var results = new List<string>();
            var created = new CreatedRecords();
            EventRecorder recorder = null;

            try
            {
                EnsureQaLicense();
                SessionManager.SetSession(new AppUserDto
                {
                    UserId = 0,
                    Username = "qa-purchase-recents",
                    DisplayName = "ServoERP QA",
                    RoleName = "Administrator",
                    IsActive = true
                }, Guid.NewGuid(), DateTime.Now.AddHours(1));

                BaselineData baseline = EnsureBaselineData();
                recorder = new EventRecorder();
                DashboardRefreshService.RefreshRequested += recorder.Handle;

                var purchaseService = new PurchaseService();
                PurchaseOrder existingPo = purchaseService.GetAllFresh()
                    .Where(po => po != null && po.POID > 0 && !string.IsNullOrWhiteSpace(po.PONumber))
                    .OrderByDescending(po => po.ModifiedDate ?? po.CreatedByDate ?? po.CreatedDate)
                    .FirstOrDefault();

                Assert(existingPo != null, "No existing purchase order is available for recent activity verification.");
                AssertDashboardContains(existingPo.PONumber, "Purchase");
                AssertDashboardPurchaseViewOpens(existingPo.POID, existingPo.PONumber);
                results.Add("PASS existing purchase order opens from dashboard recent activity as " + existingPo.PONumber);

                var jobService = new JobService();
                var tenderService = new TenderService();
                created.JobId = CreateJob(jobService, baseline);
                Job createdJob = jobService.GetById(created.JobId);
                created.QuotationId = CreateQuotation(tenderService, baseline, createdJob);
                TenderBid createdQuote = tenderService.GetByIdDetailed(created.QuotationId);
                created.PurchaseOrderId = CreatePurchaseOrder(purchaseService, baseline, createdJob, createdQuote);
                PurchaseOrder createdPo = purchaseService.GetById(created.PurchaseOrderId);
                Assert(createdPo != null && createdPo.POID > 0, "Purchase order was not created.");
                Assert(recorder.ContainsModule("Purchases"), "Purchase order save did not raise dashboard refresh.");
                AssertDashboardContains(createdPo.PONumber, "Purchase");
                AssertDashboardPurchaseViewOpens(createdPo.POID, createdPo.PONumber);
                results.Add("PASS newly created purchase order opens from dashboard recent activity as " + createdPo.PONumber);
            }
            catch (Exception ex)
            {
                results.Add("FAIL dashboard purchase recent activity smoke | " + ex.GetType().Name + ": " + ex.Message);
            }
            finally
            {
                if (recorder != null)
                    DashboardRefreshService.RefreshRequested -= recorder.Handle;

                Cleanup(created);
                SessionManager.SetSession(previousUser, previousSessionId, previousExpiry);
            }

            return results;
        }

        private static void EnsureQaLicense()
        {
            var licenseService = new LicenseService();
            LicenseValidationResult current = licenseService.ValidateCurrentLicense();
            if (current != null && current.Success && !current.IsFrozen)
                return;

            LicenseValidationResult trial = licenseService.ActivateTrial("ServoERP QA Dashboard Recents");
            if (trial == null || !trial.Success || trial.IsFrozen)
                throw new InvalidOperationException("QA dashboard-recents license activation failed: " + (trial == null ? "no response" : trial.Message));
        }

        private static BaselineData EnsureBaselineData()
        {
            var baseline = new BaselineData();
            var clientService = new ClientService();
            var siteService = new SiteService();
            var vendorService = new VendorService();
            var inventoryService = new InventoryService();

            baseline.Client = clientService.GetAllClients()
                .FirstOrDefault();
            if (baseline.Client == null)
            {
                throw new InvalidOperationException("No active client is available for dashboard recent activity smoke testing.");
            }

            baseline.Site = siteService.GetByClientId(baseline.Client.ClientID)
                .FirstOrDefault(s => string.Equals(s.SiteName, SiteName, StringComparison.OrdinalIgnoreCase));
            if (baseline.Site == null)
            {
                baseline.Site = new ClientSite
                {
                    ClientID = baseline.Client.ClientID,
                    SiteName = SiteName,
                    Address = "QA Dashboard Plant, Thane",
                    City = "Thane",
                    ACSystemCount = 2,
                    TravelRateINR = 500m
                };
                baseline.Site.SiteID = clientService.CreateSite(baseline.Site);
            }

            baseline.Vendor = vendorService.GetSuppliers()
                .FirstOrDefault();
            if (baseline.Vendor == null)
            {
                throw new InvalidOperationException("No active supplier is available for dashboard recent activity smoke testing.");
            }

            baseline.StockItem = inventoryService.GetAll()
                .FirstOrDefault(i => string.Equals(i.ItemName, ItemName, StringComparison.OrdinalIgnoreCase));
            if (baseline.StockItem == null)
            {
                baseline.StockItem = new StockItem
                {
                    ItemName = ItemName,
                    Category = "Copper",
                    Unit = "Mtr",
                    CurrentStock = 50m,
                    ReorderLevel = 5m,
                    LastPurchaseRate = 180m,
                    VendorID = baseline.Vendor.VendorID,
                    IsActive = true,
                    LastUpdated = DateTime.Now
                };
                baseline.StockItem.ItemID = inventoryService.Create(baseline.StockItem);
            }

            return baseline;
        }

        private static int CreateJob(JobService service, BaselineData baseline)
        {
            int? technicianId = new EmployeeService().GetAll().Select(e => (int?)e.EmployeeID).FirstOrDefault();
            var job = new Job
            {
                ClientID = baseline.Client.ClientID,
                SiteID = baseline.Site.SiteID,
                JobTitle = "QA Dashboard Recent Job " + DateTime.Now.ToString("HHmmss"),
                Title = "QA Dashboard Recent Job " + DateTime.Now.ToString("HHmmss"),
                JobType = "Breakdown",
                Description = QaKey + " job save should appear on dashboard recents.",
                AssignedEmployeeID = technicianId,
                ScheduledDate = DateTime.Today.AddDays(1),
                Priority = "High",
                PipelineStatus = technicianId.HasValue ? "Assigned" : "Created",
                QuotedRevenue = 12500m,
                Revenue = 12500m,
                EstimatedCost = 4200m,
                Notes = QaKey + " | job"
            };
            return service.Create(job);
        }

        private static int CreateQuotation(TenderService service, BaselineData baseline, Job job)
        {
            var quote = new TenderBid
            {
                QuotationNumber = service.GenerateQuotationNumber(),
                TenderName = "QA Dashboard Quote " + (job.JobNumber ?? DateTime.Now.ToString("HHmmss")),
                ClientID = baseline.Client.ClientID,
                SiteID = baseline.Site.SiteID,
                ClientName = baseline.Client.CompanyName,
                SiteName = baseline.Site.SiteName,
                SystemCount = 1,
                DueDate = DateTime.Today.AddDays(7),
                SubmittedDate = DateTime.Today,
                RequiredByDate = DateTime.Today.AddDays(5),
                Status = "Draft",
                CommercialFlow = "Revenue",
                CustomerDocumentStatus = "Sent to Client",
                SupplierDocumentStatus = "Received from Vendor",
                RequirementCategory = "HVAC Service",
                ItemName = "Dashboard recents verification",
                RequiredQuantity = 1m,
                Unit = "Job",
                RecommendedVendorID = baseline.Vendor.VendorID,
                Notes = "Created by dashboard-recents smoke.",
                FlowNotes = QaKey + " | JobID=" + job.JobID,
                LineItems = new List<TenderBidLineItem>
                {
                    new TenderBidLineItem
                    {
                        SortOrder = 1,
                        Category = "Material",
                        InventoryItemId = baseline.StockItem.ItemID,
                        ItemDescription = baseline.StockItem.ItemName,
                        Quantity = 6m,
                        Unit = "Mtr",
                        HsnSacCode = "74111000",
                        GSTRatePct = 18m,
                        BestSupplierId = baseline.Vendor.VendorID,
                        BestSupplierName = baseline.Vendor.VendorName,
                        CostPerUnit = 180m,
                        SellPricePerUnit = 260m,
                        Shortfall = 6m,
                        AnalysisStatus = "Supplier Required"
                    },
                    new TenderBidLineItem
                    {
                        SortOrder = 2,
                        Category = "Service",
                        ItemDescription = "Commissioning",
                        Quantity = 1m,
                        Unit = "Job",
                        HsnSacCode = "998719",
                        GSTRatePct = 18m,
                        CostPerUnit = 1800m,
                        SellPricePerUnit = 5200m,
                        IsInternalLabour = true,
                        AnalysisStatus = "Ready"
                    }
                }
            };

            return service.SaveTenderBid(quote);
        }

        private static int CreatePurchaseOrder(PurchaseService service, BaselineData baseline, Job job, TenderBid quote)
        {
            var po = new PurchaseOrder
            {
                VendorID = baseline.Vendor.VendorID,
                ClientID = baseline.Client.ClientID,
                SiteID = baseline.Site.SiteID,
                RecommendedByBidID = quote.BidID,
                PONumber = "QA-RECENT-PO-" + DateTime.Now.ToString("yyyyMMddHHmmss"),
                PODate = DateTime.Today,
                PayByDate = DateTime.Today.AddDays(30),
                LinkedToType = "WorkOrder",
                LinkedToId = job.JobID,
                LinkedToLabel = job.JobNumber,
                DeliveryMode = "Site Delivery",
                DeliveryAddress = baseline.Site.Address,
                AddToClientInvoice = true,
                Status = "Pending",
                Notes = QaKey + " | purchase",
                TotalAmount = 1080m,
                LineItems = new List<PurchaseLineItem>
                {
                    new PurchaseLineItem
                    {
                        InventoryItemId = baseline.StockItem.ItemID,
                        Description = baseline.StockItem.ItemName,
                        ItemName = baseline.StockItem.ItemName,
                        HsnSacCode = "74111000",
                        Quantity = 6m,
                        UOM = "Mtr",
                        Rate = 180m,
                        UnitPrice = 180m,
                        GSTRate = 18m,
                        CGSTRate = 9m,
                        SGSTRate = 9m,
                        Amount = 1080m,
                        JobLink = "WorkOrder",
                        LinkedWorkOrderId = job.JobID,
                        LinkedWorkOrderName = job.JobNumber
                    }
                }
            };

            return service.Create(po);
        }

        private static int CreateInvoice(InvoiceService service, BaselineData baseline, Job job, TenderBid quote)
        {
            var invoice = new Invoice
            {
                ClientID = baseline.Client.ClientID,
                SiteID = baseline.Site.SiteID,
                QuotationBidID = quote.BidID,
                InvoiceDate = DateTime.Today,
                DueDate = DateTime.Today.AddDays(30),
                PaymentStatus = "Pending",
                GSTMode = "CGST+SGST",
                GSTPercent = 18m,
                PaymentTerms = "30 Days",
                PlaceOfSupply = "Maharashtra",
                InvoiceTitle = "TAX INVOICE",
                Subject = "QA dashboard recents invoice for " + (job.JobNumber ?? "job"),
                SendInvoiceTo = baseline.Client.CompanyName,
                Notes = QaKey + " | invoice",
                LineItems = new List<InvoiceLineItem>
                {
                    new InvoiceLineItem
                    {
                        Description = "Material supply",
                        HSNCode = "74111000",
                        Category = "Material",
                        Unit = "Mtr",
                        Quantity = 6m,
                        Rate = 260m,
                        GSTPercent = 18m,
                        TaxType = "Taxable",
                        IsStockItem = true,
                        IsBillable = true
                    },
                    new InvoiceLineItem
                    {
                        Description = "Installation and commissioning",
                        HSNCode = "998719",
                        Category = "Service",
                        Unit = "Job",
                        Quantity = 1m,
                        Rate = 5200m,
                        GSTPercent = 18m,
                        TaxType = "Taxable",
                        IsBillable = true
                    }
                }
            };

            return service.CreateInvoiceWithLineItems(invoice);
        }

        private static void AssertDashboardContains(string reference, string expectedModule)
        {
            using (var dashboard = new DashboardForm())
            {
                InvokePrivate(dashboard, "LoadData");
                object items = InvokePrivate(dashboard, "BuildRecentItems");
                IEnumerable enumerable = items as IEnumerable;
                if (enumerable == null)
                    throw new InvalidOperationException("Dashboard recent items could not be enumerated.");

                bool found = false;
                foreach (object item in enumerable)
                {
                    string module = ReadString(item, "Module");
                    string itemReference = ReadString(item, "Reference");
                    if (string.Equals(module, expectedModule, StringComparison.OrdinalIgnoreCase)
                        && string.Equals(itemReference, reference, StringComparison.OrdinalIgnoreCase))
                    {
                        found = true;
                        break;
                    }
                }

                if (!found)
                    throw new InvalidOperationException(expectedModule + " " + reference + " was not present in dashboard recent activity.");
            }
        }

        private static void AssertDashboardViewOpens(string module, int recordId)
        {
            using (var main = new MainForm())
            using (var dashboard = new DashboardForm())
            {
                main.Show();
                dashboard.CreateControl();
                Application.DoEvents();

                InvokePrivate(dashboard, "LoadData");
                object item = FindRecentItem(dashboard, module, recordId);
                if (item == null)
                    throw new InvalidOperationException("Dashboard recent item was not found for view test: " + module + " #" + recordId);

                InvokePrivate(dashboard, "OpenRecentActivityItem", item);

                if (string.Equals(module, "Job", StringComparison.OrdinalIgnoreCase))
                {
                    bool openedJob = WaitUntil(() =>
                    {
                        object transientPage = ReadField(main, "_transientPage");
                        return transientPage is JobPreviewPage preview && preview.JobId == recordId;
                    });
                    if (!openedJob)
                        throw new InvalidOperationException("Dashboard View did not open the expected job detail.");
                    return;
                }

                IDictionary pageCache = ReadField(main, "_pageCache") as IDictionary;
                if (pageCache == null)
                    throw new InvalidOperationException("Main form page cache is unavailable.");

                int pageIndex = PageIndexFor(module);
                bool pageOpened = WaitUntil(() => pageCache[pageIndex] != null);
                object page = pageCache[pageIndex];
                if (page == null)
                    throw new InvalidOperationException("Dashboard View did not navigate to the " + module + " page.");

                if (string.Equals(module, "Invoice", StringComparison.OrdinalIgnoreCase))
                {
                    WaitUntil(() =>
                    {
                        Invoice pending = ReadField(page, "_current") as Invoice;
                        return pending != null && pending.InvoiceID == recordId;
                    });
                    Invoice current = ReadField(page, "_current") as Invoice;
                    if (current == null || current.InvoiceID != recordId)
                        throw new InvalidOperationException("Dashboard View did not open the expected invoice.");
                }
                else if (string.Equals(module, "Quotation", StringComparison.OrdinalIgnoreCase))
                {
                    WaitUntil(() =>
                    {
                        TenderBid pending = ReadField(page, "_current") as TenderBid;
                        return pending != null && pending.BidID == recordId;
                    });
                    TenderBid current = ReadField(page, "_current") as TenderBid;
                    if (current == null || current.BidID != recordId)
                        throw new InvalidOperationException("Dashboard View did not open the expected quotation. Actual BidID=" + (current == null ? "null" : current.BidID.ToString()) + ", pageType=" + page.GetType().FullName);
                }
                else if (string.Equals(module, "Purchase", StringComparison.OrdinalIgnoreCase))
                {
                    WaitUntil(() =>
                    {
                        PurchaseOrder pending = ReadField(page, "_current") as PurchaseOrder;
                        return pending != null && pending.POID == recordId;
                    });
                    PurchaseOrder current = ReadField(page, "_current") as PurchaseOrder;
                    if (current == null || current.POID != recordId)
                        throw new InvalidOperationException("Dashboard View did not open the expected purchase order.");
                }
            }
        }

        private static void AssertDashboardPurchaseViewOpens(int recordId, string poNumber)
        {
            using (var main = new MainForm())
            using (var dashboard = new DashboardForm())
            {
                main.Show();
                dashboard.CreateControl();
                Application.DoEvents();

                InvokePrivate(dashboard, "LoadData");
                object item = FindRecentItem(dashboard, "Purchase", recordId);
                if (item == null)
                    throw new InvalidOperationException("Dashboard recent purchase item was not found for view test: #" + recordId);

                InvokePrivate(dashboard, "OpenRecentActivityItem", item);
                bool opened = WaitUntil(() =>
                    Application.OpenForms
                        .OfType<HtmlPreviewDialog>()
                        .Any(form => form.Text.IndexOf(poNumber ?? string.Empty, StringComparison.OrdinalIgnoreCase) >= 0));

                Form preview = Application.OpenForms
                    .OfType<HtmlPreviewDialog>()
                    .FirstOrDefault(form => form.Text.IndexOf(poNumber ?? string.Empty, StringComparison.OrdinalIgnoreCase) >= 0);

                if (!opened || preview == null)
                    throw new InvalidOperationException("Dashboard View did not open the expected purchase order preview.");

                preview.Close();
                WaitForUi();
            }
        }

        private static object FindRecentItem(DashboardForm dashboard, string module, int recordId)
        {
            object items = InvokePrivate(dashboard, "BuildRecentItems");
            IEnumerable enumerable = items as IEnumerable;
            if (enumerable == null)
                return null;

            foreach (object item in enumerable)
            {
                string itemModule = ReadString(item, "Module");
                int itemRecordId = ReadInt(item, "RecordId");
                if (string.Equals(itemModule, module, StringComparison.OrdinalIgnoreCase) && itemRecordId == recordId)
                    return item;
            }

            return null;
        }

        private static object InvokePrivate(object target, string methodName, params object[] args)
        {
            MethodInfo method = target.GetType().GetMethod(methodName, BindingFlags.Instance | BindingFlags.NonPublic);
            if (method == null)
                throw new InvalidOperationException("Method not found: " + methodName);

            return method.Invoke(target, args);
        }

        private static string ReadString(object target, string propertyName)
        {
            PropertyInfo property = target.GetType().GetProperty(propertyName, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
            object value = property == null ? null : property.GetValue(target, null);
            return value == null ? string.Empty : Convert.ToString(value);
        }

        private static int ReadInt(object target, string propertyName)
        {
            PropertyInfo property = target.GetType().GetProperty(propertyName, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
            object value = property == null ? null : property.GetValue(target, null);
            return value == null ? 0 : Convert.ToInt32(value);
        }

        private static object ReadField(object target, string fieldName)
        {
            FieldInfo field = target.GetType().GetField(fieldName, BindingFlags.Instance | BindingFlags.NonPublic);
            return field == null ? null : field.GetValue(target);
        }

        private static int PageIndexFor(string module)
        {
            if (string.Equals(module, "Invoice", StringComparison.OrdinalIgnoreCase))
                return 3;
            if (string.Equals(module, "Quotation", StringComparison.OrdinalIgnoreCase))
                return 6;
            if (string.Equals(module, "Purchase", StringComparison.OrdinalIgnoreCase))
                return 10;
            return 0;
        }

        private static void WaitForUi()
        {
            for (int i = 0; i < 20; i++)
            {
                Application.DoEvents();
                System.Threading.Thread.Sleep(50);
            }
        }

        private static bool WaitUntil(Func<bool> condition, int timeoutMs = 5000)
        {
            DateTime deadline = DateTime.Now.AddMilliseconds(timeoutMs);
            while (DateTime.Now < deadline)
            {
                if (condition())
                    return true;

                Application.DoEvents();
                System.Threading.Thread.Sleep(50);
            }

            return condition();
        }

        private static void Cleanup(CreatedRecords created)
        {
            try
            {
                if (created.InvoiceId > 0)
                    new InvoiceService().DeleteInvoice(created.InvoiceId);
            }
            catch
            {
            }

            try
            {
                if (created.PurchaseOrderId > 0)
                    new PurchaseService().Delete(created.PurchaseOrderId);
            }
            catch
            {
            }

            try
            {
                if (created.QuotationId > 0)
                    new TenderService().Delete(created.QuotationId);
            }
            catch
            {
            }

            try
            {
                if (created.JobId > 0)
                    new JobService().Delete(created.JobId);
            }
            catch
            {
            }
        }

        private static void Assert(bool condition, string message)
        {
            if (!condition)
                throw new InvalidOperationException(message);
        }

        private sealed class BaselineData
        {
            public B2BClient Client { get; set; }
            public ClientSite Site { get; set; }
            public Vendor Vendor { get; set; }
            public StockItem StockItem { get; set; }
        }

        private sealed class CreatedRecords
        {
            public int JobId { get; set; }
            public int QuotationId { get; set; }
            public int PurchaseOrderId { get; set; }
            public int InvoiceId { get; set; }
        }

        private sealed class EventRecorder
        {
            private readonly List<string> _modules = new List<string>();

            public void Handle(object sender, DashboardRefreshEventArgs e)
            {
                if (e != null && !string.IsNullOrWhiteSpace(e.ModuleKey))
                    _modules.Add(e.ModuleKey);
            }

            public bool ContainsModule(string moduleKey)
            {
                return _modules.Any(module => string.Equals(module, moduleKey, StringComparison.OrdinalIgnoreCase));
            }
        }
    }
}
