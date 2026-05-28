using System;
using System.Collections.Generic;
using System.Data.SqlClient;
using HVAC_Pro_Desktop.Models;

namespace HVAC_Pro_Desktop.DAL
{
    public class ClientRepository
    {
        private readonly DatabaseManager _db = new DatabaseManager();

        // ── READ ─────────────────────────────────────────────
        public List<B2BClient> GetAll()
        {
            var list = new List<B2BClient>();
            using (var conn = _db.GetConnection())
            {
                conn.Open();
                using (var cmd = new SqlCommand(
                    "SELECT * FROM B2BClients WHERE IsActive = 1 ORDER BY CompanyName", conn))
                using (var r = cmd.ExecuteReader())
                    while (r.Read()) list.Add(Map(r));
            }
            return list;
        }

        public List<B2BClient> GetAllIncludingInactive()
        {
            var list = new List<B2BClient>();
            using (var conn = _db.GetConnection())
            {
                conn.Open();
                using (var cmd = new SqlCommand(
                    "SELECT * FROM B2BClients ORDER BY CompanyName", conn))
                using (var r = cmd.ExecuteReader())
                    while (r.Read()) list.Add(Map(r));
            }
            return list;
        }

        public B2BClient GetById(int id)
        {
            using (var conn = _db.GetConnection())
            {
                conn.Open();
                B2BClient client = null;
                using (var cmd = new SqlCommand(
                    "SELECT * FROM B2BClients WHERE ClientID = @id", conn))
                {
                    cmd.Parameters.AddWithValue("@id", id);
                    using (var r = cmd.ExecuteReader())
                        if (r.Read())
                            client = Map(r);
                }
                if (client != null)
                    client.Contacts = GetContacts(conn, id);
                return client;
            }
        }

        // ── CREATE ───────────────────────────────────────────
        public int Create(B2BClient c)
        {
            using (var conn = _db.GetConnection())
            {
                conn.Open();
                const string sql = @"
                    INSERT INTO B2BClients
                        (CompanyName,IndustryType,TotalAnnualValue,PrimaryContact,SecondaryContact,
                         Phone,CustomerSince,Email,GSTNumber,PANNumber,PaymentTermsDays,
                         CreditLimit,BillingAddress,City,GeoLatitude,GeoLongitude,GeocodeAddress,GeocodeStatus,GeocodeUpdatedOn,RelationshipStage,Tags,HealthScore,Notes,AssignedTo,LeadSource,IsActive)
                    VALUES
                        (@name,@industry,@value,@contact1,@contact2,
                         @phone,@since,@email,@gst,@pan,@terms,
                         @credit,@address,@city,@geoLat,@geoLon,@geoAddress,@geoStatus,@geoUpdatedOn,@relationshipStage,@tags,@healthScore,@notes,@assignedTo,@leadSource,1);
                    SELECT SCOPE_IDENTITY();";

                using (var cmd = new SqlCommand(sql, conn))
                {
                    AddParams(cmd, c);
                    return Convert.ToInt32(cmd.ExecuteScalar());
                }
            }
        }

        // ── UPDATE ───────────────────────────────────────────
        public void Update(B2BClient c)
        {
            using (var conn = _db.GetConnection())
            {
                conn.Open();
                const string sql = @"
                    UPDATE B2BClients SET
                        CompanyName       = @name,
                        IndustryType      = @industry,
                        TotalAnnualValue  = @value,
                        PrimaryContact    = @contact1,
                        SecondaryContact  = @contact2,
                        Phone             = @phone,
                        Email             = @email,
                        GSTNumber         = @gst,
                        PANNumber         = @pan,
                        PaymentTermsDays  = @terms,
                        CreditLimit       = @credit,
                        BillingAddress    = @address,
                        City              = @city,
                        GeoLatitude       = @geoLat,
                        GeoLongitude      = @geoLon,
                        GeocodeAddress    = @geoAddress,
                        GeocodeStatus     = @geoStatus,
                        GeocodeUpdatedOn  = @geoUpdatedOn,
                        RelationshipStage = @relationshipStage,
                        Tags              = @tags,
                        HealthScore       = @healthScore,
                        Notes             = @notes,
                        AssignedTo        = @assignedTo,
                        LeadSource        = @leadSource
                    WHERE ClientID = @id;";

                using (var cmd = new SqlCommand(sql, conn))
                {
                    AddParams(cmd, c);
                    cmd.Parameters.AddWithValue("@id", c.ClientID);
                    cmd.ExecuteNonQuery();
                }
            }
        }

