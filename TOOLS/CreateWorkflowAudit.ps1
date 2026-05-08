param(
    [string]$AssemblyPath = "C:\HVAC_PRO_MSE\SOURCE_CODE\bin\Debug\HVAC_Pro_Desktop.exe"
)

$ErrorActionPreference = "Stop"

if (!(Test-Path -LiteralPath $AssemblyPath)) {
    throw "Application assembly not found at $AssemblyPath. Build the solution first."
}

$configPath = $AssemblyPath + ".config"
if (Test-Path -LiteralPath $configPath) {
    Add-Type -AssemblyName System.Configuration
    [AppDomain]::CurrentDomain.SetData("APP_CONFIG_FILE", $configPath)
    [System.Configuration.ConfigurationManager]::RefreshSection("connectionStrings")
    [System.Configuration.ConfigurationManager]::RefreshSection("appSettings")
    $mappedConfig = New-Object System.Configuration.ExeConfigurationFileMap
    $mappedConfig.ExeConfigFilename = $configPath
    $configuration = [System.Configuration.ConfigurationManager]::OpenMappedExeConfiguration($mappedConfig, [System.Configuration.ConfigurationUserLevel]::None)
    $connection = $configuration.ConnectionStrings.ConnectionStrings["HVACPro_Connection"]
    if ($connection -and -not [System.Configuration.ConfigurationManager]::ConnectionStrings["HVACPro_Connection"]) {
        $collection = [System.Configuration.ConfigurationManager]::ConnectionStrings
        $readOnlyField = [System.Configuration.ConfigurationElementCollection].GetField("bReadOnly", [Reflection.BindingFlags]"Instance,NonPublic")
        $readOnlyField.SetValue($collection, $false)
        $collection.Add($connection)
    }
}

$assemblyDir = Split-Path -Parent $AssemblyPath
[AppDomain]::CurrentDomain.add_AssemblyResolve({
    param($sender, $args)
    $name = New-Object System.Reflection.AssemblyName($args.Name)
    $candidate = Join-Path $assemblyDir ($name.Name + ".dll")
    if (Test-Path -LiteralPath $candidate) {
        return [Reflection.Assembly]::LoadFrom($candidate)
    }
    return $null
}) | Out-Null

$asm = [Reflection.Assembly]::LoadFrom($AssemblyPath)
$prefix = "QA_CREATE_" + (Get-Date -Format "yyyyMMdd_HHmmss")
$results = New-Object System.Collections.Generic.List[object]
$state = @{}

function Get-AppType([string]$shortName, [string]$area = "Models") {
    $type = $asm.GetType("HVAC_Pro_Desktop.$area.$shortName")
    if ($null -eq $type) {
        $type = $asm.GetTypes() | Where-Object { $_.Name -eq $shortName } | Select-Object -First 1
    }
    if ($null -eq $type) { throw "Type not found: $shortName" }
    return $type
}

function New-Service([string]$name) {
    return [Activator]::CreateInstance((Get-AppType $name "Services"))
}

function New-Model([string]$name, [hashtable]$props = @{}) {
    $type = Get-AppType $name "Models"
    $obj = [Activator]::CreateInstance($type)
    foreach ($key in $props.Keys) {
        $prop = $type.GetProperty($key)
        if ($null -eq $prop) { throw "$name.$key property not found" }
        $prop.SetValue($obj, $props[$key], $null)
    }
    return $obj
}

function New-GenericList([string]$modelName, [object[]]$items) {
    $itemType = Get-AppType $modelName "Models"
    $listType = [System.Collections.Generic.List``1].MakeGenericType($itemType)
    $list = [Activator]::CreateInstance($listType)
    foreach ($item in $items) { [void]$list.Add($item) }
    return $list
}

