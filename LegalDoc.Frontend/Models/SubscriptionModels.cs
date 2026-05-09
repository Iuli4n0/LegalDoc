namespace LegalDoc.Frontend.Models;

internal class CreateCheckoutRequest
{
    public string Plan { get; set; } = string.Empty;
}

internal record CheckoutResponse(string CheckoutUrl);

internal record PortalResponse(string PortalUrl);
