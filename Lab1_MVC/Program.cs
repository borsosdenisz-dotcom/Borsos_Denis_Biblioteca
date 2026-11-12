using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Lab1_MVC.Data;
using Microsoft.Extensions.ML;
using Lab1_MVC.Models;
using Lab1_MVC.Api;
using Lab1_MVC.Services;
using Lab1_MVC.Rag;
using Microsoft.Extensions.AI;
using OllamaSharp;
using OpenAI;
using System.ClientModel;

var builder = WebApplication.CreateBuilder(args);
builder.Services.AddDbContext<LibraryContext>(options =>
    options.UseSqlite(builder.Configuration.GetConnectionString("LibraryContext") ?? throw new InvalidOperationException("Connection string 'LibraryContext' not found.")));

// Add services to the container.
builder.Services.AddControllersWithViews();

builder.Services
    .AddPredictionEnginePool<BookRating, BookRatingPrediction>()
    .FromFile(modelName: "BookRecommender",
        filePath: "MLModels/BookRecommender.zip",
        watchForChanges: true);

builder.Services.AddPredictionEnginePool<BookText, GenrePrediction>()
    .FromFile(modelName: "GenreClassifier",
        filePath: "MLModels/GenreClassifier.zip", watchForChanges: true);

builder.Services.AddScoped<RecommenderTrainer>();
builder.Services.AddGrpc();
builder.Services.AddScoped<RecommendationService>();
builder.Services.AddMcpServer()
    .WithHttpTransport()
    .WithToolsFromAssembly();
var ollamaEndpoint = builder.Configuration["Ollama:Endpoint"]
    ?? "http://localhost:11434";
var embeddingModel = builder.Configuration["Ollama:EmbeddingModel"]
    ?? "embeddinggemma";
builder.Services.AddSingleton<IEmbeddingGenerator<string, Embedding<float>>>(
    new OllamaApiClient(new Uri(ollamaEndpoint), embeddingModel));
builder.Services.AddSingleton<DocumentIndex>();

builder.Services.AddValidation();
builder.Services.AddOpenApi();

var githubModelsToken = builder.Configuration["GitHubModels:Token"];
if (!string.IsNullOrWhiteSpace(githubModelsToken))
{
    IChatClient chat = new OpenAIClient(
            new ApiKeyCredential(githubModelsToken),
            new OpenAIClientOptions { Endpoint = new Uri("https://models.github.ai/inference") })
        .GetChatClient("openai/gpt-4o-mini")
        .AsIChatClient();
    builder.Services.AddChatClient(chat);
}

var app = builder.Build();
app.MapGrpcService<RecommenderGrpcService>();
app.MapGrpcService<ReaderRatingsGrpcService>();
app.MapMcp("/mcp");
app.MapLibraryApi();

app.MapGet("/api/v2/readers/{id:int}/recommendations",
    async (int id, int? top, LibraryContext db, RecommendationService service) =>
{
    // Î6: aceeași logică (RecommendationService) pentru REST și gRPC; top lipsă => 5
    var recs = (await service.RecommendAsync(id, top ?? 5))
        .Select(r => new RecommendationDto(r.BookId, r.Title, r.Score, r.Fallback))
        .ToList();

    // S1: jurnalul recomandărilor - salvăm fiecare apel, inclusiv cold start
    db.RecommendationLog.Add(new RecommendationLog
    {
        ReaderId = id,
        CreatedAt = DateTime.UtcNow,
        BookIds = string.Join(",", recs.Select(r => r.BookId))
    });
    await db.SaveChangesAsync();

    return TypedResults.Ok(recs);
})
.WithTags("Recomandări");

if (!string.IsNullOrWhiteSpace(githubModelsToken))
{
    app.MapPost("/api/v2/books/{id:int}/pitch", async (int id, LibraryContext db, IChatClient ai) =>
    {
        var b = await db.Book.Include(x => x.Author).Include(x => x.Genre)
            .FirstOrDefaultAsync(x => x.ID == id);
        if (b is null) return Results.NotFound();

        var r = await ai.GetResponseAsync(
        [
            new ChatMessage(ChatRole.System, "Ești bibliotecar. Răspunde în română, în cel mult 2 propoziții."),
            new ChatMessage(ChatRole.User, $"Recomandă „{b.Title}” de {b.Author?.FullName} ({b.Genre?.Name}). Descriere: {b.Description}")
        ]);
        return Results.Ok(new { b.Title, Pitch = r.Text });
    });

    app.MapGet("/api/v2/ask", async (string q, IChatClient ai) =>
        (await ai.GetResponseAsync(q)).Text);
}
else
{
    const string missingToken =
        "Funcția necesită GitHubModels:Token. Configurați-l prin User Secrets; celelalte funcții ale aplicației rămân disponibile.";
    app.MapPost("/api/v2/books/{id:int}/pitch", (int id) =>
        Results.Problem(missingToken, statusCode: StatusCodes.Status503ServiceUnavailable));
    app.MapGet("/api/v2/ask", (string q) =>
        Results.Problem(missingToken, statusCode: StatusCodes.Status503ServiceUnavailable));
}

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
    try
    {
        DevelopmentEndpoints.MapScalar(app);
    }
    catch (FileLoadException exception)
    {
        app.Logger.LogWarning(
            exception,
            "Interfața Scalar nu poate fi încărcată din cauza politicii Windows Application Control. Endpoint-ul OpenAPI rămâne disponibil.");
    }
}

using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<LibraryContext>();
    db.Database.Migrate();
    LibrarySeed.Seed(db);

    var index = scope.ServiceProvider.GetRequiredService<DocumentIndex>();
    await index.BuildAsync(db,
        Path.Combine(app.Environment.ContentRootPath, "Docs", "regulament.md"),
        Path.Combine(AppContext.BaseDirectory, "Data", "books.csv"));
}

// Configure the HTTP request pipeline.
if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Home/Error");
    // The default HSTS value is 30 days. You may want to change this for production scenarios, see https://aka.ms/aspnetcore-hsts.
    app.UseHsts();
}

app.UseHttpsRedirection();
app.UseStaticFiles();

app.UseRouting();

app.UseAuthorization();

app.MapControllerRoute(
    name: "start",
    pattern: "start",
    defaults: new { controller = "Home", action = "Index" });

app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Home}/{action=Index}/{id?}");

app.Run();
