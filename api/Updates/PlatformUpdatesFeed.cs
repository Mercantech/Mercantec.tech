using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.RegularExpressions;
using Mercantec.Tech.Contracts;

namespace Mercantec.Tech.Api.Updates;

public sealed class PlatformUpdatesFeed
{
    private const string NotesPrefix = "platform-updates/notes/";
    private static readonly JsonSerializerOptions NoteJson = new(JsonSerializerDefaults.Web)
    {
        UnmappedMemberHandling = JsonUnmappedMemberHandling.Disallow
    };
    private static readonly Regex Slug = new(@"\A[a-z0-9]+(?:-[a-z0-9]+)*\z", RegexOptions.CultureInvariant);
    private static readonly System.Reflection.Assembly Assembly = typeof(PlatformUpdatesFeed).Assembly;
    private readonly HashSet<string> _imageIds;

    public PlatformUpdatesFeed(ILogger<PlatformUpdatesFeed> logger)
    {
        var curated = PlatformUpdatesCatalog.Entries;
        var ids = curated.Select(update => update.Id).ToHashSet(StringComparer.Ordinal);
        var resources = Assembly.GetManifestResourceNames().ToHashSet(StringComparer.Ordinal);
        var notes = new List<PlatformUpdate>();

        foreach (var resource in resources.Where(name => name.StartsWith(NotesPrefix, StringComparison.Ordinal))
                     .Order(StringComparer.Ordinal))
        {
            var id = resource[NotesPrefix.Length..^".json".Length];
            try
            {
                using var source = Assembly.GetManifestResourceStream(resource)!;
                var note = JsonSerializer.Deserialize<ReleaseNote>(source, NoteJson);
                if (!IsValid(id, note, resources) || !ids.Add(id))
                {
                    logger.LogWarning("Skipping invalid or duplicate LUP release note {Id}", id);
                    continue;
                }

                notes.Add(new PlatformUpdate(id, note!.Date, note.Title, note.Description, note.After)
                {
                    Authors = note.Authors ?? []
                });
            }
            catch (JsonException)
            {
                logger.LogWarning("Skipping malformed JSON for LUP release note {Id}", id);
            }
        }

        Entries = Array.AsReadOnly(notes.Concat(curated).OrderByDescending(update => update.Date).ToArray());
        WithImages = Array.AsReadOnly(Entries.Where(update => update.After is not null).ToArray());
        _imageIds = Entries.SelectMany(update => new[] { update.After, update.Before })
            .OfType<UpdateImage>().Select(image => image.Id).ToHashSet(StringComparer.Ordinal);
    }

    public IReadOnlyList<PlatformUpdate> Entries { get; }
    public IReadOnlyList<PlatformUpdate> WithImages { get; }

    public Stream? OpenImage(string id) => _imageIds.Contains(id)
        ? Assembly.GetManifestResourceStream(ImageResource(id))
        : null;

    private static string ImageResource(string id) => $"platform-updates/{id}.webp";

    private static bool IsValid(string id, ReleaseNote? note, HashSet<string> resources) =>
        id.Length <= 80 && Slug.IsMatch(id)
        && note is { Date: var date } && date != default
        && IsText(note.Title, 100) && IsText(note.Description, 500)
        && (note.Authors is null || note.Authors.All(author => author is "Mathias" or "Kasper")
            && note.Authors.Distinct(StringComparer.Ordinal).Count() == note.Authors.Length)
        && (note.After is null || IsText(note.After.Id, 80) && Slug.IsMatch(note.After.Id)
            && IsText(note.After.Alt, 200) && resources.Contains(ImageResource(note.After.Id)));

    private static bool IsText(string? text, int limit) => !string.IsNullOrWhiteSpace(text) && text.Length <= limit;

    private sealed record ReleaseNote(DateOnly Date, string Title, string Description, string[]? Authors, UpdateImage? After);
}
