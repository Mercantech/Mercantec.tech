using Mercantec.Tech.Api.Data.Entities;
using Mercantec.Tech.Contracts;

namespace Mercantec.Tech.Api.Emojis;

/// <summary>One availability and metadata policy for pickers and the editor assistant.</summary>
public static class CharacterEmojiPolicy
{
    public static IReadOnlyList<CharacterEmojiAsset> Available(IEnumerable<CustomEmoji> existing)
    {
        var rows = existing.ToArray();
        var ids = rows.Select(emoji => emoji.Id).ToHashSet();
        var names = rows.Select(emoji => emoji.Name).ToHashSet(StringComparer.OrdinalIgnoreCase);
        // Existing uploads keep their IDs and shortcodes if a legacy row collides.
        return CharacterEmojiCatalog.All.Where(emoji => !ids.Contains(emoji.Id) && !names.Contains(emoji.Name)).ToArray();
    }

    public static EmojiResolvedMetadata Resolve(CharacterEmojiAsset emoji)
    {
        var characterName = emoji.CharacterSlug switch { "lupp" => "LUPP", "beaver" => "bæver", "fox" => "ræv", "chameleon" => "kamæleonen", _ => "raymond" };
        var subject = emoji.CharacterSlug switch
        {
            "lupp" => new[] { "Game Design", "spil", "gameplay" },
            "raymond" => new[] { "SQL", "database" },
            "chameleon" => new[] { "Machine Learning", "machinelearning", "maskinlæring", "ML", "kamæleon", "kameleon", "data", "model" },
            _ => new[] { "sikkerhed", "test", emoji.CharacterSlug == "beaver" ? "blåt team" : "rødt team" }
        };
        var keywords = EmojiMetadataPolicy.Keywords(new[] { characterName, emoji.CharacterSlug, emoji.Title, "emoji", "reaktion" }
            .Concat(subject).Concat(emoji.Name.Split('_')));
        var reaction = emoji.Name[(emoji.CharacterSlug.Length + 1)..];
        var category = reaction switch
        {
            "wave" or "hello" or "thumbs_up" or "applause" => "gestures",
            "coffee" or "tea_time" or "good_morning" or "sleepy" or "goodbye" or "humming" => "everyday",
            "break" when emoji.CharacterSlug == "lupp" => "everyday",
            "help" or "listening" or "question" or "idea_notes" or "reading" => "learning",
            "explaining" or "encouragement"
                or "idea" or "sketch" or "game_loop" or "player" or "rules" or "balance" or "random" or "skill"
                or "feedback" or "prototype" or "mini_gdd" or "playtest" or "level" or "checkpoint"
                or "multiplayer" or "accessibility" when emoji.CharacterSlug == "lupp" => "learning",
            "safe_lock" or "check_logs" or "regression_test" or "debugging" or "testing" or "query_ready"
                or "join" or "filter" or "sort" or "primary_key" or "foreign_key" or "normalize" or "transaction"
                or "commit" or "rollback" or "backup" or "restore" or "performance" or "typing" => "technology",
            "controller" or "input" or "bug" or "fix" when emoji.CharacterSlug == "lupp" => "technology",
            "train_model" or "test_data" or "validation" or "overfitting" or "generalization"
                or "features" or "labels" or "classify" or "predict" or "clustering" or "gradient"
                or "loss" or "accuracy" or "precision_recall" or "bias" or "drift" or "retrain"
                or "data_cleaning" or "missing_data" or "explainability" or "privacy" or "tuning" or "baseline" or "processing"
                when emoji.CharacterSlug == "chameleon" => "technology",
            "violin" or "banjo" when emoji.CharacterSlug == "chameleon" => "everyday",
            _ => "smileys"
        };
        return new(EmojiMetadataPolicy.CleanText(emoji.Alt, EmojiMetadataPolicy.MaxAltText), category, keywords);
    }
}
