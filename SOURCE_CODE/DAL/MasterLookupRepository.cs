using System;
using System.Collections.Generic;
using System.Data.SqlClient;
using System.Linq;
using Dapper;
using HVAC_Pro_Desktop.Models;

namespace HVAC_Pro_Desktop.DAL
{
    public sealed class MasterLookupRepository
    {
        private readonly DatabaseManager _db = new DatabaseManager();

        public List<MasterLookupCategory> GetCategories(bool includeInactive = false)
        {
            try
            {
                using (SqlConnection conn = _db.GetConnection())
                {
                    return conn.Query<MasterLookupCategory>(@"
                        SELECT CategoryId, CategoryKey, ModuleKey, DisplayName, Description,
                               IsSystem, IsActive, SortOrder, CreatedDate, ModifiedDate
                        FROM MasterLookupCategories
                        WHERE (@includeInactive = 1 OR IsActive = 1)
                        ORDER BY ModuleKey, SortOrder, DisplayName;",
                        new { includeInactive = includeInactive ? 1 : 0 }).ToList();
                }
            }
            catch
            {
                return new List<MasterLookupCategory>();
            }
        }

        public List<MasterLookupValue> GetValues(string categoryKey, bool includeInactive = false)
        {
            if (string.IsNullOrWhiteSpace(categoryKey))
                return new List<MasterLookupValue>();

            try
            {
                using (SqlConnection conn = _db.GetConnection())
                {
                    return conn.Query<MasterLookupValue>(@"
                        SELECT v.ValueId, v.CategoryId, c.CategoryKey, c.ModuleKey,
                               v.ValueCode, v.DisplayText, v.Description, v.MetadataJson,
                               v.IsDefault, v.IsSystem, v.IsActive, v.SortOrder,
                               v.CreatedDate, v.ModifiedDate
                        FROM MasterLookupValues v
                        INNER JOIN MasterLookupCategories c ON c.CategoryId = v.CategoryId
                        WHERE c.CategoryKey = @categoryKey
                          AND (@includeInactive = 1 OR v.IsActive = 1)
                        ORDER BY v.SortOrder, v.DisplayText;",
                        new { categoryKey = categoryKey.Trim(), includeInactive = includeInactive ? 1 : 0 }).ToList();
                }
            }
            catch
            {
                return new List<MasterLookupValue>();
            }
        }

        public int SaveCategory(MasterLookupCategory category)
        {
            if (category == null)
                throw new ArgumentNullException(nameof(category));

            using (SqlConnection conn = _db.GetConnection())
            {
                conn.Open();
                if (category.CategoryId > 0)
                {
                    conn.Execute(@"
                        UPDATE MasterLookupCategories
                           SET ModuleKey = @ModuleKey,
                               DisplayName = @DisplayName,
                               Description = @Description,
                               IsActive = @IsActive,
                               SortOrder = @SortOrder,
                               ModifiedDate = GETDATE()
                         WHERE CategoryId = @CategoryId;", category);
                    return category.CategoryId;
                }

                return conn.QuerySingle<int>(@"
                    INSERT INTO MasterLookupCategories
                        (CategoryKey, ModuleKey, DisplayName, Description, IsSystem, IsActive, SortOrder)
                    VALUES
                        (@CategoryKey, @ModuleKey, @DisplayName, @Description, 0, @IsActive, @SortOrder);
                    SELECT CAST(SCOPE_IDENTITY() AS INT);", category);
            }
        }

        public int SaveValue(MasterLookupValue value)
        {
            if (value == null)
                throw new ArgumentNullException(nameof(value));

            using (SqlConnection conn = _db.GetConnection())
            {
                conn.Open();
                if (value.ValueId > 0)
                {
                    conn.Execute(@"
                        UPDATE MasterLookupValues
                           SET ValueCode = @ValueCode,
                               DisplayText = @DisplayText,
                               Description = @Description,
                               MetadataJson = @MetadataJson,
                               IsDefault = @IsDefault,
                               IsActive = @IsActive,
                               SortOrder = @SortOrder,
                               ModifiedDate = GETDATE()
                         WHERE ValueId = @ValueId;", value);
                    return value.ValueId;
                }

                return conn.QuerySingle<int>(@"
                    INSERT INTO MasterLookupValues
                        (CategoryId, ValueCode, DisplayText, Description, MetadataJson, IsDefault, IsSystem, IsActive, SortOrder)
                    VALUES
                        (@CategoryId, @ValueCode, @DisplayText, @Description, @MetadataJson, @IsDefault, 0, @IsActive, @SortOrder);
                    SELECT CAST(SCOPE_IDENTITY() AS INT);", value);
            }
        }
    }
}
