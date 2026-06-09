using System;
using System.Collections.Generic;
using System.Data;
using System.Data.SqlClient;
using System.Drawing;
using System.Globalization;
using System.Linq;
using System.Net;
using System.Text;
using Dapper;
using HVAC_Pro_Desktop.Models;
using HVAC_Pro_Desktop.Models.Validation;
using HVAC_Pro_Desktop.Services.Audit;
using HVAC_Pro_Desktop.Services.Validation;

namespace HVAC_Pro_Desktop.Services
{
    public partial class TenderService
    {
        private readonly PurchaseService _purchaseService = new PurchaseService();
        private readonly InventoryService _inventoryService = new InventoryService();
        private readonly VendorService _vendorService = new VendorService();
        private readonly BusinessRuleEngine _businessRules = new BusinessRuleEngine();
        private readonly CalculationVerificationService _calculationVerifier = new CalculationVerificationService();
        private readonly GlobalValidationEngine _validation = new GlobalValidationEngine();
        private readonly AuditTrailService _audit = new AuditTrailService();

        public TenderBid GetByIdDetailed(int id)
        {
            using (SqlConnection conn = _db.GetConnection())
            {
                conn.Open();
                const string sql = @"
                    SELECT t.*, c.CompanyName AS ClientResolvedName, s.SiteName, v.VendorName
                    FROM Quotations t
                    LEFT JOIN B2BClients c ON t.ClientID = c.ClientID
                    LEFT JOIN ClientSites s ON t.SiteID = s.SiteID
                    LEFT JOIN Vendors v ON t.RecommendedVendorID = v.VendorID
                    WHERE t.BidID = @id";

                using (SqlCommand cmd = new SqlCommand(sql, conn))
                {
                    TenderBid bid = null;
                    cmd.Parameters.AddWithValue("@id", id);
                    using (SqlDataReader r = cmd.ExecuteReader())
                    {
                        if (!r.Read())
                            return null;

                        bid = MapDetailedBid(r);
                    }

                    bid.LineItems = LoadTenderLineItems(conn, bid.BidID);
                    bid.Suggestions = GenerateSuggestions(bid);
                    return bid;
                }
            }
        }

        public int SaveTenderBid(TenderBid bid)
        {
            PrepareDetailedBid(bid);
            ValidateQuotationForSave(bid);

            using (SqlConnection conn = _db.GetConnection())
            {
                conn.Open();
                using (SqlTransaction tx = conn.BeginTransaction())
                {
                    if (bid.BidID <= 0)
                    {
                        if (string.IsNullOrWhiteSpace(bid.QuotationNumber))
                            throw new Exception("Quotation number is required.");

                        const string insertSql = @"
                            INSERT INTO Quotations
                            (QuotationNumber,TenderName,ClientID,SiteID,SystemCount,BidValue,DueDate,SubmittedDate,RequiredByDate,
                             Status,ClientName,RequirementCategory,ItemName,RequiredQuantity,Unit,InventoryAvailable,ShortfallQuantity,
                             EstimatedInternalRate,EstimatedSupplierRate,EstimatedInternalCost,EstimatedExternalCost,
                             RecommendedVendorID,ComparisonSummary,AnalysisStatus,Notes,IsMultiLine,TotalTaxableValue,TotalGSTAmount,
                             TotalWithGST,AverageMarginPct,TemplateId,ClientPriceMemoryApplied,SuggestionsJson,CommercialFlow,CustomerDocumentStatus,SupplierDocumentStatus,FlowNotes)
                            VALUES
                            (@QuotationNumber,@TenderName,@ClientID,@SiteID,@SystemCount,@BidValue,@DueDate,@SubmittedDate,@RequiredByDate,
                             @Status,@ClientName,@RequirementCategory,@ItemName,@RequiredQuantity,@Unit,@InventoryAvailable,@ShortfallQuantity,
                             @EstimatedInternalRate,@EstimatedSupplierRate,@EstimatedInternalCost,@EstimatedExternalCost,
                             @RecommendedVendorID,@ComparisonSummary,@AnalysisStatus,@Notes,@IsMultiLine,@TotalTaxableValue,@TotalGSTAmount,
                             @TotalWithGST,@AverageMarginPct,@TemplateId,@ClientPriceMemoryApplied,@SuggestionsJson,@CommercialFlow,@CustomerDocumentStatus,@SupplierDocumentStatus,@FlowNotes);
                            SELECT CAST(SCOPE_IDENTITY() AS INT);";
                        using (SqlCommand cmd = new SqlCommand(insertSql, conn, tx))
                        {
                            AddDetailedBidParameters(cmd, bid);
                            bid.BidID = Convert.ToInt32(cmd.ExecuteScalar());
                        }
                    }
                    else
                    {
                        const string updateSql = @"
                            UPDATE Quotations SET
                                QuotationNumber=@QuotationNumber,
                                TenderName=@TenderName,
                                ClientID=@ClientID,
                                SiteID=@SiteID,
                                SystemCount=@SystemCount,
                                BidValue=@BidValue,
                                DueDate=@DueDate,
                                SubmittedDate=@SubmittedDate,
                                RequiredByDate=@RequiredByDate,
                                Status=@Status,
                                ClientName=@ClientName,
                                RequirementCategory=@RequirementCategory,
                                ItemName=@ItemName,
                                RequiredQuantity=@RequiredQuantity,
                                Unit=@Unit,
                                InventoryAvailable=@InventoryAvailable,
                                ShortfallQuantity=@ShortfallQuantity,
                                EstimatedInternalRate=@EstimatedInternalRate,
                                EstimatedSupplierRate=@EstimatedSupplierRate,
                                EstimatedInternalCost=@EstimatedInternalCost,
                                EstimatedExternalCost=@EstimatedExternalCost,
                                RecommendedVendorID=@RecommendedVendorID,
                                ComparisonSummary=@ComparisonSummary,
                                AnalysisStatus=@AnalysisStatus,
                                Notes=@Notes,
                                IsMultiLine=@IsMultiLine,
                                TotalTaxableValue=@TotalTaxableValue,
                                TotalGSTAmount=@TotalGSTAmount,
                                TotalWithGST=@TotalWithGST,
                                AverageMarginPct=@AverageMarginPct,
                                TemplateId=@TemplateId,
                                ClientPriceMemoryApplied=@ClientPriceMemoryApplied,
                                SuggestionsJson=@SuggestionsJson,
                                CommercialFlow=@CommercialFlow,
                                CustomerDocumentStatus=@CustomerDocumentStatus,
                                SupplierDocumentStatus=@SupplierDocumentStatus,
                                FlowNotes=@FlowNotes
                            WHERE BidID=@BidID";
                        using (SqlCommand cmd = new SqlCommand(updateSql, conn, tx))
                        {
                            cmd.Parameters.AddWithValue("@BidID", bid.BidID);
                            AddDetailedBidParameters(cmd, bid);
                            cmd.ExecuteNonQuery();
                        }
                    }

                    SaveTenderLineItems(conn, tx, bid);
                    tx.Commit();
                }
            }

            AppDataCache.RemovePrefix("tenders:");
            _audit.Record("SAVE", "Quotations", bid.BidID, "Quotation saved with data-quality validation");
            return bid.BidID;
        }

        public TenderBid AnalyseTenderDraft(TenderBid bid)
        {
            if (bid == null)
                throw new ArgumentNullException(nameof(bid));

            if (bid.LineItems == null)
                bid.LineItems = new List<TenderBidLineItem>();
            bid.LineItems = bid.LineItems
                .Where(line => line != null && !string.IsNullOrWhiteSpace(line.ItemDescription))
                .ToList();

            bid.ClientPriceMemoryApplied = false;
            for (int i = 0; i < bid.LineItems.Count; i++)
            {
                TenderBidLineItem line = bid.LineItems[i];
                line.SortOrder = i;
                try
                {
                    AnalyseTenderLineItem(bid, line);
                }
                catch (Exception ex)
                {
                    line.AnalysisStatus = "Failed";
                    line.AnalysisNotes = ex.Message;
                }
            }

            ApplyTenderTotals(bid);
            bid.Suggestions = GenerateSuggestions(bid);
            bid.SuggestionsJson = SerializeSuggestions(bid.Suggestions);
            return bid;
        }

