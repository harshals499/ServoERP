using System;
using System.Collections.Generic;
using System.Data.SqlClient;
using HVAC_Pro_Desktop.Models;

namespace HVAC_Pro_Desktop.DAL
{
    public class HsnSacMasterRepository
    {
        private readonly DatabaseManager _db = new DatabaseManager();

        public List<HsnSacMasterEntry> GetAll()
        {
            var list = new List<HsnSacMasterEntry>();
            using (SqlConnection conn = _db.GetConnection())
            {
                conn.Open();
                using (SqlCommand cmd = new SqlCommand(@"
                    SELECT MasterID, CodeType, Code, Description, BusinessCategory,
                           TaxRate, CGSTRate, SGSTRate, IGSTRate, Notes, IsDefault, IsActive
                    FROM HsnSacMaster
                    ORDER BY IsDefault DESC, BusinessCategory ASC, CodeType ASC, Code ASC", conn))
                using (SqlDataReader reader = cmd.ExecuteReader())
                {
                    while (reader.Read())
                    {
                        list.Add(new HsnSacMasterEntry
                        {
                            MasterID = Convert.ToInt32(reader["MasterID"]),
                            CodeType = reader["CodeType"].ToString(),
                            Code = reader["Code"].ToString(),
                            Description = reader["Description"].ToString(),
                            BusinessCategory = reader["BusinessCategory"] == DBNull.Value ? string.Empty : reader["BusinessCategory"].ToString(),
                            TaxRate = Convert.ToDecimal(reader["TaxRate"]),
                            CGSTRate = Convert.ToDecimal(reader["CGSTRate"]),
                            SGSTRate = Convert.ToDecimal(reader["SGSTRate"]),
                            IGSTRate = Convert.ToDecimal(reader["IGSTRate"]),
                            Notes = reader["Notes"] == DBNull.Value ? string.Empty : reader["Notes"].ToString(),
                            IsDefault = reader["IsDefault"] != DBNull.Value && Convert.ToBoolean(reader["IsDefault"]),
                            IsActive = reader["IsActive"] == DBNull.Value || Convert.ToBoolean(reader["IsActive"])
                        });
                    }
                }
            }
            return list;
        }

        public void SaveAll(IEnumerable<HsnSacMasterEntry> entries)
        {
            using (SqlConnection conn = _db.GetConnection())
            {
                conn.Open();
                using (SqlTransaction tx = conn.BeginTransaction())
                {
                    foreach (HsnSacMasterEntry entry in entries)
                    {
                        using (SqlCommand cmd = new SqlCommand(@"
                            IF EXISTS (SELECT 1 FROM HsnSacMaster WHERE MasterID = @id)
                            BEGIN
                                UPDATE HsnSacMaster
                                SET CodeType = @codeType,
                                    Code = @code,
                                    Description = @description,
                                    BusinessCategory = @businessCategory,
                                    TaxRate = @taxRate,
                                    CGSTRate = @cgstRate,
                                    SGSTRate = @sgstRate,
                                    IGSTRate = @igstRate,
                                    Notes = @notes,
                                    IsDefault = @isDefault,
                                    IsActive = @isActive,
                                    UpdatedDate = GETDATE()
                                WHERE MasterID = @id
                            END
                            ELSE
                            BEGIN
                                INSERT INTO HsnSacMaster
                                    (CodeType, Code, Description, BusinessCategory, TaxRate, CGSTRate, SGSTRate, IGSTRate, Notes, IsDefault, IsActive)
                                VALUES
                                    (@codeType, @code, @description, @businessCategory, @taxRate, @cgstRate, @sgstRate, @igstRate, @notes, @isDefault, @isActive)
                            END", conn, tx))
                        {
                            cmd.Parameters.AddWithValue("@id", entry.MasterID);
                            cmd.Parameters.AddWithValue("@codeType", entry.CodeType ?? "HSN");
                            cmd.Parameters.AddWithValue("@code", entry.Code ?? string.Empty);
                            cmd.Parameters.AddWithValue("@description", entry.Description ?? string.Empty);
                            cmd.Parameters.AddWithValue("@businessCategory", string.IsNullOrWhiteSpace(entry.BusinessCategory) ? (object)DBNull.Value : entry.BusinessCategory.Trim());
                            cmd.Parameters.AddWithValue("@taxRate", entry.TaxRate);
                            cmd.Parameters.AddWithValue("@cgstRate", entry.CGSTRate);
                            cmd.Parameters.AddWithValue("@sgstRate", entry.SGSTRate);
                            cmd.Parameters.AddWithValue("@igstRate", entry.IGSTRate);
                            cmd.Parameters.AddWithValue("@notes", string.IsNullOrWhiteSpace(entry.Notes) ? (object)DBNull.Value : entry.Notes.Trim());
                            cmd.Parameters.AddWithValue("@isDefault", entry.IsDefault);
                            cmd.Parameters.AddWithValue("@isActive", entry.IsActive);
                            cmd.ExecuteNonQuery();
                        }
                    }

                    tx.Commit();
                }
            }
        }
    }
}
