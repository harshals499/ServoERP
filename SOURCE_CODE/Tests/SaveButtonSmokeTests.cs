using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using HVAC_Pro_Desktop.Models;
using HVAC_Pro_Desktop.Services;
using HVAC_Pro_Desktop.Services.Licensing;

namespace HVAC_Pro_Desktop.Tests
{
    /// <summary>
    /// Persistence-focused smoke tests for the primary save flows used by the main WinForms save buttons.
    /// Uses service-layer create/update methods so the exact repository path is exercised without fragile UI automation.
    /// </summary>
    public static class SaveButtonSmokeTests
    {
        private static readonly string QaKey = "QA-SAVE-" + DateTime.Now.ToString("yyyyMMddHHmmss");
        private static int _phoneSequence;

        public static List<string> RunAll()
        {
            return RunWithQaSession(results =>
            {
                results.Add(Run("Attendance save persists monthly record", TestAttendanceSave));
                results.Add(Run("Client save persists create and update", TestClientSave));
                results.Add(Run("Employee save persists create, update, and salary profile", TestEmployeeSave));
                results.Add(Run("Vendor save persists create and update", TestVendorSave));
                results.Add(Run("Inventory save persists create and update", TestInventorySave));
                results.Add(Run("Contract save persists create and update", TestContractSave));
                results.Add(Run("Job save persists create and update", TestJobSave));
                results.Add(Run("Purchase save persists create and update", TestPurchaseSave));
                results.Add(Run("Invoice save persists create and update", TestInvoiceSave));
                results.Add(Run("Payment save persists record and invoice status refresh", TestPaymentSave));
            });
        }

        public static List<string> RunInvoiceOnly()
        {
            return RunWithQaSession(results =>
            {
                results.Add(Run("Invoice save persists create and update", TestInvoiceSave));
            });
        }

        public static List<string> RunPaymentOnly()
        {
            return RunWithQaSession(results =>
            {
                results.Add(Run("Payment save persists record and invoice status refresh", TestPaymentSave));
            });
        }

        private static List<string> RunWithQaSession(Action<List<string>> runTests)
        {
            AppUserDto previousUser = SessionManager.CurrentUser;
            Guid? previousSessionId = SessionManager.CurrentSessionId;
            DateTime? previousExpiry = SessionManager.ExpiresAt;

            try
            {
                EnsureQaLicense();
                SessionManager.SetSession(new AppUserDto
                {
                    UserId = 0,
                    Username = "qa-save",
                    DisplayName = "ServoERP Save QA",
                    RoleName = "Administrator",
                    IsActive = true
                }, Guid.NewGuid(), DateTime.Now.AddHours(1));

                var results = new List<string>();
                runTests(results);
                return results;
            }
            finally
            {
                SessionManager.SetSession(previousUser, previousSessionId, previousExpiry);
            }
        }

        public static string WriteReport()
        {
            string dir = Path.Combine(@"C:\HVAC_PRO_MSE", "TEST_RESULTS");
            Directory.CreateDirectory(dir);
            string path = Path.Combine(dir, "save-button-smoke-" + DateTime.Now.ToString("yyyyMMdd-HHmmss") + ".txt");
            var lines = new List<string>
            {
                "Save Button Smoke Tests",
                DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"),
                string.Empty
            };
            lines.AddRange(RunAll());
            File.WriteAllLines(path, lines);
            return path;
        }

        private static string Run(string label, Action test)
        {
            try
            {
                test();
                return "PASS " + label;
            }
            catch (Exception ex)
            {
                return "FAIL " + label + " | " + Unwrap(ex).GetType().Name + ": " + Unwrap(ex).Message;
            }
        }

        private static void TestAttendanceSave()
        {
            Employee employee = EnsureEmployee();
            var attendanceService = new AttendanceService();
            DateTime day = new DateTime(DateTime.Today.Year, DateTime.Today.Month, Math.Min(5, DateTime.DaysInMonth(DateTime.Today.Year, DateTime.Today.Month)));
            string status = "Present";

            attendanceService.SaveAttendanceRecord(new AttendanceRecord
            {
                EmployeeId = employee.EmployeeID,
                AttendanceDate = day,
                Status = status,
                OvertimeHours = 0m,
                Notes = QaKey
            });

            AttendanceRecord saved = attendanceService.GetMonthlyAttendanceRecords(employee.EmployeeID, day.Month, day.Year)
                .FirstOrDefault(r => r.AttendanceDate.Date == day.Date);
            Assert(saved != null, "Attendance record was not reloaded after save.");
            Assert(string.Equals(saved.Status, status, StringComparison.OrdinalIgnoreCase), "Attendance status was not saved correctly.");
        }

