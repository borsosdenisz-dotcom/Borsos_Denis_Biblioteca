// Biblioteca.Agent 
// 1 = inspector (fara model de limbaj): ce vede clientul MCP
// 2 = agentul: Ollama + tool-urile MCP ale Bibliotecii
// 3 = cautare semantica directa in regulament si descrierile cartilor
using System.Text.Json;
using System.Text.RegularExpressions;
using Microsoft.Extensions.AI;
using ModelContextProtocol.Client;
using ModelContextProtocol.Protocol;
using OpenAI;
using System.ClientModel;

// Valorile implicite corespund launchSettings.json și instalării locale Ollama.
var mcpEndpoint = Environment.GetEnvironmentVariable("BIBLIOTECA_MCP_ENDPOINT")
    ?? "https://localhost:7105/mcp";
var ollamaEndpoint = Environment.GetEnvironmentVariable("OLLAMA_OPENAI_ENDPOINT")
    ?? "http://localhost:11434/v1";
var model = Environment.GetEnvironmentVariable("OLLAMA_CHAT_MODEL")
    ?? "llama3.2";

var transport = new HttpClientTransport(new HttpClientTransportOptions
{
    Endpoint = new Uri(mcpEndpoint)
});

McpClient mcp;
IList<McpClientTool> tools;
try
{
    mcp = await McpClient.CreateAsync(transport);
    tools = await mcp.ListToolsAsync();
}
catch (Exception exception)
{
    Console.Error.WriteLine($"Nu se poate realiza conexiunea la serverul MCP ({mcpEndpoint}).");
    Console.Error.WriteLine(exception.Message);
    Console.Error.WriteLine("Porniți mai întâi aplicația Lab1_MVC cu profilul https.");
    return;
}

await using var mcpLifetime = mcp;

Console.Write("1 Inspector ; 2 Agent ; 3 Căutare în documente > ");
switch (Console.ReadLine())
{
    case "1": await Inspector(); break;
    case "3": await CautareDocumente(); break;
    default: await Agent(); break;
}

//  Misiunea 1 (Inspectorul) 
async Task Inspector()
{
    foreach (var t in tools)
    {
        Console.WriteLine($"\n== {t.Name}\n{t.Description}");
        Console.WriteLine(JsonSerializer.Serialize(t.JsonSchema,
            new JsonSerializerOptions { WriteIndented = true }));
    }

    Console.Write("\nAnul de început pentru books_by_year: ");
    var startYearText = Console.ReadLine();
    Console.Write("Anul de sfârșit: ");
    var endYearText = Console.ReadLine();
    if (!int.TryParse(startYearText, out var startYear)
        || !int.TryParse(endYearText, out var endYear))
    {
        Console.Error.WriteLine("Introduceți doi ani întregi.");
        return;
    }

    Console.WriteLine($"  [tool] books_by_year(startYear={startYear}, endYear={endYear})");
    CallToolResult result = await mcp.CallToolAsync("books_by_year",
        new Dictionary<string, object?>
        {
            ["startYear"] = startYear,
            ["endYear"] = endYear
        });

    Console.WriteLine($"IsError: {result.IsError}");
    foreach (var block in result.Content.OfType<TextContentBlock>())
        Console.WriteLine(block.Text);
}

