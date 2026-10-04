using System;
using System.Collections.Generic;
using System.Data.SqlClient;
using System.Text.RegularExpressions;
using Dapper;
using HVAC_Pro_Desktop.Models;

namespace HVAC_Pro_Desktop.DAL
{
    public sealed class RelationshipReferenceContext
    {
        public int Id { get; set; }
        public int? ClientId { get; set; }
        public int? SiteId { get; set; }
    }

    public sealed class RelationshipIntegrityRepository
    {
        private sealed class RelationshipSpec
        {
            public string Module;
            public string Label;
            public string ChildTable;
            public string ChildColumn;
            public string ParentTable;
            public string ParentColumn;
            public string ChildClientColumn;
            public string ParentClientColumn;
            public string Recommendation;
        }

        private sealed class ForeignKeyState
        {
            public bool IsDisabled { get; set; }
            public bool IsNotTrusted { get; set; }
        }

        private static readonly Regex SqlIdentifierPattern = new Regex("^[A-Za-z][A-Za-z0-9_]*$", RegexOptions.Compiled);
        private readonly DatabaseManager _db = new DatabaseManager();

        private static readonly RelationshipSpec[] ReportSpecs =
        {
            // Established core relationships are included alongside missing relationships so the report denominator is stable.
            Spec("Clients / Sites", "Sites → Clients", "ClientSites", "ClientID", "B2BClients", "ClientID", "Every site must belong to an existing client."),
            Spec("Clients / Sites", "Sites → Technicians", "ClientSites", "AssignedTechnicianID", "Employees", "EmployeeID", "Prevent invalid default technician assignments."),
            Spec("Jobs", "Jobs → Clients", "Jobs", "ClientID", "B2BClients", "ClientID", "Protect job ownership by client."),
            Spec("Jobs", "Jobs → Sites", "Jobs", "SiteID", "ClientSites", "SiteID", "Add a site where known and correct cross-client selections.", "ClientID", "ClientID"),
            Spec("Jobs", "Jobs → Technicians", "Jobs", "AssignedEmployeeID", "Employees", "EmployeeID", "Assign a valid active technician before dispatch."),
            Spec("Jobs", "Jobs → Contracts", "Jobs", "LinkedContractId", "AMCContracts", "ContractID", "Link AMC work to its source contract."),
            Spec("Jobs", "Jobs → Invoices", "Jobs", "InvoiceId", "Invoices", "InvoiceID", "Link invoiced jobs to the generated invoice."),
            Spec("Jobs", "Checklist → Jobs", "JobChecklistItems", "JobId", "Jobs", "JobID", "Keep checklist evidence attached to a saved job."),
            Spec("Jobs", "Parts used → Jobs", "JobPartsUsed", "JobId", "Jobs", "JobID", "Keep material consumption attached to a saved job."),
            Spec("Jobs", "Activity log → Jobs", "JobActivityLog", "JobId", "Jobs", "JobID", "Keep job history attached to a saved job."),
            Spec("AMC", "Contracts → Clients", "AMCContracts", "ClientID", "B2BClients", "ClientID", "Protect AMC ownership by client."),
            Spec("AMC", "Contracts → Sites", "AMCContracts", "SiteID", "ClientSites", "SiteID", "Keep AMC sites aligned with their clients.", "ClientID", "ClientID"),
            Spec("AMC", "Equipment → Contracts", "AMCEquipment", "AMCID", "AMCContracts", "ContractID", "Keep covered equipment attached to a saved AMC."),
            Spec("AMC", "Visits → Contracts", "AMCVisits", "AMCID", "AMCContracts", "ContractID", "Keep service visits attached to a saved AMC."),
            Spec("AMC", "Visits → Jobs", "AMCVisits", "JobID", "Jobs", "JobID", "Link generated service visits to their work order."),
            Spec("Invoices", "Invoices → Clients", "Invoices", "ClientID", "B2BClients", "ClientID", "Protect customer ledger ownership."),
            Spec("Invoices", "Invoices → Sites", "Invoices", "SiteID", "ClientSites", "SiteID", "Link site-based invoices to the correct client site.", "ClientID", "ClientID"),
            Spec("Invoices", "Invoices → Contracts", "Invoices", "ContractID", "AMCContracts", "ContractID", "Preserve AMC billing traceability."),
            Spec("Invoices", "Invoices → Quotations", "Invoices", "QuotationBidID", "Quotations", "BidID", "Preserve quote-to-invoice conversion traceability."),
            Spec("Invoices", "Invoice lines → Invoices", "InvoiceLineItems", "InvoiceID", "Invoices", "InvoiceID", "Keep every line attached to its invoice."),
            Spec("Invoices", "Invoice lines → Jobs", "InvoiceLineItems", "JobID", "Jobs", "JobID", "Link invoice lines to jobs for revenue attribution."),
            Spec("Invoices", "Invoice lines → Stock", "InvoiceLineItems", "StockItemID", "StockItems", "ItemID", "Use NULL, not zero, for non-stock invoice lines."),
            Spec("Collections", "Payments → Invoices", "Payments", "InvoiceID", "Invoices", "InvoiceID", "Keep receipts attached to an invoice."),
            Spec("Collections", "Payments → Clients", "Payments", "ClientID", "B2BClients", "ClientID", "Protect receipt ownership by client."),
            Spec("Quotations", "Quotations → Clients", "Quotations", "ClientID", "B2BClients", "ClientID", "Protect quotation ownership by client."),
            Spec("Quotations", "Quotations → Sites", "Quotations", "SiteID", "ClientSites", "SiteID", "Keep quotation site ownership aligned with the client.", "ClientID", "ClientID"),
            Spec("Quotations", "Quotations → Recommended vendors", "Quotations", "RecommendedVendorID", "Vendors", "VendorID", "Protect supplier recommendations."),
            Spec("Quotations", "Quotations → Templates", "Quotations", "TemplateId", "QuoteTemplates", "TemplateId", "Preserve quotation template lineage."),
            Spec("Quotations", "Quotation lines → Quotations", "QuotationLineItems", "TenderBidId", "Quotations", "BidID", "Keep every quotation line attached to its quotation."),
            Spec("Quotations", "Quotation lines → Stock", "QuotationLineItems", "InventoryItemId", "StockItems", "ItemID", "Protect inventory availability and costing links."),
            Spec("Quotations", "Quotation lines → Vendors", "QuotationLineItems", "VendorID", "Vendors", "VendorID", "Protect selected supplier links."),
            Spec("Quotations", "Quotation lines → Best suppliers", "QuotationLineItems", "BestSupplierId", "Vendors", "VendorID", "Protect supplier comparison results."),
            Spec("Purchases", "Purchase orders → Vendors", "PurchaseOrders", "VendorID", "Vendors", "VendorID", "Keep purchase orders attached to a saved supplier/vendor."),
            Spec("Purchases", "Purchase orders → Clients", "PurchaseOrders", "ClientID", "B2BClients", "ClientID", "Protect client/project purchasing."),
            Spec("Purchases", "Purchase orders → Sites", "PurchaseOrders", "SiteID", "ClientSites", "SiteID", "Keep project purchases aligned with the client site.", "ClientID", "ClientID"),
            Spec("Purchases", "Purchase orders → Contracts", "PurchaseOrders", "RelatedContractID", "AMCContracts", "ContractID", "Protect contract cost allocation."),
            Spec("Purchases", "Purchase orders → Quotations", "PurchaseOrders", "RecommendedByBidID", "Quotations", "BidID", "Preserve quote-to-procurement lineage."),
            Spec("Purchases", "Purchase orders → Technicians", "PurchaseOrders", "AssignedTechnicianId", "Employees", "EmployeeID", "Protect delivery responsibility."),
            Spec("Purchases", "Purchase lines → Purchase orders", "PurchaseLineItems", "POID", "PurchaseOrders", "POID", "Keep every purchase line attached to its purchase order."),
            Spec("Purchases", "Purchase lines → Stock", "PurchaseLineItems", "InventoryItemId", "StockItems", "ItemID", "Protect purchased inventory links."),
            Spec("Purchases", "Purchase lines → Jobs", "PurchaseLineItems", "LinkedWorkOrderId", "Jobs", "JobID", "Link direct purchases to the consuming job."),
            Spec("Purchases", "Purchase lines → Vendors", "PurchaseLineItems", "VendorID", "Vendors", "VendorID", "Protect line-level supplier links."),
            Spec("Jobs", "Job parts → Stock", "JobPartsUsed", "InventoryItemId", "StockItems", "ItemID", "Protect inventory usage references."),
            Spec("Jobs", "Job parts → Vendors", "JobPartsUsed", "VendorID", "Vendors", "VendorID", "Link externally supplied parts to a valid supplier/vendor."),
            Spec("Jobs", "Job parts → Purchase orders", "JobPartsUsed", "LinkedPoId", "PurchaseOrders", "POID", "Preserve PO-to-consumption traceability."),
            Spec("Inventory", "Stock movements → Stock", "StockMovements", "ItemID", "StockItems", "ItemID", "Keep every stock movement attached to an inventory item."),
            Spec("Service Desk", "Incidents → Clients", "ServiceDeskIncidents", "ClientId", "B2BClients", "ClientID", "Protect customer support history."),
            Spec("Service Desk", "Incidents → Sites", "ServiceDeskIncidents", "SiteId", "ClientSites", "SiteID", "Keep incidents aligned with the reporting client site.", "ClientId", "ClientID"),
            Spec("Service Desk", "Incidents → Technicians", "ServiceDeskIncidents", "AssignedEmployeeId", "Employees", "EmployeeID", "Protect incident assignment and workload reporting."),
            Spec("Service Desk", "Incidents → Jobs", "ServiceDeskIncidents", "LinkedJobId", "Jobs", "JobID", "Link converted incidents to their work order."),
            Spec("Profitability", "Expenses → Clients", "ExpenseEntries", "ClientId", "B2BClients", "ClientID", "Protect client profitability attribution."),
            Spec("Profitability", "Expenses → Sites", "ExpenseEntries", "SiteId", "ClientSites", "SiteID", "Protect site profitability attribution.", "ClientId", "ClientID"),
            Spec("Profitability", "Expenses → Jobs", "ExpenseEntries", "JobId", "Jobs", "JobID", "Link direct expenses to jobs for margin reporting."),
            Spec("Profitability", "Import rows → Invoices", "ProfitabilityImportRows", "MatchedInvoiceId", "Invoices", "InvoiceID", "Confirm imported revenue against a saved invoice."),
            Spec("Profitability", "Import rows → Jobs", "ProfitabilityImportRows", "MatchedJobId", "Jobs", "JobID", "Confirm imported profitability rows against a saved job."),
            Spec("Profitability", "Expenses → Categories", "ExpenseEntries", "ExpenseCategoryId", "ExpenseCategories", "ExpenseCategoryId", "Keep every expense under a saved category."),
            Spec("Profitability", "Import rows → Import batches", "ProfitabilityImportRows", "ImportBatchId", "ProfitabilityImportBatches", "ImportBatchId", "Preserve the source batch for every imported profitability row."),

            Spec("Clients / Sites", "Contacts → Clients", "ClientContacts", "ClientID", "B2BClients", "ClientID", "Keep customer contacts attached to a saved client."),
            Spec("Clients / Sites", "Activity → Clients", "ClientActivity", "ClientId", "B2BClients", "ClientID", "Keep customer activity in the correct account timeline."),
            Spec("Clients / Sites", "Team members → Clients", "ClientTeam", "ClientId", "B2BClients", "ClientID", "Keep customer team members attached to a saved client."),
            Spec("Clients / Sites", "Assets → Clients", "ClientAssets", "ClientId", "B2BClients", "ClientID", "Protect equipment ownership by client."),
            Spec("Clients / Sites", "Assets → Sites", "ClientAssets", "SiteId", "ClientSites", "SiteID", "Keep installed assets at the correct customer site.", "ClientId", "ClientID"),
            Spec("Clients / Sites", "Assets → Contracts", "ClientAssets", "ContractId", "AMCContracts", "ContractID", "Link covered assets to the correct AMC contract."),
            Spec("Clients / Sites", "Documents → Clients", "ClientDocuments", "ClientId", "B2BClients", "ClientID", "Keep customer documents attached to the correct account."),
            Spec("Clients / Sites", "Documents → Sites", "ClientDocuments", "SiteId", "ClientSites", "SiteID", "Keep site documents under the correct customer.", "ClientId", "ClientID"),
            Spec("Clients / Sites", "Documents → Assets", "ClientDocuments", "AssetId", "ClientAssets", "AssetId", "Keep asset documents attached to saved equipment."),
            Spec("Clients / Sites", "Documents → Contracts", "ClientDocuments", "ContractId", "AMCContracts", "ContractID", "Keep contract documents attached to the source AMC."),
            Spec("Clients / Sites", "Rate cards → Clients", "ServiceRateCards", "ClientId", "B2BClients", "ClientID", "Keep negotiated service rates under the correct client."),
            Spec("Clients / Sites", "Price memory → Clients", "ClientPriceMemory", "ClientId", "B2BClients", "ClientID", "Keep quotation price history under the correct client."),

            Spec("Suppliers", "Stock → Suppliers", "StockItems", "VendorID", "Vendors", "VendorID", "Keep default inventory suppliers valid."),
            Spec("Suppliers", "Supplier prices → Suppliers", "SupplierItemPrices", "VendorID", "Vendors", "VendorID", "Keep price history attached to a saved supplier."),
            Spec("Suppliers", "Supplier prices → Stock", "SupplierItemPrices", "ItemID", "StockItems", "ItemID", "Connect supplier pricing to the inventory item."),
            Spec("Suppliers", "Supplier advances → Suppliers", "VendorAdvancePayments", "VendorId", "Vendors", "VendorID", "Keep supplier advances in the correct ledger."),
            Spec("Suppliers", "Supplier advances → Purchase orders", "VendorAdvancePayments", "POID", "PurchaseOrders", "POID", "Link advances to the purchase order where applicable."),

            Spec("Inventory", "Reservations → Invoices", "InvoiceInventoryReservations", "InvoiceID", "Invoices", "InvoiceID", "Keep reserved stock attached to the invoice."),
            Spec("Inventory", "Reservations → Stock", "InvoiceInventoryReservations", "StockItemID", "StockItems", "ItemID", "Keep every reservation attached to a valid inventory item."),
            Spec("Inventory", "Usage log → Invoices", "InventoryUsageLog", "InvoiceID", "Invoices", "InvoiceID", "Preserve invoice traceability for stock usage."),
            Spec("Inventory", "Usage log → Stock", "InventoryUsageLog", "StockItemID", "StockItems", "ItemID", "Preserve inventory traceability for usage entries."),
            Spec("Inventory", "Pending charges → Jobs", "PendingCharges", "WorkOrderId", "Jobs", "JobID", "Keep unbilled material charges attached to the consuming job."),
            Spec("Inventory", "Pending charges → Purchase orders", "PendingCharges", "SourcePoId", "PurchaseOrders", "POID", "Preserve the purchase source for pending job charges."),

            Spec("HR & Payroll", "Skills → Employees", "EmployeeSkills", "EmployeeID", "Employees", "EmployeeID", "Keep skills and certifications attached to the employee."),
            Spec("HR & Payroll", "Documents → Employees", "EmployeeDocuments", "EmployeeID", "Employees", "EmployeeID", "Keep employee documents attached to the employee."),
            Spec("HR & Payroll", "Salary structure → Employees", "SalaryStructure", "EmployeeID", "Employees", "EmployeeID", "Keep legacy salary structures attached to the employee."),
            Spec("HR & Payroll", "Salary structures → Employees", "SalaryStructures", "EmployeeId", "Employees", "EmployeeID", "Keep effective salary structures attached to the employee."),
            Spec("HR & Payroll", "Payroll entries → Employees", "PayrollEntries", "EmployeeId", "Employees", "EmployeeID", "Keep payroll entries attached to the employee."),
            Spec("HR & Payroll", "Payroll entries → Payroll runs", "PayrollEntries", "PayrollRunId", "PayrollRuns", "PayrollRunId", "Keep every payroll entry in its payroll run."),
            Spec("HR & Payroll", "Employee loans → Employees", "EmployeeLoans", "EmployeeId", "Employees", "EmployeeID", "Keep employee loans in the correct payroll account."),
            Spec("HR & Payroll", "Salary advances → Employees", "SalaryAdvances", "EmployeeId", "Employees", "EmployeeID", "Keep salary advances in the correct payroll account."),
            Spec("HR & Payroll", "TDS calculations → Employees", "TDSCalculations", "EmployeeId", "Employees", "EmployeeID", "Keep tax calculations attached to the employee."),
            Spec("HR & Payroll", "Statutory payments → Payroll runs", "StatutoryPayments", "PayrollRunId", "PayrollRuns", "PayrollRunId", "Keep statutory remittances attached to the payroll run."),

            Spec("Attendance", "Attendance → Employees", "AttendanceRecords", "EmployeeId", "Employees", "EmployeeID", "Keep attendance attached to a saved employee."),
            Spec("Attendance", "Field attendance → Employees", "EmployeeAttendance", "EmployeeID", "Employees", "EmployeeID", "Keep field check-in records attached to a saved employee."),
            Spec("Attendance", "Leave balances → Employees", "LeaveBalances", "EmployeeId", "Employees", "EmployeeID", "Keep leave balances attached to the employee."),
            Spec("Attendance", "Leave balances → Leave types", "LeaveBalances", "LeaveTypeId", "LeaveTypes", "LeaveTypeId", "Keep leave balances under a valid leave type."),

            Spec("Contracts", "SLA logs → Contracts", "SLALogs", "ContractID", "AMCContracts", "ContractID", "Keep SLA performance attached to the governing contract."),
            Spec("Service Desk", "Incident notes → Incidents", "ServiceDeskNotes", "IncidentId", "ServiceDeskIncidents", "IncidentId", "Keep service notes attached to the incident."),

            Spec("Master Data", "Template items → Quote templates", "QuoteTemplateItems", "TemplateId", "QuoteTemplates", "TemplateId", "Keep reusable quotation items in their source template."),
            Spec("Master Data", "Lookup values → Categories", "MasterLookupValues", "CategoryId", "MasterLookupCategories", "CategoryId", "Keep master lookup values under a valid category."),
            Spec("Master Data", "Unit aliases → Units", "UnitMeasurementAliases", "UnitMeasurementId", "UnitMeasurements", "UnitMeasurementID", "Keep unit aliases attached to a canonical unit."),
            Spec("Master Data", "Import errors → Import batches", "DataImportErrors", "BatchId", "DataImportBatches", "BatchId", "Keep import errors attached to their source batch.")
        };