        private static void TestClientSave()
        {
            var clientService = new ClientService();
            string suffix = BuildSuffix("CLIENT");
            var client = new B2BClient
            {
                CompanyName = "QA Save Client " + suffix,
                PrimaryContact = "QA Operator",
                Phone = "9876543210",
                Email = "qa.client." + suffix.ToLowerInvariant() + "@servoerp.in",
                BillingAddress = "QA Billing Address",
                City = "Thane",
                PaymentTermsDays = 30,
                CreditLimit = 100000m,
                IsActive = true
            };

            client.ClientID = clientService.CreateClient(client);
            B2BClient created = clientService.GetClientById(client.ClientID);
            Assert(created != null, "Client create did not return a persisted client.");

            string updatedName = client.CompanyName + " Updated";
            created.CompanyName = updatedName;
            clientService.UpdateClient(created);
            B2BClient updated = clientService.GetClientById(client.ClientID);
            Assert(updated != null && string.Equals(updated.CompanyName, updatedName, StringComparison.OrdinalIgnoreCase), "Client update did not persist.");
        }

        private static void TestEmployeeSave()
        {
            var employeeService = new EmployeeService();
            string suffix = BuildSuffix("EMP");
            var employee = new Employee
            {
                EmployeeCode = "QA-" + suffix,
                Name = "QA Employee " + suffix,
                Designation = "Technician",
                Department = "Service",
                ClientSite = "QA Site",
                Phone = BuildUniquePhone("98"),
                JoiningDate = DateTime.Today,
                Status = "Active",
                BasicSalary = 12000m,
                GrossSalary = 18000m
            };

            employee.EmployeeID = employeeService.Create(employee);
            Employee created = employeeService.GetById(employee.EmployeeID);
            Assert(created != null, "Employee create did not persist.");

            created.Designation = "Senior Technician";
            employeeService.Update(created);
            Employee updated = employeeService.GetById(employee.EmployeeID);
            Assert(updated != null && string.Equals(updated.Designation, "Senior Technician", StringComparison.OrdinalIgnoreCase), "Employee update did not persist.");

            var profile = new EmployeeSalaryProfileDto
            {
                EmployeeID = employee.EmployeeID,
                EffectiveFrom = DateTime.Today,
                BasicSalary = 12000m,
                HRA = 3000m,
                Allowances = 2500m,
                PFDeduction = 0m,
                ESICDeduction = 0m,
            };
            int salaryId = employeeService.SaveSalaryProfile(profile);
            Assert(salaryId > 0, "Salary profile save did not return a persisted ID.");
            EmployeeSalaryProfileDto savedProfile = employeeService.GetSalaryProfile(employee.EmployeeID);
            Assert(savedProfile != null && savedProfile.BasicSalary == profile.BasicSalary, "Salary profile did not persist.");
        }

        private static void TestVendorSave()
        {
            var vendorService = new VendorService();
            string suffix = BuildSuffix("VENDOR");
            var vendor = new Vendor
            {
                VendorName = "QA Save Vendor " + suffix,
                PANNumber = "QAVCE1234F",
                Phone = "9988776655",
                Email = "qa.vendor." + suffix.ToLowerInvariant() + "@servoerp.in",
                Address = "QA Industrial Estate",
                City = "Bhiwandi",
                Category = "HVAC Materials",
                VendorType = "Supplier",
                PreferredPaymentMode = "NEFT",
                IsActive = true,
                Notes = QaKey
            };

            vendor.VendorID = vendorService.Create(vendor);
            Vendor created = vendorService.GetById(vendor.VendorID);
            Assert(created != null, "Vendor create did not persist.");

            created.City = "Mumbai";
            vendorService.Update(created);
            Vendor updated = vendorService.GetById(vendor.VendorID);
            Assert(updated != null && string.Equals(updated.City, "Mumbai", StringComparison.OrdinalIgnoreCase), "Vendor update did not persist.");
        }

