using System;
using System.Collections.Generic;
using System.Globalization;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using System.Web.Script.Serialization;
using HVAC_Pro_Desktop.Models;

namespace HVAC_Pro_Desktop.Services.Integrations
{
    public sealed class GstEinvoiceIntegrationService
    {
        private const string Provider = "GST e-Invoice";
        private readonly JavaScriptSerializer _json = new JavaScriptSerializer { MaxJsonLength = int.MaxValue };

        public bool IsEnabled => IntegrationConfig.GetBool("GstEinvoice", "Enabled", false);

        public GstEinvoicePayload BuildInvoicePayload(Invoice invoice, GstEinvoiceParty seller, GstEinvoiceParty buyer)
        {
            if (invoice == null)
                throw new ArgumentNullException(nameof(invoice));

            var payload = new GstEinvoicePayload
            {
                DocumentType = "INV",
                DocumentNumber = invoice.InvoiceNumber,
                DocumentDate = invoice.InvoiceDate,
                SupplyType = "B2B",
                GstMode = string.IsNullOrWhiteSpace(invoice.GSTMode) ? "IGST" : invoice.GSTMode,
                Seller = seller,
                Buyer = buyer,
                TotalTaxableAmount = invoice.SubTotal,
                TotalCgstAmount = invoice.CGSTAmount,
                TotalSgstAmount = invoice.SGSTAmount,
                TotalIgstAmount = invoice.IGSTAmount,
                TotalInvoiceAmount = invoice.TotalAmount
            };

            foreach (InvoiceLineItem item in invoice.LineItems ?? new List<InvoiceLineItem>())
            {
                payload.Items.Add(new GstEinvoiceLine
                {
                    Description = item.Description,
                    HsnCode = item.HSNCode,
                    Quantity = item.Quantity,
                    Unit = string.IsNullOrWhiteSpace(item.Unit) ? "NOS" : item.Unit,
                    UnitPrice = item.Rate,
                    GstRate = item.GSTPercent,
                    TaxableAmount = item.Amount,
                    CgstAmount = string.Equals(payload.GstMode, "CGST+SGST", StringComparison.OrdinalIgnoreCase) ? item.TaxAmount / 2m : 0m,
                    SgstAmount = string.Equals(payload.GstMode, "CGST+SGST", StringComparison.OrdinalIgnoreCase) ? item.TaxAmount / 2m : 0m,
                    IgstAmount = string.Equals(payload.GstMode, "IGST", StringComparison.OrdinalIgnoreCase) ? item.TaxAmount : 0m,
                    TotalAmount = item.Amount + item.TaxAmount
                });
            }

            if (payload.Items.Count == 0)
            {
                payload.Items.Add(new GstEinvoiceLine
                {
                    Description = string.IsNullOrWhiteSpace(invoice.Subject) ? "HVAC service" : invoice.Subject,
                    HsnCode = IntegrationConfig.Get("GstEinvoice", "DefaultHsnSac", "998719"),
                    Quantity = 1m,
                    Unit = "NOS",
                    UnitPrice = invoice.SubTotal,
                    GstRate = invoice.GSTPercent,
                    TaxableAmount = invoice.SubTotal,
                    CgstAmount = invoice.CGSTAmount,
                    SgstAmount = invoice.SGSTAmount,
                    IgstAmount = invoice.IGSTAmount,
                    TotalAmount = invoice.TotalAmount
                });
            }

            return payload;
        }

        public IntegrationOperationResult ValidatePayload(GstEinvoicePayload payload)
        {
            if (payload == null)
                return IntegrationOperationResult.Fail(Provider, "ValidatePayload", "Payload is required.");

            var errors = new List<string>();
            if (string.IsNullOrWhiteSpace(payload.DocumentNumber))
                errors.Add("Document number is required.");
            if (payload.DocumentDate == default(DateTime))
                errors.Add("Document date is required.");
            ValidateParty(payload.Seller, "Seller", errors);
            ValidateParty(payload.Buyer, "Buyer", errors);
            if (payload.Items == null || payload.Items.Count == 0)
                errors.Add("At least one line item is required.");

            if (payload.Items != null)
            {
                for (int i = 0; i < payload.Items.Count; i++)
                {
                    GstEinvoiceLine item = payload.Items[i];
                    if (string.IsNullOrWhiteSpace(item.HsnCode))
                        errors.Add("Line " + (i + 1).ToString(CultureInfo.InvariantCulture) + " HSN/SAC is required.");
                    if (item.Quantity <= 0)
                        errors.Add("Line " + (i + 1).ToString(CultureInfo.InvariantCulture) + " quantity must be greater than zero.");
                    if (item.TaxableAmount < 0)
                        errors.Add("Line " + (i + 1).ToString(CultureInfo.InvariantCulture) + " taxable amount cannot be negative.");
                }
            }

            if (errors.Count > 0)
                return IntegrationOperationResult.Fail(Provider, "ValidatePayload", string.Join(" ", errors));

            return IntegrationOperationResult.Ok(Provider, "ValidatePayload", "GST e-invoice payload is ready.");
        }

