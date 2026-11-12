// Colectia vectoriala a Bibliotecii (in memorie): regulamentul + descrieri de carti.
// Se construieste o data, la pornirea aplicatiei.
using CommunityToolkit.VectorData.InMemory;
using Lab1_MVC.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.VectorData;
using System.Globalization;
using System.Text;

namespace Lab1_MVC.Rag;

public class DocumentIndex
{
    // Misiunea 3, Partea B: schimbati modul si reporniti Biblioteca.
    public const string ChunkMode = "sectiuni"; // "document" | "sectiuni" | "propozitii"

    private readonly VectorStoreCollection<string, DocumentChunk> _collection;
    private readonly ILogger<DocumentIndex> _log;
    private readonly List<DocumentChunk> _documents = [];

    public DocumentIndex(
        IEmbeddingGenerator<string, Embedding<float>> embeddings,
        ILogger<DocumentIndex> log)
    {
        _log = log;
        var store = new InMemoryVectorStore(new() { EmbeddingGenerator = embeddings });
        _collection = store.GetCollection<string, DocumentChunk>("biblioteca");
    }

    public int Count { get; private set; }

    public async Task BuildAsync(
        LibraryContext db,
        string regulamentPath,
        string catalogPath,
        CancellationToken ct = default)
    {
        if (!File.Exists(regulamentPath))
            throw new FileNotFoundException("Regulamentul nu a fost găsit.", regulamentPath);
        if (!File.Exists(catalogPath))
            throw new FileNotFoundException("Catalogul de rezervă nu a fost găsit.", catalogPath);

        await _collection.EnsureCollectionExistsAsync(ct);
        _documents.Clear();

        var parts = Chunker.Split(
            await File.ReadAllTextAsync(regulamentPath, ct),
            ChunkMode);
        for (var i = 0; i < parts.Count; i++)
        {
            var chunk = new DocumentChunk
            {
                Id = $"regulament-{i + 1:00}",
                Source = "regulament",
                Title = parts[i].Title,
                Text = parts[i].Text,
                Embedding = parts[i].Text
            };
            await _collection.UpsertAsync(chunk, ct);
            _documents.Add(chunk);
        }

        // Indexam toate cartile. Daca o descriere a fost stearsa prin CRUD,
        // titlul, autorul si genul raman totusi cautabile semantic.
        var books = await db.Book.AsNoTracking()
            .Select(b => new
            {
                b.ID,
                b.Title,
                b.Description,
                Author = b.Author == null ? "" : b.Author.FirstName + " " + b.Author.LastName,
                Genre = b.Genre == null ? "" : b.Genre.Name
            })
            .ToListAsync(ct);

        var fallbackDescriptions = ReadDescriptions(catalogPath);
        var descriptionsCompleted = 0;
        foreach (var book in books)
        {
            var description = book.Description;
            if (string.IsNullOrWhiteSpace(description)
                && fallbackDescriptions.TryGetValue(book.ID, out var fallback))
            {
                description = fallback;
                descriptionsCompleted++;
            }

            var text = $"{book.Title}, de {book.Author} ({book.Genre}). {description ?? ""}";
            var chunk = new DocumentChunk
            {
                Id = $"carte-{book.ID}",
                Source = "carte",
                Title = $"{book.Title} (ID {book.ID})",
                Text = text,
                Embedding = text
            };
            await _collection.UpsertAsync(chunk, ct);
            _documents.Add(chunk);
        }

        Count = parts.Count + books.Count;
        _log.LogInformation(
            "RAG: {Parts} fragmente din regulament ({Mode}) + {Books} carti indexate; {Completed} descrieri completate doar in memorie din CSV.",
            parts.Count,
            ChunkMode,
            books.Count,
            descriptionsCompleted);
    }

