using Mercantec.Tech.Contracts;

namespace Mercantec.Tech.Api.Speech;

public sealed record GeneratedRouletteSpeech(byte[] Bytes, string ContentType, string Provider);

public interface IRouletteSpeechGenerator
{
    Task<RouletteSpeechStatus> StatusAsync(CancellationToken ct);
    Task<GeneratedRouletteSpeech> GenerateAsync(string text, CancellationToken ct);
}

public sealed class RouletteSpeechUnavailableException : Exception { }