        private static void TestInventorySave()
        {
            var inventoryService = new InventoryService();
            string suffix = BuildSuffix("ITEM");
            var item = new StockItem
            {
                ItemName = "QA Save Item " + suffix,
                Category = "Copper",
                CurrentStock = 2m,
                Unit = "Mtr",
                LastPurchaseRate = 180m,
                ReorderLevel = 1m,
                IsActive = true
            };

            item.ItemID = inventoryService.Create(item);
            StockItem created = inventoryService.GetById(item.ItemID);
            Assert(created != null, "Inventory create did not persist.");

            created.LastPurchaseRate = 215.75m;
            inventoryService.Update(created);
            StockItem updated = inventoryService.GetById(item.ItemID);
            Assert(updated != null && Math.Abs(updated.LastPurchaseRate - 215.75m) < 0.01m, "Inventory update did not persist.");
        }

        private static void TestContractSave()
        {
            B2BClient client = EnsureClient();
            var contractService = new ContractService();
            string suffix = BuildSuffix("AMC");
            var contract = new AMCContract
            {
                ClientID = client.ClientID,
                SiteID = 0,
                StartDate = DateTime.Today,
                EndDate = DateTime.Today.AddYears(1),
                AnnualValue = 24000m,
                MonthlyValue = 2000m,
                ContractStatus = "Active",
                ContractType = "AMC",
                MaintenanceFrequency = "Monthly",
                SLAResponseTimeHours = 4,
                SLARepairTimeHours = 8,
                SLAUptimePercent = 99m,
                Notes = QaKey
            };

            contract.ContractID = contractService.CreateContract(contract);
            AMCContract created = contractService.GetContractDetails(contract.ContractID);
            Assert(created != null, "Contract create did not persist.");

            created.MonthlyValue = 2250m;
            contractService.UpdateContract(created);
            AMCContract updated = contractService.GetContractDetails(contract.ContractID);
            Assert(updated != null && updated.MonthlyValue == 2250m, "Contract update did not persist.");
        }

        private static void TestJobSave()
        {
            B2BClient client = EnsureClient();
            ClientSite site = EnsureSite(client);
            Employee employee = EnsureEmployee();
            var jobService = new JobService();
            string suffix = BuildSuffix("JOB");
            var job = new Job
            {
                ClientID = client.ClientID,
                SiteID = site.SiteID,
                JobTitle = "QA Save Job " + suffix,
                Title = "QA Save Job " + suffix,
                JobType = "Breakdown",
                Description = "QA job persistence test " + QaKey,
                AssignedEmployeeID = employee.EmployeeID,
                ScheduledDate = DateTime.Today.AddDays(1),
                Priority = "High",
                PipelineStatus = "Assigned",
                Revenue = 5000m,
                EstimatedCost = 1500m,
                Notes = QaKey
            };

            job.JobID = jobService.Create(job);
            Job created = jobService.GetById(job.JobID);
            Assert(created != null, "Job create did not persist.");

            created.Priority = "Medium";
            jobService.Update(created);
            Job updated = jobService.GetById(job.JobID);
            Assert(updated != null && string.Equals(updated.Priority, "Medium", StringComparison.OrdinalIgnoreCase), "Job update did not persist.");
        }

        private static void TestPurchaseSave()
        {
            B2BClient client = EnsureClient();
            ClientSite site = EnsureSite(client);
            Vendor vendor = EnsureVendor();
            StockItem item = EnsureInventoryItem();
            var purchaseService = new PurchaseService();
            string suffix = BuildSuffix("PO");
            var po = new PurchaseOrder
            {
                VendorID = vendor.VendorID,
                ClientID = client.ClientID,
                SiteID = site.SiteID,
                PONumber = "QA-PO-" + suffix,
                PODate = DateTime.Today,
                PayByDate = DateTime.Today.AddDays(30),
                Status = "Pending",
                DeliveryMode = "Site Delivery",
                DeliveryAddress = site.Address,
                Notes = QaKey,
                TotalAmount = 1200m,
                LineItems = new List<PurchaseLineItem>
                {
                    new PurchaseLineItem
                    {
                        InventoryItemId = item.ItemID,
                        Description = item.ItemName,
                        Quantity = 5m,
                        UOM = "Mtr",
                        Rate = 240m,
                        Amount = 1200m
                    }
                }
            };

            po.POID = purchaseService.Create(po);
            PurchaseOrder created = purchaseService.GetById(po.POID);
            Assert(created != null, "Purchase create did not persist.");

            created.Notes = QaKey + " updated";
            purchaseService.Update(created);
            PurchaseOrder updated = purchaseService.GetById(po.POID);
            Assert(updated != null && string.Equals(updated.Notes, QaKey + " updated", StringComparison.OrdinalIgnoreCase), "Purchase update did not persist.");
        }

