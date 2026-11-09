// Services/RecommenderGrpcService.cs
// Implementarea serviciului din recommender.proto.
// Recommender.RecommenderBase este generata la build din fisierul .proto (folderul obj).
using BibliotecaGrpc;
using Lab1_MVC.Data;
using Lab1_MVC.Models;
using Grpc.Core;

namespace Lab1_MVC.Services;

public class RecommenderGrpcService(
    RecommendationService recs,
    LibraryContext db,
    ILogger<RecommenderGrpcService> logger) : Recommender.RecommenderBase
{
   
    public override async Task<RecommendReply> Recommend(
        RecommendRequest request, ServerCallContext context)
    {
        var list = await recs.RecommendAsync(
            request.ReaderId, request.Top, context.CancellationToken);

        var reply = new RecommendReply();
        reply.Books.AddRange(list.Select(ToMessage));   
        return reply;
    }

    
    public override async Task StreamRecommendations(
        RecommendRequest request,
        IServerStreamWriter<BookScore> responseStream,
        ServerCallContext context)
    {
        var list = await recs.RecommendAsync(
            request.ReaderId, request.Top, context.CancellationToken);

        foreach (var r in list)
        {
            await responseStream.WriteAsync(ToMessage(r));
            // simulăm un calcul lent (de ex. un model mai mare)
            await Task.Delay(500, context.CancellationToken);
        }
    }

  
    public override async Task<RateSummary> RateBooks(
        IAsyncStreamReader<RatingInput> requestStream, ServerCallContext context)
    {
        int saved = 0, rejected = 0;
        double sum = 0;

        await foreach (var r in requestStream.ReadAllAsync(context.CancellationToken))
        {
            if (r.Stars is < 1 or > 5) { rejected++; continue; }

            db.Rating.Add(new Rating
            {
                ReaderId = r.ReaderId,
                BookID = r.BookId,
                Stars = r.Stars,
                CreatedAt = DateTime.UtcNow
            });
            saved++;
            sum += r.Stars;
        }

        await db.SaveChangesAsync(context.CancellationToken);
        logger.LogInformation("RateBooks: {Saved} salvate, {Rejected} respinse", saved, rejected);

        return new RateSummary
        {
            Saved = saved,
            Rejected = rejected,
            Average = saved == 0 ? 0 : sum / saved
        };
    }

    private static BookScore ToMessage(Recommendation r) => new()
    {
        BookId = r.BookId,
        Title = r.Title,
        Score = r.Score,
        Fallback = r.Fallback
    };
}
