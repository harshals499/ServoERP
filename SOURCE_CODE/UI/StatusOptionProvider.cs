using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Windows.Forms;
using HVAC_Pro_Desktop.Models;
using HVAC_Pro_Desktop.Services;

namespace HVAC_Pro_Desktop.UI
{
    public sealed class StatusChoice
    {
        public StatusChoice(string storedValue, string displayText, params string[] aliases)
        {
            StoredValue = storedValue ?? string.Empty;
            DisplayText = string.IsNullOrWhiteSpace(displayText) ? StoredValue : displayText;
            Aliases = (aliases ?? new string[0])
                .Where(alias => !string.IsNullOrWhiteSpace(alias))
                .ToList();
        }

        public string StoredValue { get; private set; }
        public string DisplayText { get; private set; }
        public List<string> Aliases { get; private set; }

        public bool Matches(string value)
        {
            if (string.IsNullOrWhiteSpace(value))
                return false;

            return string.Equals(value.Trim(), StoredValue, StringComparison.OrdinalIgnoreCase)
                || string.Equals(value.Trim(), DisplayText, StringComparison.OrdinalIgnoreCase)
                || Aliases.Any(alias => string.Equals(value.Trim(), alias, StringComparison.OrdinalIgnoreCase));
        }
    }

    public sealed class StatusColumnConfig
    {
        public string ModuleKey { get; set; }
        public string DisplayName { get; set; }
        public string StatusPropertyName { get; set; }
        public List<StatusChoice> Choices { get; set; } = new List<StatusChoice>();
    }

    public sealed class StatusCellContext
    {
        public string ModuleKey { get; set; }
        public string DisplayName { get; set; }
        public string StatusPropertyName { get; set; }
        public string CurrentDisplayValue { get; set; }
        public object RowData { get; set; }
        public DataGridView Grid { get; set; }
        public DataGridViewRow Row { get; set; }
        public DataGridViewColumn Column { get; set; }
        public List<StatusChoice> Choices { get; set; } = new List<StatusChoice>();
        public Dictionary<string, object> Metadata { get; private set; } = new Dictionary<string, object>(StringComparer.OrdinalIgnoreCase);
    }

    public static class StatusOptionProvider
    {
        private const string TooltipText = "Double-click to change status";

        private static readonly string[] StatusColumnTokens =
        {
            "status",
            "currentstatus",
            "jobstatus",
            "paymentstatus",
            "invoicestatus",
            "projectstatus",
            "pipelinestatus"
        };

        private static readonly StatusColumnConfig JobConfig = new StatusColumnConfig
        {
            ModuleKey = "jobs",
            DisplayName = "Job",
            StatusPropertyName = "Status",
            Choices = new List<StatusChoice>
            {
                new StatusChoice("Created", "Created", "New", "Pending"),
                new StatusChoice("Assigned", "Assigned"),
                new StatusChoice("InProgress", "In Progress", "InProgress"),
                new StatusChoice("ChecklistDone", "Checklist Done", "ChecklistDone"),
                new StatusChoice("Closed", "Closed", "Completed"),
                new StatusChoice("Invoiced", "Invoiced")
            }
        };

        private static readonly StatusColumnConfig InvoiceConfig = new StatusColumnConfig
        {
            ModuleKey = "invoices",
            DisplayName = "Invoice",
            StatusPropertyName = "PaymentStatus",
            Choices = new List<StatusChoice>
            {
                new StatusChoice("Draft", "Draft"),
                new StatusChoice("Pending", "Pending", "Sent", "Sent for Approval"),
                new StatusChoice("Approved", "Approved"),
                new StatusChoice("Partial", "Partially Paid", "Partial"),
                new StatusChoice("Paid", "Paid"),
                new StatusChoice("Overdue", "Overdue"),
                new StatusChoice("Cancelled", "Cancelled")
            }
        };

        private static readonly StatusColumnConfig QuotationConfig = new StatusColumnConfig
        {
            ModuleKey = "quotations",
            DisplayName = "Quotation",
            StatusPropertyName = "Status",
            Choices = new List<StatusChoice>
            {
                new StatusChoice("Draft", "Draft"),
                new StatusChoice("Analysed", "Analysed", "Material Check"),
                new StatusChoice("Sent", "Sent", "Submitted"),
                new StatusChoice("Won", "Won", "Accepted"),
                new StatusChoice("Lost", "Lost", "Rejected")
            }
        };

