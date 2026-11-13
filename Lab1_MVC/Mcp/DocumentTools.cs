// Agentul din I7 il descopera singur (tools/list), fara modificari de cod.
using System.ComponentModel;
using Lab1_MVC.Rag;
using ModelContextProtocol.Server;

namespace Lab1_MVC.Mcp;

[McpServerToolType]
public class DocumentTools
{
    [McpServerTool(Name = "search_documents", ReadOnly = true, Destructive = false, OpenWorld = false)]
    [Description("Caută după sens în regulamentul Bibliotecii, regulamentul fictiv al concursului anual de recenzii " +
                 "și descrierile catalogului de cărți. Folosește source='regulament' pentru împrumut, termene, " +
                 "penalități, program și sala de lectură; source='concurs-recenzii' pentru eligibilitate, calendar, " +
                 "format, punctaj, premii și contestații; source='carte' când cititorul descrie subiectul unei cărți. " +
                 "Întoarce fragmentele cele mai apropiate de întrebare, cu sursa, titlul și scorul de relevanță (0–1).")]
    public static Task<List<DocumentHit>> SearchDocuments(
        DocumentIndex index,
        [Description("Întrebarea sau descrierea, în cuvintele cititorului.")] string query,
        [Description("Sursa exactă: 'regulament' = regulamentul bibliotecii; 'concurs-recenzii' = concursul anual fictiv; " +
                     "'carte' = catalogul de cărți; 'toate' = toate sursele. Omite argumentul pentru toate sursele.")] string? source = null,
        [Description("Câte fragmente (1–8). Implicit 3.")] int top = 3,
        CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(query))
            throw new ArgumentException("Întrebarea nu poate fi goală.", nameof(query));

        var requestedSource = source?.Trim().ToLowerInvariant();
        var normalizedSource = requestedSource switch
        {
            null or "" or "toate" or "ambele" or "both" => null,
            "regulament" or "concurs-recenzii" or "carte" => requestedSource,
            _ => throw new ArgumentException(
                "Sursa trebuie să fie 'regulament', 'concurs-recenzii', 'carte' sau 'toate'.",
                nameof(source))
        };
        return index.SearchAsync(query, Math.Clamp(top, 1, 8), normalizedSource, ct);
    }
}
