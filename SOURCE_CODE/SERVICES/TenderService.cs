using System;
using System.Collections.Generic;
using System.Data;
using System.Data.SqlClient;
using System.Net;
using System.Linq;
using HVAC_Pro_Desktop.DAL;
using HVAC_Pro_Desktop.Models;
using HVAC_Pro_Desktop.Models.Validation;

namespace HVAC_Pro_Desktop.Services
{
    public partial class TenderService
    {
        private static readonly TimeSpan CacheTtl = TimeSpan.FromSeconds(30);
        private readonly DatabaseManager _db = new DatabaseManager();
        private readonly PurchaseRepository _purchaseRepo = new PurchaseRepository();
        private readonly InvoiceService _invoiceService = new InvoiceService();
        private readonly ClientRepository _clientRepo = new ClientRepository();
        private readonly SiteRepository _siteRepo = new SiteRepository();
        private readonly SettingsService _settingsSvc = new SettingsService();
        private readonly InventoryRepository _inventoryRepo = new InventoryRepository();

        public List<TenderBid> GetAll()
        {
            return AppDataCache.GetOrCreate("tenders:all", CacheTtl, LoadAllQuotes);
        }

        private List<TenderBid> LoadAllQuotes()
        {
            var list = new List<TenderBid>();
            using (SqlConnection conn = _db.GetConnection())
            {
                conn.Open();
                const string sql = @"
                    SELECT t.*, c.CompanyName AS ClientResolvedName, s.SiteName, v.VendorName
                    FROM TenderBids t
                    LEFT JOIN B2BClients c ON t.ClientID = c.ClientID
                    LEFT JOIN ClientSites s ON t.SiteID = s.SiteID
                    LEFT JOIN Vendors v ON t.RecommendedVendorID = v.VendorID
                    ORDER BY COALESCE(t.RequiredByDate, t.DueDate) DESC, t.BidID DESC";
                using (SqlCommand cmd = new SqlCommand(sql, conn))
                using (SqlDataReader r = cmd.ExecuteReader())
                    while (r.Read()) list.Add(Map(r));
            }
            return list;
        }

        public TenderBid GetById(int id)
        {
            using (SqlConnection conn = _db.GetConnection())
            {
                conn.Open();
                const string sql = @"
                    SELECT t.*, c.CompanyName AS ClientResolvedName, s.SiteName, v.VendorName
                    FROM TenderBids t
                    LEFT JOIN B2BClients c ON t.ClientID = c.ClientID
                    LEFT JOIN ClientSites s ON t.SiteID = s.SiteID
                    LEFT JOIN Vendors v ON t.RecommendedVendorID = v.VendorID
                    WHERE t.BidID = @id";
                using (SqlCommand cmd = new SqlCommand(sql, conn))
                {
                    cmd.Parameters.AddWithValue("@id", id);
                    using (SqlDataReader r = cmd.ExecuteReader())
                        return r.Read() ? Map(r) : null;
                }
            }
        }

        public int Create(TenderBid t)
        {
            SessionManager.DemandPermission("Quotations", "Create");
            if (string.IsNullOrWhiteSpace(t.QuotationNumber))
                throw new Exception("Quotation number is required.");
            ValidateQuotationForSave(t);
            if (SessionManager.IsLoggedIn)
            {
                t.CreatedByUserId = SessionManager.CurrentUser.UserId;
                t.CreatedByName = SessionManager.CurrentUser.DisplayName;
            }

            using (SqlConnection conn = _db.GetConnection())
            {
                conn.Open();
                const string sql = @"
                    INSERT INTO TenderBids
                    (QuotationNumber,TenderName,ClientID,SiteID,SystemCount,BidValue,DueDate,SubmittedDate,RequiredByDate,
                     Status,ClientName,RequirementCategory,ItemName,RequiredQuantity,Unit,InventoryAvailable,ShortfallQuantity,
                     EstimatedInternalRate,EstimatedSupplierRate,EstimatedInternalCost,EstimatedExternalCost,
                     RecommendedVendorID,ComparisonSummary,AnalysisStatus,Notes,CreatedByUserId,CreatedByName)
                    VALUES
                    (@quoteNo,@name,@clientId,@siteId,@sc,@bv,@due,@sub,@reqBy,
                     @st,@cl,@cat,@item,@qty,@unit,@available,@shortfall,
                     @internalRate,@supplierRate,@internalCost,@externalCost,
                     @vendorId,@compare,@analysis,@notes,@createdByUserId,@createdByName);
                    SELECT SCOPE_IDENTITY();";

                using (SqlCommand cmd = new SqlCommand(sql, conn))
                {
                    AddParams(cmd, t);
                    int id = Convert.ToInt32(cmd.ExecuteScalar());
                    AppDataCache.RemovePrefix("tenders:");
                    SessionManager.LogAction("CREATE", "Quotations", id, "Quotation saved");
                    return id;
                }
            }
        }

