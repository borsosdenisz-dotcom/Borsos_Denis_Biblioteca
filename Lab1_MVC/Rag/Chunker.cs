namespace Lab1_MVC.Rag;

// Imparte un document Markdown in fragmente (chunks).
// Trei moduri (Misiunea 3):
//   "document" = tot documentul este un singur fragment
//   "sectiuni" = cate un fragment pentru fiecare titlu "## ..." (implicit)
//   "propozitii" = cate un fragment pentru fiecare propozitie
public static class Chunker
{
    public static List<(string Title, string Text)> Split(string markdown, string mode)
    {
        var sections = new List<(string Title, string Text)>();
        string? title = null;
        var body = new List<string>();

        foreach (var raw in markdown.Split('\n'))
        {
            var line = raw.TrimEnd('\r');
            if (line.StartsWith("## "))
            {
                if (title is not null) sections.Add((title, string.Join(" ", body).Trim()));
                title = line[3..].Trim();
                body.Clear();
            }
            else if (title is not null && line.Trim().Length > 0 && !line.StartsWith(">"))
            {
                body.Add(line.Trim());
            }
        }
        if (title is not null) sections.Add((title, string.Join(" ", body).Trim()));

        return mode switch
        {
            "document" => [("Regulamentul Bibliotecii (tot documentul)",
                            string.Join("\n", sections.Select(s => $"{s.Title}. {s.Text}")))],

            "propozitii" => sections
                .SelectMany(s => s.Text
                    .Split(". ", StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                    .Select((p, i) => ($"{s.Title} ({i + 1})", $"{s.Title}: {p.TrimEnd('.')}.")))
                .ToList(),

            _ => sections.Select(s => (s.Title, $"{s.Title}. {s.Text}")).ToList()
        };
    }
}
