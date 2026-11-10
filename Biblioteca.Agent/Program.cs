// Biblioteca.Agent 
// 1 = inspector (fara model de limbaj): ce vede clientul MCP
// 2 = agentul: Ollama + tool-urile MCP ale Bibliotecii
using System.Text.Json;
using Microsoft.Extensions.AI;
using ModelContextProtocol.Client;
using ModelContextProtocol.Protocol;
using OpenAI;
using System.ClientModel;

// Portul https al aplicației Biblioteca (launchSettings.json)
const string McpEndpoint = "https://localhost:7105/mcp";
const string OllamaEndpoint = "http://localhost:11434/v1";
const string Model = "llama3.2";

var transport = new HttpClientTransport(new HttpClientTransportOptions
{
    Endpoint = new Uri(McpEndpoint)
});
await using McpClient mcp = await McpClient.CreateAsync(transport);
IList<McpClientTool> tools = await mcp.ListToolsAsync();

Console.Write("1 Inspector ; 2 Agent > ");
if (Console.ReadLine() == "1") await Inspector(); else await Agent();

//  Misiunea 1 (Inspectorul) 
async Task Inspector()
{
    foreach (var t in tools)
    {
        Console.WriteLine($"\n== {t.Name}\n{t.Description}");
        Console.WriteLine(JsonSerializer.Serialize(t.JsonSchema,
            new JsonSerializerOptions { WriteIndented = true }));
    }

    Console.Write("\nCautati in catalog (search_books): ");
    var query = Console.ReadLine() ?? "";
    CallToolResult result = await mcp.CallToolAsync("search_books",
        new Dictionary<string, object?> { ["query"] = query });

    Console.WriteLine($"IsError: {result.IsError}");
    foreach (var block in result.Content.OfType<TextContentBlock>())
        Console.WriteLine(block.Text);
}

//  Misiunea 2  Agentul
async Task Agent()
{
    IChatClient chat = new ChatClientBuilder(
            new OpenAIClient(new ApiKeyCredential("ollama"),
                             new OpenAIClientOptions { Endpoint = new Uri(OllamaEndpoint) })
                .GetChatClient(Model)
                .AsIChatClient())
        .UseFunctionInvocation()        // execută automat tool-urile cerute de model
        .Build();

    List<ChatMessage> messages =
    [
        new(ChatRole.System,
            "Ești asistentul Bibliotecii FSEGA. Răspunzi în limba română. " +
            "Pentru orice informație despre cărți, cititori sau recomandări folosește tool-urile. " +
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
        ChatResponse response = await chat.GetResponseAsync(messages, options);
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
