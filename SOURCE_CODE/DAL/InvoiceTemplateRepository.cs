using System;
using System.Collections.Generic;
using System.Data.SqlClient;
using HVAC_Pro_Desktop.Models;

namespace HVAC_Pro_Desktop.DAL
{
    public class InvoiceTemplateRepository
    {
        private readonly DatabaseManager _db = new DatabaseManager();

        public List<InvoiceTemplate> GetAll()
        {
            var list = new List<InvoiceTemplate>();
            using (var conn = _db.GetConnection())
            {
                conn.Open();
                using (var cmd = new SqlCommand("SELECT * FROM InvoiceTemplates WHERE IsActive = 1 ORDER BY TemplateName", conn))
                using (var reader = cmd.ExecuteReader())
                {
                    while (reader.Read())
                        list.Add(Map(reader));
                }
            }

            return list;
        }

        public InvoiceTemplate GetByCode(string templateCode)
        {
            using (var conn = _db.GetConnection())
            {
                conn.Open();
                using (var cmd = new SqlCommand("SELECT TOP 1 * FROM InvoiceTemplates WHERE TemplateCode = @code", conn))
                {
                    cmd.Parameters.AddWithValue("@code", templateCode ?? string.Empty);
                    using (var reader = cmd.ExecuteReader())
                        return reader.Read() ? Map(reader) : null;
                }
            }
        }

        private InvoiceTemplate Map(SqlDataReader reader)
        {
            return new InvoiceTemplate
            {
                TemplateID = Convert.ToInt32(reader["TemplateID"]),
                TemplateCode = reader["TemplateCode"] as string,
                TemplateName = reader["TemplateName"] as string,
                WorkflowType = reader["WorkflowType"] as string,
                DefaultSubject = reader["DefaultSubject"] as string,
                DefaultNotes = reader["DefaultNotes"] as string,
                DefaultGstMode = reader["DefaultGstMode"] as string,
                DefaultGstPercent = reader["DefaultGstPercent"] != DBNull.Value ? Convert.ToDecimal(reader["DefaultGstPercent"]) : 18m,
                DefaultPaymentTerms = reader["DefaultPaymentTerms"] as string,
                ContractCoverageType = reader["ContractCoverageType"] as string,
                DefaultChecklist = reader["DefaultChecklist"] as string,
                DefaultAssetInfo = reader["DefaultAssetInfo"] as string,
                IsActive = reader["IsActive"] != DBNull.Value && Convert.ToBoolean(reader["IsActive"])
            };
        }
    }
}
