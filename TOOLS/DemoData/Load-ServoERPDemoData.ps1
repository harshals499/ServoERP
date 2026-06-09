param(
    [string]$ConnectionString = 'Server=localhost\SQLEXPRESS;Database=HVAC_PRO;Integrated Security=True;Connection Timeout=15;',
    [switch]$ResetOnly,
    [string]$ExportFolder = 'C:\HVAC_PRO_MSE\TOOLS\DemoData\exports'
)

$ErrorActionPreference = 'Stop'
Add-Type -AssemblyName System.Data

function New-Connection {
    $conn = New-Object System.Data.SqlClient.SqlConnection $ConnectionString
    $conn.Open()
    return $conn
}

function Invoke-Scalar($Conn, [string]$Sql, [hashtable]$Params = @{}) {
    $cmd = $Conn.CreateCommand()
    $cmd.CommandText = $Sql
    foreach ($key in $Params.Keys) {
        [void]$cmd.Parameters.AddWithValue("@$key", $(if ($null -eq $Params[$key]) { [DBNull]::Value } else { $Params[$key] }))
    }
    return $cmd.ExecuteScalar()
}

function Invoke-Sql($Conn, [string]$Sql, [hashtable]$Params = @{}) {
    $cmd = $Conn.CreateCommand()
    $cmd.CommandText = $Sql
    foreach ($key in $Params.Keys) {
        [void]$cmd.Parameters.AddWithValue("@$key", $(if ($null -eq $Params[$key]) { [DBNull]::Value } else { $Params[$key] }))
    }
    [void]$cmd.ExecuteNonQuery()
}

function Get-Columns($Conn, [string]$Table) {
    $cmd = $Conn.CreateCommand()
    $cmd.CommandText = "SELECT COLUMN_NAME FROM INFORMATION_SCHEMA.COLUMNS WHERE TABLE_NAME=@table"
    [void]$cmd.Parameters.AddWithValue('@table', $Table)
    $reader = $cmd.ExecuteReader()
    $set = New-Object 'System.Collections.Generic.HashSet[string]' ([StringComparer]::OrdinalIgnoreCase)
    while ($reader.Read()) { [void]$set.Add($reader.GetString(0)) }
    $reader.Close()
    return $set
}

function Insert-Row($Conn, [string]$Table, [hashtable]$Values) {
    if (-not $script:ColumnCache) { $script:ColumnCache = @{} }
    if (-not $script:ColumnCache.ContainsKey($Table)) { $script:ColumnCache[$Table] = Get-Columns $Conn $Table }
    $columns = @($Values.Keys | Where-Object { $script:ColumnCache[$Table].Contains($_) })
    if ($columns.Count -eq 0) { throw "No matching columns for $Table" }

    $colSql = ($columns | ForEach-Object { "[$_]" }) -join ', '
    $paramSql = ($columns | ForEach-Object { "@$_" }) -join ', '
    $cmd = $Conn.CreateCommand()
    $cmd.CommandText = "INSERT INTO [$Table] ($colSql) VALUES ($paramSql); SELECT CAST(SCOPE_IDENTITY() AS INT);"
    foreach ($col in $columns) {
        [void]$cmd.Parameters.AddWithValue("@$col", $(if ($null -eq $Values[$col]) { [DBNull]::Value } else { $Values[$col] }))
    }
    return [int]$cmd.ExecuteScalar()
}

function Get-Id($Conn, [string]$Table, [string]$IdColumn, [string]$WhereColumn, [object]$Value) {
    $result = Invoke-Scalar $Conn "SELECT TOP 1 [$IdColumn] FROM [$Table] WHERE [$WhereColumn]=@value ORDER BY [$IdColumn]" @{ value = $Value }
    if ($null -eq $result -or $result -eq [DBNull]::Value) { return $null }
    return [int]$result
}

