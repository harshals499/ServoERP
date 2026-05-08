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
            if (!string.IsNullOrWhiteSpace(key))
                Entries.TryRemove(key, out _);
        }

        public static void RemovePrefix(string prefix)
        {
            if (string.IsNullOrWhiteSpace(prefix))
                return;

            foreach (var key in Entries.Keys)
            {
                if (key.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
                    Entries.TryRemove(key, out _);
            }
        }

        public static void Clear()
        {
            Entries.Clear();
            KeyLocks.Clear();
        }
    }
}
