namespace Mercantec.Tech.Api.Emojis;

public sealed record EmojiMetadata(string Category, string[] Keywords);

public static class StandardEmojiMetadata
{
    // Asset keys remain stable when teachers rename the corresponding shortcodes.
    private static readonly IReadOnlyDictionary<string, EmojiMetadata> Items = new Dictionary<string, EmojiMetadata>(StringComparer.Ordinal)
    {
        ["standard-emojis/1f604.png"] = new("smileys", ["smile", "smil", "glad", "happy", "ansigt", "😄"]),
        ["standard-emojis/1f601.png"] = new("smileys", ["grin", "smil", "glad", "happy", "ansigt", "😁"]),
        ["standard-emojis/1f602.png"] = new("smileys", ["joy", "grin", "latter", "sjov", "laugh", "tårer", "😂"]),
        ["standard-emojis/1f609.png"] = new("smileys", ["wink", "blink", "øje", "ansigt", "😉"]),
        ["standard-emojis/1f60d.png"] = new("smileys", ["heart eyes", "hjerte", "kærlighed", "elsker", "love", "😍"]),
        ["standard-emojis/1f914.png"] = new("smileys", ["thinking", "tænke", "tænker", "hmm", "overveje", "🤔"]),
        ["standard-emojis/1f62e.png"] = new("smileys", ["surprised", "overrasket", "wow", "ansigt", "😮"]),
        ["standard-emojis/1f614.png"] = new("smileys", ["sad", "trist", "ked af det", "ansigt", "😔"]),
        ["standard-emojis/1f622.png"] = new("smileys", ["cry", "græde", "græder", "trist", "tåre", "😢"]),
        ["standard-emojis/1f60e.png"] = new("smileys", ["cool", "solbriller", "sej", "sunglasses", "😎"]),
        ["standard-emojis/1f44f.png"] = new("gestures", ["clap", "klap", "applaus", "hænder", "bravo", "👏"]),
        ["standard-emojis/1f44d.png"] = new("gestures", ["thumbs up", "tommel op", "godt", "enig", "ja", "👍"]),
        ["standard-emojis/1f44e.png"] = new("gestures", ["thumbs down", "tommel ned", "uenig", "nej", "👎"]),
        ["standard-emojis/1f44b.png"] = new("gestures", ["wave", "vink", "hej", "farvel", "hånd", "👋"]),
        ["standard-emojis/1f91d.png"] = new("gestures", ["handshake", "håndtryk", "aftale", "samarbejde", "🤝"]),
        ["standard-emojis/2764.png"] = new("symbols", ["heart", "hjerte", "kærlighed", "love", "❤", "❤️"]),
        ["standard-emojis/2b50.png"] = new("symbols", ["star", "stjerne", "favorit", "⭐"]),
        ["standard-emojis/2728.png"] = new("symbols", ["sparkles", "glimmer", "glimt", "magi", "✨"]),
        ["standard-emojis/1f389.png"] = new("symbols", ["party", "fest", "fejring", "tillykke", "konfetti", "🎉"]),
        ["standard-emojis/1f525.png"] = new("symbols", ["fire", "ild", "varm", "flamme", "🔥"]),
        ["standard-emojis/2705.png"] = new("symbols", ["check", "flueben", "færdig", "godkendt", "done", "✅"]),
        ["standard-emojis/274c.png"] = new("symbols", ["cross", "kryds", "fejl", "forkert", "nej", "❌"]),
        ["standard-emojis/26a0.png"] = new("symbols", ["warning", "advarsel", "pas på", "vigtigt", "⚠", "⚠️"]),
        ["standard-emojis/2753.png"] = new("symbols", ["question", "spørgsmål", "hjælp", "hvad", "❓"]),
        ["standard-emojis/2139.png"] = new("symbols", ["info", "information", "oplysning", "ℹ", "ℹ️"]),
        ["standard-emojis/1f3af.png"] = new("symbols", ["target", "mål", "fokus", "bullseye", "🎯"]),
        ["standard-emojis/1f3c6.png"] = new("symbols", ["trophy", "pokal", "vinder", "sejr", "præmie", "🏆"]),
        ["standard-emojis/1f4a1.png"] = new("learning", ["bulb", "pære", "ide", "idé", "inspiration", "💡"]),
        ["standard-emojis/1f4d6.png"] = new("learning", ["book", "bog", "læse", "læsning", "læring", "📖"]),
        ["standard-emojis/1f4da.png"] = new("learning", ["books", "bøger", "bibliotek", "studie", "læring", "📚"]),
        ["standard-emojis/270f.png"] = new("learning", ["pencil", "blyant", "skrive", "redigere", "✏", "✏️"]),
        ["standard-emojis/1f4dd.png"] = new("learning", ["note", "notat", "noter", "skrive", "opgave", "📝"]),
        ["standard-emojis/1f680.png"] = new("technology", ["rocket", "raket", "start", "lancering", "🚀"]),
        ["standard-emojis/1f4bb.png"] = new("technology", ["computer", "bærbar", "laptop", "kode", "programmering", "💻"]),
        ["standard-emojis/1f6e0.png"] = new("technology", ["tools", "værktøj", "bygge", "reparation", "🛠", "🛠️"]),
        ["standard-emojis/1f41b.png"] = new("technology", ["bug", "fejl", "insekt", "debug", "🐛"]),
        ["standard-emojis/1f512.png"] = new("technology", ["lock", "lås", "låst", "sikkerhed", "privat", "🔒"]),
        ["standard-emojis/1f4c5.png"] = new("everyday", ["calendar", "kalender", "dato", "plan", "📅"]),
        ["standard-emojis/23f0.png"] = new("everyday", ["clock", "ur", "tid", "alarm", "vækkeur", "⏰"]),
        ["standard-emojis/2615.png"] = new("everyday", ["coffee", "kaffe", "pause", "kop", "☕"])
    };

    public static EmojiMetadata? Find(string key) => Items.GetValueOrDefault(key);
}