function Reset-DemoData($Conn) {
    Invoke-Sql $Conn @"
DELETE n FROM ServiceDeskNotes n INNER JOIN ServiceDeskIncidents i ON i.IncidentId=n.IncidentId WHERE i.IncidentNumber LIKE 'SD-INC-%';
DELETE FROM ServiceDeskIncidents WHERE IncidentNumber LIKE 'SD-INC-%';
IF OBJECT_ID('JobActivityLog','U') IS NOT NULL DELETE FROM JobActivityLog WHERE JobId IN (SELECT JobID FROM Jobs WHERE JobNumber LIKE 'SD-JOB-%');
IF OBJECT_ID('JobPartsUsed','U') IS NOT NULL DELETE FROM JobPartsUsed WHERE JobId IN (SELECT JobID FROM Jobs WHERE JobNumber LIKE 'SD-JOB-%');
IF OBJECT_ID('JobChecklistItems','U') IS NOT NULL DELETE FROM JobChecklistItems WHERE JobId IN (SELECT JobID FROM Jobs WHERE JobNumber LIKE 'SD-JOB-%');
DELETE FROM Jobs WHERE JobNumber LIKE 'SD-JOB-%';
DELETE FROM Payments WHERE PaymentNumber LIKE 'SD-PAY-%' OR ReferenceNumber LIKE 'SDUTR%';
DELETE FROM InvoiceLineItems WHERE InvoiceID IN (SELECT InvoiceID FROM Invoices WHERE InvoiceNumber LIKE 'SD-INV-%');
DELETE FROM Invoices WHERE InvoiceNumber LIKE 'SD-INV-%';
IF OBJECT_ID('TenderBidLineItems','U') IS NOT NULL DELETE FROM TenderBidLineItems WHERE TenderBidId IN (SELECT BidID FROM TenderBids WHERE QuotationNumber LIKE 'SD-QTN-%');
DELETE FROM TenderBids WHERE QuotationNumber LIKE 'SD-QTN-%';
DELETE FROM SLALogs WHERE Notes LIKE 'ServoERP demo:%';
IF OBJECT_ID('PurchaseLineItems','U') IS NOT NULL DELETE FROM PurchaseLineItems WHERE POID IN (SELECT POID FROM PurchaseOrders WHERE PONumber LIKE 'SD-PO-%');
IF OBJECT_ID('PendingCharges','U') IS NOT NULL DELETE FROM PendingCharges WHERE SourcePoId IN (SELECT POID FROM PurchaseOrders WHERE PONumber LIKE 'SD-PO-%');
DELETE FROM PurchaseOrders WHERE PONumber LIKE 'SD-PO-%';
IF OBJECT_ID('SupplierItemPrices','U') IS NOT NULL DELETE FROM SupplierItemPrices WHERE Source='ServoERP demo rate card';
IF OBJECT_ID('StockMovements','U') IS NOT NULL DELETE FROM StockMovements WHERE Notes LIKE 'ServoERP demo:%';
DELETE FROM StockItems WHERE ItemName LIKE 'SD-%';
IF OBJECT_ID('VendorAdvancePayments','U') IS NOT NULL DELETE FROM VendorAdvancePayments WHERE Notes LIKE 'ServoERP demo:%';
DELETE FROM Vendors WHERE VendorName LIKE 'SD %';
IF OBJECT_ID('ClientContacts','U') IS NOT NULL DELETE FROM ClientContacts WHERE ClientID IN (SELECT ClientID FROM B2BClients WHERE CompanyName LIKE 'SD %');
IF OBJECT_ID('ClientActivity','U') IS NOT NULL DELETE FROM ClientActivity WHERE ClientId IN (SELECT ClientID FROM B2BClients WHERE CompanyName LIKE 'SD %');
IF OBJECT_ID('ClientTeam','U') IS NOT NULL DELETE FROM ClientTeam WHERE ClientId IN (SELECT ClientID FROM B2BClients WHERE CompanyName LIKE 'SD %');
DELETE FROM AMCContracts WHERE Notes LIKE 'ServoERP demo:%';
DELETE FROM ClientSites WHERE ClientID IN (SELECT ClientID FROM B2BClients WHERE CompanyName LIKE 'SD %');
DELETE FROM B2BClients WHERE CompanyName LIKE 'SD %';
IF OBJECT_ID('StatutoryPayments','U') IS NOT NULL DELETE FROM StatutoryPayments WHERE PayrollRunId IN (SELECT PayrollRunId FROM PayrollRuns WHERE Notes LIKE 'ServoERP demo payroll%');
IF OBJECT_ID('PayrollEntries','U') IS NOT NULL DELETE FROM PayrollEntries WHERE PayrollRunId IN (SELECT PayrollRunId FROM PayrollRuns WHERE Notes LIKE 'ServoERP demo payroll%') OR EmployeeId IN (SELECT EmployeeID FROM Employees WHERE EmployeeCode LIKE 'SDT%');
IF OBJECT_ID('PayrollRuns','U') IS NOT NULL DELETE FROM PayrollRuns WHERE Notes LIKE 'ServoERP demo payroll%';
IF OBJECT_ID('AttendanceRecords','U') IS NOT NULL DELETE FROM AttendanceRecords WHERE EmployeeId IN (SELECT EmployeeID FROM Employees WHERE EmployeeCode LIKE 'SDT%') AND Notes LIKE 'ServoERP demo attendance%';
IF OBJECT_ID('SalaryStructures','U') IS NOT NULL DELETE FROM SalaryStructures WHERE EmployeeId IN (SELECT EmployeeID FROM Employees WHERE EmployeeCode LIKE 'SDT%');
DELETE FROM Employees WHERE EmployeeCode LIKE 'SDT%';
DELETE FROM Technicians WHERE Phone LIKE '98%77' AND Designation LIKE '%Demo%';
"@
}

function Get-Gstin([int]$StateCode, [string]$Pan, [int]$Index) {
    return ('{0:D2}{1}{2}Z{3}' -f $StateCode, $Pan, (($Index % 9) + 1), (($Index % 8) + 1))
}

function New-Date([int]$OffsetDays) {
    return (Get-Date).Date.AddDays($OffsetDays)
}

