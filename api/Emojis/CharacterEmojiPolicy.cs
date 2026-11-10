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
        var characterName = emoji.CharacterSlug switch
        {
            "lupp" => "LUPP",
            "beaver" => "bæver",
            "fox" => "ræv",
            "chameleon" => "kamæleonen",
            "whale" => "Docker",
            "gummianden" => "Gummianden",
            "bee" => "Cyberbien",
            _ => "raymond"
        };
        var subject = emoji.CharacterSlug switch
        {
            "lupp" => new[] { "Game Design", "spil", "gameplay" },
            "raymond" => new[] { "SQL", "database" },
            "chameleon" => new[] { "Machine Learning", "machinelearning", "maskinlæring", "ML", "kamæleon", "kameleon", "data", "model" },
            "whale" => new[] { "hval", "havnekaptajn", "DevNet", "Docker", "container", "Compose", "deploy", "deployment" },
            "gummianden" => new[] { "gummiand", "debugging", "fejlfinding", "rubber duck", "VATek" },
            "bee" => new[] { "cyber", "cybersikkerhed", "sikkerhed", "security", "bi" },
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
            "dockerfile" or "build_image" or "image_tag" or "registry" or "push" or "pull"
                or "container_run" or "compose" or "environment" or "port_mapping" or "volume"
                or "network" or "health_check" or "logs" or "metrics" or "ci_pipeline"
                or "staging" or "deploy" or "canary" or "rollout" or "outage"
                when emoji.CharacterSlug == "whale" => "technology",
            "coffee_break" or "tugboat" or "tide_log" when emoji.CharacterSlug == "whale" => "everyday",
            "idea" or "explaining" or "hypothesis" or "pair_debugging"
                when emoji.CharacterSlug == "gummianden" => "learning",
            "reproduce" or "minimal_repro" or "expected_actual" or "isolate" or "first_failure"
                or "breakpoint" or "continue" or "step_over" or "step_into" or "step_out" or "breakpoint_condition"
                or "locals" or "watch" or "call_stack" or "stack_trace" or "logs" or "log_point"
                or "empty_input" or "null_value" or "boundary" or "off_by_one" or "exception" or "timeout"
                or "red_test" or "green_test" or "fix" or "verified"
                when emoji.CharacterSlug == "gummianden" => "technology",
            "idea" when emoji.CharacterSlug == "bee" => "learning",
            "teamwork" when emoji.CharacterSlug == "bee" => "gestures",
            "honeypot" or "decoy_service" or "isolation" or "honeytoken" or "alert" or "observe"
                or "phishing" or "mfa" or "password_manager" or "access_control" or "least_privilege" or "revoke_access"
                or "network_segmentation" or "firewall" or "patch" or "secure_config"
                or "log_review" or "anomaly" or "timeline" or "correlate" or "false_positive" or "scan"
                or "triage" or "containment" or "incident_report" or "escalate" or "all_clear"
                when emoji.CharacterSlug == "bee" => "technology",
            _ => "smileys"
        };
        return new(EmojiMetadataPolicy.CleanText(emoji.Alt, EmojiMetadataPolicy.MaxAltText), category, keywords);
    }
}
