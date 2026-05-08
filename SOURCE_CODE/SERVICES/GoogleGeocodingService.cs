using System;
using System.Configuration;
using System.IO;
using System.Net;
using System.Web.Script.Serialization;

namespace HVAC_Pro_Desktop.Services
{
    public sealed class GoogleGeocodingService
    {
        private readonly string _apiKey;

        public GoogleGeocodingService()
        {
            _apiKey = Environment.GetEnvironmentVariable("GOOGLE_MAPS_API_KEY");
            if (string.IsNullOrWhiteSpace(_apiKey))
                _apiKey = ConfigurationManager.AppSettings["GoogleMapsApiKey"];
        }

        public bool IsConfigured => !string.IsNullOrWhiteSpace(_apiKey);

        public GeocodeResult TryGeocodeAddress(string rawAddress)
        {
            if (string.IsNullOrWhiteSpace(rawAddress))
                return GeocodeResult.FromFailure("Missing address");

            if (!IsConfigured)
                return GeocodeResult.FromFailure("Google Maps API key is not configured");

            string address = NormalizeIndianAddress(rawAddress.Trim());

            try
            {
                string url =
                    "https://maps.googleapis.com/maps/api/geocode/json?address=" +
                    Uri.EscapeDataString(address) +
                    "&region=in&components=country:IN&key=" + Uri.EscapeDataString(_apiKey);

                HttpWebRequest request = (HttpWebRequest)WebRequest.Create(url);
                request.Method = "GET";
                request.Timeout = 15000;
                request.ReadWriteTimeout = 15000;
                request.UserAgent = "HVAC_PRO_MSE_GeoIntelligence";

                using (HttpWebResponse response = (HttpWebResponse)request.GetResponse())
                using (StreamReader reader = new StreamReader(response.GetResponseStream()))
                {
                    string json = reader.ReadToEnd();
                    JavaScriptSerializer serializer = new JavaScriptSerializer { MaxJsonLength = int.MaxValue };
                    GoogleGeocodeResponse payload = serializer.Deserialize<GoogleGeocodeResponse>(json);

                    if (payload == null)
                        return GeocodeResult.FromFailure("Empty geocoding response");

                    if (!string.Equals(payload.status, "OK", StringComparison.OrdinalIgnoreCase))
                        return GeocodeResult.FromFailure(string.IsNullOrWhiteSpace(payload.status) ? "Unknown geocoding status" : payload.status);

                    if (payload.results == null || payload.results.Length == 0 || payload.results[0] == null || payload.results[0].geometry == null || payload.results[0].geometry.location == null)
                        return GeocodeResult.FromFailure("No coordinates found");

                    return GeocodeResult.FromSuccess(
                        payload.results[0].geometry.location.lat,
                        payload.results[0].geometry.location.lng,
                        payload.results[0].formatted_address ?? address);
                }
            }
            catch (Exception ex)
            {
                AppRuntime.LogException("GoogleGeocoding", ex);
                return GeocodeResult.FromFailure(ex.Message);
            }
        }

        public sealed class GeocodeResult
        {
            public bool Success { get; private set; }
            public double? Latitude { get; private set; }
            public double? Longitude { get; private set; }
            public string FormattedAddress { get; private set; }
            public string Status { get; private set; }

            public static GeocodeResult FromSuccess(double latitude, double longitude, string formattedAddress)
            {
                return new GeocodeResult
                {
                    Success = true,
                    Latitude = latitude,
                    Longitude = longitude,
                    FormattedAddress = formattedAddress,
                    Status = "OK"
                };
            }

            public static GeocodeResult FromFailure(string status)
            {
                return new GeocodeResult
                {
                    Success = false,
                    Status = string.IsNullOrWhiteSpace(status) ? "FAILED" : status
                };
            }
        }

        private sealed class GoogleGeocodeResponse
        {
            public string status { get; set; }
            public GoogleGeocodeResult[] results { get; set; }
        }

        private sealed class GoogleGeocodeResult
        {
            public string formatted_address { get; set; }
            public GoogleGeocodeGeometry geometry { get; set; }
        }

        private sealed class GoogleGeocodeGeometry
        {
            public GoogleGeocodeLocation location { get; set; }
        }

        private sealed class GoogleGeocodeLocation
        {
            public double lat { get; set; }
            public double lng { get; set; }
        }

        private static string NormalizeIndianAddress(string address)
        {
            string value = (address ?? string.Empty).Trim();
            if (string.IsNullOrWhiteSpace(value))
                return value;

            bool hasIndia = value.IndexOf("india", StringComparison.OrdinalIgnoreCase) >= 0;
            bool hasState = HasIndianState(value);
            if (!hasState)
                value += ", Maharashtra";
            if (!hasIndia)
                value += ", India";
            return value;
        }

        private static bool HasIndianState(string value)
        {
            string[] states =
            {
                "andhra pradesh", "arunachal pradesh", "assam", "bihar", "chhattisgarh", "goa",
                "gujarat", "haryana", "himachal pradesh", "jharkhand", "karnataka", "kerala",
                "madhya pradesh", "maharashtra", "manipur", "meghalaya", "mizoram", "nagaland",
                "odisha", "orissa", "punjab", "rajasthan", "sikkim", "tamil nadu", "telangana",
                "tripura", "uttar pradesh", "uttarakhand", "west bengal", "delhi", "chandigarh",
                "puducherry", "jammu", "kashmir", "ladakh", "thane", "mumbai", "navi mumbai", "pune"
            };

            return Array.Exists(states, state => value.IndexOf(state, StringComparison.OrdinalIgnoreCase) >= 0);
        }
    }
}
