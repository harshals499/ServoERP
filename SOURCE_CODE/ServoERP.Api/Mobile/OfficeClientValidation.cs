using FluentValidation;
using ServoERP.Engineer;
namespace ServoERP.Api.Mobile;

public sealed partial class OfficeWorkspaceService
{
    // Matches desktop B2BClientValidator for the client fields editable on mobile.
    private sealed class ClientValidator : AbstractValidator<DemoRecord>
    {
        public ClientValidator()
        {
            RuleFor(r => r.Get("name")).NotEmpty().MaximumLength(200).WithMessage("Client company name is required and must be 200 characters or less.");
            RuleFor(r => r.Get("phone")).Matches(@"^[0-9+\-\s]{10,20}$").When(r => r.Get("phone").Length > 0).WithMessage("Client phone must be a valid Indian business contact number.");
            RuleFor(r => r.Get("email")).EmailAddress().When(r => r.Get("email").Length > 0).WithMessage("Client email address is not valid.");
            RuleFor(r => r.Get("pan")).Matches("^[A-Z]{5}[0-9]{4}[A-Z]$").When(r => r.Get("pan").Length > 0).WithMessage("PAN format is invalid.");
            RuleFor(r => r.Get("gst")).Matches(@"^[0-9]{2}[A-Z]{5}[0-9]{4}[A-Z][1-9A-Z]Z[0-9A-Z]$").When(r => r.Get("gst").Length > 0).WithMessage("GST number must be a valid 15-character Indian GSTIN.");
        }
    }
    private static async Task ValidateClient(DemoRecord record, CancellationToken ct)
    {
        foreach (var key in record.Values.Keys.ToArray()) record.Values[key] = record.Get(key).Trim();
        record.Values["gst"] = record.Get("gst").ToUpperInvariant(); record.Values["pan"] = record.Get("pan").ToUpperInvariant();
        var result = await new ClientValidator().ValidateAsync(record, ct);
        if (!result.IsValid) throw new FieldRuleException(string.Join("\n", result.Errors.Select(e => e.ErrorMessage)));
    }
}
