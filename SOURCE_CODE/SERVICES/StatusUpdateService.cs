using System;
using System.Collections.Generic;
using System.Data.SqlClient;
using System.Linq;
using HVAC_Pro_Desktop.DAL;
using HVAC_Pro_Desktop.Models;
using HVAC_Pro_Desktop.UI;

namespace HVAC_Pro_Desktop.Services
{
    public sealed class StatusUpdateResult
    {
        public bool Success { get; set; }
        public string Message { get; set; }
        public string DisplayValue { get; set; }
        public string StoredValue { get; set; }
    }

    public class StatusUpdateService
    {
        private readonly JobRepository _jobRepo = new JobRepository();
        private readonly InvoiceRepository _invoiceRepo = new InvoiceRepository();
        private readonly TenderService _tenderService = new TenderService();
        private readonly ServiceDeskService _serviceDeskService = new ServiceDeskService();
        private readonly EmployeeService _employeeService = new EmployeeService();
        private readonly EmployeeRepository _employeeRepo = new EmployeeRepository();
        private readonly ClientService _clientService = new ClientService();
        private readonly DatabaseManager _db = new DatabaseManager();

        public StatusUpdateResult UpdateStatus(StatusCellContext context, string requestedValue)
        {
            if (context == null)
                return Fail("Status context is missing.");

            StatusChoice choice = (context.Choices ?? new List<StatusChoice>())
                .FirstOrDefault(option => string.Equals(option.StoredValue, requestedValue, StringComparison.OrdinalIgnoreCase));
            if (choice == null)
                return Fail("This status is not allowed here.");

            try
            {
                switch ((context.ModuleKey ?? string.Empty).Trim())
                {
                    case "jobs":
                        return UpdateJobStatus(context, choice);
                    case "invoices":
                        return UpdateInvoiceStatus(context, choice);
                    case "quotations":
                        return UpdateQuotationStatus(context, choice);
                    case "service_desk":
                        return UpdateServiceDeskStatus(context, choice);
                    case "attendance":
                        return UpdateAttendanceStatus(context, choice);
                    case "employees":
                        return UpdateEmployeeStatus(context, choice);
                    case "clients":
                        return UpdateClientStatus(context, choice);
                    default:
                        return Fail("Status editing is not configured for this table.");
                }
            }
            catch (Exception ex)
            {
                AppLogger.LogError("StatusUpdateService.UpdateStatus", ex);
                return Fail(ex.Message);
            }
        }

        private StatusUpdateResult UpdateJobStatus(StatusCellContext context, StatusChoice choice)
        {
            int jobId = GetInt(context, "JobId");
            if (jobId <= 0)
                return Fail("Job record was not found.");

            Job job = _jobRepo.GetById(jobId);
            if (job == null)
                return Fail("Job record was not found.");

            string pipeline = choice.StoredValue;
            string legacyStatus = ResolveJobLegacyStatus(pipeline);
            DateTime? completedDate = string.Equals(pipeline, "Closed", StringComparison.OrdinalIgnoreCase) || string.Equals(pipeline, "Invoiced", StringComparison.OrdinalIgnoreCase)
                ? (job.CompletedDate ?? DateTime.Now)
                : (DateTime?)null;
            DateTime? closedDate = string.Equals(pipeline, "Closed", StringComparison.OrdinalIgnoreCase) || string.Equals(pipeline, "Invoiced", StringComparison.OrdinalIgnoreCase)
                ? (job.ClosedDate ?? DateTime.Now)
                : (DateTime?)null;

            _jobRepo.UpdatePipeline(jobId, pipeline, legacyStatus, completedDate, closedDate, job.InvoiceId);
            _jobRepo.LogActivity(jobId, "Status changed to " + choice.DisplayText + " by quick editor", SessionManager.CurrentUser?.DisplayName, "Info");
            AppDataCache.RemovePrefix("jobs:");
            return Success(choice);
        }

        private StatusUpdateResult UpdateInvoiceStatus(StatusCellContext context, StatusChoice choice)
        {
            int invoiceId = GetInt(context, "InvoiceId");
            if (invoiceId <= 0)
                return Fail("Invoice record was not found.");

            Invoice invoice = _invoiceRepo.GetById(invoiceId);
            if (invoice == null)
                return Fail("Invoice record was not found.");

            decimal paidAmount = invoice.PaidAmount;
            if (string.Equals(choice.StoredValue, "Paid", StringComparison.OrdinalIgnoreCase))
                paidAmount = invoice.TotalAmount;
            else if (string.Equals(choice.StoredValue, "Draft", StringComparison.OrdinalIgnoreCase) || string.Equals(choice.StoredValue, "Pending", StringComparison.OrdinalIgnoreCase) || string.Equals(choice.StoredValue, "Approved", StringComparison.OrdinalIgnoreCase))
                paidAmount = Math.Min(invoice.PaidAmount, invoice.TotalAmount);
            else if (string.Equals(choice.StoredValue, "Cancelled", StringComparison.OrdinalIgnoreCase))
                paidAmount = invoice.PaidAmount;

            _invoiceRepo.UpdatePaymentStatus(invoiceId, paidAmount, choice.StoredValue);
            AppDataCache.RemovePrefix("invoices:");
            return Success(choice);
        }