    public async Task<List<DocumentHit>> SearchAsync(
        string query,
        int top,
        string? source,
        CancellationToken ct = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(query);

        var options = source is null
            ? null
            : new VectorSearchOptions<DocumentChunk> { Filter = chunk => chunk.Source == source };

        // Embedding-urile rămân baza căutării. Recuperăm mai mulți candidați și îi
        // rerangăm și lexical, deoarece modelele multilingve pot rata flexiuni
        // românești precum „întârzii”/„întârziere” sau „soțul”/„soțului”.
        var semanticScores = new Dictionary<string, double>(StringComparer.Ordinal);
        var candidateCount = Math.Max(top, Count);
        await foreach (var result in _collection.SearchAsync(query, candidateCount, options, ct))
        {
            semanticScores[result.Record.Id] = result.Score ?? 0;
        }

        var candidates = _documents
            .Where(document => source is null || document.Source == source)
            .Select(document =>
            {
                var semantic = semanticScores.GetValueOrDefault(document.Id);
                var lexical = LexicalSimilarity(query, $"{document.Title} {document.Text}");
                _log.LogDebug(
                    "RAG candidat {Id}: semantic={Semantic:F3}, lexical={Lexical:F3}.",
                    document.Id,
                    semantic,
                    lexical);
                return (
                    Chunk: document,
                    Semantic: semantic,
                    Relevance: semantic * 0.50 + lexical * 0.50);
            });

        return candidates
            .OrderByDescending(candidate => candidate.Relevance)
            .ThenByDescending(candidate => candidate.Semantic)
            .Take(top)
            .Select(candidate => new DocumentHit(
                candidate.Chunk.Source,
                candidate.Chunk.Title,
                candidate.Chunk.Text,
                Math.Round(candidate.Relevance, 3)))
            .ToList();
    }

    private static Dictionary<int, string> ReadDescriptions(string path)
    {
        var descriptions = new Dictionary<int, string>();
        foreach (var line in File.ReadLines(path).Skip(1))
        {
            if (string.IsNullOrWhiteSpace(line))
                continue;

            var fields = ParseCsvLine(line);
            if (fields.Count == 8 && int.TryParse(fields[0], out var id))
                descriptions[id] = fields[7];
        }

        return descriptions;
    }

    private static List<string> ParseCsvLine(string line)
    {
        var fields = new List<string>();
        var current = new StringBuilder();
        var quoted = false;
        for (var index = 0; index < line.Length; index++)
        {
            var character = line[index];
            if (character == '"' && quoted && index + 1 < line.Length && line[index + 1] == '"')
            {
                current.Append('"');
                index++;
            }
            else if (character == '"')
            {
                quoted = !quoted;
            }
            else if (character == ',' && !quoted)
            {
                fields.Add(current.ToString());
                current.Clear();
            }
            else
            {
                current.Append(character);
            }
        }

        fields.Add(current.ToString());
        return fields;
    }

    private static double LexicalSimilarity(string query, string text)
    {
        var queryTokens = Tokens(query)
            .Where(token => !StopWords.Contains(token))
            .Distinct(StringComparer.Ordinal)
            .ToArray();
        if (queryTokens.Length == 0)
            return 0;

        var textTokens = Tokens(text).ToHashSet(StringComparer.Ordinal);
        var matches = queryTokens.Count(queryToken =>
            textTokens.Any(textToken => RelatedWords(queryToken, textToken)));
        return (double)matches / queryTokens.Length;
    }

    private static IEnumerable<string> Tokens(string value)
    {
        var normalized = value.Normalize(NormalizationForm.FormD);
        var builder = new StringBuilder(normalized.Length);
        foreach (var character in normalized)
        {
            var category = CharUnicodeInfo.GetUnicodeCategory(character);
            if (category == UnicodeCategory.NonSpacingMark)
                continue;

            builder.Append(char.IsLetterOrDigit(character)
                ? char.ToLowerInvariant(character)
                : ' ');
        }

        return builder.ToString()
            .Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
    }

    private static bool RelatedWords(string left, string right)
    {
        if (left == right)
            return true;
        if (left.Length < 4 || right.Length < 4)
            return false;

        return left.AsSpan(0, 4).SequenceEqual(right.AsSpan(0, 4));
    }

    private static readonly HashSet<string> StopWords = new(StringComparer.Ordinal)
    {
        "a", "ai", "ale", "al", "am", "cat", "care", "carte", "carti", "cu",
        "de", "despre", "din", "este", "fi", "in", "la", "ma", "o", "pot",
        "poate", "sa", "si", "un", "unei", "unui"
    };
}
