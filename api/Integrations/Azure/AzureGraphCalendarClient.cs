using System.Net.Http.Headers;
using System.Text.Json;
using System.Text.Json.Serialization;
using Mercantec.Tech.Contracts;
using Microsoft.Extensions.Options;

namespace Mercantec.Tech.Api.Integrations.Azure;

public sealed class AzureGraphCalendarClient(
    IHttpClientFactory httpClientFactory,
    IOptions<AzureGraphOptions> options,
    ILogger<AzureGraphCalendarClient> logger)
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

    private readonly AzureGraphOptions _options = options.Value;
    private string? _cachedToken;
    private DateTimeOffset _tokenExpiresAt;

    public bool IsConfigured => _options.IsConfigured;

    public string? ResolveTeacherUpn(string? sub, string? email = null) =>
        _options.ResolveTeacherUpn(sub, email);

    public async Task<IReadOnlyList<TeacherCalendarEvent>> GetCalendarViewAsync(
        string azureUpn,
        DateTimeOffset from,
        DateTimeOffset to,
        CancellationToken ct)
    {
        if (!IsConfigured)
            throw new InvalidOperationException("Azure Graph er ikke konfigureret");

        var token = await GetAccessTokenAsync(ct);
        var http = httpClientFactory.CreateClient("azure-graph");
        var events = new List<TeacherCalendarEvent>();

        var query = new Dictionary<string, string>
        {
            ["startDateTime"] = from.UtcDateTime.ToString("o"),
            ["endDateTime"] = to.UtcDateTime.ToString("o"),
            ["$select"] = "id,subject,start,end,isAllDay,location,sensitivity,isOnlineMeeting,onlineMeeting,webLink,isCancelled,showAs",
            ["$orderby"] = "start/dateTime",
            ["$top"] = "100"
        };
        var qs = string.Join("&", query.Select(kv =>
            $"{Uri.EscapeDataString(kv.Key)}={Uri.EscapeDataString(kv.Value)}"));

        string? url =
            $"https://graph.microsoft.com/v1.0/users/{Uri.EscapeDataString(azureUpn)}/calendarView?{qs}";

        while (url is not null)
        {
            using var request = new HttpRequestMessage(HttpMethod.Get, url);
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
            request.Headers.TryAddWithoutValidation("Prefer", "outlook.timezone=\"Romance Standard Time\"");

            using var response = await http.SendAsync(request, ct);
            var body = await response.Content.ReadAsStringAsync(ct);
            if (!response.IsSuccessStatusCode)
            {
                logger.LogWarning("Graph calendarView fejlede for {Upn}: {Status} {Body}",
                    azureUpn, (int)response.StatusCode, Truncate(body, 400));
                throw new HttpRequestException(
                    $"Graph calendarView fejlede ({(int)response.StatusCode})", null, response.StatusCode);
            }

            var page = JsonSerializer.Deserialize<GraphEventPage>(body, JsonOptions)
                ?? new GraphEventPage();
            foreach (var item in page.Value ?? [])
                events.Add(Map(item));

            url = page.NextLink;
        }

        return events;
    }

    private async Task<string> GetAccessTokenAsync(CancellationToken ct)
    {
        if (_cachedToken is not null && _tokenExpiresAt > DateTimeOffset.UtcNow.AddMinutes(1))
            return _cachedToken;

        var http = httpClientFactory.CreateClient("azure-graph");
        using var content = new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["client_id"] = _options.ClientId,
            ["client_secret"] = _options.ClientSecret,
            ["scope"] = "https://graph.microsoft.com/.default",
            ["grant_type"] = "client_credentials"
        });

        var tokenUrl =
            $"https://login.microsoftonline.com/{Uri.EscapeDataString(_options.TenantId)}/oauth2/v2.0/token";
        using var response = await http.PostAsync(tokenUrl, content, ct);
        var body = await response.Content.ReadAsStringAsync(ct);
        if (!response.IsSuccessStatusCode)
        {
            logger.LogError("Azure token-fejl: {Status} {Body}", (int)response.StatusCode, Truncate(body, 400));
            throw new HttpRequestException($"Kunne ikke hente Azure-token ({(int)response.StatusCode})");
        }

        var token = JsonSerializer.Deserialize<TokenResponse>(body, JsonOptions)
            ?? throw new InvalidOperationException("Tom Azure-token-svar");
        _cachedToken = token.AccessToken;
        _tokenExpiresAt = DateTimeOffset.UtcNow.AddSeconds(Math.Max(60, token.ExpiresIn - 60));
        return _cachedToken;
    }

    private static TeacherCalendarEvent Map(GraphEvent e)
    {
        var isAllDay = e.IsAllDay == true;
        var isPrivate = e.Sensitivity is "private" or "personal" or "confidential";
        var start = ParseGraphDate(e.Start, isAllDay);
        var end = ParseGraphDate(e.End, isAllDay);
        return new TeacherCalendarEvent(
            e.Id ?? Guid.NewGuid().ToString("N"),
            isPrivate ? "Privat" : (string.IsNullOrWhiteSpace(e.Subject) ? "(uden titel)" : e.Subject.Trim()),
            start,
            end,
            isAllDay,
            isPrivate ? null : NullIfEmpty(e.Location?.DisplayName),
            !isPrivate && e.IsOnlineMeeting == true,
            isPrivate ? null : NullIfEmpty(e.OnlineMeeting?.JoinUrl),
            isPrivate ? null : NullIfEmpty(e.WebLink),
            isPrivate,
            e.IsCancelled == true,
            NullIfEmpty(e.ShowAs));
    }

    private static DateTimeOffset ParseGraphDate(GraphDateTime? value, bool isAllDay)
    {
        if (value?.DateTime is null)
            return DateTimeOffset.UtcNow;

        var raw = value.DateTime;
        if (isAllDay)
        {
            var date = DateOnly.Parse(raw.Length >= 10 ? raw[..10] : raw);
            return new DateTimeOffset(date.ToDateTime(TimeOnly.MinValue), TimeSpan.Zero);
        }

        // Romance Standard Time / lokal tid uden Z → behandl som local wall-clock (DK).
        if (DateTimeOffset.TryParse(raw, out var dto))
        {
            if (raw.EndsWith("Z", StringComparison.OrdinalIgnoreCase)
                || raw.Contains('+', StringComparison.Ordinal)
                || (raw.Length > 19 && raw[^6] is '+' or '-'))
                return dto;
            var unspecified = DateTime.Parse(raw, null, System.Globalization.DateTimeStyles.AssumeLocal);
            var tz = TimeZoneInfo.FindSystemTimeZoneById(
                OperatingSystem.IsWindows() ? "Romance Standard Time" : "Europe/Copenhagen");
            var utc = TimeZoneInfo.ConvertTimeToUtc(DateTime.SpecifyKind(unspecified, DateTimeKind.Unspecified), tz);
            return new DateTimeOffset(utc);
        }

        return DateTimeOffset.UtcNow;
    }

    private static string? NullIfEmpty(string? value) =>
        string.IsNullOrWhiteSpace(value) ? null : value.Trim();

    private static string Truncate(string value, int max) =>
        value.Length <= max ? value : value[..max] + "…";

    private sealed class TokenResponse
    {
        [JsonPropertyName("access_token")]
        public string AccessToken { get; set; } = "";

        [JsonPropertyName("expires_in")]
        public int ExpiresIn { get; set; }
    }

    private sealed class GraphEventPage
    {
        [JsonPropertyName("value")]
        public List<GraphEvent>? Value { get; set; }

        [JsonPropertyName("@odata.nextLink")]
        public string? NextLink { get; set; }
    }

    private sealed class GraphEvent
    {
        public string? Id { get; set; }
        public string? Subject { get; set; }
        public bool? IsAllDay { get; set; }
        public string? Sensitivity { get; set; }
        public GraphDateTime? Start { get; set; }
        public GraphDateTime? End { get; set; }
        public GraphLocation? Location { get; set; }
        public bool? IsOnlineMeeting { get; set; }
        public GraphOnlineMeeting? OnlineMeeting { get; set; }
        public string? WebLink { get; set; }
        public bool? IsCancelled { get; set; }
        public string? ShowAs { get; set; }
    }

    private sealed class GraphDateTime
    {
        public string? DateTime { get; set; }
        public string? TimeZone { get; set; }
    }

    private sealed class GraphLocation
    {
        public string? DisplayName { get; set; }
    }

    private sealed class GraphOnlineMeeting
    {
        public string? JoinUrl { get; set; }
    }
}