        public TenderBidLineItem AnalyseTenderLineItem(TenderBid bid, TenderBidLineItem line)
        {
            if (line == null)
                throw new ArgumentNullException(nameof(line));
            if (string.IsNullOrWhiteSpace(line.ItemDescription))
                throw new Exception("Item description is required.");

            int? selectedSupplierId = line.BestSupplierId;
            string selectedSupplierName = line.BestSupplierName;
            line.ItemDescription = line.ItemDescription.Trim();
            line.Unit = string.IsNullOrWhiteSpace(line.Unit) ? "Nos" : line.Unit.Trim();
            line.Quantity = line.Quantity <= 0 ? 1m : line.Quantity;
            if (!line.InventoryItemId.HasValue)
            {
                line.Category = string.IsNullOrWhiteSpace(line.Category) ? InferCategory(line.ItemDescription) : line.Category.Trim();
                line.StockAvailable = 0m;
                line.Shortfall = line.Quantity;
                line.BestSupplierId = null;
                line.BestSupplierName = string.Empty;
                line.CostPerUnit = 0m;
                line.HsnSacCode = string.IsNullOrWhiteSpace(line.HsnSacCode) ? string.Empty : line.HsnSacCode;
                line.GSTRatePct = line.GSTRatePct <= 0m ? 18m : line.GSTRatePct;
                line.TaxableLineTotal = Math.Round(line.Quantity * line.SellPricePerUnit, 2);
                line.GSTAmount = Math.Round(line.TaxableLineTotal * (line.GSTRatePct / 100m), 2);
                line.MarginPct = 0m;
                line.AnalysisStatus = "Manual";
                line.AnalysisNotes = "Free-text item retained without inventory analysis.";
                line.ModifiedDate = DateTime.Now;
                return line;
            }

            StockItem stockItem = _inventoryService.GetById(line.InventoryItemId.Value);
            if (stockItem != null)
            {
                line.ItemDescription = stockItem.ItemName;
                line.Unit = string.IsNullOrWhiteSpace(stockItem.Unit) ? line.Unit : stockItem.Unit;
                line.Category = string.IsNullOrWhiteSpace(stockItem.Category) ? InferCategory(line.ItemDescription) : stockItem.Category;
            }
            else
            {
                line.InventoryItemId = null;
                line.Category = string.IsNullOrWhiteSpace(line.Category) ? InferCategory(line.ItemDescription) : line.Category.Trim();
            }

            try
            {
                line.StockAvailable = _inventoryService.GetStockByDescription(line.ItemDescription);
                line.Shortfall = Math.Max(0m, line.Quantity - line.StockAvailable);

                SupplierOption supplier = line.IsInternalLabour ? null : ResolveSelectedSupplier(line.ItemDescription, line.Category, selectedSupplierId) ?? _vendorService.GetBestSupplierForItem(line.ItemDescription, line.Quantity, line.Category);
                line.BestSupplierId = supplier?.VendorID;
                line.BestSupplierName = !string.IsNullOrWhiteSpace(selectedSupplierName) && selectedSupplierId.HasValue
                    ? selectedSupplierName
                    : supplier?.VendorName;
                line.CostPerUnit = supplier?.Rate ?? _inventoryService.GetLastPurchaseRate(line.ItemDescription);

                decimal suggestedPrice = RoundToNearestFive(line.CostPerUnit * (1m + (GetDefaultMarkupPct() / 100m)));
                ClientPriceMemoryEntry memory = bid != null && bid.ClientID > 0 ? GetAcceptedPriceMemory(bid.ClientID, line.ItemDescription) : null;
                if (memory != null)
                {
                    suggestedPrice = memory.LastQuotedPrice;
                    line.PriceMemoryApplied = true;
                    line.PriceMemoryDate = memory.LastQuoteDate;
                    if (bid != null)
                        bid.ClientPriceMemoryApplied = true;
                }
                else
                {
                    line.PriceMemoryApplied = false;
                    line.PriceMemoryDate = null;
                }

                line.SuggestedSellPrice = suggestedPrice;
                line.MinimumRecommendedPrice = line.CostPerUnit <= 0m ? 0m : RoundToNearestFive(line.CostPerUnit / 0.85m);
                if (!line.IsSellPriceManual || line.SellPricePerUnit <= 0m)
                    line.SellPricePerUnit = suggestedPrice;

                line.HsnSacCode = ResolveTenderHsnSac(line);
                line.GSTRatePct = line.GSTRatePct <= 0 ? 18m : line.GSTRatePct;
                line.TaxableLineTotal = Math.Round(line.Quantity * line.SellPricePerUnit, 2);
                line.GSTAmount = Math.Round(line.TaxableLineTotal * (line.GSTRatePct / 100m), 2);
                line.MarginPct = line.SellPricePerUnit <= 0m ? 0m : Math.Round(((line.SellPricePerUnit - line.CostPerUnit) / line.SellPricePerUnit) * 100m, 2);
                line.AnalysisStatus = "Analysed";
                line.AnalysisNotes = "Stock " + line.StockAvailable.ToString("0.###", CultureInfo.InvariantCulture)
                    + ", shortfall " + line.Shortfall.ToString("0.###", CultureInfo.InvariantCulture)
                    + ", supplier " + (line.BestSupplierName ?? "not mapped") + ".";
                line.ModifiedDate = DateTime.Now;

                IndiaComplianceLogger.Log("TenderAnalysis",
                    "Quotation=" + (bid?.QuotationNumber ?? "draft")
                    + " | Item=" + line.ItemDescription
                    + " | Supplier=" + (line.BestSupplierName ?? "None")
                    + " | Cost=" + line.CostPerUnit.ToString("0.00", CultureInfo.InvariantCulture)
                    + " | Sell=" + line.SellPricePerUnit.ToString("0.00", CultureInfo.InvariantCulture)
                    + " | Shortfall=" + line.Shortfall.ToString("0.###", CultureInfo.InvariantCulture));
            }
            catch (Exception ex)
            {
                line.AnalysisStatus = "Failed";
                line.AnalysisNotes = ex.Message;
                IndiaComplianceLogger.Log("TenderAnalysis", "FAILED | Item=" + line.ItemDescription + " | " + ex.Message);
                throw;
            }

            return line;
        }
        public TenderBid LoadTemplateIntoTender(int tenderId, int templateId)
        {
            TenderBid bid = GetByIdDetailed(tenderId);
            QuoteTemplate template = GetQuoteTemplates().FirstOrDefault(t => t.TemplateId == templateId);
            if (bid == null)
                throw new Exception("Quotation not found.");
            if (template == null)
                throw new Exception("Template not found.");

            bid.TemplateId = template.TemplateId;
            bid.IsMultiLine = true;
            bid.LineItems = template.Items
                .OrderBy(i => i.SortOrder)
                .Select((item, index) => new TenderBidLineItem
                {
                    SortOrder = index,
                    Category = item.Category,
                    ItemDescription = item.ItemDescription,
                    Quantity = item.DefaultQuantity <= 0 ? 1m : item.DefaultQuantity,
                    Unit = string.IsNullOrWhiteSpace(item.Unit) ? "Nos" : item.Unit,
                    HsnSacCode = item.HsnSacCode,
                    GSTRatePct = item.GSTRatePct <= 0 ? 18m : item.GSTRatePct,
                    IsInternalLabour = (item.Category ?? string.Empty).IndexOf("labour", StringComparison.OrdinalIgnoreCase) >= 0
                })
                .ToList();

            AnalyseTenderDraft(bid);
            bid.CommercialFlow = "Revenue + Procurement";
            bid.SupplierDocumentStatus = "PO Sent";
            bid.FlowNotes = BuildCommercialFlowSummary(bid);
            SaveTenderBid(bid);
            return GetByIdDetailed(bid.BidID);
        }

        private SupplierOption ResolveSelectedSupplier(string itemDescription, string category, int? vendorId)
        {
            if (!vendorId.HasValue || vendorId.Value <= 0)
                return null;

            SupplierOption option = _vendorService
                .GetSupplierOptions(itemDescription, category)
                .FirstOrDefault(o => o.VendorID == vendorId.Value);
            if (option != null)
                return option;

            Vendor vendor = _vendorService.GetById(vendorId.Value);
            if (vendor == null)
                return null;

            return new SupplierOption
            {
                VendorID = vendor.VendorID,
                VendorName = vendor.VendorName,
                Rate = _inventoryService.GetLastPurchaseRate(itemDescription),
                Unit = _inventoryService.GetByName(itemDescription)?.Unit ?? "Nos",
                Source = "Manual supplier selection",
                Phone = vendor.Phone,
                Email = vendor.Email
            };
        }

