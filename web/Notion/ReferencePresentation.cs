namespace Mercantec.Tech.Web.Notion;

public static class ReferencePresentation
{
    public static string KindLabel(string kind) => kind switch
    {
        "notion-page" => "Undervisningsside", "notebook-page" => "Note / logbog", "collection" => "Samling",
        "notebook" => "Personlig notesbog", "hold" => "Hold", "teaching-course" => "Forløb", "quest" => "Opgave",
        "material" => "Materiale", "poll" => "Afstemning", "person" or "member" => "Person", "group" => "Gruppe",
        "event" => "Kalender", "submission" => "Aflevering", "file" or "notion-asset" or "notebook-asset" => "Fil",
        "drawing" => "Tegning", "learning-plan" => "Undervisningsplan", "subject" => "Fag", "lesson" => "Emne",
        "project" => "Projekt", "badge" => "Badge", "character" => "Karakter", _ => "Indhold"
    };
}