        public static int MonitoredRelationshipCount => ReportSpecs.Length;

        public RelationshipReferenceContext GetSite(int siteId) => QueryContext("SELECT SiteID AS Id, ClientID AS ClientId, CAST(NULL AS INT) AS SiteId FROM dbo.ClientSites WHERE SiteID=@id", siteId);
        public RelationshipReferenceContext GetContract(int contractId) => QueryContext("SELECT ContractID AS Id, ClientID AS ClientId, SiteID AS SiteId FROM dbo.AMCContracts WHERE ContractID=@id", contractId);
        public RelationshipReferenceContext GetInvoice(int invoiceId) => QueryContext("SELECT InvoiceID AS Id, ClientID AS ClientId, SiteID AS SiteId FROM dbo.Invoices WHERE InvoiceID=@id", invoiceId);
        public RelationshipReferenceContext GetQuotation(int quotationId) => QueryContext("SELECT BidID AS Id, ClientID AS ClientId, SiteID AS SiteId FROM dbo.Quotations WHERE BidID=@id", quotationId);
        public RelationshipReferenceContext GetJob(int jobId) => QueryContext("SELECT JobID AS Id, ClientID AS ClientId, SiteID AS SiteId FROM dbo.Jobs WHERE JobID=@id", jobId);
        public bool EmployeeExists(int employeeId) => Exists("Employees", "EmployeeID", employeeId);
        public bool VendorExists(int vendorId) => Exists("Vendors", "VendorID", vendorId);
        public bool StockItemExists(int itemId) => Exists("StockItems", "ItemID", itemId);
        public void EnsureSafeRelationships() => _db.EnsureRelationshipIntegrity();