        public List<QuoteTemplate> GetQuoteTemplates()
        {
            var templates = new List<QuoteTemplate>();
            using (SqlConnection conn = _db.GetConnection())
            {
                conn.Open();
                using (SqlCommand cmd = new SqlCommand("SELECT * FROM QuoteTemplates WHERE IsActive = 1 ORDER BY TemplateName", conn))
                using (SqlDataReader r = cmd.ExecuteReader())
                {
                    while (r.Read())
                    {
                        templates.Add(new QuoteTemplate
                        {
                            TemplateId = Convert.ToInt32(r["TemplateId"]),
                            TemplateName = Convert.ToString(r["TemplateName"]),
                            Description = r["Description"] == DBNull.Value ? string.Empty : Convert.ToString(r["Description"]),
                            IsActive = Convert.ToBoolean(r["IsActive"]),
                            CreatedDate = Convert.ToDateTime(r["CreatedDate"])
                        });
                    }
                }

                foreach (QuoteTemplate template in templates)
                {
                    using (SqlCommand cmd = new SqlCommand("SELECT * FROM QuoteTemplateItems WHERE TemplateId = @id ORDER BY SortOrder, TemplateItemId", conn))
                    {
                        cmd.Parameters.AddWithValue("@id", template.TemplateId);
                        using (SqlDataReader r = cmd.ExecuteReader())
                        {
                            while (r.Read())
                            {
                                template.Items.Add(new QuoteTemplateItem
                                {
                                    TemplateItemId = Convert.ToInt32(r["TemplateItemId"]),
                                    TemplateId = Convert.ToInt32(r["TemplateId"]),
                                    SortOrder = Convert.ToInt32(r["SortOrder"]),
                                    Category = r["Category"] == DBNull.Value ? string.Empty : Convert.ToString(r["Category"]),
                                    ItemDescription = Convert.ToString(r["ItemDescription"]),
                                    DefaultQuantity = Convert.ToDecimal(r["DefaultQuantity"]),
                                    Unit = Convert.ToString(r["Unit"]),
                                    HsnSacCode = r["HsnSacCode"] == DBNull.Value ? string.Empty : Convert.ToString(r["HsnSacCode"]),
                                    GSTRatePct = Convert.ToDecimal(r["GSTRatePct"]),
                                    DefaultMarkupPct = r["DefaultMarkupPct"] == DBNull.Value ? (decimal?)null : Convert.ToDecimal(r["DefaultMarkupPct"])
                                });
                            }
                        }
                    }
                }
            }
            return templates;
        }

        public List<string> CreatePOsFromQuotation(int tenderId)
        {
            TenderBid bid = GetByIdDetailed(tenderId);
            if (bid == null)
                throw new Exception("Quotation not found.");

            AnalyseTenderDraft(bid);
            bid.CommercialFlow = string.IsNullOrWhiteSpace(bid.CommercialFlow) ? "Revenue" : bid.CommercialFlow;
            bid.CustomerDocumentStatus = "Invoice Created";
            bid.FlowNotes = BuildCommercialFlowSummary(bid);
            SaveTenderBid(bid);

            var poNumbers = new List<string>();
            foreach (IGrouping<int?, TenderBidLineItem> group in bid.LineItems.Where(li => li != null && li.BestSupplierId.HasValue && li.Shortfall > 0m).GroupBy(li => li.BestSupplierId))
            {
                PurchaseOrder po = _purchaseService.CreatePO(group.Key.Value, group.ToList(), bid);
                poNumbers.Add(po.PONumber);
            }

            return poNumbers;
        }

        public Invoice CreateInvoiceFromQuotation(int tenderId)
        {
            TenderBid bid = GetByIdDetailed(tenderId);
            if (bid == null)
                throw new Exception("Quotation not found.");

            AnalyseTenderDraft(bid);
            bid.CommercialFlow = string.IsNullOrWhiteSpace(bid.CommercialFlow) ? "Revenue" : bid.CommercialFlow;
            bid.CustomerDocumentStatus = "Job Created";
            bid.FlowNotes = BuildCommercialFlowSummary(bid);
            SaveTenderBid(bid);

            B2BClient client = _clientRepo.GetById(bid.ClientID);
            ClientSite site = bid.SiteID > 0 ? _siteRepo.GetAll().FirstOrDefault(s => s.SiteID == bid.SiteID) : null;
            string placeOfSupply = ResolveTenderClientState(client, site);
            bool intraState = string.Equals(NormalizeStateName(_settingsSvc.Get("CompanyState", "Maharashtra")), NormalizeStateName(placeOfSupply), StringComparison.OrdinalIgnoreCase);

            var invoice = new Invoice
            {
                ClientID = bid.ClientID,
                SiteID = bid.SiteID,
                QuotationBidID = bid.BidID,
                InvoiceDate = bid.SubmittedDate ?? DateTime.Today,
                DueDate = bid.DueDate,
                PaymentStatus = "Draft",
                InvoiceTitle = "TAX INVOICE",
                Subject = bid.TenderName,
                Notes = "Auto-generated from quotation " + bid.QuotationNumber + Environment.NewLine + (bid.Notes ?? string.Empty),
                SendInvoiceTo = BuildTenderInvoiceBlock(client, site),
                CertificationNote = _settingsSvc.Get("DefaultCertificationNote", "I/We hereby certify that this tax invoice is accounted for in the turnover of sales and the due tax, if any, has been paid or shall be paid."),
                GSTMode = intraState ? "CGST+SGST" : "IGST",
                PlaceOfSupply = placeOfSupply,
                PaymentTerms = _settingsSvc.Get("DefaultPaymentTerms", "30 Days")
            };

            foreach (TenderBidLineItem line in bid.LineItems)
            {
                invoice.LineItems.Add(new InvoiceLineItem
                {
                    Description = line.ItemDescription,
                    HSNCode = line.HsnSacCode,
                    Unit = line.Unit,
                    Quantity = line.Quantity,
                    Rate = line.SellPricePerUnit,
                    GSTPercent = line.GSTRatePct,
                    Amount = line.TaxableLineTotal,
                    TaxAmount = line.GSTAmount,
                    IsBillable = true
                });
            }

            int invoiceId = _invoiceService.CreateInvoiceWithLineItems(invoice);
            return _invoiceService.GetInvoiceById(invoiceId);
        }

        public Job CreateDispatchJobFromQuotation(int tenderId)
        {
            TenderBid bid = GetByIdDetailed(tenderId);
            if (bid == null)
                throw new Exception("Quotation not found.");
            if (bid.ClientID <= 0)
                throw new Exception("Quotation must be linked to a client before creating a job.");

            AnalyseTenderDraft(bid);
            SaveTenderBid(bid);

            string description = string.Join(Environment.NewLine, bid.LineItems
                .Where(li => !string.IsNullOrWhiteSpace(li.ItemDescription))
                .Select(li => "- " + li.ItemDescription + " x " + li.Quantity.ToString("0.##") + " " + (li.Unit ?? "Nos")));
            if (string.IsNullOrWhiteSpace(description))
                description = bid.Notes ?? string.Empty;

            Job job = new Job
            {
                ClientID = bid.ClientID,
                SiteID = bid.SiteID,
                Title = bid.TenderName,
                JobTitle = bid.TenderName,
                JobType = "Quotation Handoff",
                Priority = bid.DueDate.Date <= DateTime.Today.AddDays(2) ? "High" : "Medium",
                ScheduledDate = bid.RequiredByDate ?? bid.DueDate,
                PipelineStatus = "Created",
                Status = "Pending",
                QuotedRevenue = bid.BidValue,
                Revenue = bid.BidValue,
                Description = description,
                Notes = "Created from quotation " + bid.QuotationNumber + Environment.NewLine + (bid.Notes ?? string.Empty)
            };

            int jobId = _jobService.Create(job);
            return _jobService.GetById(jobId);
        }

