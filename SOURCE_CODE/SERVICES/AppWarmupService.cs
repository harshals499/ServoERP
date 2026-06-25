using System;
using System.ComponentModel;
using System.Collections.Generic;
using System.Threading;
using HVAC_Pro_Desktop.Models;

namespace HVAC_Pro_Desktop.Services
{
    public static class AppWarmupService
    {
        private static int _started;
        private static readonly TimeSpan InitialDelay = TimeSpan.FromSeconds(3);
        private static BackgroundWorker _worker;

        public static void StartBackgroundWarmup()
        {
            if (Interlocked.Exchange(ref _started, 1) == 1)
                return;

            _worker = new BackgroundWorker();
            _worker.DoWork += (s, e) =>
            {
                Thread.Sleep(InitialDelay);

                TimeSpan ttl = TimeSpan.FromMinutes(5);
                WarmCache("clients:active", ttl, () => new ClientService().GetAllClients() ?? new List<B2BClient>());
                WarmCache("vendors:suppliers", ttl, () => new VendorService().GetSuppliers() ?? new List<Vendor>());
                WarmCache("vendors:all-including-archived", ttl, () => new VendorService().GetAllIncludingArchived() ?? new List<Vendor>());
                WarmCache("vendors:summaries", ttl, () => new VendorService().GetAllVendorsWithSummary() ?? new List<VendorSummaryDto>());
                WarmCache("inventory:all", ttl, () => new InventoryService().GetAll() ?? new List<StockItem>());
                WarmCache("jobs:all", ttl, () => new JobService().GetAll() ?? new List<Job>());
                WarmCache("jobs:summary", ttl, () => new JobService().GetAllJobsWithSummary() ?? new List<JobSummaryDto>());
                WarmCache("contracts:all", ttl, () => new ContractService().GetAllContracts() ?? new List<AMCContract>());
                WarmCache("hsnsac:all", ttl, () => new HsnSacMasterService().GetAll() ?? new List<HsnSacMasterEntry>());
            };
            _worker.RunWorkerAsync();
        }

        private static void WarmCache<T>(string key, TimeSpan ttl, Func<T> factory)
        {
            try
            {
                AppDataCache.GetOrCreate(key, ttl, factory);
            }
            catch (Exception ex)
            {
                AppLogger.LogError("AppWarmupService." + key, ex);
            }
        }
    }
}
