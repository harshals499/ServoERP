using System;
using System.Collections.Generic;
using System.Data.SqlClient;
using System.Linq;
using Dapper;
using HVAC_Pro_Desktop.Models;

namespace HVAC_Pro_Desktop.DAL
{
    public class UnitMeasurementRepository
    {
        private readonly DatabaseManager _db = new DatabaseManager();

        public List<UnitMeasurement> GetAll(bool includeInactive = false)
        {
            var items = new List<UnitMeasurement>();
            try
            {
                using (var conn = _db.GetConnection())
                {
                    string sql =
                        @"SELECT UnitMeasurementID AS UnitMeasurementId, UnitCode, DisplayName, IsActive, IsSystem
                          FROM UnitMeasurements
                          WHERE (@includeInactive = 1 OR IsActive = 1)
                          ORDER BY DisplayName;";

                    items = conn.Query<UnitMeasurement>(sql, new { includeInactive = includeInactive ? 1 : 0 }).ToList();
                }
            }
            catch
            {
                // Backward-compatible fallback for legacy databases before shared unit table migration.
            }

            return items;
        }

        public List<Tuple<string, string>> GetAliasMap()
        {
            var aliases = new List<Tuple<string, string>>();
            try
            {
                using (var conn = _db.GetConnection())
                {
                    string sql =
                        @"SELECT UA.UnitAlias, UM.UnitCode
                          FROM UnitMeasurementAliases UA
                          INNER JOIN UnitMeasurements UM ON UM.UnitMeasurementID = UA.UnitMeasurementId
                          WHERE UM.IsActive = 1
                          UNION
                          SELECT UM.UnitCode, UM.UnitCode
                          FROM UnitMeasurements UM
                          WHERE UM.IsActive = 1;";

                    aliases = conn.Query<UnitMeasurementAliasRow>(sql)
                        .Where(x => !string.IsNullOrWhiteSpace(x.UnitAlias) && !string.IsNullOrWhiteSpace(x.UnitCode))
                        .Select(x => Tuple.Create(x.UnitAlias, x.UnitCode))
                        .ToList();
                }
            }
            catch
            {
                // Backward-compatible fallback for legacy databases before shared unit table migration.
            }

            return aliases;
        }

        public string GetDisplayName(string unitCode)
        {
            if (string.IsNullOrWhiteSpace(unitCode))
                return null;

            try
            {
                using (var conn = _db.GetConnection())
                {
                    return conn.QueryFirstOrDefault<string>(
                        "SELECT DisplayName FROM UnitMeasurements WHERE UnitCode = @code",
                        new { code = unitCode.Trim() });
                }
            }
            catch
            {
                return null;
            }
        }

        public bool AddUnit(UnitMeasurement unit, IEnumerable<string> aliases, out string message)
        {
            message = null;
            if (unit == null || string.IsNullOrWhiteSpace(unit.UnitCode))
            {
                message = "Unit code is required.";
                return false;
            }

            if (string.IsNullOrWhiteSpace(unit.DisplayName))
            {
                message = "Display name is required.";
                return false;
            }

            var aliasList = (aliases ?? new string[0]).Where(x => !string.IsNullOrWhiteSpace(x)).Select(x => x.Trim()).ToList();
            try
            {
                using (var conn = _db.GetConnection())
                {
                    conn.Open();
                    using (var tx = conn.BeginTransaction())
                    {
                        try
                        {
                            int unitId = conn.QuerySingle<int>(@"
                                INSERT INTO UnitMeasurements (UnitCode, DisplayName, IsActive, IsSystem)
                                VALUES (@code, @name, 1, 0);
                                SELECT CAST(SCOPE_IDENTITY() AS INT);",
                                new
                                {
                                    code = unit.UnitCode.Trim().ToUpperInvariant(),
                                    name = unit.DisplayName.Trim()
                                },
                                tx);

                            foreach (string alias in aliasList)
                            {
                                conn.Execute(
                                    @"INSERT INTO UnitMeasurementAliases (UnitAlias, UnitMeasurementId) VALUES (@alias, @id)",
                                    new { alias, id = unitId },
                                    tx);
                            }

                            tx.Commit();
                            return true;
                        }
                        catch (SqlException ex) when (ex.Number == 2627 || ex.Number == 2601)
                        {
                            tx.Rollback();
                            message = "Unit already exists.";
                            return false;
                        }
                        catch
                        {
                            tx.Rollback();
                            throw;
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                message = "Unable to save unit: " + ex.Message;
                return false;
            }
        }

        private sealed class UnitMeasurementAliasRow
        {
            public string UnitAlias { get; set; }
            public string UnitCode { get; set; }
        }
    }
}