        private static readonly StatusColumnConfig ServiceDeskConfig = new StatusColumnConfig
        {
            ModuleKey = "service_desk",
            DisplayName = "Service Ticket",
            StatusPropertyName = "Status",
            Choices = new List<StatusChoice>
            {
                new StatusChoice("New", "New"),
                new StatusChoice("Assigned", "Assigned"),
                new StatusChoice("In Progress", "In Progress"),
                new StatusChoice("On Hold", "On Hold"),
                new StatusChoice("Resolved", "Resolved"),
                new StatusChoice("Closed", "Closed")
            }
        };

        private static readonly StatusColumnConfig EmployeeConfig = new StatusColumnConfig
        {
            ModuleKey = "employees",
            DisplayName = "Employee",
            StatusPropertyName = "Status",
            Choices = new List<StatusChoice>
            {
                new StatusChoice("Active", "Active"),
                new StatusChoice("Leave", "On Leave", "Leave"),
                new StatusChoice("Inactive", "Inactive"),
                new StatusChoice("Terminated", "Terminated")
            }
        };

        private static readonly StatusColumnConfig ClientConfig = new StatusColumnConfig
        {
            ModuleKey = "clients",
            DisplayName = "Client",
            StatusPropertyName = "RelationshipStage",
            Choices = new List<StatusChoice>
            {
                new StatusChoice("Active", "Active"),
                new StatusChoice("Prospect", "Prospect", "Lead"),
                new StatusChoice("On Hold", "On Hold", "Hold"),
                new StatusChoice("Inactive", "Inactive"),
                new StatusChoice("Blacklisted", "Blacklisted", "Blocked")
            }
        };

        private static readonly StatusColumnConfig AttendanceConfig = new StatusColumnConfig
        {
            ModuleKey = "attendance",
            DisplayName = "Attendance",
            StatusPropertyName = "Status",
            Choices = new List<StatusChoice>
            {
                new StatusChoice("Present", "Present"),
                new StatusChoice("Late", "Late"),
                new StatusChoice("HalfDay", "Half Day", "Half-Day"),
                new StatusChoice("Leave", "Leave"),
                new StatusChoice("Absent", "Absent"),
                new StatusChoice("Holiday", "Holiday"),
                new StatusChoice("WeekOff", "Week Off", "WeekOff")
            }
        };

        public static string EditableTooltip => TooltipText;

        public static bool IsStatusColumn(DataGridViewColumn column)
        {
            if (column == null || column is DataGridViewButtonColumn)
                return false;

            string normalized = Normalize(column.Name) + "|" + Normalize(column.HeaderText) + "|" + Normalize(column.DataPropertyName);
            if (normalized.Contains("statusdot"))
                return false;

            return StatusColumnTokens.Any(token => normalized.Contains(token));
        }

