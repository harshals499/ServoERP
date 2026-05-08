using System;
using System.Collections.Generic;
using System.Data;
using System.Data.SqlClient;
using HVAC_Pro_Desktop.Models;

namespace HVAC_Pro_Desktop.DAL
{
    public class EmployeeRepository
    {
        private readonly DatabaseManager _db = new DatabaseManager();

        private const string BaseSelectCols = @"
            EmployeeID, EmployeeCode, Name, Designation, Department,
            ClientSite, Phone, JoiningDate, DateOfJoining, DateOfBirth,
            EmploymentType, PAN, AadhaarLast4, UAN, UANNumber, ESICNumber,
            EPFNumber, TaxRegime, StateCode, EPFApplicable, ESIApplicable, PTApplicable,
            BankAccountNumber, BankAccount, BankIFSC, IFSCCode, BankName,
            MaritalStatus, Address, NatureOfWork, BasicSalary, GrossSalary,
            AadhaarNumber, PANNumber, BloodGroup, EmergencyContactName, EmergencyContactPhone,
            ProbationEndDate, ConfirmationDate, LastWorkingDay, IsRehire, WhatsAppNumber,
            Status, CreatedDate, CreatedByUserId, CreatedByName, ModifiedByUserId, ModifiedByName, ModifiedDate";

        private const string FullSelectCols = BaseSelectCols + ", Photo";

        public List<Employee> GetAll()
        {
            var list = new List<Employee>();
            using (SqlConnection conn = _db.GetConnection())
            {
                conn.Open();
                using (SqlCommand cmd = new SqlCommand($"SELECT {BaseSelectCols} FROM Employees ORDER BY EmployeeCode, Name", conn))
                using (SqlDataReader r = cmd.ExecuteReader())
                {
                    while (r.Read())
                        list.Add(MapEmployee(r, false));
                }
            }

            return list;
        }

        public Employee GetById(int id)
        {
            using (SqlConnection conn = _db.GetConnection())
            {
                conn.Open();
                using (SqlCommand cmd = new SqlCommand($"SELECT {FullSelectCols} FROM Employees WHERE EmployeeID = @id", conn))
                {
                    cmd.Parameters.AddWithValue("@id", id);
                    using (SqlDataReader r = cmd.ExecuteReader())
                        return r.Read() ? MapEmployee(r, true) : null;
                }
            }
        }

        public List<Employee> GetByClientSite(string site)
        {
            var list = new List<Employee>();
            using (SqlConnection conn = _db.GetConnection())
            {
                conn.Open();
                using (SqlCommand cmd = new SqlCommand($"SELECT {BaseSelectCols} FROM Employees WHERE ClientSite = @site ORDER BY Name", conn))
                {
                    cmd.Parameters.AddWithValue("@site", site);
                    using (SqlDataReader r = cmd.ExecuteReader())
                    {
                        while (r.Read())
                            list.Add(MapEmployee(r, false));
                    }
                }
            }

            return list;
        }