        public void Update(TenderBid t)
        {
            SessionManager.DemandPermission("Quotations", "Edit");
            ValidateQuotationForSave(t);
            if (SessionManager.IsLoggedIn)
            {
                t.ModifiedByUserId = SessionManager.CurrentUser.UserId;
                t.ModifiedByName = SessionManager.CurrentUser.DisplayName;
                t.ModifiedDate = DateTime.Now;
            }
            using (SqlConnection conn = _db.GetConnection())
            {
                conn.Open();
                const string sql = @"
                    UPDATE TenderBids SET
                        QuotationNumber=@quoteNo,
                        TenderName=@name,
                        ClientID=@clientId,
                        SiteID=@siteId,
                        SystemCount=@sc,
                        BidValue=@bv,
                        DueDate=@due,
                        SubmittedDate=@sub,
                        RequiredByDate=@reqBy,
                        Status=@st,
                        ClientName=@cl,
                        RequirementCategory=@cat,
                        ItemName=@item,
                        RequiredQuantity=@qty,
                        Unit=@unit,
                        InventoryAvailable=@available,
                        ShortfallQuantity=@shortfall,
                        EstimatedInternalRate=@internalRate,
                        EstimatedSupplierRate=@supplierRate,
                        EstimatedInternalCost=@internalCost,
                        EstimatedExternalCost=@externalCost,
                        RecommendedVendorID=@vendorId,
                        ComparisonSummary=@compare,
                        AnalysisStatus=@analysis,
                        Notes=@notes,
                        ModifiedByUserId=@modifiedByUserId,
                        ModifiedByName=@modifiedByName,
                        ModifiedDate=@modifiedDate
                    WHERE BidID=@id";
                using (SqlCommand cmd = new SqlCommand(sql, conn))
                {
                    cmd.Parameters.AddWithValue("@id", t.BidID);
                    AddParams(cmd, t);
                    cmd.ExecuteNonQuery();
                    AppDataCache.RemovePrefix("tenders:");
                    SessionManager.LogAction("EDIT", "Quotations", t.BidID, "Quotation saved");
                }
            }
        }

        public string GenerateQuotationNumber()
        {
            using (SqlConnection conn = _db.GetConnection())
            {
                conn.Open();
                string prefix = "QTN-" + DateTime.Now.ToString("yyyy-MM");
                using (SqlCommand cmd = new SqlCommand("SELECT COUNT(*) FROM TenderBids WHERE QuotationNumber LIKE @p", conn))
                {
                    cmd.Parameters.AddWithValue("@p", prefix + "-%");
                    int count = Convert.ToInt32(cmd.ExecuteScalar());
                    return $"{prefix}-{count + 1:D4}";
                }
            }
        }

