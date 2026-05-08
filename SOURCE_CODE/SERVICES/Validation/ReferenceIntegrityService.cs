using System.Collections.Generic;
using System.Linq;
using HVAC_Pro_Desktop.Models;
using HVAC_Pro_Desktop.Models.Validation;

namespace HVAC_Pro_Desktop.Services.Validation
{
    public sealed class ReferenceIntegrityService
    {
        public ValidationResult CheckClientSite(int clientId, int siteId, IEnumerable<ClientSite> sites, string module)
        {
            var result = new ValidationResult();
            if (clientId <= 0)
                result.Add(ValidationSeverity.Error, module, "ClientID", module + " must have a valid client.");
            if (siteId > 0 && sites != null)
            {
                ClientSite site = sites.FirstOrDefault(s => s.SiteID == siteId);
                if (site == null)
                    result.Add(ValidationSeverity.Error, module, "SiteID", "Selected site does not exist.");
                else if (clientId > 0 && site.ClientID != clientId)
                    result.Add(ValidationSeverity.Error, module, "SiteID", "Selected site belongs to a different client.", "Choose a site under the selected client.");
            }
            return result;
        }
    }
}