$industries = @('HVAC', 'Electrical', 'Plumbing', 'Fire Safety', 'Insulation', 'AMC Maintenance', 'Facility Service')
$cities = @(
    @{ City='Mumbai'; State='Maharashtra'; Code=27; Area='Andheri East MIDC' },
    @{ City='Pune'; State='Maharashtra'; Code=27; Area='Hinjewadi Phase II' },
    @{ City='Navi Mumbai'; State='Maharashtra'; Code=27; Area='Mahape MIDC' },
    @{ City='Thane'; State='Maharashtra'; Code=27; Area='Wagle Estate' },
    @{ City='Ahmedabad'; State='Gujarat'; Code=24; Area='Sanand GIDC' },
    @{ City='Surat'; State='Gujarat'; Code=24; Area='Sachin GIDC' },
    @{ City='Bengaluru'; State='Karnataka'; Code=29; Area='Peenya Industrial Area' },
    @{ City='Hyderabad'; State='Telangana'; Code=36; Area='Kukatpally Industrial Estate' },
    @{ City='Chennai'; State='Tamil Nadu'; Code=33; Area='Ambattur Industrial Estate' },
    @{ City='Delhi'; State='Delhi'; Code=7; Area='Okhla Industrial Area' }
)
$clientNames = @(
    'SD Arvind Precision Components Pvt Ltd','SD Sahyadri Foods Processing LLP','SD Metro Mall Facility Services Pvt Ltd',
    'SD Prakash Pharma Labs Pvt Ltd','SD Narmada Textile Processors','SD BluePeak IT Parks Pvt Ltd',
    'SD Shreeji Cold Chain Logistics','SD Zenith Hospital Services','SD Kaveri Hotels and Resorts Pvt Ltd',
    'SD Prime Auto Ancillaries','SD Lotus Packaging Industries','SD Western Warehousing Corporation',
    'SD Sunrise Fire Systems Client','SD Accurate Electrical Works Client','SD GreenLeaf Dairy Products',
    'SD UrbanEdge Co-working Spaces','SD Vardhan Engineering Works','SD Apex School Infrastructure',
    'SD Silverline Supermarket Chain','SD Orion Chemical Stores','SD Trident Facility Management',
    'SD Reliable Plumbing Projects','SD Bharat Insulation Works Client','SD Skyline Builders Maintenance',
    'SD Coastal Seafood Exports'
)
$contacts = @('Amit Shah','Priya Nair','Rohit Kulkarni','Neha Iyer','Sandeep Patil','Meera Menon','Vikram Rao','Anita Desai','Nilesh Jain','Farah Khan','Rakesh Pillai','Kavita Joshi','Harish Mehta','Pooja Bhat','Manoj Reddy')
$serviceTypes = @('PM Visit','Breakdown','Installation','Gas Charging','AMC Visit','Electrical Panel Audit','Plumbing Repair','Fire Safety Inspection','Duct Insulation','Chiller Overhaul')
$statusList = @('Pending','Scheduled','In Progress','Completed','Overdue')
$priorityList = @('Low','Medium','High','Urgent')

