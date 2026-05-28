using System;
using System.Collections.Generic;
using System.Linq;

namespace HVAC_Pro_Desktop.Tests
{
    public static class UiQaStateCatalogTests
    {
        public static List<string> RunAll()
        {
            var results = new List<string>();
            EnsureEveryModuleHasAllStates();
            EnsurePageIndexesAreUniqueForConcretePages();
            results.Add("PASS UI QA state catalog is complete");
            return results;
        }

        private static void EnsureEveryModuleHasAllStates()
        {
            string[] required = UiQaStateCatalog.RequiredStateKeys;
            foreach (UiQaModule module in UiQaStateCatalog.Modules)
            {
                foreach (string state in required)
                {
                    bool exists = UiQaStateCatalog.Matrix().Any(pair =>
                        string.Equals(pair.Item1.Key, module.Key, StringComparison.OrdinalIgnoreCase)
                        && string.Equals(pair.Item2, state, StringComparison.OrdinalIgnoreCase));
                    if (!exists)
                        throw new InvalidOperationException(module.Key + " is missing UI QA state " + state);
                }
            }
        }

        private static void EnsurePageIndexesAreUniqueForConcretePages()
        {
            var duplicates = UiQaStateCatalog.Modules
                .Where(m => m.PageIndex >= 0)
                .GroupBy(m => m.PageIndex)
                .Where(g => g.Count() > 1)
                .Select(g => g.Key.ToString())
                .ToArray();

            if (duplicates.Length > 0)
                throw new InvalidOperationException("Duplicate UI QA page indexes: " + string.Join(", ", duplicates));
        }
    }
}