        private static void TestInvoiceSave()
        {
            B2BClient client = EnsureClient();
            ClientSite site = EnsureSite(client);
            var invoiceService = new InvoiceService();
            string suffix = BuildSuffix("INV");
            var invoice = new Invoice
            {
                ClientID = client.ClientID,
                SiteID = site.SiteID,
                InvoiceDate = DateTime.Today,
                DueDate = DateTime.Today.AddDays(30),
                PaymentStatus = "Pending",
                GSTMode = "CGST+SGST",
                GSTPercent = 18m,
                PaymentTerms = "30 Days",
                PlaceOfSupply = "Maharashtra",
                InvoiceTitle = "TAX INVOICE",
                Subject = "QA Invoice " + suffix,
                SendInvoiceTo = client.CompanyName,
                Notes = QaKey,
                LineItems = new List<InvoiceLineItem>
                {
                    new InvoiceLineItem
                    {
                        Description = "QA Service Line",
                        HSNCode = "998719",
                        Category = "Service",
                        Unit = "Job",
                        Quantity = 1m,
                        Rate = 3000m,
                        GSTPercent = 18m,
                        TaxType = "Taxable",
                        IsBillable = true
                    }
                }
            };

            invoice.InvoiceID = invoiceService.CreateInvoiceWithLineItems(invoice);
            Invoice created = invoiceService.GetInvoiceById(invoice.InvoiceID);
            Assert(created != null, "Invoice create did not persist.");

            created.Subject = "QA Invoice " + suffix + " Updated";
            created.LineItems = created.LineItems ?? invoice.LineItems;
            invoiceService.UpdateInvoiceWithLineItems(created);
            Invoice updated = invoiceService.GetInvoiceById(invoice.InvoiceID);
            Assert(updated != null && string.Equals(updated.Subject, "QA Invoice " + suffix + " Updated", StringComparison.OrdinalIgnoreCase), "Invoice update did not persist.");
        }

        private static void TestPaymentSave()
        {
            B2BClient client = EnsureClient();
            ClientSite site = EnsureSite(client);
            Invoice invoice = EnsureInvoice(client, site);
            var paymentService = new PaymentService();
            var invoiceService = new InvoiceService();

            Invoice payable = invoiceService.GetInvoiceById(invoice.InvoiceID);
            decimal amount = Math.Max(1m, payable.BalanceDue > 0m ? payable.BalanceDue : payable.TotalAmount);
            var payment = new Payment
            {
                InvoiceID = payable.InvoiceID,
                ClientID = client.ClientID,
                AmountPaid = amount,
                PaymentDate = DateTime.Today,
                PaymentMode = "NEFT",
                ReferenceNumber = "QA-PAY-" + BuildSuffix("REF"),
                Notes = QaKey
            };

            int paymentId = paymentService.RecordPayment(payment);
            Assert(paymentId != 0, "Payment record did not return an identifier.");
            Payment saved = paymentService.GetPaymentsForInvoice(payable.InvoiceID).FirstOrDefault(p => p.PaymentID == paymentId || (p.Notes ?? string.Empty).Contains(QaKey));
            Assert(saved != null, "Payment was not reloaded after save.");

            Invoice refreshed = invoiceService.GetInvoiceById(payable.InvoiceID);
            Assert(refreshed != null && string.Equals(refreshed.PaymentStatus, "Paid", StringComparison.OrdinalIgnoreCase), "Invoice payment status was not refreshed to Paid.");
        }

        private static B2BClient EnsureClient()
        {
            var service = new ClientService();
            string suffix = BuildSuffix("BASECLIENT");
            var client = new B2BClient
            {
                CompanyName = "QA Base Client " + suffix,
                PrimaryContact = "QA Operator",
                Phone = "9898989898",
                Email = "qa.base." + suffix.ToLowerInvariant() + "@servoerp.in",
                BillingAddress = "QA Billing Address",
                City = "Thane",
                PaymentTermsDays = 30,
                CreditLimit = 100000m,
                IsActive = true
            };
            client.ClientID = service.CreateClient(client);
            return service.GetClientById(client.ClientID);
        }

        private static ClientSite EnsureSite(B2BClient client)
        {
            var siteService = new SiteService();
            var site = new ClientSite
            {
                ClientID = client.ClientID,
                SiteName = "QA Save Site " + BuildSuffix("SITE"),
                Address = "QA Service Site",
                City = "Thane",
            };
            site.SiteID = siteService.Create(site);
            return siteService.GetById(site.SiteID);
        }

