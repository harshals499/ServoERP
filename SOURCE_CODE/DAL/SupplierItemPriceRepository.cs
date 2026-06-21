using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using Dapper;
using HVAC_Pro_Desktop.Models;

namespace HVAC_Pro_Desktop.DAL
{
    public class SupplierItemPriceRepository
    {
        public List<SupplierItemPrice> GetByItemId(int itemId)
        {
            if (itemId <= 0)
                return new List<SupplierItemPrice>();

            using (var conn = DapperDatabase.CreateConnection())
            {
                conn.Open();
                EnsureSchema(conn);
                return conn.Query<SupplierItemPrice>(@"
                    SELECT
                        sip.PriceID,
                        sip.ItemID,
                        sip.VendorID,
                        v.VendorName,
                        sip.ItemName,
                        sip.Category,
                        sip.Unit,
                        sip.Rate,
                        sip.Source,
                        sip.EffectiveDate,
                        ISNULL(sip.IsPreferred, 0) AS IsPreferred,
                        ISNULL(sip.IsActive, 1) AS IsActive,
                        sip.Notes
                    FROM dbo.SupplierItemPrices sip
                    INNER JOIN dbo.Vendors v ON v.VendorID = sip.VendorID
                    WHERE sip.ItemID = @itemId
                      AND ISNULL(sip.IsActive, 1) = 1
                    ORDER BY CASE WHEN ISNULL(sip.IsPreferred, 0) = 1 THEN 0 ELSE 1 END,
                             sip.Rate,
                             v.VendorName;", new { itemId }).ToList();
            }
        }

        public List<SupplierItemPrice> GetMatchingForItem(string itemName, string category, int? itemId)
        {
            string normalizedName = (itemName ?? string.Empty).Trim();
            string normalizedCategory = (category ?? string.Empty).Trim();
            if (string.IsNullOrWhiteSpace(normalizedName) && (!itemId.HasValue || itemId.Value <= 0))
                return new List<SupplierItemPrice>();

            using (var conn = DapperDatabase.CreateConnection())
            {
                conn.Open();
                EnsureSchema(conn);
                return conn.Query<SupplierItemPrice>(@"
                    SELECT
                        sip.PriceID,
                        sip.ItemID,
                        sip.VendorID,
                        v.VendorName,
                        sip.ItemName,
                        sip.Category,
                        sip.Unit,
                        sip.Rate,
                        sip.Source,
                        sip.EffectiveDate,
                        ISNULL(sip.IsPreferred, 0) AS IsPreferred,
                        ISNULL(sip.IsActive, 1) AS IsActive,
                        sip.Notes
                    FROM dbo.SupplierItemPrices sip
                    INNER JOIN dbo.Vendors v ON v.VendorID = sip.VendorID
                    WHERE ISNULL(sip.IsActive, 1) = 1
                      AND ISNULL(v.IsActive, 1) = 1
                      AND ISNULL(v.IsArchived, 0) = 0
                      AND ISNULL(v.IsSupplier, 1) = 1
                      AND (
                            (@itemId > 0 AND sip.ItemID = @itemId)
                            OR LTRIM(RTRIM(ISNULL(sip.ItemName, ''))) = @itemName
                            OR (@category <> '' AND LTRIM(RTRIM(ISNULL(sip.Category, ''))) = @category AND LTRIM(RTRIM(ISNULL(sip.ItemName, ''))) = @itemName)
                          )
                    ORDER BY CASE WHEN ISNULL(sip.IsPreferred, 0) = 1 THEN 0 ELSE 1 END,
                             sip.Rate,
                             sip.EffectiveDate DESC,
                             v.VendorName;", new
                {
                    itemId = itemId.GetValueOrDefault(),
                    itemName = normalizedName,
                    category = normalizedCategory
                }).ToList();
            }
        }

        public void ReplaceForItem(int itemId, string itemName, string category, IEnumerable<SupplierItemPrice> prices)
        {
            if (itemId <= 0)
                throw new ArgumentException("Material item is required.", nameof(itemId));

            List<SupplierItemPrice> rows = (prices ?? Enumerable.Empty<SupplierItemPrice>())
                .Where(p => p != null && p.VendorID > 0 && p.Rate >= 0m)
                .ToList();

            using (var conn = DapperDatabase.CreateConnection())
            {
                conn.Open();
                EnsureSchema(conn);
                using (var tx = conn.BeginTransaction())
                {
                    conn.Execute(@"
                        UPDATE dbo.SupplierItemPrices
                        SET IsActive = 0,
                            IsPreferred = 0
                        WHERE ItemID = @itemId;", new { itemId }, tx);

                    if (rows.Count > 0)
                    {
                        int preferredVendorId = rows.Where(p => p.IsPreferred).Select(p => p.VendorID).FirstOrDefault();
                        if (preferredVendorId <= 0)
                            preferredVendorId = rows[0].VendorID;

                        for (int i = 0; i < rows.Count; i++)
                        {
                            SupplierItemPrice price = rows[i];
                            conn.Execute(@"
                                INSERT INTO dbo.SupplierItemPrices
                                    (ItemID, VendorID, ItemName, Category, Unit, Rate, Source, EffectiveDate, IsPreferred, IsActive, Notes)
                                VALUES
                                    (@ItemID, @VendorID, @ItemName, @Category, @Unit, @Rate, @Source, @EffectiveDate, @IsPreferred, 1, @Notes);",
                                new
                                {
                                    ItemID = itemId,
                                    VendorID = price.VendorID,
                                    ItemName = string.IsNullOrWhiteSpace(price.ItemName) ? itemName : price.ItemName.Trim(),
                                    Category = string.IsNullOrWhiteSpace(price.Category) ? category : price.Category.Trim(),
                                    Unit = string.IsNullOrWhiteSpace(price.Unit) ? "Nos" : price.Unit.Trim(),
                                    Rate = price.Rate,
                                    Source = string.IsNullOrWhiteSpace(price.Source) ? "Item details" : price.Source.Trim(),
                                    EffectiveDate = price.EffectiveDate == default(DateTime) ? DateTime.Now : price.EffectiveDate,
                                    IsPreferred = price.VendorID == preferredVendorId,
                                    Notes = string.IsNullOrWhiteSpace(price.Notes) ? null : price.Notes.Trim()
                                }, tx);
                        }
                    }

                    tx.Commit();
                }
            }
        }

        private static void EnsureSchema(IDbConnection conn)
        {
            conn.Execute(@"
                IF OBJECT_ID('dbo.SupplierItemPrices', 'U') IS NULL
                BEGIN
                    CREATE TABLE dbo.SupplierItemPrices (
                        PriceID INT PRIMARY KEY IDENTITY(1,1),
                        VendorID INT NOT NULL FOREIGN KEY REFERENCES dbo.Vendors(VendorID),
                        ItemName NVARCHAR(255) NOT NULL,
                        Category NVARCHAR(100) NULL,
                        Unit NVARCHAR(50) NULL,
                        Rate DECIMAL(12,2) NOT NULL DEFAULT 0,
                        Source NVARCHAR(100) NULL,
                        EffectiveDate DATETIME NOT NULL DEFAULT GETDATE()
                    );
                END;

                IF COL_LENGTH('dbo.SupplierItemPrices', 'ItemID') IS NULL
                    ALTER TABLE dbo.SupplierItemPrices ADD ItemID INT NULL;

                IF COL_LENGTH('dbo.SupplierItemPrices', 'IsPreferred') IS NULL
                    ALTER TABLE dbo.SupplierItemPrices ADD IsPreferred BIT NOT NULL CONSTRAINT DF_SupplierItemPrices_IsPreferred DEFAULT(0) WITH VALUES;

                IF COL_LENGTH('dbo.SupplierItemPrices', 'IsActive') IS NULL
                    ALTER TABLE dbo.SupplierItemPrices ADD IsActive BIT NOT NULL CONSTRAINT DF_SupplierItemPrices_IsActive DEFAULT(1) WITH VALUES;

                IF COL_LENGTH('dbo.SupplierItemPrices', 'Notes') IS NULL
                    ALTER TABLE dbo.SupplierItemPrices ADD Notes NVARCHAR(500) NULL;

                IF OBJECT_ID('dbo.FK_SupplierItemPrices_StockItems_ItemID', 'F') IS NULL
                   AND COL_LENGTH('dbo.SupplierItemPrices', 'ItemID') IS NOT NULL
                BEGIN
                    ALTER TABLE dbo.SupplierItemPrices
                    WITH CHECK ADD CONSTRAINT FK_SupplierItemPrices_StockItems_ItemID
                    FOREIGN KEY (ItemID) REFERENCES dbo.StockItems(ItemID);
                END;

                IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_SupplierItemPrices_ItemID' AND object_id = OBJECT_ID('dbo.SupplierItemPrices'))
                    CREATE INDEX IX_SupplierItemPrices_ItemID ON dbo.SupplierItemPrices(ItemID, IsActive, VendorID);
            ");
        }
    }
}