        // ── DELETE (soft) ────────────────────────────────────
        public void Delete(int id)
        {
            using (var conn = _db.GetConnection())
            {
                conn.Open();
                using (var cmd = new SqlCommand(
                    "UPDATE B2BClients SET IsActive = 0 WHERE ClientID = @id", conn))
                {
                    cmd.Parameters.AddWithValue("@id", id);
                    cmd.ExecuteNonQuery();
                }
            }
        }

        // ── LINKED DATA ──────────────────────────────────────
        public List<ClientSite> GetClientSites(int clientId)
        {
            var list = new List<ClientSite>();
            using (var conn = _db.GetConnection())
            {
                conn.Open();
                using (var cmd = new SqlCommand(
                    "SELECT * FROM ClientSites WHERE ClientID = @id", conn))
                {
                    cmd.Parameters.AddWithValue("@id", clientId);
                    using (var r = cmd.ExecuteReader())
                        while (r.Read())
                            list.Add(new ClientSite
                            {
                                SiteID                   = (int)r["SiteID"],
                                ClientID                 = (int)r["ClientID"],
                                SiteName                 = r["SiteName"].ToString(),
                                Address                  = r["Address"].ToString(),
                                City                     = r["City"].ToString(),
                                ACSystemCount            = (int)r["ACSystemCount"],
                                RefrigerationSystemCount = (int)r["RefrigerationSystemCount"],
                                CoolingTowerCount        = (int)r["CoolingTowerCount"],
                                IsCritical               = (bool)r["IsCritical"]
                            });
                }
            }
            return HVAC_Pro_Desktop.Services.SiteService.ApplyDisplayNames(list);
        }

        public List<ClientContact> GetClientContacts(int clientId)
        {
            using (var conn = _db.GetConnection())
            {
                conn.Open();
                return GetContacts(conn, clientId);
            }
        }

