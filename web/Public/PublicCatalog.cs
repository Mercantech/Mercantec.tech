using Mercantec.Tech.Web.Courses;
using Mercantec.Tech.Web.Demo;
using Mercantec.Tech.Web.Characters;

namespace Mercantec.Tech.Web.Public;

// Editorial public content only. Hold publication and DemoCatalog.Project.Visibility
// are deliberately not used here: shared with a class does not mean public.
public static class PublicCatalog
{
    public static IReadOnlyList<PublicCourse> FeaturedCourses { get; } =
    [
        new("h1", "H1", "Grundlæggende programmering", "code", "public/h1-course.svg"),
        new("h2", "H2", "Web og databaser", "database", "public/h2-course.svg"),
        new("h3", "H3", "IoT og programmering", "chip", "h3-cover.jpg"),
        new("game-design", "Game Design", "Spiludvikling i Unity", "game", "game-cover.jpg")
    ];

    public static IReadOnlyList<PublicSubject> Subjects { get; } =
    [
        new("programming", "C# og .NET", "C#, objektorientering og applikationer.", "code", "h1", null),
        new("web", "Webudvikling", "Frontend, API-kald og visning af data.", "chart", "h3", "dashboard"),
        new("database", "Databaser", "Datamodeller, relationer og lagring.", "database", "h3", "database"),
        new("iot", "IoT og elektronik", "Arduino, sensorer og kommunikation.", "chip", "h3", "iot"),
        new("game-design", "Game design", "Regler, core loop og afprøvning af spil.", "game", "game-design", "design"),
        new("collaboration", "Git og samarbejde", "Versionsstyring, projektarbejde og dokumentation.", "loop", "h1", null)
    ];

    public static IReadOnlyList<PublicProject> Projects { get; } =
    [
        new("sensorstation", "Sensorstation", "IoT", "h3-cover.jpg", "h3",
            "Et undervisningseksempel på en sensorstation, der sender målinger til et API.",
            ["En Arduino aflæser temperatur og luftfugtighed.", "Et API validerer og gemmer målingerne.", "Et dashboard viser værdi, enhed og tidspunkt."],
            ["Arduino og C++", ".NET API", "Database", "Dashboard"]),
        new("platform-game", "Platformspil", "Game design", "game-cover.jpg", "game-design",
            "Et undervisningseksempel på et lille spil med to ruter og forskellige risici.",
            ["Spilleren kan vælge en sikker omvej eller en kortere rute med forhindringer.", "Regler og feedback gør valgene forståelige.", "En playtest bruges til at justere sværhedsgraden."],
            ["Unity", "Core loop", "Risiko og belønning", "Playtest"]),
        new("workshop-data", "Data fra værkstedet", "Web", "public/workshop-dashboard.svg", "h3",
            "Et undervisningseksempel på en webvisning af data fra forbundne enheder.",
            ["Webklienten henter målinger fra et API.", "Visningen viser aktuelle værdier og udviklingen over tid.", "Tomme data og forbindelsesfejl håndteres i grænsefladen."],
            ["Blazor", "API-kald", "Grafer", "Fejlhåndtering"])
    ];

    // Public definitions omit personal ranks, ownership and award history.
    public static IReadOnlyList<PublicBadge> Badges { get; } = DemoCatalog.Badges
        .Select(b => new PublicBadge(b.Id, b.Name, b.Topic, b.Desc, b.Icon, b.Special)).ToArray();

    public static PublicSubject? FindSubject(string? id) => Subjects.FirstOrDefault(s => s.Id == id);
    public static PublicProject? FindProject(string? id) => Projects.FirstOrDefault(p => p.Id == id);
    public static PublicBadge? FindBadge(string? id) => Badges.FirstOrDefault(b => b.Id == id);

    public static IEnumerable<LearningContent.Lesson> Lessons(PublicSubject subject) =>
        subject.LessonSubject is null ? [] : LearningContent.Lessons.Where(l => l.Subject == subject.LessonSubject);

    public static IEnumerable<PublicSearchHit> Search(string? query)
    {
        var hits = CourseTemplateCatalog.All.Select(c => new PublicSearchHit("Forløb", c.Short + " · " + c.Title, c.Lead, c.Path))
            .Concat(Subjects.Select(s => new PublicSearchHit("Fag", s.Name, s.Description, s.Path)))
            .Concat(Subjects.SelectMany(s => Lessons(s).Select(l => new PublicSearchHit("Emne", l.Name, l.Lead, s.Path + "/" + l.Id))))
            .Concat(Projects.Select(p => new PublicSearchHit("Projekt · DEMO", p.Name, p.Description, p.Path)))
            .Concat(Badges.Select(b => new PublicSearchHit("Badgekrav", b.Name, b.Description, "/badges/" + b.Id)))
            .Concat(CharacterCatalog.All.Select(c => new PublicSearchHit("Karakter", c.Name, c.Role + " · " + c.Summary, c.Path)));
        var words = (query ?? "").Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        return words.Length == 0 ? hits.Take(12) : hits.Where(h => words.All(w =>
            (h.Title + " " + h.Description + " " + h.Kind).Contains(w, StringComparison.OrdinalIgnoreCase)));
    }
}

public sealed record PublicCourse(string Slug, string Name, string Subject, string Icon, string Cover)
{
    public CourseTemplate Plan => CourseTemplateCatalog.Find(Slug)!;
    public string PreviewText => Slug switch { "h1" => "C#, OOP, SQL og Blazor.", "h2" => ".NET API, EF Core og Blazor.", "h3" => "Arduino, API og databaser.", _ => "Spilmekanik, prototyper og Unity." };
    public string? SubjectPath => Slug switch { "h1" => "/subjects/programming", "h2" => "/subjects/web", "h3" => "/subjects/iot", "game-design" => "/subjects/game-design", _ => null };
}
public sealed record PublicSubject(string Id, string Name, string Description, string Icon, string CourseSlug, string? LessonSubject)
{
    public string Path => "/subjects/" + Id;
    public CourseTemplate Plan => CourseTemplateCatalog.Find(CourseSlug)!;
}
public sealed record PublicProject(string Id, string Name, string Category, string Cover, string CourseSlug, string Description, string[] Features, string[] Technologies)
{
    public string Path => "/explore/projects/" + Id;
    public bool IsDemo => true;
}
public sealed record PublicBadge(string Id, string Name, string Topic, string Description, string Icon, bool Special);
public sealed record PublicSearchHit(string Kind, string Title, string Description, string Href);