        public static bool TryGetContext(DataGridView grid, int rowIndex, int columnIndex, out StatusCellContext context)
        {
            context = null;
            if (grid == null || rowIndex < 0 || columnIndex < 0 || rowIndex >= grid.Rows.Count || columnIndex >= grid.Columns.Count)
                return false;

            DataGridViewColumn column = grid.Columns[columnIndex];
            if (!IsStatusColumn(column))
                return false;

            DataGridViewRow row = grid.Rows[rowIndex];
            if (row == null || row.IsNewRow)
                return false;

            object rowData = row.DataBoundItem;
            if (rowData is ServiceDeskIncident incident && incident.IncidentId > 0)
            {
                context = CreateContext(ServiceDeskConfig, grid, row, column, rowData);
                context.Metadata["IncidentId"] = incident.IncidentId;
                return true;
            }

            if (rowData is EmployeeJobSummaryDto employeeJob && employeeJob.JobID > 0)
            {
                context = CreateContext(JobConfig, grid, row, column, rowData);
                context.Metadata["JobId"] = employeeJob.JobID;
                return true;
            }

            if (rowData is EmployeeAttendanceDayDto attendance && attendance.AttendanceID > 0)
            {
                int employeeId = TryGetCurrentEmployeeId(grid);
                if (employeeId <= 0)
                    return false;

                context = CreateContext(AttendanceConfig, grid, row, column, rowData);
                context.Metadata["AttendanceId"] = attendance.AttendanceID;
                context.Metadata["EmployeeId"] = employeeId;
                context.Metadata["AttendanceDate"] = attendance.AttendanceDate.Date;
                return true;
            }

            if (rowData is InvoiceRecentRow invoiceRecent && invoiceRecent.InvoiceId > 0)
            {
                context = CreateContext(InvoiceConfig, grid, row, column, rowData);
                context.Metadata["InvoiceId"] = invoiceRecent.InvoiceId;
                return true;
            }

            Type ownerType = FindOwningControl(grid)?.GetType();
            if (ownerType != null && string.Equals(ownerType.Name, "ClientManagementForm", StringComparison.OrdinalIgnoreCase))
            {
                int clientId = TryGetHiddenInt(row, "ClientId");
                if (clientId > 0)
                {
                    context = CreateContext(ClientConfig, grid, row, column, rowData);
                    context.Metadata["ClientId"] = clientId;
                    return true;
                }
            }

            if (ownerType != null && string.Equals(ownerType.Name, "InvoiceForm", StringComparison.OrdinalIgnoreCase))
            {
                int invoiceId = TryGetHiddenInt(row, "InvoiceId");
                if (invoiceId > 0)
                {
                    context = CreateContext(InvoiceConfig, grid, row, column, rowData);
                    context.Metadata["InvoiceId"] = invoiceId;
                    return true;
                }
            }

            if (ownerType != null && string.Equals(ownerType.Name, "TenderBidForm", StringComparison.OrdinalIgnoreCase))
            {
                int bidId = TryGetHiddenInt(row, "BidId");
                if (bidId > 0)
                {
                    context = CreateContext(QuotationConfig, grid, row, column, rowData);
                    context.Metadata["BidId"] = bidId;
                    return true;
                }
            }

            return false;
        }

        private static StatusCellContext CreateContext(StatusColumnConfig config, DataGridView grid, DataGridViewRow row, DataGridViewColumn column, object rowData)
        {
            string currentValue = Convert.ToString(row.Cells[column.Index].Value);
            return new StatusCellContext
            {
                ModuleKey = config.ModuleKey,
                DisplayName = config.DisplayName,
                StatusPropertyName = config.StatusPropertyName,
                CurrentDisplayValue = currentValue ?? string.Empty,
                Grid = grid,
                Row = row,
                Column = column,
                RowData = rowData,
                Choices = config.Choices.Select(choice => new StatusChoice(choice.StoredValue, choice.DisplayText, choice.Aliases.ToArray())).ToList()
            };
        }

        private static int TryGetHiddenInt(DataGridViewRow row, string columnName)
        {
            if (row == null || row.DataGridView == null || string.IsNullOrWhiteSpace(columnName) || !row.DataGridView.Columns.Contains(columnName))
                return 0;

            return int.TryParse(Convert.ToString(row.Cells[columnName].Value), out int value) ? value : 0;
        }

        private static Control FindOwningControl(Control control)
        {
            Control current = control;
            while (current != null)
            {
                string typeName = current.GetType().Name;
                if (typeName == "ClientManagementForm" || typeName == "InvoiceForm" || typeName == "TenderBidForm" || typeName == "EmployeeForm" || typeName == "ServiceDeskForm")
                    return current;
                current = current.Parent;
            }
            return null;
        }

        private static int TryGetCurrentEmployeeId(Control control)
        {
            Control owner = FindOwningControl(control);
            if (owner == null || !string.Equals(owner.GetType().Name, "EmployeeForm", StringComparison.OrdinalIgnoreCase))
                return 0;

            FieldInfo field = owner.GetType().GetField("_currentEmployee", BindingFlags.Instance | BindingFlags.NonPublic);
            Employee employee = field?.GetValue(owner) as Employee;
            return employee?.EmployeeID ?? 0;
        }

        private static string Normalize(string value)
        {
            return (value ?? string.Empty).Trim().ToLowerInvariant().Replace(" ", string.Empty).Replace("_", string.Empty);
        }
    }
}
