using System;
using System.Collections.Generic;
using System.Data.SqlClient;
using HVAC_Pro_Desktop.Models;

namespace HVAC_Pro_Desktop.DAL
{
    public class MasterDataRepository
    {
        private readonly DatabaseManager _db = new DatabaseManager();

        public List<ClientAsset> GetAssets()
        {
            var list = new List<ClientAsset>();
            using (SqlConnection conn = _db.GetConnection())
            {
                conn.Open();
                using (SqlCommand cmd = new SqlCommand(@"
                    SELECT a.*, c.CompanyName AS ClientName, s.SiteName
                    FROM ClientAssets a
                    JOIN B2BClients c ON a.ClientId = c.ClientID
                    LEFT JOIN ClientSites s ON a.SiteId = s.SiteID
                    ORDER BY c.CompanyName, s.SiteName, a.EquipmentType, a.AssetTag", conn))
                using (SqlDataReader r = cmd.ExecuteReader())
                    while (r.Read()) list.Add(MapAsset(r));
            }
            return list;
        }

        public int SaveAsset(ClientAsset asset)
        {
            using (SqlConnection conn = _db.GetConnection())
            {
                conn.Open();
                if (asset.AssetId > 0)
                {
                    using (SqlCommand cmd = new SqlCommand(@"
                        UPDATE ClientAssets SET
                            ClientId=@clientId, SiteId=@siteId, ContractId=@contractId, AssetTag=@assetTag,
                            EquipmentType=@equipmentType, Brand=@brand, ModelNumber=@modelNumber,
                            SerialNumber=@serialNumber, Capacity=@capacity, LocationDetail=@locationDetail,
                            InstallDate=@installDate, WarrantyExpiry=@warrantyExpiry, IsAmcCovered=@isAmcCovered,
                            MaintenanceFrequency=@maintenanceFrequency, Notes=@notes, IsActive=@isActive,
                            ModifiedDate=GETDATE()
                        WHERE AssetId=@assetId", conn))
                    {
                        AddAssetParams(cmd, asset);
                        cmd.Parameters.AddWithValue("@assetId", asset.AssetId);
                        cmd.ExecuteNonQuery();
                        return asset.AssetId;
                    }
                }

                using (SqlCommand cmd = new SqlCommand(@"
                    INSERT INTO ClientAssets
                        (ClientId, SiteId, ContractId, AssetTag, EquipmentType, Brand, ModelNumber, SerialNumber,
                         Capacity, LocationDetail, InstallDate, WarrantyExpiry, IsAmcCovered, MaintenanceFrequency,
                         Notes, IsActive)
                    VALUES
                        (@clientId, @siteId, @contractId, @assetTag, @equipmentType, @brand, @modelNumber, @serialNumber,
                         @capacity, @locationDetail, @installDate, @warrantyExpiry, @isAmcCovered, @maintenanceFrequency,
                         @notes, @isActive);
                    SELECT SCOPE_IDENTITY();", conn))
                {
                    AddAssetParams(cmd, asset);
                    return (int)(decimal)cmd.ExecuteScalar();
                }
            }
        }

        public List<ClientDocument> GetDocuments()
        {
            var list = new List<ClientDocument>();
            using (SqlConnection conn = _db.GetConnection())
            {
                conn.Open();
                using (SqlCommand cmd = new SqlCommand(@"
                    SELECT d.*, c.CompanyName AS ClientName, s.SiteName
                    FROM ClientDocuments d
                    LEFT JOIN B2BClients c ON d.ClientId = c.ClientID
                    LEFT JOIN ClientSites s ON d.SiteId = s.SiteID
                    ORDER BY d.UploadedDate DESC, d.DocumentType, d.Title", conn))
                using (SqlDataReader r = cmd.ExecuteReader())
                    while (r.Read()) list.Add(MapDocument(r));
            }
            return list;
        }

        public int SaveDocument(ClientDocument doc)
        {
            using (SqlConnection conn = _db.GetConnection())
            {
                conn.Open();
                using (SqlCommand cmd = new SqlCommand(@"
                    INSERT INTO ClientDocuments
                        (ClientId, SiteId, AssetId, ContractId, DocumentType, Title, FilePath, OriginalFileName,
                         ExpiryDate, Notes, UploadedBy)
                    VALUES
                        (@clientId, @siteId, @assetId, @contractId, @documentType, @title, @filePath, @originalFileName,
                         @expiryDate, @notes, @uploadedBy);
                    SELECT SCOPE_IDENTITY();", conn))
                {
                    cmd.Parameters.AddWithValue("@clientId", Db(doc.ClientId));
                    cmd.Parameters.AddWithValue("@siteId", Db(doc.SiteId));
                    cmd.Parameters.AddWithValue("@assetId", Db(doc.AssetId));
                    cmd.Parameters.AddWithValue("@contractId", Db(doc.ContractId));
                    cmd.Parameters.AddWithValue("@documentType", Db(doc.DocumentType));
                    cmd.Parameters.AddWithValue("@title", Db(doc.Title));
                    cmd.Parameters.AddWithValue("@filePath", Db(doc.FilePath));
                    cmd.Parameters.AddWithValue("@originalFileName", Db(doc.OriginalFileName));
                    cmd.Parameters.AddWithValue("@expiryDate", Db(doc.ExpiryDate));
                    cmd.Parameters.AddWithValue("@notes", Db(doc.Notes));
                    cmd.Parameters.AddWithValue("@uploadedBy", Db(doc.UploadedBy));
                    return (int)(decimal)cmd.ExecuteScalar();
                }
            }
        }

        public List<ServiceRateCard> GetRateCards()
        {
            var list = new List<ServiceRateCard>();
            using (SqlConnection conn = _db.GetConnection())
            {
                conn.Open();
                using (SqlCommand cmd = new SqlCommand(@"
                    SELECT r.*, c.CompanyName AS ClientName
                    FROM ServiceRateCards r
                    LEFT JOIN B2BClients c ON r.ClientId = c.ClientID
                    ORDER BY r.IsActive DESC, r.Category, r.ServiceName", conn))
                using (SqlDataReader r = cmd.ExecuteReader())
                    while (r.Read()) list.Add(MapRateCard(r));
            }
            return list;
        }

        public int SaveRateCard(ServiceRateCard rate)
        {
            using (SqlConnection conn = _db.GetConnection())
            {
                conn.Open();
                if (rate.RateId > 0)
                {
                    using (SqlCommand cmd = new SqlCommand(@"
                        UPDATE ServiceRateCards SET
                            ClientId=@clientId, Category=@category, ServiceName=@serviceName, Unit=@unit,
                            Rate=@rate, GstPercent=@gstPercent, IsEmergencyRate=@isEmergencyRate,
                            EffectiveFrom=@effectiveFrom, Notes=@notes, IsActive=@isActive
                        WHERE RateId=@rateId", conn))
                    {
                        AddRateParams(cmd, rate);
                        cmd.Parameters.AddWithValue("@rateId", rate.RateId);
                        cmd.ExecuteNonQuery();
                        return rate.RateId;
                    }
                }

                using (SqlCommand cmd = new SqlCommand(@"
                    INSERT INTO ServiceRateCards
                        (ClientId, Category, ServiceName, Unit, Rate, GstPercent, IsEmergencyRate, EffectiveFrom, Notes, IsActive)
                    VALUES
                        (@clientId, @category, @serviceName, @unit, @rate, @gstPercent, @isEmergencyRate, @effectiveFrom, @notes, @isActive);
                    SELECT SCOPE_IDENTITY();", conn))
                {
                    AddRateParams(cmd, rate);
                    return (int)(decimal)cmd.ExecuteScalar();
                }
            }
        }

        public List<PrivateServerConnection> GetPrivateServerConnections()
        {
            var list = new List<PrivateServerConnection>();
            using (SqlConnection conn = _db.GetConnection())
            {
                conn.Open();
                using (SqlCommand cmd = new SqlCommand("SELECT * FROM PrivateServerConnections ORDER BY IsActive DESC, ConnectionName", conn))
                using (SqlDataReader r = cmd.ExecuteReader())
                    while (r.Read()) list.Add(MapConnection(r));
            }
            return list;
        }

        public int SavePrivateServerConnection(PrivateServerConnection connection)
        {
            using (SqlConnection conn = _db.GetConnection())
            {
                conn.Open();
                if (connection.ConnectionId > 0)
                {
                    using (SqlCommand cmd = new SqlCommand(@"
                        UPDATE PrivateServerConnections SET
                            ConnectionName=@connectionName, ServerType=@serverType, Host=@host, Port=@port,
                            DatabaseName=@databaseName, ApiBaseUrl=@apiBaseUrl, Username=@username,
                            EncryptedSecret=@encryptedSecret, SyncDirection=@syncDirection, LastSyncStatus=@lastSyncStatus,
                            LastSyncDate=@lastSyncDate, IsActive=@isActive, ModifiedDate=GETDATE()
                        WHERE ConnectionId=@connectionId", conn))
                    {
                        AddConnectionParams(cmd, connection);
                        cmd.Parameters.AddWithValue("@connectionId", connection.ConnectionId);
                        cmd.ExecuteNonQuery();
                        return connection.ConnectionId;
                    }
                }

                using (SqlCommand cmd = new SqlCommand(@"
                    INSERT INTO PrivateServerConnections
                        (ConnectionName, ServerType, Host, Port, DatabaseName, ApiBaseUrl, Username, EncryptedSecret,
                         SyncDirection, LastSyncStatus, LastSyncDate, IsActive)
                    VALUES
                        (@connectionName, @serverType, @host, @port, @databaseName, @apiBaseUrl, @username, @encryptedSecret,
                         @syncDirection, @lastSyncStatus, @lastSyncDate, @isActive);
                    SELECT SCOPE_IDENTITY();", conn))
                {
                    AddConnectionParams(cmd, connection);
                    return (int)(decimal)cmd.ExecuteScalar();
                }
            }
        }

        public List<DataImportBatch> GetImportBatches()
        {
            var list = new List<DataImportBatch>();
            using (SqlConnection conn = _db.GetConnection())
            {
                conn.Open();
                using (SqlCommand cmd = new SqlCommand("SELECT * FROM DataImportBatches ORDER BY StartedAt DESC", conn))
                using (SqlDataReader r = cmd.ExecuteReader())
                    while (r.Read()) list.Add(MapBatch(r));
            }
            return list;
        }

        public int Count(string tableName)
        {
            string safeTableName = ToSafeIdentifier(tableName);
            if (string.IsNullOrWhiteSpace(safeTableName))
                return 0;

            using (SqlConnection conn = _db.GetConnection())
            {
                conn.Open();
                using (SqlCommand cmd = new SqlCommand("SELECT COUNT(*) FROM [" + safeTableName + "]", conn))
                    return Convert.ToInt32(cmd.ExecuteScalar());
            }
        }

        private static string ToSafeIdentifier(string value)
        {
            if (string.IsNullOrWhiteSpace(value))
                return string.Empty;

            value = value.Trim();
            foreach (char c in value)
            {
                if (!(char.IsLetterOrDigit(c) || c == '_'))
                    return string.Empty;
            }

            return value.Replace("]", "]]");
        }

        /// <summary>Counts records for a known master-data upload module using whitelisted SQL only.</summary>
        public int CountUploadRecords(string moduleKey)
        {
            string sql = ResolveUploadCountSql(moduleKey);
            if (string.IsNullOrWhiteSpace(sql))
                return 0;

            using (SqlConnection conn = _db.GetConnection())
            {
                conn.Open();
                using (SqlCommand cmd = new SqlCommand(sql, conn))
                    return Convert.ToInt32(cmd.ExecuteScalar() ?? 0);
            }
        }

        /// <summary>Resolves safe count SQL for Master Data smart upload cards.</summary>
        private static string ResolveUploadCountSql(string moduleKey)
        {
            switch ((moduleKey ?? string.Empty).Trim())
            {
                case "Clients":
                    return "SELECT COUNT(*) FROM dbo.B2BClients;";
                case "Suppliers":
                    return @"
                        IF COL_LENGTH('dbo.Vendors', 'IsSupplier') IS NULL AND COL_LENGTH('dbo.Vendors', 'IsArchived') IS NULL
                        BEGIN
                            IF COL_LENGTH('dbo.Vendors', 'IsActive') IS NULL
                                EXEC sp_executesql N'SELECT COUNT(*) FROM dbo.Vendors;';
                            ELSE
                                EXEC sp_executesql N'SELECT COUNT(*) FROM dbo.Vendors WHERE ISNULL(IsActive, 1) = 1;';
                        END
                        ELSE IF COL_LENGTH('dbo.Vendors', 'IsSupplier') IS NULL
                        BEGIN
                            IF COL_LENGTH('dbo.Vendors', 'IsActive') IS NULL
                                EXEC sp_executesql N'SELECT COUNT(*) FROM dbo.Vendors WHERE ISNULL(IsArchived, 0) = 0;';
                            ELSE
                                EXEC sp_executesql N'SELECT COUNT(*) FROM dbo.Vendors WHERE ISNULL(IsArchived, 0) = 0 AND ISNULL(IsActive, 1) = 1;';
                        END
                        ELSE IF COL_LENGTH('dbo.Vendors', 'IsArchived') IS NULL
                        BEGIN
                            IF COL_LENGTH('dbo.Vendors', 'IsActive') IS NULL
                                EXEC sp_executesql N'SELECT COUNT(*) FROM dbo.Vendors WHERE ISNULL(IsSupplier, 1) = 1;';
                            ELSE
                                EXEC sp_executesql N'SELECT COUNT(*) FROM dbo.Vendors WHERE ISNULL(IsSupplier, 1) = 1 AND ISNULL(IsActive, 1) = 1;';
                        END
                        ELSE IF COL_LENGTH('dbo.Vendors', 'IsActive') IS NULL
                            EXEC sp_executesql N'SELECT COUNT(*) FROM dbo.Vendors WHERE ISNULL(IsSupplier, 1) = 1 AND ISNULL(IsArchived, 0) = 0;';
                        ELSE
                            EXEC sp_executesql N'SELECT COUNT(*) FROM dbo.Vendors WHERE ISNULL(IsSupplier, 1) = 1 AND ISNULL(IsArchived, 0) = 0 AND ISNULL(IsActive, 1) = 1;';";
                case "Sites":
                    return "SELECT COUNT(*) FROM dbo.ClientSites;";
                case "Inventory":
                    return "SELECT COUNT(*) FROM dbo.StockItems;";
                case "Purchases":
                    return "SELECT COUNT(*) FROM dbo.PurchaseOrders;";
                case "Invoices":
                    return "SELECT COUNT(*) FROM dbo.Invoices;";
                case "Payments":
                    return "SELECT COUNT(*) FROM dbo.Payments;";
                case "Quotations":
                    return "SELECT COUNT(*) FROM dbo.Quotations;";
                case "Jobs":
                    return "SELECT COUNT(*) FROM dbo.Jobs;";
                case "Employees":
                    return "SELECT COUNT(*) FROM dbo.Employees;";
                default:
                    return null;
            }
        }

        private static void AddAssetParams(SqlCommand cmd, ClientAsset asset)
        {
            cmd.Parameters.AddWithValue("@clientId", asset.ClientId);
            cmd.Parameters.AddWithValue("@siteId", Db(asset.SiteId));
            cmd.Parameters.AddWithValue("@contractId", Db(asset.ContractId));
            cmd.Parameters.AddWithValue("@assetTag", Db(asset.AssetTag));
            cmd.Parameters.AddWithValue("@equipmentType", Db(asset.EquipmentType));
            cmd.Parameters.AddWithValue("@brand", Db(asset.Brand));
            cmd.Parameters.AddWithValue("@modelNumber", Db(asset.ModelNumber));
            cmd.Parameters.AddWithValue("@serialNumber", Db(asset.SerialNumber));
            cmd.Parameters.AddWithValue("@capacity", Db(asset.Capacity));
            cmd.Parameters.AddWithValue("@locationDetail", Db(asset.LocationDetail));
            cmd.Parameters.AddWithValue("@installDate", Db(asset.InstallDate));
            cmd.Parameters.AddWithValue("@warrantyExpiry", Db(asset.WarrantyExpiry));
            cmd.Parameters.AddWithValue("@isAmcCovered", asset.IsAmcCovered);
            cmd.Parameters.AddWithValue("@maintenanceFrequency", Db(asset.MaintenanceFrequency));
            cmd.Parameters.AddWithValue("@notes", Db(asset.Notes));
            cmd.Parameters.AddWithValue("@isActive", asset.IsActive);
        }

        private static void AddRateParams(SqlCommand cmd, ServiceRateCard rate)
        {
            cmd.Parameters.AddWithValue("@clientId", Db(rate.ClientId));
            cmd.Parameters.AddWithValue("@category", Db(rate.Category));
            cmd.Parameters.AddWithValue("@serviceName", Db(rate.ServiceName));
            cmd.Parameters.AddWithValue("@unit", Db(rate.Unit));
            cmd.Parameters.AddWithValue("@rate", rate.Rate);
            cmd.Parameters.AddWithValue("@gstPercent", rate.GstPercent);
            cmd.Parameters.AddWithValue("@isEmergencyRate", rate.IsEmergencyRate);
            cmd.Parameters.AddWithValue("@effectiveFrom", rate.EffectiveFrom == default(DateTime) ? DateTime.Today : rate.EffectiveFrom);
            cmd.Parameters.AddWithValue("@notes", Db(rate.Notes));
            cmd.Parameters.AddWithValue("@isActive", rate.IsActive);
        }

        private static void AddConnectionParams(SqlCommand cmd, PrivateServerConnection connection)
        {
            cmd.Parameters.AddWithValue("@connectionName", Db(connection.ConnectionName));
            cmd.Parameters.AddWithValue("@serverType", Db(connection.ServerType));
            cmd.Parameters.AddWithValue("@host", Db(connection.Host));
            cmd.Parameters.AddWithValue("@port", Db(connection.Port));
            cmd.Parameters.AddWithValue("@databaseName", Db(connection.DatabaseName));
            cmd.Parameters.AddWithValue("@apiBaseUrl", Db(connection.ApiBaseUrl));
            cmd.Parameters.AddWithValue("@username", Db(connection.Username));
            cmd.Parameters.AddWithValue("@encryptedSecret", Db(connection.EncryptedSecret));
            cmd.Parameters.AddWithValue("@syncDirection", Db(connection.SyncDirection));
            cmd.Parameters.AddWithValue("@lastSyncStatus", Db(connection.LastSyncStatus));
            cmd.Parameters.AddWithValue("@lastSyncDate", Db(connection.LastSyncDate));
            cmd.Parameters.AddWithValue("@isActive", connection.IsActive);
        }

        private static ClientAsset MapAsset(SqlDataReader r)
        {
            return new ClientAsset
            {
                AssetId = (int)r["AssetId"],
                ClientId = (int)r["ClientId"],
                SiteId = r["SiteId"] == DBNull.Value ? (int?)null : (int)r["SiteId"],
                ContractId = r["ContractId"] == DBNull.Value ? (int?)null : (int)r["ContractId"],
                ClientName = r["ClientName"] as string,
                SiteName = r["SiteName"] as string,
                AssetTag = r["AssetTag"] as string,
                EquipmentType = r["EquipmentType"] as string,
                Brand = r["Brand"] as string,
                ModelNumber = r["ModelNumber"] as string,
                SerialNumber = r["SerialNumber"] as string,
                Capacity = r["Capacity"] as string,
                LocationDetail = r["LocationDetail"] as string,
                InstallDate = r["InstallDate"] == DBNull.Value ? (DateTime?)null : (DateTime)r["InstallDate"],
                WarrantyExpiry = r["WarrantyExpiry"] == DBNull.Value ? (DateTime?)null : (DateTime)r["WarrantyExpiry"],
                IsAmcCovered = (bool)r["IsAmcCovered"],
                MaintenanceFrequency = r["MaintenanceFrequency"] as string,
                Notes = r["Notes"] as string,
                IsActive = (bool)r["IsActive"],
                CreatedDate = (DateTime)r["CreatedDate"],
                ModifiedDate = (DateTime)r["ModifiedDate"]
            };
        }

        private static ClientDocument MapDocument(SqlDataReader r)
        {
            return new ClientDocument
            {
                DocumentId = (int)r["DocumentId"],
                ClientId = r["ClientId"] == DBNull.Value ? (int?)null : (int)r["ClientId"],
                SiteId = r["SiteId"] == DBNull.Value ? (int?)null : (int)r["SiteId"],
                AssetId = r["AssetId"] == DBNull.Value ? (int?)null : (int)r["AssetId"],
                ContractId = r["ContractId"] == DBNull.Value ? (int?)null : (int)r["ContractId"],
                ClientName = r["ClientName"] as string,
                SiteName = r["SiteName"] as string,
                DocumentType = r["DocumentType"] as string,
                Title = r["Title"] as string,
                FilePath = r["FilePath"] as string,
                OriginalFileName = r["OriginalFileName"] as string,
                ExpiryDate = r["ExpiryDate"] == DBNull.Value ? (DateTime?)null : (DateTime)r["ExpiryDate"],
                Notes = r["Notes"] as string,
                UploadedDate = (DateTime)r["UploadedDate"],
                UploadedBy = r["UploadedBy"] as string
            };
        }

        private static ServiceRateCard MapRateCard(SqlDataReader r)
        {
            return new ServiceRateCard
            {
                RateId = (int)r["RateId"],
                ClientId = r["ClientId"] == DBNull.Value ? (int?)null : (int)r["ClientId"],
                ClientName = r["ClientName"] as string,
                Category = r["Category"] as string,
                ServiceName = r["ServiceName"] as string,
                Unit = r["Unit"] as string,
                Rate = (decimal)r["Rate"],
                GstPercent = (decimal)r["GstPercent"],
                IsEmergencyRate = (bool)r["IsEmergencyRate"],
                EffectiveFrom = (DateTime)r["EffectiveFrom"],
                Notes = r["Notes"] as string,
                IsActive = (bool)r["IsActive"]
            };
        }

        private static PrivateServerConnection MapConnection(SqlDataReader r)
        {
            return new PrivateServerConnection
            {
                ConnectionId = (int)r["ConnectionId"],
                ConnectionName = r["ConnectionName"] as string,
                ServerType = r["ServerType"] as string,
                Host = r["Host"] as string,
                Port = r["Port"] == DBNull.Value ? (int?)null : (int)r["Port"],
                DatabaseName = r["DatabaseName"] as string,
                ApiBaseUrl = r["ApiBaseUrl"] as string,
                Username = r["Username"] as string,
                EncryptedSecret = r["EncryptedSecret"] as string,
                SyncDirection = r["SyncDirection"] as string,
                LastSyncStatus = r["LastSyncStatus"] as string,
                LastSyncDate = r["LastSyncDate"] == DBNull.Value ? (DateTime?)null : (DateTime)r["LastSyncDate"],
                IsActive = (bool)r["IsActive"],
                CreatedDate = (DateTime)r["CreatedDate"],
                ModifiedDate = (DateTime)r["ModifiedDate"]
            };
        }

        private static DataImportBatch MapBatch(SqlDataReader r)
        {
            return new DataImportBatch
            {
                BatchId = (int)r["BatchId"],
                ImportType = r["ImportType"] as string,
                SourceFile = r["SourceFile"] as string,
                Status = r["Status"] as string,
                TotalRows = (int)r["TotalRows"],
                SuccessRows = (int)r["SuccessRows"],
                ErrorRows = (int)r["ErrorRows"],
                StartedAt = (DateTime)r["StartedAt"],
                CompletedAt = r["CompletedAt"] == DBNull.Value ? (DateTime?)null : (DateTime)r["CompletedAt"],
                ImportedBy = r["ImportedBy"] as string,
                Notes = r["Notes"] as string
            };
        }

        private static object Db(object value)
        {
            return value ?? DBNull.Value;
        }
    }
}
