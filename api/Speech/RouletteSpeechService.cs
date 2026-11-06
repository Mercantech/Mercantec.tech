using System.Security.Cryptography;
using System.Text;
using Mercantec.Tech.Contracts;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Options;

namespace Mercantec.Tech.Api.Speech;

public sealed class RouletteSpeechOptions
{
    public const string SectionName = "Rost";
    public bool Enabled { get; set; }
    public string BaseUrl { get; set; } = "http://127.0.0.1:8091";
    public int TimeoutSeconds { get; set; } = 30;
}

/// <summary>Keeps prepared clips private to their teacher and hold, and bounds local model work.</summary>
public sealed class RouletteSpeechService(IRouletteSpeechGenerator generator, IOptions<RouletteSpeechOptions> options) : IDisposable
{
    public const int MaxAudioBytes = 8 * 1024 * 1024;
    private readonly MemoryCache cache = new(new MemoryCacheOptions { SizeLimit = 32 * 1024 * 1024 });
    private readonly SemaphoreSlim generation = new(1);

    public async Task<RouletteSpeechStatus> StatusAsync(CancellationToken ct)
    {
        if (!options.Value.Enabled) return new(false, "Røst", "Lokal oplæsning er ikke tilsluttet på denne server.");
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(ct);
        timeout.CancelAfter(TimeSpan.FromSeconds(3));
        try { return await generator.StatusAsync(timeout.Token); }
        catch (Exception ex) when (!ct.IsCancellationRequested && ex is HttpRequestException or OperationCanceledException or RouletteSpeechUnavailableException)
        { return new(false, "Røst", "Den lokale Røst-tjeneste svarer ikke lige nu."); }
    }

    public async Task<RouletteSpeechAudio> GenerateAsync(Guid teacherId, Guid holdId, string text, CancellationToken ct)
    {
        if (!options.Value.Enabled) throw new RouletteSpeechUnavailableException();
        var key = (teacherId, holdId, Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(text))));
        if (cache.TryGetValue<RouletteSpeechAudio>(key, out var prepared)) return prepared!;
        if (!await generation.WaitAsync(TimeSpan.FromSeconds(2), ct)) throw new RouletteSpeechUnavailableException();
        try
        {
            if (cache.TryGetValue<RouletteSpeechAudio>(key, out prepared)) return prepared!;
            using var timeout = CancellationTokenSource.CreateLinkedTokenSource(ct);
            timeout.CancelAfter(TimeSpan.FromSeconds(Math.Clamp(options.Value.TimeoutSeconds, 1, 60)));
            GeneratedRouletteSpeech audio;
            try { audio = await generator.GenerateAsync(text, timeout.Token); }
            catch (Exception ex) when (!ct.IsCancellationRequested && ex is HttpRequestException or OperationCanceledException or InvalidDataException)
            { throw new RouletteSpeechUnavailableException(); }
            if (audio.ContentType != "audio/wav" || audio.Bytes.Length is < 44 or > MaxAudioBytes
                || !audio.Bytes.AsSpan(0, 4).SequenceEqual("RIFF"u8) || !audio.Bytes.AsSpan(8, 4).SequenceEqual("WAVE"u8))
                throw new RouletteSpeechUnavailableException();
            prepared = new(Convert.ToBase64String(audio.Bytes), audio.ContentType, audio.Provider);
            cache.Set(key, prepared, new MemoryCacheEntryOptions { AbsoluteExpirationRelativeToNow = TimeSpan.FromHours(1),
                Size = prepared.AudioBase64.Length * sizeof(char) });
            return prepared;
        }
        finally { generation.Release(); }
    }

    public void Dispose() { cache.Dispose(); generation.Dispose(); }
}
