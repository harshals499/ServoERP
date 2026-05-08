using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Net;
using System.Threading;
using System.Web.Script.Serialization;

namespace HVAC_Pro_Desktop.Services
{
    public class NominatimGeocodingService
    {
        private static readonly object RequestGate = new object();
        private static DateTime _lastRequestUtc = DateTime.MinValue;

        public GeocodeResult LocateAddress(string rawAddress)
        {
            string address = (rawAddress ?? string.Empty).Trim();
            if (string.IsNullOrWhiteSpace(address))
                throw new Exception("Enter an address before locating it.");

            try
            {
                RespectRateLimit();

                string query = Uri.EscapeDataString(NormalizeIndianAddress(address));
                string url = "https://nominatim.openstreetmap.org/search?q=" + query + "&format=json&limit=1&countrycodes=in&addressdetails=1";
                HttpWebRequest request = (HttpWebRequest)WebRequest.Create(url);
                request.Method = "GET";
                request.Timeout = 15000;
                request.ReadWriteTimeout = 15000;
                request.UserAgent = "HVACPRO-MSE/1.0";
                request.AutomaticDecompression = DecompressionMethods.GZip | DecompressionMethods.Deflate;

                using (HttpWebResponse response = (HttpWebResponse)request.GetResponse())
                using (Stream stream = response.GetResponseStream())
                using (StreamReader reader = new StreamReader(stream))
                {
                    string json = reader.ReadToEnd();
                    var serializer = new JavaScriptSerializer();
                    object[] rows = serializer.DeserializeObject(json) as object[];
                    if (rows == null || rows.Length == 0)
                        throw new Exception("No coordinates were found for that address.");

                    var first = rows[0] as Dictionary<string, object>;
                    if (first == null)
                        throw new Exception("The geocoding response could not be read.");

                    string latText = first.ContainsKey("lat") ? Convert.ToString(first["lat"], CultureInfo.InvariantCulture) : null;
                    string lonText = first.ContainsKey("lon") ? Convert.ToString(first["lon"], CultureInfo.InvariantCulture) : null;
                    string displayName = first.ContainsKey("display_name") ? Convert.ToString(first["display_name"], CultureInfo.InvariantCulture) : address;

                    if (!double.TryParse(latText, NumberStyles.Float, CultureInfo.InvariantCulture, out double latitude) ||
                        !double.TryParse(lonText, NumberStyles.Float, CultureInfo.InvariantCulture, out double longitude))
                    {
                        throw new Exception("The geocoding response did not include valid coordinates.");
                    }

                    return new GeocodeResult
                    {
                        Latitude = latitude,
                        Longitude = longitude,
                        DisplayName = displayName
                    };
                }
            }
            catch (WebException ex)
            {
                AppRuntime.LogException("NominatimGeocodingService.LocateAddress", ex);
                throw new Exception("OpenStreetMap lookup failed. Please check internet access and try again.");
            }
            catch (Exception ex)
            {
                AppRuntime.LogException("NominatimGeocodingService.LocateAddress", ex);
                throw;
            }
        }

        private static void RespectRateLimit()
        {
            lock (RequestGate)
            {
                TimeSpan wait = TimeSpan.FromSeconds(1) - (DateTime.UtcNow - _lastRequestUtc);
                if (wait > TimeSpan.Zero)
                    Thread.Sleep(wait);

                _lastRequestUtc = DateTime.UtcNow;
            }
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

    public class GeocodeResult
    {
        public double Latitude { get; set; }
        public double Longitude { get; set; }
        public string DisplayName { get; set; }
    }
}
