IF COL_LENGTH('Employees', 'Photo') IS NULL
    ALTER TABLE Employees ADD Photo VARBINARY(MAX) NULL;
IF COL_LENGTH('Employees', 'AadhaarNumber') IS NULL
    ALTER TABLE Employees ADD AadhaarNumber NVARCHAR(12) NULL;
IF COL_LENGTH('Employees', 'PANNumber') IS NULL
    ALTER TABLE Employees ADD PANNumber NVARCHAR(10) NULL;
IF COL_LENGTH('Employees', 'BloodGroup') IS NULL
    ALTER TABLE Employees ADD BloodGroup NVARCHAR(5) NULL;
IF COL_LENGTH('Employees', 'EmergencyContactName') IS NULL
    ALTER TABLE Employees ADD EmergencyContactName NVARCHAR(100) NULL;
IF COL_LENGTH('Employees', 'EmergencyContactPhone') IS NULL
    ALTER TABLE Employees ADD EmergencyContactPhone NVARCHAR(15) NULL;
IF COL_LENGTH('Employees', 'ProbationEndDate') IS NULL
    ALTER TABLE Employees ADD ProbationEndDate DATE NULL;
IF COL_LENGTH('Employees', 'ConfirmationDate') IS NULL
    ALTER TABLE Employees ADD ConfirmationDate DATE NULL;
IF COL_LENGTH('Employees', 'LastWorkingDay') IS NULL
    ALTER TABLE Employees ADD LastWorkingDay DATE NULL;
IF COL_LENGTH('Employees', 'IsRehire') IS NULL
    ALTER TABLE Employees ADD IsRehire BIT NOT NULL CONSTRAINT DF_Employees_IsRehire_EmployeeModule DEFAULT 0;
IF COL_LENGTH('Employees', 'WhatsAppNumber') IS NULL
    ALTER TABLE Employees ADD WhatsAppNumber NVARCHAR(15) NULL;

UPDATE Employees
SET AadhaarNumber = CASE
                        WHEN NULLIF(AadhaarNumber, '') IS NOT NULL THEN AadhaarNumber
                        WHEN NULLIF(AadhaarLast4, '') IS NOT NULL THEN AadhaarLast4
                        ELSE AadhaarNumber
                    END,
    PANNumber = ISNULL(NULLIF(PANNumber, ''), NULLIF(PAN, ''))
WHERE 1 = 1;

IF NOT EXISTS (SELECT * FROM sys.tables WHERE name = 'EmployeeSkills')
BEGIN
    CREATE TABLE EmployeeSkills (
        SkillID INT IDENTITY(1,1) PRIMARY KEY,
        EmployeeID INT NOT NULL FOREIGN KEY REFERENCES Employees(EmployeeID),
        SkillName NVARCHAR(100) NULL,
        CertificationNumber NVARCHAR(50) NULL,
        ExpiryDate DATE NULL,
        IsExpired AS (CASE WHEN ExpiryDate IS NOT NULL AND ExpiryDate < CONVERT(date, GETDATE()) THEN 1 ELSE 0 END)
    );
END;

IF NOT EXISTS (SELECT * FROM sys.tables WHERE name = 'EmployeeAttendance')
BEGIN
    CREATE TABLE EmployeeAttendance (
        AttendanceID INT IDENTITY(1,1) PRIMARY KEY,
        EmployeeID INT NOT NULL FOREIGN KEY REFERENCES Employees(EmployeeID),
        AttendanceDate DATE NULL,
        CheckInTime TIME NULL,
        CheckOutTime TIME NULL,
        CheckInLatitude DECIMAL(9,6) NULL,
        CheckInLongitude DECIMAL(9,6) NULL,
        Status NVARCHAR(20) NULL
    );
END;

IF NOT EXISTS (SELECT * FROM sys.tables WHERE name = 'EmployeeDocuments')
BEGIN
    CREATE TABLE EmployeeDocuments (
        DocumentID INT IDENTITY(1,1) PRIMARY KEY,
        EmployeeID INT NOT NULL FOREIGN KEY REFERENCES Employees(EmployeeID),
        DocumentType NVARCHAR(50) NULL,
        FileName NVARCHAR(200) NULL,
        FileData VARBINARY(MAX) NULL,
        UploadedOn DATETIME NOT NULL DEFAULT GETDATE(),
        ExpiryDate DATE NULL
    );
END;

IF NOT EXISTS (SELECT * FROM sys.tables WHERE name = 'SalaryStructure')
BEGIN
    CREATE TABLE SalaryStructure (
        SalaryID INT IDENTITY(1,1) PRIMARY KEY,
        EmployeeID INT NOT NULL FOREIGN KEY REFERENCES Employees(EmployeeID),
        BasicSalary DECIMAL(10,2) NULL,
        HRA DECIMAL(10,2) NULL,
        Allowances DECIMAL(10,2) NULL,
        PFDeduction DECIMAL(10,2) NULL,
        ESICDeduction DECIMAL(10,2) NULL,
        EffectiveFrom DATE NULL
    );
END;
