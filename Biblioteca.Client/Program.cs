// Client consola pentru serviciul gRPC al Bibliotecii
using System.Diagnostics;
using System.Text;
using BibliotecaGrpc;
using Grpc.Core;
using Grpc.Net.Client;

// Adresa HTTPS a aplicatiei Biblioteca (Properties/launchSettings.json, profilul https).
// gRPC are nevoie de HTTP/2: folositi adresa https, nu pe cea http.
const string Address = "https://localhost:7105";
const int ReaderId = 5;

using var channel = GrpcChannel.ForAddress(Address);
var client = new Recommender.RecommenderClient(channel);

while (true)
{
    Console.WriteLine();
    Console.WriteLine("1 Unary · 2 Server streaming · 3 Client streaming · " +
                      "4 Cronometrul · 5 Deadline · 0 Ieșire");
    Console.Write("> ");
    var choice = Console.ReadLine();
    try
    {
        switch (choice)
        {
            case "1": await Unary(); break;
            case "2": await ServerStreaming(); break;
            case "3": await ClientStreaming(); break;
            case "4": await Stopwatches(100); break;
            case "5": await Deadline(); break;
            case "0": return;
        }
    }
    catch (RpcException ex)
    {
        Console.WriteLine($"RpcException: {ex.StatusCode} · {ex.Status.Detail}");
    }
}

async Task Unary()
{
    var reply = await client.RecommendAsync(new RecommendRequest { ReaderId = ReaderId, Top = 3 });
    foreach (var b in reply.Books)
        Console.WriteLine($"{b.BookId,3}  {b.Title,-40} {b.Score,5:0.00} {(b.Fallback ? "(rezervă)" : "")}");
}

async Task ServerStreaming()
{
    using var call = client.StreamRecommendations(new RecommendRequest { ReaderId = ReaderId });
    await foreach (var b in call.ResponseStream.ReadAllAsync())
        Console.WriteLine($"[{DateTime.Now:HH:mm:ss.fff}] {b.Title,-40} {b.Score,5:0.00}");
}

async Task ClientStreaming()
{
   
    int me = 5;
    var ratings = new[] { (3, 5), (12, 4), (25, 2), (7, 9) };   

    using var call = client.RateBooks();
    foreach (var (book, stars) in ratings)
        await call.RequestStream.WriteAsync(
            new RatingInput { ReaderId = me, BookId = book, Stars = stars });
    await call.RequestStream.CompleteAsync();     

    RateSummary s = await call.ResponseAsync;
    Console.WriteLine($"Salvate: {s.Saved} · Respinse: {s.Rejected} · Media: {s.Average:0.00}");
}



// Opțiunile 4 și 5 vin la următoarea întâlnire; deocamdată doar compilează.
Task Stopwatches(int n)
{
    Console.WriteLine("Neimplementat (următoarea întâlnire)");
    return Task.CompletedTask;
}

Task Deadline()
{
    Console.WriteLine("Neimplementat (următoarea întâlnire)");
    return Task.CompletedTask;
}
