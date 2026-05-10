namespace LegalDoc.Frontend.Models;

public class CreateCheckoutRequest
{
    public string Plan { get; set; } = string.Empty;
}

public record CheckoutResponse(string CheckoutUrl);

public record PortalResponse(string PortalUrl);