        private static Employee EnsureEmployee()
        {
            var service = new EmployeeService();
            string suffix = BuildSuffix("BASEEMP");
            var employee = new Employee
            {
                EmployeeCode = "QA-" + suffix,
                Name = "QA Base Employee " + suffix,
                Designation = "Technician",
                Department = "Service",
                Phone = BuildUniquePhone("90"),
                JoiningDate = DateTime.Today,
                Status = "Active",
                BasicSalary = 10000m,
                GrossSalary = 15000m
            };
            employee.EmployeeID = service.Create(employee);
            return service.GetById(employee.EmployeeID);
        }

        private static string BuildUniquePhone(string prefix)
        {
            int sequence = System.Threading.Interlocked.Increment(ref _phoneSequence);
            string suffix = (DateTime.Now.Ticks + sequence).ToString();
            if (suffix.Length < 8)
                suffix = suffix.PadLeft(8, '0');

            return (prefix + suffix.Substring(suffix.Length - 8)).Substring(0, 10);
        }

        private static Vendor EnsureVendor()
        {
            var service = new VendorService();
            string suffix = BuildSuffix("BASEVENDOR");
            var vendor = new Vendor
            {
                VendorName = "QA Base Vendor " + suffix,
                PANNumber = "QAVCE1234F",
                Phone = "9988776655",
                Email = "qa.base.vendor." + suffix.ToLowerInvariant() + "@servoerp.in",
                Address = "QA Industrial Estate",
                City = "Bhiwandi",
                Category = "HVAC Materials",
                VendorType = "Supplier",
                PreferredPaymentMode = "NEFT",
                IsActive = true,
                Notes = QaKey
            };
            vendor.VendorID = service.Create(vendor);
            return service.GetById(vendor.VendorID);
        }

        private static StockItem EnsureInventoryItem()
        {
            var service = new InventoryService();
            string suffix = BuildSuffix("BASEITEM");
            var item = new StockItem
            {
                ItemName = "QA Base Item " + suffix,
                Category = "Copper",
                CurrentStock = 10m,
                Unit = "Mtr",
                LastPurchaseRate = 200m,
                ReorderLevel = 2m,
                IsActive = true
            };
            item.ItemID = service.Create(item);
            return service.GetById(item.ItemID);
        }

        private static Invoice EnsureInvoice(B2BClient client, ClientSite site)
        {
            var service = new InvoiceService();
            string suffix = BuildSuffix("BASEINV");
            var invoice = new Invoice
            {
                ClientID = client.ClientID,
                SiteID = site.SiteID,
                InvoiceDate = DateTime.Today,
                DueDate = DateTime.Today.AddDays(30),
                PaymentStatus = "Pending",
                GSTMode = "CGST+SGST",
                GSTPercent = 18m,
                PaymentTerms = "30 Days",
                PlaceOfSupply = "Maharashtra",
                InvoiceTitle = "TAX INVOICE",
                Subject = "QA Base Invoice " + suffix,
                SendInvoiceTo = client.CompanyName,
                Notes = QaKey + " payment seed",
                LineItems = new List<InvoiceLineItem>
                {
                    new InvoiceLineItem
                    {
                        Description = "QA Base Service Line",
                        HSNCode = "998719",
                        Category = "Service",
                        Unit = "Job",
                        Quantity = 1m,
                        Rate = 2200m,
                        GSTPercent = 18m,
                        TaxType = "Taxable",
                        IsBillable = true
                    }
                }
            };
            invoice.InvoiceID = service.CreateInvoiceWithLineItems(invoice);
            return service.GetInvoiceById(invoice.InvoiceID);
        }

        private static string BuildSuffix(string prefix)
        {
            return prefix + "-" + Guid.NewGuid().ToString("N").Substring(0, 8).ToUpperInvariant();
        }

        private static void EnsureQaLicense()
        {
            var licenseService = new LicenseService();
            LicenseValidationResult current = licenseService.ValidateCurrentLicense();
            if (current != null && current.Success && !current.IsFrozen)
                return;

            LicenseValidationResult trial = licenseService.ActivateTrial("ServoERP Save Smoke");
            if (trial == null || !trial.Success || trial.IsFrozen)
                throw new InvalidOperationException("QA smoke license activation failed: " + (trial == null ? "no response" : trial.Message));
        }

        private static void Assert(bool condition, string message)
        {
            if (!condition)
                throw new InvalidOperationException(message);
        }

        private static Exception Unwrap(Exception ex)
        {
            while (ex != null && ex.InnerException != null)
                ex = ex.InnerException;
            return ex;
        }
    }
}
