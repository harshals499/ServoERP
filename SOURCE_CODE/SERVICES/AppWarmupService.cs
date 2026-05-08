using System;
using System.Threading;
using System.Threading.Tasks;

namespace HVAC_Pro_Desktop.Services
{
    public static class AppWarmupService
    {
        private static int _started;

        public static void StartBackgroundWarmup()
        {
            if (Interlocked.Exchange(ref _started, 1) == 1)
                return;

            Task.Run(() =>
            {
                Warm("settings", () => new SettingsService().GetAll());
                Warm("clients", () => new ClientService().GetAllClients());
                Warm("sites", () => new SiteService().GetAll());
                Warm("contracts", () => new ContractService().GetAllContracts());
                Warm("invoices", () => new InvoiceService().GetAllInvoices());
                Warm("payments", () => new PaymentService().GetAllPayments());
                Warm("vendors", () => new VendorService().GetAll());
                Warm("vendor summaries", () => new VendorService().GetAllVendorsWithSummary());
                Warm("purchases", () => new PurchaseService().GetAll());
                Warm("inventory", () => new InventoryService().GetAll());
                Warm("jobs", () => new JobService().GetAll());
                Warm("job summaries", () => new JobService().GetAllJobsWithSummary());
                Warm("employees", () => new EmployeeService().GetAll());
                Warm("sla", () => new SLAService().GetAll());
                Warm("hsn/sac", () => new HsnSacMasterService().GetAll());
                Warm("quotations", () => new TenderService().GetAll());
                Warm("service desk", () => new ServiceDeskService().GetAll());
            });
        }

        private static void Warm<T>(string name, Func<T> factory)
        {
            try
            {
                factory();
            }
            catch (Exception ex)
            {
                AppLogger.LogError("AppWarmupService." + name, ex);
            }
        }
    }
}