function Invoke-Check([string]$name, [scriptblock]$body) {
    $sw = [Diagnostics.Stopwatch]::StartNew()
    try {
        $detail = & $body
        $sw.Stop()
        $results.Add([pscustomobject]@{
            Workflow = $name
            Status = "PASS"
            Detail = [string]$detail
            Ms = $sw.ElapsedMilliseconds
        }) | Out-Null
    }
    catch {
        $sw.Stop()
        $message = $_.Exception.Message
        if ($_.Exception.InnerException) { $message = $_.Exception.InnerException.Message }
        $results.Add([pscustomobject]@{
            Workflow = $name
            Status = "FAIL"
            Detail = $message
            Ms = $sw.ElapsedMilliseconds
        }) | Out-Null
    }
}

Invoke-Check "Client: New + Save" {
    $svc = New-Service "ClientService"
    $client = New-Model "B2BClient" @{
        CompanyName = "$prefix Client"
        IndustryType = "HVAC QA"
        PrimaryContact = "QA Tester"
        Phone = "9999999999"
        Email = "$($prefix.ToLower())@example.com"
        GSTNumber = "27ABCDE1234F1Z5"
        PANNumber = "ABCDE1234F"
        PaymentTermsDays = 30
        CreditLimit = [decimal]25000
        BillingAddress = "QA test address"
        City = "Pune"
        RelationshipStage = "Active"
        Tags = "qa,create-test"
        HealthScore = 80
        Notes = "Created by automated create workflow audit."
        AssignedTo = "QA"
        LeadSource = "Automation"
        TotalAnnualValue = [decimal]120000
        CustomerSince = [DateTime]::Today
        IsActive = $true
    }
    $id = $svc.CreateClient($client)
    if ($id -le 0) { throw "CreateClient returned invalid id." }
    $read = $svc.GetClientById($id)
    if ($null -eq $read -or $read.CompanyName -notlike "$prefix*") { throw "Created client could not be read back." }
    $state.ClientId = $id
    "ClientID=$id"
}

Invoke-Check "Client Site: New + Save" {
    if (!$state.ClientId) { throw "Client dependency missing." }
    $svc = New-Service "ClientService"
    $site = New-Model "ClientSite" @{
        ClientID = [int]$state.ClientId
        SiteName = "$prefix Site"
        Address = "QA site address"
        City = "Pune"
        ACSystemCount = 2
        RefrigerationSystemCount = 1
        CoolingTowerCount = 0
        IsCritical = $true
        TravelRateINR = [decimal]450
    }
    $id = $svc.CreateSite($site)
    if ($id -le 0) { throw "CreateSite returned invalid id." }
    $sites = $svc.GetClientSites([int]$state.ClientId)
    if (-not ($sites | Where-Object { $_.SiteID -eq $id })) { throw "Created site could not be read back." }
    $state.SiteId = $id
    "SiteID=$id"
}

Invoke-Check "Client Team: Add + Save" {
    if (!$state.ClientId) { throw "Client dependency missing." }
    $svc = New-Service "ClientService"
    $member = New-Model "ClientTeamMember" @{
        ClientId = [int]$state.ClientId
        EmployeeName = "$prefix Contact"
        Position = "Facilities"
        EmailId = "$($prefix.ToLower()).contact@example.com"
        ContactNo = "8888888888"
        IsPrimary = $true
        IsActive = $true
    }
    $svc.SaveTeamMember($member)
    $members = $svc.GetTeamMembers([int]$state.ClientId)
    $match = $members | Where-Object { $_.EmployeeName -eq "$prefix Contact" } | Select-Object -First 1
    if ($null -eq $match) { throw "Created team member could not be read back." }
    "TeamMemberID=$($match.Id)"
}

Invoke-Check "Client Activity: Add Note" {
    if (!$state.ClientId) { throw "Client dependency missing." }
    $svc = New-Service "ClientService"
    $activity = New-Model "ClientActivity" @{
        ClientId = [int]$state.ClientId
        ActivityType = "Note"
        Title = "$prefix QA Activity"
        Detail = "Automated create workflow note."
        CreatedAt = [DateTime]::Now
        CreatedBy = "QA Audit"
    }
    $svc.LogActivity($activity)
    $activities = $svc.GetActivities([int]$state.ClientId, $null)
    if (-not ($activities | Where-Object { $_.Title -eq "$prefix QA Activity" })) { throw "Created client activity could not be read back." }
    "ActivityCreated"
}