        public int Create(Employee employee)
        {
            using (SqlConnection conn = _db.GetConnection())
            {
                conn.Open();
                using (SqlCommand cmd = new SqlCommand(@"
                    INSERT INTO Employees
                    (
                        EmployeeCode, Name, Designation, Department, ClientSite, Phone, JoiningDate, DateOfJoining, DateOfBirth,
                        EmploymentType, PAN, AadhaarLast4, UAN, UANNumber, ESICNumber, EPFNumber, TaxRegime, StateCode,
                        EPFApplicable, ESIApplicable, PTApplicable, BankAccountNumber, BankAccount, BankIFSC, IFSCCode, BankName,
                        MaritalStatus, Address, NatureOfWork, BasicSalary, GrossSalary, Photo, AadhaarNumber, PANNumber,
                        BloodGroup, EmergencyContactName, EmergencyContactPhone, ProbationEndDate, ConfirmationDate,
                        LastWorkingDay, IsRehire, WhatsAppNumber, Status, CreatedByUserId, CreatedByName
                    )
                    VALUES
                    (
                        @code, @name, @designation, @department, @site, @phone, @joiningDate, @dateOfJoining, @dateOfBirth,
                        @employmentType, @pan, @aadhaarLast4, @uan, @uanNumber, @esicNumber, @epfNumber, @taxRegime, @stateCode,
                        @epfApplicable, @esiApplicable, @ptApplicable, @bankAccountNumber, @bankAccount, @bankIfsc, @ifscCode, @bankName,
                        @maritalStatus, @address, @natureOfWork, @basicSalary, @grossSalary, @photo, @aadhaarNumber, @panNumber,
                        @bloodGroup, @emergencyContactName, @emergencyContactPhone, @probationEndDate, @confirmationDate,
                        @lastWorkingDay, @isRehire, @whatsAppNumber, @status, @createdByUserId, @createdByName
                    );
                    SELECT CAST(SCOPE_IDENTITY() AS INT);", conn))
                {
                    AddEmployeeParameters(cmd, employee, false);
                    return Convert.ToInt32(cmd.ExecuteScalar());
                }
            }
        }

        public void Update(Employee employee)
        {
            using (SqlConnection conn = _db.GetConnection())
            {
                conn.Open();
                using (SqlCommand cmd = new SqlCommand(@"
                    UPDATE Employees SET
                        EmployeeCode = @code,
                        Name = @name,
                        Designation = @designation,
                        Department = @department,
                        ClientSite = @site,
                        Phone = @phone,
                        JoiningDate = @joiningDate,
                        DateOfJoining = @dateOfJoining,
                        DateOfBirth = @dateOfBirth,
                        EmploymentType = @employmentType,
                        PAN = @pan,
                        AadhaarLast4 = @aadhaarLast4,
                        UAN = @uan,
                        UANNumber = @uanNumber,
                        ESICNumber = @esicNumber,
                        EPFNumber = @epfNumber,
                        TaxRegime = @taxRegime,
                        StateCode = @stateCode,
                        EPFApplicable = @epfApplicable,
                        ESIApplicable = @esiApplicable,
                        PTApplicable = @ptApplicable,
                        BankAccountNumber = @bankAccountNumber,
                        BankAccount = @bankAccount,
                        BankIFSC = @bankIfsc,
                        IFSCCode = @ifscCode,
                        BankName = @bankName,
                        MaritalStatus = @maritalStatus,
                        Address = @address,
                        NatureOfWork = @natureOfWork,
                        BasicSalary = @basicSalary,
                        GrossSalary = @grossSalary,
                        Photo = @photo,
                        AadhaarNumber = @aadhaarNumber,
                        PANNumber = @panNumber,
                        BloodGroup = @bloodGroup,
                        EmergencyContactName = @emergencyContactName,
                        EmergencyContactPhone = @emergencyContactPhone,
                        ProbationEndDate = @probationEndDate,
                        ConfirmationDate = @confirmationDate,
                        LastWorkingDay = @lastWorkingDay,
                        IsRehire = @isRehire,
                        WhatsAppNumber = @whatsAppNumber,
                        Status = @status,
                        ModifiedByUserId = @modifiedByUserId,
                        ModifiedByName = @modifiedByName,
                        ModifiedDate = @modifiedDate
                    WHERE EmployeeID = @id", conn))
                {
                    cmd.Parameters.AddWithValue("@id", employee.EmployeeID);
                    AddEmployeeParameters(cmd, employee, true);
                    cmd.ExecuteNonQuery();
                }
            }
        }

        public void SoftDelete(int employeeId)
        {
            using (SqlConnection conn = _db.GetConnection())
            {
                conn.Open();
                using (SqlCommand cmd = new SqlCommand(@"
                    UPDATE Employees
                    SET Status = 'Inactive',
                        LastWorkingDay = ISNULL(LastWorkingDay, CAST(GETDATE() AS DATE))
                    WHERE EmployeeID = @id", conn))
                {
                    cmd.Parameters.AddWithValue("@id", employeeId);
                    cmd.ExecuteNonQuery();
                }
            }
        }

        public int GetActiveCount()
        {
            using (SqlConnection conn = _db.GetConnection())
            {
                conn.Open();
                using (SqlCommand cmd = new SqlCommand("SELECT COUNT(*) FROM Employees WHERE Status = 'Active'", conn))
                    return Convert.ToInt32(cmd.ExecuteScalar());
            }
        }

        public string GenerateNextEmployeeCode()
        {
            using (SqlConnection conn = _db.GetConnection())
            {
                conn.Open();
                using (SqlCommand cmd = new SqlCommand(@"
                    SELECT ISNULL(MAX(TRY_CAST(SUBSTRING(EmployeeCode, 4, 10) AS INT)), 0)
                    FROM Employees
                    WHERE EmployeeCode LIKE 'EMP%';", conn))
                {
                    int next = Convert.ToInt32(cmd.ExecuteScalar()) + 1;
                    return "EMP" + next.ToString("000");
                }
            }
        }

        public EmployeeDashboardStats GetDashboardStats()
        {
            using (SqlConnection conn = _db.GetConnection())
            {
                conn.Open();
                using (SqlCommand cmd = new SqlCommand(@"
                    SELECT
                        (SELECT COUNT(*) FROM Employees) AS TotalEmployees,
                        (SELECT COUNT(*) FROM Employees WHERE Status = 'Active') AS ActiveToday,
                        (SELECT COUNT(DISTINCT EmployeeID)
                         FROM EmployeeAttendance
                         WHERE AttendanceDate = CAST(GETDATE() AS DATE)
                           AND Status IN ('Present', 'Late', 'HalfDay')) AS OnDuty,
                        (SELECT COUNT(DISTINCT EmployeeID)
                         FROM EmployeeAttendance
                         WHERE AttendanceDate = CAST(GETDATE() AS DATE)
                           AND Status = 'Leave') AS OnLeave;", conn))
                using (SqlDataReader r = cmd.ExecuteReader())
                {
                    if (!r.Read())
                        return new EmployeeDashboardStats();

                    return new EmployeeDashboardStats
                    {
                        TotalEmployees = ToInt(r["TotalEmployees"]),
                        ActiveToday = ToInt(r["ActiveToday"]),
                        OnDuty = ToInt(r["OnDuty"]),
                        OnLeave = ToInt(r["OnLeave"])
                    };
                }
            }
        }

        public List<EmployeeSummaryDto> GetEmployeeSummaries()
        {
            var list = new List<EmployeeSummaryDto>();
            using (SqlConnection conn = _db.GetConnection())
            {
                conn.Open();
                using (SqlCommand cmd = new SqlCommand(@"
                    SELECT
                        e.EmployeeID,
                        e.EmployeeCode,
                        e.Name,
                        e.Designation,
                        e.Department,
                        e.ClientSite,
                        e.Phone,
                        e.Status,
                        CASE WHEN EXISTS (
                            SELECT 1 FROM EmployeeAttendance a
                            WHERE a.EmployeeID = e.EmployeeID
                              AND a.AttendanceDate = CAST(GETDATE() AS DATE)
                              AND a.Status IN ('Present', 'Late', 'HalfDay')
                        ) THEN 1 ELSE 0 END AS CheckedInToday,
                        CASE WHEN EXISTS (
                            SELECT 1 FROM EmployeeAttendance a
                            WHERE a.EmployeeID = e.EmployeeID
                              AND a.AttendanceDate = CAST(GETDATE() AS DATE)
                              AND a.Status = 'Leave'
                        ) THEN 1 ELSE 0 END AS OnLeaveToday
                    FROM Employees e
                    ORDER BY
                        CASE
                            WHEN e.Status <> 'Active' THEN 2
                            WHEN EXISTS (
                                SELECT 1 FROM EmployeeAttendance a
                                WHERE a.EmployeeID = e.EmployeeID
                                  AND a.AttendanceDate = CAST(GETDATE() AS DATE)
                                  AND a.Status IN ('Present', 'Late', 'HalfDay')
                            ) THEN 0
                            ELSE 1
                        END,
                        e.Name;", conn))
                using (SqlDataReader r = cmd.ExecuteReader())
                {
                    while (r.Read())
                    {
                        list.Add(new EmployeeSummaryDto
                        {
                            EmployeeID = ToInt(r["EmployeeID"]),
                            EmployeeCode = ToString(r["EmployeeCode"]),
                            Name = ToString(r["Name"]),
                            Designation = ToString(r["Designation"]),
                            Department = ToString(r["Department"]),
                            ClientSite = ToString(r["ClientSite"]),
                            Phone = ToString(r["Phone"]),
                            Status = ToString(r["Status"]),
                            CheckedInToday = ToBool(r["CheckedInToday"]),
                            OnLeaveToday = ToBool(r["OnLeaveToday"]),
                            IsInactive = !string.Equals(ToString(r["Status"]), "Active", StringComparison.OrdinalIgnoreCase)
                        });
                    }
                }
            }

            return list;
        }

        public List<EmployeeJobSummaryDto> GetEmployeeJobs(int employeeId)
        {
            var list = new List<EmployeeJobSummaryDto>();
            using (SqlConnection conn = _db.GetConnection())
            {
                conn.Open();
                using (SqlCommand cmd = new SqlCommand(@"
                    SELECT
                        j.JobID,
                        j.JobNumber,
                        ISNULL(cs.SiteName, '') AS Site,
                        ISNULL(NULLIF(j.JobType, ''), 'General') AS JobType,
                        ISNULL(j.ModifiedDate, j.CreatedDate) AS AssignedDate,
                        ISNULL(NULLIF(j.PipelineStatus, ''), j.Status) AS JobStatus,
                        j.ClosedDate,
                        CASE
                            WHEN j.ClosedDate IS NULL THEN DATEDIFF(DAY, ISNULL(j.ModifiedDate, j.CreatedDate), GETDATE())
                            ELSE DATEDIFF(DAY, ISNULL(j.ModifiedDate, j.CreatedDate), j.ClosedDate)
                        END AS ClosureDays
                    FROM Jobs j
                    LEFT JOIN ClientSites cs ON cs.SiteID = j.SiteID
                    WHERE j.AssignedEmployeeID = @employeeId
                    ORDER BY ISNULL(j.ModifiedDate, j.CreatedDate) DESC, j.JobID DESC;", conn))
                {
                    cmd.Parameters.AddWithValue("@employeeId", employeeId);
                    using (SqlDataReader r = cmd.ExecuteReader())
                    {
                        while (r.Read())
                        {
                            list.Add(new EmployeeJobSummaryDto
                            {
                                JobID = ToInt(r["JobID"]),
                                JobNumber = ToString(r["JobNumber"]),
                                Site = ToString(r["Site"]),
                                JobType = ToString(r["JobType"]),
                                AssignedDate = ToDateTime(r["AssignedDate"]) ?? DateTime.Today,
                                Status = ToString(r["JobStatus"]),
                                ClosedDate = ToDateTime(r["ClosedDate"]),
                                ClosureDays = ToInt(r["ClosureDays"])
                            });
                        }
                    }
                }
            }

            return list;
        }

        public List<EmployeeAttendanceDayDto> GetEmployeeAttendance(int employeeId, int year, int month)
        {
            var list = new List<EmployeeAttendanceDayDto>();
            using (SqlConnection conn = _db.GetConnection())
            {
                conn.Open();
                using (SqlCommand cmd = new SqlCommand(@"
                    SELECT
                        AttendanceID,
                        AttendanceDate,
                        CheckInTime,
                        CheckOutTime,
                        CheckInLatitude,
                        CheckInLongitude,
                        Status
                    FROM EmployeeAttendance
                    WHERE EmployeeID = @employeeId
                      AND YEAR(AttendanceDate) = @year
                      AND MONTH(AttendanceDate) = @month
                    ORDER BY AttendanceDate DESC;", conn))
                {
                    cmd.Parameters.AddWithValue("@employeeId", employeeId);
                    cmd.Parameters.AddWithValue("@year", year);
                    cmd.Parameters.AddWithValue("@month", month);
                    using (SqlDataReader r = cmd.ExecuteReader())
                    {
                        while (r.Read())
                        {
                            TimeSpan? checkIn = ToTime(r["CheckInTime"]);
                            TimeSpan? checkOut = ToTime(r["CheckOutTime"]);
                            decimal hoursWorked = 0m;
                            if (checkIn.HasValue && checkOut.HasValue && checkOut.Value > checkIn.Value)
                                hoursWorked = Convert.ToDecimal((checkOut.Value - checkIn.Value).TotalHours);

                            list.Add(new EmployeeAttendanceDayDto
                            {
                                AttendanceID = ToInt(r["AttendanceID"]),
                                AttendanceDate = ToDateTime(r["AttendanceDate"]) ?? DateTime.Today,
                                CheckInTime = checkIn,
                                CheckOutTime = checkOut,
                                HoursWorked = decimal.Round(hoursWorked, 2),
                                Status = ToString(r["Status"]),
                                CheckInLatitude = ToNullableDecimal(r["CheckInLatitude"]),
                                CheckInLongitude = ToNullableDecimal(r["CheckInLongitude"])
                            });
                        }
                    }
                }
            }

            return list;
        }

        public EmployeeAttendanceSummaryDto GetEmployeeAttendanceSummary(int employeeId, int year, int month)
        {
            using (SqlConnection conn = _db.GetConnection())
            {
                conn.Open();
                using (SqlCommand cmd = new SqlCommand(@"
                    SELECT
                        SUM(CASE WHEN Status = 'Present' THEN 1 ELSE 0 END) AS PresentDays,
                        SUM(CASE WHEN Status = 'Absent' THEN 1 ELSE 0 END) AS AbsentDays,
                        SUM(CASE WHEN Status = 'Late' THEN 1 ELSE 0 END) AS LateDays,
                        SUM(CASE WHEN Status = 'Leave' THEN 1 ELSE 0 END) AS LeaveDays
                    FROM EmployeeAttendance
                    WHERE EmployeeID = @employeeId
                      AND YEAR(AttendanceDate) = @year
                      AND MONTH(AttendanceDate) = @month;", conn))
                {
                    cmd.Parameters.AddWithValue("@employeeId", employeeId);
                    cmd.Parameters.AddWithValue("@year", year);
                    cmd.Parameters.AddWithValue("@month", month);
                    using (SqlDataReader r = cmd.ExecuteReader())
                    {
                        if (!r.Read())
                            return new EmployeeAttendanceSummaryDto();

                        return new EmployeeAttendanceSummaryDto
                        {
                            PresentDays = ToInt(r["PresentDays"]),
                            AbsentDays = ToInt(r["AbsentDays"]),
                            LateDays = ToInt(r["LateDays"]),
                            LeaveDays = ToInt(r["LeaveDays"])
                        };
                    }
                }
            }
        }

        public List<EmployeeSkillDto> GetEmployeeSkills(int employeeId)
        {
            var list = new List<EmployeeSkillDto>();
            using (SqlConnection conn = _db.GetConnection())
            {
                conn.Open();
                using (SqlCommand cmd = new SqlCommand(@"
                    SELECT
                        SkillID,
                        EmployeeID,
                        SkillName,
                        CertificationNumber,
                        ExpiryDate,
                        CASE WHEN ExpiryDate IS NOT NULL AND ExpiryDate < CAST(GETDATE() AS DATE) THEN 1 ELSE 0 END AS IsExpired
                    FROM EmployeeSkills
                    WHERE EmployeeID = @employeeId
                    ORDER BY CASE WHEN ExpiryDate IS NULL THEN 1 ELSE 0 END, ExpiryDate, SkillName;", conn))
                {
                    cmd.Parameters.AddWithValue("@employeeId", employeeId);
                    using (SqlDataReader r = cmd.ExecuteReader())
                    {
                        while (r.Read())
                        {
                            DateTime? expiry = ToDateTime(r["ExpiryDate"]);
                            list.Add(new EmployeeSkillDto
                            {
                                SkillID = ToInt(r["SkillID"]),
                                EmployeeID = ToInt(r["EmployeeID"]),
                                SkillName = ToString(r["SkillName"]),
                                CertificationNumber = ToString(r["CertificationNumber"]),
                                ExpiryDate = expiry,
                                IsExpired = ToBool(r["IsExpired"]),
                                ExpiresWithinThirtyDays = expiry.HasValue && expiry.Value.Date >= DateTime.Today && expiry.Value.Date <= DateTime.Today.AddDays(30)
                            });
                        }
                    }
                }
            }

            return list;
        }

        public List<EmployeeSkillDto> GetExpiringSkills(int days)
        {
            var list = new List<EmployeeSkillDto>();
            using (SqlConnection conn = _db.GetConnection())
            {
                conn.Open();
                using (SqlCommand cmd = new SqlCommand(@"
                    SELECT
                        s.SkillID,
                        s.EmployeeID,
                        e.Name AS EmployeeName,
                        s.SkillName,
                        s.CertificationNumber,
                        s.ExpiryDate,
                        CASE WHEN s.ExpiryDate IS NOT NULL AND s.ExpiryDate < CAST(GETDATE() AS DATE) THEN 1 ELSE 0 END AS IsExpired
                    FROM EmployeeSkills s
                    INNER JOIN Employees e ON e.EmployeeID = s.EmployeeID
                    WHERE s.ExpiryDate IS NOT NULL
                      AND s.ExpiryDate >= CAST(GETDATE() AS DATE)
                      AND s.ExpiryDate <= DATEADD(DAY, @days, CAST(GETDATE() AS DATE))
                    ORDER BY s.ExpiryDate, e.Name, s.SkillName;", conn))
                {
                    cmd.Parameters.AddWithValue("@days", days);
                    using (SqlDataReader r = cmd.ExecuteReader())
                    {
                        while (r.Read())
                        {
                            DateTime? expiry = ToDateTime(r["ExpiryDate"]);
                            list.Add(new EmployeeSkillDto
                            {
                                SkillID = ToInt(r["SkillID"]),
                                EmployeeID = ToInt(r["EmployeeID"]),
                                EmployeeName = ToString(r["EmployeeName"]),
                                SkillName = ToString(r["SkillName"]),
                                CertificationNumber = ToString(r["CertificationNumber"]),
                                ExpiryDate = expiry,
                                IsExpired = ToBool(r["IsExpired"]),
                                ExpiresWithinThirtyDays = expiry.HasValue
                            });
                        }
                    }
                }
            }

            return list;
        }

        public int SaveSkill(EmployeeSkillDto skill)
        {
            using (SqlConnection conn = _db.GetConnection())
            {
                conn.Open();
                if (skill.SkillID > 0)
                {
                    using (SqlCommand cmd = new SqlCommand(@"
                        UPDATE EmployeeSkills
                        SET SkillName = @skillName,
                            CertificationNumber = @certificationNumber,
                            ExpiryDate = @expiryDate
                        WHERE SkillID = @skillId;", conn))
                    {
                        AddSkillParameters(cmd, skill);
                        cmd.Parameters.AddWithValue("@skillId", skill.SkillID);
                        cmd.ExecuteNonQuery();
                        return skill.SkillID;
                    }
                }

                using (SqlCommand cmd = new SqlCommand(@"
                    INSERT INTO EmployeeSkills (EmployeeID, SkillName, CertificationNumber, ExpiryDate)
                    VALUES (@employeeId, @skillName, @certificationNumber, @expiryDate);
                    SELECT CAST(SCOPE_IDENTITY() AS INT);", conn))
                {
                    AddSkillParameters(cmd, skill);
                    return Convert.ToInt32(cmd.ExecuteScalar());
                }
            }
        }

        public List<EmployeeDocumentDto> GetEmployeeDocuments(int employeeId)
        {
            var list = new List<EmployeeDocumentDto>();
            using (SqlConnection conn = _db.GetConnection())
            {
                conn.Open();
                using (SqlCommand cmd = new SqlCommand(@"
                    SELECT DocumentID, EmployeeID, DocumentType, FileName, UploadedOn, ExpiryDate
                    FROM EmployeeDocuments
                    WHERE EmployeeID = @employeeId
                    ORDER BY UploadedOn DESC, DocumentID DESC;", conn))
                {
                    cmd.Parameters.AddWithValue("@employeeId", employeeId);
                    using (SqlDataReader r = cmd.ExecuteReader())
                    {
                        while (r.Read())
                        {
                            list.Add(new EmployeeDocumentDto
                            {
                                DocumentID = ToInt(r["DocumentID"]),
                                EmployeeID = ToInt(r["EmployeeID"]),
                                DocumentType = ToString(r["DocumentType"]),
                                FileName = ToString(r["FileName"]),
                                UploadedOn = ToDateTime(r["UploadedOn"]) ?? DateTime.Now,
                                ExpiryDate = ToDateTime(r["ExpiryDate"])
                            });
                        }
                    }
                }
            }

            return list;
        }

        public int SaveDocument(EmployeeDocumentDto document)
        {
            using (SqlConnection conn = _db.GetConnection())
            {
                conn.Open();
                using (SqlCommand cmd = new SqlCommand(@"
                    INSERT INTO EmployeeDocuments (EmployeeID, DocumentType, FileName, FileData, ExpiryDate)
                    VALUES (@employeeId, @documentType, @fileName, @fileData, @expiryDate);
                    SELECT CAST(SCOPE_IDENTITY() AS INT);", conn))
                {
                    cmd.Parameters.AddWithValue("@employeeId", document.EmployeeID);
                    cmd.Parameters.AddWithValue("@documentType", string.IsNullOrWhiteSpace(document.DocumentType) ? (object)DBNull.Value : document.DocumentType);
                    cmd.Parameters.AddWithValue("@fileName", string.IsNullOrWhiteSpace(document.FileName) ? (object)DBNull.Value : document.FileName);
                    SqlParameter fileParam = cmd.Parameters.Add("@fileData", SqlDbType.VarBinary, -1);
                    fileParam.Value = document.FileData ?? (object)DBNull.Value;
                    cmd.Parameters.AddWithValue("@expiryDate", document.ExpiryDate.HasValue ? (object)document.ExpiryDate.Value.Date : DBNull.Value);
                    return Convert.ToInt32(cmd.ExecuteScalar());
                }
            }
        }

        public EmployeeDocumentDto GetDocumentById(int documentId)
        {
            using (SqlConnection conn = _db.GetConnection())
            {
                conn.Open();
                using (SqlCommand cmd = new SqlCommand(@"
                    SELECT DocumentID, EmployeeID, DocumentType, FileName, FileData, UploadedOn, ExpiryDate
                    FROM EmployeeDocuments
                    WHERE DocumentID = @documentId;", conn))
                {
                    cmd.Parameters.AddWithValue("@documentId", documentId);
                    using (SqlDataReader r = cmd.ExecuteReader())
                    {
                        if (!r.Read())
                            return null;

                        return new EmployeeDocumentDto
                        {
                            DocumentID = ToInt(r["DocumentID"]),
                            EmployeeID = ToInt(r["EmployeeID"]),
                            DocumentType = ToString(r["DocumentType"]),
                            FileName = ToString(r["FileName"]),
                            FileData = r["FileData"] == DBNull.Value ? null : (byte[])r["FileData"],
                            UploadedOn = ToDateTime(r["UploadedOn"]) ?? DateTime.Now,
                            ExpiryDate = ToDateTime(r["ExpiryDate"])
                        };
                    }
                }
            }
        }

        public EmployeeSalaryProfileDto GetSalaryProfile(int employeeId)
        {
            using (SqlConnection conn = _db.GetConnection())
            {
                conn.Open();
                using (SqlCommand cmd = new SqlCommand(@"
                    SELECT TOP 1 SalaryID, EmployeeID, BasicSalary, HRA, Allowances, PFDeduction, ESICDeduction, EffectiveFrom
                    FROM SalaryStructure
                    WHERE EmployeeID = @employeeId
                    ORDER BY EffectiveFrom DESC, SalaryID DESC;", conn))
                {
                    cmd.Parameters.AddWithValue("@employeeId", employeeId);
                    using (SqlDataReader r = cmd.ExecuteReader())
                    {
                        if (!r.Read())
                        {
                            return new EmployeeSalaryProfileDto
                            {
                                EmployeeID = employeeId,
                                EffectiveFrom = DateTime.Today
                            };
                        }

                        return MapSalaryProfile(r);
                    }
                }
            }
        }

        public int SaveSalaryProfile(EmployeeSalaryProfileDto profile)
        {
            using (SqlConnection conn = _db.GetConnection())
            {
                conn.Open();
                if (profile.SalaryID > 0)
                {
                    using (SqlCommand cmd = new SqlCommand(@"
                        UPDATE SalaryStructure
                        SET BasicSalary = @basicSalary,
                            HRA = @hra,
                            Allowances = @allowances,
                            PFDeduction = @pfDeduction,
                            ESICDeduction = @esicDeduction,
                            EffectiveFrom = @effectiveFrom
                        WHERE SalaryID = @salaryId;", conn))
                    {
                        AddSalaryProfileParameters(cmd, profile);
                        cmd.Parameters.AddWithValue("@salaryId", profile.SalaryID);
                        cmd.ExecuteNonQuery();
                        return profile.SalaryID;
                    }
                }

                using (SqlCommand cmd = new SqlCommand(@"
                    INSERT INTO SalaryStructure
                    (EmployeeID, BasicSalary, HRA, Allowances, PFDeduction, ESICDeduction, EffectiveFrom)
                    VALUES
                    (@employeeId, @basicSalary, @hra, @allowances, @pfDeduction, @esicDeduction, @effectiveFrom);
                    SELECT CAST(SCOPE_IDENTITY() AS INT);", conn))
                {
                    AddSalaryProfileParameters(cmd, profile);
                    return Convert.ToInt32(cmd.ExecuteScalar());
                }
            }
        }

        private static void AddEmployeeParameters(SqlCommand cmd, Employee employee, bool includeModified)
        {
            string aadhaar = NormalizeDigits(employee.AadhaarNumber);
            string aadhaarLast4 = !string.IsNullOrWhiteSpace(aadhaar) && aadhaar.Length >= 4
                ? aadhaar.Substring(aadhaar.Length - 4)
                : employee.AadhaarLast4;
            string panNumber = NormalizeUpper(employee.PANNumber);
            string panValue = NormalizeUpper(string.IsNullOrWhiteSpace(panNumber) ? employee.PAN : panNumber);

            cmd.Parameters.AddWithValue("@code", employee.EmployeeCode ?? string.Empty);
            cmd.Parameters.AddWithValue("@name", employee.Name ?? string.Empty);
            cmd.Parameters.AddWithValue("@designation", NullableValue(employee.Designation));
            cmd.Parameters.AddWithValue("@department", NullableValue(employee.Department));
            cmd.Parameters.AddWithValue("@site", NullableValue(employee.ClientSite));
            cmd.Parameters.AddWithValue("@phone", NullableValue(employee.Phone));
            cmd.Parameters.AddWithValue("@joiningDate", employee.JoiningDate.HasValue ? (object)employee.JoiningDate.Value.Date : DBNull.Value);
            cmd.Parameters.AddWithValue("@dateOfJoining", employee.DateOfJoining.HasValue ? (object)employee.DateOfJoining.Value.Date : (employee.JoiningDate.HasValue ? (object)employee.JoiningDate.Value.Date : DBNull.Value));
            cmd.Parameters.AddWithValue("@dateOfBirth", employee.DateOfBirth.HasValue ? (object)employee.DateOfBirth.Value.Date : DBNull.Value);
            cmd.Parameters.AddWithValue("@employmentType", NullableValue(employee.EmploymentType));
            cmd.Parameters.AddWithValue("@pan", NullableValue(panValue));
            cmd.Parameters.AddWithValue("@aadhaarLast4", NullableValue(aadhaarLast4));
            cmd.Parameters.AddWithValue("@uan", NullableValue(employee.UAN));
            cmd.Parameters.AddWithValue("@uanNumber", NullableValue(employee.UANNumber));
            cmd.Parameters.AddWithValue("@esicNumber", NullableValue(employee.ESICNumber));
            cmd.Parameters.AddWithValue("@epfNumber", NullableValue(employee.EPFNumber));
            cmd.Parameters.AddWithValue("@taxRegime", NullableValue(employee.TaxRegime));
            cmd.Parameters.AddWithValue("@stateCode", NullableValue(employee.StateCode));
            cmd.Parameters.AddWithValue("@epfApplicable", employee.EPFApplicable);
            cmd.Parameters.AddWithValue("@esiApplicable", employee.ESIApplicable);
            cmd.Parameters.AddWithValue("@ptApplicable", employee.PTApplicable);
            cmd.Parameters.AddWithValue("@bankAccountNumber", NullableValue(employee.BankAccountNumber));
            cmd.Parameters.AddWithValue("@bankAccount", NullableValue(employee.BankAccount));
            cmd.Parameters.AddWithValue("@bankIfsc", NullableValue(employee.BankIFSC));
            cmd.Parameters.AddWithValue("@ifscCode", NullableValue(employee.IFSCCode));
            cmd.Parameters.AddWithValue("@bankName", NullableValue(employee.BankName));
            cmd.Parameters.AddWithValue("@maritalStatus", NullableValue(employee.MaritalStatus));
            cmd.Parameters.AddWithValue("@address", NullableValue(employee.Address));
            cmd.Parameters.AddWithValue("@natureOfWork", NullableValue(employee.NatureOfWork));
            cmd.Parameters.AddWithValue("@basicSalary", employee.BasicSalary);
            cmd.Parameters.AddWithValue("@grossSalary", employee.GrossSalary);
            SqlParameter photoParam = cmd.Parameters.Add("@photo", SqlDbType.VarBinary, -1);
            photoParam.Value = employee.Photo ?? (object)DBNull.Value;
            cmd.Parameters.AddWithValue("@aadhaarNumber", NullableValue(aadhaar));
            cmd.Parameters.AddWithValue("@panNumber", NullableValue(panNumber));
            cmd.Parameters.AddWithValue("@bloodGroup", NullableValue(employee.BloodGroup));
            cmd.Parameters.AddWithValue("@emergencyContactName", NullableValue(employee.EmergencyContactName));
            cmd.Parameters.AddWithValue("@emergencyContactPhone", NullableValue(employee.EmergencyContactPhone));
            cmd.Parameters.AddWithValue("@probationEndDate", employee.ProbationEndDate.HasValue ? (object)employee.ProbationEndDate.Value.Date : DBNull.Value);
            cmd.Parameters.AddWithValue("@confirmationDate", employee.ConfirmationDate.HasValue ? (object)employee.ConfirmationDate.Value.Date : DBNull.Value);
            cmd.Parameters.AddWithValue("@lastWorkingDay", employee.LastWorkingDay.HasValue ? (object)employee.LastWorkingDay.Value.Date : DBNull.Value);
            cmd.Parameters.AddWithValue("@isRehire", employee.IsRehire);
            cmd.Parameters.AddWithValue("@whatsAppNumber", NullableValue(employee.WhatsAppNumber));
            cmd.Parameters.AddWithValue("@status", string.IsNullOrWhiteSpace(employee.Status) ? "Active" : employee.Status);

            if (cmd.CommandText.IndexOf("@createdByUserId", StringComparison.OrdinalIgnoreCase) >= 0)
                cmd.Parameters.AddWithValue("@createdByUserId", employee.CreatedByUserId.HasValue ? (object)employee.CreatedByUserId.Value : DBNull.Value);
            if (cmd.CommandText.IndexOf("@createdByName", StringComparison.OrdinalIgnoreCase) >= 0)
                cmd.Parameters.AddWithValue("@createdByName", NullableValue(employee.CreatedByName));

            if (includeModified)
            {
                cmd.Parameters.AddWithValue("@modifiedByUserId", employee.ModifiedByUserId.HasValue ? (object)employee.ModifiedByUserId.Value : DBNull.Value);
                cmd.Parameters.AddWithValue("@modifiedByName", NullableValue(employee.ModifiedByName));
                cmd.Parameters.AddWithValue("@modifiedDate", employee.ModifiedDate.HasValue ? (object)employee.ModifiedDate.Value : DBNull.Value);
            }
        }

        private static void AddSkillParameters(SqlCommand cmd, EmployeeSkillDto skill)
        {
            cmd.Parameters.AddWithValue("@employeeId", skill.EmployeeID);
            cmd.Parameters.AddWithValue("@skillName", NullableValue(skill.SkillName));
            cmd.Parameters.AddWithValue("@certificationNumber", NullableValue(skill.CertificationNumber));
            cmd.Parameters.AddWithValue("@expiryDate", skill.ExpiryDate.HasValue ? (object)skill.ExpiryDate.Value.Date : DBNull.Value);
        }

        private static void AddSalaryProfileParameters(SqlCommand cmd, EmployeeSalaryProfileDto profile)
        {
            cmd.Parameters.AddWithValue("@employeeId", profile.EmployeeID);
            cmd.Parameters.AddWithValue("@basicSalary", profile.BasicSalary);
            cmd.Parameters.AddWithValue("@hra", profile.HRA);
            cmd.Parameters.AddWithValue("@allowances", profile.Allowances);
            cmd.Parameters.AddWithValue("@pfDeduction", profile.PFDeduction);
            cmd.Parameters.AddWithValue("@esicDeduction", profile.ESICDeduction);
            cmd.Parameters.AddWithValue("@effectiveFrom", profile.EffectiveFrom.Date);
        }

        private static Employee MapEmployee(SqlDataReader r, bool includePhoto)
        {
            return new Employee
            {
                EmployeeID = ToInt(r["EmployeeID"]),
                EmployeeCode = ToString(r["EmployeeCode"]),
                Name = ToString(r["Name"]),
                Designation = ToString(r["Designation"]),
                Department = ToString(r["Department"]),
                ClientSite = ToString(r["ClientSite"]),
                Phone = ToString(r["Phone"]),
                JoiningDate = ToDateTime(r["JoiningDate"]),
                DateOfJoining = ToDateTime(r["DateOfJoining"]),
                DateOfBirth = ToDateTime(r["DateOfBirth"]),
                EmploymentType = ToString(r["EmploymentType"]),
                PAN = ToString(r["PAN"]),
                AadhaarLast4 = ToString(r["AadhaarLast4"]),
                UAN = ToString(r["UAN"]),
                UANNumber = ToString(r["UANNumber"]),
                ESICNumber = ToString(r["ESICNumber"]),
                EPFNumber = ToString(r["EPFNumber"]),
                TaxRegime = ToString(r["TaxRegime"]),
                StateCode = ToString(r["StateCode"]),
                EPFApplicable = ToBool(r["EPFApplicable"]),
                ESIApplicable = ToBool(r["ESIApplicable"]),
                PTApplicable = ToBool(r["PTApplicable"]),
                BankAccountNumber = ToString(r["BankAccountNumber"]),
                BankAccount = ToString(r["BankAccount"]),
                BankIFSC = ToString(r["BankIFSC"]),
                IFSCCode = ToString(r["IFSCCode"]),
                BankName = ToString(r["BankName"]),
                MaritalStatus = ToString(r["MaritalStatus"]),
                Address = ToString(r["Address"]),
                NatureOfWork = ToString(r["NatureOfWork"]),
                BasicSalary = ToDecimal(r["BasicSalary"]),
                GrossSalary = ToDecimal(r["GrossSalary"]),
                Photo = includePhoto && r["Photo"] != DBNull.Value ? (byte[])r["Photo"] : null,
                AadhaarNumber = ToString(r["AadhaarNumber"]),
                PANNumber = ToString(r["PANNumber"]),
                BloodGroup = ToString(r["BloodGroup"]),
                EmergencyContactName = ToString(r["EmergencyContactName"]),
                EmergencyContactPhone = ToString(r["EmergencyContactPhone"]),
                ProbationEndDate = ToDateTime(r["ProbationEndDate"]),
                ConfirmationDate = ToDateTime(r["ConfirmationDate"]),
                LastWorkingDay = ToDateTime(r["LastWorkingDay"]),
                IsRehire = ToBool(r["IsRehire"]),
                WhatsAppNumber = ToString(r["WhatsAppNumber"]),
                Status = ToString(r["Status"]),
                CreatedDate = ToDateTime(r["CreatedDate"]) ?? DateTime.Now,
                CreatedByUserId = r["CreatedByUserId"] == DBNull.Value ? (int?)null : ToInt(r["CreatedByUserId"]),
                CreatedByName = ToString(r["CreatedByName"]),
                ModifiedByUserId = r["ModifiedByUserId"] == DBNull.Value ? (int?)null : ToInt(r["ModifiedByUserId"]),
                ModifiedByName = ToString(r["ModifiedByName"]),
                ModifiedDate = ToDateTime(r["ModifiedDate"])
            };
        }

        private static EmployeeSalaryProfileDto MapSalaryProfile(SqlDataReader r)
        {
            return new EmployeeSalaryProfileDto
            {
                SalaryID = ToInt(r["SalaryID"]),
                EmployeeID = ToInt(r["EmployeeID"]),
                BasicSalary = ToDecimal(r["BasicSalary"]),
                HRA = ToDecimal(r["HRA"]),
                Allowances = ToDecimal(r["Allowances"]),
                PFDeduction = ToDecimal(r["PFDeduction"]),
                ESICDeduction = ToDecimal(r["ESICDeduction"]),
                EffectiveFrom = ToDateTime(r["EffectiveFrom"]) ?? DateTime.Today
            };
        }

        private static object NullableValue(string value)
        {
            return string.IsNullOrWhiteSpace(value) ? (object)DBNull.Value : value.Trim();
        }

        private static string NormalizeDigits(string value)
        {
            if (string.IsNullOrWhiteSpace(value))
                return null;

            char[] chars = value.Trim().ToCharArray();
            char[] buffer = new char[chars.Length];
            int index = 0;
            foreach (char c in chars)
            {
                if (char.IsDigit(c))
                    buffer[index++] = c;
            }

            return index == 0 ? null : new string(buffer, 0, index);
        }

        private static string NormalizeUpper(string value)
        {
            return string.IsNullOrWhiteSpace(value) ? null : value.Trim().ToUpperInvariant();
        }

        private static string ToString(object value) => value == DBNull.Value ? string.Empty : Convert.ToString(value);
        private static int ToInt(object value) => value == DBNull.Value ? 0 : Convert.ToInt32(value);
        private static bool ToBool(object value) => value != DBNull.Value && Convert.ToBoolean(value);
        private static decimal ToDecimal(object value) => value == DBNull.Value ? 0m : Convert.ToDecimal(value);
        private static decimal? ToNullableDecimal(object value) => value == DBNull.Value ? (decimal?)null : Convert.ToDecimal(value);
        private static DateTime? ToDateTime(object value) => value == DBNull.Value ? (DateTime?)null : Convert.ToDateTime(value);
        private static TimeSpan? ToTime(object value) => value == DBNull.Value ? (TimeSpan?)null : (TimeSpan)value;
    }
}
