using System;
using System.Collections.Generic;
using System.Data.SqlClient;
using HVAC_Pro_Desktop.Models;

namespace HVAC_Pro_Desktop.DAL
{
    public class SiteRepository
    {
        private readonly DatabaseManager _db = new DatabaseManager();

        public List<ClientSite> GetByClientId(int clientId)
        {
            var list = new List<ClientSite>();
            using (SqlConnection conn = _db.GetConnection())
            {
                conn.Open();
                using (SqlCommand cmd = new SqlCommand(
                    "SELECT * FROM ClientSites WHERE ClientID=@cid ORDER BY SiteName", conn))
                {
                    cmd.Parameters.AddWithValue("@cid", clientId);
                    using (SqlDataReader r = cmd.ExecuteReader())
                        while (r.Read()) list.Add(Map(r));
                }
            }
            return list;
        }

        public List<ClientSite> GetAll()
        {
            var list = new List<ClientSite>();
            using (SqlConnection conn = _db.GetConnection())
            {
                conn.Open();
                using (SqlCommand cmd = new SqlCommand("SELECT * FROM ClientSites ORDER BY SiteName", conn))
                using (SqlDataReader r = cmd.ExecuteReader())
                    while (r.Read()) list.Add(Map(r));
            }
            return list;
        }