Invoke-Check "Vendor: New Supplier + Save" {
    $svc = New-Service "VendorService"
    $vendor = New-Model "Vendor" @{
        VendorName = "$prefix Supplier"
        GSTNumber = "27ABCDE1234F1Z5"
        DefaultCreditDays = 30
        PANNumber = "ABCDE1234F"
        Phone = "7777777777"
        Email = "$($prefix.ToLower()).vendor@example.com"
        Address = "QA vendor address"
        City = "Pune"
        Category = "HVAC Parts"
        VendorType = "Supplier"
        MSMERegistered = "No"
        GSTRegistrationType = "Regular"
        PreferredPaymentMode = "Bank Transfer"
        StateCode = "27"
        Notes = "Created by automated create workflow audit."
        SpecialisationTags = "qa,parts"
        IsActive = $true
        CreatedDate = [DateTime]::Now
    }
    $id = $svc.Create($vendor)
    if ($id -le 0) { throw "Vendor create returned invalid id." }
    $read = $svc.GetById($id)
    if ($null -eq $read -or $read.VendorName -notlike "$prefix*") { throw "Created vendor could not be read back." }
    $state.VendorId = $id
    "VendorID=$id"
}

Invoke-Check "Inventory: New Item + Save" {
    $svc = New-Service "InventoryService"
    $item = New-Model "StockItem" @{
        ItemName = "$prefix Filter Set"
        Category = "Consumables"
        CurrentStock = [decimal]10
        Unit = "Nos"
        LastPurchaseRate = [decimal]125
        ReorderLevel = [decimal]3
        ReservedStock = [decimal]0
        LastUpdated = [DateTime]::Now
    }
    $id = $svc.Create($item)
    if ($id -le 0) { throw "Inventory create returned invalid id." }
    $read = $svc.GetById($id)
    if ($null -eq $read -or $read.ItemName -notlike "$prefix*") { throw "Created stock item could not be read back." }
    $state.ItemId = $id
    "ItemID=$id"
}

Invoke-Check "Contract: New AMC + Save" {
    if (!$state.ClientId -or !$state.SiteId) { throw "Client/site dependency missing." }
    $svc = New-Service "ContractService"
    $contract = New-Model "AMCContract" @{
        ClientID = [int]$state.ClientId
        SiteID = [int]$state.SiteId
        StartDate = [DateTime]::Today
        EndDate = [DateTime]::Today.AddYears(1)
        MonthlyValue = [decimal]5000
        AnnualValue = [decimal]60000
        ContractStatus = "Active"
        SLAResponseTimeHours = 8
        SLAUptimePercent = [decimal]98
        SLARepairTimeHours = 24
        MaintenanceFrequency = "Monthly"
        ContractType = "QA AMC"
        Notes = "Created by automated create workflow audit."
    }
    $id = $svc.CreateContract($contract)
    if ($id -le 0) { throw "Contract create returned invalid id." }
    $read = $svc.GetContractDetails($id)
    if ($null -eq $read -or $read.ContractID -ne $id) { throw "Created contract could not be read back." }
    $state.ContractId = $id
    "ContractID=$id"
}

Invoke-Check "Service Desk: New Ticket + Save" {
    if (!$state.ClientId -or !$state.SiteId) { throw "Client/site dependency missing." }
    $svc = New-Service "ServiceDeskService"
    $opened = [DateTime]::Now
    $incident = New-Model "ServiceDeskIncident" @{
        ClientId = [Nullable[int]][int]$state.ClientId
        SiteId = [Nullable[int]][int]$state.SiteId
        CallerName = "QA Caller"
        CallerPhone = "6666666666"
        Category = "Breakdown"
        EquipmentType = "Split AC"
        AssetSerialNumber = "$prefix-SERIAL"
        Priority = "High"
        Status = "Open"
        ShortDescription = "$prefix service ticket"
        Description = "Automated create workflow service desk incident."
        OpenedAt = $opened
        SlaDueAt = $opened.AddHours(4)
        CreatedByName = "QA Audit"
    }
    $id = $svc.Save($incident)
    if ($id -le 0) { throw "Service ticket save returned invalid id." }
    $read = $svc.GetDetail($id)
    if ($null -eq $read -or $read.Incident -eq $null -or $read.Incident.IncidentId -ne $id) { throw "Created service ticket could not be read back." }
    $state.IncidentId = $id
    "IncidentID=$id"
}

