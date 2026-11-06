using System.Net.Http.Json;
using System.Text.Json;
using Mercantec.Tech.Contracts;
using Microsoft.Extensions.Options;

namespace Mercantec.Tech.Api.Speech;

/// <summary>Calls the repository's offline Røst bridge; destinations never come from a browser request.</summary>
public sealed class RostSpeechGenerator(IHttpClientFactory clients, IOptions<RouletteSpeechOptions> options) : IRouletteSpeechGenerator
{
    private const string Provider = "Røst";

    private Uri Endpoint(string path)
    {
        if (!Uri.TryCreate(options.Value.BaseUrl, UriKind.Absolute, out var uri) || uri.Scheme != Uri.UriSchemeHttp
            || uri.UserInfo.Length > 0 || uri.Query.Length > 0 || uri.Fragment.Length > 0 || uri.AbsolutePath != "/"
            || !(uri.IsLoopback || uri.Host.Equals("host.docker.internal", StringComparison.OrdinalIgnoreCase)))
            throw new RouletteSpeechUnavailableException();
        return new Uri(uri, path);
    }

    public async Task<RouletteSpeechStatus> StatusAsync(CancellationToken ct)
    {
        using var request = new HttpRequestMessage(HttpMethod.Get, Endpoint("health"));
        using var response = await clients.CreateClient("rost").SendAsync(request, HttpCompletionOption.ResponseHeadersRead, ct);
        if (!response.IsSuccessStatusCode) return new(false, Provider, "Røst er ved at starte eller svarer ikke lige nu.");
        var bytes = await ReadBounded(response.Content, 4096, ct);
        try
        {
            using var json = JsonDocument.Parse(bytes);
            var ready = json.RootElement.TryGetProperty("ready", out var value) && value.ValueKind == JsonValueKind.True;
            return new(ready, Provider, ready ? null : "Røst er ved at starte.");
        }
        catch (JsonException) { throw new RouletteSpeechUnavailableException(); }
    }

    public async Task<GeneratedRouletteSpeech> GenerateAsync(string text, CancellationToken ct)
    {
        using var request = new HttpRequestMessage(HttpMethod.Post, Endpoint("speech")) { Content = JsonContent.Create(new { text }) };
        using var response = await clients.CreateClient("rost").SendAsync(request, HttpCompletionOption.ResponseHeadersRead, ct);
        if (!response.IsSuccessStatusCode || response.Content.Headers.ContentType?.MediaType != "audio/wav")
            throw new RouletteSpeechUnavailableException();
        return new(await ReadBounded(response.Content, RouletteSpeechService.MaxAudioBytes, ct), "audio/wav", Provider);
    }

    private static async Task<byte[]> ReadBounded(HttpContent content, int maximum, CancellationToken ct)
    {
        if (content.Headers.ContentLength > maximum) throw new RouletteSpeechUnavailableException();
        await using var input = await content.ReadAsStreamAsync(ct);
        using var output = new MemoryStream();
        var buffer = new byte[81920];
        int count;
        while ((count = await input.ReadAsync(buffer, ct)) > 0)
        {
            if (output.Length + count > maximum) throw new RouletteSpeechUnavailableException();
            await output.WriteAsync(buffer.AsMemory(0, count), ct);
        }
        return output.ToArray();
    }
}
