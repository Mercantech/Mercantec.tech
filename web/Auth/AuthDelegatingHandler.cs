using System.Net;
using System.Net.Http.Headers;

namespace Mercantec.Tech.Web.Auth;

public sealed class AuthDelegatingHandler(AuthService auth) : DelegatingHandler
{
    protected override async Task<HttpResponseMessage> SendAsync(
        HttpRequestMessage request,
        CancellationToken cancellationToken)
    {
        var token = await auth.GetValidAccessTokenAsync();
        if (!string.IsNullOrEmpty(token))
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);

        var response = await base.SendAsync(request, cancellationToken);

        if (response.StatusCode == HttpStatusCode.Unauthorized)
        {
            try
            {
                if (!await auth.TryRefreshOnUnauthorizedAsync())
                    return response;

                using var retry = await CloneRequestAsync(request, cancellationToken);
                var newToken = await auth.GetValidAccessTokenAsync();
                if (string.IsNullOrEmpty(newToken))
                    return response;

                retry.Headers.Authorization = new AuthenticationHeaderValue("Bearer", newToken);
                // A failed refresh must return the original readable401. Only
                // release it once an actual retry will replace the response.
                response.Dispose();
                // Calling the inner handler directly gives each request one
                // retry, without a shared flag affecting later requests.
                return await base.SendAsync(retry, cancellationToken);
            }
            catch
            {
                response.Dispose();
                throw;
            }
        }

        return response;
    }

    private static async Task<HttpRequestMessage> CloneRequestAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        var clone = new HttpRequestMessage(request.Method, request.RequestUri);
        if (request.Content is not null)
        {
            var bytes = await request.Content.ReadAsByteArrayAsync(cancellationToken);
            clone.Content = new ByteArrayContent(bytes);
            foreach (var header in request.Content.Headers)
                clone.Content.Headers.TryAddWithoutValidation(header.Key, header.Value);
        }

        foreach (var header in request.Headers)
            clone.Headers.TryAddWithoutValidation(header.Key, header.Value);

        return clone;
    }
}