//  Misiunea 2  Agentul
async Task Agent()
{
    IChatClient chat = new ChatClientBuilder(
            new OpenAIClient(new ApiKeyCredential("ollama"),
                             new OpenAIClientOptions { Endpoint = new Uri(ollamaEndpoint) })
                .GetChatClient(model)
                .AsIChatClient())
        .UseFunctionInvocation()        // execută automat tool-urile cerute de model
        .Build();

    List<ChatMessage> messages =
    [
        new(ChatRole.System,
            "Ești asistentul Bibliotecii FSEGA. Răspunzi în limba română. " +
            "Pentru orice informație despre cărți, cititori sau recomandări folosește tool-urile. " +
            "Pentru cărți publicate într-un an sau într-un interval de ani folosește books_by_year. " +
            "Pentru regulament și program folosește search_documents cu source='regulament'. " +
            "Pentru o carte descrisă prin subiect folosește search_documents cu source='carte', apoi get_book_details pentru preț. " +
            "La reguli, include în răspuns limitele, durata și condițiile relevante din fragment. " +
            "Pentru get_book_details trimite exclusiv bookId numeric extras din titlul rezultatului, de forma '(ID 1)'. " +
            "La search_documents, source poate fi doar 'regulament' sau 'carte'; pentru ambele surse omite argumentul. " +
            "Nu inventa titluri, prețuri sau autori: dacă tool-urile nu întorc informația, spune că nu o ai.")
    ];
    var options = new ChatOptions { Tools = [.. tools] };

    Console.WriteLine($"Tool-uri disponibile: {string.Join(", ", tools.Select(t => t.Name))}");
    while (true)
    {
        Console.Write("\nTu: ");
        var question = Console.ReadLine();
        if (string.IsNullOrWhiteSpace(question)) break;

        messages.Add(new(ChatRole.User, question));
        var routedAnswer = await TryAnswerBookPriceAsync(question);
        if (routedAnswer is not null)
        {
            messages.Add(new ChatMessage(ChatRole.Assistant, routedAnswer));
            Console.WriteLine($"Asistent: {routedAnswer}");
            continue;
        }

        ChatResponse response;
        try
        {
            response = await chat.GetResponseAsync(messages, options);
        }
        catch (Exception exception)
        {
            Console.Error.WriteLine($"Ollama nu a putut răspunde prin {ollamaEndpoint} cu modelul {model}: {exception.Message}");
            Console.Error.WriteLine($"Verificați serviciul și rulați: ollama pull {model}");
            continue;
        }
        messages.AddMessages(response);

        // urma agentului: ce tool-uri a apelat si cu ce argumente
        foreach (var call in response.Messages.SelectMany(m => m.Contents).OfType<FunctionCallContent>())
        {
            var args = string.Join(", ", (call.Arguments ?? new Dictionary<string, object?>())
                .Select(a => $"{a.Key}={a.Value}"));
            Console.WriteLine($"  [tool] {call.Name}({args})");
        }
        Console.WriteLine($"Asistent: {response.Text}");
    }
}

// llama3.2 poate confunda schema celui de-al doilea tool într-un lanț.
// Pentru întrebările despre preț orchestrăm explicit căutarea după titlu sau
// descrierea semantică, urmată de get_book_details.
async Task<string?> TryAnswerBookPriceAsync(string question)
{
    var asksForPrice = question.Contains("cost", StringComparison.OrdinalIgnoreCase)
        || question.Contains("preț", StringComparison.OrdinalIgnoreCase)
        || question.Contains("pret", StringComparison.OrdinalIgnoreCase);
    var describesSubject = question.Contains("despre", StringComparison.OrdinalIgnoreCase)
        || question.Contains("caut", StringComparison.OrdinalIgnoreCase);
    if (!asksForPrice)
        return null;

    try
    {
        int bookId;
        if (describesSubject)
        {
            Console.WriteLine($"  [tool] search_documents(query={question}, source=carte, top=3)");
            var search = await mcp.CallToolAsync("search_documents", new Dictionary<string, object?>
            {
                ["query"] = question,
                ["source"] = "carte",
                ["top"] = 3
            });
            if (search.IsError == true)
                return null;

            var searchJson = search.Content.OfType<TextContentBlock>().FirstOrDefault()?.Text;
            if (string.IsNullOrWhiteSpace(searchJson))
                return null;

            using var document = JsonDocument.Parse(searchJson);
            if (document.RootElement.ValueKind != JsonValueKind.Array
                || document.RootElement.GetArrayLength() == 0)
                return null;

            var titleWithId = GetProperty(document.RootElement[0], "title").GetString();
            var idMatch = Regex.Match(titleWithId ?? "", @"\(ID\s+(?<id>\d+)\)");
            if (!idMatch.Success || !int.TryParse(idMatch.Groups["id"].Value, out bookId))
                return null;
        }
        else
        {
            var titleQuery = Regex.Replace(
                    question,
                    @"(?i)\b(cât|cat)\s+(costă|costa)\b",
                    "")
                .Trim(' ', '?', '.', '!', '"', '„', '”');
            if (string.IsNullOrWhiteSpace(titleQuery))
                return null;

            Console.WriteLine($"  [tool] search_books(query={titleQuery})");
            var search = await mcp.CallToolAsync("search_books", new Dictionary<string, object?>
            {
                ["query"] = titleQuery
            });
            if (search.IsError == true)
                return null;

            var searchJson = search.Content.OfType<TextContentBlock>().FirstOrDefault()?.Text;
            if (string.IsNullOrWhiteSpace(searchJson))
                return null;

            using var document = JsonDocument.Parse(searchJson);
            if (document.RootElement.ValueKind != JsonValueKind.Array
                || document.RootElement.GetArrayLength() == 0)
                return null;

            bookId = GetProperty(document.RootElement[0], "id").GetInt32();
        }

        Console.WriteLine($"  [tool] get_book_details(bookId={bookId})");
        var details = await mcp.CallToolAsync("get_book_details", new Dictionary<string, object?>
        {
            ["bookId"] = bookId
        });
        if (details.IsError == true)
            return null;

        var detailsJson = details.Content.OfType<TextContentBlock>().FirstOrDefault()?.Text;
        if (string.IsNullOrWhiteSpace(detailsJson))
            return null;

        using var detailsDocument = JsonDocument.Parse(detailsJson);
        var title = GetProperty(detailsDocument.RootElement, "title").GetString();
        var price = GetProperty(detailsDocument.RootElement, "price").GetDecimal();
        return $"Cartea este „{title}” și costă {price:0.##} lei.";
    }
    catch (Exception exception)
    {
        Console.Error.WriteLine($"Lanțul semantic carte-preț a eșuat: {exception.Message}");
        return null;
    }
}