Invoke-Check "Service Desk: Add Note" {
    if (!$state.IncidentId) { throw "Incident dependency missing." }
    $svc = New-Service "ServiceDeskService"
    $note = New-Model "ServiceDeskNote" @{
        IncidentId = [int]$state.IncidentId
        NoteType = "Internal"
        NoteText = "$prefix QA note"
        CreatedByName = "QA Audit"
        CreatedAt = [DateTime]::Now
    }
    $svc.AddNote($note)
    $read = $svc.GetDetail([int]$state.IncidentId)
    if (-not ($read.Notes | Where-Object { $_.NoteText -eq "$prefix QA note" })) { throw "Created service desk note could not be read back." }
    "NoteCreated"
}

Invoke-Check "Job: New Job + Save" {
    if (!$state.ClientId -or !$state.SiteId) { throw "Client/site dependency missing." }
    $svc = New-Service "JobService"
    $job = New-Model "Job" @{
        JobNumber = $svc.GenerateJobNumber()
        ClientID = [int]$state.ClientId
        SiteID = [int]$state.SiteId
        Title = "$prefix Field Visit"
        JobTitle = "$prefix Field Visit"
        Description = "Automated create workflow job."
        ScheduledDate = [DateTime]::Today.AddDays(1)
        Priority = "Medium"
        Status = "Pending"
        PipelineStatus = "Pending"
        JobType = "Service"
        EstimatedCost = [decimal]1500
        Revenue = [decimal]3500
        QuotedRevenue = [decimal]3500
        Notes = "QA audit"
    }
    $id = $svc.Create($job)
    if ($id -le 0) { throw "Job create returned invalid id." }
    $read = $svc.GetById($id)
    if ($null -eq $read -or $read.JobID -ne $id) { throw "Created job could not be read back." }
    $state.JobId = $id
    "JobID=$id"
}

Invoke-Check "Invoice: New Draft + Save" {
    if (!$state.ClientId -or !$state.SiteId) { throw "Client/site dependency missing." }
    $svc = New-Service "InvoiceService"
    $contractId = 0
    if ($state.ContractId) { $contractId = [int]$state.ContractId }
    $line = New-Model "InvoiceLineItem" @{
        StockItemID = [Nullable[int]][int]$state.ItemId
        Description = "$prefix Preventive service"
        HSNCode = "998719"
        Unit = "Nos"
        Quantity = [decimal]1
        Rate = [decimal]2500
        GSTPercent = [decimal]18
        IsStockItem = $false
        IsBillable = $true
        CoverageNote = "QA"
    }
    $inv = New-Model "Invoice" @{
        ContractID = $contractId
        ClientID = [int]$state.ClientId
        SiteID = [int]$state.SiteId
        InvoiceTitle = "TAX INVOICE"
        WorkflowType = "Service"
        Subject = "$prefix invoice"
        GSTMode = "IGST"
        PaymentTerms = "30 Days"
        PlaceOfSupply = "Maharashtra"
        InvoiceDate = [DateTime]::Today
        DueDate = [DateTime]::Today.AddDays(30)
        GSTPercent = [decimal]18
        PaymentStatus = "Draft"
        Notes = "Created by automated create workflow audit."
    }
    $inv.LineItems = New-GenericList "InvoiceLineItem" @($line)
    $id = $svc.CreateInvoiceWithLineItems($inv)
    if ($id -le 0) { throw "Invoice create returned invalid id." }
    $read = $svc.GetInvoiceById($id)
    if ($null -eq $read -or $read.InvoiceID -ne $id) { throw "Created invoice could not be read back." }
    $state.InvoiceId = $id
    "InvoiceID=$id"
}

