using System;
using System.Collections.Concurrent;

namespace HVAC_Pro_Desktop.Services
{
    internal static class AppDataCache
    {
        private sealed class CacheEntry
        {
            public DateTime ExpiresAtUtc { get; set; }
            public object Value { get; set; }
        }

        private static readonly ConcurrentDictionary<string, CacheEntry> Entries =
            new ConcurrentDictionary<string, CacheEntry>(StringComparer.OrdinalIgnoreCase);
        private static readonly ConcurrentDictionary<string, object> KeyLocks =
            new ConcurrentDictionary<string, object>(StringComparer.OrdinalIgnoreCase);

        public static T GetOrCreate<T>(string key, TimeSpan ttl, Func<T> factory)
        {
            if (string.IsNullOrWhiteSpace(key))
                throw new ArgumentException("Cache key is required.", nameof(key));
            if (factory == null)
                throw new ArgumentNullException(nameof(factory));

            DateTime now = DateTime.UtcNow;
            if (Entries.TryGetValue(key, out var existing) && existing.ExpiresAtUtc > now && existing.Value is T cached)
                return cached;

            object keyLock = KeyLocks.GetOrAdd(key, _ => new object());
            lock (keyLock)
            {
                now = DateTime.UtcNow;
                if (Entries.TryGetValue(key, out existing) && existing.ExpiresAtUtc > now && existing.Value is T lockedCached)
                    return lockedCached;

                T value = factory();
                Entries[key] = new CacheEntry
                {
                    ExpiresAtUtc = now.Add(ttl),
                    Value = value
                };
                return value;
            }
        }

        public static void Remove(string key)
        {
            if (!string.IsNullOrWhiteSpace(key) && Entries.TryRemove(key, out _))
                DashboardRefreshService.NotifyChanged(GetModuleKey(key));
        }

        public static void RemovePrefix(string prefix)
        {
            if (string.IsNullOrWhiteSpace(prefix))
                return;

            bool removed = false;
            foreach (var key in Entries.Keys)
            {
                if (key.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
                    removed |= Entries.TryRemove(key, out _);
            }

            if (removed)
                DashboardRefreshService.NotifyChanged(GetModuleKey(prefix));
        }

        public static void Clear()
        {
            bool hadEntries = !Entries.IsEmpty;
            Entries.Clear();
            KeyLocks.Clear();
            if (hadEntries)
                DashboardRefreshService.NotifyChanged("All");
        }

        private static string GetModuleKey(string cacheKey)
        {
            string key = (cacheKey ?? string.Empty).Trim().ToLowerInvariant();
            if (key.StartsWith("clients:") || key.StartsWith("sites:")) return "Clients";
            if (key.StartsWith("vendors:")) return "Suppliers";
            if (key.StartsWith("inventory:")) return "Inventory";
            if (key.StartsWith("invoices:")) return "Invoices";
            if (key.StartsWith("payments:")) return "Payments";
            if (key.StartsWith("purchases:")) return "Purchases";
            if (key.StartsWith("tenders:") || key.StartsWith("quotations:")) return "Quotations";
            if (key.StartsWith("jobs:")) return "Jobs";
            if (key.StartsWith("employees:")) return "Employees";
            if (key.StartsWith("servicedesk:")) return "Service Operations";
            return "Master Data";
        }
    }
}
