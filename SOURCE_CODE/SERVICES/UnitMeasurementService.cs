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

        private static readonly string[] CategoryOrder =
        {
            "Length",
            "Area",
            "Volume",
            "Weight and Mass",
            "Pressure",
            "Temperature",
            "Energy and Power",
            "Electrical",
            "Airflow and Velocity",
            "Refrigerant and Gas",
            "Concentration and Purity",
            "Count and Packaging",
            "Length of run",
            "Time",
            "Service billing",
            "Consumable dispensing"
        };

        private static readonly Dictionary<string, string> BuiltInAliases = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["NO"] = "NOS",
            ["NOS."] = "NOS",
            ["NOS"] = "NOS",
            ["NUMBER"] = "NOS",
            ["NUMBERS"] = "NOS",
            ["PIECE"] = "PCS",
            ["PC"] = "PCS",
            ["PCS"] = "PCS",
            ["METER"] = "MTR",
            ["METERS"] = "MTR",
            ["METRE"] = "MTR",
            ["METRES"] = "MTR",
            ["M"] = "MTR",
            ["LTR"] = "LTR",
            ["LTRS"] = "LTR",
            ["LITRE"] = "LTR",
            ["LITRES"] = "LTR",
            ["LITER"] = "LTR",
            ["LITERS"] = "LTR",
            ["L"] = "LTR",
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
            ["KILOGRAM"] = "KG",
            ["KILOGRAMS"] = "KG",
            ["KGS"] = "KG",
            ["RUNNINGMETER"] = "RMT",
            ["RUNNINGMTR"] = "RMT",
            ["RUNNINGMTRS"] = "RMT",
            ["RMETER"] = "RMT",
            ["RMT"] = "RMT",
            ["R_M_T"] = "RMT",
            ["R_MTR"] = "RMT",
            ["RUNNINGMETRE"] = "RMT",
            ["RUNNINGMETRES"] = "RMT",
            ["HOUR"] = "HOUR",
            ["HOURS"] = "HOUR",
            ["HRS"] = "HOUR",
            ["HR"] = "HOUR",
            ["DAY"] = "DAY",
            ["DAYS"] = "DAY",
            ["VISITS"] = "VISIT",
            ["SET"] = "SET",
            ["SETS"] = "SET",
            ["BOX"] = "BOX",
            ["BOXES"] = "BOX",
            ["PAIR"] = "PAIR",
            ["PAIRS"] = "PAIR",
            ["ROLL"] = "ROLL",
            ["ROLLS"] = "ROLL",
            ["CAN"] = "CAN",
            ["CANS"] = "CAN",
            ["FOOT"] = "FT",
            ["FEET"] = "FT",
            ["DRUM"] = "DRUM",
            ["DRUMS"] = "DRUM",
            ["DRUMP"] = "DRUM",
            ["DRUMPS"] = "DRUM",
            ["LOTT"] = "LOT",
            ["PUMP"] = "PUMP",
            ["PUMPS"] = "PUMP",
            ["PERCENT"] = "PERCENT",
            ["PERCENTAGE"] = "PERCENT"
        };

        private static readonly string[] UnitOrder =
        {
            "MM", "CM", "DM", "MTR", "KM", "IN", "FT", "YD", "MI", "NMI",
            "SQMM", "SQCM", "SQM", "SQKM", "SQIN", "SQFT", "SQYD", "ACRE", "HECTARE",
            "ML", "CL", "DL", "LTR", "CC", "CUM", "CUIN", "CUFT", "CUYD", "GAL_US", "GAL_IMP", "QUART", "PINT", "FLOZ", "BBL",
            "MG", "GM", "DAG", "HG", "KG", "TONNE", "OZ", "LB", "STONE", "SHORT_TON", "LONG_TON",
            "PA", "KPA", "MPA", "BAR", "MBAR", "PSI", "ATM", "TORR", "MMHG", "INHG", "INWC",
            "CELSIUS", "FAHRENHEIT", "KELVIN",
            "J", "KJ", "MW", "BTU", "BTU_HR", "TR", "KWH", "HP",
            "V", "MV", "A", "MA", "OHM", "W", "KW", "HZ", "KVA", "PF",
            "CFM", "CMH", "LPS", "MPS", "FTMIN", "RPM",
            "CYL", "CAN", "TON",
            "PPM", "PERCENT",
            "PCS", "NOS", "UNIT", "PAIR", "SET", "KIT", "BOX", "PACK", "CARTON", "BAG", "ROLL", "SHEET", "COIL", "BUNDLE", "SPOOL", "REEL", "DOZEN", "GROSS", "LOT", "PALLET", "CONTAINER", "PUMP",
            "RMT", "RFT", "RYD",
            "SEC", "MIN", "HOUR", "DAY", "WEEK", "MONTH", "QUARTER", "YEAR",
            "VISIT", "CALL", "MANHOUR", "MANDAY", "SHIFT",
            "SACHET", "TUBE", "BOTTLE", "DRUM", "JERRYCAN", "TANKER"
        };

        private static readonly Dictionary<string, string> FallbackDisplayByCode = BuildFallbackDisplayMap();
        private static readonly HashSet<string> FallbackCodes = new HashSet<string>(FallbackDisplayByCode.Keys, StringComparer.OrdinalIgnoreCase);

        public IReadOnlyList<UnitMeasurement> GetUnits()
        {
            EnsureLoaded();
            return _snapshot ?? new List<UnitMeasurement>();
        }

        public string NormalizeForDisplay(string value)
        {
            string canonical = ResolveCanonical(value);
            EnsureLoaded();
            UnitMeasurement unit = _snapshot?.FirstOrDefault(x => string.Equals(x.UnitCode, canonical, StringComparison.OrdinalIgnoreCase));
            if (unit != null)
                return BuildCompactLabel(unit);

            if (_aliasToCanonical != null && _aliasToCanonical.ContainsKey(Key(value)))
                return GetDisplayFromCanonical(_aliasToCanonical[Key(value)]);

            return string.IsNullOrWhiteSpace(value) ? GetDisplayFromCanonical(DefaultCode) : value.Trim();
        }

        public string NormalizeForDisplayOrDefault(string value)
        {
            return string.IsNullOrWhiteSpace(value) ? GetDisplayFromCanonical(DefaultCode) : NormalizeForDisplay(value);
        }

        public string NormalizeForPickerDisplay(string value)
        {
            string canonical = ResolveCanonical(value);
            EnsureLoaded();
            UnitMeasurement unit = _snapshot?.FirstOrDefault(x => string.Equals(x.UnitCode, canonical, StringComparison.OrdinalIgnoreCase));
            if (unit != null)
                return BuildPickerLabel(unit);

            return string.IsNullOrWhiteSpace(value) ? GetPickerDisplayFromCanonical(DefaultCode) : value.Trim();
        }

        public string NormalizeForPickerDisplayOrDefault(string value)
        {
            return string.IsNullOrWhiteSpace(value) ? GetPickerDisplayFromCanonical(DefaultCode) : NormalizeForPickerDisplay(value);
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
                .Select(BuildPickerLabel)
                .Where(x => !string.IsNullOrWhiteSpace(x))
                .Distinct(StringComparer.OrdinalIgnoreCase)
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

            string trimmed = value.Trim();
            if (trimmed == "%")
                return "PERCENT";

            string upper = trimmed.ToUpperInvariant();
            if (upper == "°C" || upper == "DEGC")
                return "CELSIUS";
            if (upper == "°F" || upper == "DEGF")
                return "FAHRENHEIT";

            string cleaned = Regex.Replace(upper, "[^A-Z0-9]", "");
            if (string.IsNullOrWhiteSpace(cleaned))
                return DefaultCode;

            return cleaned;
        }

        private static string Key(string value) => NormalizeToken(value);

        private static string GetDisplayFromCanonical(string canonical)
        {
            if (string.IsNullOrWhiteSpace(canonical))
                return GetCompactFallback(DefaultCode);

            if (FallbackDisplayByCode.TryGetValue(canonical.ToUpperInvariant(), out string display))
                return display;

            return canonical;
        }

        private static string GetPickerDisplayFromCanonical(string canonical)
        {
            if (string.IsNullOrWhiteSpace(canonical))
                return FallbackDisplayByCode[DefaultCode];

            if (FallbackDisplayByCode.TryGetValue(canonical.ToUpperInvariant(), out string display))
                return display;

            return canonical;
        }

        private static string GetCompactFallback(string canonical)
        {
            if (string.IsNullOrWhiteSpace(canonical))
                return "Nos";

            if (FallbackDisplayByCode.TryGetValue(canonical.ToUpperInvariant(), out string display))
            {
                int open = display.LastIndexOf('(');
                int close = display.LastIndexOf(')');
                if (open >= 0 && close > open)
                    return display.Substring(open + 1, close - open - 1);
                return display;
            }

            return canonical;
        }

        private static string BuildCompactLabel(UnitMeasurement unit)
        {
            if (unit == null)
                return GetCompactFallback(DefaultCode);

            string shortCode = (unit.ShortCode ?? unit.UnitCode ?? string.Empty).Trim();
            if (!string.IsNullOrWhiteSpace(shortCode))
                return shortCode;

            string name = (unit.DisplayName ?? string.Empty).Trim();
            return string.IsNullOrWhiteSpace(name) ? GetCompactFallback(DefaultCode) : name;
        }

        private static string BuildPickerLabel(UnitMeasurement unit)
        {
            if (unit == null)
                return GetPickerDisplayFromCanonical(DefaultCode);

            string name = (unit.DisplayName ?? string.Empty).Trim();
            string shortCode = (unit.ShortCode ?? unit.UnitCode ?? string.Empty).Trim();
            if (string.IsNullOrWhiteSpace(name))
                return string.IsNullOrWhiteSpace(shortCode) ? GetPickerDisplayFromCanonical(DefaultCode) : shortCode;
            if (string.IsNullOrWhiteSpace(shortCode))
                return name;
            return name + " (" + shortCode + ")";
        }

        private static Dictionary<string, string> BuildFallbackDisplayMap()
        {
            var map = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                ["NOS"] = "number (Nos)",
                ["PCS"] = "piece (Pcs)",
                ["KG"] = "kilogram (kg)",
                ["LTR"] = "litre (L)",
                ["MTR"] = "meter (m)",
                ["SQFT"] = "square foot (sq ft)",
                ["SQM"] = "square meter (sq m)",
                ["KIT"] = "kit (kit)",
                ["SET"] = "set (set)",
                ["BOX"] = "box (box)",
                ["PAIR"] = "pair (pair)",
                ["ROLL"] = "roll (roll)",
                ["CAN"] = "can (can)",
                ["LOT"] = "lot (lot)",
                ["DRUM"] = "drum (drum)",
                ["FT"] = "foot (ft)",
                ["PUMP"] = "pump (pump)",
                ["HOUR"] = "hour (hr)",
                ["DAY"] = "day (day)",
                ["VISIT"] = "visit (visit)",
                ["RMT"] = "running meter (RMT)"
            };

            return map;
        }

        private static int CategoryRank(string category)
        {
            int index = Array.FindIndex(CategoryOrder, item => string.Equals(item, category ?? string.Empty, StringComparison.OrdinalIgnoreCase));
            return index < 0 ? int.MaxValue : index;
        }

        private static int UnitRank(string unitCode)
        {
            int index = Array.FindIndex(UnitOrder, item => string.Equals(item, unitCode ?? string.Empty, StringComparison.OrdinalIgnoreCase));
            return index < 0 ? int.MaxValue : index;
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
                        ShortCode = code,
                        DisplayName = GetDisplayFromCanonical(code),
                        Category = string.Empty,
                        MeasurementSystem = string.Empty,
                        IsActive = true,
                        IsSystem = true
                    }).ToList();
                }

                units = units
                    .OrderBy(x => CategoryRank(x.Category))
                    .ThenBy(x => UnitRank(x.UnitCode))
                    .ThenBy(x => x.DisplayName)
                    .ToList();

                var aliases = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
                foreach (var aliasPair in BuiltInAliases)
                    aliases[NormalizeToken(aliasPair.Key)] = aliasPair.Value.ToUpperInvariant();

                foreach (var aliasPair in _repo.GetAliasMap())
                    aliases[NormalizeToken(aliasPair.Item1)] = aliasPair.Item2.ToUpperInvariant();

                foreach (var unit in units)
                {
                    aliases[NormalizeToken(unit.UnitCode)] = (unit.UnitCode ?? DefaultCode).ToUpperInvariant();
                    if (!string.IsNullOrWhiteSpace(unit.ShortCode))
                        aliases[NormalizeToken(unit.ShortCode)] = (unit.UnitCode ?? DefaultCode).ToUpperInvariant();
                    if (!string.IsNullOrWhiteSpace(unit.DisplayName))
                        aliases[NormalizeToken(unit.DisplayName)] = (unit.UnitCode ?? DefaultCode).ToUpperInvariant();
                    string displayLabel = BuildPickerLabel(unit);
                    if (!string.IsNullOrWhiteSpace(displayLabel))
                        aliases[NormalizeToken(displayLabel)] = (unit.UnitCode ?? DefaultCode).ToUpperInvariant();
                }

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
