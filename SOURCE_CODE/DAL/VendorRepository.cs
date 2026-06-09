using System;
using System.Collections.Generic;
using System.Data.SqlClient;
using HVAC_Pro_Desktop.Models;

namespace HVAC_Pro_Desktop.DAL
{
    public class VendorRepository
    {
        private readonly DatabaseManager _db = new DatabaseManager();

        public List<Vendor> GetAll(bool includeArchived = false)
        {
            var list = new List<Vendor>();
            using (SqlConnection conn = _db.GetConnection())
            {
                conn.Open();
                using (SqlCommand cmd = new SqlCommand(
                    @"SELECT * FROM Vendors
                      WHERE (@includeArchived = 1 OR ISNULL(IsArchived, 0) = 0)
                      ORDER BY ISNULL(IsArchived, 0), VendorName", conn))
                {
                    cmd.Parameters.AddWithValue("@includeArchived", includeArchived ? 1 : 0);
                    using (SqlDataReader r = cmd.ExecuteReader())
                    {
                        while (r.Read())
                            list.Add(Map(r));
                    }
                }
            }
            return list;
        }

        /// <summary>Returns active material suppliers for purchase, inventory, stock, and supplier price-list workflows.</summary>
        public List<Vendor> GetSuppliers(bool includeArchived = false)
        {
            return GetAll(includeArchived).FindAll(v => v.IsSupplier);
        }

        /// <summary>Returns active service vendors for subcontracting, job support, labour, and external service workflows.</summary>
        public List<Vendor> GetServiceVendors(bool includeArchived = false)
        {
            return GetAll(includeArchived).FindAll(v => v.IsServiceVendor);
        }

        public Vendor GetById(int id)
        {
            using (SqlConnection conn = _db.GetConnection())
            {
                conn.Open();
                using (SqlCommand cmd = new SqlCommand(
                    "SELECT * FROM Vendors WHERE VendorID = @id", conn))
                {
                    cmd.Parameters.AddWithValue("@id", id);
                    using (SqlDataReader r = cmd.ExecuteReader())
                        return r.Read() ? Map(r) : null;
                }
            }
        }

        public int Create(Vendor v)
        {
            using (SqlConnection conn = _db.GetConnection())
            {
                conn.Open();
                using (SqlCommand cmd = new SqlCommand(@"
                    INSERT INTO Vendors
                    (VendorName,GSTNumber,DefaultCreditDays,PANNumber,Phone,Email,Address,City,Category,IsActive,
                     WhatsAppNumber,VendorType,MSMERegistered,MSMENumber,GSTRegistrationType,TDSApplicable,TDSSection,
                     TDSRate,RCMApplicable,IsSupplier,IsServiceVendor,BankAccountNumber,BankIFSC,BankAccountName,BankName,PreferredPaymentMode,StateCode,Notes,IsArchived,
                     SpecialisationTags,TotalPurchased,GeoLatitude,GeoLongitude,GeocodeAddress,GeocodeStatus,GeocodeUpdatedOn)
                    VALUES
                    (@n,@g,@credit,@p,@ph,@em,@ad,@ci,@ca,@ia,
                     @wa,@vendorType,@msmeRegistered,@msmeNumber,@gstType,@tdsApplicable,@tdsSection,
                     @tdsRate,@rcmApplicable,@isSupplier,@isServiceVendor,@bankAccountNumber,@bankIfsc,@bankAccountName,@bankName,@preferredPaymentMode,@stateCode,@notes,@isArchived,
                     @tags,@totalPurchased,@lat,@lng,@geoAddr,@geoStatus,@geoUpdatedOn);
                    SELECT SCOPE_IDENTITY();", conn))
                {
                    AddCommonParameters(cmd, v, includeId: false);
                    return (int)(decimal)cmd.ExecuteScalar();
                }
            }
        }

        public void Update(Vendor v)
        {
            using (SqlConnection conn = _db.GetConnection())
            {
                conn.Open();
                using (SqlCommand cmd = new SqlCommand(@"
                    UPDATE Vendors SET
                        VendorName=@n,
                        GSTNumber=@g,
                        DefaultCreditDays=@credit,
                        PANNumber=@p,
                        Phone=@ph,
                        Email=@em,
                        Address=@ad,
                        City=@ci,
                        Category=@ca,
                        IsActive=@ia,
                        WhatsAppNumber=@wa,
                        VendorType=@vendorType,
                        MSMERegistered=@msmeRegistered,
                        MSMENumber=@msmeNumber,
                        GSTRegistrationType=@gstType,
                        TDSApplicable=@tdsApplicable,
                        TDSSection=@tdsSection,
                        TDSRate=@tdsRate,
                        RCMApplicable=@rcmApplicable,
                        IsSupplier=@isSupplier,
                        IsServiceVendor=@isServiceVendor,
                        BankAccountNumber=@bankAccountNumber,
                        BankIFSC=@bankIfsc,
                        BankAccountName=@bankAccountName,
                        BankName=@bankName,
                        PreferredPaymentMode=@preferredPaymentMode,
                        StateCode=@stateCode,
                        Notes=@notes,
                        IsArchived=@isArchived,
                        SpecialisationTags=@tags,
                        TotalPurchased=@totalPurchased,
                        GeoLatitude=@lat,
                        GeoLongitude=@lng,
                        GeocodeAddress=@geoAddr,
                        GeocodeStatus=@geoStatus,
                        GeocodeUpdatedOn=@geoUpdatedOn
                    WHERE VendorID=@id", conn))
                {
                    AddCommonParameters(cmd, v, includeId: true);
                    cmd.ExecuteNonQuery();
                }
            }
        }

        public void UpdateGeoCoordinates(int vendorId, double? latitude, double? longitude, string geocodeAddress, string geocodeStatus)
        {
            using (SqlConnection conn = _db.GetConnection())
            {
                conn.Open();
                using (SqlCommand cmd = new SqlCommand(@"
                    UPDATE Vendors SET
                        GeoLatitude=@lat,
                        GeoLongitude=@lng,
                        GeocodeAddress=@geoAddr,
                        GeocodeStatus=@geoStatus,
                        GeocodeUpdatedOn=GETDATE()
                    WHERE VendorID=@id", conn))
                {
                    cmd.Parameters.AddWithValue("@id", vendorId);
                    cmd.Parameters.AddWithValue("@lat", latitude.HasValue ? (object)latitude.Value : DBNull.Value);
                    cmd.Parameters.AddWithValue("@lng", longitude.HasValue ? (object)longitude.Value : DBNull.Value);
                    cmd.Parameters.AddWithValue("@geoAddr", (object)geocodeAddress ?? DBNull.Value);
                    cmd.Parameters.AddWithValue("@geoStatus", (object)geocodeStatus ?? DBNull.Value);
                    cmd.ExecuteNonQuery();
                }
            }
        }

        public void Delete(int id)
        {
            using (SqlConnection conn = _db.GetConnection())
            {
                conn.Open();
                using (SqlCommand cmd = new SqlCommand(
                    "UPDATE Vendors SET IsActive=0, IsArchived=1 WHERE VendorID=@id", conn))
                {
                    cmd.Parameters.AddWithValue("@id", id);
                    cmd.ExecuteNonQuery();
                }
            }
        }

        public void SetArchived(int vendorId, bool isArchived)
        {
            using (SqlConnection conn = _db.GetConnection())
            {
                conn.Open();
                using (SqlCommand cmd = new SqlCommand(
                    "UPDATE Vendors SET IsArchived=@archived WHERE VendorID=@id", conn))
                {
                    cmd.Parameters.AddWithValue("@id", vendorId);
                    cmd.Parameters.AddWithValue("@archived", isArchived ? 1 : 0);
                    cmd.ExecuteNonQuery();
                }
            }
        }

        public void SetLifecycleStatus(int vendorId, bool isActive, bool isArchived)
        {
            using (SqlConnection conn = _db.GetConnection())
            {
                conn.Open();
                using (SqlCommand cmd = new SqlCommand(
                    "UPDATE Vendors SET IsActive=@active, IsArchived=@archived WHERE VendorID=@id", conn))
                {
                    cmd.Parameters.AddWithValue("@id", vendorId);
                    cmd.Parameters.AddWithValue("@active", isActive ? 1 : 0);
                    cmd.Parameters.AddWithValue("@archived", isArchived ? 1 : 0);
                    cmd.ExecuteNonQuery();
                }
            }
        }

        /// <summary>Moves supplier records that only qualified for derived pending approval into inactive status.</summary>
        public int MovePendingApprovalSuppliersToInactive()
        {
            using (SqlConnection conn = _db.GetConnection())
            {
                conn.Open();
                using (SqlCommand cmd = new SqlCommand(@"
                    UPDATE Vendors
                    SET IsActive = 0
                    WHERE ISNULL(IsSupplier, 1) = 1
                      AND ISNULL(IsArchived, 0) = 0
                      AND ISNULL(IsActive, 1) = 1
                      AND NULLIF(LTRIM(RTRIM(ISNULL(Phone, ''))), '') IS NULL
                      AND NULLIF(LTRIM(RTRIM(ISNULL(Category, ''))), '') IS NULL", conn))
                {
                    return cmd.ExecuteNonQuery();
                }
            }
        }

        public void SetArchivedMany(IEnumerable<int> vendorIds, bool isArchived)
        {
            if (vendorIds == null)
                return;

            using (SqlConnection conn = _db.GetConnection())
            {
                conn.Open();
                foreach (int vendorId in vendorIds)
                {
                    using (SqlCommand cmd = new SqlCommand(
                        "UPDATE Vendors SET IsArchived=@archived WHERE VendorID=@id", conn))
                    {
                        cmd.Parameters.AddWithValue("@id", vendorId);
                        cmd.Parameters.AddWithValue("@archived", isArchived ? 1 : 0);
                        cmd.ExecuteNonQuery();
                    }
                }
            }
        }

        public int CountOpenPurchaseOrders(int vendorId)
        {
            using (SqlConnection conn = _db.GetConnection())
            {
                conn.Open();
                using (SqlCommand cmd = new SqlCommand(
                    @"SELECT COUNT(*) FROM PurchaseOrders
                      WHERE VendorID = @id
                        AND ISNULL(Status, 'Pending') IN ('Draft','Pending','Pending Approval','Approval Pending','Approved','Partial','Partially Received')", conn))
                {
                    cmd.Parameters.AddWithValue("@id", vendorId);
                    return Convert.ToInt32(cmd.ExecuteScalar());
                }
            }
        }

        public void ReassignPurchaseOrders(int masterVendorId, IEnumerable<int> duplicateVendorIds)
        {
            if (duplicateVendorIds == null)
                return;

            using (SqlConnection conn = _db.GetConnection())
            {
                conn.Open();
                foreach (int vendorId in duplicateVendorIds)
                {
                    using (SqlCommand cmd = new SqlCommand(
                        "UPDATE PurchaseOrders SET VendorID=@masterId WHERE VendorID=@sourceId", conn))
                    {
                        cmd.Parameters.AddWithValue("@masterId", masterVendorId);
                        cmd.Parameters.AddWithValue("@sourceId", vendorId);
                        cmd.ExecuteNonQuery();
                    }
                }
            }
        }

        public void UpdateTotalPurchased(int vendorId, decimal totalPurchased)
        {
            using (SqlConnection conn = _db.GetConnection())
            {
                conn.Open();
                using (SqlCommand cmd = new SqlCommand(
                    "UPDATE Vendors SET TotalPurchased=@total WHERE VendorID=@id", conn))
                {
                    cmd.Parameters.AddWithValue("@id", vendorId);
                    cmd.Parameters.AddWithValue("@total", totalPurchased);
                    cmd.ExecuteNonQuery();
                }
            }
        }

        public int GetActiveCount()
        {
            using (SqlConnection conn = _db.GetConnection())
            {
                conn.Open();
                using (SqlCommand cmd = new SqlCommand(
                    "SELECT COUNT(*) FROM Vendors WHERE IsActive=1 AND ISNULL(IsArchived,0)=0", conn))
                    return Convert.ToInt32(cmd.ExecuteScalar());
            }
        }

        private void AddCommonParameters(SqlCommand cmd, Vendor v, bool includeId)
        {
            if (includeId)
                cmd.Parameters.AddWithValue("@id", v.VendorID);

            cmd.Parameters.AddWithValue("@n", (object)v.VendorName ?? DBNull.Value);
            cmd.Parameters.AddWithValue("@g", (object)v.GSTNumber ?? DBNull.Value);
            cmd.Parameters.AddWithValue("@credit", v.DefaultCreditDays <= 0 ? 30 : v.DefaultCreditDays);
            cmd.Parameters.AddWithValue("@p", (object)v.PANNumber ?? DBNull.Value);
            cmd.Parameters.AddWithValue("@ph", (object)v.Phone ?? DBNull.Value);
            cmd.Parameters.AddWithValue("@em", (object)v.Email ?? DBNull.Value);
            cmd.Parameters.AddWithValue("@ad", (object)v.Address ?? DBNull.Value);
            cmd.Parameters.AddWithValue("@ci", (object)v.City ?? DBNull.Value);
            cmd.Parameters.AddWithValue("@ca", (object)v.Category ?? DBNull.Value);
            cmd.Parameters.AddWithValue("@ia", v.IsActive ? 1 : 0);
            cmd.Parameters.AddWithValue("@wa", (object)v.WhatsAppNumber ?? DBNull.Value);
            cmd.Parameters.AddWithValue("@vendorType", string.IsNullOrWhiteSpace(v.VendorType) ? "Supplier" : v.VendorType.Trim());
            cmd.Parameters.AddWithValue("@msmeRegistered", string.IsNullOrWhiteSpace(v.MSMERegistered) ? "No" : v.MSMERegistered.Trim());
            cmd.Parameters.AddWithValue("@msmeNumber", (object)v.MSMENumber ?? DBNull.Value);
            cmd.Parameters.AddWithValue("@gstType", string.IsNullOrWhiteSpace(v.GSTRegistrationType) ? "Regular" : v.GSTRegistrationType.Trim());
            cmd.Parameters.AddWithValue("@tdsApplicable", v.TDSApplicable);
            cmd.Parameters.AddWithValue("@tdsSection", (object)v.TDSSection ?? DBNull.Value);
            cmd.Parameters.AddWithValue("@tdsRate", v.TDSRate);
            cmd.Parameters.AddWithValue("@rcmApplicable", v.RCMApplicable);
            cmd.Parameters.AddWithValue("@isSupplier", v.IsSupplier);
            cmd.Parameters.AddWithValue("@isServiceVendor", v.IsServiceVendor);
            cmd.Parameters.AddWithValue("@bankAccountNumber", (object)v.BankAccountNumber ?? DBNull.Value);
            cmd.Parameters.AddWithValue("@bankIfsc", (object)v.BankIFSC ?? DBNull.Value);
            cmd.Parameters.AddWithValue("@bankAccountName", (object)v.BankAccountName ?? DBNull.Value);
            cmd.Parameters.AddWithValue("@bankName", (object)v.BankName ?? DBNull.Value);
            cmd.Parameters.AddWithValue("@preferredPaymentMode", (object)v.PreferredPaymentMode ?? DBNull.Value);
            cmd.Parameters.AddWithValue("@stateCode", (object)v.StateCode ?? DBNull.Value);
            cmd.Parameters.AddWithValue("@notes", (object)v.Notes ?? DBNull.Value);
            cmd.Parameters.AddWithValue("@isArchived", v.IsArchived);
            cmd.Parameters.AddWithValue("@tags", (object)v.SpecialisationTags ?? DBNull.Value);
            cmd.Parameters.AddWithValue("@totalPurchased", v.TotalPurchased);
            cmd.Parameters.AddWithValue("@lat", v.GeoLatitude.HasValue ? (object)v.GeoLatitude.Value : DBNull.Value);
            cmd.Parameters.AddWithValue("@lng", v.GeoLongitude.HasValue ? (object)v.GeoLongitude.Value : DBNull.Value);
            cmd.Parameters.AddWithValue("@geoAddr", (object)v.GeocodeAddress ?? DBNull.Value);
            cmd.Parameters.AddWithValue("@geoStatus", (object)v.GeocodeStatus ?? DBNull.Value);
            cmd.Parameters.AddWithValue("@geoUpdatedOn", v.GeocodeUpdatedOn.HasValue ? (object)v.GeocodeUpdatedOn.Value : DBNull.Value);
        }

        private static Vendor Map(SqlDataReader r)
        {
            return new Vendor
            {
                VendorID = (int)r["VendorID"],
                VendorName = r["VendorName"] as string,
                GSTNumber = r["GSTNumber"] as string,
                DefaultCreditDays = r["DefaultCreditDays"] == DBNull.Value ? 30 : Convert.ToInt32(r["DefaultCreditDays"]),
                PANNumber = r["PANNumber"] as string,
                Phone = r["Phone"] as string,
                Email = r["Email"] as string,
                Address = r["Address"] as string,
                City = r["City"] as string,
                Category = r["Category"] as string,
                WhatsAppNumber = HasColumn(r, "WhatsAppNumber") ? r["WhatsAppNumber"] as string : null,
                VendorType = HasColumn(r, "VendorType") && r["VendorType"] != DBNull.Value ? Convert.ToString(r["VendorType"]) : "Supplier",
                MSMERegistered = HasColumn(r, "MSMERegistered") && r["MSMERegistered"] != DBNull.Value ? Convert.ToString(r["MSMERegistered"]) : "No",
                MSMENumber = HasColumn(r, "MSMENumber") ? r["MSMENumber"] as string : null,
                GSTRegistrationType = HasColumn(r, "GSTRegistrationType") && r["GSTRegistrationType"] != DBNull.Value ? Convert.ToString(r["GSTRegistrationType"]) : "Regular",
                TDSApplicable = HasColumn(r, "TDSApplicable") && r["TDSApplicable"] != DBNull.Value && Convert.ToBoolean(r["TDSApplicable"]),
                TDSSection = HasColumn(r, "TDSSection") ? r["TDSSection"] as string : null,
                TDSRate = HasColumn(r, "TDSRate") && r["TDSRate"] != DBNull.Value ? Convert.ToDecimal(r["TDSRate"]) : 0m,
                RCMApplicable = HasColumn(r, "RCMApplicable") && r["RCMApplicable"] != DBNull.Value && Convert.ToBoolean(r["RCMApplicable"]),
                IsSupplier = HasColumn(r, "IsSupplier") ? r["IsSupplier"] != DBNull.Value && Convert.ToBoolean(r["IsSupplier"]) : InferSupplierRole(HasColumn(r, "VendorType") ? r["VendorType"] as string : null),
                IsServiceVendor = HasColumn(r, "IsServiceVendor") ? r["IsServiceVendor"] != DBNull.Value && Convert.ToBoolean(r["IsServiceVendor"]) : InferServiceVendorRole(HasColumn(r, "VendorType") ? r["VendorType"] as string : null),
                BankAccountNumber = HasColumn(r, "BankAccountNumber") ? r["BankAccountNumber"] as string : null,
                BankIFSC = HasColumn(r, "BankIFSC") ? r["BankIFSC"] as string : null,
                BankAccountName = HasColumn(r, "BankAccountName") ? r["BankAccountName"] as string : null,
                BankName = HasColumn(r, "BankName") ? r["BankName"] as string : null,
                PreferredPaymentMode = HasColumn(r, "PreferredPaymentMode") ? r["PreferredPaymentMode"] as string : null,
                StateCode = HasColumn(r, "StateCode") ? r["StateCode"] as string : null,
                Notes = HasColumn(r, "Notes") ? r["Notes"] as string : null,
                IsArchived = HasColumn(r, "IsArchived") && r["IsArchived"] != DBNull.Value && Convert.ToBoolean(r["IsArchived"]),
                SpecialisationTags = HasColumn(r, "SpecialisationTags") ? r["SpecialisationTags"] as string : null,
                TotalPurchased = HasColumn(r, "TotalPurchased") && r["TotalPurchased"] != DBNull.Value ? Convert.ToDecimal(r["TotalPurchased"]) : 0m,
                IsActive = r["IsActive"] != DBNull.Value && Convert.ToBoolean(r["IsActive"]),
                GeoLatitude = HasColumn(r, "GeoLatitude") && r["GeoLatitude"] != DBNull.Value ? (double?)Convert.ToDouble(r["GeoLatitude"]) : null,
                GeoLongitude = HasColumn(r, "GeoLongitude") && r["GeoLongitude"] != DBNull.Value ? (double?)Convert.ToDouble(r["GeoLongitude"]) : null,
                GeocodeAddress = HasColumn(r, "GeocodeAddress") ? r["GeocodeAddress"] as string : null,
                GeocodeStatus = HasColumn(r, "GeocodeStatus") ? r["GeocodeStatus"] as string : null,
                GeocodeUpdatedOn = HasColumn(r, "GeocodeUpdatedOn") && r["GeocodeUpdatedOn"] != DBNull.Value ? (DateTime?)Convert.ToDateTime(r["GeocodeUpdatedOn"]) : null,
                CreatedDate = r["CreatedDate"] == DBNull.Value ? DateTime.Now : Convert.ToDateTime(r["CreatedDate"])
            };
        }

        private static bool InferSupplierRole(string vendorType)
        {
            string normalized = (vendorType ?? string.Empty).Trim();
            return string.IsNullOrWhiteSpace(normalized)
                || string.Equals(normalized, "Supplier", StringComparison.OrdinalIgnoreCase)
                || string.Equals(normalized, "Distributor", StringComparison.OrdinalIgnoreCase)
                || string.Equals(normalized, "Trader", StringComparison.OrdinalIgnoreCase);
        }

        private static bool InferServiceVendorRole(string vendorType)
        {
            string normalized = (vendorType ?? string.Empty).Trim();
            return string.Equals(normalized, "Vendor", StringComparison.OrdinalIgnoreCase)
                || string.Equals(normalized, "Subcontractor", StringComparison.OrdinalIgnoreCase)
                || string.Equals(normalized, "Labour", StringComparison.OrdinalIgnoreCase)
                || string.Equals(normalized, "Service Provider", StringComparison.OrdinalIgnoreCase);
        }

        private static bool HasColumn(SqlDataReader reader, string columnName)
        {
            for (int i = 0; i < reader.FieldCount; i++)
            {
                if (string.Equals(reader.GetName(i), columnName, StringComparison.OrdinalIgnoreCase))
                    return true;
            }
            return false;
        }
    }
}
