using System.Net;
using System.Net.Http.Headers;

namespace Mercantec.Tech.Web.Auth;

public sealed class AuthDelegatingHandler(AuthService auth) : DelegatingHandler
{
    private bool _retried;

    protected override async Task<HttpResponseMessage> SendAsync(
        HttpRequestMessage request,
        CancellationToken cancellationToken)
    {
        var token = await auth.GetValidAccessTokenAsync();
        if (!string.IsNullOrEmpty(token))
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);

        var response = await base.SendAsync(request, cancellationToken);

        if (response.StatusCode == HttpStatusCode.Unauthorized && !_retried)
        {
            _retried = true;
            response.Dispose();
            var refreshed = await auth.TryRefreshOnUnauthorizedAsync();
            if (refreshed)
            {
                var retry = await CloneRequestAsync(request);
                var newToken = await auth.GetValidAccessTokenAsync();
                if (!string.IsNullOrEmpty(newToken))
                    retry.Headers.Authorization = new AuthenticationHeaderValue("Bearer", newToken);

                return await base.SendAsync(retry, cancellationToken);
            }
        }

        return response;
    }

    private static async Task<HttpRequestMessage> CloneRequestAsync(HttpRequestMessage request)
    {
        var clone = new HttpRequestMessage(request.Method, request.RequestUri);
        if (request.Content is not null)
        {
            var bytes = await request.Content.ReadAsByteArrayAsync();
            clone.Content = new ByteArrayContent(bytes);
            foreach (var header in request.Content.Headers)
                clone.Content.Headers.TryAddWithoutValidation(header.Key, header.Value);
        }

        foreach (var header in request.Headers)
            clone.Headers.TryAddWithoutValidation(header.Key, header.Value);

        return clone;
    }
}