        public async Task<IntegrationOperationResult> SubmitAsync(GstEinvoicePayload payload, CancellationToken cancellationToken)
        {
            IntegrationOperationResult validation = ValidatePayload(payload);
            if (!validation.Success)
                return validation;

            if (!IsEnabled)
                return IntegrationOperationResult.Fail(Provider, "Submit", "GST e-invoice integration is disabled.");

            string endpoint = IntegrationConfig.Get("GstEinvoice", "EndpointUrl", string.Empty);
            if (string.IsNullOrWhiteSpace(endpoint))
                return IntegrationOperationResult.Fail(Provider, "Submit", "GST e-invoice endpoint URL is not configured.");

            try
            {
                using (var client = new HttpClient { Timeout = TimeSpan.FromSeconds(45) })
                using (var request = new HttpRequestMessage(HttpMethod.Post, endpoint))
                {
                    ApplyHeaders(client);
                    request.Content = new StringContent(_json.Serialize(payload), Encoding.UTF8, "application/json");

                    using (HttpResponseMessage response = await client.SendAsync(request, cancellationToken).ConfigureAwait(false))
                    {
                        string raw = await response.Content.ReadAsStringAsync().ConfigureAwait(false);
                        var result = response.IsSuccessStatusCode
                            ? IntegrationOperationResult.Ok(Provider, "Submit", "GST e-invoice request submitted.")
                            : IntegrationOperationResult.Fail(Provider, "Submit", "GST e-invoice endpoint returned HTTP " + (int)response.StatusCode + ".");
                        result.ReferenceId = payload.DocumentNumber;
                        result.RawResponse = raw;
                        return result;
                    }
                }
            }
            catch (Exception ex)
            {
                AppLogger.LogError("GstEinvoiceIntegrationService.SubmitAsync", ex);
                return IntegrationOperationResult.Fail(Provider, "Submit", ex.Message);
            }
        }

        private static void ApplyHeaders(HttpClient client)
        {
            string bearer = IntegrationConfig.Get("GstEinvoice", "BearerToken", string.Empty);
            if (!string.IsNullOrWhiteSpace(bearer))
                client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", bearer.Trim());

            AddHeader(client, "client_id", IntegrationConfig.Get("GstEinvoice", "ClientId", string.Empty));
            AddHeader(client, "client_secret", IntegrationConfig.Get("GstEinvoice", "ClientSecret", string.Empty));
            AddHeader(client, "gstin", IntegrationConfig.Get("GstEinvoice", "Gstin", string.Empty));
        }

        private static void AddHeader(HttpClient client, string name, string value)
        {
            if (!string.IsNullOrWhiteSpace(value))
                client.DefaultRequestHeaders.TryAddWithoutValidation(name, value.Trim());
        }

        private static void ValidateParty(GstEinvoiceParty party, string name, List<string> errors)
        {
            if (party == null)
            {
                errors.Add(name + " details are required.");
                return;
            }

            if (!IsValidGstin(party.Gstin))
                errors.Add(name + " GSTIN is invalid.");
            if (string.IsNullOrWhiteSpace(party.LegalName))
                errors.Add(name + " legal name is required.");
            if (string.IsNullOrWhiteSpace(party.StateCode))
                errors.Add(name + " state code is required.");
        }

        public static bool IsValidGstin(string gstin)
        {
            if (string.IsNullOrWhiteSpace(gstin))
                return false;

            return Regex.IsMatch(gstin.Trim().ToUpperInvariant(), @"^[0-9]{2}[A-Z]{5}[0-9]{4}[A-Z][1-9A-Z]Z[0-9A-Z]$");
        }
    }
}