        public TenderBid AnalyzeRequirement(TenderBid bid)
        {
            if (bid == null) throw new ArgumentNullException(nameof(bid));
            if (string.IsNullOrWhiteSpace(bid.ItemName)) throw new Exception("Item name is required for analysis.");
            if (bid.RequiredQuantity <= 0) throw new Exception("Required quantity must be greater than zero.");

            StockItem stock = null;
            var supplierOptions = new List<SupplierOption>();

            using (SqlConnection conn = _db.GetConnection())
            {
                conn.Open();

                using (SqlCommand stockCmd = new SqlCommand(@"
                    SELECT TOP 1 s.*, v.VendorName
                    FROM StockItems s
                    LEFT JOIN Vendors v ON s.VendorID = v.VendorID
                    WHERE s.ItemName LIKE @item
                    ORDER BY CASE WHEN s.ItemName = @exact THEN 0 ELSE 1 END, s.ItemName", conn))
                {
                    stockCmd.Parameters.AddWithValue("@item", "%" + bid.ItemName.Trim() + "%");
                    stockCmd.Parameters.AddWithValue("@exact", bid.ItemName.Trim());
                    using (SqlDataReader r = stockCmd.ExecuteReader())
                        if (r.Read()) stock = MapStock(r);
                }

                using (SqlCommand supplierCmd = new SqlCommand(@"
                    SELECT sip.*, v.VendorName, v.Phone, v.Email
                    FROM SupplierItemPrices sip
                    INNER JOIN Vendors v ON sip.VendorID = v.VendorID
                    WHERE sip.ItemName LIKE @item
                       OR (@category <> '' AND sip.Category = @category)
                    ORDER BY sip.Rate ASC, v.VendorName ASC", conn))
                {
                    supplierCmd.Parameters.AddWithValue("@item", "%" + bid.ItemName.Trim() + "%");
                    supplierCmd.Parameters.AddWithValue("@category", bid.RequirementCategory ?? "");
                    using (SqlDataReader r = supplierCmd.ExecuteReader())
                    {
                        while (r.Read())
                        {
                            supplierOptions.Add(new SupplierOption
                            {
                                VendorID = (int)r["VendorID"],
                                VendorName = r["VendorName"].ToString(),
                                Rate = (decimal)r["Rate"],
                                Unit = r["Unit"] == DBNull.Value ? (bid.Unit ?? "Nos") : r["Unit"].ToString(),
                                Source = r["Source"] == DBNull.Value ? "Supplier master" : r["Source"].ToString(),
                                Phone = r["Phone"] == DBNull.Value ? string.Empty : r["Phone"].ToString(),
                                Email = r["Email"] == DBNull.Value ? string.Empty : r["Email"].ToString()
                            });
                        }
                    }
                }

                if (supplierOptions.Count == 0)
                {
                    using (SqlCommand historyCmd = new SqlCommand(@"
                        SELECT TOP 8
                            p.VendorID,
                            v.VendorName,
                            v.Phone,
                            v.Email,
                            ISNULL(NULLIF(MAX(NULLIF(LTRIM(RTRIM(pli.Description)), '')), ''), @itemName) AS SourceItem,
                            MAX(CASE WHEN pli.Quantity > 0 THEN pli.Amount / NULLIF(pli.Quantity, 0) ELSE pli.Rate END) AS DerivedRate,
                            MAX(CASE WHEN pli.Quantity > 0 THEN pli.Quantity ELSE 0 END) AS LastQuantity,
                            MAX(p.PODate) AS LastPODate
                        FROM PurchaseLineItems pli
                        INNER JOIN PurchaseOrders p ON pli.POID = p.POID
                        INNER JOIN Vendors v ON p.VendorID = v.VendorID
                        WHERE pli.Description LIKE @item
                           OR (@category <> '' AND pli.Description LIKE '%' + @category + '%')
                        GROUP BY p.VendorID, v.VendorName, v.Phone, v.Email
                        HAVING MAX(CASE WHEN pli.Quantity > 0 THEN pli.Amount / NULLIF(pli.Quantity, 0) ELSE pli.Rate END) IS NOT NULL
                        ORDER BY MAX(CASE WHEN pli.Quantity > 0 THEN pli.Amount / NULLIF(pli.Quantity, 0) ELSE pli.Rate END) ASC, MAX(p.PODate) DESC", conn))
                    {
                        historyCmd.Parameters.AddWithValue("@item", "%" + bid.ItemName.Trim() + "%");
                        historyCmd.Parameters.AddWithValue("@category", bid.RequirementCategory ?? "");
                        historyCmd.Parameters.AddWithValue("@itemName", bid.ItemName.Trim());
                        using (SqlDataReader r = historyCmd.ExecuteReader())
                        {
                            while (r.Read())
                            {
                                decimal rate = r["DerivedRate"] == DBNull.Value ? 0m : Convert.ToDecimal(r["DerivedRate"]);
                                if (rate <= 0)
                                    continue;

                                supplierOptions.Add(new SupplierOption
                                {
                                    VendorID = (int)r["VendorID"],
                                    VendorName = r["VendorName"].ToString(),
                                    Rate = rate,
                                    Unit = string.IsNullOrWhiteSpace(stock?.Unit) ? (bid.Unit ?? "Nos") : stock.Unit,
                                    Source = "Purchase history",
                                    Phone = r["Phone"] == DBNull.Value ? string.Empty : r["Phone"].ToString(),
                                    Email = r["Email"] == DBNull.Value ? string.Empty : r["Email"].ToString()
                                });
                            }
                        }
                    }
                }
            }

            supplierOptions = supplierOptions
                .GroupBy(s => s.VendorID)
                .Select(g => g.OrderBy(x => x.Rate).First())
                .OrderBy(x => x.Rate)
                .ThenBy(x => x.VendorName)
                .ToList();

            decimal available = stock?.CurrentStock ?? 0m;
            decimal internalRate = stock?.LastPurchaseRate ?? 0m;
            decimal shortfall = Math.Max(0, bid.RequiredQuantity - available);
            decimal fulfilFromInventory = Math.Min(bid.RequiredQuantity, available);
            SupplierOption bestSupplier = supplierOptions.Count > 0 ? supplierOptions[0] : null;

            bid.Unit = string.IsNullOrWhiteSpace(bid.Unit) ? stock?.Unit ?? "Nos" : bid.Unit.Trim();
            bid.InventoryAvailable = available;
            bid.ShortfallQuantity = shortfall;
            bid.EstimatedInternalRate = internalRate;
            bid.EstimatedSupplierRate = bestSupplier?.Rate ?? 0m;
            bid.EstimatedInternalCost = Math.Round(fulfilFromInventory * internalRate, 2);
            bid.EstimatedExternalCost = Math.Round(shortfall * (bestSupplier?.Rate ?? 0m), 2);
            bid.RecommendedVendorID = bestSupplier?.VendorID;
            bid.RecommendedVendorName = bestSupplier?.VendorName;
            bid.AnalysisStatus = shortfall <= 0
                ? "Fully covered by inventory"
                : bestSupplier != null
                    ? "Shortfall requires supplier procurement"
                    : "Shortfall detected - no supplier price mapped";
            bid.ComparisonSummary =
                $"Inventory available: {available:N2} {bid.Unit}. " +
                $"Required: {bid.RequiredQuantity:N2} {bid.Unit}. " +
                $"Shortfall: {shortfall:N2} {bid.Unit}. " +
                (bestSupplier != null
                    ? $"Best supplier: {bestSupplier.VendorName} @ Rs {bestSupplier.Rate:N2}/{bestSupplier.Unit}."
                    : "No mapped supplier quote available.");
            bid.SupplierOptions = supplierOptions;

            if (bid.BidValue <= 0)
            {
                decimal baseCost = bid.EstimatedInternalCost + bid.EstimatedExternalCost;
                bid.BidValue = Math.Round(baseCost * 1.18m, 2);
            }

            return bid;
        }

        public PurchaseOrder CreatePurchaseOrderForShortfall(int bidId)
        {
            TenderBid bid = GetById(bidId);
            if (bid == null) throw new Exception("Quotation not found.");

            bid = AnalyzeRequirement(bid);
            if (bid.ShortfallQuantity <= 0)
                throw new Exception("No supplier order is needed. Inventory already covers this requirement.");
            if (!bid.RecommendedVendorID.HasValue)
                throw new Exception("No supplier recommendation is available for this item.");

            var po = new PurchaseOrder
            {
                VendorID = bid.RecommendedVendorID.Value,
                ClientID = bid.ClientID,
                SiteID = bid.SiteID,
                RecommendedByBidID = bid.BidID,
                PONumber = "PO-" + DateTime.Now.ToString("yyyyMMdd-HHmmss"),
                PODate = DateTime.Today,
                Status = "Pending",
                TotalAmount = Math.Round(bid.ShortfallQuantity * bid.EstimatedSupplierRate, 2),
                ComparisonNotes = bid.ComparisonSummary,
                Notes = "Auto-created from quotation analysis for " + (bid.SiteName ?? bid.ClientName)
            };
            po.LineItems.Add(new PurchaseLineItem
            {
                Description = bid.ItemName,
                Quantity = bid.ShortfallQuantity,
                Rate = bid.EstimatedSupplierRate,
                Amount = po.TotalAmount
            });

            po.POID = _purchaseRepo.Create(po);
            return _purchaseRepo.GetById(po.POID) ?? po;
        }

        public Invoice GenerateInvoiceForBid(int bidId)
        {
            TenderBid bid = GetById(bidId);
            if (bid == null) throw new Exception("Quotation not found.");
            if (bid.ClientID <= 0) throw new Exception("Quotation must be linked to a client before generating an invoice.");
            bid = AnalyzeRequirement(bid);

            var client = _clientRepo.GetById(bid.ClientID);
            var site = bid.SiteID > 0 ? _siteRepo.GetAll().FirstOrDefault(s => s.SiteID == bid.SiteID) : null;
            var stock = !string.IsNullOrWhiteSpace(bid.ItemName) ? _inventoryRepo.GetByName(bid.ItemName) : null;

            string subject = BuildInvoiceSubject(bid, site);
            string sendInvoiceTo = BuildSendInvoiceBlock(client, site);
            string certification = _settingsSvc.Get(
                "DefaultCertificationNote",
                "I/We hereby certify that this tax invoice is accounted for in the turnover of sales and the due tax, if any, has been paid or shall be paid.");

            var invoice = new Invoice
            {
                ClientID = bid.ClientID,
                SiteID = bid.SiteID,
                QuotationBidID = bid.BidID,
                ContractID = 0,
                InvoiceDate = DateTime.Today,
                DueDate = DateTime.Today.AddDays(30),
                PaymentStatus = "Draft",
                Notes = BuildInvoiceNotes(bid),
                GSTPercent = 18m,
                Subject = subject,
                SendInvoiceTo = sendInvoiceTo,
                CertificationNote = certification,
                InvoiceTitle = "TAX INVOICE"
            };
            invoice.LineItems.Add(new InvoiceLineItem
            {
                Description = string.IsNullOrWhiteSpace(bid.ItemName) ? (bid.TenderName ?? "Quotation Service") : bid.ItemName,
                HSNCode = GetHsnCodeForCategory(stock?.Category ?? bid.RequirementCategory),
                Unit = !string.IsNullOrWhiteSpace(stock?.Unit) ? stock.Unit : (bid.Unit ?? "Nos"),
                Quantity = bid.RequiredQuantity > 0 ? bid.RequiredQuantity : 1,
                Rate = bid.RequiredQuantity > 0 ? Math.Round(bid.BidValue / bid.RequiredQuantity, 2) : bid.BidValue,
                Amount = bid.BidValue
            });

            int invoiceId = _invoiceService.CreateInvoiceWithLineItems(invoice);
            return _invoiceService.GetInvoiceById(invoiceId);
        }

        public string BuildQuotationHtml(TenderBid bid)
        {
            if (bid == null) throw new ArgumentNullException(nameof(bid));

            var client = bid.ClientID > 0 ? _clientRepo.GetById(bid.ClientID) : null;
            var site = bid.SiteID > 0 ? _siteRepo.GetAll().Find(s => s.SiteID == bid.SiteID) : null;
            var settings = _settingsSvc.GetAll();

            string companyName = GetSetting(settings, "CompanyName", "New Client");
            string companyGst = GetSetting(settings, "CompanyGST", DocumentBranding.DefaultGstNumber);
            string companyPan = GetSetting(settings, "CompanyPAN", DocumentBranding.DefaultPanNumber);
            string companyAddress = GetSetting(settings, "CompanyAddress", "");
            string shopLicense = GetSetting(settings, "CompanyShopLicense", DocumentBranding.DefaultShopLicense);
            string pfNumber = GetSetting(settings, "CompanyPFNumber", DocumentBranding.DefaultPfNumber);
            string esicNumber = GetSetting(settings, "CompanyESICNumber", DocumentBranding.DefaultEsicNumber);
            string profTax = GetSetting(settings, "CompanyProfTax", GetSetting(settings, "CompanyProfessionalTax", DocumentBranding.DefaultProfTaxNumber));
            string msmeNumber = GetSetting(settings, "CompanyMSMENumber", DocumentBranding.DefaultMsmeNumber);

            decimal gst = Math.Round(bid.BidValue * 0.18m, 2);
            decimal total = bid.BidValue + gst;
            decimal quotedRate = bid.RequiredQuantity > 0 ? Math.Round(bid.BidValue / bid.RequiredQuantity, 2) : bid.BidValue;
            string siteLine = site == null ? "" : WebUtility.HtmlEncode(site.SiteName + (string.IsNullOrWhiteSpace(site.Address) ? "" : ", " + site.Address));

            string analysis = string.IsNullOrWhiteSpace(bid.ComparisonSummary)
                ? (string.IsNullOrWhiteSpace(bid.AnalysisStatus) ? "Analysis pending." : bid.AnalysisStatus)
                : bid.ComparisonSummary;

            string terms = string.IsNullOrWhiteSpace(bid.Notes)
                ? "1. Rates are subject to GST as applicable.\n2. Delivery is subject to stock and supplier availability.\n3. Approval of quotation authorises procurement for any analysed shortfall.\n4. Payment terms to follow client agreement."
                : bid.Notes;

            return @"<!DOCTYPE html>
<html><head><meta charset='utf-8'/>
<style>
body{font-family:'Segoe UI',sans-serif;color:#1f2937;margin:20px;}
.page{max-width:980px;margin:0 auto;}"
            + DocumentBranding.BuildOfficialHeaderCss()
            + @"
.title{text-align:center;font-size:22px;font-weight:700;letter-spacing:1px;margin-bottom:16px;}
.top{width:100%;border-collapse:collapse;margin-bottom:14px;}
.top td{vertical-align:top;padding:4px 6px;}
.right{text-align:right;}
.sub{font-size:13px;line-height:1.45;}
.box{border:1px solid #d1d5db;padding:10px 12px;margin-bottom:12px;}
.label{font-weight:700;}
table.items{width:100%;border-collapse:collapse;margin-top:8px;}
table.items th,table.items td{border:1px solid #d1d5db;padding:8px 6px;font-size:12px;}
table.items th{background:#f8fafc;text-align:left;}
.num{text-align:right;}
.summary{width:340px;margin-left:auto;margin-top:12px;border-collapse:collapse;}
.summary td{border:1px solid #d1d5db;padding:8px 10px;font-size:12px;}
.summary .head{background:#f8fafc;font-weight:700;}
.footer-grid{display:grid;grid-template-columns:1fr 1fr;gap:16px;margin-top:18px;}
.small{font-size:12px;line-height:1.45;}
.muted{color:#6b7280;}
.signature img{display:block;max-width:190px;max-height:70px;margin:8px auto 4px auto;object-fit:contain;}
.signature .blank-space{display:block;height:58px;}
pre{white-space:pre-wrap;margin:0;font-family:'Segoe UI',sans-serif;}
</style></head><body><div class='page'>"
            + DocumentBranding.BuildOfficialHeaderHtml()
            + new DocumentTemplateRenderer().BuildTemplateBannerHtml(CompanyDocumentTemplateType.Quotation)
            + "<div class='title'>QUOTATION</div>"
            + "<table class='top'><tr><td style='width:58%'><div class='sub'><span class='label'>To,</span><br/>"
            + Html(client?.CompanyName ?? bid.ClientName) + "<br/>"
            + Html(client?.BillingAddress) + "<br/>"
            + Html(siteLine)
            + "</div></td><td class='right sub'><div><span class='label'>Date :</span> " + DateTime.Today.ToString("dd/MM/yyyy") + "</div>"
            + "<div><span class='label'>Quotation No.</span> " + Html(bid.QuotationNumber) + "</div>"
            + "<div><span class='label'>Status :</span> " + Html(bid.Status ?? "Draft") + "</div>"
            + "<div><span class='label'>Due Date :</span> " + bid.DueDate.ToString("dd/MM/yyyy") + "</div>"
            + "<div><span class='label'>Required By :</span> " + (bid.RequiredByDate.HasValue ? bid.RequiredByDate.Value.ToString("dd/MM/yyyy") : "-") + "</div></td></tr></table>"
            + "<div class='box'><span class='label'>Sub :</span> " + Html(bid.TenderName) + "<br/>"
            + "<span class='label'>Requirement Category :</span> " + Html(bid.RequirementCategory) + "</div>"
            + "<table class='items'><thead><tr><th style='width:50px'>Sr No.</th><th>Description</th><th style='width:90px'>Unit</th><th style='width:80px'>Qty</th><th style='width:120px'>Rate (Rs.)</th><th style='width:130px'>Amount (Rs.)</th></tr></thead><tbody>"
            + "<tr><td>1</td><td>" + Html(bid.ItemName) + "</td><td>" + Html(bid.Unit) + "</td><td class='num'>" + bid.RequiredQuantity.ToString("N2") + "</td><td class='num'>" + quotedRate.ToString("N2") + "</td><td class='num'>" + bid.BidValue.ToString("N2") + "</td></tr>"
            + "</tbody></table>"
            + "<table class='summary'>"
            + "<tr><td class='head'>Quoted Amount</td><td class='num'>" + bid.BidValue.ToString("N2") + "</td></tr>"
            + "<tr><td>Indicative GST @ 18%</td><td class='num'>" + gst.ToString("N2") + "</td></tr>"
            + "<tr><td class='head'>Grand Total Amount</td><td class='num'><strong>" + total.ToString("N2") + "</strong></td></tr>"
            + "</table>"
            + "<div class='footer-grid'><div class='box'><span class='label'>Procurement and Cost Analysis</span><pre>" + Html(analysis) + "</pre></div>"
            + "<div class='box'><span class='label'>Commercial Notes</span><pre>" + Html(terms) + "</pre></div></div>"
            + "<div class='footer-grid'><div class='small'><div class='label'>Compliance Details</div>"
            + DocumentBranding.BuildComplianceBlockHtml(shopLicense, pfNumber, esicNumber, profTax, companyPan, companyGst, msmeNumber, false) + "</div>"
            + "<div class='small signature'>" + DocumentBranding.BuildSignatureHtml(companyName) + "</div></div>"
            + "<div class='box small'>" + DocumentBranding.BuildCertificationTextHtml() + "</div>"
            + "<div class='small muted'>" + Html(companyName) + (string.IsNullOrWhiteSpace(companyAddress) ? "" : " | " + Html(companyAddress)) + "</div>"
            + "</div></body></html>";
        }

        public string BuildQuotationComparison(TenderBid bid)
        {
            var checks = new List<string>();
            checks.Add(Check("Quotation number present", !string.IsNullOrWhiteSpace(bid.QuotationNumber)));
            checks.Add(Check("Client selected", bid.ClientID > 0));
            checks.Add(Check("Site selected", bid.SiteID > 0));
            checks.Add(Check("Project / quotation title present", !string.IsNullOrWhiteSpace(bid.TenderName)));
            checks.Add(Check("Item selected", !string.IsNullOrWhiteSpace(bid.ItemName)));
            checks.Add(Check("Quantity entered", bid.RequiredQuantity > 0));
            checks.Add(Check("Quoted amount entered", bid.BidValue > 0));
            checks.Add(Check("Due date entered", bid.DueDate != default(DateTime)));
            checks.Add(Check("Analysis completed", !string.IsNullOrWhiteSpace(bid.AnalysisStatus) || !string.IsNullOrWhiteSpace(bid.ComparisonSummary)));
            checks.Add(Check("Supplier recommendation available when shortfall exists", bid.ShortfallQuantity <= 0 || bid.RecommendedVendorID.HasValue));
            return string.Join(Environment.NewLine, checks);
        }

        public string GetBehavioralNudges(TenderBid bid)
        {
            var nudges = new List<string>();
            if (bid == null) return string.Empty;

            decimal totalCost = bid.EstimatedInternalCost + bid.EstimatedExternalCost;
            decimal margin = bid.BidValue - totalCost;
            decimal marginPct = bid.BidValue > 0 ? margin / bid.BidValue : 0;

            if (bid.BidValue <= 0)
                nudges.Add("Set a quoted value before sending. Customers respond better to clean, immediate pricing.");
            else if (marginPct < 0.15m)
                nudges.Add("You are underpricing this job. Margin is below 15%.");
            else if (marginPct > 0.35m)
                nudges.Add("Strong margin. Customer is still likely to accept if delivery urgency is clear.");

            if (bid.ShortfallQuantity > 0 && bid.RecommendedVendorID.HasValue)
                nudges.Add("Inventory shortfall detected. Best supplier is ready for quick PO conversion.");
            else if (bid.ShortfallQuantity > 0)
                nudges.Add("Shortfall exists but no supplier is mapped yet. Compare vendors before sending the quote.");

            if (!string.IsNullOrWhiteSpace(bid.RequirementCategory) && bid.RequirementCategory.Contains("Filters"))
                nudges.Add("Upsell idea: offer AMC or quarterly preventive replacement along with this quote.");

            if (bid.RequiredByDate.HasValue && bid.RequiredByDate.Value <= DateTime.Today.AddDays(3))
                nudges.Add("Urgent requirement. A faster response increases acceptance probability.");

            if (bid.BidValue > 0 && marginPct >= 0.18m && bid.RequiredByDate.HasValue)
                nudges.Add("Customer likely to accept this quote if sent today with delivery commitment.");

            return nudges.Count == 0 ? "Quote looks commercially sound." : string.Join(Environment.NewLine, nudges);
        }

        private void AddParams(SqlCommand cmd, TenderBid t)
        {
            cmd.Parameters.AddWithValue("@quoteNo", (object)t.QuotationNumber ?? DBNull.Value);
            cmd.Parameters.AddWithValue("@name", t.TenderName ?? "");
            cmd.Parameters.AddWithValue("@clientId", t.ClientID > 0 ? (object)t.ClientID : DBNull.Value);
            cmd.Parameters.AddWithValue("@siteId", t.SiteID > 0 ? (object)t.SiteID : DBNull.Value);
            cmd.Parameters.AddWithValue("@sc", t.SystemCount);
            cmd.Parameters.AddWithValue("@bv", t.BidValue);
            cmd.Parameters.AddWithValue("@due", t.DueDate == default ? DateTime.Today.AddDays(7) : t.DueDate);
            cmd.Parameters.AddWithValue("@sub", t.SubmittedDate.HasValue ? (object)t.SubmittedDate.Value : DBNull.Value);
            cmd.Parameters.AddWithValue("@reqBy", t.RequiredByDate.HasValue ? (object)t.RequiredByDate.Value : DBNull.Value);
            cmd.Parameters.AddWithValue("@st", t.Status ?? "Draft");
            cmd.Parameters.AddWithValue("@cl", t.ClientName ?? "");
            cmd.Parameters.AddWithValue("@cat", (object)t.RequirementCategory ?? DBNull.Value);
            cmd.Parameters.AddWithValue("@item", (object)t.ItemName ?? DBNull.Value);
            cmd.Parameters.AddWithValue("@qty", t.RequiredQuantity);
            cmd.Parameters.AddWithValue("@unit", (object)t.Unit ?? DBNull.Value);
            cmd.Parameters.AddWithValue("@available", t.InventoryAvailable);
            cmd.Parameters.AddWithValue("@shortfall", t.ShortfallQuantity);
            cmd.Parameters.AddWithValue("@internalRate", t.EstimatedInternalRate);
            cmd.Parameters.AddWithValue("@supplierRate", t.EstimatedSupplierRate);
            cmd.Parameters.AddWithValue("@internalCost", t.EstimatedInternalCost);
            cmd.Parameters.AddWithValue("@externalCost", t.EstimatedExternalCost);
            cmd.Parameters.AddWithValue("@vendorId", t.RecommendedVendorID.HasValue ? (object)t.RecommendedVendorID.Value : DBNull.Value);
            cmd.Parameters.AddWithValue("@compare", (object)t.ComparisonSummary ?? DBNull.Value);
            cmd.Parameters.AddWithValue("@analysis", (object)t.AnalysisStatus ?? DBNull.Value);
            cmd.Parameters.AddWithValue("@notes", t.Notes ?? "");
            if (cmd.CommandText.IndexOf("@createdByUserId", StringComparison.OrdinalIgnoreCase) >= 0)
                cmd.Parameters.AddWithValue("@createdByUserId", t.CreatedByUserId.HasValue ? (object)t.CreatedByUserId.Value : DBNull.Value);
            if (cmd.CommandText.IndexOf("@createdByName", StringComparison.OrdinalIgnoreCase) >= 0)
                cmd.Parameters.AddWithValue("@createdByName", string.IsNullOrWhiteSpace(t.CreatedByName) ? (object)DBNull.Value : t.CreatedByName);
            if (cmd.CommandText.IndexOf("@modifiedByUserId", StringComparison.OrdinalIgnoreCase) >= 0)
                cmd.Parameters.AddWithValue("@modifiedByUserId", t.ModifiedByUserId.HasValue ? (object)t.ModifiedByUserId.Value : DBNull.Value);
            if (cmd.CommandText.IndexOf("@modifiedByName", StringComparison.OrdinalIgnoreCase) >= 0)
                cmd.Parameters.AddWithValue("@modifiedByName", string.IsNullOrWhiteSpace(t.ModifiedByName) ? (object)DBNull.Value : t.ModifiedByName);
            if (cmd.CommandText.IndexOf("@modifiedDate", StringComparison.OrdinalIgnoreCase) >= 0)
                cmd.Parameters.AddWithValue("@modifiedDate", t.ModifiedDate.HasValue ? (object)t.ModifiedDate.Value : DBNull.Value);
        }

        private static TenderBid Map(SqlDataReader r)
        {
            return new TenderBid
            {
                BidID = (int)r["BidID"],
                QuotationNumber = r["QuotationNumber"] as string,
                TenderName = r["TenderName"] as string,
                ClientID = r["ClientID"] == DBNull.Value ? 0 : (int)r["ClientID"],
                SiteID = r["SiteID"] == DBNull.Value ? 0 : (int)r["SiteID"],
                SystemCount = r["SystemCount"] == DBNull.Value ? 0 : (int)r["SystemCount"],
                BidValue = r["BidValue"] == DBNull.Value ? 0 : (decimal)r["BidValue"],
                DueDate = r["DueDate"] == DBNull.Value ? DateTime.Today : (DateTime)r["DueDate"],
                SubmittedDate = r["SubmittedDate"] == DBNull.Value ? (DateTime?)null : (DateTime)r["SubmittedDate"],
                RequiredByDate = r["RequiredByDate"] == DBNull.Value ? (DateTime?)null : (DateTime)r["RequiredByDate"],
                Status = r["Status"] as string,
                ClientName = !string.IsNullOrWhiteSpace(r["ClientResolvedName"] as string) ? r["ClientResolvedName"].ToString() : r["ClientName"] as string,
                SiteName = r["SiteName"] as string,
                RequirementCategory = r["RequirementCategory"] as string,
                ItemName = r["ItemName"] as string,
                RequiredQuantity = r["RequiredQuantity"] == DBNull.Value ? 0 : (decimal)r["RequiredQuantity"],
                Unit = r["Unit"] as string,
                InventoryAvailable = r["InventoryAvailable"] == DBNull.Value ? 0 : (decimal)r["InventoryAvailable"],
                ShortfallQuantity = r["ShortfallQuantity"] == DBNull.Value ? 0 : (decimal)r["ShortfallQuantity"],
                EstimatedInternalRate = r["EstimatedInternalRate"] == DBNull.Value ? 0 : (decimal)r["EstimatedInternalRate"],
                EstimatedSupplierRate = r["EstimatedSupplierRate"] == DBNull.Value ? 0 : (decimal)r["EstimatedSupplierRate"],
                EstimatedInternalCost = r["EstimatedInternalCost"] == DBNull.Value ? 0 : (decimal)r["EstimatedInternalCost"],
                EstimatedExternalCost = r["EstimatedExternalCost"] == DBNull.Value ? 0 : (decimal)r["EstimatedExternalCost"],
                RecommendedVendorID = r["RecommendedVendorID"] == DBNull.Value ? (int?)null : (int)r["RecommendedVendorID"],
                RecommendedVendorName = r["VendorName"] as string,
                ComparisonSummary = r["ComparisonSummary"] as string,
                AnalysisStatus = r["AnalysisStatus"] as string,
                Notes = r["Notes"] as string,
                CreatedByUserId = r["CreatedByUserId"] == DBNull.Value ? (int?)null : (int)r["CreatedByUserId"],
                CreatedByName = r["CreatedByName"] as string,
                ModifiedByUserId = r["ModifiedByUserId"] == DBNull.Value ? (int?)null : (int)r["ModifiedByUserId"],
                ModifiedByName = r["ModifiedByName"] as string,
                ModifiedDate = r["ModifiedDate"] == DBNull.Value ? (DateTime?)null : (DateTime)r["ModifiedDate"],
            };
        }

        private static StockItem MapStock(IDataRecord r)
        {
            return new StockItem
            {
                ItemID = (int)r["ItemID"],
                ItemName = r["ItemName"] as string,
                Category = r["Category"] as string,
                CurrentStock = (decimal)r["CurrentStock"],
                Unit = r["Unit"] as string,
                LastPurchaseRate = (decimal)r["LastPurchaseRate"],
                ReorderLevel = (decimal)r["ReorderLevel"],
                VendorID = r["VendorID"] == DBNull.Value ? (int?)null : (int)r["VendorID"],
                VendorName = r["VendorName"] as string,
                LastUpdated = r["LastUpdated"] == DBNull.Value ? default(DateTime) : (DateTime)r["LastUpdated"]
            };
        }

        private string Check(string label, bool passed)
        {
            return (passed ? "[OK] " : "[Missing] ") + label;
        }

        private static string BuildInvoiceSubject(TenderBid bid, ClientSite site)
        {
            string item = string.IsNullOrWhiteSpace(bid.ItemName) ? "HVAC supply / service" : bid.ItemName;
            string siteName = site == null || string.IsNullOrWhiteSpace(site.SiteName) ? bid.ClientName : site.SiteName;
            return string.IsNullOrWhiteSpace(bid.TenderName)
                ? "Subject: Supply / service of " + item + " at " + siteName
                : bid.TenderName;
        }

        private static string BuildSendInvoiceBlock(B2BClient client, ClientSite site)
        {
            var lines = new List<string>();
            if (client != null && !string.IsNullOrWhiteSpace(client.CompanyName))
                lines.Add(client.CompanyName);
            if (site != null && !string.IsNullOrWhiteSpace(site.SiteName))
                lines.Add(site.SiteName);
            if (!string.IsNullOrWhiteSpace(site?.Address))
                lines.Add(site.Address);
            else if (!string.IsNullOrWhiteSpace(client?.BillingAddress))
                lines.Add(client.BillingAddress);
            if (!string.IsNullOrWhiteSpace(client?.GSTNumber))
                lines.Add("GST No.: " + client.GSTNumber);
            return string.Join(Environment.NewLine, lines.Where(l => !string.IsNullOrWhiteSpace(l)));
        }

        private static string BuildInvoiceNotes(TenderBid bid)
        {
            var lines = new List<string>
            {
                "Generated from quotation " + (bid.QuotationNumber ?? ("Bid #" + bid.BidID))
            };

            if (bid.InventoryAvailable > 0)
                lines.Add("Inventory considered: " + bid.InventoryAvailable.ToString("N2") + " " + bid.Unit + " available.");
            if (bid.ShortfallQuantity > 0)
                lines.Add("Supplier-backed shortfall: " + bid.ShortfallQuantity.ToString("N2") + " " + bid.Unit + ".");
            if (!string.IsNullOrWhiteSpace(bid.ComparisonSummary))
                lines.Add(bid.ComparisonSummary);

            return string.Join(Environment.NewLine, lines);
        }

        private static string GetHsnCodeForCategory(string category)
        {
            string key = (category ?? string.Empty).Trim().ToLowerInvariant();
            if (key.Contains("compressor")) return "84143000";
            if (key.Contains("refrigerant")) return "38276300";
            if (key.Contains("filter")) return "84213990";
            if (key.Contains("electrical")) return "85322990";
            if (key.Contains("copper")) return "74111000";
            if (key.Contains("belt")) return "40103999";
            if (key.Contains("valve")) return "84818030";
            if (key.Contains("tool")) return "82055990";
            return "998719";
        }

        private static string GetSetting(Dictionary<string, string> settings, string key, string fallback)
        {
            return settings.ContainsKey(key) && !string.IsNullOrWhiteSpace(settings[key]) ? settings[key] : fallback;
        }

        private static string Html(string value)
        {
            return WebUtility.HtmlEncode(value ?? string.Empty).Replace(Environment.NewLine, "<br/>");
        }

        private void ValidateQuotationForSave(TenderBid bid)
        {
            ValidationResult result = _businessRules.ValidateQuotation(bid);
            result.Merge(_calculationVerifier.VerifyQuotation(bid));
            if (bid != null && !string.IsNullOrWhiteSpace(bid.QuotationNumber))
            {
                bool duplicateNumber = GetAll().Any(existing =>
                    existing.BidID != bid.BidID &&
                    string.Equals((existing.QuotationNumber ?? string.Empty).Trim(), bid.QuotationNumber.Trim(), StringComparison.OrdinalIgnoreCase));
                if (duplicateNumber)
                    result.Add(ValidationSeverity.Error, "Quotations", "QuotationNumber", "Another quotation already uses this quotation number.", "Open the existing quotation or generate a new quotation number.");
            }
            _validation.EnsureValid(result, "Quotation validation failed");
        }
    }
}
