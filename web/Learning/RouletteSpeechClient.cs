using System.Net.Http.Json;
using Mercantec.Tech.Contracts;

namespace Mercantec.Tech.Web.Learning;

public sealed class RouletteSpeechClient(HttpClient http)
{
    public async Task<RouletteSpeechStatus> Status(Guid holdId, CancellationToken ct)
    {
        using var response = await http.GetAsync($"holds/{holdId}/roulette/speech", ct);
        if (!response.IsSuccessStatusCode)
            return new(false, "Røst", "Røst er ikke tilgængelig lige nu. Du kan bruge browserstemmen.");
        return await response.Content.ReadFromJsonAsync<RouletteSpeechStatus>(ct)
            ?? new(false, "Røst", "Stemmens status kunne ikke hentes.");
    }

    public async Task<RouletteSpeechAudio> Generate(Guid holdId, string groupName, CancellationToken ct)
    {
        using var response = await http.PostAsJsonAsync($"holds/{holdId}/roulette/speech",
            new CreateRouletteSpeech(groupName, [], false), ct);
        if (!response.IsSuccessStatusCode)
            throw new LearningException(response.StatusCode == System.Net.HttpStatusCode.ServiceUnavailable
                ? "Røst kunne ikke lave oplæsningen lige nu. Browserstemmen bliver brugt i stedet."
                : "Oplæsningen kunne ikke hentes. Prøv stemmen igen.");
        var audio = await response.Content.ReadFromJsonAsync<RouletteSpeechAudio>(ct);
        return audio is not null && audio.ContentType == "audio/wav" && audio.AudioBase64 is { Length: > 0 and <= 12_000_000 }
            ? audio : throw new LearningException("Stemmen returnerede ikke en gyldig lydfil.");
    }
}