Invoke-Check "Payment: Record Payment" {
    if (!$state.InvoiceId -or !$state.ClientId) { throw "Invoice dependency missing." }
    $svc = New-Service "PaymentService"
    $payment = New-Model "Payment" @{
        InvoiceID = [int]$state.InvoiceId
        ClientID = [int]$state.ClientId
        AmountPaid = [decimal]500
        PaymentDate = [DateTime]::Today
        PaymentMode = "UPI"
        ReferenceNumber = "$prefix-PAY"
        Notes = "Automated QA payment."
        CreatedDate = [DateTime]::Now
    }
    $id = $svc.RecordPayment($payment)
    if ($id -le 0) { throw "Payment create returned invalid id." }
    $payments = $svc.GetPaymentsForInvoice([int]$state.InvoiceId)
    if (-not ($payments | Where-Object { $_.PaymentID -eq $id })) { throw "Created payment could not be read back." }
    "PaymentID=$id"
}

Invoke-Check "Purchase: New PO + Save" {
    if (!$state.VendorId -or !$state.ClientId -or !$state.SiteId) { throw "Vendor/client/site dependency missing." }
    $svc = New-Service "PurchaseService"
    $line = New-Model "PurchaseLineItem" @{
        InventoryItemId = [Nullable[int]][int]$state.ItemId
        Description = "$prefix Copper pipe"
        HsnSacCode = "841590"
        Quantity = [decimal]2
        UOM = "Nos"
        Rate = [decimal]600
        GSTRate = [decimal]18
        CGSTRate = [decimal]9
        SGSTRate = [decimal]9
        JobLink = "QA"
        Amount = [decimal]1200
    }
    $po = New-Model "PurchaseOrder" @{
        VendorID = [int]$state.VendorId
        ClientID = [int]$state.ClientId
        SiteID = [int]$state.SiteId
        PONumber = "$prefix-PO"
        PODate = [DateTime]::Today
        PayByDate = [DateTime]::Today.AddDays(30)
        VendorInvoiceNumber = "$prefix-VINV"
        LinkedToType = "QA"
        DeliveryMode = "SiteDelivery"
        DeliveryAddress = "QA site address"
        AddToClientInvoice = $false
        Status = "Pending"
        TotalAmount = [decimal]1200
        Notes = "Created by automated create workflow audit."
        CreatedDate = [DateTime]::Now
    }
    $po.LineItems = New-GenericList "PurchaseLineItem" @($line)
    $id = $svc.Create($po)
    if ($id -le 0) { throw "Purchase create returned invalid id." }
    $read = $svc.GetById($id)
    if ($null -eq $read -or $read.POID -ne $id) { throw "Created purchase order could not be read back." }
    $state.PoId = $id
    "POID=$id"
}

Invoke-Check "Quotation: New Quote + Save" {
    if (!$state.ClientId -or !$state.SiteId) { throw "Client/site dependency missing." }
    $svc = New-Service "TenderService"
    $bid = New-Model "TenderBid" @{
        QuotationNumber = "$prefix-QTN"
        TenderName = "$prefix Quotation"
        ClientID = [int]$state.ClientId
        SiteID = [int]$state.SiteId
        SystemCount = 1
        BidValue = [decimal]3500
        DueDate = [DateTime]::Today.AddDays(15)
        RequiredByDate = [Nullable[DateTime]][DateTime]::Today.AddDays(7)
        Status = "Draft"
        ClientName = "$prefix Client"
        RequirementCategory = "Service"
        ItemName = "$prefix Filter Set"
        RequiredQuantity = [decimal]1
        Unit = "Nos"
        InventoryAvailable = [decimal]10
        ShortfallQuantity = [decimal]0
        EstimatedInternalRate = [decimal]1000
        EstimatedSupplierRate = [decimal]1250
        EstimatedInternalCost = [decimal]1000
        EstimatedExternalCost = [decimal]1250
        RecommendedVendorID = [Nullable[int]][int]$state.VendorId
        ComparisonSummary = "QA quote"
        AnalysisStatus = "Ready"
        Notes = "Created by automated create workflow audit."
    }
    $id = $svc.Create($bid)
    if ($id -le 0) { throw "Quotation create returned invalid id." }
    $read = $svc.GetById($id)
    if ($null -eq $read -or $read.BidID -ne $id) { throw "Created quotation could not be read back." }
    $state.BidId = $id
    "BidID=$id"
}