        public RelationshipHealthSnapshot GetHealthSnapshot()
        {
            var snapshot = new RelationshipHealthSnapshot();
            using (SqlConnection conn = _db.GetConnection())
            {
                conn.Open();
                foreach (RelationshipSpec spec in ReportSpecs)
                {
                    if (!TableAndColumnsExist(conn, spec))
                        continue;
                    RelationshipHealthRow row = ReadHealthRow(conn, spec);
                    row.EnforcementStatus = ReadForeignKeyStatus(conn, spec);
                    row.IsForeignKeyEnforced = row.EnforcementStatus == "Enforced" || row.EnforcementStatus == "Untrusted";
                    row.RequiresContextProtection = !string.IsNullOrWhiteSpace(spec.ChildClientColumn);
                    if (row.RequiresContextProtection)
                    {
                        string contextStatus = ReadContextForeignKeyStatus(conn, spec);
                        row.IsContextProtectionEnforced = contextStatus == "Enforced" || contextStatus == "Untrusted";
                        row.EnforcementStatus = row.IsContextProtectionEnforced
                            ? "Direct + ownership enforced"
                            : row.ContextViolations > 0 ? "Ownership blocked by data" : "Ownership pending";
                    }
                    if (!row.IsForeignKeyEnforced && row.OrphanKeys > 0)
                        row.EnforcementStatus = "Blocked by data";
                    row.Severity = row.OrphanKeys > 0 || row.ContextViolations > 0
                        ? "Critical"
                        : !row.IsFullyProtected || row.EnforcementStatus == "Untrusted" ? "Important" : "Healthy";
                    row.Recommendation = spec.Recommendation;
                    snapshot.Rows.Add(row);
                }
            }
            return snapshot;
        }