        public void ReplaceClientContacts(int clientId, List<ClientContact> contacts)
        {
            using (var conn = _db.GetConnection())
            {
                conn.Open();
                using (var tx = conn.BeginTransaction())
                {
                    using (var deleteCmd = new SqlCommand("DELETE FROM ClientContacts WHERE ClientID = @clientId", conn, tx))
                    {
                        deleteCmd.Parameters.AddWithValue("@clientId", clientId);
                        deleteCmd.ExecuteNonQuery();
                    }

                    foreach (ClientContact contact in contacts ?? new List<ClientContact>())
                    {
                        if (string.IsNullOrWhiteSpace(contact.ContactName))
                            continue;

                        using (var insertCmd = new SqlCommand(@"
                            INSERT INTO ClientContacts
                                (ClientID, ContactName, Role, Phone, Email, IsPrimary, Notes)
                            VALUES
                                (@clientId, @name, @role, @phone, @email, @isPrimary, @notes);", conn, tx))
                        {
                            insertCmd.Parameters.AddWithValue("@clientId", clientId);
                            insertCmd.Parameters.AddWithValue("@name", contact.ContactName ?? string.Empty);
                            insertCmd.Parameters.AddWithValue("@role", (object)(contact.Role ?? string.Empty));
                            insertCmd.Parameters.AddWithValue("@phone", (object)(contact.Phone ?? string.Empty));
                            insertCmd.Parameters.AddWithValue("@email", (object)(contact.Email ?? string.Empty));
                            insertCmd.Parameters.AddWithValue("@isPrimary", contact.IsPrimary);
                            insertCmd.Parameters.AddWithValue("@notes", (object)(contact.Notes ?? string.Empty));
                            insertCmd.ExecuteNonQuery();
                        }
                    }

                    tx.Commit();
                }
            }
        }

        public int CreateSite(ClientSite site)
        {
            using (var conn = _db.GetConnection())
            {
                conn.Open();
                const string sql = @"
                    INSERT INTO ClientSites
                        (ClientID,SiteName,Address,City,ACSystemCount,
                         RefrigerationSystemCount,CoolingTowerCount,IsCritical,AssignedTechnicianID)
                    VALUES (@cid,@name,@addr,@city,@ac,@ref,@ct,@crit,@tech);
                    SELECT SCOPE_IDENTITY();";
                using (var cmd = new SqlCommand(sql, conn))
                {
                    cmd.Parameters.AddWithValue("@cid",  site.ClientID);
                    cmd.Parameters.AddWithValue("@name", site.SiteName ?? "");
                    cmd.Parameters.AddWithValue("@addr", site.Address ?? "");
                    cmd.Parameters.AddWithValue("@city", site.City ?? "");
                    cmd.Parameters.AddWithValue("@ac",   site.ACSystemCount);
                    cmd.Parameters.AddWithValue("@ref",  site.RefrigerationSystemCount);
                    cmd.Parameters.AddWithValue("@ct",   site.CoolingTowerCount);
                    cmd.Parameters.AddWithValue("@crit", site.IsCritical);
                    cmd.Parameters.AddWithValue("@tech", site.AssignedTechnicianID.HasValue ? (object)site.AssignedTechnicianID.Value : DBNull.Value);
                    return Convert.ToInt32(cmd.ExecuteScalar());
                }
            }
        }

        public List<ClientTeamMember> GetTeamMembers(int clientId)
        {
            var list = new List<ClientTeamMember>();
            using (var conn = _db.GetConnection())
            {
                conn.Open();
                using (var cmd = new SqlCommand(@"
                    SELECT Id, ClientId, EmployeeName, Position, EmailId, ContactNo, IsPrimary, IsActive
                    FROM ClientTeam
                    WHERE ClientId = @clientId AND ISNULL(IsActive, 1) = 1
                    ORDER BY IsPrimary DESC, EmployeeName ASC;", conn))
                {
                    cmd.Parameters.AddWithValue("@clientId", clientId);
                    using (var r = cmd.ExecuteReader())
                    {
                        while (r.Read())
                            list.Add(MapTeamMember(r));
                    }
                }
            }
            return list;
        }

        public void SaveTeamMember(ClientTeamMember member)
        {
            using (var conn = _db.GetConnection())
            {
                conn.Open();
                if (member.Id > 0)
                {
                    using (var cmd = new SqlCommand(@"
                        UPDATE ClientTeam SET
                            ClientId=@clientId,
                            EmployeeName=@employeeName,
                            Position=@position,
                            EmailId=@emailId,
                            ContactNo=@contactNo,
                            IsPrimary=@isPrimary,
                            IsActive=@isActive
                        WHERE Id=@id;", conn))
                    {
                        AddTeamParams(cmd, member);
                        cmd.Parameters.AddWithValue("@id", member.Id);
                        cmd.ExecuteNonQuery();
                    }
                }
                else
                {
                    using (var cmd = new SqlCommand(@"
                        INSERT INTO ClientTeam
                            (ClientId, EmployeeName, Position, EmailId, ContactNo, IsPrimary, IsActive)
                        VALUES
                            (@clientId, @employeeName, @position, @emailId, @contactNo, @isPrimary, @isActive);", conn))
                    {
                        AddTeamParams(cmd, member);
                        cmd.ExecuteNonQuery();
                    }
                }
            }
        }

        public void DeleteTeamMember(int memberId)
        {
            using (var conn = _db.GetConnection())
            {
                conn.Open();
                using (var cmd = new SqlCommand("UPDATE ClientTeam SET IsActive = 0 WHERE Id = @id", conn))
                {
                    cmd.Parameters.AddWithValue("@id", memberId);
                    cmd.ExecuteNonQuery();
                }
            }
        }

        public List<ClientActivity> GetActivities(int clientId, string filterType)
        {
            var list = new List<ClientActivity>();
            using (var conn = _db.GetConnection())
            {
                conn.Open();
                string normalized = NormalizeActivityFilter(filterType);
                string where = normalized == "All" ? string.Empty : " AND ActivityType = @type";
                using (var cmd = new SqlCommand(@"
                    SELECT Id, ClientId, ActivityType, Title, Detail, CreatedAt, CreatedBy
                    FROM ClientActivity
                    WHERE ClientId = @clientId" + where + @"
                    ORDER BY CreatedAt DESC, Id DESC;", conn))
                {
                    cmd.Parameters.AddWithValue("@clientId", clientId);
                    if (normalized != "All")
                        cmd.Parameters.AddWithValue("@type", normalized);
                    using (var r = cmd.ExecuteReader())
                    {
                        while (r.Read())
                            list.Add(MapActivity(r));
                    }
                }
            }
            return list;
        }

        public void LogActivity(ClientActivity activity)
        {
            using (var conn = _db.GetConnection())
            {
                conn.Open();
                using (var cmd = new SqlCommand(@"
                    INSERT INTO ClientActivity
                        (ClientId, ActivityType, Title, Detail, CreatedAt, CreatedBy)
                    VALUES
                        (@clientId, @type, @title, @detail, @createdAt, @createdBy);", conn))
                {
                    cmd.Parameters.AddWithValue("@clientId", activity.ClientId);
                    cmd.Parameters.AddWithValue("@type", activity.ActivityType ?? "Note");
                    cmd.Parameters.AddWithValue("@title", activity.Title ?? string.Empty);
                    cmd.Parameters.AddWithValue("@detail", activity.Detail ?? string.Empty);
                    cmd.Parameters.AddWithValue("@createdAt", activity.CreatedAt == default(DateTime) ? DateTime.Now : activity.CreatedAt);
                    cmd.Parameters.AddWithValue("@createdBy", activity.CreatedBy ?? string.Empty);
                    cmd.ExecuteNonQuery();
                }
            }
        }

        public void UpdateTags(int clientId, string tags)
        {
            using (var conn = _db.GetConnection())
            {
                conn.Open();
                using (var cmd = new SqlCommand("UPDATE B2BClients SET Tags=@tags WHERE ClientID=@id", conn))
                {
                    cmd.Parameters.AddWithValue("@id", clientId);
                    cmd.Parameters.AddWithValue("@tags", (object)(tags ?? string.Empty));
                    cmd.ExecuteNonQuery();
                }
            }
        }

        public void UpdateHealthScore(int clientId, int score)
        {
            using (var conn = _db.GetConnection())
            {
                conn.Open();
                using (var cmd = new SqlCommand("UPDATE B2BClients SET HealthScore=@score WHERE ClientID=@id", conn))
                {
                    cmd.Parameters.AddWithValue("@id", clientId);
                    cmd.Parameters.AddWithValue("@score", score);
                    cmd.ExecuteNonQuery();
                }
            }
        }

        // ── HELPERS ──────────────────────────────────────────
        private void AddParams(SqlCommand cmd, B2BClient c)
        {
            cmd.Parameters.AddWithValue("@name",     c.CompanyName ?? "");
            cmd.Parameters.AddWithValue("@industry", c.IndustryType ?? "");
            cmd.Parameters.AddWithValue("@value",    c.TotalAnnualValue);
            cmd.Parameters.AddWithValue("@contact1", c.PrimaryContact ?? "");
            cmd.Parameters.AddWithValue("@contact2", c.SecondaryContact ?? "");
            cmd.Parameters.AddWithValue("@phone",    c.Phone ?? "");
            cmd.Parameters.AddWithValue("@since",    c.CustomerSince == default ? DateTime.Now : c.CustomerSince);
            cmd.Parameters.AddWithValue("@email",    c.Email ?? "");
            cmd.Parameters.AddWithValue("@gst",      c.GSTNumber ?? "");
            cmd.Parameters.AddWithValue("@pan",      c.PANNumber ?? "");
            cmd.Parameters.AddWithValue("@terms",    c.PaymentTermsDays == 0 ? 30 : c.PaymentTermsDays);
            cmd.Parameters.AddWithValue("@credit",   c.CreditLimit);
            cmd.Parameters.AddWithValue("@address",  c.BillingAddress ?? "");
            cmd.Parameters.AddWithValue("@city",     c.City ?? "");
            cmd.Parameters.AddWithValue("@geoLat", c.GeoLatitude.HasValue ? (object)c.GeoLatitude.Value : DBNull.Value);
            cmd.Parameters.AddWithValue("@geoLon", c.GeoLongitude.HasValue ? (object)c.GeoLongitude.Value : DBNull.Value);
            cmd.Parameters.AddWithValue("@geoAddress", (object)(c.GeocodeAddress ?? string.Empty));
            cmd.Parameters.AddWithValue("@geoStatus", (object)(c.GeocodeStatus ?? string.Empty));
            cmd.Parameters.AddWithValue("@geoUpdatedOn", c.GeocodeUpdatedOn.HasValue ? (object)c.GeocodeUpdatedOn.Value : DBNull.Value);
            cmd.Parameters.AddWithValue("@relationshipStage", c.RelationshipStage ?? "");
            cmd.Parameters.AddWithValue("@tags", c.Tags ?? "");
            cmd.Parameters.AddWithValue("@healthScore", c.HealthScore);
            cmd.Parameters.AddWithValue("@notes", c.Notes ?? "");
            cmd.Parameters.AddWithValue("@assignedTo", c.AssignedTo ?? "");
            cmd.Parameters.AddWithValue("@leadSource", c.LeadSource ?? "");
        }

        private static void AddTeamParams(SqlCommand cmd, ClientTeamMember member)
        {
            cmd.Parameters.AddWithValue("@clientId", member.ClientId);
            cmd.Parameters.AddWithValue("@employeeName", member.EmployeeName ?? string.Empty);
            cmd.Parameters.AddWithValue("@position", member.Position ?? string.Empty);
            cmd.Parameters.AddWithValue("@emailId", member.EmailId ?? string.Empty);
            cmd.Parameters.AddWithValue("@contactNo", member.ContactNo ?? string.Empty);
            cmd.Parameters.AddWithValue("@isPrimary", member.IsPrimary);
            cmd.Parameters.AddWithValue("@isActive", member.IsActive);
        }

        private List<ClientContact> GetContacts(SqlConnection conn, int clientId)
        {
            var contacts = new List<ClientContact>();
            using (var cmd = new SqlCommand(@"
                SELECT ContactID, ClientID, ContactName, Role, Phone, Email, IsPrimary, Notes, CreatedDate
                FROM ClientContacts
                WHERE ClientID = @clientId
                ORDER BY IsPrimary DESC, ContactName ASC;", conn))
            {
                cmd.Parameters.AddWithValue("@clientId", clientId);
                using (var r = cmd.ExecuteReader())
                {
                    while (r.Read())
                    {
                        contacts.Add(new ClientContact
                        {
                            ContactID = (int)r["ContactID"],
                            ClientID = (int)r["ClientID"],
                            ContactName = r["ContactName"].ToString(),
                            Role = r["Role"] == DBNull.Value ? string.Empty : r["Role"].ToString(),
                            Phone = r["Phone"] == DBNull.Value ? string.Empty : r["Phone"].ToString(),
                            Email = r["Email"] == DBNull.Value ? string.Empty : r["Email"].ToString(),
                            IsPrimary = r["IsPrimary"] != DBNull.Value && Convert.ToBoolean(r["IsPrimary"]),
                            Notes = r["Notes"] == DBNull.Value ? string.Empty : r["Notes"].ToString(),
                            CreatedDate = r["CreatedDate"] == DBNull.Value ? DateTime.Now : (DateTime)r["CreatedDate"]
                        });
                    }
                }
            }

            return contacts;
        }

        private B2BClient Map(SqlDataReader r)
        {
            B2BClient c = new B2BClient
            {
                ClientID        = (int)r["ClientID"],
                CompanyName     = r["CompanyName"].ToString(),
                IndustryType    = r["IndustryType"].ToString(),
                TotalAnnualValue= r["TotalAnnualValue"] != DBNull.Value ? (decimal)r["TotalAnnualValue"] : 0,
                PrimaryContact  = r["PrimaryContact"].ToString(),
                SecondaryContact= r["SecondaryContact"].ToString(),
                Phone           = r["Phone"].ToString(),
                CustomerSince   = r["CustomerSince"] != DBNull.Value ? (DateTime)r["CustomerSince"] : default(DateTime)
            };

            // New columns (may not exist yet on first run)
            try { c.Email           = r["Email"].ToString(); }           catch { }
            try { c.GSTNumber       = r["GSTNumber"].ToString(); }       catch { }
            try { c.PANNumber       = r["PANNumber"].ToString(); }       catch { }
            try { c.PaymentTermsDays= r["PaymentTermsDays"] != DBNull.Value ? (int)r["PaymentTermsDays"] : 30; } catch { }
            try { c.CreditLimit     = r["CreditLimit"] != DBNull.Value ? (decimal)r["CreditLimit"] : 0; }        catch { }
            try { c.BillingAddress  = r["BillingAddress"].ToString(); }  catch { }
            try { c.City            = r["City"].ToString(); }            catch { }
            try { c.GeoLatitude     = r["GeoLatitude"] == DBNull.Value ? (double?)null : Convert.ToDouble(r["GeoLatitude"]); } catch { }
            try { c.GeoLongitude    = r["GeoLongitude"] == DBNull.Value ? (double?)null : Convert.ToDouble(r["GeoLongitude"]); } catch { }
            try { c.GeocodeAddress  = r["GeocodeAddress"].ToString(); } catch { }
            try { c.GeocodeStatus   = r["GeocodeStatus"].ToString(); } catch { }
            try { c.GeocodeUpdatedOn = r["GeocodeUpdatedOn"] == DBNull.Value ? (DateTime?)null : (DateTime)r["GeocodeUpdatedOn"]; } catch { }
            try { c.IsActive        = r["IsActive"] != DBNull.Value && (bool)r["IsActive"]; } catch { c.IsActive = true; }
            try { c.RelationshipStage = r["RelationshipStage"].ToString(); } catch { }
            try { c.Tags = r["Tags"].ToString(); } catch { }
            try { c.HealthScore = r["HealthScore"] != DBNull.Value ? Convert.ToInt32(r["HealthScore"]) : 0; } catch { }
            try { c.Notes = r["Notes"].ToString(); } catch { }
            try { c.AssignedTo = r["AssignedTo"].ToString(); } catch { }
            try { c.LeadSource = r["LeadSource"].ToString(); } catch { }

            return c;
        }

        private static ClientTeamMember MapTeamMember(SqlDataReader r)
        {
            return new ClientTeamMember
            {
                Id = (int)r["Id"],
                ClientId = (int)r["ClientId"],
                EmployeeName = r["EmployeeName"] == DBNull.Value ? string.Empty : r["EmployeeName"].ToString(),
                Position = r["Position"] == DBNull.Value ? string.Empty : r["Position"].ToString(),
                EmailId = r["EmailId"] == DBNull.Value ? string.Empty : r["EmailId"].ToString(),
                ContactNo = r["ContactNo"] == DBNull.Value ? string.Empty : r["ContactNo"].ToString(),
                IsPrimary = r["IsPrimary"] != DBNull.Value && Convert.ToBoolean(r["IsPrimary"]),
                IsActive = r["IsActive"] == DBNull.Value || Convert.ToBoolean(r["IsActive"])
            };
        }

        private static ClientActivity MapActivity(SqlDataReader r)
        {
            return new ClientActivity
            {
                Id = (int)r["Id"],
                ClientId = (int)r["ClientId"],
                ActivityType = r["ActivityType"] == DBNull.Value ? string.Empty : r["ActivityType"].ToString(),
                Title = r["Title"] == DBNull.Value ? string.Empty : r["Title"].ToString(),
                Detail = r["Detail"] == DBNull.Value ? string.Empty : r["Detail"].ToString(),
                CreatedAt = r["CreatedAt"] == DBNull.Value ? DateTime.Now : (DateTime)r["CreatedAt"],
                CreatedBy = r["CreatedBy"] == DBNull.Value ? string.Empty : r["CreatedBy"].ToString()
            };
        }

        private static string NormalizeActivityFilter(string filterType)
        {
            string value = (filterType ?? "All").Trim();
            if (value.EndsWith("s", StringComparison.OrdinalIgnoreCase))
                value = value.Substring(0, value.Length - 1);
            switch (value.ToUpperInvariant())
            {
                case "CALL": return "Call";
                case "EMAIL": return "Email";
                case "JOB": return "Job";
                case "NOTE": return "Note";
                default: return "All";
            }
        }
    }
}
