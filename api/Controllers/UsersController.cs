using System.Net;
using System.Text.Json;
using Mercantec.Tech.Api.Auth;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Mercantec.Tech.Api.Controllers;

[ApiController]
[Route("api/users")]
[Authorize]
public class UsersController(IHttpClientFactory httpClientFactory, IConfiguration config) : ControllerBase
{
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);

    [HttpGet]
    public async Task<ActionResult<IReadOnlyList<UserListItem>>> List(CancellationToken ct)
    {
        if (!UserClaims.IsTeacher(User))
            return Forbid();

        var authorization = Request.Headers.Authorization.ToString();
        if (!authorization.StartsWith("Bearer ", StringComparison.OrdinalIgnoreCase))
            return Unauthorized();

        var issuer = (config["AUTH_ISSUER"] ?? "https://auth.mercantec.tech").TrimEnd('/');
        var client = httpClientFactory.CreateClient("auth-admin");

        using var directoryRequest = AuthorizedGet($"{issuer}/api/admin/users-directory", authorization);
        using var directoryResponse = await client.SendAsync(directoryRequest, ct);
        if (directoryResponse.StatusCode is HttpStatusCode.Unauthorized or HttpStatusCode.Forbidden)
        {
            return StatusCode(StatusCodes.Status403Forbidden, new
            {
                error = "Kun en administrator i Mercantec Auth kan hente brugerlisten."
            });
        }

        if (!directoryResponse.IsSuccessStatusCode)
        {
            return StatusCode(StatusCodes.Status502BadGateway, new
            {
                error = "Brugerlisten kunne ikke hentes fra Mercantec Auth."
            });
        }

        var directory = await directoryResponse.Content.ReadFromJsonAsync<List<AuthDirectoryUser>>(Json, ct) ?? [];
        var roles = await RolesForAsync(client, issuer, authorization, directory, ct);
        if (roles.Error is not null)
            return StatusCode(roles.Status, new { error = roles.Error });

        var items = directory.Select((user, index) =>
        {
            var names = roles.Sets![index]
                .Where(role => !string.IsNullOrWhiteSpace(role))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .OrderBy(role => role, StringComparer.OrdinalIgnoreCase)
                .ToArray();
            return new UserListItem(
                user.Id,
                string.IsNullOrWhiteSpace(user.DisplayName) ? "Ukendt" : user.DisplayName.Trim(),
                string.IsNullOrWhiteSpace(user.Email) ? null : user.Email.Trim(),
                user.IsDisabled,
                UserClaims.ToAppRole(names),
                names);
        }).ToArray();

        return Ok(items);
    }

    private static async Task<RoleFetch> RolesForAsync(
        HttpClient client,
        string issuer,
        string authorization,
        List<AuthDirectoryUser> users,
        CancellationToken ct)
    {
        if (users.Count > 0 && users.All(user => user.Roles is not null))
            return new RoleFetch(users.Select(user => (IReadOnlyList<string>)user.Roles!).ToArray(), null, StatusCodes.Status200OK);

        var gate = new SemaphoreSlim(8);
        var sets = new IReadOnlyList<string>[users.Count];
        var failed = 0;
        var forbidden = 0;

        var tasks = users.Select(async (user, index) =>
        {
            if (user.Roles is not null)
            {
                sets[index] = user.Roles;
                return;
            }

            await gate.WaitAsync(ct);
            try
            {
                using var request = AuthorizedGet($"{issuer}/api/admin/users/{user.Id}/roles", authorization);
                using var response = await client.SendAsync(request, ct);
                if (response.StatusCode is HttpStatusCode.Unauthorized or HttpStatusCode.Forbidden)
                {
                    Interlocked.Increment(ref forbidden);
                    sets[index] = [];
                    return;
                }

                if (!response.IsSuccessStatusCode)
                {
                    Interlocked.Increment(ref failed);
                    sets[index] = [];
                    return;
                }

                sets[index] = await response.Content.ReadFromJsonAsync<List<string>>(Json, ct) ?? [];
            }
            finally
            {
                gate.Release();
            }
        });

        await Task.WhenAll(tasks);

        if (forbidden > 0)
        {
            return new RoleFetch(null, "Kun en administrator i Mercantec Auth kan hente brugerlisten.", StatusCodes.Status403Forbidden);
        }

        if (failed > 0)
        {
            return new RoleFetch(null, "Rollerne kunne ikke hentes fra Mercantec Auth.", StatusCodes.Status502BadGateway);
        }

        return new RoleFetch(sets, null, StatusCodes.Status200OK);
    }

    private static HttpRequestMessage AuthorizedGet(string url, string authorization)
    {
        var request = new HttpRequestMessage(HttpMethod.Get, url);
        request.Headers.TryAddWithoutValidation("Authorization", authorization);
        return request;
    }

    private sealed class AuthDirectoryUser
    {
        public Guid Id { get; set; }
        public string? DisplayName { get; set; }
        public string? Email { get; set; }
        public bool IsDisabled { get; set; }
        public List<string>? Roles { get; set; }
    }

    private sealed record RoleFetch(IReadOnlyList<string>[]? Sets, string? Error, int Status);
}

public sealed record UserListItem(
    Guid Id,
    string DisplayName,
    string? Email,
    bool IsDisabled,
    string AppRole,
    IReadOnlyList<string> Roles);