        private RelationshipReferenceContext QueryContext(string sql, int id)
        {
            using (SqlConnection conn = _db.GetConnection())
            {
                conn.Open();
                return conn.QuerySingleOrDefault<RelationshipReferenceContext>(sql, new { id });
            }
        }

        private bool Exists(string table, string column, int id)
        {
            ValidateIdentifier(table);
            ValidateIdentifier(column);
            using (SqlConnection conn = _db.GetConnection())
            {
                conn.Open();
                return conn.ExecuteScalar<int>("SELECT COUNT(1) FROM dbo.[" + table + "] WHERE [" + column + "]=@id", new { id }) > 0;
            }
        }

        private static RelationshipHealthRow ReadHealthRow(SqlConnection conn, RelationshipSpec spec)
        {
            string child = "dbo.[" + spec.ChildTable + "]";
            string parent = "dbo.[" + spec.ParentTable + "]";
            string childColumn = "[" + spec.ChildColumn + "]";
            string parentColumn = "[" + spec.ParentColumn + "]";
            string context = string.IsNullOrWhiteSpace(spec.ChildClientColumn)
                ? "0"
                : "COALESCE(SUM(CASE WHEN c." + childColumn + " IS NOT NULL AND p." + parentColumn + " IS NOT NULL AND c.[" + spec.ChildClientColumn + "]<>p.[" + spec.ParentClientColumn + "] THEN 1 ELSE 0 END),0)";
            string sql = @"
SELECT COUNT(*) AS ChildRows,
       COUNT(c." + childColumn + @") AS PopulatedKeys,
       COALESCE(SUM(CASE WHEN p." + parentColumn + @" IS NOT NULL THEN 1 ELSE 0 END),0) AS MatchedKeys,
       COALESCE(SUM(CASE WHEN c." + childColumn + @" IS NOT NULL AND p." + parentColumn + @" IS NULL THEN 1 ELSE 0 END),0) AS OrphanKeys,
       " + context + @" AS ContextViolations
FROM " + child + @" c
LEFT JOIN " + parent + @" p ON p." + parentColumn + "=c." + childColumn + ";";
            RelationshipHealthRow row = conn.QuerySingle<RelationshipHealthRow>(sql);
            row.Module = spec.Module;
            row.RelationshipName = spec.Label;
            row.ChildTable = spec.ChildTable;
            row.ChildColumn = spec.ChildColumn;
            row.ParentTable = spec.ParentTable;
            row.ParentColumn = spec.ParentColumn;
            return row;
        }