        private StatusUpdateResult UpdateQuotationStatus(StatusCellContext context, StatusChoice choice)
        {
            int bidId = GetInt(context, "BidId");
            if (bidId <= 0)
                return Fail("Quotation record was not found.");

            TenderBid bid = _tenderService.GetById(bidId);
            if (bid == null)
                return Fail("Quotation record was not found.");

            bid.Status = choice.StoredValue;
            _tenderService.Update(bid);
            AppDataCache.RemovePrefix("tenders:");
            return Success(choice);
        }

        private StatusUpdateResult UpdateServiceDeskStatus(StatusCellContext context, StatusChoice choice)
        {
            int incidentId = GetInt(context, "IncidentId");
            if (incidentId <= 0)
                return Fail("Service ticket was not found.");

            ServiceDeskDetail detail = _serviceDeskService.GetDetail(incidentId);
            if (detail?.Incident == null)
                return Fail("Service ticket was not found.");

            detail.Incident.Status = choice.StoredValue;
            _serviceDeskService.Save(detail.Incident);
            return Success(choice);
        }

        private StatusUpdateResult UpdateAttendanceStatus(StatusCellContext context, StatusChoice choice)
        {
            int attendanceId = GetInt(context, "AttendanceId");
            int employeeId = GetInt(context, "EmployeeId");
            DateTime attendanceDate = GetDate(context, "AttendanceDate");
            if (attendanceId <= 0 || employeeId <= 0 || attendanceDate == default(DateTime))
                return Fail("Attendance record was not found.");

            using (SqlConnection conn = _db.GetConnection())
            {
                conn.Open();
                using (SqlCommand cmd = new SqlCommand(@"
                    UPDATE EmployeeAttendance
                    SET Status = @status
                    WHERE AttendanceID = @attendanceId
                      AND EmployeeID = @employeeId
                      AND AttendanceDate = @attendanceDate", conn))
                {
                    cmd.Parameters.AddWithValue("@status", choice.StoredValue);
                    cmd.Parameters.AddWithValue("@attendanceId", attendanceId);
                    cmd.Parameters.AddWithValue("@employeeId", employeeId);
                    cmd.Parameters.AddWithValue("@attendanceDate", attendanceDate.Date);
                    if (cmd.ExecuteNonQuery() <= 0)
                        return Fail("Attendance record was not updated.");
                }
            }

            return Success(choice);
        }

        private StatusUpdateResult UpdateEmployeeStatus(StatusCellContext context, StatusChoice choice)
        {
            int employeeId = GetInt(context, "EmployeeId");
            if (employeeId <= 0 && context.RowData is Employee employeeRow)
                employeeId = employeeRow.EmployeeID;
            if (employeeId <= 0)
                return Fail("Employee record was not found.");

            Employee employee = _employeeRepo.GetById(employeeId);
            if (employee == null)
                return Fail("Employee record was not found.");

            employee.Status = choice.StoredValue;
            if (string.Equals(choice.StoredValue, "Inactive", StringComparison.OrdinalIgnoreCase) || string.Equals(choice.StoredValue, "Terminated", StringComparison.OrdinalIgnoreCase))
                employee.LastWorkingDay = employee.LastWorkingDay ?? DateTime.Today;
            _employeeService.Update(employee);
            return Success(choice);
        }

        private StatusUpdateResult UpdateClientStatus(StatusCellContext context, StatusChoice choice)
        {
            int clientId = GetInt(context, "ClientId");
            if (clientId <= 0)
                return Fail("Client record was not found.");

            _clientService.UpdateLifecycleStatus(clientId, choice.StoredValue);
            return Success(choice);
        }

        private static StatusUpdateResult Success(StatusChoice choice)
        {
            return new StatusUpdateResult
            {
                Success = true,
                Message = "Status updated.",
                DisplayValue = choice.DisplayText,
                StoredValue = choice.StoredValue
            };
        }

        private static StatusUpdateResult Fail(string message)
        {
            return new StatusUpdateResult
            {
                Success = false,
                Message = string.IsNullOrWhiteSpace(message) ? "Status update failed." : message
            };
        }

        private static int GetInt(StatusCellContext context, string key)
        {
            if (context == null || !context.Metadata.TryGetValue(key, out object value) || value == null)
                return 0;
            return value is int typed ? typed : int.TryParse(Convert.ToString(value), out int parsed) ? parsed : 0;
        }

        private static DateTime GetDate(StatusCellContext context, string key)
        {
            if (context == null || !context.Metadata.TryGetValue(key, out object value) || value == null)
                return default(DateTime);
            return value is DateTime typed ? typed : DateTime.TryParse(Convert.ToString(value), out DateTime parsed) ? parsed.Date : default(DateTime);
        }

        private static string ResolveJobLegacyStatus(string pipeline)
        {
            switch ((pipeline ?? string.Empty).Trim())
            {
                case "Created":
                case "Assigned":
                    return "Pending";
                case "InProgress":
                case "ChecklistDone":
                    return "In Progress";
                case "Closed":
                case "Invoiced":
                    return "Completed";
                default:
                    return "Pending";
            }
        }
    }
}
