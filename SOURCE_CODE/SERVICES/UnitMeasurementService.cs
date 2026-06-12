using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using HVAC_Pro_Desktop.DAL;
using HVAC_Pro_Desktop.Models;

namespace HVAC_Pro_Desktop.Services
{
    public sealed class UnitMeasurementService
    {
        private readonly UnitMeasurementRepository _repo = new UnitMeasurementRepository();
        private readonly object _lock = new object();
        private IReadOnlyList<UnitMeasurement> _snapshot;
        private Dictionary<string, string> _aliasToCanonical;
        public static readonly string DefaultCode = "NOS";

        private static readonly HashSet<string> FallbackCodes = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            "NOS", "PCS", "KG", "LTR", "MTR", "SQFT", "SQM", "KIT", "TIN", "SET", "BOX", "JOB", "VISIT", "LOT", "HOUR", "DAY", "RMT"
        };

        private static readonly Dictionary<string, string> BuiltInAliases = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["NO"] = "NOS",
            ["NOS."] = "NOS",
            ["NUMBER"] = "NOS",
            ["NUMBERS"] = "NOS",
            ["PIECE"] = "PCS",
            ["PIECES"] = "PCS",
            ["METER"] = "MTR",
            ["METERS"] = "MTR",
            ["METRE"] = "MTR",
            ["LTR"] = "LTR",
            ["LTRS"] = "LTR",
            ["LITRE"] = "LTR",
            ["LITRES"] = "LTR",
            ["LITER"] = "LTR",
            ["LITERS"] = "LTR",
            ["SQUAREFEET"] = "SQFT",
            ["SQUAREFOOT"] = "SQFT",
            ["SQFEET"] = "SQFT",
            ["SFT"] = "SQFT",
            ["SQM"] = "SQM",
            ["SQMT"] = "SQM",
            ["SQMTR"] = "SQM",
            ["SQMTRS"] = "SQM",
            ["SQUAREMETER"] = "SQM",
            ["SQUAREMETERS"] = "SQM",
            ["SQUAREMETRE"] = "SQM",
            ["SQUAREMETRES"] = "SQM",
            ["RUNNINGMETER"] = "RMT",
            ["RUNNINGMTR"] = "RMT",
            ["RUNNINGMTRS"] = "RMT",
            ["RMETER"] = "RMT",
            ["RMT"] = "RMT",
            ["R_M_T"] = "RMT",
            ["R_MTR"] = "RMT",
            ["RMTX"] = "RMT",
            ["HOUR"] = "HOUR",
            ["HOURS"] = "HOUR",
            ["HRS"] = "HOUR",
            ["HR"] = "HOUR",
            ["DAY"] = "DAY",
            ["DAYS"] = "DAY",
            ["SET"] = "SET",
            ["SETS"] = "SET",
            ["BOX"] = "BOX",
            ["BOXES"] = "BOX"
        };

        private static readonly Dictionary<string, string> FallbackDisplayByCode = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["NOS"] = "Nos",
            ["PCS"] = "Pcs",
            ["KG"] = "Kg",
            ["LTR"] = "Ltr",
            ["MTR"] = "Mtr",
            ["SQFT"] = "Sqft",
            ["SQM"] = "Sqm",
            ["KIT"] = "Kit",
            ["TIN"] = "Tin",
            ["SET"] = "Set",
            ["BOX"] = "Box",
            ["JOB"] = "Job",
            ["VISIT"] = "Visit",
            ["LOT"] = "Lot",
            ["HOUR"] = "Hour",
            ["DAY"] = "Day",
            ["RMT"] = "RMT"
        };

        public IReadOnlyList<UnitMeasurement> GetUnits()
        {
            EnsureLoaded();
            return _snapshot ?? new List<UnitMeasurement>();
        }

        public string NormalizeForDisplay(string value)
        {
            string canonical = ResolveCanonical(value);
            if (string.Equals(canonical, "RMT", StringComparison.OrdinalIgnoreCase))
                return "RMT";

            EnsureLoaded();
            UnitMeasurement unit = _snapshot?.FirstOrDefault(x => string.Equals(x.UnitCode, canonical, StringComparison.OrdinalIgnoreCase));
            if (unit != null && !string.IsNullOrWhiteSpace(unit.DisplayName))
                return unit.DisplayName;

            if (_aliasToCanonical != null && _aliasToCanonical.ContainsKey(Key(value)))
                return GetDisplayFromCanonical(_aliasToCanonical[Key(value)]);

            return string.IsNullOrWhiteSpace(value) ? GetDisplayFromCanonical(DefaultCode) : value.Trim();
        }

        public string NormalizeForDisplayOrDefault(string value)
        {
            return string.IsNullOrWhiteSpace(value) ? GetDisplayFromCanonical(DefaultCode) : NormalizeForDisplay(value);
        }

        public string NormalizeForStorage(string value)
        {
            string canonical = ResolveCanonical(value);
            return string.IsNullOrWhiteSpace(canonical) ? DefaultCode : canonical;
        }

        public bool IsKnownUnit(string value)
        {
            EnsureLoaded();
            string canonical = ResolveCanonical(value);
            if (string.IsNullOrWhiteSpace(canonical))
                return false;

            if (FallbackCodes.Contains(canonical))
                return true;

            return GetUnits().Any(x => string.Equals(x.UnitCode, canonical, StringComparison.OrdinalIgnoreCase) && x.IsActive);
        }

        public string[] GetDisplayUnits()
        {
            EnsureLoaded();
            return GetUnits()
                .Where(x => x.IsActive)
                .Select(x => string.Equals(x.UnitCode, "RMT", StringComparison.OrdinalIgnoreCase) ? "RMT" : x.DisplayName)
                .Where(x => !string.IsNullOrWhiteSpace(x))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .OrderBy(x => string.Equals(x, "Nos", StringComparison.OrdinalIgnoreCase) ? string.Empty : x)
                .ToArray();
        }

        public string ResolveCanonical(string value)
        {
            EnsureLoaded();
            string trimmed = (value ?? string.Empty).Trim();
            if (string.IsNullOrWhiteSpace(trimmed))
                return DefaultCode;

            string key = Key(trimmed);
            if (_aliasToCanonical != null && _aliasToCanonical.TryGetValue(key, out string canonical))
                return canonical;

            string normalized = NormalizeToken(trimmed);
            if (FallbackCodes.Contains(normalized))
                return normalized;

            if (BuiltInAliases.TryGetValue(normalized, out string builtInCanonical))
                return builtInCanonical;

            return normalized;
        }

        public bool TryAddUnit(string unitCode, string displayName, IEnumerable<string> aliases, out string message)
        {
            if (string.IsNullOrWhiteSpace(unitCode))
            {
                message = "Unit code is required.";
                return false;
            }

            if (string.IsNullOrWhiteSpace(displayName))
            {
                message = "Display name is required.";
                return false;
            }

            EnsureLoaded();
            string normalizedCode = NormalizeToken(unitCode);
            string canonicalDisplay = NormalizeForDisplay(normalizedCode);

            foreach (string existing in GetUnits().Select(x => x.UnitCode))
            {
                if (string.Equals(existing, normalizedCode, StringComparison.OrdinalIgnoreCase))
                {
                    message = "Unit code already exists.";
                    return false;
                }
            }

            if ((aliases ?? Array.Empty<string>()).Any(a => _aliasToCanonical.ContainsKey(Key(a))))
            {
                message = "Unit alias already exists.";
                return false;
            }

            var unit = new UnitMeasurement
            {
                UnitCode = normalizedCode,
                DisplayName = (displayName ?? canonicalDisplay).Trim(),
                IsActive = true,
                IsSystem = false
            };

            bool created = _repo.AddUnit(unit, BuildAliasList(aliases), out message);
            if (created)
                Invalidate();

            return created;
        }

        private static string NormalizeToken(string value)
        {
            if (string.IsNullOrWhiteSpace(value))
                return DefaultCode;

            string cleaned = Regex.Replace(value.Trim().ToUpperInvariant(), "[^A-Z0-9]", "");
            if (string.IsNullOrWhiteSpace(cleaned))
                return DefaultCode;

            return cleaned;
        }

        private static string Key(string value) => NormalizeToken(value);

        private static string GetDisplayFromCanonical(string canonical)
        {
            if (string.IsNullOrWhiteSpace(canonical))
                return FallbackDisplayByCode[DefaultCode];

            if (FallbackDisplayByCode.TryGetValue(canonical.ToUpperInvariant(), out string display))
                return display;

            return canonical;
        }

        private void EnsureLoaded()
        {
            if (_snapshot != null && _aliasToCanonical != null)
                return;

            lock (_lock)
            {
                if (_snapshot != null && _aliasToCanonical != null)
                    return;

                var units = _repo.GetAll();
                if (units == null || units.Count == 0)
                {
                    units = FallbackCodes.Select(code => new UnitMeasurement
                    {
                        UnitCode = code,
                        DisplayName = GetDisplayFromCanonical(code),
                        IsActive = true,
                        IsSystem = true
                    }).ToList();
                }

                var aliases = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
                foreach (var aliasPair in BuiltInAliases)
                    aliases[NormalizeToken(aliasPair.Key)] = aliasPair.Value.ToUpperInvariant();

                foreach (var aliasPair in _repo.GetAliasMap())
                    aliases[NormalizeToken(aliasPair.Item1)] = aliasPair.Item2.ToUpperInvariant();

                foreach (var unit in units)
                    aliases[NormalizeToken(unit.UnitCode)] = (unit.UnitCode ?? DefaultCode).ToUpperInvariant();

                _snapshot = units;
                _aliasToCanonical = aliases;
            }
        }

        private static IEnumerable<string> BuildAliasList(IEnumerable<string> aliases)
        {
            return (aliases ?? Array.Empty<string>())
                .Where(a => !string.IsNullOrWhiteSpace(a))
                .Select(a => a.Trim())
                .Distinct(StringComparer.OrdinalIgnoreCase);
        }

        private void Invalidate()
        {
            lock (_lock)
            {
                _snapshot = null;
                _aliasToCanonical = null;
            }
        }
    }
}