        public void RecordClientPriceMemory(int tenderId, bool wasWon)
        {
            TenderBid bid = GetByIdDetailed(tenderId);
            if (bid == null || bid.ClientID <= 0)
                return;

            using (SqlConnection conn = _db.GetConnection())
            {
                conn.Open();
                foreach (TenderBidLineItem line in bid.LineItems.Where(li => li != null && !string.IsNullOrWhiteSpace(li.ItemDescription)))
                {
                    using (SqlCommand cmd = new SqlCommand(@"
                        IF EXISTS (SELECT 1 FROM ClientPriceMemory WHERE ClientId=@ClientId AND ItemDescription=@ItemDescription)
                            UPDATE ClientPriceMemory
                               SET LastQuotedPrice=@LastQuotedPrice, LastQuoteDate=@LastQuoteDate, WasAccepted=@WasAccepted, QuotationNumber=@QuotationNumber
                             WHERE ClientId=@ClientId AND ItemDescription=@ItemDescription;
                        ELSE
                            INSERT INTO ClientPriceMemory (ClientId, ItemDescription, LastQuotedPrice, LastQuoteDate, WasAccepted, QuotationNumber)
                            VALUES (@ClientId, @ItemDescription, @LastQuotedPrice, @LastQuoteDate, @WasAccepted, @QuotationNumber);", conn))
                    {
                        cmd.Parameters.AddWithValue("@ClientId", bid.ClientID);
                        cmd.Parameters.AddWithValue("@ItemDescription", line.ItemDescription.Trim());
                        cmd.Parameters.AddWithValue("@LastQuotedPrice", line.SellPricePerUnit);
                        cmd.Parameters.AddWithValue("@LastQuoteDate", bid.SubmittedDate ?? DateTime.Today);
                        cmd.Parameters.AddWithValue("@WasAccepted", wasWon);
                        cmd.Parameters.AddWithValue("@QuotationNumber", (object)(bid.QuotationNumber ?? string.Empty));
                        cmd.ExecuteNonQuery();
                    }
                }
            }
        }

        public Color GetQuoteExpiryColour(DateTime dueDate)
        {
            DateTime today = DateTime.Now.Date;
            if (today > dueDate.Date)
                return Color.FromArgb(252, 235, 235);

            double days = (dueDate.Date - today).TotalDays;
            if (days <= 15) return Color.FromArgb(252, 235, 235);
            if (days <= 30) return Color.FromArgb(250, 238, 218);
            if (days <= 60) return Color.FromArgb(250, 250, 210);
            return Color.FromArgb(234, 243, 222);
        }

        public List<TenderBid> GetRenewalAlerts()
        {
            DateTime today = DateTime.Today;
            return GetAll().Where(q => q.DueDate.Date >= today && q.DueDate.Date <= today.AddDays(30)).OrderBy(q => q.DueDate).ToList();
        }

        public List<string> GenerateSuggestions(TenderBid bid)
        {
            var suggestions = new List<string>();
            if (bid == null || bid.LineItems == null)
                return suggestions;

            List<TenderBidLineItem> lines = bid.LineItems.Where(li => li != null).ToList();

            foreach (IGrouping<int?, TenderBidLineItem> group in lines.Where(li => li.BestSupplierId.HasValue && li.Shortfall > 0m).GroupBy(li => li.BestSupplierId))
            {
                string supplierName = group.First().BestSupplierName ?? "Mapped supplier";
                string items = string.Join(", ", group.Select(li => li.ItemDescription).Distinct());
                suggestions.Add("PO ready: " + supplierName + " (" + items + "). Click Create PO to auto-generate.");
            }

            foreach (TenderBidLineItem line in lines.Where(li => li.Shortfall > 0m))
            {
                suggestions.Add("Shortfall: " + line.Shortfall.ToString("0.###", CultureInfo.InvariantCulture) + " " + line.Unit + " of " + line.ItemDescription
                    + " not in stock. " + (string.IsNullOrWhiteSpace(line.BestSupplierName) ? "Mapped supplier not found." : line.BestSupplierName + " can supply."));
            }

            foreach (TenderBidLineItem line in lines.Where(li => li.PriceMemoryApplied && li.PriceMemoryDate.HasValue))
            {
                suggestions.Add((bid.ClientName ?? "Client") + " last accepted " + IndiaFormatHelper.FormatCurrency(line.SellPricePerUnit) + "/" + line.Unit
                    + " for " + line.ItemDescription + " on " + IndiaFormatHelper.FormatDate(line.PriceMemoryDate.Value) + ".");
            }

            foreach (TenderBidLineItem line in lines.Where(li => li.MarginPct < 15m && li.SellPricePerUnit > 0m))
            {
                suggestions.Add("Warning: " + line.ItemDescription + " margin is " + line.MarginPct.ToString("0.##", CultureInfo.InvariantCulture)
                    + "% - below minimum 15%. Suggested: " + IndiaFormatHelper.FormatCurrency(line.MinimumRecommendedPrice) + ".");
            }

            return suggestions.Distinct().ToList();
        }

        public string BuildQuotationDocumentHtml(TenderBid bid)
        {
            if (bid == null)
                throw new ArgumentNullException(nameof(bid));

            bid.LineItems = bid.LineItems ?? new List<TenderBidLineItem>();
            ApplyTenderTotals(bid);
            bid.Suggestions = GenerateSuggestions(bid);

            B2BClient client = bid.ClientID > 0 ? _clientRepo.GetById(bid.ClientID) : null;
            ClientSite site = bid.SiteID > 0 ? _siteRepo.GetAll().FirstOrDefault(s => s.SiteID == bid.SiteID) : null;
            Dictionary<string, string> settings = _settingsSvc.GetAll();
            string companyState = GetTenderSetting(settings, "CompanyState", "Maharashtra");
            string companyName = NormalizeMseSetting(GetTenderSetting(settings, "CompanyName", DocumentBranding.DefaultCompanyName), DocumentBranding.DefaultCompanyName);
            string companyGstin = GetTenderSetting(settings, "CompanyGSTIN", GetTenderSetting(settings, "CompanyGST", DocumentBranding.DefaultGstNumber));
            string companyPan = GetTenderSetting(settings, "CompanyPAN", DocumentBranding.DefaultPanNumber);
            string shopLicense = GetTenderSetting(settings, "CompanyShopLicense", DocumentBranding.DefaultShopLicense);
            string pfNumber = GetTenderSetting(settings, "CompanyPFNumber", DocumentBranding.DefaultPfNumber);
            string esicNumber = GetTenderSetting(settings, "CompanyESICNumber", DocumentBranding.DefaultEsicNumber);
            string profTax = GetTenderSetting(settings, "CompanyProfTax", GetTenderSetting(settings, "CompanyProfessionalTax", DocumentBranding.DefaultProfTaxNumber));
            string msmeNumber = GetTenderSetting(settings, "CompanyMSMENumber", DocumentBranding.DefaultMsmeNumber);
            string placeOfSupply = ResolveTenderClientState(client, site);
            bool intraState = string.Equals(NormalizeStateName(companyState), NormalizeStateName(placeOfSupply), StringComparison.OrdinalIgnoreCase);
            decimal cgst = intraState ? Math.Round(bid.TotalGSTAmount / 2m, 2) : 0m;
            decimal sgst = intraState ? bid.TotalGSTAmount - cgst : 0m;
            decimal igst = intraState ? 0m : bid.TotalGSTAmount;
            int validityDays = 5;

            var rows = new StringBuilder();
            int sr = 1;
            bid.LineItems = bid.LineItems.Where(li => li != null && !string.IsNullOrWhiteSpace(li.ItemDescription)).ToList();
            int actualLineCount = bid.LineItems.Count;
            string itemRowClass = actualLineCount > 8 ? "item-row dense-item-row" : actualLineCount > 4 ? "item-row compact-item-row" : "item-row";
            foreach (TenderBidLineItem line in bid.LineItems)
            {
                rows.Append("<tr class='" + itemRowClass + "'>");
                rows.AppendFormat("<td class='center'>{0}</td>", sr++);
                rows.AppendFormat("<td class='desc'>{0}</td>", HtmlTender(line.ItemDescription));
                rows.AppendFormat("<td class='center'>{0}</td>", HtmlTender(line.HsnSacCode));
                rows.AppendFormat("<td class='center'>{0}</td>", HtmlTender(line.Unit));
                rows.AppendFormat("<td class='center'>{0}</td>", line.Quantity.ToString("0.###", CultureInfo.InvariantCulture));
                rows.AppendFormat("<td class='num'>{0}</td>", FormatTenderAmount(line.SellPricePerUnit));
                rows.AppendFormat("<td class='num'>{0}</td>", FormatTenderAmount(line.TaxableLineTotal));
                rows.Append("</tr>");
            }

            while (sr <= 2)
            {
                rows.Append("<tr class='item-row'>");
                rows.AppendFormat("<td class='center'>{0}</td>", sr++);
                rows.Append("<td class='desc'>&nbsp;</td><td class='center'>&nbsp;</td><td class='center'>&nbsp;</td><td class='center'>&nbsp;</td><td class='num'>&nbsp;</td><td class='num'>&nbsp;</td>");
                rows.Append("</tr>");
            }

            string taxRows = intraState
                ? "<tr><td class='total-label' colspan='6'>Add: CGST @ 9%</td><td class='total-value'>" + FormatTenderAmount(cgst) + "</td></tr><tr><td class='total-label' colspan='6'>Add: SGST @ 9%</td><td class='total-value'>" + FormatTenderAmount(sgst) + "</td></tr>"
                : "<tr><td class='total-label' colspan='6'>Add: IGST @ 18%</td><td class='total-value'>" + FormatTenderAmount(igst) + "</td></tr>";

            string amountWords = "Rupees :- " + ToTenderWords((long)Math.Round(bid.TotalWithGST)) + " Only.";
            string subject = string.IsNullOrWhiteSpace(bid.TenderName) ? "Quotation for HVAC supply / service work at your site." : bid.TenderName;
            string customerBlockHtml = BuildTenderCustomerBlock(client, site, bid.ClientName);
            string submittedDate = IndiaFormatHelper.FormatDate(bid.SubmittedDate ?? DateTime.Today);

            return "<!DOCTYPE html><html><head><meta charset='utf-8'/><style>"
            + DocumentBranding.BuildOfficialHeaderCss()
            + BuildMseQuotationCss()
            + "</style></head><body><div class='page'>"
            + DocumentBranding.BuildOfficialHeaderHtml()
            + "<div class='quote-frame'><div class='quote-title'>::Quotation ::</div>"
            + "<table class='quote-grid quote-head'><tr>"
            + "<td class='to-cell' rowspan='4'><div class='cell-title'>To,</div><div class='client-lines'>"
            + customerBlockHtml
            + "</div></td>"
            + "<td class='meta-row'><span>Quotation No</span><span>: " + HtmlTender(bid.QuotationNumber) + "</span></td></tr>"
            + "<tr><td class='meta-row'><span>Quotation Date</span><span>: " + HtmlTender(submittedDate) + "</span></td></tr>"
            + "<tr><td class='from-label'>From:</td></tr>"
            + "<tr><td class='from-cell'><strong>" + HtmlTender(companyName) + "</strong><br/>"
            + "Shop Lic.No&nbsp;&nbsp; : " + HtmlTender(shopLicense) + "<br/>"
            + "P.F.No.&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp; : " + HtmlTender(pfNumber) + "<br/>"
            + "ESIC Code No. : " + HtmlTender(esicNumber) + "<br/>"
            + "Prof. Tax No.&nbsp;&nbsp; : " + HtmlTender(profTax) + "<br/>"
            + "PAN CARD NO. : " + HtmlTender(companyPan) + "<br/>"
            + "GST NUMBER&nbsp;&nbsp;&nbsp;: " + HtmlTender(companyGstin) + "</td></tr>"
            + "</table>"
            + "<div class='subject-line'><strong>Sub:</strong> " + HtmlTender(subject) + "</div>"
            + "<table class='quote-grid items'><thead><tr><th style='width:48px'>Sr No.</th><th>Description</th><th style='width:84px'>HSN / SAC<br/>Code</th><th style='width:54px'>Unit</th><th style='width:46px'>Qty</th><th style='width:96px'>Rate<br/>(Rs.)</th><th style='width:112px'>Amount<br/>(Rs.)</th></tr></thead><tbody>"
            + rows
            + "<tr><td class='total-label' colspan='6'>Total Rs.</td><td class='total-value'>" + FormatTenderAmount(bid.TotalTaxableValue) + "</td></tr>"
            + taxRows
            + "<tr><td class='total-label grand' colspan='6'>Total Amount</td><td class='total-value grand'>" + FormatTenderAmount(bid.TotalWithGST) + "</td></tr>"
            + "<tr><td colspan='7' class='words'>" + HtmlTender(amountWords) + "</td></tr></tbody></table>"
            + "<table class='quote-grid terms'><tr><td class='terms-left'>"
            + "<div>&#8226; Quotation is Valid Upto " + validityDays + " days.</div>"
            + "<div>&#8226; If any Extra Work Required Charge We be Extra at<br/>Actual</div>"
            + "</td><td class='terms-right'></td></tr>"
            + "<tr><td class='comments'>Comments &amp; Special Instructions, if any.</td>"
            + "<td class='contact'>For any querries about this Quotation, please contact Mr. Santosh<br/>Sonawane on 9967604066 or at <strong>msentp.info@gmail.com</strong></td></tr>"
            + "</table></div></div></body></html>";
        }

        private static string BuildMseQuotationCss()
        {
            return @"
@page{size:A4;margin:12mm;}
body{font-family:'Times New Roman',serif;color:#000;margin:0;background:#fff;}
.page{width:100%;max-width:760px;margin:0 auto;background:#fff;}
.mse-official-header{margin:0 0 10px 0;padding:0;border-bottom:0;}
.mse-official-header-logo img{max-width:760px;width:100%;height:auto;}
.quote-frame{border:1px solid #000;margin-top:6px;}
.quote-title{text-align:center;font-size:17px;font-weight:700;line-height:1.1;border-bottom:1px solid #000;padding:4px 0 5px 0;}
.quote-grid{width:100%;border-collapse:collapse;table-layout:fixed;}
.quote-grid td,.quote-grid th{border:1px solid #000;padding:3px 5px;vertical-align:top;font-size:14px;line-height:1.16;}
.quote-head{border-left:0;border-right:0;}
.quote-head td{border-top:0;}
.quote-head tr td:first-child{border-left:0;}
.quote-head tr td:last-child{border-right:0;}
.to-cell{width:47%;height:142px;font-size:15px;}
.cell-title{font-weight:700;margin-bottom:8px;}
.client-lines{font-size:14px;line-height:1.25;min-height:108px;}
.meta-row{height:23px;font-weight:700;}
.meta-row span:first-child{display:inline-block;width:112px;}
.from-label{height:20px;font-weight:700;}
.from-cell{height:86px;font-size:13px;line-height:1.15;}
.subject-line{border-top:0;border-bottom:1px solid #000;padding:4px 6px;font-size:14px;line-height:1.2;min-height:20px;}
.items th{text-align:center;font-weight:700;font-size:13px;line-height:1.12;padding:4px 3px;}
.items td{font-size:13px;padding:4px 5px;}
.items .center{text-align:center;}
.items .num{text-align:right;}
.items .desc{line-height:1.2;}
.item-row td{height:56px;}
.compact-item-row td{height:32px;}
.dense-item-row td{height:24px;font-size:12px;}
.total-label{text-align:left;font-weight:700;font-size:14px;}
.total-value{text-align:right;font-weight:700;font-size:14px;}
.grand{font-size:15px;}
.words{font-weight:700;font-size:14px;line-height:1.25;height:24px;}
.terms td{height:58px;font-size:13px;line-height:1.22;}
.terms tr:first-child td{border-top:0;}
.terms-left{width:47%;}
.terms-right{width:53%;}
.comments{font-weight:700;height:38px;}
.contact{font-size:12px;line-height:1.22;}
@media screen{
body{background:#fff;}
.page{max-width:760px;}
.mse-official-header{margin-bottom:6px;}
.mse-official-header-logo img{max-width:760px;}
.quote-frame{margin-top:3px;}
.quote-title{font-size:16px;padding:3px 0;}
.quote-grid td,.quote-grid th{font-size:12px;line-height:1.1;padding:2px 4px;}
.to-cell{height:118px;font-size:13px;}
.client-lines{font-size:12px;min-height:82px;}
.meta-row{height:20px;}
.from-label{height:18px;}
.from-cell{height:76px;font-size:12px;line-height:1.08;}
.subject-line{font-size:12px;padding:3px 5px;min-height:16px;}
.items th{font-size:12px;padding:3px 2px;}
.items td{font-size:12px;padding:3px 4px;}
.item-row td{height:42px;}
.compact-item-row td{height:28px;}
.dense-item-row td{height:22px;font-size:11px;}
.total-label,.total-value{font-size:12px;}
.grand{font-size:13px;}
.words{font-size:12px;height:20px;}
.terms td{height:42px;font-size:12px;}
.comments{height:30px;}
.contact{font-size:11px;}
}
@media print{body{-webkit-print-color-adjust:exact;print-color-adjust:exact;}.page{max-width:none;}.quote-frame{break-inside:avoid;page-break-inside:avoid;}}
";
        }

        private static string FormatTenderAmount(decimal value)
        {
            return value == 0m ? string.Empty : value.ToString("N2");
        }

        private static string NormalizeMseSetting(string value, string fallback)
        {
            if (string.IsNullOrWhiteSpace(value) || string.Equals(value.Trim(), "New Client", StringComparison.OrdinalIgnoreCase))
                return fallback;

            return value.Trim();
        }

        private static string SerializeSuggestions(IEnumerable<string> values)
        {
            return "[" + string.Join(",", (values ?? Enumerable.Empty<string>()).Where(v => !string.IsNullOrWhiteSpace(v)).Select(v => "\"" + EscapeJson(v.Trim()) + "\"")) + "]";
        }

        private static string EscapeJson(string value)
        {
            return (value ?? string.Empty).Replace("\\", "\\\\").Replace("\"", "\\\"").Replace("\r", "\\r").Replace("\n", "\\n");
        }

        private void PrepareDetailedBid(TenderBid bid)
        {
            if (bid.ClientID <= 0) throw new Exception("Please select a client.");
            if (string.IsNullOrWhiteSpace(bid.TenderName)) throw new Exception("Quotation / project name is required.");
            bid.SubmittedDate = (bid.SubmittedDate ?? DateTime.Today).Date;
            bid.DueDate = bid.DueDate == default(DateTime) ? bid.SubmittedDate.Value.AddDays(30) : bid.DueDate.Date;
            bid.RequiredByDate = bid.RequiredByDate?.Date;
            bid.Status = string.IsNullOrWhiteSpace(bid.Status) ? "Draft" : bid.Status.Trim();
            bid.LineItems = (bid.LineItems ?? new List<TenderBidLineItem>()).Where(li => li != null && !string.IsNullOrWhiteSpace(li.ItemDescription)).ToList();
            if (bid.LineItems.Count == 0)
                throw new Exception("Add at least one line item.");
            bid.IsMultiLine = bid.LineItems.Count > 1 || bid.IsMultiLine;
            ApplyTenderTotals(bid);
            ValidateTenderForSave(bid);
            bid.Suggestions = GenerateSuggestions(bid);
            bid.SuggestionsJson = SerializeSuggestions(bid.Suggestions);
            PrepareCommercialFlow(bid);
        }

        private void ValidateTenderForSave(TenderBid bid)
        {
            ValidationResult result = _businessRules.ValidateQuotation(bid);
            result.Merge(_calculationVerifier.VerifyQuotation(bid));
            _validation.EnsureValid(result, "Quotation validation failed");
        }

        private void ApplyTenderTotals(TenderBid bid)
        {
            bid.LineItems = (bid.LineItems ?? new List<TenderBidLineItem>())
                .Where(li => li != null && !string.IsNullOrWhiteSpace(li.ItemDescription))
                .ToList();
            decimal taxable = 0m;
            decimal gst = 0m;
            decimal weightedMarginTotal = 0m;
            decimal weightedBase = 0m;
            bid.InventoryAvailable = 0m;
            bid.ShortfallQuantity = 0m;

            foreach (TenderBidLineItem line in bid.LineItems.Where(li => li != null && !string.IsNullOrWhiteSpace(li.ItemDescription)))
            {
                if (line == null)
                    continue;
                line.Category = string.IsNullOrWhiteSpace(line.Category) ? "Service" : line.Category.Trim();
                line.Unit = string.IsNullOrWhiteSpace(line.Unit) ? "Nos" : line.Unit.Trim();
                line.AnalysisStatus = string.IsNullOrWhiteSpace(line.AnalysisStatus) ? "Manual" : line.AnalysisStatus.Trim();
                line.Quantity = line.Quantity <= 0 ? 1m : line.Quantity;
                line.TaxableLineTotal = Math.Round(line.Quantity * line.SellPricePerUnit, 2);
                line.GSTAmount = Math.Round(line.TaxableLineTotal * (line.GSTRatePct / 100m), 2);
                line.MarginPct = line.SellPricePerUnit <= 0m ? 0m : Math.Round(((line.SellPricePerUnit - line.CostPerUnit) / line.SellPricePerUnit) * 100m, 2);
                taxable += line.TaxableLineTotal;
                gst += line.GSTAmount;
                weightedBase += line.TaxableLineTotal;
                weightedMarginTotal += line.TaxableLineTotal * line.MarginPct;
                bid.InventoryAvailable += line.StockAvailable;
                bid.ShortfallQuantity += line.Shortfall;
            }

            bid.TotalTaxableValue = Math.Round(taxable, 2);
            bid.TotalGSTAmount = Math.Round(gst, 2);
            bid.TotalWithGST = Math.Round(taxable + gst, 2);
            bid.AverageMarginPct = weightedBase <= 0m ? 0m : Math.Round(weightedMarginTotal / weightedBase, 2);
            bid.BidValue = bid.TotalTaxableValue;

            TenderBidLineItem first = bid.LineItems.FirstOrDefault();
            if (first != null)
            {
                bid.RequirementCategory = first.Category;
                bid.ItemName = bid.LineItems.Count == 1 ? first.ItemDescription : first.ItemDescription + " +" + (bid.LineItems.Count - 1) + " more";
                bid.RequiredQuantity = bid.LineItems.Count == 1 ? first.Quantity : bid.LineItems.Where(li => li != null).Sum(li => li.Quantity);
                bid.Unit = first.Unit;
                bid.RecommendedVendorID = first.BestSupplierId;
                bid.RecommendedVendorName = first.BestSupplierName;
            }
        }
        private List<TenderBidLineItem> LoadTenderLineItems(SqlConnection conn, int bidId)
        {
            var lines = new List<TenderBidLineItem>();
            using (SqlCommand cmd = new SqlCommand("SELECT * FROM QuotationLineItems WHERE TenderBidId=@id ORDER BY SortOrder, LineItemId", conn))
            {
                cmd.Parameters.AddWithValue("@id", bidId);
                using (SqlDataReader r = cmd.ExecuteReader())
                {
                    while (r.Read())
                    {
                        lines.Add(new TenderBidLineItem
                        {
                            LineItemId = Convert.ToInt32(r["LineItemId"]),
                            TenderBidId = Convert.ToInt32(r["TenderBidId"]),
                            SortOrder = Convert.ToInt32(r["SortOrder"]),
                            Category = r["Category"] == DBNull.Value ? string.Empty : Convert.ToString(r["Category"]),
                            InventoryItemId = r["InventoryItemId"] == DBNull.Value ? (int?)null : Convert.ToInt32(r["InventoryItemId"]),
                            ItemDescription = Convert.ToString(r["ItemDescription"]),
                            Quantity = Convert.ToDecimal(r["Quantity"]),
                            Unit = Convert.ToString(r["Unit"]),
                            HsnSacCode = r["HsnSacCode"] == DBNull.Value ? string.Empty : Convert.ToString(r["HsnSacCode"]),
                            GSTRatePct = Convert.ToDecimal(r["GSTRatePct"]),
                            BestSupplierId = r["BestSupplierId"] == DBNull.Value ? (int?)null : Convert.ToInt32(r["BestSupplierId"]),
                            BestSupplierName = r["BestSupplierName"] == DBNull.Value ? string.Empty : Convert.ToString(r["BestSupplierName"]),
                            CostPerUnit = Convert.ToDecimal(r["CostPerUnit"]),
                            SellPricePerUnit = Convert.ToDecimal(r["SellPricePerUnit"]),
                            TaxableLineTotal = Convert.ToDecimal(r["TaxableLineTotal"]),
                            GSTAmount = Convert.ToDecimal(r["GSTAmount"]),
                            MarginPct = Convert.ToDecimal(r["MarginPct"]),
                            StockAvailable = Convert.ToDecimal(r["StockAvailable"]),
                            Shortfall = Convert.ToDecimal(r["Shortfall"]),
                            IsInternalLabour = Convert.ToBoolean(r["IsInternalLabour"]),
                            AnalysisStatus = Convert.ToString(r["AnalysisStatus"]),
                            AnalysisNotes = r["AnalysisNotes"] == DBNull.Value ? string.Empty : Convert.ToString(r["AnalysisNotes"]),
                            IsSellPriceManual = r["IsSellPriceManual"] != DBNull.Value && Convert.ToBoolean(r["IsSellPriceManual"]),
                            CreatedDate = Convert.ToDateTime(r["CreatedDate"]),
                            ModifiedDate = Convert.ToDateTime(r["ModifiedDate"])
                        });
                    }
                }
            }
            return lines;
        }

        private void SaveTenderLineItems(SqlConnection conn, SqlTransaction tx, TenderBid bid)
        {
            conn.Execute("DELETE FROM QuotationLineItems WHERE TenderBidId=@id", new { id = bid.BidID }, tx);

            foreach (TenderBidLineItem line in (bid.LineItems ?? new List<TenderBidLineItem>())
                .Where(li => li != null && !string.IsNullOrWhiteSpace(li.ItemDescription))
                .OrderBy(li => li.SortOrder))
            {
                conn.Execute(@"
                    INSERT INTO QuotationLineItems
                    (TenderBidId,SortOrder,Category,InventoryItemId,ItemDescription,Quantity,Unit,HsnSacCode,GSTRatePct,
                     BestSupplierId,BestSupplierName,CostPerUnit,SellPricePerUnit,TaxableLineTotal,GSTAmount,MarginPct,
                     StockAvailable,Shortfall,IsInternalLabour,AnalysisStatus,AnalysisNotes,IsSellPriceManual,CreatedDate,ModifiedDate)
                    VALUES
                    (@TenderBidId,@SortOrder,@Category,@InventoryItemId,@ItemDescription,@Quantity,@Unit,@HsnSacCode,@GSTRatePct,
                     @BestSupplierId,@BestSupplierName,@CostPerUnit,@SellPricePerUnit,@TaxableLineTotal,@GSTAmount,@MarginPct,
                     @StockAvailable,@Shortfall,@IsInternalLabour,@AnalysisStatus,@AnalysisNotes,@IsSellPriceManual,@CreatedDate,@ModifiedDate)",
                    new
                {
                    TenderBidId = bid.BidID,
                    line.SortOrder,
                    Category = line.Category ?? string.Empty,
                    line.InventoryItemId,
                    ItemDescription = line.ItemDescription ?? string.Empty,
                    line.Quantity,
                    Unit = line.Unit ?? "Nos",
                    HsnSacCode = line.HsnSacCode ?? string.Empty,
                    line.GSTRatePct,
                    line.BestSupplierId,
                    BestSupplierName = line.BestSupplierName ?? string.Empty,
                    line.CostPerUnit,
                    line.SellPricePerUnit,
                    line.TaxableLineTotal,
                    line.GSTAmount,
                    line.MarginPct,
                    line.StockAvailable,
                    line.Shortfall,
                    line.IsInternalLabour,
                    AnalysisStatus = line.AnalysisStatus ?? "Pending",
                    AnalysisNotes = line.AnalysisNotes ?? string.Empty,
                    line.IsSellPriceManual,
                    CreatedDate = line.CreatedDate == default(DateTime) ? DateTime.Now : line.CreatedDate,
                    ModifiedDate = line.ModifiedDate == default(DateTime) ? DateTime.Now : line.ModifiedDate
                }, tx);
            }
        }

        private void AddDetailedBidParameters(SqlCommand cmd, TenderBid bid)
        {
            cmd.Parameters.AddWithValue("@QuotationNumber", (object)(bid.QuotationNumber ?? string.Empty));
            cmd.Parameters.AddWithValue("@TenderName", bid.TenderName ?? string.Empty);
            cmd.Parameters.AddWithValue("@ClientID", bid.ClientID);
            cmd.Parameters.AddWithValue("@SiteID", bid.SiteID > 0 ? (object)bid.SiteID : DBNull.Value);
            cmd.Parameters.AddWithValue("@SystemCount", bid.SystemCount);
            cmd.Parameters.AddWithValue("@BidValue", bid.BidValue);
            cmd.Parameters.AddWithValue("@DueDate", bid.DueDate);
            cmd.Parameters.AddWithValue("@SubmittedDate", (object)(bid.SubmittedDate ?? DateTime.Today));
            cmd.Parameters.AddWithValue("@RequiredByDate", bid.RequiredByDate.HasValue ? (object)bid.RequiredByDate.Value : DBNull.Value);
            cmd.Parameters.AddWithValue("@Status", bid.Status ?? "Draft");
            cmd.Parameters.AddWithValue("@ClientName", (object)(bid.ClientName ?? string.Empty));
            cmd.Parameters.AddWithValue("@RequirementCategory", (object)(bid.RequirementCategory ?? string.Empty));
            cmd.Parameters.AddWithValue("@ItemName", (object)(bid.ItemName ?? string.Empty));
            cmd.Parameters.AddWithValue("@RequiredQuantity", bid.RequiredQuantity);
            cmd.Parameters.AddWithValue("@Unit", (object)(bid.Unit ?? "Nos"));
            cmd.Parameters.AddWithValue("@InventoryAvailable", bid.InventoryAvailable);
            cmd.Parameters.AddWithValue("@ShortfallQuantity", bid.ShortfallQuantity);
            cmd.Parameters.AddWithValue("@EstimatedInternalRate", bid.EstimatedInternalRate);
            cmd.Parameters.AddWithValue("@EstimatedSupplierRate", bid.EstimatedSupplierRate);
            cmd.Parameters.AddWithValue("@EstimatedInternalCost", bid.EstimatedInternalCost);
            cmd.Parameters.AddWithValue("@EstimatedExternalCost", bid.EstimatedExternalCost);
            cmd.Parameters.AddWithValue("@RecommendedVendorID", bid.RecommendedVendorID.HasValue ? (object)bid.RecommendedVendorID.Value : DBNull.Value);
            cmd.Parameters.AddWithValue("@ComparisonSummary", (object)(bid.ComparisonSummary ?? string.Empty));
            cmd.Parameters.AddWithValue("@AnalysisStatus", (object)(bid.AnalysisStatus ?? string.Empty));
            cmd.Parameters.AddWithValue("@Notes", (object)(bid.Notes ?? string.Empty));
            cmd.Parameters.AddWithValue("@IsMultiLine", bid.IsMultiLine);
            cmd.Parameters.AddWithValue("@TotalTaxableValue", bid.TotalTaxableValue);
            cmd.Parameters.AddWithValue("@TotalGSTAmount", bid.TotalGSTAmount);
            cmd.Parameters.AddWithValue("@TotalWithGST", bid.TotalWithGST);
            cmd.Parameters.AddWithValue("@AverageMarginPct", bid.AverageMarginPct);
            cmd.Parameters.AddWithValue("@TemplateId", bid.TemplateId.HasValue ? (object)bid.TemplateId.Value : DBNull.Value);
            cmd.Parameters.AddWithValue("@ClientPriceMemoryApplied", bid.ClientPriceMemoryApplied);
            cmd.Parameters.AddWithValue("@SuggestionsJson", (object)(bid.SuggestionsJson ?? "[]"));
            PrepareCommercialFlow(bid);
            cmd.Parameters.AddWithValue("@CommercialFlow", (object)(bid.CommercialFlow ?? "Revenue"));
            cmd.Parameters.AddWithValue("@CustomerDocumentStatus", (object)(bid.CustomerDocumentStatus ?? "Quote Draft"));
            cmd.Parameters.AddWithValue("@SupplierDocumentStatus", (object)(bid.SupplierDocumentStatus ?? "Not Required"));
            cmd.Parameters.AddWithValue("@FlowNotes", string.IsNullOrWhiteSpace(bid.FlowNotes) ? (object)DBNull.Value : bid.FlowNotes);
        }

        private TenderBid MapDetailedBid(IDataRecord r)
        {
            TenderBid bid = Map((SqlDataReader)r);
            bid.IsMultiLine = r["IsMultiLine"] != DBNull.Value && Convert.ToBoolean(r["IsMultiLine"]);
            bid.TotalTaxableValue = r["TotalTaxableValue"] == DBNull.Value ? 0m : Convert.ToDecimal(r["TotalTaxableValue"]);
            bid.TotalGSTAmount = r["TotalGSTAmount"] == DBNull.Value ? 0m : Convert.ToDecimal(r["TotalGSTAmount"]);
            bid.TotalWithGST = r["TotalWithGST"] == DBNull.Value ? 0m : Convert.ToDecimal(r["TotalWithGST"]);
            bid.AverageMarginPct = r["AverageMarginPct"] == DBNull.Value ? 0m : Convert.ToDecimal(r["AverageMarginPct"]);
            bid.TemplateId = r["TemplateId"] == DBNull.Value ? (int?)null : Convert.ToInt32(r["TemplateId"]);
            bid.ClientPriceMemoryApplied = r["ClientPriceMemoryApplied"] != DBNull.Value && Convert.ToBoolean(r["ClientPriceMemoryApplied"]);
            bid.SuggestionsJson = r["SuggestionsJson"] == DBNull.Value ? string.Empty : Convert.ToString(r["SuggestionsJson"]);
            return bid;
        }

        private ClientPriceMemoryEntry GetAcceptedPriceMemory(int clientId, string itemDescription)
        {
            using (SqlConnection conn = _db.GetConnection())
            {
                conn.Open();
                using (SqlCommand cmd = new SqlCommand("SELECT TOP 1 * FROM ClientPriceMemory WHERE ClientId=@ClientId AND ItemDescription=@ItemDescription AND WasAccepted=1 ORDER BY LastQuoteDate DESC, MemoryId DESC", conn))
                {
                    cmd.Parameters.AddWithValue("@ClientId", clientId);
                    cmd.Parameters.AddWithValue("@ItemDescription", itemDescription.Trim());
                    using (SqlDataReader r = cmd.ExecuteReader())
                    {
                        if (!r.Read())
                            return null;

                        return new ClientPriceMemoryEntry
                        {
                            MemoryId = Convert.ToInt32(r["MemoryId"]),
                            ClientId = Convert.ToInt32(r["ClientId"]),
                            ItemDescription = Convert.ToString(r["ItemDescription"]),
                            LastQuotedPrice = Convert.ToDecimal(r["LastQuotedPrice"]),
                            LastQuoteDate = Convert.ToDateTime(r["LastQuoteDate"]),
                            WasAccepted = Convert.ToBoolean(r["WasAccepted"]),
                            QuotationNumber = r["QuotationNumber"] == DBNull.Value ? string.Empty : Convert.ToString(r["QuotationNumber"]),
                            CreatedDate = Convert.ToDateTime(r["CreatedDate"])
                        };
                    }
                }
            }
        }
        private decimal GetDefaultMarkupPct()
        {
            return decimal.TryParse(_settingsSvc.Get("DefaultMarkupPct", "25"), out decimal value) ? value : 25m;
        }

        private string GenerateQuotationNumber(DateTime quoteDate)
        {
            string prefix = "Q/" + IndiaFinancialYearHelper.GetFinancialYearCode(quoteDate) + "/";
            using (SqlConnection conn = _db.GetConnection())
            {
                conn.Open();
                using (SqlCommand cmd = new SqlCommand("SELECT COUNT(*) FROM Quotations WHERE QuotationNumber LIKE @p", conn))
                {
                    cmd.Parameters.AddWithValue("@p", prefix + "%");
                    int count = Convert.ToInt32(cmd.ExecuteScalar());
                    return prefix + (count + 1).ToString("D3");
                }
            }
        }

        private static decimal RoundToNearestFive(decimal value)
        {
            return value <= 0m ? 0m : Math.Round(value / 5m, MidpointRounding.AwayFromZero) * 5m;
        }

        private static string InferCategory(string text)
        {
            string probe = (text ?? string.Empty).ToLowerInvariant();
            if (probe.Contains("copper") || probe.Contains("pipe")) return "Copper";
            if (probe.Contains("cable") || probe.Contains("wire") || probe.Contains("electrical")) return "Electrical";
            if (probe.Contains("labour") || probe.Contains("visit") || probe.Contains("clean")) return "Labour";
            if (probe.Contains("split ac") || probe.Contains("ac unit") || probe.Contains("equipment")) return "AC Unit";
            return "Service";
        }

        private static string ResolveTenderHsnSac(TenderBidLineItem line)
        {
            string category = (line.Category ?? string.Empty).ToLowerInvariant();
            string description = (line.ItemDescription ?? string.Empty).ToLowerInvariant();
            if (line.IsInternalLabour) return "998519";
            if (category.Contains("copper") || category.Contains("pipe") || description.Contains("copper") || description.Contains("pipe")) return "7411";
            if (category.Contains("ac unit") || category.Contains("equipment") || description.Contains("ac unit") || description.Contains("split ac")) return "8415";
            if (category.Contains("electrical") || description.Contains("cable") || description.Contains("wire")) return "8544";
            return "998519";
        }

        private static string NormalizeStateName(string state)
        {
            return IndiaStateCatalog.NormalizeStateName(string.IsNullOrWhiteSpace(state) ? "Maharashtra" : state);
        }

        private string ResolveTenderClientState(B2BClient client, ClientSite site)
        {
            string gstState = TryStateFromGstin(client?.GSTNumber);
            if (!string.IsNullOrWhiteSpace(gstState)) return gstState;
            string siteState = MatchKnownState(site?.City);
            if (!string.IsNullOrWhiteSpace(siteState)) return siteState;
            string clientState = MatchKnownState(client?.City);
            if (!string.IsNullOrWhiteSpace(clientState)) return clientState;
            return _settingsSvc.Get("DefaultPlaceOfSupply", _settingsSvc.Get("CompanyState", "Maharashtra"));
        }

        private static string TryStateFromGstin(string gstin)
        {
            gstin = IndiaTaxValidationHelper.NormalizeTaxId(gstin);
            if (string.IsNullOrWhiteSpace(gstin) || gstin.Length < 2) return null;
            string code = gstin.Substring(0, 2);
            foreach (string state in IndiaStateCatalog.Names)
            {
                if (string.Equals(IndiaStateCatalog.GetCodeByName(state), code, StringComparison.OrdinalIgnoreCase))
                    return state;
            }
            return null;
        }

        private static string MatchKnownState(string value)
        {
            if (string.IsNullOrWhiteSpace(value)) return null;
            return IndiaStateCatalog.Names.FirstOrDefault(state => string.Equals(state, value.Trim(), StringComparison.OrdinalIgnoreCase));
        }

        private static string BuildTenderInvoiceBlock(B2BClient client, ClientSite site)
        {
            var parts = new List<string>();
            if (!string.IsNullOrWhiteSpace(client?.CompanyName)) parts.Add(client.CompanyName);
            if (!string.IsNullOrWhiteSpace(client?.BillingAddress)) parts.Add(client.BillingAddress);
            if (!string.IsNullOrWhiteSpace(site?.SiteName)) parts.Add("Site: " + site.SiteName);
            if (!string.IsNullOrWhiteSpace(site?.Address) && !SameTenderAddress(client?.BillingAddress, site.Address)) parts.Add("Site Address: " + site.Address);
            return string.Join(Environment.NewLine, parts);
        }

        private static string BuildTenderCustomerBlock(B2BClient client, ClientSite site, string fallbackName)
        {
            var lines = new List<string>();
            string customerName = !string.IsNullOrWhiteSpace(client?.CompanyName) ? client.CompanyName.Trim() : (fallbackName ?? "Client").Trim();
            lines.Add(customerName);
            AddTenderAddressLines(lines, "Registered Address", client?.BillingAddress);
            if (!string.IsNullOrWhiteSpace(site?.SiteName))
                lines.Add("Site: " + site.SiteName.Trim());
            if (!string.IsNullOrWhiteSpace(site?.Address) && !SameTenderAddress(client?.BillingAddress, site.Address))
                AddTenderAddressLines(lines, "Site Address", site.Address);
            if (!string.IsNullOrWhiteSpace(client?.GSTNumber))
                lines.Add("GST No. " + client.GSTNumber.Trim());
            return string.Join("<br/>", lines.Select(HtmlTender));
        }

        private static void AddTenderAddressLines(List<string> lines, string label, string address)
        {
            string[] addressLines = SplitTenderAddressLines(address);
            if (addressLines.Length == 0)
                return;
            lines.Add(label + ": " + addressLines[0]);
            for (int i = 1; i < addressLines.Length; i++)
                lines.Add(addressLines[i]);
        }

        private static string[] SplitTenderAddressLines(string address)
        {
            if (string.IsNullOrWhiteSpace(address))
                return new string[0];
            return address.Split(new[] { "\r\n", "\n", "\r" }, StringSplitOptions.RemoveEmptyEntries)
                .Select(line => line.Trim())
                .Where(line => !string.IsNullOrWhiteSpace(line))
                .ToArray();
        }

        private static bool SameTenderAddress(string left, string right)
        {
            string normalizedLeft = string.Join(" ", SplitTenderAddressLines(left));
            string normalizedRight = string.Join(" ", SplitTenderAddressLines(right));
            return !string.IsNullOrWhiteSpace(normalizedLeft)
                && string.Equals(normalizedLeft, normalizedRight, StringComparison.OrdinalIgnoreCase);
        }

        private static string GetTenderSetting(Dictionary<string, string> settings, string key, string fallback)
        {
            return settings != null && settings.TryGetValue(key, out string value) && !string.IsNullOrWhiteSpace(value) ? value : fallback;
        }

        private static string HtmlTender(string value)
        {
            return WebUtility.HtmlEncode(value ?? string.Empty);
        }

        private static string NormalizeJurisdiction(string companyAddress, string fallbackState)
        {
            if (string.IsNullOrWhiteSpace(companyAddress)) return fallbackState;
            string[] parts = companyAddress.Split(new[] { ',' }, StringSplitOptions.RemoveEmptyEntries);
            return parts.Length == 0 ? fallbackState : parts[parts.Length - 1].Trim();
        }

        private static string ToTenderWords(long number)
        {
            if (number == 0) return "Zero";
            if (number < 0) return "Minus " + ToTenderWords(Math.Abs(number));
            string[] units = { "", "One", "Two", "Three", "Four", "Five", "Six", "Seven", "Eight", "Nine", "Ten", "Eleven", "Twelve", "Thirteen", "Fourteen", "Fifteen", "Sixteen", "Seventeen", "Eighteen", "Nineteen" };
            string[] tens = { "", "", "Twenty", "Thirty", "Forty", "Fifty", "Sixty", "Seventy", "Eighty", "Ninety" };
            Func<long, string> underHundred = n => n < 20 ? units[n] : tens[n / 10] + (n % 10 > 0 ? " " + units[n % 10] : "");
            Func<long, string> convert = null;
            convert = n =>
            {
                if (n < 100) return underHundred(n);
                if (n < 1000) return units[n / 100] + " Hundred" + (n % 100 > 0 ? " " + convert(n % 100) : "");
                if (n < 100000) return convert(n / 1000) + " Thousand" + (n % 1000 > 0 ? " " + convert(n % 1000) : "");
                if (n < 10000000) return convert(n / 100000) + " Lakh" + (n % 100000 > 0 ? " " + convert(n % 100000) : "");
                return convert(n / 10000000) + " Crore" + (n % 10000000 > 0 ? " " + convert(n % 10000000) : "");
            };
            return convert(number);
        }
    }
}
