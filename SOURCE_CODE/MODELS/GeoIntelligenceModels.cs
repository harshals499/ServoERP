using System;
using System.Collections.Generic;

namespace HVAC_Pro_Desktop.Models
{
    public class SiteMapDto
    {
        public int SiteId { get; set; }
        public string SiteName { get; set; }
        public string ClientName { get; set; }
        public double Lat { get; set; }
        public double Lon { get; set; }
        public string ContractType { get; set; }
        public DateTime? ContractExpiry { get; set; }
        public int OpenCallsCount { get; set; }
        public int OverdueInvoiceCount { get; set; }
        public DateTime? LastPMDate { get; set; }
        public DateTime? NextPMDueDate { get; set; }
        public string PinColour { get; set; }
        public string PopupHtml { get; set; }
    }

    public class GeoFilterOptions
    {
        public DateTime DateFrom { get; set; }
        public DateTime DateTo { get; set; }
        public string RangeKey { get; set; }
        public string ContractType { get; set; }
        public int? TechnicianId { get; set; }
        public bool UrgentOnly { get; set; }
        public bool OpenCallsOnly { get; set; }
        public bool RenewalRiskOnly { get; set; }
        public bool NoContractOnly { get; set; }
    }

    public class GeoSummaryItemDto
    {
        public int SiteId { get; set; }
        public string Title { get; set; }
        public string Detail { get; set; }
        public bool IsClickable { get; set; } = true;
    }

    public class GeoNotificationDto
    {
        public string Type { get; set; }
        public string Message { get; set; }
    }

    public class ZoneInsightDto
    {
        public int SiteId { get; set; }
        public string SiteName { get; set; }
        public string ClientName { get; set; }
        public string ContractType { get; set; }
        public DateTime? ContractExpiry { get; set; }
        public decimal ContractValue { get; set; }
        public int OpenCallsCount { get; set; }
        public DateTime? LastPMDate { get; set; }
        public DateTime? NextPMDueDate { get; set; }
        public decimal OverdueInvoicesAmount { get; set; }
        public string TopIssue { get; set; }
        public string RecommendedAction { get; set; }
        public string DemandScoreLabel { get; set; }
        public int BreakdownRatePercent { get; set; }
        public int AvgTravelTimeMinutes { get; set; }
        public int AdditionalTechniciansRecommended { get; set; }
        public string HealthColour { get; set; }
    }

    public class SummaryCardsDto
    {
        public List<GeoSummaryItemDto> TopDemandAreas { get; set; } = new List<GeoSummaryItemDto>();
        public List<GeoSummaryItemDto> RisingBreakdowns { get; set; } = new List<GeoSummaryItemDto>();
        public string TravelAlertTitle { get; set; }
        public string TravelAlertDetail { get; set; }
        public bool TravelNeedsOfficeCoordinates { get; set; }
        public int UpsellOpportunity { get; set; }
        public int MappedSitesCount { get; set; }
    }
}