//  Misiunea 2/3 - rezultatele brute ale tool-ului RAG, fara model de limbaj
async Task CautareDocumente()
{
    while (true)
    {
        Console.Write("\nÎntrebare (Enter = ieșire): ");
        var query = Console.ReadLine();
        if (string.IsNullOrWhiteSpace(query)) break;

        Console.Write("Sursa (regulament/carte/Enter = ambele): ");
        var source = Console.ReadLine();
        Console.Write("Top (1-8, implicit 3): ");
        var topText = Console.ReadLine();
        var top = int.TryParse(topText, out var parsedTop) ? Math.Clamp(parsedTop, 1, 8) : 3;

        var arguments = new Dictionary<string, object?>
        {
            ["query"] = query,
            ["top"] = top
        };
        if (!string.IsNullOrWhiteSpace(source)) arguments["source"] = source.Trim();

        try
        {
            CallToolResult result = await mcp.CallToolAsync("search_documents", arguments);
            if (result.IsError == true)
            {
                foreach (var block in result.Content.OfType<TextContentBlock>())
                    Console.Error.WriteLine($"Eroare MCP: {block.Text}");
                continue;
            }

            foreach (var block in result.Content.OfType<TextContentBlock>())
                PrintDocumentHits(block.Text);
        }
        catch (Exception exception)
        {
            Console.Error.WriteLine($"Apelul search_documents a eșuat: {exception.Message}");
        }
    }
}

static void PrintDocumentHits(string json)
{
    try
    {
        using var document = JsonDocument.Parse(json);
        var position = 1;
        foreach (var hit in document.RootElement.EnumerateArray())
        {
            var score = GetProperty(hit, "score").GetDouble();
            var source = GetProperty(hit, "source").GetString();
            var title = GetProperty(hit, "title").GetString();
            var text = GetProperty(hit, "text").GetString();
            Console.WriteLine($"\n{position++}. [{score:F3}] {source} · {title}");
            Console.WriteLine($"   {text}");
        }
    }
    catch (JsonException)
    {
        Console.WriteLine(json);
    }
}

static JsonElement GetProperty(JsonElement element, string name) =>
    element.EnumerateObject()
        .First(property => string.Equals(property.Name, name, StringComparison.OrdinalIgnoreCase))
        .Value;
