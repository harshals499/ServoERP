using System.Collections.Generic;
using System.Data.SqlClient;

namespace HVAC_Pro_Desktop.DAL
{
    public class SettingsRepository
    {
        private readonly DatabaseManager _db = new DatabaseManager();

        public Dictionary<string, string> GetAll()
        {
            var d = new Dictionary<string, string>();
            using (SqlConnection conn = _db.GetConnection())
            {
                conn.Open();
                using (SqlCommand cmd = new SqlCommand("SELECT SettingKey,SettingValue FROM CompanySettings", conn))
                using (SqlDataReader r = cmd.ExecuteReader())
                    while (r.Read())
                        d[r["SettingKey"].ToString()] = r["SettingValue"] as string ?? "";
            }
            return d;
        }

        public void Set(string key, string value)
        {
            using (SqlConnection conn = _db.GetConnection())
            {
                conn.Open();
                using (SqlCommand cmd = new SqlCommand(@"
                    IF EXISTS (SELECT 1 FROM CompanySettings WHERE SettingKey=@k)
                        UPDATE CompanySettings SET SettingValue=@v, UpdatedDate=GETDATE() WHERE SettingKey=@k
                    ELSE
                        INSERT INTO CompanySettings (SettingKey,SettingValue) VALUES (@k,@v);", conn))
                {
                    cmd.Parameters.AddWithValue("@k", key);
                    cmd.Parameters.AddWithValue("@v", value ?? "");
                    cmd.ExecuteNonQuery();
                }
            }
        }

        public string Get(string key, string defaultValue = "")
        {
            using (SqlConnection conn = _db.GetConnection())
            {
                conn.Open();
                using (SqlCommand cmd = new SqlCommand("SELECT SettingValue FROM CompanySettings WHERE SettingKey=@k", conn))
                {
                    cmd.Parameters.AddWithValue("@k", key);
                    var r = cmd.ExecuteScalar();
                    return r == null || r == System.DBNull.Value ? defaultValue : r.ToString();
                }
            }
        }
    }
}