        private static string ReadForeignKeyStatus(SqlConnection conn, RelationshipSpec spec)
        {
            const string sql = @"
SELECT TOP 1 fk.is_disabled AS IsDisabled, fk.is_not_trusted AS IsNotTrusted
FROM sys.foreign_key_columns fkc
INNER JOIN sys.foreign_keys fk ON fk.object_id=fkc.constraint_object_id
WHERE fkc.parent_object_id=OBJECT_ID(@child)
  AND COL_NAME(fkc.parent_object_id,fkc.parent_column_id)=@childColumn
  AND fkc.referenced_object_id=OBJECT_ID(@parent)
  AND COL_NAME(fkc.referenced_object_id,fkc.referenced_column_id)=@parentColumn
ORDER BY fk.is_disabled, fk.is_not_trusted;";
            ForeignKeyState state = conn.QuerySingleOrDefault<ForeignKeyState>(sql, new { child = "dbo." + spec.ChildTable, spec.ChildColumn, parent = "dbo." + spec.ParentTable, spec.ParentColumn });
            if (state == null) return "Missing FK";
            if ((bool)state.IsDisabled) return "Disabled";
            return (bool)state.IsNotTrusted ? "Untrusted" : "Enforced";
        }

        private static string ReadContextForeignKeyStatus(SqlConnection conn, RelationshipSpec spec)
        {
            const string sql = @"
SELECT TOP 1 fk.is_disabled AS IsDisabled, fk.is_not_trusted AS IsNotTrusted
FROM sys.foreign_keys fk
WHERE fk.parent_object_id=OBJECT_ID(@child)
  AND fk.referenced_object_id=OBJECT_ID(@parent)
  AND (SELECT COUNT(*) FROM sys.foreign_key_columns allcols WHERE allcols.constraint_object_id=fk.object_id)=2
  AND EXISTS (
      SELECT 1 FROM sys.foreign_key_columns clientcol
      WHERE clientcol.constraint_object_id=fk.object_id
        AND COL_NAME(clientcol.parent_object_id,clientcol.parent_column_id)=@childClientColumn
        AND COL_NAME(clientcol.referenced_object_id,clientcol.referenced_column_id)=@parentClientColumn)
  AND EXISTS (
      SELECT 1 FROM sys.foreign_key_columns sitecol
      WHERE sitecol.constraint_object_id=fk.object_id
        AND COL_NAME(sitecol.parent_object_id,sitecol.parent_column_id)=@childColumn
        AND COL_NAME(sitecol.referenced_object_id,sitecol.referenced_column_id)=@parentColumn)
ORDER BY fk.is_disabled, fk.is_not_trusted;";
            ForeignKeyState state = conn.QuerySingleOrDefault<ForeignKeyState>(sql, new
            {
                child = "dbo." + spec.ChildTable,
                parent = "dbo." + spec.ParentTable,
                spec.ChildClientColumn,
                spec.ParentClientColumn,
                spec.ChildColumn,
                spec.ParentColumn
            });
            if (state == null) return "Missing FK";
            if (state.IsDisabled) return "Disabled";
            return state.IsNotTrusted ? "Untrusted" : "Enforced";
        }

