using System;
using System.Data.SqlClient;
using HVAC_Pro_Desktop.Services;

namespace HVAC_Pro_Desktop.DAL
{
    public static class DbSettings
    {
        private const string UserSettingsTableSql = @"
            IF OBJECT_ID('dbo.UserSettings', 'U') IS NULL
            BEGIN
                CREATE TABLE dbo.UserSettings (
                    SettingKey   NVARCHAR(100) NOT NULL PRIMARY KEY,
                    SettingValue NVARCHAR(255) NOT NULL
                );
            END;";

        /// <summary>Creates the user settings table when it is missing.</summary>
        public static void EnsureUserSettingsTable()
        {
            try
            {
                using (SqlConnection conn = new DatabaseManager().GetConnection())
                {
                    conn.Open();
                    using (SqlCommand cmd = new SqlCommand(UserSettingsTableSql, conn))
                    {
                        cmd.CommandTimeout = 60;
                        cmd.ExecuteNonQuery();
                    }
                }
            }
            catch (Exception ex)
            {
                AppLogger.LogError("DbSettings.EnsureUserSettingsTable", ex);
            }
        }

        /// <summary>Reads a user setting value, returning the fallback when missing.</summary>
        public static string Get(string key, string defaultValue)
        {
            try
            {
                EnsureUserSettingsTable();
                using (SqlConnection conn = new DatabaseManager().GetConnection())
                {
                    conn.Open();
                    using (SqlCommand cmd = new SqlCommand("SELECT SettingValue FROM dbo.UserSettings WHERE SettingKey = @key;", conn))
                    {
                        cmd.Parameters.AddWithValue("@key", key ?? string.Empty);
                        object value = cmd.ExecuteScalar();
                        return value == null || value == DBNull.Value ? defaultValue : value.ToString();
                    }
                }
            }
            catch (Exception ex)
            {
                AppLogger.LogError("DbSettings.Get", ex);
                return defaultValue;
            }
        }

        /// <summary>Writes a user setting value using an upsert.</summary>
        public static void Set(string key, string value)
        {
            try
            {
                EnsureUserSettingsTable();
                using (SqlConnection conn = new DatabaseManager().GetConnection())
                {
                    conn.Open();
                    using (SqlCommand cmd = new SqlCommand(@"
                        IF EXISTS (SELECT 1 FROM dbo.UserSettings WHERE SettingKey = @key)
                            UPDATE dbo.UserSettings SET SettingValue = @value WHERE SettingKey = @key;
                        ELSE
                            INSERT INTO dbo.UserSettings (SettingKey, SettingValue) VALUES (@key, @value);", conn))
                    {
                        cmd.Parameters.AddWithValue("@key", key ?? string.Empty);
                        cmd.Parameters.AddWithValue("@value", value ?? string.Empty);
                        cmd.ExecuteNonQuery();
                    }
                }
            }
            catch (Exception ex)
            {
                AppLogger.LogError("DbSettings.Set", ex);
            }
        }
    }
}
