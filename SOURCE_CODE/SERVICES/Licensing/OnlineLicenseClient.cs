using HVAC_Pro_Desktop.Models;

namespace HVAC_Pro_Desktop.Services.Licensing
{
    public sealed class OnlineLicenseClient : IOnlineLicenseClient
    {
        public LicenseValidationResult Activate(LicenseActivationRequest request)
        {
            return new LicenseValidationResult
            {
                Success = false,
                RequiresActivation = true,
                Message = "Online licensing API is not configured. Use a signed offline activation file."
            };
        }

        public LicenseValidationResult Validate(LicenseSnapshot snapshot)
        {
            return new LicenseValidationResult
            {
                Success = false,
                Message = "Online licensing API is not configured."
            };
        }
    }
}
