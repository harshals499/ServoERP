using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using Newtonsoft.Json.Linq;

namespace HVAC_Pro_Desktop.OfflineDesktop
{
    internal static class OfflinePresentation
    {
        private static readonly CultureInfo India=CultureInfo.GetCultureInfo("en-IN");
        public static string Describe(OfflineRecord record,OfflineWorkspaceStore store)
        {
            if(record==null)return "Select a record to view its downloaded details.";
            var text=new List<string> {record.DisplayName,"", "Status: "+record.Status,""};
            var data=record.Data;
            if(record.Module!="Clients")
            {
                var client=store.ByServerId("Clients",(int?)data["ClientID"]??0);
                Guid clientId;if(client==null && Guid.TryParse((string)data["ClientEntityId"],out clientId))client=store.Record(clientId);
                if(client!=null)text.Add("Client: "+client.DisplayName);
                else if(((int?)data["ClientID"]??0)>0)text.Add("Client reference: #"+data["ClientID"]);
                if(((int?)data["SiteID"]??0)>0)
                {
                    var site=store.ByServerId("Sites",data.Value<int>("SiteID"));text.Add(site!=null?"Site: "+site.DisplayName:"Site reference: #"+data["SiteID"]);
                }
            }
            string[] fields=record.Module=="Clients"?new[]{"PrimaryContact","Phone","Email","BillingAddress","City","GSTNumber","PANNumber","PaymentTermsDays","CreditLimit","Notes"}:
                record.Module=="Sites"?new[]{"SiteName","Address","City","ACSystemCount","RefrigerationSystemCount","CoolingTowerCount"}:
                record.Module=="Jobs"?new[]{"JobNumber","Title","ScheduledDate","Priority","Status","Description","Notes"}:
                record.Module=="InvoiceDrafts"?new[]{"InvoiceDate","DueDate","GSTMode","PlaceOfSupply","Notes"}:
                record.Module=="Invoices"?new[]{"InvoiceNumber","InvoiceDate","DueDate","PaymentStatus","SubTotal","TaxAmount","TotalAmount","PaidAmount","BalanceDue","GSTMode","PlaceOfSupply","Notes"}:
                new[]{"PaymentNumber","PaymentDate","AmountPaid","PaymentMode","ReferenceNumber","Notes"};
            var labels=new Dictionary<string,string> { {"PrimaryContact","Contact"},{"BillingAddress","Address"},{"GSTNumber","GSTIN"},{"PANNumber","PAN"},{"PaymentTermsDays","Payment terms (days)"},{"CreditLimit","Credit limit"},{"ACSystemCount","AC systems"},{"RefrigerationSystemCount","Refrigeration systems"},{"CoolingTowerCount","Cooling towers"},{"JobNumber","Job number"},{"ScheduledDate","Scheduled date"},{"InvoiceNumber","Invoice number"},{"InvoiceDate","Invoice date"},{"DueDate","Due date"},{"PaymentStatus","Payment status"},{"SubTotal","Subtotal"},{"TaxAmount","Tax"},{"TotalAmount","Total"},{"PaidAmount","Paid"},{"BalanceDue","Balance due"},{"GSTMode","GST mode"},{"PlaceOfSupply","Place of supply"},{"PaymentNumber","Payment number"},{"PaymentDate","Payment date"},{"AmountPaid","Amount"},{"PaymentMode","Payment mode"},{"ReferenceNumber","Reference / UTR"} };
            foreach(string field in fields)
            {
                var value=data[field];if(value==null || value.Type==JTokenType.Null || string.IsNullOrWhiteSpace(Convert.ToString(value)))continue;
                string display=Convert.ToString(value,India);
                if(field.EndsWith("Date"))
                {
                    DateTime date;
                    if(value.Type==JTokenType.Date)display=value.Value<DateTime>().ToString("dd/MM/yyyy",India);
                    else if(DateTime.TryParse((string)value,India,DateTimeStyles.RoundtripKind,out date))display=date.ToString("dd/MM/yyyy",India);
                }
                if(new[]{"CreditLimit","SubTotal","TaxAmount","TotalAmount","PaidAmount","BalanceDue","AmountPaid"}.Contains(field) && decimal.TryParse(display,NumberStyles.Number,CultureInfo.InvariantCulture,out var amount))display="INR "+amount.ToString("N2",India);
                text.Add((labels.ContainsKey(field)?labels[field]:field)+": "+display);
            }
            if(data["LineItems"] is JArray lines)
            {
                text.Add("");text.Add("Line items");
                foreach(var line in lines)text.Add((string)line["Description"]+" | Qty "+line["Quantity"]+" | Rate INR "+line["Rate"]+" | GST "+line["GSTPercent"]+"%");
            }
            if(OfflineModules.IsDraft(record.Module)) {text.Add("");text.Add(record.Status=="Finalized"?"Finalized on the office server.":"Provisional draft. No official number, payment posting or balance change until finalized online.");}
            var issue=store.LatestIssue(record.EntityId);
            if(!string.IsNullOrWhiteSpace(issue?.Error)){text.Add("");text.Add("Needs office review: "+issue.Error);}
            return string.Join(Environment.NewLine,text);
        }
    }
}
