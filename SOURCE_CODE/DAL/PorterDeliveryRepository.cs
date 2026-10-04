using System;
using System.Collections.Generic;
using System.Data.SqlClient;
using HVAC_Pro_Desktop.Models;

namespace HVAC_Pro_Desktop.DAL
{
    public class PorterDeliveryRepository
    {
        private readonly DatabaseManager _db = new DatabaseManager();

        public List<PorterDelivery> GetAll()
        {
            var items = new List<PorterDelivery>();
            using (SqlConnection connection = _db.GetConnection())
            {
                connection.Open();
                using (var command = new SqlCommand(@"
SELECT d.*, c.CompanyName AS ClientName, s.SiteName
FROM PorterDeliveries d
LEFT JOIN B2BClients c ON c.ClientID = d.ClientId
LEFT JOIN ClientSites s ON s.SiteID = d.SiteId
ORDER BY CASE WHEN d.Status IN ('Delivered','Cancelled') THEN 1 ELSE 0 END,
         ISNULL(d.ScheduledAt, d.CreatedAt) DESC;", connection))
                using (SqlDataReader reader = command.ExecuteReader())
                    while (reader.Read()) items.Add(Map(reader));
            }
            return items;
        }

        public int Save(PorterDelivery item)
        {
            using (SqlConnection connection = _db.GetConnection())
            {
                connection.Open();
                string sql = item.PorterDeliveryId <= 0 ? @"
INSERT INTO PorterDeliveries
(BookingReference, ClientId, SiteId, LinkedJobId, PickupAddress, DropAddress, ContactName, ContactPhone,
 VehicleType, ScheduledAt, Status, EstimatedAmount, FinalAmount, DriverName, DriverPhone, TrackingUrl,
 Notes, CreatedAt, CreatedByName, ModifiedAt, ModifiedByName)
VALUES
(@booking, @client, @site, @job, @pickup, @drop, @contact, @phone, @vehicle, @scheduled, @status,
 @estimated, @final, @driver, @driverPhone, @tracking, @notes, @created, @createdBy, @modified, @modifiedBy);
SELECT CONVERT(INT, SCOPE_IDENTITY());" : @"
UPDATE PorterDeliveries SET BookingReference=@booking, ClientId=@client, SiteId=@site, LinkedJobId=@job,
 PickupAddress=@pickup, DropAddress=@drop, ContactName=@contact, ContactPhone=@phone, VehicleType=@vehicle,
 ScheduledAt=@scheduled, Status=@status, EstimatedAmount=@estimated, FinalAmount=@final, DriverName=@driver,
 DriverPhone=@driverPhone, TrackingUrl=@tracking, Notes=@notes, ModifiedAt=@modified, ModifiedByName=@modifiedBy
WHERE PorterDeliveryId=@id;
SELECT @id;";
                using (var command = new SqlCommand(sql, connection))
                {
                    AddParameters(command, item);
                    command.Parameters.AddWithValue("@id", item.PorterDeliveryId);
                    return Convert.ToInt32(command.ExecuteScalar());
                }
            }
        }

        private static void AddParameters(SqlCommand command, PorterDelivery x)
        {
            command.Parameters.AddWithValue("@booking", x.BookingReference.Trim());
            command.Parameters.AddWithValue("@client", (object)x.ClientId ?? DBNull.Value);
            command.Parameters.AddWithValue("@site", (object)x.SiteId ?? DBNull.Value);
            command.Parameters.AddWithValue("@job", (object)x.LinkedJobId ?? DBNull.Value);
            command.Parameters.AddWithValue("@pickup", x.PickupAddress.Trim());
            command.Parameters.AddWithValue("@drop", x.DropAddress.Trim());
            command.Parameters.AddWithValue("@contact", Db(x.ContactName));
            command.Parameters.AddWithValue("@phone", Db(x.ContactPhone));
            command.Parameters.AddWithValue("@vehicle", Db(x.VehicleType));
            command.Parameters.AddWithValue("@scheduled", (object)x.ScheduledAt ?? DBNull.Value);
            command.Parameters.AddWithValue("@status", x.Status ?? "Booked");
            command.Parameters.AddWithValue("@estimated", x.EstimatedAmount);
            command.Parameters.AddWithValue("@final", x.FinalAmount);
            command.Parameters.AddWithValue("@driver", Db(x.DriverName));
            command.Parameters.AddWithValue("@driverPhone", Db(x.DriverPhone));
            command.Parameters.AddWithValue("@tracking", Db(x.TrackingUrl));
            command.Parameters.AddWithValue("@notes", Db(x.Notes));
            command.Parameters.AddWithValue("@created", x.CreatedAt == default(DateTime) ? DateTime.Now : x.CreatedAt);
            command.Parameters.AddWithValue("@createdBy", Db(x.CreatedByName));
            command.Parameters.AddWithValue("@modified", (object)x.ModifiedAt ?? DBNull.Value);
            command.Parameters.AddWithValue("@modifiedBy", Db(x.ModifiedByName));
        }

        private static object Db(string value) { return string.IsNullOrWhiteSpace(value) ? (object)DBNull.Value : value.Trim(); }
        private static string Str(object value) { return value == DBNull.Value ? string.Empty : Convert.ToString(value); }
        private static int? IntN(object value) { return value == DBNull.Value ? (int?)null : Convert.ToInt32(value); }
        private static DateTime? DateN(object value) { return value == DBNull.Value ? (DateTime?)null : Convert.ToDateTime(value); }

        private static PorterDelivery Map(SqlDataReader r)
        {
            return new PorterDelivery
            {
                PorterDeliveryId = Convert.ToInt32(r["PorterDeliveryId"]), BookingReference = Str(r["BookingReference"]),
                ClientId = IntN(r["ClientId"]), SiteId = IntN(r["SiteId"]), LinkedJobId = IntN(r["LinkedJobId"]),
                ClientName = Str(r["ClientName"]), SiteName = Str(r["SiteName"]), PickupAddress = Str(r["PickupAddress"]),
                DropAddress = Str(r["DropAddress"]), ContactName = Str(r["ContactName"]), ContactPhone = Str(r["ContactPhone"]),
                VehicleType = Str(r["VehicleType"]), ScheduledAt = DateN(r["ScheduledAt"]), Status = Str(r["Status"]),
                EstimatedAmount = Convert.ToDecimal(r["EstimatedAmount"]), FinalAmount = Convert.ToDecimal(r["FinalAmount"]),
                DriverName = Str(r["DriverName"]), DriverPhone = Str(r["DriverPhone"]), TrackingUrl = Str(r["TrackingUrl"]),
                Notes = Str(r["Notes"]), CreatedAt = Convert.ToDateTime(r["CreatedAt"]), CreatedByName = Str(r["CreatedByName"]),
                ModifiedAt = DateN(r["ModifiedAt"]), ModifiedByName = Str(r["ModifiedByName"])
            };
        }
    }
}