Invoke-Check "Employee: New Employee + Save" {
    $svc = New-Service "EmployeeService"
    $employee = New-Model "Employee" @{
        EmployeeCode = $svc.GenerateNextEmployeeCode()
        Name = "$prefix Technician"
        Designation = "Technician"
        Department = "Service"
        ClientSite = "QA Site"
        Phone = "5555555555"
        JoiningDate = [Nullable[DateTime]][DateTime]::Today
        DateOfJoining = [Nullable[DateTime]][DateTime]::Today
        EmploymentType = "Full-time"
        PAN = "ABCDE1234F"
        AadhaarLast4 = "1234"
        TaxRegime = "Old"
        StateCode = "27"
        EPFApplicable = $true
        ESIApplicable = $false
        PTApplicable = $true
        BankAccountNumber = "1234567890"
        BankIFSC = "HDFC0000001"
        BankName = "HDFC"
        Address = "QA employee address"
        NatureOfWork = "HVAC service"
        BasicSalary = [decimal]25000
        GrossSalary = [decimal]30000
        Status = "Active"
        CreatedDate = [DateTime]::Now
    }
    $id = $svc.Create($employee)
    if ($id -le 0) { throw "Employee create returned invalid id." }
    $read = $svc.GetById($id)
    if ($null -eq $read -or $read.EmployeeID -ne $id) { throw "Created employee could not be read back." }
    $state.EmployeeId = $id
    "EmployeeID=$id"
}

Invoke-Check "Employee Skill: Add + Save" {
    if (!$state.EmployeeId) { throw "Employee dependency missing." }
    $svc = New-Service "EmployeeService"
    $skill = New-Model "EmployeeSkillDto" @{
        EmployeeID = [int]$state.EmployeeId
        SkillName = "$prefix Brazing"
        CertificationNumber = "$prefix-CERT"
        ExpiryDate = [Nullable[DateTime]][DateTime]::Today.AddYears(1)
    }
    $id = $svc.SaveSkill($skill)
    if ($id -le 0) { throw "Skill save returned invalid id." }
    $skills = $svc.GetEmployeeSkills([int]$state.EmployeeId)
    if (-not ($skills | Where-Object { $_.SkillID -eq $id })) { throw "Created employee skill could not be read back." }
    "SkillID=$id"
}

Invoke-Check "Payroll: Salary Structure + Advance + Loan" {
    if (!$state.EmployeeId) { throw "Employee dependency missing." }
    $svc = New-Service "PayrollService"
    $structure = New-Model "SalaryStructure" @{
        EmployeeId = [int]$state.EmployeeId
        EffectiveFrom = [DateTime]::Today
        BasicSalary = [decimal]25000
        DA = [decimal]0
        HRA = [decimal]10000
        SpecialAllowance = [decimal]5000
        ConveyanceAllowance = [decimal]1600
        MedicalAllowance = [decimal]1250
        LTA = [decimal]0
        OtherAllowances = [decimal]0
        GrossSalary = [decimal]42850
        IsActive = $true
        CreatedDate = [DateTime]::Now
    }
    $structureResult = $svc.SaveSalaryStructure($structure)
    if (-not $structureResult.Success) { throw "Salary structure save failed: $($structureResult.Message)" }
    $advance = New-Model "SalaryAdvance" @{
        EmployeeId = [int]$state.EmployeeId
        AdvanceAmount = [decimal]1000
        AdvanceDate = [DateTime]::Today
        RecoveryMonth = [int](Get-Date).Month
        RecoveryYear = [int](Get-Date).Year
        Recovered = $false
    }
    $advanceId = $svc.SaveSalaryAdvance($advance)
    $advances = $svc.GetAdvancesByEmployee([int]$state.EmployeeId)
    $advanceReadBack = $advances | Where-Object { $_.AdvanceAmount -eq [decimal]1000 -and $_.RecoveryMonth -eq [int](Get-Date).Month -and $_.RecoveryYear -eq [int](Get-Date).Year } | Select-Object -First 1
    if ($null -eq $advanceReadBack) { throw "Created salary advance could not be read back." }
    $loan = New-Model "EmployeeLoan" @{
        EmployeeId = [int]$state.EmployeeId
        LoanAmount = [decimal]5000
        MonthlyDeduction = [decimal]500
        LoanDate = [DateTime]::Today
        RemainingBalance = [decimal]5000
        Purpose = "QA audit"
        IsActive = $true
    }
    $loanId = $svc.SaveEmployeeLoan($loan)
    if ($loanId -le 0) { throw "Employee loan save returned invalid id." }
    "SalaryStructureID=$($structureResult.Data); AdvanceID=$($advanceReadBack.AdvanceId); LoanID=$loanId"
}