        private static bool TableAndColumnsExist(SqlConnection conn, RelationshipSpec spec)
        {
            const string sql = @"
SELECT CASE WHEN OBJECT_ID(@child,'U') IS NOT NULL AND OBJECT_ID(@parent,'U') IS NOT NULL
              AND COL_LENGTH(@child,@childColumn) IS NOT NULL AND COL_LENGTH(@parent,@parentColumn) IS NOT NULL
              AND (@childClientColumn IS NULL OR COL_LENGTH(@child,@childClientColumn) IS NOT NULL)
              AND (@parentClientColumn IS NULL OR COL_LENGTH(@parent,@parentClientColumn) IS NOT NULL)
            THEN 1 ELSE 0 END;";
            return conn.ExecuteScalar<int>(sql, new
            {
                child = "dbo." + spec.ChildTable,
                parent = "dbo." + spec.ParentTable,
                spec.ChildColumn,
                spec.ParentColumn,
                childClientColumn = spec.ChildClientColumn,
                parentClientColumn = spec.ParentClientColumn
            }) == 1;
        }

        private static RelationshipSpec Spec(string module, string label, string childTable, string childColumn, string parentTable, string parentColumn, string recommendation, string childClientColumn = null, string parentClientColumn = null)
        {
            ValidateIdentifier(childTable);
            ValidateIdentifier(childColumn);
            ValidateIdentifier(parentTable);
            ValidateIdentifier(parentColumn);
            if (!string.IsNullOrWhiteSpace(childClientColumn)) ValidateIdentifier(childClientColumn);
            if (!string.IsNullOrWhiteSpace(parentClientColumn)) ValidateIdentifier(parentClientColumn);
            return new RelationshipSpec { Module = module, Label = label, ChildTable = childTable, ChildColumn = childColumn, ParentTable = parentTable, ParentColumn = parentColumn, ChildClientColumn = childClientColumn, ParentClientColumn = parentClientColumn, Recommendation = recommendation };
        }

        private static void ValidateIdentifier(string identifier)
        {
            if (string.IsNullOrWhiteSpace(identifier) || !SqlIdentifierPattern.IsMatch(identifier))
                throw new InvalidOperationException("Unsafe relationship schema identifier.");
        }
    }
}
