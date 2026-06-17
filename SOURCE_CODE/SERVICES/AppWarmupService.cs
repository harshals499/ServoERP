using System;
using System.Threading;
using System.Threading.Tasks;

namespace HVAC_Pro_Desktop.Services
{
    public static class AppWarmupService
    {
        private static int _started;
        private static readonly TimeSpan InitialDelay = TimeSpan.FromSeconds(3);
        private static readonly TimeSpan BetweenBatchesDelay = TimeSpan.FromSeconds(2);
        private static readonly TimeSpan HeavyBatchDelay = TimeSpan.FromSeconds(6);

        public static void StartBackgroundWarmup()
        {
            if (Interlocked.Exchange(ref _started, 1) == 1)
                return;

            Task.Run(async () =>
            {
                await Task.Delay(InitialDelay).ConfigureAwait(false);

                // Warm the smallest, most frequently reused datasets first.
                Warm("settings", () => new SettingsService().GetAll());
                Warm("clients", () => new ClientService().GetAllClients());
                Warm("sites", () => new SiteService().GetAll());
                Warm("contracts", () => new ContractService().GetAllContracts());
                Warm("invoices", () => new InvoiceService().GetAllInvoices());
                Warm("payments", () => new PaymentService().GetAllPayments());
                Warm("inventory", () => new InventoryService().GetAll());
                Warm("jobs", () => new JobService().GetAll());

                await Task.Delay(BetweenBatchesDelay).ConfigureAwait(false);

                Warm("vendors", () => new VendorService().GetAll());
                Warm("purchases", () => new PurchaseService().GetAll());
                Warm("employees", () => new EmployeeService().GetAll());
                Warm("sla", () => new SLAService().GetAll());
                Warm("hsn/sac", () => new HsnSacMasterService().GetAll());
                Warm("quotations", () => new TenderService().GetAll());
                Warm("service desk", () => new ServiceDeskService().GetAll());

                // Summary-heavy warmups are useful, but they are also some of the most
                // expensive joins/aggregations in the app. Delay them until after the
                // user has had a chance to open the shell and first page.
                await Task.Delay(HeavyBatchDelay).ConfigureAwait(false);
                Warm("vendor summaries", () => new VendorService().GetAllVendorsWithSummary());
                Warm("job summaries", () => new JobService().GetAllJobsWithSummary());
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