$conn = New-Connection
try {
    Reset-DemoData $conn
    if ($ResetOnly) {
        Write-Output 'Demo data reset complete.'
        return
    }

    $employeeIds = @()
    $techNames = @('Ramesh Patil','Suresh Yadav','Imran Shaikh','Vijay Pawar','Mahesh Jadhav','Karthik Rao','Deepak Sharma','Nitin More','Arun Nair','Rafiq Ansari','Prakash Solanki','Ganesh Shinde','Ravi Kumar','Sanjay Chauhan','Ajay Singh')
    for ($i = 0; $i -lt 15; $i++) {
        $code = 'SDT{0:D3}' -f ($i + 1)
        $id = Insert-Row $conn 'Employees' @{
            EmployeeCode=$code; Name=$techNames[$i]; Designation=(@('Senior HVAC Technician','Electrical Technician','Plumbing Technician','Fire Safety Inspector','Insulation Supervisor')[$i % 5]);
            Department=(@('Field Service','Electrical','Plumbing','Fire Safety','Projects')[$i % 5]); ClientSite='Demo field team';
            Phone=('98{0:D8}' -f (44000000 + $i * 1371)); WhatsAppNumber=('98{0:D8}' -f (44000000 + $i * 1371));
            BloodGroup=(@('A+','B+','O+','AB+','B-')[$i % 5]); AadhaarNumber=('42{0:D10}' -f (1000000000 + $i * 23917));
            PANNumber=('DEMO{0:D4}A' -f ($i + 1)); JoiningDate=(New-Date (-900 + ($i * 31))); Status='Active';
            BasicSalary=(26000 + $i * 1850); GrossSalary=(36000 + $i * 2400)
        }
        $employeeIds += $id
        [void](Insert-Row $conn 'Technicians' @{ Name=$techNames[$i]; Phone=('98{0:D6}77' -f (550000 + $i * 173)); HourlyRate=(450 + $i * 20); Designation='Demo Field Technician'; YearsExperience=(3 + ($i % 12)) })
    }

    $payrollMonth = 5
    $payrollYear = 2026
    $payrollRunId = Insert-Row $conn 'PayrollRuns' @{
        PayrollMonth=$payrollMonth; PayrollYear=$payrollYear; RunDate=([datetime]'2026-05-31T18:30:00'); Status='Completed'; ProcessedBy='Administrator';
        TotalGross=0; TotalNetPay=0; TotalEPFEmployee=0; TotalEPFEmployer=0; TotalESIEmployee=0; TotalESIEmployer=0; TotalTDS=0; TotalPT=0;
        Notes='ServoERP demo payroll run for screenshot-ready data.'
    }
    $payTotals = @{ Gross=0; Net=0; EpfEmployee=0; EpfEmployer=0; EsiEmployee=0; EsiEmployer=0; Tds=0; Pt=0 }
    for ($i = 0; $i -lt $employeeIds.Count; $i++) {
        $rn = $i + 1
        $basic = [decimal](25000 + $rn * 1800)
        $da = 0
        $hra = [decimal]($basic * 0.40)
        $special = [decimal](4500 + $rn * 350)
        $conveyance = 1600
        $medical = 1250
        $lta = 0
        $other = [decimal](2200 + $rn * 180)
        $grossBase = $basic + $da + $hra + $special + $conveyance + $medical + $lta + $other
        $overtime = if (@(3,8,13) -contains $rn) { 1800 } else { 0 }
        $bonus = if (@(5,10,15) -contains $rn) { 2500 } else { 0 }
        $gross = $grossBase + $overtime + $bonus
        $epfEmployee = [decimal]([Math]::Min([double]($basic * 0.12), 1800))
        $epfEmployer = $epfEmployee
        $epsEmployer = [decimal]([Math]::Min([double]($basic * 0.0833), 1250))
        $esiEmployee = if ($grossBase -le 21000) { [decimal]($grossBase * 0.0075) } else { 0 }
        $esiEmployer = if ($grossBase -le 21000) { [decimal]($grossBase * 0.0325) } else { 0 }
        $tds = if ($grossBase -gt 65000) { 1500 } elseif ($grossBase -gt 52000) { 900 } else { 0 }
        $pt = if ($grossBase -gt 25000) { 200 } else { 0 }
        $advance = if (@(6,11) -contains $rn) { 2000 } else { 0 }
        $deductions = $epfEmployee + $esiEmployee + $tds + $pt + $advance
        $net = $gross - $deductions
        $daysPresent = if (@(2,7,12) -contains $rn) { 24 } elseif (@(4,9,14) -contains $rn) { 25 } else { 26 }
        $leaveDays = if (@(2,7,12) -contains $rn) { 1 } elseif (@(4,9,14) -contains $rn) { 0.5 } else { 0 }

        [void](Insert-Row $conn 'SalaryStructures' @{
            EmployeeId=$employeeIds[$i]; EffectiveFrom=([datetime]'2026-04-01'); EffectiveTo=$null; BasicSalary=$basic; DA=$da; HRA=$hra;
            SpecialAllowance=$special; ConveyanceAllowance=$conveyance; MedicalAllowance=$medical; LTA=$lta; OtherAllowances=$other; IsActive=1; CreatedDate=(Get-Date)
        })
        [void](Insert-Row $conn 'PayrollEntries' @{
            PayrollRunId=$payrollRunId; EmployeeId=$employeeIds[$i]; EmployeeName=$techNames[$i]; Designation=(@('Senior HVAC Technician','Electrical Technician','Plumbing Technician','Fire Safety Inspector','Insulation Supervisor')[$i % 5]);
            BasicSalary=$basic; DA=$da; HRA=$hra; SpecialAllowance=$special; ConveyanceAllowance=$conveyance; MedicalAllowance=$medical; LTA=$lta; OtherAllowances=$other;
            OvertimePay=$overtime; Bonus=$bonus; GrossSalary=$gross; WorkingDaysInMonth=26; DaysPresent=$daysPresent; DaysAbsent=(26 - $daysPresent - $leaveDays); LeaveDays=$leaveDays; OvertimeHours=$(if ($overtime -gt 0) { 8 } else { 0 });
            EPFEmployee=$epfEmployee; ESIEmployee=$esiEmployee; TDSDeducted=$tds; ProfessionalTax=$pt; LoanDeduction=0; AdvanceDeduction=$advance; OtherDeductions=0; TotalDeductions=$deductions;
            EPFEmployer=$epfEmployer; EPSEmployer=$epsEmployer; ESIEmployer=$esiEmployer; NetSalary=$net; TaxRegime='New Regime';
            UAN=('10000000{0:D3}' -f $rn); ESICNumber=('31{0:D8}' -f (26040000 + $rn)); BankAccount=('508000{0:D6}' -f (260400 + $rn)); BankIFSC='HDFC0001234'; PayslipGenerated=0; PayslipPath=$null
        })
        for ($d = 1; $d -le 31; $d++) {
            $date = [datetime]::new(2026, 5, $d)
            $status = if ($date.DayOfWeek -eq [DayOfWeek]::Sunday) { 'WeekOff' } elseif ($d -eq (6 + ($rn % 5)) -or $d -eq (18 + ($rn % 7))) { 'Leave' } else { 'Present' }
            [void](Insert-Row $conn 'AttendanceRecords' @{ EmployeeId=$employeeIds[$i]; AttendanceDate=$date; Status=$status; OvertimeHours=$(if (@(9,16,23) -contains $d -and @(3,8,13) -contains $rn) { 2 } else { 0 }); Notes='ServoERP demo attendance' })
        }
        $payTotals.Gross += $gross; $payTotals.Net += $net; $payTotals.EpfEmployee += $epfEmployee; $payTotals.EpfEmployer += $epfEmployer; $payTotals.EsiEmployee += $esiEmployee; $payTotals.EsiEmployer += $esiEmployer; $payTotals.Tds += $tds; $payTotals.Pt += $pt
    }
    Invoke-Sql $conn "UPDATE PayrollRuns SET TotalGross=@gross, TotalNetPay=@net, TotalEPFEmployee=@epfEmp, TotalEPFEmployer=@epfEr, TotalESIEmployee=@esiEmp, TotalESIEmployer=@esiEr, TotalTDS=@tds, TotalPT=@pt WHERE PayrollRunId=@id" @{
        gross=$payTotals.Gross; net=$payTotals.Net; epfEmp=$payTotals.EpfEmployee; epfEr=$payTotals.EpfEmployer; esiEmp=$payTotals.EsiEmployee; esiEr=$payTotals.EsiEmployer; tds=$payTotals.Tds; pt=$payTotals.Pt; id=$payrollRunId
    }
    [void](Insert-Row $conn 'StatutoryPayments' @{ PayrollRunId=$payrollRunId; PaymentType='EPF'; Amount=($payTotals.EpfEmployee + $payTotals.EpfEmployer); DueDate=([datetime]'2026-06-15'); PaidDate=$null; ReferenceNumber='PF-MAY26-DEMO'; Status='Pending'; Notes='Demo EPF challan' })
    [void](Insert-Row $conn 'StatutoryPayments' @{ PayrollRunId=$payrollRunId; PaymentType='ESI'; Amount=($payTotals.EsiEmployee + $payTotals.EsiEmployer); DueDate=([datetime]'2026-06-15'); PaidDate=$null; ReferenceNumber='ESI-MAY26-DEMO'; Status='Pending'; Notes='Demo ESI challan' })
    [void](Insert-Row $conn 'StatutoryPayments' @{ PayrollRunId=$payrollRunId; PaymentType='Professional Tax'; Amount=$payTotals.Pt; DueDate=([datetime]'2026-06-30'); PaidDate=$null; ReferenceNumber='PT-MAY26-DEMO'; Status='Pending'; Notes='Demo PT payable' })
    [void](Insert-Row $conn 'StatutoryPayments' @{ PayrollRunId=$payrollRunId; PaymentType='TDS'; Amount=$payTotals.Tds; DueDate=([datetime]'2026-06-07'); PaidDate=$null; ReferenceNumber='TDS-MAY26-DEMO'; Status='Pending'; Notes='Demo TDS payable' })

    $vendorIds = @()
    $vendorNames = @(
        'SD CoolEdge HVAC Spares Pvt Ltd','SD Bharat Electrical Traders','SD AquaLine Plumbing Supplies','SD SafeGuard Fire Systems',
        'SD ThermoWrap Insulation LLP','SD Western Refrigeration Co','SD Metro Tools and Gauges','SD Prime Copper Tubes',
        'SD National Controls Corporation','SD AirFlow Filters India','SD Reliable Pumps and Valves','SD Zenith Safety Equipment',
        'SD Shakti Switchgear House','SD Monsoon Service Consumables','SD Rapid Logistics and Crane','SD EcoChem Coil Cleaners',
        'SD Precision Duct Fabricators','SD Delta Compressor Works','SD City Hardware Mart','SD Galaxy PPE Suppliers'
    )
    for ($i = 0; $i -lt 20; $i++) {
        $city = $cities[$i % $cities.Count]
        $pan = 'SDVND' + ('{0:D4}' -f ($i + 1)) + 'A'
        $vendorIds += Insert-Row $conn 'Vendors' @{
            VendorName=$vendorNames[$i]; GSTNumber=(Get-Gstin $city.Code $pan $i); PANNumber=$pan; DefaultCreditDays=(15 + (($i % 4) * 15));
            Phone=('97{0:D8}' -f (32000000 + $i * 2468)); Email=('sales{0}@demo-vendor.in' -f ($i + 1));
            Address=('{0}, {1}, {2}' -f (12 + $i), $city.Area, $city.City); City=$city.City; Category=(@('HVAC Spares','Electrical','Plumbing','Fire Safety','Insulation')[$i % 5]);
            IsActive=1; Notes='ServoERP demo: professional vendor record for prospect demonstrations.'
        }
    }

    $clientIds = @()
    $siteIds = @()
    for ($i = 0; $i -lt 25; $i++) {
        $city = $cities[$i % $cities.Count]
        $pan = 'SDCLI' + ('{0:D4}' -f ($i + 1)) + 'B'
        $annual = 480000 + ($i * 85000)
        $clientId = Insert-Row $conn 'B2BClients' @{
            CompanyName=$clientNames[$i]; IndustryType=$industries[$i % $industries.Count]; TotalAnnualValue=$annual;
            PrimaryContact=$contacts[$i % $contacts.Count]; SecondaryContact=$contacts[($i + 5) % $contacts.Count];
            Phone=('91{0:D8}' -f (70000000 + $i * 3381)); CustomerSince=(New-Date (-365 + $i * 9));
            Email=('facilities{0}@demo-client.in' -f ($i + 1)); GSTNumber=(Get-Gstin $city.Code $pan $i); PANNumber=$pan;
            PaymentTermsDays=(@(15,30,45,60)[$i % 4]); CreditLimit=($annual / 2); BillingAddress=('{0}, {1}, {2} - 400{3:D3}, {4}' -f $city.Area, $city.City, $city.State, ($i + 11), $city.State);
            City=$city.City; IsActive=1; RelationshipStage=(@('Active','Renewal Due','Expansion','New Prospect')[$i % 4]);
            Tags=($industries[$i % $industries.Count] + ', Demo, SME'); HealthScore=(72 + ($i % 24)); AssignedTo='ServoERP Demo Team'; LeadSource='Website enquiry';
            Notes='ServoERP demo: fake Indian SME/service-business account for screenshots and sales demos.'
        }
        $clientIds += $clientId
        [void](Insert-Row $conn 'ClientContacts' @{ ClientID=$clientId; ContactName=$contacts[$i % $contacts.Count]; Role='Facility Manager'; Phone=('91{0:D8}' -f (71000000 + $i * 779)); Email=('contact{0}@demo-client.in' -f ($i + 1)); IsPrimary=1; Notes='Primary demo contact' })

        $siteCount = if ($i -lt 15) { 2 } else { 1 }
        for ($s = 0; $s -lt $siteCount; $s++) {
            if ($siteIds.Count -ge 40) { break }
            $siteName = if ($s -eq 0) { 'Main Facility' } else { @('Warehouse Block','Production Unit','Corporate Office','Retail Site')[$i % 4] }
            $siteIds += Insert-Row $conn 'ClientSites' @{
                ClientID=$clientId; SiteName=('SD {0} - {1}' -f ($city.City), $siteName); Address=('{0}, Plot {1}, {2}, {3}' -f $siteName, (20 + $i + $s), $city.Area, $city.City);
                City=$city.City; ACSystemCount=(4 + (($i + $s) % 22)); RefrigerationSystemCount=(($i + $s) % 5); CoolingTowerCount=(($i + $s) % 3);
                IsCritical=([int](($i + $s) % 4 -eq 0)); AssignedTechnicianID=$null; TravelRateINR=(650 + (($i + $s) * 25))
            }
        }
    }

    $contractIds = @()
    for ($i = 0; $i -lt 20; $i++) {
        $monthly = 28000 + ($i * 5500)
        $contractIds += Insert-Row $conn 'AMCContracts' @{
            ClientID=$clientIds[$i]; SiteID=$siteIds[$i]; StartDate=(New-Date (-300 + $i * 8)); EndDate=(New-Date (45 + $i * 5));
            MonthlyValue=$monthly; AnnualValue=($monthly * 12); ContractStatus=(@('Active','Active','Renewal Due','Pending Renewal')[$i % 4]);
            SLAResponseTimeHours=(@(2,4,6,8)[$i % 4]); SLAUptimePercent=(98.5 + (($i % 5) / 10)); SLARepairTimeHours=(@(8,12,16,24)[$i % 4]);
            MaintenanceFrequency=(@('Monthly','Quarterly','Bi-monthly')[$i % 3]); ContractType=(@('Comprehensive AMC','Preventive AMC','Facility Support')[$i % 3]);
            Notes='ServoERP demo: AMC coverage for HVAC/electrical/plumbing/fire safety operations.'
        }
    }

    $stockIds = @()
    $items = @(
        @('R410A Refrigerant Cylinder','Refrigerant','Cylinder',4200),@('R32 Refrigerant Cylinder','Refrigerant','Cylinder',3800),@('Copper Pipe 5/8 inch','Copper','Meter',340),
        @('Copper Pipe 3/8 inch','Copper','Meter',220),@('AHU Pre Filter 24x24','Filters','Nos',420),@('Pocket Filter 24x24x12','Filters','Nos',1550),
        @('V Belt B-72','Belts','Nos',480),@('Contactor 32A 3 Pole','Electrical','Nos',880),@('MCB 32A C Curve','Electrical','Nos',330),
        @('Capacitor 60 MFD','Electrical','Nos',720),@('Smoke Detector Addressable','Fire Safety','Nos',1450),@('Fire Alarm MCP','Fire Safety','Nos',1150),
        @('GI Duct Sheet 24G','Ducting','Sqft',135),@('Nitrile Rubber Insulation 19mm','Insulation','Meter',210),@('PVC Drain Pipe 32mm','Plumbing','Meter',95),
        @('Ball Valve 1 inch','Plumbing','Nos',390),@('Pressure Gauge 0-300 PSI','Tools','Nos',1050),@('Compressor Oil 5L','Consumables','Can',1850),
        @('Coil Cleaner Chemical 5L','Consumables','Can',1250),@('Cable 2.5 sqmm 3 Core','Electrical','Meter',85),@('Chilled Water Pump Seal Kit','Pump Spares','Kit',2450),
        @('Butterfly Valve 2 inch','Valves','Nos',3200),@('Thermostat Controller','Controls','Nos',2700),@('Temperature Sensor NTC','Controls','Nos',650),
        @('Flexible Duct Connector','Ducting','Meter',180)
    )
    for ($i = 0; $i -lt 50; $i++) {
        $base = $items[$i % $items.Count]
        $stockIds += Insert-Row $conn 'StockItems' @{
            ItemName=('SD-{0:D3} {1}' -f ($i + 1), $base[0]); Category=$base[1]; CurrentStock=(5 + (($i * 7) % 85));
            Unit=$base[2]; LastPurchaseRate=([decimal]$base[3] + (($i % 5) * 35)); ReorderLevel=(3 + ($i % 12)); HsnSacCode=(@('8415','8544','8424','4009','998719')[$i % 5]);
            Location=(@('Main Store','Service Van 1','Service Van 2','Project Store')[$i % 4]); IsActive=1
        }
    }

    foreach ($vendorId in $vendorIds) {
        for ($i = 0; $i -lt 3; $i++) {
            $stockIndex = (($vendorId + $i) % $stockIds.Count)
            [void](Insert-Row $conn 'SupplierItemPrices' @{ VendorID=$vendorId; ItemName=('Demo mapped item {0}' -f ($stockIndex + 1)); Category='Demo Rate Card'; Unit='Nos'; Rate=(900 + $stockIndex * 45); Source='ServoERP demo rate card'; EffectiveDate=(Get-Date).Date })
        }
    }

    $quotationIds = @()
    for ($i = 0; $i -lt 30; $i++) {
        $clientIndex = $i % $clientIds.Count
        $amount = 45000 + ($i * 12500)
        $quotationIds += Insert-Row $conn 'TenderBids' @{
            QuotationNumber=('SD-QTN-2026-{0:D4}' -f ($i + 1)); ClientID=$clientIds[$clientIndex]; SiteID=$siteIds[$clientIndex]; TenderName=('{0} proposal for {1}' -f $serviceTypes[$i % $serviceTypes.Count], $clientNames[$clientIndex].Replace('SD ',''));
            SystemCount=(1 + ($i % 8)); BidValue=$amount; TotalTaxableValue=$amount; TotalGST=($amount * 0.18); TotalWithGST=($amount * 1.18);
            DueDate=(New-Date (-120 + $i * 8)); SubmittedDate=(New-Date (-135 + $i * 8)); Status=(@('Draft','Pending','Approved','Submitted','Won')[$i % 5]);
            ClientName=$clientNames[$clientIndex]; Notes='ServoERP demo: quotation with GST-ready service pricing.'
        }
    }

    $invoiceIds = @()
    for ($i = 0; $i -lt 25; $i++) {
        $clientIndex = $i % $clientIds.Count
        $sub = 32000 + ($i * 9600)
        $paid = if ($i % 5 -eq 0) { 0 } elseif ($i % 5 -eq 1) { [math]::Round($sub * 1.18 / 2, 2) } else { [math]::Round($sub * 1.18, 2) }
        $total = [math]::Round($sub * 1.18, 2)
        $invoiceId = Insert-Row $conn 'Invoices' @{
            ContractID=$(if ($i -lt $contractIds.Count) { $contractIds[$i] } else { $null }); ClientID=$clientIds[$clientIndex]; SiteID=$siteIds[$clientIndex]; QuotationBidID=$(if ($i -lt $quotationIds.Count) { $quotationIds[$i] } else { $null });
            InvoiceNumber=('SD-INV-2026-{0:D4}' -f ($i + 1)); InvoiceDate=(New-Date (-170 + $i * 7)); DueDate=(New-Date (-140 + $i * 7));
            SubTotal=$sub; GSTPercent=18; TaxAmount=($total - $sub); TotalAmount=$total; PaidAmount=$paid; BalanceDue=($total - $paid);
            PaymentStatus=$(if ($paid -eq 0) { if ($i % 4 -eq 0) { 'Overdue' } else { 'Pending' } } elseif ($paid -lt $total) { 'Partially Paid' } else { 'Paid' });
            Subject=($serviceTypes[$i % $serviceTypes.Count] + ' service invoice'); PaymentTerms='30 Days'; PlaceOfSupply=$cities[$clientIndex % $cities.Count].State;
            GSTMode='IGST'; Notes='ServoERP demo: tax invoice for sales and marketing screenshots.'
        }
        $invoiceIds += $invoiceId
        [void](Insert-Row $conn 'InvoiceLineItems' @{ InvoiceID=$invoiceId; Description=($serviceTypes[$i % $serviceTypes.Count] + ' labour and consumables'); HSNCode='998719'; Unit='Job'; Quantity=1; Rate=$sub; Amount=$sub; GSTPercent=18; TaxAmount=($total - $sub); IsBillable=1 })
        if ($paid -gt 0) {
            [void](Insert-Row $conn 'Payments' @{ PaymentNumber=('SD-PAY-2026-{0:D4}' -f ($i + 1)); InvoiceID=$invoiceId; ClientID=$clientIds[$clientIndex]; AmountPaid=$paid; PaymentDate=(New-Date (-132 + $i * 7)); PaymentMode=(@('NEFT/RTGS','UPI','Cheque','Bank Transfer')[$i % 4]); ReferenceNumber=('SDUTR{0:D8}' -f ($i + 1001)); Notes='ServoERP demo: collection history for dashboard and payment module.' })
        }
    }

    for ($i = 0; $i -lt 80; $i++) {
        $clientIndex = $i % $clientIds.Count
        $status = $statusList[$i % $statusList.Count]
        $scheduled = New-Date (-240 + $i * 5)
        $jobId = Insert-Row $conn 'Jobs' @{
            JobNumber=('SD-JOB-2026-{0:D4}' -f ($i + 1)); ClientID=$clientIds[$clientIndex]; SiteID=$siteIds[$clientIndex % $siteIds.Count];
            Title=('{0} - {1}' -f $serviceTypes[$i % $serviceTypes.Count], $clientNames[$clientIndex].Replace('SD ',''));
            JobTitle=('{0} - {1}' -f $serviceTypes[$i % $serviceTypes.Count], $cities[$clientIndex % $cities.Count].City);
            Description='Demo service job with realistic technician assignment, parts usage, and customer notes.';
            AssignedEmployeeID=$employeeIds[$i % $employeeIds.Count]; ScheduledDate=$scheduled; CompletedDate=$(if ($status -eq 'Completed') { $scheduled.AddDays(1) } else { $null });
            Priority=$priorityList[$i % $priorityList.Count]; Status=$status; EstimatedCost=(4500 + $i * 350); Revenue=(8500 + $i * 700);
            Notes='ServoERP demo: field-service work order for dispatch, jobs and reporting.'; CreatedDate=$scheduled.AddDays(-2); JobType=$serviceTypes[$i % $serviceTypes.Count];
            LinkedContractId=$(if ($i -lt $contractIds.Count) { $contractIds[$i] } else { $null }); PipelineStatus=$(if ($status -eq 'Completed') { 'Closed' } elseif ($status -eq 'In Progress') { 'InProgress' } else { 'Assigned' });
            QuotedRevenue=(9000 + $i * 750); ActualRevenue=$(if ($status -eq 'Completed') { 8500 + $i * 700 } else { 0 }); IsOverdue=([int]($status -eq 'Overdue'))
        }
        [void](Insert-Row $conn 'JobActivityLog' @{ JobId=$jobId; ActivityText='Technician assigned and customer informed.'; PerformedBy='ServoERP Demo Dispatcher'; ActivityDate=$scheduled.AddHours(-6); ActivityType='Info' })
        if ($i -lt 35) {
            [void](Insert-Row $conn 'JobPartsUsed' @{ JobId=$jobId; InventoryItemId=$stockIds[$i % $stockIds.Count]; ItemDescription='Demo consumable used on site'; QuantityUsed=(1 + ($i % 4)); Unit='Nos'; UnitCost=(250 + $i * 12); TotalCost=((1 + ($i % 4)) * (250 + $i * 12)); IsFromInventory=1; StockStatus='Issued' })
        }
    }

    for ($i = 0; $i -lt 30; $i++) {
        $clientIndex = $i % $clientIds.Count
        $opened = New-Date (-90 + $i * 4)
        $status = @('New','Assigned','In Progress','Resolved','Closed')[$i % 5]
        $incidentId = Insert-Row $conn 'ServiceDeskIncidents' @{
            IncidentNumber=('SD-INC-2026-{0:D4}' -f ($i + 1)); ClientId=$clientIds[$clientIndex]; SiteId=$siteIds[$clientIndex % $siteIds.Count];
            AssignedEmployeeId=$employeeIds[$i % $employeeIds.Count]; LinkedJobId=$null; CallerName=$contacts[$i % $contacts.Count]; CallerPhone=('91{0:D8}' -f (72000000 + $i * 617));
            Category=(@('HVAC','Electrical','Plumbing','Fire Safety','Insulation')[$i % 5]); EquipmentType=(@('VRF System','Main LT Panel','Pump Room','Fire Alarm Panel','Duct Insulation')[$i % 5]);
            AssetSerialNumber=('ASSET-SD-{0:D5}' -f ($i + 1)); Priority=$priorityList[$i % $priorityList.Count]; Status=$status;
            ShortDescription=('{0} support request at {1}' -f $serviceTypes[$i % $serviceTypes.Count], $cities[$clientIndex % $cities.Count].City);
            Description='Customer reported operational issue. Demo incident includes SLA, assignment and resolution trail.';
            RootCause=$(if ($status -in @('Resolved','Closed')) { 'Wear and tear / preventive replacement completed' } else { $null });
            ResolutionCode=$(if ($status -in @('Resolved','Closed')) { 'Resolved on site' } else { $null });
            OpenedAt=$opened; AssignedAt=$opened.AddHours(2); ResolvedAt=$(if ($status -in @('Resolved','Closed')) { $opened.AddHours(18) } else { $null });
            ClosedAt=$(if ($status -eq 'Closed') { $opened.AddHours(24) } else { $null }); SlaDueAt=$opened.AddHours(24); SlaBreached=([int]($i % 7 -eq 0));
            CreatedByName='ServoERP Demo Desk'; ModifiedByName='ServoERP Demo Desk'; ModifiedDate=(Get-Date)
        }
        [void](Insert-Row $conn 'ServiceDeskNotes' @{ IncidentId=$incidentId; NoteType='Work note'; NoteText='Demo note: ticket triaged, customer updated, field team coordinated.'; CreatedByName='ServoERP Demo Desk'; CreatedAt=$opened.AddHours(3) })
    }

    for ($i = 0; $i -lt 20; $i++) {
        $vendorId = $vendorIds[$i % $vendorIds.Count]
        $poTotal = 18000 + ($i * 8700)
        $paid = if ($i % 4 -eq 0) { $poTotal } elseif ($i % 4 -eq 1) { [math]::Round($poTotal * 0.4, 2) } else { 0 }
        $poId = Insert-Row $conn 'PurchaseOrders' @{
            VendorID=$vendorId; PONumber=('SD-PO-2026-{0:D4}' -f ($i + 1)); PODate=(New-Date (-200 + $i * 9)); PayByDate=(New-Date (-170 + $i * 9));
            VendorInvoiceNumber=('VIN-SD-{0:D5}' -f ($i + 1)); LinkedToType=(@('WorkOrder','Contract','General','Inventory')[$i % 4]); LinkedToId=$null;
            TotalAmount=$poTotal; PaidAmount=$paid; Status=$(if ($paid -eq $poTotal) { 'Paid' } elseif ($paid -gt 0) { 'Partially Paid' } else { 'Pending' });
            PaymentReference=$(if ($paid -gt 0) { 'SDPOUTR' + ('{0:D6}' -f ($i + 1)) } else { $null }); Notes='ServoERP demo: purchase order for materials and vendor module.'
        }
        [void](Insert-Row $conn 'PurchaseLineItems' @{ POID=$poId; InventoryItemId=$stockIds[$i % $stockIds.Count]; Description='Demo material purchase line'; HsnSacCode='8415'; Quantity=(2 + ($i % 6)); Rate=($poTotal / (2 + ($i % 6))); GSTRate=18; CGSTRate=9; SGSTRate=9; IGSTRate=18; Amount=$poTotal })
    }

    for ($i = 0; $i -lt $contractIds.Count; $i++) {
        [void](Insert-Row $conn 'SLALogs' @{ ContractID=$contractIds[$i]; MetricType=(@('Response Time','Repair Time','Uptime')[$i % 3]); Target=(@('4 Hours','12 Hours','99%')[$i % 3]); Actual=(@('3.5 Hours','10 Hours','99.3%')[$i % 3]); LogDate=(New-Date (-60 + $i * 3)); Compliant=([int]($i % 6 -ne 0)); Notes='ServoERP demo: SLA record for AMC dashboard.' })
    }

    New-Item -ItemType Directory -Force -Path $ExportFolder | Out-Null
    $summary = foreach ($table in 'B2BClients','ClientSites','AMCContracts','Jobs','TenderBids','Invoices','Payments','Vendors','PurchaseOrders','StockItems','Employees','ServiceDeskIncidents') {
        [pscustomobject]@{ Table=$table; Count=(Invoke-Scalar $conn "SELECT COUNT(*) FROM [$table]") }
    }
    $summary | Export-Csv -NoTypeInformation -Path (Join-Path $ExportFolder 'demo-data-counts.csv')
    $summary | Format-Table -AutoSize
}
finally {
    $conn.Close()
}

