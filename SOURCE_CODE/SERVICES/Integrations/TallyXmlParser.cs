using System;
using System.Collections.Generic;
using System.Globalization;
using System.Xml;
using HVAC_Pro_Desktop.Models;

namespace HVAC_Pro_Desktop.Services.Integrations
{
    public sealed class TallyXmlParser
    {
        public List<TallyMasterRecord> ParseMasters(string xml)
        {
            var records = new List<TallyMasterRecord>();
            if (string.IsNullOrWhiteSpace(xml))
                return records;

            var document = new XmlDocument();
            document.LoadXml(xml);
            foreach (XmlNode ledger in document.GetElementsByTagName("LEDGER"))
                records.Add(ParseLedger(ledger));
            foreach (XmlNode item in document.GetElementsByTagName("STOCKITEM"))
                records.Add(ParseStockItem(item));
            foreach (XmlNode unit in document.GetElementsByTagName("UNIT"))
                records.Add(ParseUnit(unit));
            return records;
        }

        public string ReadResponseSummary(string responseXml)
        {
            if (string.IsNullOrWhiteSpace(responseXml))
                return "No response from Tally.";

            try
            {
                var document = new XmlDocument();
                document.LoadXml(responseXml);
                string created = Read(document, "//CREATED");
                string altered = Read(document, "//ALTERED");
                string errors = Read(document, "//ERRORS");
                string ignored = Read(document, "//IGNORED");
                string lineError = Read(document, "//LINEERROR");
                string summary = "Created=" + BlankToZero(created) + ", Altered=" + BlankToZero(altered) + ", Ignored=" + BlankToZero(ignored) + ", Errors=" + BlankToZero(errors);
                return string.IsNullOrWhiteSpace(lineError) ? summary : summary + ". " + lineError;
            }
            catch
            {
                return responseXml.Length > 500 ? responseXml.Substring(0, 500) : responseXml;
            }
        }

        public bool IsSuccess(string responseXml)
        {
            if (string.IsNullOrWhiteSpace(responseXml))
                return false;

            try
            {
                var document = new XmlDocument();
                document.LoadXml(responseXml);
                int errors = ParseInt(Read(document, "//ERRORS"));
                string lineError = Read(document, "//LINEERROR");
                return errors == 0 && string.IsNullOrWhiteSpace(lineError);
            }
            catch
            {
                return responseXml.IndexOf("<LINEERROR>", StringComparison.OrdinalIgnoreCase) < 0;
            }
        }

        private static TallyMasterRecord ParseLedger(XmlNode node)
        {
            return new TallyMasterRecord
            {
                MasterType = "Ledger",
                Name = ReadAttributeOrChild(node, "NAME"),
                Parent = ReadChild(node, "PARENT"),
                Guid = ReadChild(node, "GUID"),
                MasterId = ParseNullableInt(ReadChild(node, "MASTERID")),
                Gstin = ReadChild(node, "PARTYGSTIN"),
                ClosingBalance = ParseDecimal(ReadChild(node, "CLOSINGBALANCE"))
            };
        }

        private static TallyMasterRecord ParseStockItem(XmlNode node)
        {
            return new TallyMasterRecord
            {
                MasterType = "StockItem",
                Name = ReadAttributeOrChild(node, "NAME"),
                Parent = ReadChild(node, "PARENT"),
                Guid = ReadChild(node, "GUID"),
                MasterId = ParseNullableInt(ReadChild(node, "MASTERID")),
                Unit = ReadChild(node, "BASEUNITS"),
                HsnCode = ReadChild(node, "GSTHSNNAME"),
                ClosingQuantity = ParseQuantity(ReadChild(node, "CLOSINGBALANCE")),
                Rate = ParseRate(ReadChild(node, "RATE"))
            };
        }

        private static TallyMasterRecord ParseUnit(XmlNode node)
        {
            return new TallyMasterRecord
            {
                MasterType = "Unit",
                Name = ReadAttributeOrChild(node, "NAME"),
                Parent = ReadChild(node, "ORIGINALNAME"),
                Guid = ReadChild(node, "GUID"),
                MasterId = ParseNullableInt(ReadChild(node, "MASTERID"))
            };
        }

        private static string Read(XmlDocument document, string xpath)
        {
            XmlNode node = document.SelectSingleNode(xpath);
            return node == null ? string.Empty : (node.InnerText ?? string.Empty).Trim();
        }

        private static string ReadChild(XmlNode node, string childName)
        {
            if (node == null)
                return string.Empty;
            XmlNode child = node.SelectSingleNode(childName);
            return child == null ? string.Empty : (child.InnerText ?? string.Empty).Trim();
        }

        private static string ReadAttributeOrChild(XmlNode node, string name)
        {
            if (node == null)
                return string.Empty;
            XmlAttribute attribute = node.Attributes == null ? null : node.Attributes[name];
            if (attribute != null && !string.IsNullOrWhiteSpace(attribute.Value))
                return attribute.Value.Trim();
            return ReadChild(node, name);
        }

        private static int ParseInt(string value)
        {
            int parsed;
            return int.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out parsed) ? parsed : 0;
        }

        private static int? ParseNullableInt(string value)
        {
            int parsed;
            return int.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out parsed) ? (int?)parsed : null;
        }

        private static decimal ParseDecimal(string value)
        {
            if (string.IsNullOrWhiteSpace(value))
                return 0m;
            value = value.Replace(",", string.Empty).Trim();
            int space = value.IndexOf(' ');
            if (space > 0)
                value = value.Substring(0, space);
            decimal parsed;
            return decimal.TryParse(value, NumberStyles.Any, CultureInfo.InvariantCulture, out parsed) ? parsed : 0m;
        }

        private static decimal ParseQuantity(string value)
        {
            return ParseDecimal(value);
        }

        private static decimal ParseRate(string value)
        {
            if (string.IsNullOrWhiteSpace(value))
                return 0m;
            int slash = value.IndexOf('/');
            if (slash > 0)
                value = value.Substring(0, slash);
            return ParseDecimal(value);
        }

        private static string BlankToZero(string value)
        {
            return string.IsNullOrWhiteSpace(value) ? "0" : value.Trim();
        }
    }
}
