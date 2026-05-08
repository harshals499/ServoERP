using System;
using System.Collections.Generic;
using System.Data.SqlClient;
using HVAC_Pro_Desktop.Models;

namespace HVAC_Pro_Desktop.DAL
{
    public class ServiceDeskRepository
    {
        private readonly DatabaseManager _db = new DatabaseManager();

        public List<ServiceDeskIncident> GetAll()
        {
            var list = new List<ServiceDeskIncident>();
            using (SqlConnection conn = _db.GetConnection())
            {
                conn.Open();
                using (SqlCommand cmd = new SqlCommand(@"
                    SELECT i.*, c.CompanyName AS ClientName, s.SiteName, e.Name AS AssignedEmployeeName
                    FROM ServiceDeskIncidents i
                    LEFT JOIN B2BClients c ON c.ClientID = i.ClientId
                    LEFT JOIN ClientSites s ON s.SiteID = i.SiteId
                    LEFT JOIN Employees e ON e.EmployeeID = i.AssignedEmployeeId
                    ORDER BY CASE WHEN i.Status IN ('Resolved','Closed') THEN 1 ELSE 0 END,
                             i.SlaDueAt ASC, i.OpenedAt DESC;", conn))
                using (SqlDataReader r = cmd.ExecuteReader())
                    while (r.Read())
                        list.Add(MapIncident(r));
            }
            return list;
        }

        public ServiceDeskIncident GetById(int id)
        {
            using (SqlConnection conn = _db.GetConnection())
            {
                conn.Open();
                using (SqlCommand cmd = new SqlCommand(@"
                    SELECT i.*, c.CompanyName AS ClientName, s.SiteName, e.Name AS AssignedEmployeeName
                    FROM ServiceDeskIncidents i
                    LEFT JOIN B2BClients c ON c.ClientID = i.ClientId
                    LEFT JOIN ClientSites s ON s.SiteID = i.SiteId
                    LEFT JOIN Employees e ON e.EmployeeID = i.AssignedEmployeeId
                    WHERE i.IncidentId = @id;", conn))
                {
                    cmd.Parameters.AddWithValue("@id", id);
                    using (SqlDataReader r = cmd.ExecuteReader())
                        return r.Read() ? MapIncident(r) : null;
                }
            }
        }

        public int Create(ServiceDeskIncident incident)
        {
            using (SqlConnection conn = _db.GetConnection())
            {
                conn.Open();
                using (SqlCommand cmd = new SqlCommand(@"
                    INSERT INTO ServiceDeskIncidents
                        (IncidentNumber, ClientId, SiteId, AssignedEmployeeId, CallerName, CallerPhone,
                         Category, EquipmentType, AssetSerialNumber, Priority, Status, ShortDescription,
                         Description, RootCause, ResolutionCode, OpenedAt, AssignedAt, ResolvedAt, ClosedAt,
                         SlaDueAt, SlaBreached, LinkedJobId, CreatedByName, ModifiedByName, ModifiedDate)
                    VALUES
                        (@number, @clientId, @siteId, @employeeId, @callerName, @callerPhone,
                         @category, @equipment, @serial, @priority, @status, @shortDescription,
                         @description, @rootCause, @resolutionCode, @openedAt, @assignedAt, @resolvedAt, @closedAt,
                         @slaDueAt, @slaBreached, @linkedJobId, @createdBy, @modifiedBy, @modifiedDate);
                    SELECT SCOPE_IDENTITY();", conn))
                {
                    AddParams(cmd, incident);
                    return Convert.ToInt32(cmd.ExecuteScalar());
                }
            }
        }

        public void Update(ServiceDeskIncident incident)
        {
            using (SqlConnection conn = _db.GetConnection())
            {
                conn.Open();
                using (SqlCommand cmd = new SqlCommand(@"
                    UPDATE ServiceDeskIncidents SET
                        IncidentNumber = @number,
                        ClientId = @clientId,
                        SiteId = @siteId,
                        AssignedEmployeeId = @employeeId,
                        CallerName = @callerName,
                        CallerPhone = @callerPhone,
                        Category = @category,
                        EquipmentType = @equipment,
                        AssetSerialNumber = @serial,
                        Priority = @priority,
                        Status = @status,
                        ShortDescription = @shortDescription,
                        Description = @description,
                        RootCause = @rootCause,
                        ResolutionCode = @resolutionCode,
                        OpenedAt = @openedAt,
                        AssignedAt = @assignedAt,
                        ResolvedAt = @resolvedAt,
                        ClosedAt = @closedAt,
                        SlaDueAt = @slaDueAt,
                        SlaBreached = @slaBreached,
                        LinkedJobId = @linkedJobId,
                        ModifiedByName = @modifiedBy,
                        ModifiedDate = @modifiedDate
                    WHERE IncidentId = @id;", conn))
                {
                    AddParams(cmd, incident);
                    cmd.Parameters.AddWithValue("@id", incident.IncidentId);
                    cmd.ExecuteNonQuery();
                }
            }
        }

        public void LinkJob(int incidentId, int jobId)
        {
            using (SqlConnection conn = _db.GetConnection())
            {
                conn.Open();
                using (SqlCommand cmd = new SqlCommand("UPDATE ServiceDeskIncidents SET LinkedJobId = @jobId, ModifiedDate = GETDATE() WHERE IncidentId = @id", conn))
                {
                    cmd.Parameters.AddWithValue("@id", incidentId);
                    cmd.Parameters.AddWithValue("@jobId", jobId);
                    cmd.ExecuteNonQuery();
                }
            }
        }

        public List<ServiceDeskNote> GetNotes(int incidentId)
        {
            var list = new List<ServiceDeskNote>();
            using (SqlConnection conn = _db.GetConnection())
            {
                conn.Open();
                using (SqlCommand cmd = new SqlCommand(@"
                    SELECT NoteId, IncidentId, NoteType, NoteText, CreatedByName, CreatedAt
                    FROM ServiceDeskNotes
                    WHERE IncidentId = @id
                    ORDER BY CreatedAt DESC;", conn))
                {
                    cmd.Parameters.AddWithValue("@id", incidentId);
                    using (SqlDataReader r = cmd.ExecuteReader())
                        while (r.Read())
                            list.Add(new ServiceDeskNote
                            {
                                NoteId = ToInt(r["NoteId"]),
                                IncidentId = ToInt(r["IncidentId"]),
                                NoteType = ToString(r["NoteType"]),
                                NoteText = ToString(r["NoteText"]),
                                CreatedByName = ToString(r["CreatedByName"]),
                                CreatedAt = ToDate(r["CreatedAt"]) ?? DateTime.Now
                            });
                }
            }
            return list;
        }

        public int AddNote(ServiceDeskNote note)
        {
            using (SqlConnection conn = _db.GetConnection())
            {
                conn.Open();
                using (SqlCommand cmd = new SqlCommand(@"
                    INSERT INTO ServiceDeskNotes (IncidentId, NoteType, NoteText, CreatedByName, CreatedAt)
                    VALUES (@incidentId, @type, @text, @by, @createdAt);
                    SELECT SCOPE_IDENTITY();", conn))
                {
                    cmd.Parameters.AddWithValue("@incidentId", note.IncidentId);
                    cmd.Parameters.AddWithValue("@type", (object)note.NoteType ?? DBNull.Value);
                    cmd.Parameters.AddWithValue("@text", (object)note.NoteText ?? DBNull.Value);
                    cmd.Parameters.AddWithValue("@by", (object)note.CreatedByName ?? DBNull.Value);
                    cmd.Parameters.AddWithValue("@createdAt", note.CreatedAt == default(DateTime) ? DateTime.Now : note.CreatedAt);
                    return Convert.ToInt32(cmd.ExecuteScalar());
                }
            }
        }

        public string GenerateIncidentNumber()
        {
            using (SqlConnection conn = _db.GetConnection())
            {
                conn.Open();
                using (SqlCommand cmd = new SqlCommand("SELECT ISNULL(MAX(IncidentId), 0) + 1 FROM ServiceDeskIncidents", conn))
                {
                    int next = Convert.ToInt32(cmd.ExecuteScalar());
                    return "INC" + next.ToString("000000");
                }
            }
        }

        private static void AddParams(SqlCommand cmd, ServiceDeskIncident i)
        {
            cmd.Parameters.AddWithValue("@number", (object)i.IncidentNumber ?? DBNull.Value);
            cmd.Parameters.AddWithValue("@clientId", (object)i.ClientId ?? DBNull.Value);
            cmd.Parameters.AddWithValue("@siteId", (object)i.SiteId ?? DBNull.Value);
            cmd.Parameters.AddWithValue("@employeeId", (object)i.AssignedEmployeeId ?? DBNull.Value);
            cmd.Parameters.AddWithValue("@callerName", (object)i.CallerName ?? DBNull.Value);
            cmd.Parameters.AddWithValue("@callerPhone", (object)i.CallerPhone ?? DBNull.Value);
            cmd.Parameters.AddWithValue("@category", (object)i.Category ?? DBNull.Value);
            cmd.Parameters.AddWithValue("@equipment", (object)i.EquipmentType ?? DBNull.Value);
            cmd.Parameters.AddWithValue("@serial", (object)i.AssetSerialNumber ?? DBNull.Value);
            cmd.Parameters.AddWithValue("@priority", (object)i.Priority ?? DBNull.Value);
            cmd.Parameters.AddWithValue("@status", (object)i.Status ?? DBNull.Value);
            cmd.Parameters.AddWithValue("@shortDescription", (object)i.ShortDescription ?? DBNull.Value);
            cmd.Parameters.AddWithValue("@description", (object)i.Description ?? DBNull.Value);
            cmd.Parameters.AddWithValue("@rootCause", (object)i.RootCause ?? DBNull.Value);
            cmd.Parameters.AddWithValue("@resolutionCode", (object)i.ResolutionCode ?? DBNull.Value);
            cmd.Parameters.AddWithValue("@openedAt", i.OpenedAt == default(DateTime) ? DateTime.Now : i.OpenedAt);
            cmd.Parameters.AddWithValue("@assignedAt", (object)i.AssignedAt ?? DBNull.Value);
            cmd.Parameters.AddWithValue("@resolvedAt", (object)i.ResolvedAt ?? DBNull.Value);
            cmd.Parameters.AddWithValue("@closedAt", (object)i.ClosedAt ?? DBNull.Value);
            cmd.Parameters.AddWithValue("@slaDueAt", i.SlaDueAt == default(DateTime) ? DateTime.Now.AddHours(4) : i.SlaDueAt);
            cmd.Parameters.AddWithValue("@slaBreached", i.SlaBreached);
            cmd.Parameters.AddWithValue("@linkedJobId", (object)i.LinkedJobId ?? DBNull.Value);
            cmd.Parameters.AddWithValue("@createdBy", (object)i.CreatedByName ?? DBNull.Value);
            cmd.Parameters.AddWithValue("@modifiedBy", (object)i.ModifiedByName ?? DBNull.Value);
            cmd.Parameters.AddWithValue("@modifiedDate", (object)i.ModifiedDate ?? DBNull.Value);
        }

        private static ServiceDeskIncident MapIncident(SqlDataReader r)
        {
            return new ServiceDeskIncident
            {
                IncidentId = ToInt(r["IncidentId"]),
                IncidentNumber = ToString(r["IncidentNumber"]),
                ClientId = ToNullableInt(r["ClientId"]),
                SiteId = ToNullableInt(r["SiteId"]),
                AssignedEmployeeId = ToNullableInt(r["AssignedEmployeeId"]),
                LinkedJobId = ToNullableInt(r["LinkedJobId"]),
                ClientName = HasColumn(r, "ClientName") ? ToString(r["ClientName"]) : null,
                SiteName = HasColumn(r, "SiteName") ? ToString(r["SiteName"]) : null,
                AssignedEmployeeName = HasColumn(r, "AssignedEmployeeName") ? ToString(r["AssignedEmployeeName"]) : null,
                CallerName = ToString(r["CallerName"]),
                CallerPhone = ToString(r["CallerPhone"]),
                Category = ToString(r["Category"]),
                EquipmentType = ToString(r["EquipmentType"]),
                AssetSerialNumber = ToString(r["AssetSerialNumber"]),
                Priority = ToString(r["Priority"]),
                Status = ToString(r["Status"]),
                ShortDescription = ToString(r["ShortDescription"]),
                Description = ToString(r["Description"]),
                RootCause = ToString(r["RootCause"]),
                ResolutionCode = ToString(r["ResolutionCode"]),
                OpenedAt = ToDate(r["OpenedAt"]) ?? DateTime.Now,
                AssignedAt = ToDate(r["AssignedAt"]),
                ResolvedAt = ToDate(r["ResolvedAt"]),
                ClosedAt = ToDate(r["ClosedAt"]),
                SlaDueAt = ToDate(r["SlaDueAt"]) ?? DateTime.Now,
                SlaBreached = ToBool(r["SlaBreached"]),
                CreatedByName = ToString(r["CreatedByName"]),
                ModifiedByName = ToString(r["ModifiedByName"]),
                ModifiedDate = ToDate(r["ModifiedDate"])
            };
        }

        private static bool HasColumn(SqlDataReader r, string name)
        {
            for (int i = 0; i < r.FieldCount; i++)
                if (string.Equals(r.GetName(i), name, StringComparison.OrdinalIgnoreCase))
                    return true;
            return false;
        }

        private static int ToInt(object value) => value == DBNull.Value ? 0 : Convert.ToInt32(value);
        private static int? ToNullableInt(object value) => value == DBNull.Value ? (int?)null : Convert.ToInt32(value);
        private static string ToString(object value) => value == DBNull.Value ? string.Empty : Convert.ToString(value);
        private static bool ToBool(object value) => value != DBNull.Value && Convert.ToBoolean(value);
        private static DateTime? ToDate(object value) => value == DBNull.Value ? (DateTime?)null : Convert.ToDateTime(value);
    }
}
