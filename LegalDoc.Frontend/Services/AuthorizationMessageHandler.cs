namespace LegalDoc.Frontend.Services;

public class AuthorizationMessageHandler : DelegatingHandler
{
    private readonly AuthStateService _authState;

    public AuthorizationMessageHandler(AuthStateService authState)
    {
        _authState = authState;
    }

    protected override async Task<HttpResponseMessage> SendAsync(
        HttpRequestMessage request, CancellationToken cancellationToken)
    {
        var token = _authState.Token;
        if (!string.IsNullOrEmpty(token))
        {
            request.Headers.Authorization =
                new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", token);
        }

        return await base.SendAsync(request, cancellationToken);
    }
}

