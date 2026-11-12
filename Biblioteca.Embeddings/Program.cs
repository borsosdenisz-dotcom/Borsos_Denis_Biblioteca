using System.Globalization;
using CommunityToolkit.VectorData.InMemory;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.VectorData;
using OllamaSharp;

var ollamaUri = Environment.GetEnvironmentVariable("OLLAMA_ENDPOINT")
    ?? "http://localhost:11434";
var model = Environment.GetEnvironmentVariable("OLLAMA_EMBEDDING_MODEL")
    ?? "embeddinggemma";
var catalogPath = Path.Combine(AppContext.BaseDirectory, "books.csv");

try
{
    IEmbeddingGenerator<string, Embedding<float>> generator =
        new OllamaApiClient(new Uri(ollamaUri), model);

    Console.Write($"Model: {model}\n1 Vector · 2 Similarități · 3 Căutare în catalog > ");
    switch (Console.ReadLine())
    {
        case "1":
            await ShowVectorAsync(generator);
            break;
        case "2":
            await ShowSimilaritiesAsync(generator);
            break;
        default:
            await SearchCatalogAsync(generator, catalogPath);
            break;
    }
}
catch (Exception exception) when (exception is HttpRequestException or TaskCanceledException)
{
    Console.Error.WriteLine($"Nu se poate contacta Ollama la {ollamaUri}: {exception.Message}");
    Console.Error.WriteLine($"Porniți Ollama și instalați modelul cu: ollama pull {model}");
    Environment.ExitCode = 1;
}

static async Task ShowVectorAsync(IEmbeddingGenerator<string, Embedding<float>> generator)
{
    while (true)
    {
        Console.Write("\nText (Enter = ieșire): ");
        var text = Console.ReadLine();
        if (string.IsNullOrWhiteSpace(text))
        {
            return;
        }

        ReadOnlyMemory<float> vector = await generator.GenerateVectorAsync(text);
        Console.WriteLine($"Dimensiune: {vector.Length}");
        Console.WriteLine("Primele 8 valori: " +
            string.Join("  ", vector.Span[..Math.Min(8, vector.Length)].ToArray()
                .Select(value => value.ToString("F4", CultureInfo.InvariantCulture))));
    }
}

static async Task ShowSimilaritiesAsync(IEmbeddingGenerator<string, Embedding<float>> generator)
{
    string[] phrases =
    [
        "A: Pot împrumuta o carte pentru două săptămâni?",
        "B: Care este durata unui împrumut?",
        "C: Cât costă dacă întârzii cu returnarea?",
        "D: Ce penalitate primesc pentru o carte adusă mai târziu?",
        "E: Rețetă de sarmale cu smântână.",
        "F: How long can I borrow a book?"
    ];
    GeneratedEmbeddings<Embedding<float>> embeddings = await generator.GenerateAsync(phrases);

    foreach (var phrase in phrases)
    {
        Console.WriteLine(phrase);
    }

    Console.Write("\n     ");
    for (var column = 0; column < phrases.Length; column++)
    {
        Console.Write($"   {phrases[column][0]}  ");
    }

    Console.WriteLine();
    for (var row = 0; row < phrases.Length; row++)
    {
        Console.Write($"  {phrases[row][0]}  ");
        for (var column = 0; column < phrases.Length; column++)
        {
            Console.Write($" {CosineSimilarity(embeddings[row].Vector.Span, embeddings[column].Vector.Span):F3}");
        }

        Console.WriteLine();
    }
}

static async Task SearchCatalogAsync(
    IEmbeddingGenerator<string, Embedding<float>> generator,
    string catalogPath)
{
    if (!File.Exists(catalogPath))
    {
        throw new FileNotFoundException("Catalogul books.csv nu a fost găsit.", catalogPath);
    }

    var store = new InMemoryVectorStore(new() { EmbeddingGenerator = generator });
    VectorStoreCollection<int, BookVector> books = store.GetCollection<int, BookVector>("carti");
    await books.EnsureCollectionExistsAsync();

    var indexed = 0;
    foreach (var book in ReadCatalog(catalogPath))
    {
        await books.UpsertAsync(book);
        indexed++;
    }

    Console.WriteLine($"Catalogul a fost indexat ({indexed} cărți).");
    while (true)
    {
        Console.Write("\nCe fel de carte căutați? (Enter = ieșire): ");
        var query = Console.ReadLine();
        if (string.IsNullOrWhiteSpace(query))
        {
            return;
        }

        await foreach (VectorSearchResult<BookVector> result in books.SearchAsync(query, top: 3))
        {
            Console.WriteLine($"[{result.Score:F3}] {result.Record.Title} · {result.Record.Description}");
        }
    }
}

static double CosineSimilarity(ReadOnlySpan<float> left, ReadOnlySpan<float> right)
{
    if (left.Length != right.Length)
    {
        throw new ArgumentException("Vectorii trebuie să aibă aceeași dimensiune.");
    }

    double dotProduct = 0;
    double leftNorm = 0;
    double rightNorm = 0;
    for (var index = 0; index < left.Length; index++)
    {
        dotProduct += left[index] * right[index];
        leftNorm += left[index] * left[index];
        rightNorm += right[index] * right[index];
    }

    return dotProduct / (Math.Sqrt(leftNorm) * Math.Sqrt(rightNorm));
}

static IEnumerable<BookVector> ReadCatalog(string path)
{
    foreach (var line in File.ReadLines(path).Skip(1))
    {
        if (string.IsNullOrWhiteSpace(line))
        {
            continue;
        }

        var fields = ParseCsvLine(line);
        if (fields.Count != 8)
        {
            throw new InvalidDataException($"Linie CSV invalidă (se așteptau 8 câmpuri): {line}");
        }

        var semanticText = $"{fields[1]}, de {fields[2]} {fields[3]} ({fields[4]}). {fields[7]}";
        yield return new BookVector
        {
            Id = int.Parse(fields[0], CultureInfo.InvariantCulture),
            Title = fields[1],
            Description = fields[7],
            Text = semanticText
        };
    }
}

static List<string> ParseCsvLine(string line)
{
    var fields = new List<string>();
    var current = new System.Text.StringBuilder();
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

internal sealed class BookVector
{
    [VectorStoreKey]
    public int Id { get; set; }

    [VectorStoreData]
    public string Title { get; set; } = "";

    [VectorStoreData]
    public string Description { get; set; } = "";

    [VectorStoreVector(768, DistanceFunction = DistanceFunction.CosineSimilarity)]
    public string Text { get; set; } = "";
}