Invoke-Check "SLA Log: Add Event" {
    if (!$state.ContractId) { throw "Contract dependency missing." }
    $svc = New-Service "SLAService"
    $svc.LogSLAEvent([int]$state.ContractId, "Response", "8h", "4h", $true, "$prefix QA SLA")
    $logs = $svc.GetAllLogsForContract([int]$state.ContractId)
    if (-not ($logs | Where-Object { $_.Notes -eq "$prefix QA SLA" })) { throw "Created SLA log could not be read back." }
    "SLALogCreated"
}

Invoke-Check "Settings: Add HSN/SAC Row + Save" {
    $svc = New-Service "HsnSacMasterService"
    $existingCodes = @($svc.GetAll() | ForEach-Object { $_.Code })
    do {
        $code = (900000 + (Get-Random -Minimum 0 -Maximum 99999)).ToString("000000")
    } while ($existingCodes -contains $code)
    $entry = New-Model "HsnSacMasterEntry" @{
        CodeType = "SAC"
        Code = $code
        Description = "$prefix HVAC service"
        BusinessCategory = "Service"
        TaxRate = [decimal]18
        CGSTRate = [decimal]9
        SGSTRate = [decimal]9
        IGSTRate = [decimal]18
        Notes = "QA audit"
        IsDefault = $false
        IsActive = $true
    }
    $entryType = Get-AppType "HsnSacMasterEntry" "Models"
    $listType = [System.Collections.Generic.List``1].MakeGenericType($entryType)
    $list = [Activator]::CreateInstance($listType)
    foreach ($existing in $svc.GetAll()) { [void]$list.Add($existing) }
    [void]$list.Add($entry)
    $svc.SaveAll($list)
    $read = $svc.GetAll()
    if (-not ($read | Where-Object { $_.Description -eq "$prefix HVAC service" })) { throw "Created HSN/SAC row could not be read back." }
    "HsnSacSaved"
}

Invoke-Check "Settings: Create Login User" {
    $svc = New-Service "AuthService"
    $roles = $svc.GetRoles()
    $role = $roles | Select-Object -First 1
    if ($null -eq $role) { throw "No roles available for user creation." }
    $username = ($prefix.ToLower() -replace '[^a-z0-9_]', '_') + "_user"
    $result = $svc.CreateUser($username, "$prefix User", [int]$role.RoleId, $true)
    if (-not $result.Item1) { throw $result.Item2 }
    $users = $svc.GetUsers()
    if (-not ($users | Where-Object { $_.Username -eq $username })) { throw "Created user could not be read back." }
    "UserID=$($result.Item4); Username=$username"
}

$results | Format-Table -AutoSize
$failed = @($results | Where-Object { $_.Status -ne "PASS" })
Write-Host ""
Write-Host "QA prefix: $prefix"
Write-Host "Passed: $(($results | Where-Object Status -eq 'PASS').Count) / $($results.Count)"
if ($failed.Count -gt 0) {
    Write-Host "Failures:"
    $failed | Format-Table -AutoSize
    exit 1
}