        public int Create(ClientSite s)
        {
            using (SqlConnection conn = _db.GetConnection())
            {
                conn.Open();
                using (SqlCommand cmd = new SqlCommand(@"
                    INSERT INTO ClientSites (ClientID,SiteName,Address,City,ACSystemCount,RefrigerationSystemCount,CoolingTowerCount,IsCritical,AssignedTechnicianID,GeoLatitude,GeoLongitude,GeocodeAddress,GeocodeStatus,GeocodeUpdatedOn,TravelRateINR)
                    VALUES (@cid,@name,@addr,@city,@ac,@ref,@ct,@crit,@tech,@lat,@lng,@geoAddr,@geoStatus,@geoUpdatedOn,@travelRate);
                    SELECT SCOPE_IDENTITY();", conn))
                {
                    cmd.Parameters.AddWithValue("@cid",  s.ClientID);
                    cmd.Parameters.AddWithValue("@name", s.SiteName ?? "");
                    cmd.Parameters.AddWithValue("@addr", (object)s.Address ?? DBNull.Value);
                    cmd.Parameters.AddWithValue("@city", (object)s.City    ?? DBNull.Value);
                    cmd.Parameters.AddWithValue("@ac",   s.ACSystemCount);
                    cmd.Parameters.AddWithValue("@ref",  s.RefrigerationSystemCount);
                    cmd.Parameters.AddWithValue("@ct",   s.CoolingTowerCount);
                    cmd.Parameters.AddWithValue("@crit", s.IsCritical);
                    cmd.Parameters.AddWithValue("@tech", s.AssignedTechnicianID.HasValue ? (object)s.AssignedTechnicianID.Value : DBNull.Value);
                    cmd.Parameters.AddWithValue("@lat", s.GeoLatitude.HasValue ? (object)s.GeoLatitude.Value : DBNull.Value);
                    cmd.Parameters.AddWithValue("@lng", s.GeoLongitude.HasValue ? (object)s.GeoLongitude.Value : DBNull.Value);
                    cmd.Parameters.AddWithValue("@geoAddr", (object)s.GeocodeAddress ?? DBNull.Value);
                    cmd.Parameters.AddWithValue("@geoStatus", (object)s.GeocodeStatus ?? DBNull.Value);
                    cmd.Parameters.AddWithValue("@geoUpdatedOn", s.GeocodeUpdatedOn.HasValue ? (object)s.GeocodeUpdatedOn.Value : DBNull.Value);
                    cmd.Parameters.AddWithValue("@travelRate", s.TravelRateINR);
                    return (int)(decimal)cmd.ExecuteScalar();
                }
            }
        }

        public void Update(ClientSite s)
        {
            using (SqlConnection conn = _db.GetConnection())
            {
                conn.Open();
                using (SqlCommand cmd = new SqlCommand(@"
                    UPDATE ClientSites SET SiteName=@name,Address=@addr,City=@city,
                    ACSystemCount=@ac,RefrigerationSystemCount=@ref,CoolingTowerCount=@ct,
                    IsCritical=@crit,AssignedTechnicianID=@tech,
                    GeoLatitude=@lat,GeoLongitude=@lng,GeocodeAddress=@geoAddr,GeocodeStatus=@geoStatus,GeocodeUpdatedOn=@geoUpdatedOn,
                    TravelRateINR=@travelRate
                    WHERE SiteID=@id", conn))
                {
                    cmd.Parameters.AddWithValue("@id",   s.SiteID);
                    cmd.Parameters.AddWithValue("@name", s.SiteName ?? "");
                    cmd.Parameters.AddWithValue("@addr", (object)s.Address ?? DBNull.Value);
                    cmd.Parameters.AddWithValue("@city", (object)s.City    ?? DBNull.Value);
                    cmd.Parameters.AddWithValue("@ac",   s.ACSystemCount);
                    cmd.Parameters.AddWithValue("@ref",  s.RefrigerationSystemCount);
                    cmd.Parameters.AddWithValue("@ct",   s.CoolingTowerCount);
                    cmd.Parameters.AddWithValue("@crit", s.IsCritical);
                    cmd.Parameters.AddWithValue("@tech", s.AssignedTechnicianID.HasValue ? (object)s.AssignedTechnicianID.Value : DBNull.Value);
                    cmd.Parameters.AddWithValue("@lat", s.GeoLatitude.HasValue ? (object)s.GeoLatitude.Value : DBNull.Value);
                    cmd.Parameters.AddWithValue("@lng", s.GeoLongitude.HasValue ? (object)s.GeoLongitude.Value : DBNull.Value);
                    cmd.Parameters.AddWithValue("@geoAddr", (object)s.GeocodeAddress ?? DBNull.Value);
                    cmd.Parameters.AddWithValue("@geoStatus", (object)s.GeocodeStatus ?? DBNull.Value);
                    cmd.Parameters.AddWithValue("@geoUpdatedOn", s.GeocodeUpdatedOn.HasValue ? (object)s.GeocodeUpdatedOn.Value : DBNull.Value);
                    cmd.Parameters.AddWithValue("@travelRate", s.TravelRateINR);
                    cmd.ExecuteNonQuery();
                }
            }
        }

        public void UpdateGeoCoordinates(int siteId, double? latitude, double? longitude, string geocodeAddress, string geocodeStatus)
        {
            using (SqlConnection conn = _db.GetConnection())
            {
                conn.Open();
                using (SqlCommand cmd = new SqlCommand(@"
                    UPDATE ClientSites SET
                        GeoLatitude=@lat,
                        GeoLongitude=@lng,
                        GeocodeAddress=@geoAddr,
                        GeocodeStatus=@geoStatus,
                        GeocodeUpdatedOn=GETDATE()
                    WHERE SiteID=@id", conn))
                {
                    cmd.Parameters.AddWithValue("@id", siteId);
                    cmd.Parameters.AddWithValue("@lat", latitude.HasValue ? (object)latitude.Value : DBNull.Value);
                    cmd.Parameters.AddWithValue("@lng", longitude.HasValue ? (object)longitude.Value : DBNull.Value);
                    cmd.Parameters.AddWithValue("@geoAddr", (object)geocodeAddress ?? DBNull.Value);
                    cmd.Parameters.AddWithValue("@geoStatus", (object)geocodeStatus ?? DBNull.Value);
                    cmd.ExecuteNonQuery();
                }
            }
        }

        public void Delete(int siteId)
        {
            using (SqlConnection conn = _db.GetConnection())
            {
                conn.Open();
                using (SqlCommand cmd = new SqlCommand("DELETE FROM ClientSites WHERE SiteID=@id", conn))
                {
                    cmd.Parameters.AddWithValue("@id", siteId);
                    cmd.ExecuteNonQuery();
                }
            }
        }

        private static ClientSite Map(SqlDataReader r) => new ClientSite
        {
            SiteID                   = (int)r["SiteID"],
            ClientID                 = (int)r["ClientID"],
            SiteName                 = r["SiteName"] as string,
            Address                  = r["Address"]  as string,
            City                     = r["City"]     as string,
            ACSystemCount            = r["ACSystemCount"]            == DBNull.Value ? 0 : (int)r["ACSystemCount"],
            RefrigerationSystemCount = r["RefrigerationSystemCount"] == DBNull.Value ? 0 : (int)r["RefrigerationSystemCount"],
            CoolingTowerCount        = r["CoolingTowerCount"]        == DBNull.Value ? 0 : (int)r["CoolingTowerCount"],
            IsCritical               = r["IsCritical"] != DBNull.Value && (bool)r["IsCritical"],
            AssignedTechnicianID     = r["AssignedTechnicianID"] == DBNull.Value ? (int?)null : (int)r["AssignedTechnicianID"],
            GeoLatitude              = r["GeoLatitude"] == DBNull.Value ? (double?)null : Convert.ToDouble(r["GeoLatitude"]),
            GeoLongitude             = r["GeoLongitude"] == DBNull.Value ? (double?)null : Convert.ToDouble(r["GeoLongitude"]),
            GeocodeAddress           = r["GeocodeAddress"] as string,
            GeocodeStatus            = r["GeocodeStatus"] as string,
            GeocodeUpdatedOn         = r["GeocodeUpdatedOn"] == DBNull.Value ? (DateTime?)null : (DateTime)r["GeocodeUpdatedOn"],
            TravelRateINR            = r["TravelRateINR"] == DBNull.Value ? 0m : (decimal)r["TravelRateINR"],
        };
    }
}
